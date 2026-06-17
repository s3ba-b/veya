namespace Veya.Daemon.Voice;

/// <summary>
/// Configuration for voice I/O (ADR-0015), bound from the <c>Voice</c> config
/// section.
/// </summary>
public sealed class VoiceOptions
{
    /// <summary>
    /// Path to the ggml Whisper model used for transcription. Not bundled or
    /// auto-downloaded — fetch once with <c>scripts/download-whisper-model.sh</c>.
    /// </summary>
    public string WhisperModelPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "veya", "models", "ggml-base.bin");

    /// <summary>Upper bound on how long <c>AskVoice</c> will record for.</summary>
    public int MaxRecordingMs { get; set; } = 15_000;
}
