using Veya.Shared;
using Veya.Shared.Safety;

namespace Veya.Daemon;

/// <summary>
/// <see cref="IAuditLog"/> decorator that surfaces inference activity to the
/// D-Bus layer (issue #51). It writes every event to the wrapped log unchanged,
/// then — for <c>cloud.request</c>/<c>local.request</c> events — tracks the active
/// backend and, for cloud requests only, raises <see cref="CloudRequested"/>. The
/// audit event stays the single source of truth, so the live signal can never
/// drift from the recorded trail (docs/security.md).
/// </summary>
public sealed class BackendActivityAuditLog(IAuditLog inner, string initialBackend = "ollama")
    : IAuditLog, IBackendActivityMonitor
{
    private volatile string _activeBackend = initialBackend;

    public event Action<CloudUsageInfo>? CloudRequested;

    public string ActiveBackend => _activeBackend;

    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await inner.WriteAsync(auditEvent, cancellationToken).ConfigureAwait(false);

        switch (auditEvent.EventType)
        {
            case "cloud.request":
                _activeBackend = Field(auditEvent, "backend", _activeBackend);
                CloudRequested?.Invoke(new CloudUsageInfo
                {
                    Backend = Field(auditEvent, "backend", "unknown"),
                    Model = Field(auditEvent, "model", "unknown"),
                    InputTokens = TokenField(auditEvent, "inputTokens"),
                    OutputTokens = TokenField(auditEvent, "outputTokens"),
                });
                break;

            case "local.request":
                _activeBackend = Field(auditEvent, "backend", _activeBackend);
                break;
        }
    }

    private static string Field(AuditEvent auditEvent, string key, string fallback) =>
        auditEvent.Fields.TryGetValue(key, out var value) && value is string text ? text : fallback;

    private static uint TokenField(AuditEvent auditEvent, string key) =>
        auditEvent.Fields.TryGetValue(key, out var value) && value is int count && count > 0 ? (uint)count : 0;
}
