using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Veya.Shared.Inference;
using Xunit;

namespace Veya.Shared.Tests.Inference;

public class ClaudeBackendTests
{
    private sealed class FakeApiKeyProvider(string? apiKey) : IApiKeyProvider
    {
        public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(apiKey);
    }

    private sealed class FakeHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });
    }

    [Fact]
    public async Task CompleteAsync_ThrowsBackendUnavailableWhenNoApiKey()
    {
        var backend = new ClaudeBackend(new FakeApiKeyProvider(null), "claude-sonnet-4-6");

        var request = new InferenceRequest(
            SystemPrompt: null,
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("hi")])],
            Tools: []);

        await Assert.ThrowsAsync<BackendUnavailableException>(() => backend.CompleteAsync(request));
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

        using var httpClient = new HttpClient(new FakeHttpMessageHandler(responseJson));
        var backend = new ClaudeBackend(new FakeApiKeyProvider("sk-test-123"), "claude-sonnet-4-6", httpClient);

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

        using var httpClient = new HttpClient(new FakeHttpMessageHandler(responseJson));
        var backend = new ClaudeBackend(new FakeApiKeyProvider("sk-test-123"), "claude-sonnet-4-6", httpClient);

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
}
