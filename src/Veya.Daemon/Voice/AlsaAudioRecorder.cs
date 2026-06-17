using Veya.Shared.Safety;
using Veya.Shared.Voice;

namespace Veya.Daemon.Voice;

/// <summary>
/// Records microphone audio via <c>arecord</c> (ADR-0015), run through
/// <see cref="ISafeExecutor"/>. Talks to ALSA directly, which both PulseAudio
/// and PipeWire provide a compatibility shim for on stock Ubuntu, so no
/// sound-server detection is needed (unlike <c>ClipboardTool</c>'s
/// Wayland/X11 split).
/// </summary>
public sealed class AlsaAudioRecorder(ISafeExecutor executor) : IAudioRecorder
{
    private const int MaxDurationSeconds = 60;

    public static IReadOnlyDictionary<string, CommandSpec> Allowlist { get; } = new Dictionary<string, CommandSpec>
    {
        // arecord -q -t wav -f S16_LE -c 1 -r 16000 -d <seconds> <tmpfile>:
        // quiet, 16kHz mono 16-bit PCM wav — the shape Whisper.net expects.
        ["arecord"] = new CommandSpec("/usr/bin/arecord", IsRecordArgumentsAllowed),
    };

    public async Task<string?> RecordToFileAsync(TimeSpan maxDuration, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(Path.GetTempPath(), $"veya-voice-{Guid.NewGuid():N}.wav");
        var seconds = Math.Clamp((int)Math.Ceiling(maxDuration.TotalSeconds), 1, MaxDurationSeconds);

        try
        {
            var result = await executor.RunAsync(
                new ExecRequest("ask_voice", "arecord", ["-q", "-t", "wav", "-f", "S16_LE", "-c", "1", "-r", "16000", "-d", seconds.ToString(), path]),
                cancellationToken).ConfigureAwait(false);

            return result is { ExitCode: 0, TimedOut: false } && File.Exists(path) ? path : null;
        }
        catch (CommandNotAllowedException)
        {
            return null;
        }
    }

    private static bool IsRecordArgumentsAllowed(IReadOnlyList<string> args) =>
        args.Count == 12
        && args[0] == "-q"
        && args[1] == "-t" && args[2] == "wav"
        && args[3] == "-f" && args[4] == "S16_LE"
        && args[5] == "-c" && args[6] == "1"
        && args[7] == "-r" && args[8] == "16000"
        && args[9] == "-d"
        && int.TryParse(args[10], out var seconds) && seconds is > 0 and <= MaxDurationSeconds
        && args[11].StartsWith(Path.GetTempPath(), StringComparison.Ordinal) && args[11].EndsWith(".wav", StringComparison.Ordinal);
}
