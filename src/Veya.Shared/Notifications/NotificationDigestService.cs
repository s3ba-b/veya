using Veya.Shared.Permissions;
using Veya.Shared.Safety;

namespace Veya.Shared.Notifications;

/// <summary>
/// Builds a deterministic <see cref="NotificationDigest"/> from the store
/// (ADR-0011), gating the <c>Notifications</c> permission at query time and
/// audit-logging the read (counts only). The "prioritize" half of notification
/// intelligence; the model-driven "summarize/answer" half is a follow-up.
/// </summary>
public sealed class NotificationDigestService(INotificationStore store, IPermissionGate permissionGate, IAuditLog auditLog)
{
    /// <summary>Requester string recorded in permission and audit events for digest reads.</summary>
    public const string Requester = "notification.query";

    /// <summary>
    /// Returns a digest with up to <paramref name="topItems"/> highest-priority
    /// notifications, or <c>null</c> if the <c>Notifications</c> permission is
    /// denied (the gate has logged the decision).
    /// </summary>
    public async Task<NotificationDigest?> BuildAsync(int topItems = 10, CancellationToken cancellationToken = default)
    {
        var granted = await permissionGate.CheckAsync(PermissionSource.Notifications, Requester, cancellationToken).ConfigureAwait(false);
        if (!granted)
        {
            return null;
        }

        var startedAt = DateTimeOffset.UtcNow;

        var all = store.GetRecent(store.Count);

        var perApp = all
            .GroupBy(notification => notification.AppName)
            .Select(group => new AppCount(group.Key, group.Count()))
            .OrderByDescending(appCount => appCount.Count)
            .ThenBy(appCount => appCount.AppName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var top = all
            .OrderByDescending(notification => notification.Urgency)
            .ThenByDescending(notification => notification.Timestamp)
            .Take(topItems)
            .ToList();

        var digest = new NotificationDigest(all.Count, perApp, top);

        await auditLog.WriteAsync(AuditEvent.NotificationQuery(top.Count, DateTimeOffset.UtcNow - startedAt), cancellationToken)
            .ConfigureAwait(false);

        return digest;
    }
}
