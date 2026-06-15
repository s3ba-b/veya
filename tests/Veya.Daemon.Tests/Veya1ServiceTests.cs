using Tmds.DBus;
using Veya.Shared;
using Veya.Shared.Inference;
using Xunit;

namespace Veya.Daemon.Tests;

public class Veya1ServiceTests
{
    private sealed class FakeModelRouter(Func<string, Task<string>> respond) : IModelRouter
    {
        public Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default) => respond(prompt);
    }

    private static Veya1Service Service(IModelRouter router, IBackendActivityMonitor? monitor = null) =>
        new(router, monitor ?? new FakeBackendActivityMonitor());

    [Fact]
    public async Task AskAsync_ReturnsRouterReply()
    {
        var service = Service(new FakeModelRouter(prompt => Task.FromResult($"Veya received: {prompt}")));

        var reply = await service.AskAsync("ping");

        Assert.Equal("Veya received: ping", reply);
    }

    [Fact]
    public async Task AskAsync_WhenBackendUnavailable_ReturnsErrorMessageInsteadOfThrowing()
    {
        var service = Service(new FakeModelRouter(_ => throw new BackendUnavailableException("no API key configured")));

        var reply = await service.AskAsync("ping");

        Assert.Contains("no API key configured", reply);
    }

    [Fact]
    public void ObjectPath_MatchesDocumentedContract()
    {
        var service = Service(new FakeModelRouter(prompt => Task.FromResult(prompt)));

        Assert.Equal(new ObjectPath(VeyaDBus.ObjectPath), service.ObjectPath);
    }

    [Fact]
    public async Task GetStatusAsync_ReportsVersionAndActiveBackend()
    {
        var monitor = new FakeBackendActivityMonitor("mistral");
        var service = Service(new FakeModelRouter(prompt => Task.FromResult(prompt)), monitor);

        var status = await service.GetStatusAsync();

        Assert.Equal("mistral", status["activeBackend"]);
        Assert.False(string.IsNullOrEmpty((string)status["version"]));
    }

    [Fact]
    public void CloudUsage_ForwardsMonitorActivityToSignalSubscribers()
    {
        // The bus-marshaling path (WatchCloudUsageAsync -> SignalWatcher) needs a real
        // connection, so headless (hard rule #3) we verify the in-process seam: a cloud
        // request observed by the monitor is re-raised on the CloudUsage signal event.
        var monitor = new FakeBackendActivityMonitor();
        var service = Service(new FakeModelRouter(prompt => Task.FromResult(prompt)), monitor);

        CloudUsageInfo? received = null;
        service.CloudUsage += info => received = info;

        monitor.RaiseCloudRequested(new CloudUsageInfo { Backend = "mistral", Model = "mistral-large", InputTokens = 12, OutputTokens = 34 });

        Assert.NotNull(received);
        Assert.Equal("mistral", received!.Value.Backend);
        Assert.Equal("mistral-large", received.Value.Model);
        Assert.Equal(12u, received.Value.InputTokens);
        Assert.Equal(34u, received.Value.OutputTokens);
    }
}
