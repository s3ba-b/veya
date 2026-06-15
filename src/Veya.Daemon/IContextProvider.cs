namespace Veya.Daemon;

/// <summary>
/// Supplies personal context to fold into a prompt (ADR-0009). The model router
/// asks for a context block before each <c>Ask</c>; the implementation handles
/// permission checks, embedding, and retrieval. A null result means "no context"
/// — retrieval found nothing, nothing is granted, or the embedding backend is
/// down — and the router proceeds without it.
/// </summary>
public interface IContextProvider
{
    /// <summary>
    /// Returns a formatted context block relevant to <paramref name="prompt"/>,
    /// or <c>null</c> when there is nothing to add.
    /// </summary>
    public Task<string?> GetContextBlockAsync(string prompt, CancellationToken cancellationToken = default);
}
