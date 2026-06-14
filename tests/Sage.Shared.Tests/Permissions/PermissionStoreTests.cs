using Sage.Shared.Permissions;
using Xunit;

namespace Sage.Shared.Tests.Permissions;

public class PermissionStoreTests
{
    [Fact]
    public void IsGranted_DefaultsToDenyForUnknownSource()
    {
        var store = new PermissionStore();

        Assert.False(store.IsGranted(PermissionSource.Clipboard));
    }

    [Fact]
    public void IsGranted_DeniesSourceExplicitlySetFalse()
    {
        var store = new PermissionStore(new Dictionary<PermissionSource, bool>
        {
            [PermissionSource.Clipboard] = false,
        });

        Assert.False(store.IsGranted(PermissionSource.Clipboard));
    }

    [Fact]
    public void IsGranted_AllowsSourceExplicitlyGranted()
    {
        var store = new PermissionStore(new Dictionary<PermissionSource, bool>
        {
            [PermissionSource.Clipboard] = true,
        });

        Assert.True(store.IsGranted(PermissionSource.Clipboard));
    }

    [Fact]
    public void IsGranted_GrantsAreIndependentPerSource()
    {
        var store = new PermissionStore(new Dictionary<PermissionSource, bool>
        {
            [PermissionSource.Clipboard] = true,
        });

        Assert.True(store.IsGranted(PermissionSource.Clipboard));
        Assert.False(store.IsGranted(PermissionSource.Files));
        Assert.False(store.IsGranted(PermissionSource.Screen));
    }
}
