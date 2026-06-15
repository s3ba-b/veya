using Veya.Shared.Permissions;

namespace Veya.Shared.Context;

/// <summary>
/// A provider of personal-context chunks for the index (ADR-0009), e.g. a file
/// folder or the notification stream. Each source maps to one
/// <see cref="PermissionSource"/>, which <see cref="ContextIndexer"/> gates
/// before reading anything.
/// </summary>
/// <remarks>
/// Milestone 3 ships no concrete sources beyond tests/manual; file and
/// notification sources arrive with their own ADRs (ADR-0009).
/// </remarks>
public interface IContextSource
{
    /// <summary>The permission source this provider's chunks belong to.</summary>
    public PermissionSource Source { get; }

    /// <summary>
    /// Yields the chunks to index. Called only after the source's permission has
    /// been granted, so implementations need not re-check it.
    /// </summary>
    public IAsyncEnumerable<ContextChunk> ReadAsync(CancellationToken cancellationToken = default);
}
