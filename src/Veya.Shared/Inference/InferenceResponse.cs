namespace Veya.Shared.Inference;

/// <summary>A response from an <see cref="IInferenceBackend"/>.</summary>
/// <param name="Message">The assistant's reply.</param>
/// <param name="StopReason">Why generation stopped, e.g. <c>"end_turn"</c> or <c>"tool_use"</c>.</param>
/// <param name="InputTokens">Number of input tokens billed for this request.</param>
/// <param name="OutputTokens">Number of output tokens billed for this request.</param>
public sealed record InferenceResponse(ChatMessage Message, string StopReason, int InputTokens, int OutputTokens);
