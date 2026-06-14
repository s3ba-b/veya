using Veya.Shared;
using Xunit;

namespace Veya.Shared.Tests;

public class VeyaDBusTests
{
    [Fact]
    public void DBusIdentity_MatchesDocumentedContract()
    {
        // Must stay in sync with docs/dbus-interfaces.md.
        Assert.Equal("org.veya.Veya1", VeyaDBus.BusName);
        Assert.Equal("org.veya.Veya1", VeyaDBus.InterfaceName);
        Assert.Equal("/org/veya/Veya1", VeyaDBus.ObjectPath);
    }
}
