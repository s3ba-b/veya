namespace Veya.Daemon.Voice;

/// <summary>
/// Answers a single voice question (ADR-0015): record, transcribe, run the
/// transcript through the same pipeline as a typed <c>Ask</c>, speak the
/// reply aloud. Backs the D-Bus <c>AskVoice</c> method on <see cref="Veya1Service"/>.
/// </summary>
public interface IVoiceAskService
{
    public Task<(string Transcript, string Reply)> AskAsync(uint maxDurationMs, CancellationToken cancellationToken = default);
}
