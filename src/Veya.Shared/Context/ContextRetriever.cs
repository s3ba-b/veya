using Veya.Shared.Inference;
using Veya.Shared.Permissions;
using Veya.Shared.Safety;

namespace Veya.Shared.Context;

/// <summary>
/// Retrieves personal context for a query (ADR-0009): re-checks each candidate
/// source's permission at query time, embeds the query locally, and searches the
/// index restricted to the granted sources. The query half of "permission
/// checked at ingest and query".
/// </summary>
/// <remarks>
/// If the embedding backend is unavailable, retrieval returns nothing rather
/// than throwing, so <c>Ask</c> still answers — just without personal context
/// (ADR-0009: the index is an enhancement, never a precondition).
/// </remarks>
public sealed class ContextRetriever
{
    /// <summary>Requester string recorded in permission and audit events for retrieval.</summary>
    public const string Requester = "context.query";

    private readonly IEmbeddingBackend _embeddingBackend;
    private readonly IContextStore _store;
    private readonly IPermissionGate _permissionGate;
    private readonly IAuditLog _auditLog;
    private readonly IReadOnlyList<PermissionSource> _candidateSources;

    /// <param name="candidateSources">
    /// Sources that may hold indexed context (e.g. <see cref="PermissionSource.PersonalIndex"/>);
    /// each is re-checked through the gate per query.
    /// </param>
    public ContextRetriever(
        IEmbeddingBackend embeddingBackend,
        IContextStore store,
        IPermissionGate permissionGate,
        IAuditLog auditLog,
        IReadOnlyList<PermissionSource> candidateSources)
    {
        _embeddingBackend = embeddingBackend;
        _store = store;
        _permissionGate = permissionGate;
        _auditLog = auditLog;
        _candidateSources = candidateSources;
    }

    /// <summary>
    /// Returns up to <paramref name="k"/> chunks relevant to <paramref name="query"/>
    /// from currently-granted sources, nearest first. Returns empty (never throws)
    /// when nothing is granted or the embedding backend is down.
    /// </summary>
    public async Task<IReadOnlyList<ContextMatch>> RetrieveAsync(string query, int k, CancellationToken cancellationToken = default)
    {
        var allowed = new HashSet<PermissionSource>();
        foreach (var source in _candidateSources)
        {
            if (await _permissionGate.CheckAsync(source, Requester, cancellationToken).ConfigureAwait(false))
            {
                allowed.Add(source);
            }
        }

        if (allowed.Count == 0)
        {
            return [];
        }

        var startedAt = DateTimeOffset.UtcNow;
        IReadOnlyList<ContextMatch> matches;
        try
        {
            var embeddings = await _embeddingBackend.EmbedAsync([query], cancellationToken).ConfigureAwait(false);
            matches = await _store.SearchAsync(embeddings[0], k, allowed, cancellationToken).ConfigureAwait(false);
        }
        catch (BackendUnavailableException)
        {
            // No embeddings means no retrieval; Ask proceeds without context.
            return [];
        }

        await _auditLog.WriteAsync(
            AuditEvent.ContextQuery(Requester, allowed.Select(source => source.ToString()).ToList(), matches.Count, DateTimeOffset.UtcNow - startedAt),
            cancellationToken).ConfigureAwait(false);

        return matches;
    }
}
