using Sage.McpServer.Tools;
using Sage.Shared.Safety;
using Xunit;

namespace Sage.McpServer.Tests.Tools;

public class JournalToolTests
{
    private sealed class FakeAuditLog : IAuditLog
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private const string SampleJournalOutput =
        "2026-06-12T16:17:57+02:00 Dragonfly systemd[1]: NetworkManager-dispatcher.service: Deactivated successfully.\n" +
        "2026-06-12T16:20:00+02:00 Dragonfly systemd-journald[343]: Forwarding to syslog missed 1035 messages.\n" +
        "this line does not match the expected format\n";

    [Fact]
    public void ParseJournalEntries_ParsesTimestampUnitAndMessage()
    {
        var entries = JournalTool.ParseJournalEntries(SampleJournalOutput).ToList();

        Assert.Equal(3, entries.Count);

        Assert.Equal("2026-06-12T16:17:57+02:00", entries[0].Timestamp);
        Assert.Equal("systemd", entries[0].Unit);
        Assert.Equal("NetworkManager-dispatcher.service: Deactivated successfully.", entries[0].Message);

        Assert.Equal("2026-06-12T16:20:00+02:00", entries[1].Timestamp);
        Assert.Equal("systemd-journald", entries[1].Unit);
        Assert.Equal("Forwarding to syslog missed 1035 messages.", entries[1].Message);
    }

    [Fact]
    public void ParseJournalEntries_FallsBackToRawMessageForUnmatchedLines()
    {
        var entries = JournalTool.ParseJournalEntries(SampleJournalOutput).ToList();

        Assert.Null(entries[2].Timestamp);
        Assert.Null(entries[2].Unit);
        Assert.Equal("this line does not match the expected format", entries[2].Message);
    }

    [Fact]
    public void Allowlist_AllowsValidFixedAndOptionalArgumentForms()
    {
        var spec = JournalTool.Allowlist["journalctl"];

        Assert.True(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "50"]));
        Assert.True(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "1"]));
        Assert.True(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "200"]));
        Assert.True(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "50", "-u", "ssh.service"]));
        Assert.True(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "50", "-p", "err"]));
        Assert.True(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "50", "-p", "3"]));
        Assert.True(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "50", "-u", "ssh.service", "-p", "warning"]));
    }

    [Fact]
    public void Allowlist_RejectsMalformedOrOutOfRangeArguments()
    {
        var spec = JournalTool.Allowlist["journalctl"];

        Assert.False(spec.ArgumentsAllowed([]));
        Assert.False(spec.ArgumentsAllowed(["--no-pager", "-o", "json", "-n", "50"]));
        Assert.False(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "0"]));
        Assert.False(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "201"]));
        Assert.False(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "abc"]));
        Assert.False(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "-5"]));
        Assert.False(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "50", "-u", "; rm -rf /"]));
        Assert.False(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "50", "-u", "not-a-service"]));
        Assert.False(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "50", "-p", "9"]));
        Assert.False(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "50", "-p", "verbose"]));
        Assert.False(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "50", "-u"]));
        Assert.False(spec.ArgumentsAllowed(["--no-pager", "-o", "short-iso", "-n", "50", "extra"]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task QueryJournalAsync_RejectsOutOfRangeLines(int lines)
    {
        var executor = new SafeExecutor(JournalTool.Allowlist, new FakeAuditLog());
        var tool = new JournalTool(executor);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tool.QueryJournalAsync(lines: lines));
    }

    [Fact]
    public async Task QueryJournalAsync_RejectsInvalidUnit()
    {
        var executor = new SafeExecutor(JournalTool.Allowlist, new FakeAuditLog());
        var tool = new JournalTool(executor);

        await Assert.ThrowsAsync<ArgumentException>(() => tool.QueryJournalAsync(unit: "not-a-service"));
    }

    [Fact]
    public async Task QueryJournalAsync_RejectsInvalidPriority()
    {
        var executor = new SafeExecutor(JournalTool.Allowlist, new FakeAuditLog());
        var tool = new JournalTool(executor);

        await Assert.ThrowsAsync<ArgumentException>(() => tool.QueryJournalAsync(priority: "verbose"));
    }

    [Fact]
    public async Task QueryJournalAsync_ReturnsEntries()
    {
        var executor = new SafeExecutor(JournalTool.Allowlist, new FakeAuditLog());
        var tool = new JournalTool(executor);

        var entries = await tool.QueryJournalAsync(lines: 5);

        Assert.NotEmpty(entries);
        Assert.True(entries.Count <= 5);
    }
}
