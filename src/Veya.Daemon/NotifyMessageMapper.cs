using System.Globalization;
using Veya.Shared.Notifications;

namespace Veya.Daemon;

/// <summary>
/// Maps the arguments of a freedesktop <c>org.freedesktop.Notifications.Notify</c>
/// call (ADR-0012) to a <see cref="Notification"/>. Pure and D-Bus-free so it can
/// be unit-tested without a session bus (hard rule 3).
/// </summary>
public static class NotifyMessageMapper
{
    /// <summary>
    /// Maps <c>Notify(app_name, replaces_id, app_icon, summary, body, actions, hints, expire_timeout)</c>
    /// to a <see cref="Notification"/>. <paramref name="appIcon"/>, <paramref name="actions"/>
    /// and <paramref name="expireTimeout"/> are accepted to mirror the D-Bus
    /// signature but are not currently represented on <see cref="Notification"/>.
    /// </summary>
    public static Notification Map(
        string appName,
        uint replacesId,
        string appIcon,
        string summary,
        string body,
        string[] actions,
        IDictionary<string, object> hints,
        int expireTimeout)
    {
        var id = replacesId != 0
            ? replacesId.ToString(CultureInfo.InvariantCulture)
            : Guid.NewGuid().ToString("N");

        return new Notification(id, appName, summary, body, ResolveUrgency(hints), DateTimeOffset.UtcNow);
    }

    private static NotificationUrgency ResolveUrgency(IDictionary<string, object> hints)
    {
        if (hints.TryGetValue("urgency", out var value) && value is byte urgency && urgency is 0 or 1 or 2)
        {
            return (NotificationUrgency)urgency;
        }

        return NotificationUrgency.Normal;
    }
}
