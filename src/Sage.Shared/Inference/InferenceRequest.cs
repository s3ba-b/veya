namespace Sage.Shared.Inference;

/// <summary>A request to an <see cref="IInferenceBackend"/>.</summary>
public sealed record InferenceRequest(
    string? SystemPrompt,
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<ToolDefinition> Tools);
