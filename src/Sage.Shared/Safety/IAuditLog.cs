namespace Sage.Shared.Safety;

/// <summary>
/// Append-only audit trail (docs/security.md). Every safety-layer decision —
/// allowed or denied — is written here.
/// </summary>
public interface IAuditLog
{
    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}
