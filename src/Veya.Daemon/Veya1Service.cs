using System.Reflection;
using Tmds.DBus;
using Veya.Shared;
using Veya.Shared.Inference;

namespace Veya.Daemon;

/// <summary>
/// Implementation of org.veya.Veya1. <see cref="AskAsync"/> routes the prompt
/// through the <see cref="IModelRouter"/> (model router); <see cref="GetStatusAsync"/>
/// and the <c>CloudUsage</c> signal surface backend activity from
/// <see cref="IBackendActivityMonitor"/>. Real session/context management arrives
/// in later issues.
/// </summary>
public sealed class Veya1Service : IVeya1
{
    private static readonly string Version =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    private readonly IModelRouter _modelRouter;
    private readonly IBackendActivityMonitor _activity;

    public Veya1Service(IModelRouter modelRouter, IBackendActivityMonitor activity)
    {
        _modelRouter = modelRouter;
        _activity = activity;
        _activity.CloudRequested += info => CloudUsage?.Invoke(info);
    }

    /// <summary>
    /// Backs the D-Bus <c>CloudUsage</c> signal: Tmds.DBus discovers it by name and
    /// emits when it is raised, and <see cref="WatchCloudUsageAsync"/> subscribes bus
    /// clients to it. Public because Tmds.DBus reflects for a public event member.
    /// </summary>
    public event Action<CloudUsageInfo>? CloudUsage;

    public ObjectPath ObjectPath => new(VeyaDBus.ObjectPath);

    public async Task<string> AskAsync(string prompt)
    {
        try
        {
            return await _modelRouter.AskAsync(prompt).ConfigureAwait(false);
        }
        catch (BackendUnavailableException ex)
        {
            return $"Veya can't reach its model backend right now: {ex.Message}";
        }
    }

    public Task<IDictionary<string, object>> GetStatusAsync()
    {
        IDictionary<string, object> status = new Dictionary<string, object>
        {
            ["version"] = Version,
            ["activeBackend"] = _activity.ActiveBackend,
        };
        return Task.FromResult(status);
    }

    public Task<IDisposable> WatchCloudUsageAsync(Action<CloudUsageInfo> handler) =>
        SignalWatcher.AddAsync(this, nameof(CloudUsage), handler);
}
