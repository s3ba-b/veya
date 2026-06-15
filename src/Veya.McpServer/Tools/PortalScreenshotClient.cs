using Microsoft.Extensions.Logging;
using Tmds.DBus;

namespace Veya.McpServer.Tools;

/// <summary>
/// Real <see cref="IScreenCapture"/> (ADR-0013): calls the XDG Desktop Portal's
/// <c>org.freedesktop.portal.Screenshot</c> on the session bus, which shows the
/// user a native screenshot prompt (a second consent layer on top of the
/// <c>Screen</c> permission), and resolves to the resulting temp file's path.
/// </summary>
/// <remarks>
/// If there is no session bus, the portal is unavailable, the call fails, or
/// the user declines the prompt, this returns <c>null</c> (hard rule 3). Not
/// exercised in CI; <see cref="PortalScreenshotResponse"/> carries the
/// testable logic.
/// </remarks>
public sealed class PortalScreenshotClient(ILogger<PortalScreenshotClient> logger) : IScreenCapture
{
    private const string PortalBusName = "org.freedesktop.portal.Desktop";
    private static readonly ObjectPath PortalPath = new("/org/freedesktop/portal/desktop");
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(30);

    public async Task<string?> CaptureToFileAsync(CancellationToken cancellationToken = default)
    {
        var address = Address.Session;
        if (string.IsNullOrEmpty(address))
        {
            logger.LogInformation("No D-Bus session bus available; screen capture disabled.");
            return null;
        }

        try
        {
            using var connection = new Connection(address);
            await connection.ConnectAsync().ConfigureAwait(false);

            var screenshot = connection.CreateProxy<IScreenshotPortal>(PortalBusName, PortalPath);
            var requestPath = await screenshot.ScreenshotAsync(string.Empty, new Dictionary<string, object> { ["interactive"] = false }).ConfigureAwait(false);

            var request = connection.CreateProxy<IPortalRequest>(PortalBusName, requestPath);
            var responseTcs = new TaskCompletionSource<(uint Response, IDictionary<string, object> Results)>();
            using var subscription = await request.WatchResponseAsync(args => responseTcs.TrySetResult(args)).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ResponseTimeout);
            using var registration = timeoutCts.Token.Register(() => responseTcs.TrySetCanceled());

            var (response, results) = await responseTcs.Task.ConfigureAwait(false);
            return PortalScreenshotResponse.TryGetFilePath(response, results);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Screen capture via the XDG portal failed.");
            return null;
        }
    }

    /// <summary>The <c>org.freedesktop.portal.Screenshot</c> interface.</summary>
    [DBusInterface("org.freedesktop.portal.Screenshot")]
    internal interface IScreenshotPortal : IDBusObject
    {
        public Task<ObjectPath> ScreenshotAsync(string parentWindow, IDictionary<string, object> options);
    }

    /// <summary>The <c>org.freedesktop.portal.Request</c> interface for a pending portal call.</summary>
    [DBusInterface("org.freedesktop.portal.Request")]
    internal interface IPortalRequest : IDBusObject
    {
        public Task<IDisposable> WatchResponseAsync(Action<(uint Response, IDictionary<string, object> Results)> handler);
    }
}
