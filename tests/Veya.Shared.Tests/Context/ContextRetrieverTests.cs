using Veya.Shared.Context;
using Veya.Shared.Permissions;
using Veya.TestSupport;
using Xunit;

namespace Veya.Shared.Tests.Context;

public class ContextRetrieverTests
{
    private static readonly IReadOnlyList<PermissionSource> Candidates = [PermissionSource.PersonalIndex];

    private static async Task<SqliteContextStore> SeededStoreAsync()
    {
        var store = new SqliteContextStore("Data Source=:memory:");
        // FakeEmbeddingBackend maps text -> [text.Length, 0, 0]; seed two lengths.
        await store.UpsertAsync(new ContextChunk("a", PermissionSource.PersonalIndex, "o/a", "ab"), [2f, 0f, 0f]);
        await store.UpsertAsync(new ContextChunk("b", PermissionSource.PersonalIndex, "o/b", "abcdefghij"), [10f, 0f, 0f]);
        return store;
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsMatchesAndAuditsQuery_WhenGranted()
    {
        await using var store = await SeededStoreAsync();
        var audit = new RecordingAuditLog();
        var retriever = new ContextRetriever(
            new FakeEmbeddingBackend(), store, new FakePermissionGate(PermissionSource.PersonalIndex), audit, Candidates);

        // Query "ab" embeds to [2,0,0] -> nearest is chunk "a".
        var matches = await retriever.RetrieveAsync("ab", k: 1);

        var match = Assert.Single(matches);
        Assert.Equal("a", match.Chunk.Id);

        var query = Assert.Single(audit.Events, e => e.EventType == "context.query");
        Assert.Equal(1, query.Fields["matchCount"]);
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsEmpty_WhenSourceDenied()
    {
        await using var store = await SeededStoreAsync();
        var audit = new RecordingAuditLog();
        var retriever = new ContextRetriever(
            new FakeEmbeddingBackend(), store, new FakePermissionGate(), audit, Candidates);

        var matches = await retriever.RetrieveAsync("ab", k: 5);

        Assert.Empty(matches);
        // Denied before embedding: no query event, but the gate was consulted.
        Assert.DoesNotContain(audit.Events, e => e.EventType == "context.query");
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsEmptyWithoutThrowing_WhenEmbeddingUnavailable()
    {
        await using var store = await SeededStoreAsync();
        var retriever = new ContextRetriever(
            new FakeEmbeddingBackend(unavailable: true), store, new FakePermissionGate(PermissionSource.PersonalIndex), new RecordingAuditLog(), Candidates);

        var matches = await retriever.RetrieveAsync("ab", k: 5);

        Assert.Empty(matches);
    }
}
