namespace Veya.Shared.Voice;

/// <summary>
/// Transcribes a recorded wav file to text (ADR-0015). The real
/// implementation lives in the Daemon, using a local Whisper model.
/// </summary>
public interface ISpeechToText
{
    /// <summary>
    /// Transcribes the wav file at <paramref name="wavFilePath"/>, returning
    /// the recognized text, or <c>null</c> if transcription failed (e.g. the
    /// model isn't installed).
    /// </summary>
    public Task<string?> TranscribeAsync(string wavFilePath, CancellationToken cancellationToken = default);
}
