using Veya.Shared.Safety;
using Veya.Shared.Voice;

namespace Veya.Daemon.Voice;

/// <summary>
/// Speaks text aloud via <c>espeak-ng</c> (ADR-0015), run through
/// <see cref="ISafeExecutor"/>. The text is piped via stdin (no text
/// argument), so it never appears in the <c>tool.exec</c> audit log's
/// recorded argv — the same reason <c>ClipboardTool</c> passes clipboard
/// content via stdin (ADR-0006).
/// </summary>
public sealed class EspeakTextToSpeech(ISafeExecutor executor) : ITextToSpeech
{
    public static IReadOnlyDictionary<string, CommandSpec> Allowlist { get; } = new Dictionary<string, CommandSpec>
    {
        ["espeak-ng"] = CommandSpec.AllowNoArguments("/usr/bin/espeak-ng"),
    };

    public async Task<bool> SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            // Detached: the call returns once playback has started, mirroring
            // wl-copy's fire-and-forget execution (ADR-0006) rather than
            // blocking the caller for the full length of the reply.
            await executor.RunAsync(new ExecRequest("ask_voice", "espeak-ng", [], StandardInput: text, Detached: true), cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (CommandNotAllowedException)
        {
            return false;
        }
    }
}
