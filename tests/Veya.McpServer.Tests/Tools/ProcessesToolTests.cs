using Veya.McpServer.Tools;
using Veya.Shared.Safety;
using Xunit;

namespace Veya.McpServer.Tests.Tools;

public class ProcessesToolTests
{
    private sealed class FakeAuditLog : IAuditLog
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private const string SamplePsOutput =
        "  60428  110 14.3 4567540 llama-server\n" +
        "  53937  9.4  2.0 642120 Isolated Web Co\n" +
        "   5233  6.2  0.9 296728 gnome-shell\n";

    [Fact]
    public void ParseProcesses_ParsesFieldsAndConvertsRssToBytes()
    {
        var processes = ProcessesTool.ParseProcesses(SamplePsOutput).ToList();

        Assert.Equal(3, processes.Count);

        Assert.Equal(60428, processes[0].Pid);
        Assert.Equal(110, processes[0].CpuPercent);
        Assert.Equal(14.3, processes[0].MemoryPercent);
        Assert.Equal(4567540L * 1024, processes[0].ResidentSetSizeBytes);
        Assert.Equal("llama-server", processes[0].Command);

        Assert.Equal("Isolated Web Co", processes[1].Command);
    }

    [Fact]
    public void ParseProcesses_IgnoresBlankLines()
    {
        var processes = ProcessesTool.ParseProcesses("\n" + SamplePsOutput + "\n").ToList();

        Assert.Equal(3, processes.Count);
    }

    [Fact]
    public void Allowlist_AllowsOnlyTheTwoFixedPsInvocations()
    {
        var spec = ProcessesTool.Allowlist["ps"];

        Assert.True(spec.ArgumentsAllowed(["-eo", "pid,pcpu,pmem,rss,comm", "--sort=-pcpu", "--no-headers"]));
        Assert.True(spec.ArgumentsAllowed(["-eo", "pid,pcpu,pmem,rss,comm", "--sort=-pmem", "--no-headers"]));
        Assert.False(spec.ArgumentsAllowed(["-eo", "pid,pcpu,pmem,rss,comm", "--sort=-pcpu"]));
        Assert.False(spec.ArgumentsAllowed(["-ef"]));
        Assert.False(spec.ArgumentsAllowed([]));
    }

    [Fact]
    public async Task ListProcessesAsync_RejectsUnknownSortBy()
    {
        var executor = new SafeExecutor(ProcessesTool.Allowlist, new FakeAuditLog());
        var tool = new ProcessesTool(executor);

        await Assert.ThrowsAsync<ArgumentException>(() => tool.ListProcessesAsync(sortBy: "watch"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task ListProcessesAsync_RejectsOutOfRangeLimit(int limit)
    {
        var executor = new SafeExecutor(ProcessesTool.Allowlist, new FakeAuditLog());
        var tool = new ProcessesTool(executor);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tool.ListProcessesAsync(limit: limit));
    }

    [Theory]
    [InlineData("cpu")]
    [InlineData("mem")]
    public async Task ListProcessesAsync_ReturnsProcessesSortedByRequestedMetric(string sortBy)
    {
        var executor = new SafeExecutor(ProcessesTool.Allowlist, new FakeAuditLog());
        var tool = new ProcessesTool(executor);

        var processes = await tool.ListProcessesAsync(sortBy: sortBy, limit: 5);

        Assert.NotEmpty(processes);
        Assert.True(processes.Count <= 5);
    }
}
