using Veya.McpServer.Tools;
using Veya.Shared.Permissions;
using Veya.Shared.Safety;
using Xunit;

namespace Veya.McpServer.Tests.Tools;

public class ClipboardToolTests
{
    private sealed class FakeExecutor : ISafeExecutor
    {
        public ExecRequest? LastRequest { get; private set; }
        public int CallCount { get; private set; }

        public Task<ExecResult> RunAsync(ExecRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(new ExecResult(0, string.Empty, string.Empty, TimeSpan.Zero, false, false, false));
        }
    }

    private sealed class FixedGate(bool granted) : IPermissionGate
    {
        public Task<bool> CheckAsync(PermissionSource source, string requester, CancellationToken cancellationToken = default)
        {
            Source = source;
            Requester = requester;
            return Task.FromResult(granted);
        }

        public PermissionSource? Source { get; private set; }
        public string? Requester { get; private set; }
    }

    [Fact]
    public async Task SetClipboardAsync_WhenDenied_DoesNotExecuteAndReportsRefusal()
    {
        var executor = new FakeExecutor();
        var tool = new ClipboardTool(executor, new FixedGate(granted: false));

        var result = await tool.SetClipboardAsync("hello");

        Assert.Equal(0, executor.CallCount);
        Assert.Contains("not granted", result);
    }

    [Fact]
    public async Task SetClipboardAsync_ChecksClipboardPermissionForThisTool()
    {
        var gate = new FixedGate(granted: false);
        var tool = new ClipboardTool(new FakeExecutor(), gate);

        await tool.SetClipboardAsync("hello");

        Assert.Equal(PermissionSource.Clipboard, gate.Source);
        Assert.Equal("set_clipboard", gate.Requester);
    }

    [Fact]
    public async Task SetClipboardAsync_WhenGranted_PassesTextViaStandardInput()
    {
        var executor = new FakeExecutor();
        var tool = new ClipboardTool(executor, new FixedGate(granted: true));

        var result = await tool.SetClipboardAsync("clipboard payload");

        Assert.Equal(1, executor.CallCount);
        Assert.NotNull(executor.LastRequest);
        Assert.Equal("set_clipboard", executor.LastRequest!.Tool);
        // Content travels via stdin, never argv — keeps it out of the audit log.
        Assert.Equal("clipboard payload", executor.LastRequest.StandardInput);
        Assert.DoesNotContain("clipboard payload", executor.LastRequest.Arguments);
        Assert.Contains("clipboard", result);
    }

    [Fact]
    public void Allowlist_WlCopyTakesNoArguments()
    {
        var spec = ClipboardTool.Allowlist["wl-copy"];

        Assert.True(spec.ArgumentsAllowed([]));
        Assert.False(spec.ArgumentsAllowed(["--primary"]));
    }

    [Fact]
    public void Allowlist_XclipTargetsClipboardSelectionOnly()
    {
        var spec = ClipboardTool.Allowlist["xclip"];

        Assert.True(spec.ArgumentsAllowed(["-selection", "clipboard"]));
        Assert.False(spec.ArgumentsAllowed(["-selection", "primary"]));
        Assert.False(spec.ArgumentsAllowed([]));
    }
}
