namespace Veya.McpServer.Tools;

/// <summary>
/// Pure mapping from an <c>org.freedesktop.portal.Request.Response</c> signal
/// (ADR-0013) to a local screenshot file path. No D-Bus dependency, so it is
/// unit-tested without a session bus or portal (hard rule 3).
/// </summary>
public static class PortalScreenshotResponse
{
    /// <summary>
    /// <paramref name="response"/> is the portal's result code: <c>0</c> means
    /// success and <paramref name="results"/> should contain a <c>file://</c>
    /// <c>uri</c> for the screenshot. Any other code means the user cancelled
    /// or declined the prompt. Returns <c>null</c> unless a valid local file
    /// URI is present.
    /// </summary>
    public static string? TryGetFilePath(uint response, IDictionary<string, object> results)
    {
        if (response != 0)
        {
            return null;
        }

        if (!results.TryGetValue("uri", out var value) || value is not string uri)
        {
            return null;
        }

        return Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile
            ? parsed.LocalPath
            : null;
    }
}
