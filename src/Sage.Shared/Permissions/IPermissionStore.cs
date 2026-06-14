namespace Sage.Shared.Permissions;

/// <summary>
/// Records which <see cref="PermissionSource"/>s the user has granted. A pure
/// lookup with no side effects — auditing of decisions is the responsibility
/// of <see cref="IPermissionGate"/>. Implementations must default to deny for
/// any source the user has not explicitly granted.
/// </summary>
public interface IPermissionStore
{
    /// <summary>True only if the user has explicitly granted <paramref name="source"/>.</summary>
    public bool IsGranted(PermissionSource source);
}
