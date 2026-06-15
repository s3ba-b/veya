using Microsoft.Extensions.Logging.Abstractions;
using Veya.Shared.Notifications;
using Xunit;

namespace Veya.Daemon.Tests;

public class SessionBusNotificationSourceTests
{
    [Fact]
    public async Task ReadAsync_DoesNotThrow_RegardlessOfSessionBus()
    {
        // CI is headless (hard rule #3): no DBUS_SESSION_BUS_ADDRESS, so this
        // yields nothing. On a dev desktop with a session bus, it starts
        // monitoring until cancelled. Either way, this must not throw.
        var hasSessionBus = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS"));

        var source = new SessionBusNotificationSource(NullLogger<SessionBusNotificationSource>.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var notifications = new List<Notification>();
        try
        {
            await foreach (var notification in source.ReadAsync(cts.Token))
            {
                notifications.Add(notification);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected once monitoring is cancelled on a dev desktop.
        }

        if (!hasSessionBus)
        {
            Assert.Empty(notifications);
        }
    }
}
