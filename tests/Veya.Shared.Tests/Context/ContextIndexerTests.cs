using Veya.Shared.Context;
using Veya.Shared.Permissions;
using Xunit;

namespace Veya.Shared.Tests.Context;

public class ContextIndexerTests
{
    private static ContextChunk Chunk(string id) => new(id, PermissionSource.PersonalIndex, $"origin/{id}", $"text-{id}");

    private static InMemoryContextSource Source(params string[] ids) =>
        new(PermissionSource.PersonalIndex, ids.Select(Chunk));

    [Fact]
    public async Task IngestAsync_StoresChunksAndWritesAuditEvent_WhenGranted()
    {
        await using var store = new SqliteContextStore("Data Source=:memory:");
        var audit = new RecordingAuditLog();
        var indexer = new ContextIndexer(new FakeEmbeddingBackend(), store, new FakePermissionGate(PermissionSource.PersonalIndex), audit);

        var result = await indexer.IngestAsync(Source("a", "b"));

        Assert.True(result.PermissionGranted);
        Assert.Equal(2, result.IndexedCount);
        Assert.False(result.EmbeddingUnavailable);

        var ingest = Assert.Single(audit.Events, e => e.EventType == "context.ingest");
        Assert.Equal("PersonalIndex", ingest.Fields["source"]);
        Assert.Equal(2, ingest.Fields["indexedCount"]);

        var stored = await store.SearchAsync([5f, 0f, 0f], k: 10, new HashSet<PermissionSource> { PermissionSource.PersonalIndex });
        Assert.Equal(2, stored.Count);
    }

    [Fact]
    public async Task IngestAsync_ReadsNothingAndDoesNotStore_WhenDenied()
    {
        await using var store = new SqliteContextStore("Data Source=:memory:");
        var audit = new RecordingAuditLog();
        // Default-deny gate (nothing granted).
        var indexer = new ContextIndexer(new FakeEmbeddingBackend(), store, new FakePermissionGate(), audit);

        var result = await indexer.IngestAsync(Source("a"));

        Assert.False(result.PermissionGranted);
        Assert.Equal(0, result.IndexedCount);
        Assert.DoesNotContain(audit.Events, e => e.EventType == "context.ingest");
    }

    [Fact]
    public async Task IngestAsync_WithReplaceExisting_RemovesStaleChunks()
    {
        await using var store = new SqliteContextStore("Data Source=:memory:");
        var indexer = new ContextIndexer(new FakeEmbeddingBackend(), store, new FakePermissionGate(PermissionSource.PersonalIndex), new RecordingAuditLog());

        await indexer.IngestAsync(Source("a", "b", "c"), replaceExisting: true);
        // Re-index with fewer chunks: the dropped ones must not linger.
        await indexer.IngestAsync(Source("a"), replaceExisting: true);

        var stored = await store.SearchAsync([6f, 0f, 0f], k: 10, new HashSet<PermissionSource> { PermissionSource.PersonalIndex });
        var match = Assert.Single(stored);
        Assert.Equal("a", match.Chunk.Id);
    }

    [Fact]
    public async Task IngestAsync_WithReplaceExisting_DoesNotDeleteWhenDenied()
    {
        await using var store = new SqliteContextStore("Data Source=:memory:");
        // Seed via a granting indexer, then attempt a denied replace-index.
        await new ContextIndexer(new FakeEmbeddingBackend(), store, new FakePermissionGate(PermissionSource.PersonalIndex), new RecordingAuditLog())
            .IngestAsync(Source("a"), replaceExisting: true);

        var deniedIndexer = new ContextIndexer(new FakeEmbeddingBackend(), store, new FakePermissionGate(), new RecordingAuditLog());
        var result = await deniedIndexer.IngestAsync(Source("a", "b"), replaceExisting: true);

        Assert.False(result.PermissionGranted);
        var stored = await store.SearchAsync([5f, 0f, 0f], k: 10, new HashSet<PermissionSource> { PermissionSource.PersonalIndex });
        Assert.Single(stored);
    }

    [Fact]
    public async Task IngestAsync_DegradesWithoutThrowing_WhenEmbeddingUnavailable()
    {
        await using var store = new SqliteContextStore("Data Source=:memory:");
        var indexer = new ContextIndexer(
            new FakeEmbeddingBackend(unavailable: true), store, new FakePermissionGate(PermissionSource.PersonalIndex), new RecordingAuditLog());

        var result = await indexer.IngestAsync(Source("a", "b"));

        Assert.True(result.PermissionGranted);
        Assert.Equal(0, result.IndexedCount);
        Assert.True(result.EmbeddingUnavailable);
    }
}
