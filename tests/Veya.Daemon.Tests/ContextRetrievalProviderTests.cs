using Veya.Shared.Context;
using Veya.Shared.Inference;
using Veya.Shared.Permissions;
using Veya.Shared.Safety;
using Xunit;

namespace Veya.Daemon.Tests;

public class ContextRetrievalProviderTests
{
    private sealed class NullAuditLog : IAuditLog
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class GrantAllGate : IPermissionGate
    {
        public Task<bool> CheckAsync(PermissionSource source, string requester, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class DenyAllGate : IPermissionGate
    {
        public Task<bool> CheckAsync(PermissionSource source, string requester, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    // Embeds each text to its length so SearchAsync's nearest-neighbor ordering is predictable.
    private sealed class LengthEmbeddingBackend : IEmbeddingBackend
    {
        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<float[]>>(texts.Select(t => new[] { (float)t.Length, 0f, 0f }).ToList());
    }

    private static async Task<SqliteContextStore> SeededStoreAsync(params string[] texts)
    {
        var store = new SqliteContextStore("Data Source=:memory:");
        for (var i = 0; i < texts.Length; i++)
        {
            var chunk = new ContextChunk($"chunk{i}", PermissionSource.PersonalIndex, $"origin/{i}", texts[i]);
            await store.UpsertAsync(chunk, [(float)texts[i].Length, 0f, 0f]);
        }

        return store;
    }

    private static ContextRetriever Retriever(IContextStore store, IPermissionGate gate) =>
        new(new LengthEmbeddingBackend(), store, gate, new NullAuditLog(), [PermissionSource.PersonalIndex]);

    [Fact]
    public async Task GetContextBlockAsync_ReturnsNull_WhenNoMatches()
    {
        await using var store = await SeededStoreAsync();
        var provider = new ContextRetrievalProvider(Retriever(store, new DenyAllGate()));

        var block = await provider.GetContextBlockAsync("anything");

        Assert.Null(block);
    }

    [Fact]
    public async Task GetContextBlockAsync_FormatsMatchesAsBulletList()
    {
        await using var store = await SeededStoreAsync("alpha", "beta");
        var provider = new ContextRetrievalProvider(Retriever(store, new GrantAllGate()));

        // "alpha" (5 chars) embeds closest to a same-length query.
        var block = await provider.GetContextBlockAsync("alpha");

        Assert.NotNull(block);
        Assert.StartsWith("Relevant context from the user's approved personal sources:", block);
        Assert.Contains("- alpha", block);
        Assert.DoesNotContain("\n\n", block);
        Assert.False(block!.EndsWith('\n'));
    }

    [Fact]
    public async Task GetContextBlockAsync_PassesMaxChunksAsRetrievalLimit()
    {
        await using var store = await SeededStoreAsync("a", "bb", "ccc");
        var provider = new ContextRetrievalProvider(Retriever(store, new GrantAllGate()), maxChunks: 1);

        var block = await provider.GetContextBlockAsync("a");

        Assert.NotNull(block);
        Assert.Equal(1, block!.Split('\n').Length - 1);
    }
}
