using Sage.Shared;
using Xunit;

namespace Sage.Shared.Tests;

public class SageDBusTests
{
    [Fact]
    public void DBusIdentity_MatchesDocumentedContract()
    {
        // Must stay in sync with docs/dbus-interfaces.md.
        Assert.Equal("org.sage.Sage1", SageDBus.BusName);
        Assert.Equal("org.sage.Sage1", SageDBus.InterfaceName);
        Assert.Equal("/org/sage/Sage1", SageDBus.ObjectPath);
    }
}
