using Tmds.DBus;

namespace Sage.Shared;

/// <summary>
/// D-Bus contract for org.sage.Sage1, documented in docs/dbus-interfaces.md.
/// Method names ending in "Async" map to the D-Bus method without that
/// suffix (Tmds.DBus convention): <see cref="AskAsync"/> is D-Bus
/// <c>Ask(in s prompt, out s reply)</c>.
/// </summary>
[DBusInterface(SageDBus.InterfaceName)]
public interface ISage1 : IDBusObject
{
    public Task<string> AskAsync(string prompt);
}
