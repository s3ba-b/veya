namespace Veya.Shared.Permissions;

/// <summary>
/// The single checkpoint every permission-gated action passes through. It
/// consults an <see cref="IPermissionStore"/> and audit-logs the decision
/// (<c>permission.decision</c>), so "decisions are audit-logged"
/// (docs/security.md) is enforced centrally rather than in each tool.
/// </summary>
public interface IPermissionGate
{
    /// <summary>
    /// Returns whether <paramref name="source"/> is granted, writing a
    /// <c>permission.decision</c> audit event for the request either way.
    /// </summary>
    /// <param name="source">The source being accessed.</param>
    /// <param name="requester">The tool or component asking, for the audit log.</param>
    public Task<bool> CheckAsync(PermissionSource source, string requester, CancellationToken cancellationToken = default);
}
