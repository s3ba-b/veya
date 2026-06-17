using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Veya.Shared.Inference;
using Xunit;

namespace Veya.Shared.Tests.Inference;

public class OllamaBackendTests
{
    private sealed class FakeHttpMessageHandler(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connection refused");
    }

    private static OllamaBackend CreateBackend(HttpMessageHandler handler, string model = "llama3.1") =>
        new(new HttpClient(handler), new OllamaOptions { Model = model });

    [Fact]
    public async Task CompleteAsync_MapsTextResponse()
    {
        const string responseJson = """
        {
          "model": "llama3.1",
          "created_at": "2026-06-12T00:00:00Z",
          "message": {"role": "assistant", "content": "Hello there."},
          "done": true,
          "done_reason": "stop",
          "prompt_eval_count": 12,
          "eval_count": 7
        }
        """;

        var backend = CreateBackend(new FakeHttpMessageHandler(responseJson));

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
    public async Task CompleteAsync_MapsToolUseResponseWithSynthesizedId()
    {
        const string responseJson = """
        {
          "message": {
            "role": "assistant",
            "content": "",
            "tool_calls": [{"function": {"name": "get_disk_usage", "arguments": {"path": "/"}}}]
          },
          "done": true,
          "prompt_eval_count": 20,
          "eval_count": 8
        }
        """;

        var backend = CreateBackend(new FakeHttpMessageHandler(responseJson));

        var schema = JsonDocument.Parse("""{"type":"object","properties":{"path":{"type":"string"}}}""").RootElement;
        var request = new InferenceRequest(
            SystemPrompt: null,
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("How full is my disk?")])],
            Tools: [new ToolDefinition("get_disk_usage", "Reports disk usage.", schema)]);

        var response = await backend.CompleteAsync(request);

        Assert.Equal("tool_use", response.StopReason);
        var toolUse = Assert.IsType<ToolUseBlock>(response.Message.Content[0]);
        Assert.False(string.IsNullOrEmpty(toolUse.Id));
        Assert.Equal("get_disk_usage", toolUse.Name);
        Assert.Equal("/", toolUse.Input.GetProperty("path").GetString());
    }

    [Fact]
    public async Task CompleteAsync_SendsRequestWithModelToolsAndNoStreaming()
    {
        const string responseJson = """
        {"message": {"role": "assistant", "content": "ok"}, "done": true, "prompt_eval_count": 1, "eval_count": 1}
        """;

        var handler = new FakeHttpMessageHandler(responseJson);
        var backend = CreateBackend(handler, model: "qwen2.5");

        var schema = JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement;
        var request = new InferenceRequest(
            SystemPrompt: "You are Veya.",
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("hi")])],
            Tools: [new ToolDefinition("ping", "Pings.", schema)]);

        await backend.CompleteAsync(request);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;
        Assert.Equal("qwen2.5", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());

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
    public async Task CompleteAsync_SendsToolResultAsToolRoleMessageWithRecoveredName()
    {
        const string responseJson = """
        {"message": {"role": "assistant", "content": "Your disk is 42% full."}, "done": true, "prompt_eval_count": 1, "eval_count": 1}
        """;

        var handler = new FakeHttpMessageHandler(responseJson);
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
        var toolCalls = assistantMessage.GetProperty("tool_calls");
        Assert.Equal("get_disk_usage", toolCalls[0].GetProperty("function").GetProperty("name").GetString());

        var toolMessage = messages[2];
        Assert.Equal("tool", toolMessage.GetProperty("role").GetString());
        Assert.Equal("42% used", toolMessage.GetProperty("content").GetString());
        Assert.Equal("get_disk_usage", toolMessage.GetProperty("tool_name").GetString());
    }

    [Fact]
    public async Task CompleteAsync_ThrowsBackendUnavailable_WhenOllamaUnreachable()
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
        var backend = CreateBackend(new FakeHttpMessageHandler("model not found", HttpStatusCode.NotFound));

        var request = new InferenceRequest(
            SystemPrompt: null,
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("hi")])],
            Tools: []);

        var ex = await Assert.ThrowsAsync<BackendUnavailableException>(() => backend.CompleteAsync(request));
        Assert.Contains("404", ex.Message);
    }
}
