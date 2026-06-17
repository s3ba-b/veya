using Veya.Shared.Safety;

namespace Veya.TestSupport;

/// <summary>
/// In-memory <see cref="IAuditLog"/> that records every event for assertions,
/// shared across test projects to avoid re-declaring this fake in each one.
/// </summary>
public sealed class RecordingAuditLog : IAuditLog
{
    public List<AuditEvent> Events { get; } = [];

    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        Events.Add(auditEvent);
        return Task.CompletedTask;
    }
}
