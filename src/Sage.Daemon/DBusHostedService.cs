namespace Sage.Daemon;

/// <summary>
/// Registers <see cref="Sage1Service"/> on the D-Bus session bus at startup.
/// If no session bus is available (headless), logs a warning and the daemon
/// continues to run without the D-Bus endpoint.
/// </summary>
public sealed class DBusHostedService(IDBusSessionConnector connector, ILogger<DBusHostedService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var registered = await connector.TryRegisterAsync(new Sage1Service());
        if (!registered)
        {
            logger.LogWarning("Sage daemon running without a D-Bus endpoint (no session bus).");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
