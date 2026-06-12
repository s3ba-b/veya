namespace Sage.Shared.Inference;

/// <summary>
/// A source of model completions (docs/architecture.md, "Model router").
/// <c>ClaudeBackend</c> is the first implementation; <c>LocalBackend</c>
/// (Ollama/LLamaSharp) arrives in Milestone 2.
/// </summary>
public interface IInferenceBackend
{
    public Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default);
}
