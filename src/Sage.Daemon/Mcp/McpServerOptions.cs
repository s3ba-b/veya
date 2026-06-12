namespace Sage.Daemon.Mcp;

/// <summary>
/// Configures how the Daemon launches the Sage.McpServer child process over
/// stdio (docs/security.md: unprivileged, no elevated transport). Bound from
/// the "Mcp" configuration section, e.g. the <c>Mcp__ServerPath</c>
/// environment variable.
/// </summary>
public sealed class McpServerOptions
{
    /// <summary>
    /// Path to the Sage.McpServer executable. Defaults to a sibling of the
    /// Daemon's own executable, matching the packaging layout in
    /// packaging/systemd/sage-daemon.service.
    /// </summary>
    public string? ServerPath { get; set; }

    /// <summary>Extra arguments passed to the Sage.McpServer process.</summary>
    public IReadOnlyList<string> Arguments { get; set; } = [];
}
