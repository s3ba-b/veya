using System.Text;
using Veya.Shared.Notifications;

namespace Veya.Daemon;

/// <summary>
/// <see cref="IContextProvider"/> backed by the notification digest (ADR-0011):
/// folds the per-app counts and highest-priority recent notifications into a
/// prompt block, so <c>Ask</c> can answer questions like "what did I miss?".
/// Returns <c>null</c> when the <c>Notifications</c> permission is denied or
/// there is nothing captured yet, matching <see cref="ContextRetrievalProvider"/>'s
/// "no context" convention.
/// </summary>
public sealed class NotificationDigestContextProvider(NotificationDigestService digestService, int topItems = 5) : IContextProvider
{
    public async Task<string?> GetContextBlockAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var digest = await digestService.BuildAsync(topItems, cancellationToken).ConfigureAwait(false);
        if (digest is null || digest.Total == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Recent desktop notifications ({digest.Total} total):");
        foreach (var appCount in digest.PerApp)
        {
            builder.Append("- ").Append(appCount.AppName).Append(": ").Append(appCount.Count).AppendLine();
        }

        builder.AppendLine("Most important:");
        foreach (var notification in digest.Top)
        {
            builder.Append("- [").Append(notification.Urgency).Append("] ").Append(notification.AppName).Append(": ").AppendLine(notification.Summary);
        }

        return builder.ToString().TrimEnd();
    }
}
