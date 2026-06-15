using Veya.Daemon;
using Xunit;

namespace Veya.Daemon.Tests;

public class CompositeContextProviderTests
{
    private sealed class FixedProvider(string? block) : IContextProvider
    {
        public Task<string?> GetContextBlockAsync(string prompt, CancellationToken cancellationToken = default) =>
            Task.FromResult(block);
    }

    [Fact]
    public async Task GetContextBlockAsync_ReturnsNull_WhenAllProvidersReturnNull()
    {
        var composite = new CompositeContextProvider([new FixedProvider(null), new FixedProvider(null)]);

        var block = await composite.GetContextBlockAsync("hello");

        Assert.Null(block);
    }

    [Fact]
    public async Task GetContextBlockAsync_ConcatenatesNonNullBlocks()
    {
        var composite = new CompositeContextProvider([new FixedProvider("Block A"), new FixedProvider(null), new FixedProvider("Block B")]);

        var block = await composite.GetContextBlockAsync("hello");

        Assert.Equal("Block A\n\nBlock B", block);
    }

    [Fact]
    public async Task GetContextBlockAsync_ReturnsSingleBlock_WhenOnlyOneProviderHasContext()
    {
        var composite = new CompositeContextProvider([new FixedProvider(null), new FixedProvider("Only block")]);

        var block = await composite.GetContextBlockAsync("hello");

        Assert.Equal("Only block", block);
    }
}
