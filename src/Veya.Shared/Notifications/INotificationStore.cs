namespace Veya.Shared.Notifications;

/// <summary>
/// A capacity-capped, time-ordered store of recently captured notifications
/// (ADR-0011). Notifications are transient and unbounded, so the oldest drop out
/// once capacity is reached. Permission is enforced by callers, not here.
/// </summary>
public interface INotificationStore
{
    /// <summary>Adds a notification, evicting the oldest if at capacity.</summary>
    public void Add(Notification notification);

    /// <summary>Returns up to <paramref name="count"/> most recent notifications, newest first.</summary>
    public IReadOnlyList<Notification> GetRecent(int count);

    /// <summary>Returns all stored notifications from <paramref name="appName"/>, newest first.</summary>
    public IReadOnlyList<Notification> GetByApp(string appName);

    /// <summary>The number of notifications currently held.</summary>
    public int Count { get; }
}
