using Veya.Overlay;
using Xunit;

namespace Veya.Overlay.Tests;

public class OverlayViewModelTests
{
    private sealed class FakeVeya1Client(Func<string, string> reply) : IVeya1Client
    {
        public string? LastPrompt { get; private set; }

        public Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default)
        {
            LastPrompt = prompt;
            return Task.FromResult(reply(prompt));
        }
    }

    private sealed class ThrowingVeya1Client(Exception exception) : IVeya1Client
    {
        public Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw exception;
    }

    [Fact]
    public async Task AskAsync_ReturnsClientReply()
    {
        var client = new FakeVeya1Client(prompt => $"Veya says: {prompt}");
        var viewModel = new OverlayViewModel(client);

        var reply = await viewModel.AskAsync("how full is my disk?");

        Assert.Equal("Veya says: how full is my disk?", reply);
        Assert.Equal("how full is my disk?", client.LastPrompt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AskAsync_WithBlankPrompt_ReturnsEmptyWithoutCallingClient(string prompt)
    {
        var client = new FakeVeya1Client(_ => throw new InvalidOperationException("should not be called"));
        var viewModel = new OverlayViewModel(client);

        var reply = await viewModel.AskAsync(prompt);

        Assert.Equal(string.Empty, reply);
        Assert.Null(client.LastPrompt);
    }

    [Fact]
    public async Task AskAsync_WhenClientThrows_ReturnsFriendlyError()
    {
        var client = new ThrowingVeya1Client(new InvalidOperationException("No D-Bus session bus is available."));
        var viewModel = new OverlayViewModel(client);

        var reply = await viewModel.AskAsync("hello");

        Assert.Equal("Veya is unreachable: No D-Bus session bus is available.", reply);
    }

    [Fact]
    public async Task AskAsync_PropagatesCancellation()
    {
        var client = new ThrowingVeya1Client(new OperationCanceledException());
        var viewModel = new OverlayViewModel(client);

        await Assert.ThrowsAsync<OperationCanceledException>(() => viewModel.AskAsync("hello"));
    }
}
