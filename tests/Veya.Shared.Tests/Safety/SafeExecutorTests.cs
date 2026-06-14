using System.Diagnostics;
using Veya.Shared.Safety;
using Xunit;

namespace Veya.Shared.Tests.Safety;

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

    // Best-effort cleanup of a detached survivor a test intentionally spawned.
    private static void KillSurvivor(string pattern)
    {
        try
        {
            var psi = new ProcessStartInfo("/usr/bin/pkill");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add(pattern);
            using var pkill = Process.Start(psi);
            pkill?.WaitForExit(2000);
        }
        catch
        {
            // Cleanup is best-effort; the survivor self-terminates anyway.
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
    public async Task RunAsync_DetachedDescendantHoldingPipe_DoesNotHangPastTimeout()
    {
        // Regression for #41: bash exits immediately but backgrounds a child that
        // re-parents away from the process tree while still holding the captured
        // stderr pipe. The old code blocked on WaitForExitAsync for the child's
        // whole lifetime (observed: minutes); the hard timeout must bound it.
        // The survivor sleep outlives the call by far; "sleep 31" is the unique
        // pattern we clean up by (distinct from the 30s used elsewhere).
        const string pattern = "sleep 31";
        var allowlist = new Dictionary<string, CommandSpec>
        {
            ["bash"] = CommandSpec.AllowAnyArguments("/usr/bin/bash"),
        };
        var executor = new SafeExecutor(allowlist, new RecordingAuditLog(), timeout: TimeSpan.FromMilliseconds(300));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // bash backgrounds the sleep, disowns it (re-parents away from the
            // tree while still holding the captured stderr pipe), and exits 0.
            await executor.RunAsync(new ExecRequest("test_tool", "bash", ["-c", "sleep 31 & disown; exit 0"]));
            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"RunAsync took {stopwatch.Elapsed} — it waited on the detached child.");
        }
        finally
        {
            KillSurvivor(pattern);
        }
    }

    [Fact]
    public async Task RunAsync_Detached_ReturnsPromptlyAndDoesNotCaptureOutput()
    {
        // Detached mode (clipboard helpers): a survivor is expected; the call
        // returns as soon as the foreground process exits, without timing out.
        const string pattern = "sleep 32";
        var allowlist = new Dictionary<string, CommandSpec>
        {
            ["bash"] = CommandSpec.AllowAnyArguments("/usr/bin/bash"),
        };
        var executor = new SafeExecutor(allowlist, new RecordingAuditLog(), timeout: TimeSpan.FromSeconds(5));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await executor.RunAsync(
                new ExecRequest("test_tool", "bash", ["-c", "sleep 32 & disown; exit 0"], Detached: true));
            stopwatch.Stop();

            Assert.False(result.TimedOut);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Detached run took {stopwatch.Elapsed}.");
            Assert.Equal(string.Empty, result.StandardOutput);
        }
        finally
        {
            KillSurvivor(pattern);
        }
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
