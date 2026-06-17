using Microsoft.Extensions.Logging.Abstractions;
using Veya.Daemon.Voice;
using Veya.Shared.Inference;
using Xunit;

namespace Veya.Daemon.Tests;

public class DBusSessionConnectorTests
{
    private sealed class FakeVoiceAskService : IVoiceAskService
    {
        public Task<(string Transcript, string Reply)> AskAsync(uint maxDurationMs, CancellationToken cancellationToken = default) =>
            Task.FromResult((string.Empty, string.Empty));
    }

    [Fact]
    public async Task TryRegisterAsync_MatchesSessionBusAvailability()
    {
        // CI is headless (hard rule #3): no DBUS_SESSION_BUS_ADDRESS, so
        // Address.Session is unset and registration is skipped. On a dev
        // desktop with a session bus, registration succeeds. Either way,
        // this must not throw.
        var hasSessionBus = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS"));

        using var connector = new DBusSessionConnector(NullLogger<DBusSessionConnector>.Instance);
        var registered = await connector.TryRegisterAsync(new Veya1Service(new FakeModelRouter(prompt => Task.FromResult(prompt)), new FakeVoiceAskService(), new FakeBackendActivityMonitor()));

        Assert.Equal(hasSessionBus, registered);
    }
}
