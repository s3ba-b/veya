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

    [Fact]
    public async Task CheckAsync_ReturnsTrueWhenStoreGrants()
    {
        var gate = new PermissionGate(new FixedStore(granted: true), new RecordingAuditLog());

        Assert.True(await gate.CheckAsync(PermissionSource.Clipboard, "set_clipboard"));
    }

    [Fact]
    public async Task CheckAsync_ReturnsFalseWhenStoreDenies()
    {
        var gate = new PermissionGate(new FixedStore(granted: false), new RecordingAuditLog());

        Assert.False(await gate.CheckAsync(PermissionSource.Clipboard, "set_clipboard"));
    }

    [Fact]
    public async Task CheckAsync_AuditsGrantedDecision()
    {
        var audit = new RecordingAuditLog();
        var gate = new PermissionGate(new FixedStore(granted: true), audit);

        await gate.CheckAsync(PermissionSource.Clipboard, "set_clipboard");

        var ev = Assert.Single(audit.Events);
        Assert.Equal("permission.decision", ev.EventType);
        Assert.Equal("Clipboard", ev.Fields["source"]);
        Assert.Equal("set_clipboard", ev.Fields["requester"]);
        Assert.Equal(true, ev.Fields["granted"]);
    }

    [Fact]
    public async Task CheckAsync_AuditsDeniedDecision()
    {
        var audit = new RecordingAuditLog();
        var gate = new PermissionGate(new FixedStore(granted: false), audit);

        await gate.CheckAsync(PermissionSource.Clipboard, "set_clipboard");

        var ev = Assert.Single(audit.Events);
        Assert.Equal("permission.decision", ev.EventType);
        Assert.Equal(false, ev.Fields["granted"]);
    }
}
