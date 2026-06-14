namespace Veya.Shared.Permissions;

/// <summary>
/// Default <see cref="IPermissionStore"/>: an immutable map of explicit grants.
/// Any source absent from the map — or present and set to <c>false</c> — is
/// denied, so the store is default-deny (docs/security.md). The map is
/// typically bound from configuration by the host (see McpServer
/// <c>Program.cs</c>); this type stays free of any configuration framework so
/// it lives in <c>Veya.Shared</c>.
/// </summary>
public sealed class PermissionStore(IReadOnlyDictionary<PermissionSource, bool>? grants = null) : IPermissionStore
{
    private readonly IReadOnlyDictionary<PermissionSource, bool> _grants =
        grants ?? new Dictionary<PermissionSource, bool>();

    public bool IsGranted(PermissionSource source) =>
        _grants.TryGetValue(source, out var granted) && granted;
}
