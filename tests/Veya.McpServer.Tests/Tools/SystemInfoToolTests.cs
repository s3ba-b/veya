using System.Runtime.InteropServices;
using Veya.McpServer.Tools;
using Veya.Shared.Safety;
using Xunit;

namespace Veya.McpServer.Tests.Tools;

public class SystemInfoToolTests
{
    private sealed class FakeAuditLog : IAuditLog
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Theory]
    [InlineData("12345.67 98765.43\n", 12345.67)]
    [InlineData("0.50 0.10", 0.5)]
    public void ParseUptime_ParsesFirstFieldAsSeconds(string procUptimeContents, double expectedSeconds)
    {
        var uptime = SystemInfoTool.ParseUptime(procUptimeContents);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), uptime);
    }

    [Fact]
    public void Allowlist_AllowsUnameWithReleaseFlagOnly()
    {
        var spec = SystemInfoTool.Allowlist["uname"];

        Assert.True(spec.ArgumentsAllowed(["-r"]));
        Assert.False(spec.ArgumentsAllowed(["-a"]));
        Assert.False(spec.ArgumentsAllowed([]));
    }

    [Fact]
    public async Task GetSystemInfoAsync_ReturnsHostInfoUsingSafeExecutor()
    {
        var executor = new SafeExecutor(SystemInfoTool.Allowlist, new FakeAuditLog());
        var tool = new SystemInfoTool(executor);

        var info = await tool.GetSystemInfoAsync();

        Assert.Equal(Environment.MachineName, info.Hostname);
        Assert.Equal(RuntimeInformation.OSDescription, info.OperatingSystem);
        Assert.Equal(RuntimeInformation.OSArchitecture.ToString(), info.Architecture);
        Assert.NotEmpty(info.KernelVersion);
        Assert.True(info.Uptime > TimeSpan.Zero);
    }
}
