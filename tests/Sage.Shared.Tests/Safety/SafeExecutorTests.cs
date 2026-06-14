using Sage.Shared.Safety;
using Xunit;

namespace Sage.Shared.Tests.Safety;

public class SafeExecutorTests
{
    private sealed class RecordingAuditLog : IAuditLog
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RunAsync_AllowedCommand_ReturnsOutputAndAuditsAllowed()
    {
        var allowlist = new Dictionary<string, CommandSpec>
        {
            ["echo"] = CommandSpec.AllowAnyArguments("/usr/bin/echo"),
        };
        var auditLog = new RecordingAuditLog();
        var executor = new SafeExecutor(allowlist, auditLog);

        var result = await executor.RunAsync(new ExecRequest("test_tool", "echo", ["hello"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.StandardOutput);
        Assert.False(result.TimedOut);
        Assert.False(result.StdoutTruncated);

        var auditEvent = Assert.Single(auditLog.Events);
        Assert.Equal("tool.exec", auditEvent.EventType);
        Assert.Equal(true, auditEvent.Fields["allowed"]);
        Assert.Equal("echo", auditEvent.Fields["binary"]);
    }

    [Fact]
    public async Task RunAsync_PipesStandardInputToProcess()
    {
        var allowlist = new Dictionary<string, CommandSpec>
        {
            ["cat"] = CommandSpec.AllowAnyArguments("/usr/bin/cat"),
        };
        var executor = new SafeExecutor(allowlist, new RecordingAuditLog());

        var result = await executor.RunAsync(new ExecRequest("test_tool", "cat", [], StandardInput: "piped-in"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("piped-in", result.StandardOutput);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task RunAsync_StandardInputContentIsNotAudited()
    {
        var allowlist = new Dictionary<string, CommandSpec>
        {
            ["cat"] = CommandSpec.AllowAnyArguments("/usr/bin/cat"),
        };
        var auditLog = new RecordingAuditLog();
        var executor = new SafeExecutor(allowlist, auditLog);

        await executor.RunAsync(new ExecRequest("test_tool", "cat", [], StandardInput: "secret-content"));

        var auditEvent = Assert.Single(auditLog.Events);
        Assert.DoesNotContain("secret-content", auditEvent.Fields.Values.Select(v => v?.ToString()));
        Assert.DoesNotContain("secret-content", string.Join("|", auditEvent.Fields.Keys));
    }

    [Fact]
    public async Task RunAsync_BinaryNotAllowlisted_ThrowsAndAuditsDenied()
    {
        var allowlist = new Dictionary<string, CommandSpec>();
        var auditLog = new RecordingAuditLog();
        var executor = new SafeExecutor(allowlist, auditLog);

        await Assert.ThrowsAsync<CommandNotAllowedException>(
            () => executor.RunAsync(new ExecRequest("test_tool", "rm", ["-rf", "/"])));

        var auditEvent = Assert.Single(auditLog.Events);
        Assert.Equal("tool.exec", auditEvent.EventType);
        Assert.Equal(false, auditEvent.Fields["allowed"]);
        Assert.Equal("rm", auditEvent.Fields["binary"]);
    }

    [Fact]
    public async Task RunAsync_ArgumentsRejectedByValidator_ThrowsAndAuditsDenied()
    {
        var allowlist = new Dictionary<string, CommandSpec>
        {
            ["systemctl"] = new CommandSpec("/usr/bin/systemctl", args => args is ["status", _]),
        };
        var auditLog = new RecordingAuditLog();
        var executor = new SafeExecutor(allowlist, auditLog);

        await Assert.ThrowsAsync<CommandNotAllowedException>(
            () => executor.RunAsync(new ExecRequest("test_tool", "systemctl", ["restart", "ssh"])));

        var auditEvent = Assert.Single(auditLog.Events);
        Assert.Equal(false, auditEvent.Fields["allowed"]);
    }

    [Fact]
    public async Task RunAsync_CommandExceedsTimeout_IsKilledAndFlagged()
    {
        var allowlist = new Dictionary<string, CommandSpec>
        {
            ["sleep"] = CommandSpec.AllowAnyArguments("/usr/bin/sleep"),
        };
        var auditLog = new RecordingAuditLog();
        var executor = new SafeExecutor(allowlist, auditLog, timeout: TimeSpan.FromMilliseconds(200));

        var result = await executor.RunAsync(new ExecRequest("test_tool", "sleep", ["30"]));

        Assert.True(result.TimedOut);

        var auditEvent = Assert.Single(auditLog.Events);
        Assert.Equal(true, auditEvent.Fields["timedOut"]);
    }

    [Fact]
    public async Task RunAsync_OutputExceedsCap_IsTruncated()
    {
        var allowlist = new Dictionary<string, CommandSpec>
        {
            ["yes"] = CommandSpec.AllowAnyArguments("/usr/bin/yes"),
        };
        var auditLog = new RecordingAuditLog();
        var executor = new SafeExecutor(allowlist, auditLog, timeout: TimeSpan.FromSeconds(2), maxOutputBytes: 1024);

        var result = await executor.RunAsync(new ExecRequest("test_tool", "yes", []));

        Assert.True(result.StdoutTruncated);

        var auditEvent = Assert.Single(auditLog.Events);
        Assert.Equal(true, auditEvent.Fields["stdoutTruncated"]);
    }
}
