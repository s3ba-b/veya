namespace Veya.Shared.Voice;

/// <summary>
/// Records microphone audio to a temporary file (ADR-0015). The real
/// implementation lives in the Daemon, behind <c>ISafeExecutor</c>.
/// </summary>
public interface IAudioRecorder
{
    /// <summary>
    /// Records up to <paramref name="maxDuration"/> of audio and returns the
    /// path to a temporary wav file, or <c>null</c> if capture failed.
    /// </summary>
    public Task<string?> RecordToFileAsync(TimeSpan maxDuration, CancellationToken cancellationToken = default);
}
