using Veya.Shared.Permissions;
using Veya.Shared.Safety;
using Veya.TestSupport;
using Xunit;

namespace Veya.Shared.Tests.Permissions;

public class PermissionGateTests
{
    private sealed class FixedStore(bool granted) : IPermissionStore
    {
        public bool IsGranted(PermissionSource source) => granted;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CheckAsync_ReturnsStoreDecision(bool granted)
    {
        var gate = new PermissionGate(new FixedStore(granted), new RecordingAuditLog());

        Assert.Equal(granted, await gate.CheckAsync(PermissionSource.Clipboard, "set_clipboard"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CheckAsync_AuditsDecision(bool granted)
    {
        var audit = new RecordingAuditLog();
        var gate = new PermissionGate(new FixedStore(granted), audit);

        await gate.CheckAsync(PermissionSource.Clipboard, "set_clipboard");

        var ev = Assert.Single(audit.Events);
        Assert.Equal("permission.decision", ev.EventType);
        Assert.Equal("Clipboard", ev.Fields["source"]);
        Assert.Equal("set_clipboard", ev.Fields["requester"]);
        Assert.Equal(granted, ev.Fields["granted"]);
    }
}
