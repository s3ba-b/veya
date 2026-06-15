namespace Veya.Shared.Inference;

/// <summary>
/// Reads a cloud backend's API key from an environment variable. Defaults to
/// <c>ANTHROPIC_API_KEY</c> for <see cref="ClaudeBackend"/>; pass another name
/// (e.g. <c>MISTRAL_API_KEY</c>) for other cloud backends. Interim for
/// Milestone 1 (docs/security.md); never written to a config file in the repo.
/// </summary>
/// <param name="variableName">Name of the environment variable holding the key.</param>
public sealed class EnvironmentApiKeyProvider(string variableName = "ANTHROPIC_API_KEY") : IApiKeyProvider
{
    public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Environment.GetEnvironmentVariable(variableName));
}
