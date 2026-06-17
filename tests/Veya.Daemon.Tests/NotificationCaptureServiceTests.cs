using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Veya.Daemon;
using Veya.Shared.Notifications;
using Veya.Shared.Permissions;
using Veya.TestSupport;
using Xunit;

namespace Veya.Daemon.Tests;

public class NotificationCaptureServiceTests
{
    private sealed class FixedGate(bool granted) : IPermissionGate
    {
        public Task<bool> CheckAsync(PermissionSource source, string requester, CancellationToken cancellationToken = default) =>
            Task.FromResult(granted);
    }

    private sealed class ListSource(params Notification[] notifications) : INotificationSource
    {
        public async IAsyncEnumerable<Notification> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var notification in notifications)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return notification;
                await Task.Yield();
            }
        }
    }

    private static Notification Note(string id) =>
        new(id, "App", $"summary-{id}", "body", NotificationUrgency.Normal, DateTimeOffset.UtcNow);

    [Fact]
    public async Task RunAsync_CapturesIntoStoreAndAudits_WhenGranted()
    {
        var store = new InMemoryNotificationStore();
        var audit = new RecordingAuditLog();
        var service = new NotificationCaptureService(
            new ListSource(Note("a"), Note("b")), store, new FixedGate(true), audit, NullLogger<NotificationCaptureService>.Instance);

        await service.RunAsync(CancellationToken.None);

        Assert.Equal(2, store.Count);
        Assert.Equal(2, audit.Events.Count(e => e.EventType == "notification.capture"));
    }

    [Fact]
    public async Task RunAsync_CapturesNothing_WhenDenied()
    {
        var store = new InMemoryNotificationStore();
        var audit = new RecordingAuditLog();
        var service = new NotificationCaptureService(
            new ListSource(Note("a")), store, new FixedGate(false), audit, NullLogger<NotificationCaptureService>.Instance);

        await service.RunAsync(CancellationToken.None);

        Assert.Equal(0, store.Count);
        Assert.DoesNotContain(audit.Events, e => e.EventType == "notification.capture");
    }
}
