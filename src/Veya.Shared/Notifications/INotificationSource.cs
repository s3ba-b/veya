namespace Veya.Shared.Notifications;

/// <summary>
/// A stream of incoming desktop notifications (ADR-0011). The real
/// <c>org.freedesktop.Notifications</c> session-bus implementation is a deferred
/// follow-up; this abstraction keeps the capture pipeline desktop-free and
/// headless-testable (hard rule 3).
/// </summary>
public interface INotificationSource
{
    /// <summary>
    /// Yields notifications as they arrive, until cancelled. Called only after
    /// the <c>Notifications</c> permission is granted (the capture service gates
    /// it), so implementations need not re-check it.
    /// </summary>
    public IAsyncEnumerable<Notification> ReadAsync(CancellationToken cancellationToken = default);
}
