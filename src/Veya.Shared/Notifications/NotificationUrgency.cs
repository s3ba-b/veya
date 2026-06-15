namespace Veya.Shared.Notifications;

/// <summary>
/// Notification urgency (ADR-0011), matching the freedesktop notification
/// urgency hint: <c>Low</c> = 0, <c>Normal</c> = 1, <c>Critical</c> = 2.
/// Ordered so higher values prioritise first in a digest.
/// </summary>
public enum NotificationUrgency
{
    Low = 0,
    Normal = 1,
    Critical = 2,
}
