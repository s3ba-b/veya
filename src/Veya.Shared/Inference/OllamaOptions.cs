namespace Veya.Shared.Inference;

/// <summary>
/// Configures <see cref="OllamaBackend"/> (ADR-0004). Bound from the "Ollama"
/// configuration section, e.g. the <c>Ollama__Model</c> environment variable.
/// </summary>
public sealed class OllamaOptions
{
    /// <summary>Base URL of the local Ollama server.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Name of the model to request, e.g. <c>"llama3.1"</c>.</summary>
    public string Model { get; set; } = "llama3.1";

    /// <summary>
    /// Name of the embedding model for <see cref="OllamaEmbeddingBackend"/>
    /// (ADR-0009), e.g. <c>"nomic-embed-text"</c>. Used by the personal context
    /// index, not by chat completions.
    /// </summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
}
