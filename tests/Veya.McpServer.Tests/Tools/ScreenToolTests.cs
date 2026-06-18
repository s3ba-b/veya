using Veya.McpServer.Tools;
using Veya.Shared.Permissions;
using Veya.Shared.Safety;
using Veya.TestSupport;
using Xunit;

namespace Veya.McpServer.Tests.Tools;

public class ScreenToolTests
{
    private sealed class FakeExecutor : ISafeExecutor
    {
        public ExecRequest? LastRequest { get; private set; }
        public int CallCount { get; private set; }
        public string StandardOutput { get; set; } = string.Empty;

        public Task<ExecResult> RunAsync(ExecRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(new ExecResult(0, StandardOutput, string.Empty, TimeSpan.Zero, false, false, false));
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

    private sealed class FakeScreenCapture(string? path) : IScreenCapture
    {
        public int CallCount { get; private set; }

        public Task<string?> CaptureToFileAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(path);
        }
    }

    [Fact]
    public async Task ReadScreenTextAsync_WhenDenied_DoesNotCaptureAndReportsRefusal()
    {
        var capture = new FakeScreenCapture(path: "/tmp/whatever.png");
        var executor = new FakeExecutor();
        var gate = new FixedGate(granted: false);
        var tool = new ScreenTool(capture, executor, gate, new RecordingAuditLog());

        var result = await tool.ReadScreenTextAsync();

        Assert.Contains("not granted", result);
        Assert.Equal(0, capture.CallCount);
        Assert.Equal(0, executor.CallCount);
        Assert.Equal(PermissionSource.Screen, gate.Source);
        Assert.Equal("read_screen_text", gate.Requester);
    }

    [Fact]
    public async Task ReadScreenTextAsync_WhenCaptureFails_ReportsFailureAndAuditsUnsuccessful()
    {
        var capture = new FakeScreenCapture(path: null);
        var executor = new FakeExecutor();
        var auditLog = new RecordingAuditLog();
        var tool = new ScreenTool(capture, executor, new FixedGate(granted: true), auditLog);

        var result = await tool.ReadScreenTextAsync();

        Assert.Contains("Could not capture", result);
        Assert.Equal(0, executor.CallCount);

        var screenEvent = Assert.Single(auditLog.Events);
        Assert.Equal("screen.capture", screenEvent.EventType);
        Assert.Equal(false, screenEvent.Fields["success"]);
        Assert.Equal(0, screenEvent.Fields["textLength"]);
    }

    [Fact]
    public async Task ReadScreenTextAsync_WhenTextDetected_ReturnsTextAuditsSuccessAndDeletesTempFile()
    {
        var path = Path.GetTempFileName();
        var capture = new FakeScreenCapture(path);
        var executor = new FakeExecutor { StandardOutput = "Hello, screen!\n" };
        var auditLog = new RecordingAuditLog();
        var tool = new ScreenTool(capture, executor, new FixedGate(granted: true), auditLog);

        var result = await tool.ReadScreenTextAsync();

        Assert.Equal("Hello, screen!", result);
        Assert.Equal(1, executor.CallCount);
        Assert.Equal("read_screen_text", executor.LastRequest!.Tool);
        Assert.Equal([path, "stdout"], executor.LastRequest.Arguments);
        Assert.False(File.Exists(path));

        var screenEvent = Assert.Single(auditLog.Events);
        Assert.Equal(true, screenEvent.Fields["success"]);
        Assert.Equal("Hello, screen!".Length, screenEvent.Fields["textLength"]);
    }

    [Fact]
    public async Task ReadScreenTextAsync_WhenNoTextDetected_ReturnsNoTextMessage()
    {
        var path = Path.GetTempFileName();
        var capture = new FakeScreenCapture(path);
        var executor = new FakeExecutor { StandardOutput = "   \n" };
        var tool = new ScreenTool(capture, executor, new FixedGate(granted: true), new RecordingAuditLog());

        var result = await tool.ReadScreenTextAsync();

        Assert.Equal("No text was detected on the screen.", result);
    }

    [Fact]
    public async Task ReadScreenTextAsync_WhenTempFileDeleteFails_StillReturnsResultWithoutThrowing()
    {
        // File.Delete on a directory throws UnauthorizedAccessException; used here to
        // exercise the best-effort cleanup's catch without relying on filesystem permissions.
        var dirPath = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var capture = new FakeScreenCapture(dirPath);
            var executor = new FakeExecutor { StandardOutput = "Hello, screen!\n" };
            var tool = new ScreenTool(capture, executor, new FixedGate(granted: true), new RecordingAuditLog());

            var result = await tool.ReadScreenTextAsync();

            Assert.Equal("Hello, screen!", result);
            Assert.True(Directory.Exists(dirPath));
        }
        finally
        {
            Directory.Delete(dirPath);
        }
    }
}
