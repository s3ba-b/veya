using Veya.Shared.Context;
using Veya.Shared.Permissions;
using Xunit;

namespace Veya.Shared.Tests.Context;

/// <summary>
/// Runs against a real in-memory SQLite with the sqlite-vec extension loaded
/// (ADR-0009): if the native <c>vec0</c> asset cannot load on headless CI, these
/// fail rather than being mocked away.
/// </summary>
public class SqliteContextStoreTests
{
    private static SqliteContextStore NewStore() => new("Data Source=:memory:");

    private static float[] Vec(params float[] values) => values;

    private static ContextChunk Chunk(string id, PermissionSource source, string text) =>
        new(id, source, $"origin/{id}", text);

    [Fact]
    public async Task SearchAsync_ReturnsNearestChunkFirst()
    {
        await using var store = NewStore();

        await store.UpsertAsync(Chunk("a", PermissionSource.PersonalIndex, "cats"), Vec(1f, 0f, 0f));
        await store.UpsertAsync(Chunk("b", PermissionSource.PersonalIndex, "dogs"), Vec(0f, 1f, 0f));

        var matches = await store.SearchAsync(Vec(0.9f, 0.1f, 0f), k: 2, AllowedSet(PermissionSource.PersonalIndex));

        Assert.Equal(2, matches.Count);
        Assert.Equal("a", matches[0].Chunk.Id);
        Assert.Equal("b", matches[1].Chunk.Id);
        Assert.True(matches[0].Distance <= matches[1].Distance);
    }

    [Fact]
    public async Task SearchAsync_ExcludesChunksFromDisallowedSources()
    {
        await using var store = NewStore();

        await store.UpsertAsync(Chunk("idx", PermissionSource.PersonalIndex, "indexed"), Vec(1f, 0f, 0f));
        await store.UpsertAsync(Chunk("clip", PermissionSource.Clipboard, "clipboard"), Vec(1f, 0f, 0f));

        var matches = await store.SearchAsync(Vec(1f, 0f, 0f), k: 10, AllowedSet(PermissionSource.PersonalIndex));

        var match = Assert.Single(matches);
        Assert.Equal(PermissionSource.PersonalIndex, match.Chunk.Source);
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmpty_WhenNoAllowedSources()
    {
        await using var store = NewStore();
        await store.UpsertAsync(Chunk("a", PermissionSource.PersonalIndex, "x"), Vec(1f, 0f, 0f));

        var matches = await store.SearchAsync(Vec(1f, 0f, 0f), k: 5, AllowedSet());

        Assert.Empty(matches);
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmpty_BeforeAnyUpsert()
    {
        await using var store = NewStore();

        var matches = await store.SearchAsync(Vec(1f, 0f, 0f), k: 5, AllowedSet(PermissionSource.PersonalIndex));

        Assert.Empty(matches);
    }

    [Fact]
    public async Task UpsertAsync_ReplacesChunkWithSameSourceAndId()
    {
        await using var store = NewStore();

        await store.UpsertAsync(Chunk("a", PermissionSource.PersonalIndex, "first"), Vec(1f, 0f, 0f));
        await store.UpsertAsync(Chunk("a", PermissionSource.PersonalIndex, "second"), Vec(0f, 1f, 0f));

        var matches = await store.SearchAsync(Vec(0f, 1f, 0f), k: 10, AllowedSet(PermissionSource.PersonalIndex));

        var match = Assert.Single(matches);
        Assert.Equal("second", match.Chunk.Text);
    }

    [Fact]
    public async Task DeleteBySourceAsync_RemovesOnlyThatSource()
    {
        await using var store = NewStore();

        await store.UpsertAsync(Chunk("idx", PermissionSource.PersonalIndex, "indexed"), Vec(1f, 0f, 0f));
        await store.UpsertAsync(Chunk("clip", PermissionSource.Clipboard, "clipboard"), Vec(0f, 1f, 0f));

        await store.DeleteBySourceAsync(PermissionSource.PersonalIndex);

        var personal = await store.SearchAsync(Vec(1f, 0f, 0f), k: 10, AllowedSet(PermissionSource.PersonalIndex));
        Assert.Empty(personal);

        var clipboard = await store.SearchAsync(Vec(0f, 1f, 0f), k: 10, AllowedSet(PermissionSource.Clipboard));
        Assert.Single(clipboard);
    }

    private static IReadOnlySet<PermissionSource> AllowedSet(params PermissionSource[] sources) => sources.ToHashSet();
}
