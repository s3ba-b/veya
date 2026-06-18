using Veya.McpServer.Tools;
using Veya.Shared.Safety;
using Xunit;

namespace Veya.McpServer.Tests.Tools;

public class ServiceStatusToolTests
{
    private sealed class FakeAuditLog : IAuditLog
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeExecutor(string standardOutput) : ISafeExecutor
    {
        public ExecRequest? LastRequest { get; private set; }

        public Task<ExecResult> RunAsync(ExecRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new ExecResult(0, standardOutput, string.Empty, TimeSpan.Zero, false, false, false));
        }
    }

    private const string PropertyArgument = "--property=ActiveState,SubState,UnitFileState,LoadState";

    [Fact]
    public void ParseServiceStatus_ParsesFixedOrderValueLines()
    {
        var status = ServiceStatusTool.ParseServiceStatus("ssh-agent.service", "loaded\nactive\nrunning\nstatic\n");

        Assert.Equal("ssh-agent.service", status.Unit);
        Assert.Equal("loaded", status.LoadState);
        Assert.Equal("active", status.ActiveState);
        Assert.Equal("running", status.SubState);
        Assert.Equal("static", status.UnitFileState);
    }

    [Fact]
    public void ParseServiceStatus_HandlesEmptyUnitFileStateForUnknownUnit()
    {
        var status = ServiceStatusTool.ParseServiceStatus("nonexistent.service", "not-found\ninactive\ndead\n\n");

        Assert.Equal("not-found", status.LoadState);
        Assert.Equal("inactive", status.ActiveState);
        Assert.Equal("dead", status.SubState);
        Assert.Equal(string.Empty, status.UnitFileState);
    }

    [Fact]
    public void Allowlist_AllowsOnlyTheFixedArgvShapeWithValidUnitNames()
    {
        var spec = ServiceStatusTool.Allowlist["systemctl"];

        Assert.True(spec.ArgumentsAllowed(["--user", "show", "ssh-agent.service", PropertyArgument, "--value"]));
        Assert.True(spec.ArgumentsAllowed(["--user", "show", "my-app@1.service", PropertyArgument, "--value"]));
    }

    [Fact]
    public void Allowlist_RejectsMalformedArgumentsOrUnitNames()
    {
        var spec = ServiceStatusTool.Allowlist["systemctl"];

        Assert.False(spec.ArgumentsAllowed([]));
        Assert.False(spec.ArgumentsAllowed(["--user", "show", "ssh-agent.service", PropertyArgument]));
        Assert.False(spec.ArgumentsAllowed(["show", "ssh-agent.service", PropertyArgument, "--value"]));
        Assert.False(spec.ArgumentsAllowed(["--user", "show", "not-a-service", PropertyArgument, "--value"]));
        Assert.False(spec.ArgumentsAllowed(["--user", "show", "ssh-agent.service; rm -rf /", PropertyArgument, "--value"]));
        Assert.False(spec.ArgumentsAllowed(["--user", "show", "ssh-agent.service", "--property=ActiveState", "--value"]));
        Assert.False(spec.ArgumentsAllowed(["--user", "show", "ssh-agent.service", PropertyArgument, "--value", "extra"]));
    }

    [Theory]
    [InlineData("not-a-service")]
    [InlineData("ssh-agent")]
    [InlineData("ssh-agent.service; rm -rf /")]
    [InlineData("")]
    public async Task GetServiceStatusAsync_RejectsInvalidUnitNames(string unit)
    {
        var executor = new SafeExecutor(ServiceStatusTool.Allowlist, new FakeAuditLog());
        var tool = new ServiceStatusTool(executor);

        await Assert.ThrowsAsync<ArgumentException>(() => tool.GetServiceStatusAsync(unit));
    }

    [Fact]
    public async Task GetServiceStatusAsync_RunsSystemctlShowAndReturnsParsedStatus()
    {
        var executor = new FakeExecutor("loaded\nactive\nrunning\nstatic\n");
        var tool = new ServiceStatusTool(executor);

        var status = await tool.GetServiceStatusAsync("ssh-agent.service");

        Assert.Equal("ssh-agent.service", status.Unit);
        Assert.Equal("loaded", status.LoadState);
        Assert.Equal("active", status.ActiveState);
        Assert.Equal("running", status.SubState);
        Assert.Equal("static", status.UnitFileState);

        Assert.Equal("get_service_status", executor.LastRequest!.Tool);
        Assert.Equal("systemctl", executor.LastRequest.Binary);
        Assert.Equal(["--user", "show", "ssh-agent.service", PropertyArgument, "--value"], executor.LastRequest.Arguments);
    }
}
