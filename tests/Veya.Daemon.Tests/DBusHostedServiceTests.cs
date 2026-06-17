using Microsoft.Extensions.Logging.Abstractions;
using Tmds.DBus;
using Veya.Daemon.Voice;
using Xunit;

namespace Veya.Daemon.Tests;

public class DBusHostedServiceTests
{
    private sealed class FakeConnector(bool result) : IDBusSessionConnector
    {
        public Task<bool> TryRegisterAsync(IDBusObject service) => Task.FromResult(result);
    }


    private sealed class FakeVoiceAskService : IVoiceAskService
    {
        public Task<(string Transcript, string Reply)> AskAsync(uint maxDurationMs, CancellationToken cancellationToken = default) =>
            Task.FromResult((string.Empty, string.Empty));
    }

    [Fact]
    public async Task StartAsync_WhenSessionBusAvailable_CompletesSuccessfully()
    {
        var hostedService = new DBusHostedService(new FakeConnector(true), new Veya1Service(new FakeModelRouter(prompt => Task.FromResult($"Veya received: {prompt}")), new FakeVoiceAskService(), new FakeBackendActivityMonitor()), NullLogger<DBusHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenNoSessionBus_StillCompletes()
    {
        // Hard rule #3: the daemon must keep running headless, just without
        // the D-Bus endpoint.
        var hostedService = new DBusHostedService(new FakeConnector(false), new Veya1Service(new FakeModelRouter(prompt => Task.FromResult($"Veya received: {prompt}")), new FakeVoiceAskService(), new FakeBackendActivityMonitor()), NullLogger<DBusHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);
    }
}
