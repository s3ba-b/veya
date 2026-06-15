namespace Veya.Shared.Context;

/// <summary>
/// A chunk returned by <see cref="IContextStore.SearchAsync"/> together with its
/// distance from the query embedding (smaller is closer).
/// </summary>
public sealed record ContextMatch(ContextChunk Chunk, double Distance);
