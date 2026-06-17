using System.Net;
using System.Net.Http;
using System.Text.Json;
using Veya.Shared.Inference;
using Veya.TestSupport;
using Xunit;

namespace Veya.Shared.Tests.Inference;

public class MistralBackendTests
{
    private static MistralBackend CreateBackend(
        HttpMessageHandler handler,
        string? apiKey = "mk-test-123",
        string model = "mistral-large-latest") =>
        new(new HttpClient(handler), new FakeApiKeyProvider(apiKey), new MistralOptions { Model = model });

    [Fact]
    public async Task CompleteAsync_MapsTextResponse()
    {
        const string responseJson = """
        {
          "choices": [{"message": {"role": "assistant", "content": "Hello there."}, "finish_reason": "stop"}],
          "usage": {"prompt_tokens": 12, "completion_tokens": 7}
        }
        """;

        var backend = CreateBackend(new CapturingHttpMessageHandler(responseJson));

        var request = new InferenceRequest(
            SystemPrompt: "You are Veya.",
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("hi")])],
            Tools: []);

        var response = await backend.CompleteAsync(request);

        Assert.Equal("end_turn", response.StopReason);
        Assert.Equal(ChatRole.Assistant, response.Message.Role);
        Assert.Equal("Hello there.", Assert.IsType<TextBlock>(response.Message.Content[0]).Text);
        Assert.Equal(12, response.InputTokens);
        Assert.Equal(7, response.OutputTokens);
    }

    [Fact]
    public async Task CompleteAsync_MapsToolUseResponsePreservingMistralId()
    {
        const string responseJson = """
        {
          "choices": [{
            "message": {
              "role": "assistant",
              "content": null,
              "tool_calls": [{"id": "abc123XYZ", "function": {"name": "get_disk_usage", "arguments": "{\"path\": \"/\"}"}}]
            },
            "finish_reason": "tool_calls"
          }],
          "usage": {"prompt_tokens": 20, "completion_tokens": 8}
        }
        """;

        var backend = CreateBackend(new CapturingHttpMessageHandler(responseJson));

        var schema = JsonDocument.Parse("""{"type":"object","properties":{"path":{"type":"string"}}}""").RootElement;
        var request = new InferenceRequest(
            SystemPrompt: null,
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("How full is my disk?")])],
            Tools: [new ToolDefinition("get_disk_usage", "Reports disk usage.", schema)]);

        var response = await backend.CompleteAsync(request);

        Assert.Equal("tool_use", response.StopReason);
        var toolUse = Assert.IsType<ToolUseBlock>(response.Message.Content[0]);
        Assert.Equal("abc123XYZ", toolUse.Id);
        Assert.Equal("get_disk_usage", toolUse.Name);
        Assert.Equal("/", toolUse.Input.GetProperty("path").GetString());
    }

    [Fact]
    public async Task CompleteAsync_SendsBearerKeyModelAndToolsWithAutoChoice()
    {
        const string responseJson = """
        {"choices": [{"message": {"role": "assistant", "content": "ok"}, "finish_reason": "stop"}], "usage": {"prompt_tokens": 1, "completion_tokens": 1}}
        """;

        var handler = new CapturingHttpMessageHandler(responseJson);
        var backend = CreateBackend(handler, apiKey: "mk-secret", model: "mistral-small-latest");

        var schema = JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement;
        var request = new InferenceRequest(
            SystemPrompt: "You are Veya.",
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("hi")])],
            Tools: [new ToolDefinition("ping", "Pings.", schema)]);

        await backend.CompleteAsync(request);

        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("mk-secret", handler.LastRequest!.Headers.Authorization!.Parameter);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;
        Assert.Equal("mistral-small-latest", root.GetProperty("model").GetString());
        Assert.Equal("auto", root.GetProperty("tool_choice").GetString());

        var messages = root.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("You are Veya.", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("hi", messages[1].GetProperty("content").GetString());

        var tools = root.GetProperty("tools");
        Assert.Equal("function", tools[0].GetProperty("type").GetString());
        Assert.Equal("ping", tools[0].GetProperty("function").GetProperty("name").GetString());
    }

    [Fact]
    public async Task CompleteAsync_SerializesAssistantToolCallArgumentsAsJsonStringAndToolResult()
    {
        const string responseJson = """
        {"choices": [{"message": {"role": "assistant", "content": "Your disk is 42% full."}, "finish_reason": "stop"}], "usage": {"prompt_tokens": 1, "completion_tokens": 1}}
        """;

        var handler = new CapturingHttpMessageHandler(responseJson);
        var backend = CreateBackend(handler);

        var input = JsonDocument.Parse("""{"path":"/"}""").RootElement;
        var request = new InferenceRequest(
            SystemPrompt: null,
            Messages:
            [
                new ChatMessage(ChatRole.User, [new TextBlock("How full is my disk?")]),
                new ChatMessage(ChatRole.Assistant, [new ToolUseBlock("call_1", "get_disk_usage", input)]),
                new ChatMessage(ChatRole.User, [new ToolResultBlock("call_1", "42% used")]),
            ],
            Tools: []);

        await backend.CompleteAsync(request);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var messages = body.RootElement.GetProperty("messages");

        var assistantMessage = messages[1];
        Assert.Equal("assistant", assistantMessage.GetProperty("role").GetString());
        var toolCall = assistantMessage.GetProperty("tool_calls")[0];
        Assert.Equal("call_1", toolCall.GetProperty("id").GetString());
        Assert.Equal("get_disk_usage", toolCall.GetProperty("function").GetProperty("name").GetString());

        // Mistral requires arguments to be a JSON string, not an object.
        var arguments = toolCall.GetProperty("function").GetProperty("arguments");
        Assert.Equal(JsonValueKind.String, arguments.ValueKind);
        using var parsedArgs = JsonDocument.Parse(arguments.GetString()!);
        Assert.Equal("/", parsedArgs.RootElement.GetProperty("path").GetString());

        var toolMessage = messages[2];
        Assert.Equal("tool", toolMessage.GetProperty("role").GetString());
        Assert.Equal("42% used", toolMessage.GetProperty("content").GetString());
        Assert.Equal("call_1", toolMessage.GetProperty("tool_call_id").GetString());
    }

    [Fact]
    public async Task CompleteAsync_ThrowsBackendUnavailable_WhenNoApiKey()
    {
        var backend = CreateBackend(new CapturingHttpMessageHandler("{}"), apiKey: null);

        var request = new InferenceRequest(
            SystemPrompt: null,
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("hi")])],
            Tools: []);

        var ex = await Assert.ThrowsAsync<BackendUnavailableException>(() => backend.CompleteAsync(request));
        Assert.Contains("MISTRAL_API_KEY", ex.Message);
    }

    [Fact]
    public async Task CompleteAsync_ThrowsBackendUnavailable_WhenMistralUnreachable()
    {
        var backend = CreateBackend(new ThrowingHttpMessageHandler());

        var request = new InferenceRequest(
            SystemPrompt: null,
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("hi")])],
            Tools: []);

        await Assert.ThrowsAsync<BackendUnavailableException>(() => backend.CompleteAsync(request));
    }

    [Fact]
    public async Task CompleteAsync_ThrowsBackendUnavailable_OnErrorStatusCode()
    {
        var backend = CreateBackend(new CapturingHttpMessageHandler("unauthorized", HttpStatusCode.Unauthorized));

        var request = new InferenceRequest(
            SystemPrompt: null,
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("hi")])],
            Tools: []);

        var ex = await Assert.ThrowsAsync<BackendUnavailableException>(() => backend.CompleteAsync(request));
        Assert.Contains("401", ex.Message);
    }
}
