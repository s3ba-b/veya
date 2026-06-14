namespace Veya.Shared.Inference;

/// <summary>
/// <see cref="IInferenceBackend"/> decorator implementing the local-first
/// policy from docs/security.md ("Cloud transparency"): tries
/// <paramref name="primary"/> first, falling back to
/// <paramref name="secondary"/> only when <paramref name="primary"/> throws
/// <see cref="BackendUnavailableException"/>. Any other exception (including
/// <see cref="OperationCanceledException"/>) propagates without falling back.
/// </summary>
public sealed class FallbackInferenceBackend(IInferenceBackend primary, IInferenceBackend secondary) : IInferenceBackend
{
    public async Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await primary.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (BackendUnavailableException)
        {
            return await secondary.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
