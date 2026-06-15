using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Tmds.DBus;
using Veya.Shared.Notifications;

namespace Veya.Daemon;

/// <summary>
/// Real <see cref="INotificationSource"/> (ADR-0012): becomes a session-bus
/// monitor scoped to <c>org.freedesktop.Notifications.Notify</c> calls and maps
/// each one to a <see cref="Notification"/> via <see cref="NotifyMessageMapper"/>.
/// Observation-only — does not own or replace <c>org.freedesktop.Notifications</c>,
/// so the desktop's own notifications keep working unchanged.
/// </summary>
/// <remarks>
/// Connects lazily, only when <see cref="ReadAsync"/> is enumerated (the
/// <see cref="NotificationCaptureService"/> only does so once the
/// <c>Notifications</c> permission is granted). If there is no session bus, or
/// becoming a monitor fails for any reason (older bus, policy denial, Tmds.DBus
/// proxy limitations), this logs and yields nothing — the same graceful
/// degradation as <see cref="DBusSessionConnector"/> (hard rule 3).
/// </remarks>
public sealed class SessionBusNotificationSource(ILogger<SessionBusNotificationSource> logger) : INotificationSource
{
    private const string NotificationsInterface = "org.freedesktop.Notifications";
    private const string MatchRule = "type='method_call',interface='" + NotificationsInterface + "',member='Notify'";
    private const string DBusBusName = "org.freedesktop.DBus";
    private static readonly ObjectPath NotificationsPath = new("/org/freedesktop/Notifications");
    private static readonly ObjectPath DBusPath = new("/org/freedesktop/DBus");

    public async IAsyncEnumerable<Notification> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var address = Address.Session;
        if (string.IsNullOrEmpty(address))
        {
            logger.LogInformation("No D-Bus session bus available; notification capture disabled.");
            yield break;
        }

        var channel = Channel.CreateBounded<Notification>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        });

        Connection? connection = null;
        try
        {
            connection = new Connection(address);
            await connection.ConnectAsync().ConfigureAwait(false);
            await connection.RegisterObjectAsync(new NotifyMonitorObject(channel.Writer)).ConfigureAwait(false);
            var monitoring = connection.CreateProxy<IDBusMonitoring>(DBusBusName, DBusPath);
            await monitoring.BecomeMonitorAsync([MatchRule], 0).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to start notification monitoring on the session bus.");
            connection?.Dispose();
            yield break;
        }

        logger.LogInformation("Monitoring {Interface}.Notify on the session bus.", NotificationsInterface);
        try
        {
            await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return notification;
            }
        }
        finally
        {
            connection.Dispose();
        }
    }

    /// <summary>The bus driver's <c>org.freedesktop.DBus.Monitoring</c> interface.</summary>
    [DBusInterface("org.freedesktop.DBus.Monitoring")]
    internal interface IDBusMonitoring : IDBusObject
    {
        public Task BecomeMonitorAsync(string[] rules, uint flags);
    }

    /// <summary>
    /// The <c>org.freedesktop.Notifications</c> interface, as far as <c>Notify</c>
    /// is concerned. Registered at <see cref="NotificationsPath"/> so that, once
    /// this connection becomes a monitor, forwarded <c>Notify</c> calls are
    /// dispatched here.
    /// </summary>
    [DBusInterface(NotificationsInterface)]
    internal interface IFreedesktopNotifications : IDBusObject
    {
        public Task<uint> NotifyAsync(string appName, uint replacesId, string appIcon, string summary, string body, string[] actions, IDictionary<string, object> hints, int expireTimeout);
    }

    /// <summary>
    /// Maps every observed <c>Notify</c> call to the channel via
    /// <see cref="NotifyMessageMapper"/>. The return value is never delivered to
    /// the real caller (this connection is a monitor, not the notification
    /// server), so any fixed value is fine.
    /// </summary>
    private sealed class NotifyMonitorObject(ChannelWriter<Notification> writer) : IFreedesktopNotifications
    {
        public ObjectPath ObjectPath => NotificationsPath;

        public Task<uint> NotifyAsync(string appName, uint replacesId, string appIcon, string summary, string body, string[] actions, IDictionary<string, object> hints, int expireTimeout)
        {
            writer.TryWrite(NotifyMessageMapper.Map(appName, replacesId, appIcon, summary, body, actions, hints, expireTimeout));
            return Task.FromResult(0u);
        }
    }
}
