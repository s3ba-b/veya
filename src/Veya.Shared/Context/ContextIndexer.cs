using Veya.Shared.Inference;
using Veya.Shared.Permissions;
using Veya.Shared.Safety;

namespace Veya.Shared.Context;

/// <summary>
/// Ingests an <see cref="IContextSource"/> into the personal context index
/// (ADR-0009): gates the source's permission, embeds its chunks locally, and
/// stores them. The ingestion half of "permission checked at ingest and query".
/// </summary>
/// <remarks>
/// If the embedding backend is unavailable the run stops and the unprocessed
/// chunks are left for a later run — nothing is stored without its embedding
/// (ADR-0009: the index degrades, it does not break).
/// </remarks>
public sealed class ContextIndexer
{
    /// <summary>Requester string recorded in permission and audit events for ingestion.</summary>
    public const string Requester = "context.ingest";

    private const int BatchSize = 32;

    private readonly IEmbeddingBackend _embeddingBackend;
    private readonly IContextStore _store;
    private readonly IPermissionGate _permissionGate;
    private readonly IAuditLog _auditLog;

    public ContextIndexer(IEmbeddingBackend embeddingBackend, IContextStore store, IPermissionGate permissionGate, IAuditLog auditLog)
    {
        _embeddingBackend = embeddingBackend;
        _store = store;
        _permissionGate = permissionGate;
        _auditLog = auditLog;
    }

    /// <summary>
    /// Indexes <paramref name="source"/>. Returns without reading anything if its
    /// permission is denied (the gate has already logged the decision).
    /// </summary>
    public async Task<IndexResult> IngestAsync(IContextSource source, CancellationToken cancellationToken = default)
    {
        var granted = await _permissionGate.CheckAsync(source.Source, Requester, cancellationToken).ConfigureAwait(false);
        if (!granted)
        {
            return new IndexResult(PermissionGranted: false, IndexedCount: 0, EmbeddingUnavailable: false);
        }

        var startedAt = DateTimeOffset.UtcNow;
        var indexed = 0;
        var embeddingUnavailable = false;

        var batch = new List<ContextChunk>(BatchSize);
        try
        {
            await foreach (var chunk in source.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                batch.Add(chunk);
                if (batch.Count >= BatchSize)
                {
                    indexed += await EmbedAndStoreAsync(batch, cancellationToken).ConfigureAwait(false);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                indexed += await EmbedAndStoreAsync(batch, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (BackendUnavailableException)
        {
            // Leave the rest for a later run; keep whatever already stored.
            embeddingUnavailable = true;
        }

        await _auditLog.WriteAsync(
            AuditEvent.ContextIngest(source.Source.ToString(), Requester, indexed, DateTimeOffset.UtcNow - startedAt),
            cancellationToken).ConfigureAwait(false);

        return new IndexResult(PermissionGranted: true, indexed, embeddingUnavailable);
    }

    private async Task<int> EmbedAndStoreAsync(IReadOnlyList<ContextChunk> chunks, CancellationToken cancellationToken)
    {
        var embeddings = await _embeddingBackend.EmbedAsync(chunks.Select(chunk => chunk.Text).ToList(), cancellationToken)
            .ConfigureAwait(false);

        for (var i = 0; i < chunks.Count; i++)
        {
            await _store.UpsertAsync(chunks[i], embeddings[i], cancellationToken).ConfigureAwait(false);
        }

        return chunks.Count;
    }
}
