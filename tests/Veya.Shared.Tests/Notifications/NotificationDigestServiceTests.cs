using Veya.Shared.Notifications;
using Veya.Shared.Permissions;
using Veya.Shared.Tests.Context;
using Xunit;

namespace Veya.Shared.Tests.Notifications;

public class NotificationDigestServiceTests
{
    private static Notification Note(string id, string app, NotificationUrgency urgency = NotificationUrgency.Normal, int minutesAgo = 0) =>
        new(id, app, $"summary-{id}", $"body-{id}", urgency, DateTimeOffset.UtcNow.AddMinutes(-minutesAgo));

    private static InMemoryNotificationStore Seeded()
    {
        var store = new InMemoryNotificationStore();
        store.Add(Note("a", "Slack", NotificationUrgency.Normal, minutesAgo: 5));
        store.Add(Note("b", "Slack", NotificationUrgency.Low, minutesAgo: 3));
        store.Add(Note("c", "Email", NotificationUrgency.Critical, minutesAgo: 1));
        return store;
    }

    [Fact]
    public async Task BuildAsync_SummarizesPerAppAndPrioritizesTop_WhenGranted()
    {
        var store = Seeded();
        var audit = new RecordingAuditLog();
        var service = new NotificationDigestService(store, new FakePermissionGate(PermissionSource.Notifications), audit);

        var digest = await service.BuildAsync(topItems: 2);

        Assert.NotNull(digest);
        Assert.Equal(3, digest!.Total);

        // Per-app, busiest first.
        Assert.Equal("Slack", digest.PerApp[0].AppName);
        Assert.Equal(2, digest.PerApp[0].Count);
        Assert.Equal("Email", digest.PerApp[1].AppName);

        // Top: by urgency first — Critical "c", then Normal "a" (outranks Low "b").
        Assert.Equal(["c", "a"], digest.Top.Select(n => n.Id));

        var query = Assert.Single(audit.Events, e => e.EventType == "notification.query");
        Assert.Equal(2, query.Fields["returnedCount"]);
    }

    [Fact]
    public async Task BuildAsync_ReturnsNull_WhenDenied()
    {
        var audit = new RecordingAuditLog();
        var service = new NotificationDigestService(Seeded(), new FakePermissionGate(), audit);

        var digest = await service.BuildAsync();

        Assert.Null(digest);
        Assert.DoesNotContain(audit.Events, e => e.EventType == "notification.query");
    }
}
