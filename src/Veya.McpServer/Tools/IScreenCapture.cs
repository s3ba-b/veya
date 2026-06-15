namespace Veya.McpServer.Tools;

/// <summary>
/// Captures a single screenshot to a temporary file (ADR-0013). Returns the
/// local file path, or <c>null</c> if capture is unavailable (no session bus,
/// no portal, or the user declined the screenshot prompt) — the caller treats
/// either case the same way.
/// </summary>
public interface IScreenCapture
{
    public Task<string?> CaptureToFileAsync(CancellationToken cancellationToken = default);
}
