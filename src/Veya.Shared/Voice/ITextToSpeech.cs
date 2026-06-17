namespace Veya.Shared.Voice;

/// <summary>
/// Speaks text aloud through the default local audio output (ADR-0015). The
/// real implementation lives in the Daemon, behind <c>ISafeExecutor</c>.
/// </summary>
public interface ITextToSpeech
{
    /// <summary>
    /// Speaks <paramref name="text"/> aloud. Returns whether playback was
    /// started successfully; failure is best-effort and never fails the
    /// caller's broader request.
    /// </summary>
    public Task<bool> SpeakAsync(string text, CancellationToken cancellationToken = default);
}
