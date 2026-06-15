namespace Veya.Shared.Notifications;

/// <summary>
/// A captured desktop notification (ADR-0011). Mirrors the salient fields of a
/// freedesktop <c>Notify</c> call. Gated by <c>PermissionSource.Notifications</c>
/// at capture and query.
/// </summary>
/// <param name="Id">Identifier for de-duplication/replacement; unique enough within the recent window.</param>
/// <param name="AppName">The application that raised it (e.g. "Slack").</param>
/// <param name="Summary">The short title line.</param>
/// <param name="Body">The longer body text; may be empty.</param>
/// <param name="Urgency">Priority hint used to order a digest.</param>
/// <param name="Timestamp">When it was captured.</param>
public sealed record Notification(
    string Id,
    string AppName,
    string Summary,
    string Body,
    NotificationUrgency Urgency,
    DateTimeOffset Timestamp);
