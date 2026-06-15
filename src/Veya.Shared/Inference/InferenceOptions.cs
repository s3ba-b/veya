namespace Veya.Shared.Inference;

/// <summary>
/// Selects which cloud <see cref="IInferenceBackend"/> sits behind the local
/// model in the <see cref="FallbackInferenceBackend"/> (ADR-0008). Bound from
/// the "Inference" configuration section, e.g. the <c>Inference__CloudBackend</c>
/// environment variable.
/// </summary>
public sealed class InferenceOptions
{
    /// <summary>
    /// Which cloud backend to use: <c>"mistral"</c> (default) or <c>"claude"</c>.
    /// Case-insensitive.
    /// </summary>
    public string CloudBackend { get; set; } = "mistral";

    /// <summary>The Claude model name, used when <see cref="CloudBackend"/> is <c>"claude"</c>.</summary>
    public string ClaudeModel { get; set; } = "claude-sonnet-4-6";
}
