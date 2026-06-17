using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;

namespace Veya.Shared.Context;

/// <summary>
/// Loads the <c>sqlite-vec</c> native extension (ADR-0009) into a SQLite
/// connection. Kept separate from <see cref="SqliteContextStore"/> so the store
/// owns query logic only and this owns native-asset/RID resolution (SRP).
/// </summary>
internal static class SqliteVecExtension
{
    public static void Load(SqliteConnection connection)
    {
        // sqlite-vec deploys the native library under runtimes/<package-rid>/native
        // (e.g. linux-x64), keyed by the package's RID rather than the running
        // RID (ubuntu.26.04-x64). Try the bare name first in case the loader path
        // already covers it, then fall back to the explicit deployed path. The
        // path is passed without its suffix because SQLite appends the
        // platform-specific one itself.
        try
        {
            connection.LoadExtension("vec0");
        }
        catch (SqliteException)
        {
            connection.LoadExtension(ResolveVecPath());
        }
    }

    private static string ResolveVecPath()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            var other => throw new PlatformNotSupportedException($"sqlite-vec has no native asset for {other}."),
        };

        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
            : "linux";

        // No suffix: SQLite appends .so/.dylib/.dll for the platform.
        return Path.Combine(AppContext.BaseDirectory, "runtimes", $"{os}-{arch}", "native", "vec0");
    }
}
