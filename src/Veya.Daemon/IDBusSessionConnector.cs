using Tmds.DBus;

namespace Veya.Daemon;

/// <summary>
/// Connects to the D-Bus session bus and registers Veya's service object.
/// Abstracted so daemon startup can be tested without a real session bus
/// (hard rule #3: no session bus assumed in CI).
/// </summary>
public interface IDBusSessionConnector
{
    /// <summary>
    /// Attempts to register <paramref name="service"/> as
    /// <see cref="VeyaDBus.BusName"/> on the session bus. Returns
    /// <c>false</c> without throwing if no session bus is available.
    /// </summary>
    public Task<bool> TryRegisterAsync(IDBusObject service);
}
