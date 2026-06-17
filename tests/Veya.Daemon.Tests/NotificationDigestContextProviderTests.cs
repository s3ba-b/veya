using Veya.Daemon;
using Veya.Shared.Notifications;
using Veya.Shared.Permissions;
using Veya.TestSupport;
using Xunit;

namespace Veya.Daemon.Tests;

public class NotificationDigestContextProviderTests
{
    private sealed class FixedGate(bool granted) : IPermissionGate
    {
        public Task<bool> CheckAsync(PermissionSource source, string requester, CancellationToken cancellationToken = default) =>
            Task.FromResult(granted);
    }

    private static Notification Note(string id, string app, NotificationUrgency urgency = NotificationUrgency.Normal) =>
        new(id, app, $"summary-{id}", $"body-{id}", urgency, DateTimeOffset.UtcNow);

    [Fact]
    public async Task GetContextBlockAsync_ReturnsNull_WhenDenied()
    {
        var store = new InMemoryNotificationStore();
        store.Add(Note("a", "Slack"));
        var digestService = new NotificationDigestService(store, new FixedGate(false), new RecordingAuditLog());
        var provider = new NotificationDigestContextProvider(digestService);

        var block = await provider.GetContextBlockAsync("what did I miss?");

        Assert.Null(block);
    }

    [Fact]
    public async Task GetContextBlockAsync_ReturnsNull_WhenStoreEmpty()
    {
        var store = new InMemoryNotificationStore();
        var digestService = new NotificationDigestService(store, new FixedGate(true), new RecordingAuditLog());
        var provider = new NotificationDigestContextProvider(digestService);

        var block = await provider.GetContextBlockAsync("what did I miss?");

        Assert.Null(block);
    }

    [Fact]
    public async Task GetContextBlockAsync_FormatsPerAppAndTopItems_WhenGrantedAndNonEmpty()
    {
        var store = new InMemoryNotificationStore();
        store.Add(Note("a", "Slack"));
        store.Add(Note("b", "Slack"));
        store.Add(Note("c", "Email", NotificationUrgency.Critical));
        var digestService = new NotificationDigestService(store, new FixedGate(true), new RecordingAuditLog());
        var provider = new NotificationDigestContextProvider(digestService);

        var block = await provider.GetContextBlockAsync("what did I miss?");

        Assert.NotNull(block);
        Assert.Contains("3 total", block);
        Assert.Contains("Slack: 2", block);
        Assert.Contains("Email: 1", block);
        Assert.Contains("[Critical] Email: summary-c", block);
    }
}
