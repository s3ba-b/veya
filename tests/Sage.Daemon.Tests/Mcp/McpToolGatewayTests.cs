using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sage.Daemon.Mcp;
using Xunit;

namespace Sage.Daemon.Tests.Mcp;

public class McpToolGatewayTests
{
    // CLAUDE.md hard rule #3: no real Sage.McpServer process in CI. These
    // tests point at a path that can't be spawned and verify the gateway
    // degrades gracefully (docs/architecture.md, "Model router").
    private static McpToolGateway CreateGateway(string serverPath = "/nonexistent/sage-mcpserver-test-binary") =>
        new(Options.Create(new McpServerOptions { ServerPath = serverPath }), NullLogger<McpToolGateway>.Instance);

    [Fact]
    public async Task GetToolsAsync_WhenServerUnavailable_ReturnsEmptyList()
    {
        var gateway = CreateGateway();

        var tools = await gateway.GetToolsAsync();

        Assert.Empty(tools);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServerUnavailable_ThrowsForUnknownTool()
    {
        var gateway = CreateGateway();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => gateway.ExecuteAsync("get_system_info", JsonDocument.Parse("{}").RootElement));
    }
}
