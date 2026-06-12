using Sage.Overlay;
using Xunit;

namespace Sage.Overlay.Tests;

public class OverlayViewModelTests
{
    private sealed class FakeSage1Client(Func<string, string> reply) : ISage1Client
    {
        public string? LastPrompt { get; private set; }

        public Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default)
        {
            LastPrompt = prompt;
            return Task.FromResult(reply(prompt));
        }
    }

    private sealed class ThrowingSage1Client(Exception exception) : ISage1Client
    {
        public Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw exception;
    }

    [Fact]
    public async Task AskAsync_ReturnsClientReply()
    {
        var client = new FakeSage1Client(prompt => $"Sage says: {prompt}");
        var viewModel = new OverlayViewModel(client);

        var reply = await viewModel.AskAsync("how full is my disk?");

        Assert.Equal("Sage says: how full is my disk?", reply);
        Assert.Equal("how full is my disk?", client.LastPrompt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AskAsync_WithBlankPrompt_ReturnsEmptyWithoutCallingClient(string prompt)
    {
        var client = new FakeSage1Client(_ => throw new InvalidOperationException("should not be called"));
        var viewModel = new OverlayViewModel(client);

        var reply = await viewModel.AskAsync(prompt);

        Assert.Equal(string.Empty, reply);
        Assert.Null(client.LastPrompt);
    }

    [Fact]
    public async Task AskAsync_WhenClientThrows_ReturnsFriendlyError()
    {
        var client = new ThrowingSage1Client(new InvalidOperationException("No D-Bus session bus is available."));
        var viewModel = new OverlayViewModel(client);

        var reply = await viewModel.AskAsync("hello");

        Assert.Equal("Sage is unreachable: No D-Bus session bus is available.", reply);
    }

    [Fact]
    public async Task AskAsync_PropagatesCancellation()
    {
        var client = new ThrowingSage1Client(new OperationCanceledException());
        var viewModel = new OverlayViewModel(client);

        await Assert.ThrowsAsync<OperationCanceledException>(() => viewModel.AskAsync("hello"));
    }
}
