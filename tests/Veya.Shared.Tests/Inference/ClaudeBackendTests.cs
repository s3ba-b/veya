using System.Net;
using System.Net.Http;
using System.Text.Json;
using Veya.Shared.Inference;
using Veya.TestSupport;
using Xunit;

namespace Veya.Shared.Tests.Inference;

public class ClaudeBackendTests
{
    private static ClaudeBackend CreateBackend(
        HttpMessageHandler handler,
        string? apiKey = "sk-test-123",
        string model = "claude-sonnet-4-6") =>
        new(new FakeApiKeyProvider(apiKey), model, new HttpClient(handler));

    [Fact]
    public async Task CompleteAsync_ThrowsBackendUnavailableWhenNoApiKey()
    {
        var backend = CreateBackend(new CapturingHttpMessageHandler("{}"), apiKey: null);

        var request = new InferenceRequest(
            SystemPrompt: null,
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("hi")])],
            Tools: []);

        var ex = await Assert.ThrowsAsync<BackendUnavailableException>(() => backend.CompleteAsync(request));
        Assert.Contains("ANTHROPIC_API_KEY", ex.Message);
    }

    [Fact]
    public async Task CompleteAsync_MapsTextResponse()
    {
        const string responseJson = """
        {
          "id": "msg_123",
          "type": "message",
          "role": "assistant",
          "model": "claude-sonnet-4-6",
          "content": [{"type": "text", "text": "Hello there."}],
          "stop_reason": "end_turn",
          "stop_sequence": null,
          "usage": {"input_tokens": 12, "output_tokens": 7}
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
    public async Task CompleteAsync_MapsToolUseResponse()
    {
        const string responseJson = """
        {
          "id": "msg_456",
          "type": "message",
          "role": "assistant",
          "model": "claude-sonnet-4-6",
          "content": [{"type": "tool_use", "id": "toolu_1", "name": "get_disk_usage", "input": {"path": "/"}}],
          "stop_reason": "tool_use",
          "stop_sequence": null,
          "usage": {"input_tokens": 20, "output_tokens": 8}
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
        Assert.Equal("toolu_1", toolUse.Id);
        Assert.Equal("get_disk_usage", toolUse.Name);
        Assert.Equal("/", toolUse.Input.GetProperty("path").GetString());
    }

    [Fact]
    public async Task CompleteAsync_SendsApiKeyHeaderModelSystemPromptAndTools()
    {
        const string responseJson = """
        {
          "id": "msg_789",
          "type": "message",
          "role": "assistant",
          "model": "claude-sonnet-4-6",
          "content": [{"type": "text", "text": "ok"}],
          "stop_reason": "end_turn",
          "stop_sequence": null,
          "usage": {"input_tokens": 1, "output_tokens": 1}
        }
        """;

        var handler = new CapturingHttpMessageHandler(responseJson);
        var backend = CreateBackend(handler, apiKey: "sk-secret", model: "claude-opus-4-8");

        var schema = JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement;
        var request = new InferenceRequest(
            SystemPrompt: "You are Veya.",
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("hi")])],
            Tools: [new ToolDefinition("ping", "Pings.", schema)]);

        await backend.CompleteAsync(request);

        var sentRequest = handler.LastRequest!;
        Assert.Equal("sk-secret", Assert.Single(sentRequest.Headers.GetValues("x-api-key")));
        Assert.EndsWith("/v1/messages", sentRequest.RequestUri!.AbsolutePath);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;
        Assert.Equal("claude-opus-4-8", root.GetProperty("model").GetString());
        Assert.Equal("You are Veya.", root.GetProperty("system").GetString());

        var messages = root.GetProperty("messages");
        Assert.Equal("user", messages[0].GetProperty("role").GetString());

        var tools = root.GetProperty("tools");
        Assert.Equal("ping", tools[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task CompleteAsync_SendsToolUseAndToolResultBackToApi()
    {
        const string responseJson = """
        {
          "id": "msg_999",
          "type": "message",
          "role": "assistant",
          "model": "claude-sonnet-4-6",
          "content": [{"type": "text", "text": "Your disk is 42% full."}],
          "stop_reason": "end_turn",
          "stop_sequence": null,
          "usage": {"input_tokens": 1, "output_tokens": 1}
        }
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

        var assistantContent = messages[1].GetProperty("content");
        Assert.Equal("tool_use", assistantContent[0].GetProperty("type").GetString());
        Assert.Equal("call_1", assistantContent[0].GetProperty("id").GetString());
        Assert.Equal("get_disk_usage", assistantContent[0].GetProperty("name").GetString());

        var toolResultContent = messages[2].GetProperty("content");
        Assert.Equal("tool_result", toolResultContent[0].GetProperty("type").GetString());
        Assert.Equal("call_1", toolResultContent[0].GetProperty("tool_use_id").GetString());
        Assert.Equal("42% used", toolResultContent[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task CompleteAsync_ThrowsBackendUnavailable_OnErrorStatusCode()
    {
        var backend = CreateBackend(new CapturingHttpMessageHandler(
            """{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}""",
            HttpStatusCode.Unauthorized));

        var request = new InferenceRequest(
            SystemPrompt: null,
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("hi")])],
            Tools: []);

        var ex = await Assert.ThrowsAsync<BackendUnavailableException>(() => backend.CompleteAsync(request));
        Assert.Contains("Claude", ex.Message);
    }

    [Fact]
    public async Task CompleteAsync_ThrowsBackendUnavailable_WhenClaudeUnreachable()
    {
        var backend = CreateBackend(new ThrowingHttpMessageHandler());

        var request = new InferenceRequest(
            SystemPrompt: null,
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("hi")])],
            Tools: []);

        await Assert.ThrowsAsync<BackendUnavailableException>(() => backend.CompleteAsync(request));
    }
}
