using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Systemd;
using Xunit;

namespace Veya.Daemon.Tests;

public class SystemdIntegrationTests
{
    [Fact]
    public async Task Host_WithSystemd_StartsAndStopsWithoutNotifySocket()
    {
        // CI and dev machines have no NOTIFY_SOCKET (hard rule #3: no desktop
        // session / systemd assumed). AddSystemd() must be a no-op in that
        // case rather than fail.
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSystemd();
        builder.Services.AddHostedService<Worker>();

        using var host = builder.Build();

        // AddSystemd() only registers ISystemdNotifier/SystemdLifetime when
        // NOTIFY_SOCKET is set (SystemdHelpers.IsSystemdService()); outside
        // systemd it's a no-op, so the service may be absent entirely.
        var notifier = host.Services.GetService<ISystemdNotifier>();
        Assert.True(notifier is null || !notifier.IsEnabled);

        await host.StartAsync();
        await host.StopAsync();
    }
}
