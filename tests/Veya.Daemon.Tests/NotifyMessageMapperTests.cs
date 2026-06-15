using Veya.Daemon;
using Veya.Shared.Notifications;
using Xunit;

namespace Veya.Daemon.Tests;

public class NotifyMessageMapperTests
{
    private static readonly Dictionary<string, object> NoHints = [];

    [Fact]
    public void Map_UsesReplacesId_WhenNonZero()
    {
        var notification = NotifyMessageMapper.Map("Slack", 42, "icon", "Summary", "Body", [], NoHints, -1);

        Assert.Equal("42", notification.Id);
        Assert.Equal("Slack", notification.AppName);
        Assert.Equal("Summary", notification.Summary);
        Assert.Equal("Body", notification.Body);
    }

    [Fact]
    public void Map_GeneratesId_WhenReplacesIdIsZero()
    {
        var first = NotifyMessageMapper.Map("Slack", 0, "icon", "Summary", "Body", [], NoHints, -1);
        var second = NotifyMessageMapper.Map("Slack", 0, "icon", "Summary", "Body", [], NoHints, -1);

        Assert.NotEqual("0", first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData((byte)0, NotificationUrgency.Low)]
    [InlineData((byte)1, NotificationUrgency.Normal)]
    [InlineData((byte)2, NotificationUrgency.Critical)]
    public void Map_TranslatesUrgencyHint(byte hintValue, NotificationUrgency expected)
    {
        var hints = new Dictionary<string, object> { ["urgency"] = hintValue };

        var notification = NotifyMessageMapper.Map("App", 1, "icon", "Summary", "Body", [], hints, -1);

        Assert.Equal(expected, notification.Urgency);
    }

    [Fact]
    public void Map_DefaultsToNormalUrgency_WhenHintAbsent()
    {
        var notification = NotifyMessageMapper.Map("App", 1, "icon", "Summary", "Body", [], NoHints, -1);

        Assert.Equal(NotificationUrgency.Normal, notification.Urgency);
    }

    [Fact]
    public void Map_DefaultsToNormalUrgency_WhenHintUnparseable()
    {
        var hints = new Dictionary<string, object> { ["urgency"] = "high" };

        var notification = NotifyMessageMapper.Map("App", 1, "icon", "Summary", "Body", [], hints, -1);

        Assert.Equal(NotificationUrgency.Normal, notification.Urgency);
    }

    [Fact]
    public void Map_SetsTimestampToNow()
    {
        var before = DateTimeOffset.UtcNow;
        var notification = NotifyMessageMapper.Map("App", 1, "icon", "Summary", "Body", [], NoHints, -1);
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(notification.Timestamp, before, after);
    }
}
