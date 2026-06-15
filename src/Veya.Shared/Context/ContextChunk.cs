using Veya.Shared.Permissions;

namespace Veya.Shared.Context;

/// <summary>
/// One indexable unit of personal context (ADR-0009): a piece of text plus the
/// metadata needed to permission-gate and trace it. Stored in the context index
/// with its embedding.
/// </summary>
/// <param name="Id">Stable identifier, unique within a <see cref="Source"/>. Re-ingesting the same id replaces the row.</param>
/// <param name="Source">The permission source this chunk came from; gated at ingest and query (ADR-0005).</param>
/// <param name="Origin">Human-readable provenance, e.g. a file path or notification id. For tracing, not security.</param>
/// <param name="Text">The chunk text that gets embedded and, on retrieval, fed back as context.</param>
public sealed record ContextChunk(string Id, PermissionSource Source, string Origin, string Text);
