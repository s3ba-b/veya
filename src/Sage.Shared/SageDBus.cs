namespace Sage.Shared;

/// <summary>
/// Well-known D-Bus identity of the Sage daemon. Single source of truth for
/// these names in code; the contract is documented in docs/dbus-interfaces.md.
/// </summary>
public static class SageDBus
{
    public const string BusName = "org.sage.Sage1";

    public const string InterfaceName = "org.sage.Sage1";

    public const string ObjectPath = "/org/sage/Sage1";
}
