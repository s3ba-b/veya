namespace Veya.Shared.Context;

/// <summary>
/// Outcome of a <see cref="ContextIndexer.IngestAsync"/> run (ADR-0009).
/// </summary>
/// <param name="PermissionGranted">Whether the source's permission was granted; if false nothing was read.</param>
/// <param name="IndexedCount">How many chunks were embedded and stored.</param>
/// <param name="EmbeddingUnavailable">
/// True when the embedding backend was unreachable, so the run was cut short and
/// the remaining chunks were left to be re-indexed later (ADR-0009: degrade,
/// don't break).
/// </param>
public sealed record IndexResult(bool PermissionGranted, int IndexedCount, bool EmbeddingUnavailable);
