namespace Veya.Shared.Inference;

/// <summary>
/// Turns text into embedding vectors for the personal context index
/// (ADR-0009). <see cref="OllamaEmbeddingBackend"/> computes them locally so
/// nothing leaves the machine; a cloud implementation is deferred.
/// </summary>
/// <remarks>
/// When the backend is unavailable it throws
/// <see cref="BackendUnavailableException"/>, exactly like
/// <see cref="IInferenceBackend"/>. Callers treat that as "index degraded":
/// ingestion skips the chunk, query falls back to no personal context, and
/// <c>Ask</c> still answers (ADR-0009).
/// </remarks>
public interface IEmbeddingBackend
{
    /// <summary>
    /// Embeds each input string, returning one vector per input in the same
    /// order. All vectors share the model's dimension.
    /// </summary>
    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
