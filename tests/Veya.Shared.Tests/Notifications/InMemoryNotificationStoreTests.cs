using Veya.Shared.Notifications;
using Xunit;

namespace Veya.Shared.Tests.Notifications;

public class InMemoryNotificationStoreTests
{
    private static Notification Note(string id, string app = "App", NotificationUrgency urgency = NotificationUrgency.Normal, int minutesAgo = 0) =>
        new(id, app, $"summary-{id}", $"body-{id}", urgency, DateTimeOffset.UtcNow.AddMinutes(-minutesAgo));

    [Fact]
    public void GetRecent_ReturnsNewestFirst()
    {
        var store = new InMemoryNotificationStore();
        store.Add(Note("a"));
        store.Add(Note("b"));
        store.Add(Note("c"));

        var recent = store.GetRecent(2);

        Assert.Equal(["c", "b"], recent.Select(n => n.Id));
    }

    [Fact]
    public void Add_EvictsOldestPastCapacity()
    {
        var store = new InMemoryNotificationStore(capacity: 2);
        store.Add(Note("a"));
        store.Add(Note("b"));
        store.Add(Note("c"));

        Assert.Equal(2, store.Count);
        Assert.Equal(["c", "b"], store.GetRecent(10).Select(n => n.Id));
    }

    [Fact]
    public void GetByApp_IsCaseInsensitiveAndNewestFirst()
    {
        var store = new InMemoryNotificationStore();
        store.Add(Note("a", app: "Slack"));
        store.Add(Note("b", app: "Email"));
        store.Add(Note("c", app: "slack"));

        var slack = store.GetByApp("SLACK");

        Assert.Equal(["c", "a"], slack.Select(n => n.Id));
    }

    [Fact]
    public void GetRecent_ReturnsEmpty_ForNonPositiveCount()
    {
        var store = new InMemoryNotificationStore();
        store.Add(Note("a"));

        Assert.Empty(store.GetRecent(0));
    }

    [Fact]
    public void Constructor_Throws_OnNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryNotificationStore(0));
    }
}
