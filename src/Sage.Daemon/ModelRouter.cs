using Sage.Daemon.Mcp;
using Sage.Shared.Inference;

namespace Sage.Daemon;

/// <summary>
/// Milestone 1 implementation of <see cref="IModelRouter"/>: a single-backend
/// passthrough to <see cref="IInferenceBackend"/> (always
/// <see cref="ClaudeBackend"/>), with tools discovered from and executed via
/// <see cref="IMcpToolGateway"/> (docs/roadmap.md step 7).
/// </summary>
public sealed class ModelRouter(IInferenceBackend backend, IMcpToolGateway toolGateway) : IModelRouter
{
    private const string SystemPrompt = "You are Sage, a privacy-conscious AI assistant for Ubuntu/Linux.";

    public async Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var tools = await toolGateway.GetToolsAsync(cancellationToken).ConfigureAwait(false);

        var request = new InferenceRequest(
            SystemPrompt: SystemPrompt,
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock(prompt)])],
            Tools: tools);

        var response = await ToolUseLoopRunner.RunAsync(backend, request, toolGateway, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return string.Concat(response.Message.Content.OfType<TextBlock>().Select(block => block.Text));
    }
}
