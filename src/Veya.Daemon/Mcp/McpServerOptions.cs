namespace Veya.Daemon.Mcp;

/// <summary>
/// Configures how the Daemon launches the Veya.McpServer child process over
/// stdio (docs/security.md: unprivileged, no elevated transport). Bound from
/// the "Mcp" configuration section, e.g. the <c>Mcp__ServerPath</c>
/// environment variable.
/// </summary>
public sealed class McpServerOptions
{
    /// <summary>
    /// Path to the Veya.McpServer executable. Defaults to a sibling of the
    /// Daemon's own executable, matching the packaging layout in
    /// packaging/systemd/veya-daemon.service.
    /// </summary>
    public string? ServerPath { get; set; }

    /// <summary>Extra arguments passed to the Veya.McpServer process.</summary>
    public IReadOnlyList<string> Arguments { get; set; } = [];
}
