using Veya.Daemon.Mcp;
using Veya.Shared.Inference;

namespace Veya.Daemon;

/// <summary>
/// Implementation of <see cref="IModelRouter"/>: a passthrough to whichever
/// <see cref="IInferenceBackend"/> is configured (Milestone 2:
/// <see cref="FallbackInferenceBackend"/>, local-first with cloud fallback —
/// docs/architecture.md "Model router"), with tools discovered from and
/// executed via <see cref="IMcpToolGateway"/> (docs/roadmap.md step 7).
/// When an <see cref="IContextProvider"/> is supplied, relevant personal context
/// is folded into the system prompt before the call (ADR-0009).
/// </summary>
public sealed class ModelRouter(IInferenceBackend backend, IMcpToolGateway toolGateway, IContextProvider? contextProvider = null) : IModelRouter
{
    private const string SystemPrompt = "You are Veya, a privacy-conscious AI assistant for Ubuntu/Linux.";

    public async Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var tools = await toolGateway.GetToolsAsync(cancellationToken).ConfigureAwait(false);

        var systemPrompt = SystemPrompt;
        if (contextProvider is not null)
        {
            var contextBlock = await contextProvider.GetContextBlockAsync(prompt, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(contextBlock))
            {
                systemPrompt = $"{SystemPrompt}\n\n{contextBlock}";
            }
        }

        var request = new InferenceRequest(
            SystemPrompt: systemPrompt,
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock(prompt)])],
            Tools: tools);

        var response = await ToolUseLoopRunner.RunAsync(backend, request, toolGateway, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return string.Concat(response.Message.Content.OfType<TextBlock>().Select(block => block.Text));
    }
}
