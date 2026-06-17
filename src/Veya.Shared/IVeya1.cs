using Tmds.DBus;

namespace Veya.Shared;

/// <summary>
/// D-Bus contract for org.veya.Veya1, documented in docs/dbus-interfaces.md.
/// Method names ending in "Async" map to the D-Bus method without that
/// suffix (Tmds.DBus convention): <see cref="AskAsync"/> is D-Bus
/// <c>Ask(in s prompt, out s reply)</c>. <c>Watch*Async</c> methods register
/// handlers for the like-named D-Bus signal.
/// </summary>
[DBusInterface(VeyaDBus.InterfaceName)]
public interface IVeya1 : IDBusObject
{
    public Task<string> AskAsync(string prompt);

    /// <summary>
    /// D-Bus <c>AskVoice(in u maxDurationMs, out s transcript, out s reply)</c>
    /// (ADR-0015): records up to <c>maxDurationMs</c> of microphone audio
    /// (default <c>Voice:MaxRecordingMs</c> when 0), transcribes it locally,
    /// runs the transcript through the same pipeline as <see cref="AskAsync"/>,
    /// and speaks the reply aloud best-effort. Gated by the <c>Microphone</c>
    /// permission (default-deny); a denial returns an explanatory reply with
    /// an empty transcript rather than recording anything.
    /// </summary>
    public Task<(string Transcript, string Reply)> AskVoiceAsync(uint maxDurationMs);

    /// <summary>
    /// D-Bus <c>GetStatus(out a{sv} status)</c>: daemon status as a string→variant
    /// map. Currently carries <c>version</c> and <c>activeBackend</c> (the backend
    /// that served the most recent request: <c>"ollama"</c>, <c>"mistral"</c>, or
    /// <c>"claude"</c>). MCP health and pending-request counts are a follow-up.
    /// </summary>
    public Task<IDictionary<string, object>> GetStatusAsync();

    /// <summary>
    /// Registers a handler for the <c>CloudUsage</c> signal, emitted whenever a
    /// request leaves the machine to a cloud backend (the user-visible cloud-usage
    /// hook — a product pillar). Local-only requests never fire it.
    /// </summary>
    public Task<IDisposable> WatchCloudUsageAsync(Action<CloudUsageInfo> handler);
}
