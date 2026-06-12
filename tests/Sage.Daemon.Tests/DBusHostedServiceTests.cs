using Microsoft.Extensions.Logging.Abstractions;
using Tmds.DBus;
using Xunit;

namespace Sage.Daemon.Tests;

public class DBusHostedServiceTests
{
    private sealed class FakeConnector(bool result) : IDBusSessionConnector
    {
        public Task<bool> TryRegisterAsync(IDBusObject service) => Task.FromResult(result);
    }

    [Fact]
    public async Task StartAsync_WhenSessionBusAvailable_CompletesSuccessfully()
    {
        var hostedService = new DBusHostedService(new FakeConnector(true), NullLogger<DBusHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenNoSessionBus_StillCompletes()
    {
        // Hard rule #3: the daemon must keep running headless, just without
        // the D-Bus endpoint.
        var hostedService = new DBusHostedService(new FakeConnector(false), NullLogger<DBusHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);
    }
}
