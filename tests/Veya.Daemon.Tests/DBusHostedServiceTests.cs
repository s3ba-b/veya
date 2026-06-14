using Microsoft.Extensions.Logging.Abstractions;
using Tmds.DBus;
using Xunit;

namespace Veya.Daemon.Tests;

public class DBusHostedServiceTests
{
    private sealed class FakeConnector(bool result) : IDBusSessionConnector
    {
        public Task<bool> TryRegisterAsync(IDBusObject service) => Task.FromResult(result);
    }

    private sealed class FakeModelRouter : IModelRouter
    {
        public Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default) =>
            Task.FromResult($"Veya received: {prompt}");
    }

    [Fact]
    public async Task StartAsync_WhenSessionBusAvailable_CompletesSuccessfully()
    {
        var hostedService = new DBusHostedService(new FakeConnector(true), new Veya1Service(new FakeModelRouter()), NullLogger<DBusHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenNoSessionBus_StillCompletes()
    {
        // Hard rule #3: the daemon must keep running headless, just without
        // the D-Bus endpoint.
        var hostedService = new DBusHostedService(new FakeConnector(false), new Veya1Service(new FakeModelRouter()), NullLogger<DBusHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);
    }
}
