namespace Veya.Shared.Inference;

/// <summary>
/// A source of model completions (docs/architecture.md, "Model router").
/// <c>ClaudeBackend</c> calls the Claude API; <c>OllamaBackend</c> (ADR-0004)
/// calls a local Ollama server. The router's local-vs-cloud policy is a
/// follow-up to Milestone 2.
/// </summary>
public interface IInferenceBackend
{
    public Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default);
}
