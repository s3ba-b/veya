using System.Diagnostics;
using Veya.Shared.Notifications;
using Veya.Shared.Permissions;
using Veya.Shared.Safety;

namespace Veya.Daemon;

/// <summary>
/// Streams desktop notifications from an <see cref="INotificationSource"/> into
/// the recent store (ADR-0011), gated by the <c>Notifications</c> permission and
/// audit-logged (counts only). Runs off the critical path: a denied permission
/// captures nothing, and source failures are logged, never fatal.
/// </summary>
public sealed class NotificationCaptureService(
    INotificationSource source,
    INotificationStore store,
    IPermissionGate permissionGate,
    IAuditLog auditLog,
    ILogger<NotificationCaptureService> logger) : BackgroundService
{
    /// <summary>Requester string recorded in permission and audit events for capture.</summary>
    public const string Requester = "notification.capture";

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => RunAsync(stoppingToken);

    /// <summary>Captures notifications until cancelled. Exposed for tests.</summary>
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        var granted = await permissionGate.CheckAsync(PermissionSource.Notifications, Requester, stoppingToken).ConfigureAwait(false);
        if (!granted)
        {
            logger.LogInformation("Notification capture disabled: permission denied.");
            return;
        }

        try
        {
            await foreach (var notification in source.ReadAsync(stoppingToken).ConfigureAwait(false))
            {
                var stopwatch = Stopwatch.StartNew();
                store.Add(notification);
                stopwatch.Stop();
                await auditLog.WriteAsync(AuditEvent.NotificationCapture(1, stopwatch.Elapsed), stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Notification capture stopped on error.");
        }
    }
}
