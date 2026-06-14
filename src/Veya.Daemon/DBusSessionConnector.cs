using Tmds.DBus;
using Veya.Shared;

namespace Veya.Daemon;

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
                VeyaDBus.InterfaceName);
            return false;
        }

        try
        {
            var connection = new Connection(address);
            await connection.ConnectAsync();
            await connection.RegisterServiceAsync(VeyaDBus.BusName);
            await connection.RegisterObjectAsync(service);
            _connection = connection;

            logger.LogInformation(
                "Registered {Interface} at {ObjectPath} on the session bus.",
                VeyaDBus.InterfaceName,
                VeyaDBus.ObjectPath);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to register {Interface} on the session bus.",
                VeyaDBus.InterfaceName);
            return false;
        }
    }

    public void Dispose() => _connection?.Dispose();
}
