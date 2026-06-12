using System.Text.Json;
using Sage.Shared.Inference;

namespace Sage.Daemon;

/// <summary>
/// Milestone 1 implementation of <see cref="IModelRouter"/>: a single-backend
/// passthrough to <see cref="IInferenceBackend"/> (always
/// <see cref="ClaudeBackend"/>), with no tool execution. End-to-end MCP tool
/// discovery/execution arrives in docs/roadmap.md step 7.
/// </summary>
public sealed class ModelRouter(IInferenceBackend backend) : IModelRouter
{
    private const string SystemPrompt = "You are Sage, a privacy-conscious AI assistant for Ubuntu/Linux.";

    public async Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var request = new InferenceRequest(
            SystemPrompt: SystemPrompt,
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock(prompt)])],
            Tools: []);

        var response = await ToolUseLoopRunner.RunAsync(backend, request, NoOpToolExecutor.Instance, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return string.Concat(response.Message.Content.OfType<TextBlock>().Select(block => block.Text));
    }

    /// <summary>
    /// Placeholder for <see cref="ToolUseLoopRunner"/>: Milestone 1 sends no
    /// tool definitions, so the model never requests a tool call.
    /// </summary>
    private sealed class NoOpToolExecutor : IToolExecutor
    {
        public static readonly NoOpToolExecutor Instance = new();

        public Task<string> ExecuteAsync(string toolName, JsonElement input, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException($"Tool '{toolName}' cannot be executed: no tools are registered yet.");
    }
}
