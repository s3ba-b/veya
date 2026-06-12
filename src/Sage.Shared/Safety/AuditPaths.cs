namespace Sage.Shared.Safety;

/// <summary>
/// Resolves the default audit log location per docs/security.md:
/// <c>$XDG_STATE_HOME/sage/audit/</c>, falling back to
/// <c>~/.local/state/sage/audit/</c>.
/// </summary>
public static class AuditPaths
{
    public static string DefaultDirectory()
    {
        var stateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
        var baseDir = string.IsNullOrEmpty(stateHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state")
            : stateHome;
        return Path.Combine(baseDir, "sage", "audit");
    }
}
