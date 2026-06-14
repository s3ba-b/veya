using Veya.Shared.Safety;

namespace Veya.Shared.Permissions;

/// <summary>
/// Default <see cref="IPermissionGate"/>: looks the source up in the
/// <paramref name="store"/> and writes a <c>permission.decision</c> audit
/// event for every check, allowed or denied.
/// </summary>
public sealed class PermissionGate(IPermissionStore store, IAuditLog auditLog) : IPermissionGate
{
    public async Task<bool> CheckAsync(PermissionSource source, string requester, CancellationToken cancellationToken = default)
    {
        var granted = store.IsGranted(source);
        await auditLog.WriteAsync(AuditEvent.PermissionDecision(source.ToString(), requester, granted), cancellationToken)
            .ConfigureAwait(false);
        return granted;
    }
}
