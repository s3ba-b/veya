using System.ComponentModel;
using ModelContextProtocol.Server;
using Veya.Shared.Permissions;
using Veya.Shared.Safety;

namespace Veya.McpServer.Tools;

/// <summary>
/// Reads the text currently visible on screen via an on-demand screenshot and
/// local OCR (ADR-0013). Gated by <see cref="IPermissionGate"/>
/// (<see cref="PermissionSource.Screen"/>, default-deny) in addition to the
/// XDG portal's own screenshot prompt. The screenshot is captured to a temp
/// file, OCR'd with <c>tesseract</c>, and deleted immediately — nothing
/// persists, and neither the image nor the extracted text is audit-logged.
/// </summary>
[McpServerToolType]
public sealed class ScreenTool(IScreenCapture screenCapture, ISafeExecutor executor, IPermissionGate permissionGate, IAuditLog auditLog)
{
    public static IReadOnlyDictionary<string, CommandSpec> Allowlist { get; } = new Dictionary<string, CommandSpec>
    {
        // tesseract <image> stdout: OCR the image and write the text to stdout.
        ["tesseract"] = new CommandSpec("/usr/bin/tesseract", args => args.Count == 2 && args[1] == "stdout"),
    };

    [McpServerTool(Name = "read_screen_text")]
    [Description("Reads the text currently visible on screen via a screenshot and OCR. Requires the user to have granted Veya the screen permission and to approve the screenshot prompt; if either is refused, this does nothing and reports that it was denied.")]
    public async Task<string> ReadScreenTextAsync(CancellationToken cancellationToken = default)
    {
        if (!await permissionGate.CheckAsync(PermissionSource.Screen, "read_screen_text", cancellationToken).ConfigureAwait(false))
        {
            return "Screen access is not granted. The user must enable the screen permission before Veya can read on-screen text.";
        }

        var startedAt = DateTimeOffset.UtcNow;
        var path = await screenCapture.CaptureToFileAsync(cancellationToken).ConfigureAwait(false);
        if (path is null)
        {
            await auditLog.WriteAsync(AuditEvent.ScreenCapture(false, 0, DateTimeOffset.UtcNow - startedAt), cancellationToken).ConfigureAwait(false);
            return "Could not capture the screen. The user may have declined the screenshot prompt, or no screenshot portal is available.";
        }

        try
        {
            var result = await executor.RunAsync(new ExecRequest("read_screen_text", "tesseract", [path, "stdout"]), cancellationToken).ConfigureAwait(false);
            var text = result.StandardOutput.Trim();

            await auditLog.WriteAsync(AuditEvent.ScreenCapture(true, text.Length, DateTimeOffset.UtcNow - startedAt), cancellationToken).ConfigureAwait(false);

            return text.Length == 0 ? "No text was detected on the screen." : text;
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
