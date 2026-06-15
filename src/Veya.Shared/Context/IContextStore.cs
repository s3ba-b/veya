using Veya.Shared.Permissions;

namespace Veya.Shared.Context;

/// <summary>
/// Persists context chunks with their embeddings and answers nearest-neighbour
/// queries over them (ADR-0009). The personal context index's storage layer.
/// </summary>
/// <remarks>
/// <see cref="SearchAsync"/> filters to an explicit set of allowed sources. The
/// caller passes only sources the permission gate has approved for this query;
/// the store's own filter is defence in depth so a revoked source cannot surface
/// even while its rows are still present (ADR-0009).
/// </remarks>
public interface IContextStore
{
    /// <summary>Inserts or replaces a chunk and its embedding (keyed by source + id).</summary>
    public Task UpsertAsync(ContextChunk chunk, float[] embedding, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns up to <paramref name="k"/> chunks closest to
    /// <paramref name="queryEmbedding"/>, restricted to <paramref name="allowedSources"/>,
    /// ordered nearest first. An empty allowed set returns nothing.
    /// </summary>
    public Task<IReadOnlyList<ContextMatch>> SearchAsync(
        float[] queryEmbedding,
        int k,
        IReadOnlySet<PermissionSource> allowedSources,
        CancellationToken cancellationToken = default);

    /// <summary>Removes every chunk belonging to a source (e.g. when the user revokes it).</summary>
    public Task DeleteBySourceAsync(PermissionSource source, CancellationToken cancellationToken = default);
}
