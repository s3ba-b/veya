using System.Text.Json;
using Sage.Shared.Inference;
using Xunit;

namespace Sage.Shared.Tests.Inference;

public class ToolUseLoopRunnerTests
{
    private sealed class ScriptedBackend : IInferenceBackend
    {
        private readonly Queue<InferenceResponse> _responses;

        public ScriptedBackend(IEnumerable<InferenceResponse> responses) => _responses = new Queue<InferenceResponse>(responses);

        public List<InferenceRequest> Requests { get; } = [];

        public Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class FakeToolExecutor : IToolExecutor
    {
        public List<(string Tool, JsonElement Input)> Calls { get; } = [];

        public Task<string> ExecuteAsync(string toolName, JsonElement input, CancellationToken cancellationToken = default)
        {
            Calls.Add((toolName, input));
            return Task.FromResult($"result-for-{toolName}");
        }
    }

    [Fact]
    public async Task RunAsync_ExecutesToolAndReturnsFinalResponse()
    {
        var toolInput = JsonDocument.Parse("""{"path":"/"}""").RootElement;
        var toolUseResponse = new InferenceResponse(
            new ChatMessage(ChatRole.Assistant, [new ToolUseBlock("call_1", "get_disk_usage", toolInput)]),
            StopReason: "tool_use",
            InputTokens: 10,
            OutputTokens: 5);

        var finalResponse = new InferenceResponse(
            new ChatMessage(ChatRole.Assistant, [new TextBlock("Disk usage is 42%.")]),
            StopReason: "end_turn",
            InputTokens: 20,
            OutputTokens: 8);

        var backend = new ScriptedBackend([toolUseResponse, finalResponse]);
        var toolExecutor = new FakeToolExecutor();

        var request = new InferenceRequest(
            SystemPrompt: "You are Sage.",
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("How full is my disk?")])],
            Tools: []);

        var result = await ToolUseLoopRunner.RunAsync(backend, request, toolExecutor);

        Assert.Equal("end_turn", result.StopReason);
        Assert.Equal("Disk usage is 42%.", Assert.IsType<TextBlock>(result.Message.Content[0]).Text);

        var call = Assert.Single(toolExecutor.Calls);
        Assert.Equal("get_disk_usage", call.Tool);
        Assert.Equal("/", call.Input.GetProperty("path").GetString());

        Assert.Equal(2, backend.Requests.Count);
        var toolResultMessage = backend.Requests[1].Messages[^1];
        Assert.Equal(ChatRole.User, toolResultMessage.Role);
        var toolResult = Assert.IsType<ToolResultBlock>(toolResultMessage.Content[0]);
        Assert.Equal("call_1", toolResult.ToolUseId);
        Assert.Equal("result-for-get_disk_usage", toolResult.Content);
    }

    [Fact]
    public async Task RunAsync_ThrowsWhenMaxIterationsExceeded()
    {
        var toolInput = JsonDocument.Parse("{}").RootElement;
        InferenceResponse LoopingResponse() => new(
            new ChatMessage(ChatRole.Assistant, [new ToolUseBlock("call_x", "noop", toolInput)]),
            StopReason: "tool_use",
            InputTokens: 1,
            OutputTokens: 1);

        var backend = new ScriptedBackend(Enumerable.Range(0, 10).Select(_ => LoopingResponse()));
        var toolExecutor = new FakeToolExecutor();

        var request = new InferenceRequest(
            SystemPrompt: null,
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("loop")])],
            Tools: []);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ToolUseLoopRunner.RunAsync(backend, request, toolExecutor, maxIterations: 3));
    }
}
