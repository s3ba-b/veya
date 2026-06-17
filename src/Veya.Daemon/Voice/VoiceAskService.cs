using Veya.Shared.Inference;
using Veya.Shared.Permissions;
using Veya.Shared.Safety;
using Veya.Shared.Voice;

namespace Veya.Daemon.Voice;

/// <summary>
/// Orchestrates a single voice question (ADR-0015): permission check, record,
/// transcribe, run the transcript through the existing <see cref="IModelRouter"/>
/// pipeline exactly like a typed <c>Ask</c>, then speak the reply aloud
/// best-effort. Backs the D-Bus <c>AskVoice</c> method.
/// </summary>
public sealed class VoiceAskService(
    IPermissionGate permissionGate,
    IAudioRecorder recorder,
    ISpeechToText speechToText,
    ITextToSpeech textToSpeech,
    IModelRouter modelRouter,
    IAuditLog auditLog,
    VoiceOptions options) : IVoiceAskService
{
    private const string Requester = "ask_voice";

    public async Task<(string Transcript, string Reply)> AskAsync(uint maxDurationMs, CancellationToken cancellationToken = default)
    {
        if (!await permissionGate.CheckAsync(PermissionSource.Microphone, Requester, cancellationToken).ConfigureAwait(false))
        {
            return (string.Empty, "Microphone access is not granted. The user must enable the microphone permission before Veya can listen.");
        }

        var requestedMs = maxDurationMs == 0 ? options.MaxRecordingMs : (int)maxDurationMs;
        var duration = TimeSpan.FromMilliseconds(Math.Min(requestedMs, options.MaxRecordingMs));

        var startedAt = DateTimeOffset.UtcNow;
        var wavPath = await recorder.RecordToFileAsync(duration, cancellationToken).ConfigureAwait(false);
        if (wavPath is null)
        {
            await auditLog.WriteAsync(AuditEvent.VoiceCapture(false, 0, DateTimeOffset.UtcNow - startedAt), cancellationToken).ConfigureAwait(false);
            return (string.Empty, "Veya couldn't hear anything. Check that a microphone is connected and try again.");
        }

        string? transcript;
        try
        {
            transcript = await speechToText.TranscribeAsync(wavPath, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDelete(wavPath);
        }

        if (string.IsNullOrWhiteSpace(transcript))
        {
            await auditLog.WriteAsync(AuditEvent.VoiceCapture(false, 0, DateTimeOffset.UtcNow - startedAt), cancellationToken).ConfigureAwait(false);
            return (string.Empty, "Veya didn't catch any words. Make sure the voice model is installed and try speaking clearly.");
        }

        await auditLog.WriteAsync(AuditEvent.VoiceCapture(true, transcript.Length, DateTimeOffset.UtcNow - startedAt), cancellationToken).ConfigureAwait(false);

        string reply;
        try
        {
            reply = await modelRouter.AskAsync(transcript, cancellationToken).ConfigureAwait(false);
        }
        catch (BackendUnavailableException ex)
        {
            reply = $"Veya can't reach its model backend right now: {ex.Message}";
        }

        var speakStartedAt = DateTimeOffset.UtcNow;
        var spoken = await textToSpeech.SpeakAsync(reply, cancellationToken).ConfigureAwait(false);
        await auditLog.WriteAsync(AuditEvent.VoiceSpeak(spoken, reply.Length, DateTimeOffset.UtcNow - speakStartedAt), cancellationToken).ConfigureAwait(false);

        return (transcript, reply);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
