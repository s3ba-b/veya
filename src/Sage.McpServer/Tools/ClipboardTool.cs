using System.ComponentModel;
using ModelContextProtocol.Server;
using Sage.Shared.Permissions;
using Sage.Shared.Safety;

namespace Sage.McpServer.Tools;

/// <summary>
/// The first non-read-only tool: writes text to the system clipboard. Gated by
/// <see cref="IPermissionGate"/> (<see cref="PermissionSource.Clipboard"/>,
/// default-deny — ADR-0005) and executed through <see cref="ISafeExecutor"/>
/// (<c>wl-copy</c> on Wayland, <c>xclip</c> on X11 — ADR-0006). The text is
/// passed via stdin, so the clipboard content never appears in the audit log.
/// </summary>
[McpServerToolType]
public sealed class ClipboardTool(ISafeExecutor executor, IPermissionGate permissionGate)
{
    public static IReadOnlyDictionary<string, CommandSpec> Allowlist { get; } = new Dictionary<string, CommandSpec>
    {
        // wl-copy reads the clipboard content from stdin; no arguments needed.
        ["wl-copy"] = new CommandSpec("/usr/bin/wl-copy", args => args.Count == 0),
        // xclip targets the CLIPBOARD selection (not PRIMARY) and reads stdin.
        ["xclip"] = new CommandSpec("/usr/bin/xclip", args => args is ["-selection", "clipboard"]),
    };

    [McpServerTool(Name = "set_clipboard")]
    [Description("Writes text to the user's system clipboard. Requires the user to have granted Sage the clipboard permission; if not granted, this does nothing and reports that it was denied.")]
    public async Task<string> SetClipboardAsync(
        [Description("The text to place on the clipboard.")] string text,
        CancellationToken cancellationToken = default)
    {
        if (!await permissionGate.CheckAsync(PermissionSource.Clipboard, "set_clipboard", cancellationToken))
        {
            return "Clipboard access is not granted. The user must enable the clipboard permission before Sage can write to it.";
        }

        var (binary, arguments) = SelectBackend();
        await executor.RunAsync(new ExecRequest("set_clipboard", binary, arguments, StandardInput: text), cancellationToken);
        return "Copied the text to the clipboard.";
    }

    private static (string Binary, IReadOnlyList<string> Arguments) SelectBackend() =>
        IsWaylandSession()
            ? ("wl-copy", Array.Empty<string>())
            : ("xclip", ["-selection", "clipboard"]);

    private static bool IsWaylandSession() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
        || string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);
}
