using Sage.Shared;
using Tmds.DBus;

namespace Sage.Daemon;

/// <summary>
/// Default <see cref="IDBusSessionConnector"/>: connects to the real D-Bus
/// session bus, if one is available, and registers the service there.
/// </summary>
public sealed class DBusSessionConnector(ILogger<DBusSessionConnector> logger)
    : IDBusSessionConnector, IDisposable
{
    private Connection? _connection;

    public async Task<bool> TryRegisterAsync(IDBusObject service)
    {
        var address = Address.Session;
        if (string.IsNullOrEmpty(address))
        {
            logger.LogInformation(
                "No D-Bus session bus available; {Interface} not registered.",
                SageDBus.InterfaceName);
            return false;
        }

        try
        {
            var connection = new Connection(address);
            await connection.ConnectAsync();
            await connection.RegisterServiceAsync(SageDBus.BusName);
            await connection.RegisterObjectAsync(service);
            _connection = connection;

            logger.LogInformation(
                "Registered {Interface} at {ObjectPath} on the session bus.",
                SageDBus.InterfaceName,
                SageDBus.ObjectPath);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to register {Interface} on the session bus.",
                SageDBus.InterfaceName);
            return false;
        }
    }

    public void Dispose() => _connection?.Dispose();
}
