using Veya.Shared;

namespace Veya.Daemon;

/// <summary>
/// Observes inference-backend activity recorded in the audit log so the D-Bus
/// layer can surface it live (docs/security.md: the <c>CloudUsage</c> signal
/// "mirrors" the audit log). Implemented by <see cref="BackendActivityAuditLog"/>.
/// </summary>
public interface IBackendActivityMonitor
{
    /// <summary>
    /// Raised after a <c>cloud.request</c> audit event is written — i.e. a request
    /// left the machine. Local requests do not raise it.
    /// </summary>
    public event Action<CloudUsageInfo>? CloudRequested;

    /// <summary>
    /// The backend that served the most recent request (<c>"ollama"</c>,
    /// <c>"mistral"</c>, or <c>"claude"</c>). Defaults to the local-first backend
    /// before any request has been handled.
    /// </summary>
    public string ActiveBackend { get; }
}
