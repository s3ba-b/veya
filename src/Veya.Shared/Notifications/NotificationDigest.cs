namespace Veya.Shared.Notifications;

/// <summary>Number of notifications from one application (ADR-0011 digest).</summary>
public sealed record AppCount(string AppName, int Count);

/// <summary>
/// A deterministic summary of the notification store (ADR-0011): the total, a
/// per-app breakdown, and the highest-priority items. Built without a model
/// call; model-driven natural-language summaries are a follow-up that can take
/// this as input.
/// </summary>
/// <param name="Total">Total notifications considered.</param>
/// <param name="PerApp">Counts per application, busiest first.</param>
/// <param name="Top">The most important items, most urgent then most recent first.</param>
public sealed record NotificationDigest(int Total, IReadOnlyList<AppCount> PerApp, IReadOnlyList<Notification> Top);
