namespace Veya.Shared.Inference;

/// <summary>
/// Configures <see cref="MistralBackend"/> (ADR-0008). Bound from the "Mistral"
/// configuration section, e.g. the <c>Mistral__Model</c> environment variable.
/// The API key is read separately from <c>MISTRAL_API_KEY</c>
/// (<see cref="EnvironmentApiKeyProvider"/>), never from config in the repo.
/// </summary>
public sealed class MistralOptions
{
    /// <summary>Base URL of Mistral's hosted API ("La Plateforme").</summary>
    public string BaseUrl { get; set; } = "https://api.mistral.ai";

    /// <summary>Name of the model to request, e.g. <c>"mistral-large-latest"</c>.</summary>
    public string Model { get; set; } = "mistral-large-latest";
}
