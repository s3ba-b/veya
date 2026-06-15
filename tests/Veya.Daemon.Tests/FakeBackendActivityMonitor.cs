using Veya.Shared;

namespace Veya.Daemon.Tests;

/// <summary>
/// Test double for <see cref="IBackendActivityMonitor"/>: lets a test drive the
/// <see cref="CloudRequested"/> event and the reported active backend by hand.
/// </summary>
internal sealed class FakeBackendActivityMonitor(string activeBackend = "ollama") : IBackendActivityMonitor
{
    public event Action<CloudUsageInfo>? CloudRequested;

    public string ActiveBackend { get; set; } = activeBackend;

    public void RaiseCloudRequested(CloudUsageInfo info) => CloudRequested?.Invoke(info);
}
