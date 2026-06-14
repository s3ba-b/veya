namespace Veya.Shared.Inference;

/// <summary>One turn in a conversation with an <see cref="IInferenceBackend"/>.</summary>
public sealed record ChatMessage(ChatRole Role, IReadOnlyList<ContentBlock> Content);
