using System.Runtime.CompilerServices;
using Veya.Shared.Context;
using Veya.Shared.Inference;
using Veya.Shared.Permissions;

namespace Veya.Shared.Tests.Context;

/// <summary>Grants the configured sources; denies everything else (default-deny).</summary>
internal sealed class FakePermissionGate(params PermissionSource[] granted) : IPermissionGate
{
    private readonly HashSet<PermissionSource> _granted = granted.ToHashSet();

    public List<(PermissionSource Source, string Requester)> Checks { get; } = [];

    public Task<bool> CheckAsync(PermissionSource source, string requester, CancellationToken cancellationToken = default)
    {
        Checks.Add((source, requester));
        return Task.FromResult(_granted.Contains(source));
    }
}

/// <summary>
/// Embeds each text as a fixed-dimension vector derived from its length, or
/// throws to simulate an unavailable backend.
/// </summary>
internal sealed class FakeEmbeddingBackend(bool unavailable = false) : IEmbeddingBackend
{
    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        if (unavailable)
        {
            throw new BackendUnavailableException("test: embedding backend down");
        }

        IReadOnlyList<float[]> vectors = texts.Select(text => new[] { text.Length, 0f, 0f }).ToList();
        return Task.FromResult(vectors);
    }
}

internal sealed class InMemoryContextSource(PermissionSource source, IEnumerable<ContextChunk> chunks) : IContextSource
{
    private readonly List<ContextChunk> _chunks = chunks.ToList();

    public PermissionSource Source { get; } = source;

    public async IAsyncEnumerable<ContextChunk> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var chunk in _chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return chunk;
            await Task.Yield();
        }
    }
}
