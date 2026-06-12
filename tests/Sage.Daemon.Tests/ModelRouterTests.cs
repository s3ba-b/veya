using System.Text.Json;
using Sage.Daemon.Mcp;
using Sage.Shared.Inference;
using Xunit;

namespace Sage.Daemon.Tests;

public class ModelRouterTests
{
    private sealed class FakeInferenceBackend(InferenceResponse response) : IInferenceBackend
    {
        public Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(response);
    }

    private sealed class ThrowingInferenceBackend : IInferenceBackend
    {
        public Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default) =>
            throw new BackendUnavailableException("no API key configured");
    }

    private sealed class NoToolsGateway : IMcpToolGateway
    {
        public Task<IReadOnlyList<ToolDefinition>> GetToolsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ToolDefinition>>([]);

        public Task<string> ExecuteAsync(string toolName, JsonElement input, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException($"Tool '{toolName}' cannot be executed: no tools are registered.");
    }

    [Fact]
    public async Task AskAsync_ReturnsTextFromFinalResponse()
    {
        var response = new InferenceResponse(
            new ChatMessage(ChatRole.Assistant, [new TextBlock("Your disk is 42% full.")]),
            StopReason: "end_turn",
            InputTokens: 10,
            OutputTokens: 8);

        var router = new ModelRouter(new FakeInferenceBackend(response), new NoToolsGateway());

        var reply = await router.AskAsync("How full is my disk?");

        Assert.Equal("Your disk is 42% full.", reply);
    }

    [Fact]
    public async Task AskAsync_ConcatenatesMultipleTextBlocks()
    {
        var response = new InferenceResponse(
            new ChatMessage(ChatRole.Assistant, [new TextBlock("Part one. "), new TextBlock("Part two.")]),
            StopReason: "end_turn",
            InputTokens: 10,
            OutputTokens: 8);

        var router = new ModelRouter(new FakeInferenceBackend(response), new NoToolsGateway());

        var reply = await router.AskAsync("Tell me something.");

        Assert.Equal("Part one. Part two.", reply);
    }

    [Fact]
    public async Task AskAsync_PropagatesBackendUnavailable()
    {
        var router = new ModelRouter(new ThrowingInferenceBackend(), new NoToolsGateway());

        await Assert.ThrowsAsync<BackendUnavailableException>(() => router.AskAsync("ping"));
    }

    [Fact]
    public async Task AskAsync_SendsDiscoveredToolsToBackend()
    {
        InferenceRequest? capturedRequest = null;
        var response = new InferenceResponse(
            new ChatMessage(ChatRole.Assistant, [new TextBlock("done")]),
            StopReason: "end_turn",
            InputTokens: 1,
            OutputTokens: 1);

        var backend = new CapturingInferenceBackend(request =>
        {
            capturedRequest = request;
            return response;
        });

        var tool = new ToolDefinition("get_system_info", "Returns system info.", JsonDocument.Parse("{\"type\":\"object\"}").RootElement);
        var router = new ModelRouter(backend, new FixedToolsGateway([tool]));

        await router.AskAsync("What OS am I running?");

        Assert.NotNull(capturedRequest);
        Assert.Same(tool, Assert.Single(capturedRequest!.Tools));
    }

    private sealed class CapturingInferenceBackend(Func<InferenceRequest, InferenceResponse> handler) : IInferenceBackend
    {
        public Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(handler(request));
    }

    private sealed class FixedToolsGateway(IReadOnlyList<ToolDefinition> tools) : IMcpToolGateway
    {
        public Task<IReadOnlyList<ToolDefinition>> GetToolsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(tools);

        public Task<string> ExecuteAsync(string toolName, JsonElement input, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException($"Tool '{toolName}' was not expected to be called in this test.");
    }
}
