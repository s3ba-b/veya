namespace Veya.Shared.Context;

/// <summary>
/// Resolves the default personal-context index location (ADR-0009):
/// <c>$XDG_DATA_HOME/veya/context.db</c>, falling back to
/// <c>~/.local/share/veya/context.db</c>. Parallels
/// <see cref="Veya.Shared.Safety.AuditPaths"/> but under the data dir, since the
/// index is user data rather than state/logs.
/// </summary>
public static class ContextPaths
{
    public static string DefaultDatabasePath()
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var baseDir = string.IsNullOrEmpty(dataHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
            : dataHome;
        return Path.Combine(baseDir, "veya", "context.db");
    }
}
