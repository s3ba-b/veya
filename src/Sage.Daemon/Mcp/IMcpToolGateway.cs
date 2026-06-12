using Sage.Shared.Inference;

namespace Sage.Daemon.Mcp;

/// <summary>
/// Discovers and executes tools exposed by the Sage.McpServer process
/// (docs/architecture.md, "Model router"). <see cref="McpToolGateway"/> is the
/// real implementation; tests fake this interface so they don't need to spawn
/// a process (CLAUDE.md hard rule #3).
/// </summary>
public interface IMcpToolGateway : IToolExecutor
{
    /// <summary>
    /// The tools currently available from the MCP server, or an empty list if
    /// the server is unavailable.
    /// </summary>
    public Task<IReadOnlyList<ToolDefinition>> GetToolsAsync(CancellationToken cancellationToken = default);
}
