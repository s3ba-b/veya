namespace Veya.Shared;

/// <summary>
/// Well-known D-Bus identity of the Veya daemon. Single source of truth for
/// these names in code; the contract is documented in docs/dbus-interfaces.md.
/// </summary>
public static class VeyaDBus
{
    public const string BusName = "org.veya.Veya1";

    public const string InterfaceName = "org.veya.Veya1";

    public const string ObjectPath = "/org/veya/Veya1";
}
