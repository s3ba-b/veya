namespace Veya.Shared.Inference;

/// <summary>
/// Drives the request -&gt; tool-execute -&gt; response cycle against an
/// <see cref="IInferenceBackend"/> until it returns a final answer (i.e. its
/// <see cref="InferenceResponse.StopReason"/> is no longer <c>"tool_use"</c>).
/// </summary>
public static class ToolUseLoopRunner
{
    /// <exception cref="InvalidOperationException">
    /// Thrown if the model keeps requesting tool calls beyond <paramref name="maxIterations"/>.
    /// </exception>
    public static async Task<InferenceResponse> RunAsync(
        IInferenceBackend backend,
        InferenceRequest initialRequest,
        IToolExecutor toolExecutor,
        int maxIterations = 8,
        CancellationToken cancellationToken = default)
    {
        var messages = initialRequest.Messages.ToList();

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var request = initialRequest with { Messages = messages };
            var response = await backend.CompleteAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StopReason != "tool_use")
            {
                return response;
            }

            messages.Add(response.Message);

            var resultBlocks = new List<ContentBlock>();
            foreach (var block in response.Message.Content)
            {
                if (block is ToolUseBlock toolUse)
                {
                    var result = await toolExecutor.ExecuteAsync(toolUse.Name, toolUse.Input, cancellationToken).ConfigureAwait(false);
                    resultBlocks.Add(new ToolResultBlock(toolUse.Id, result));
                }
            }

            messages.Add(new ChatMessage(ChatRole.User, resultBlocks));
        }

        throw new InvalidOperationException($"Tool-use loop exceeded the maximum of {maxIterations} iterations.");
    }
}
