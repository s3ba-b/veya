using System.Text.Json;

namespace Sage.Shared.Inference;

/// <summary>
/// Executes a tool call requested by the model. The real MCP-backed
/// implementation arrives with end-to-end wiring (docs/roadmap.md, step 7);
/// for now this is only consumed by <see cref="ToolUseLoopRunner"/>.
/// </summary>
public interface IToolExecutor
{
    public Task<string> ExecuteAsync(string toolName, JsonElement input, CancellationToken cancellationToken = default);
}
