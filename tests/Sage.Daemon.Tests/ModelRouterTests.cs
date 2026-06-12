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

    [Fact]
    public async Task AskAsync_ReturnsTextFromFinalResponse()
    {
        var response = new InferenceResponse(
            new ChatMessage(ChatRole.Assistant, [new TextBlock("Your disk is 42% full.")]),
            StopReason: "end_turn",
            InputTokens: 10,
            OutputTokens: 8);

        var router = new ModelRouter(new FakeInferenceBackend(response));

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

        var router = new ModelRouter(new FakeInferenceBackend(response));

        var reply = await router.AskAsync("Tell me something.");

        Assert.Equal("Part one. Part two.", reply);
    }

    [Fact]
    public async Task AskAsync_PropagatesBackendUnavailable()
    {
        var router = new ModelRouter(new ThrowingInferenceBackend());

        await Assert.ThrowsAsync<BackendUnavailableException>(() => router.AskAsync("ping"));
    }
}
