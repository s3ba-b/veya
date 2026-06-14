namespace Veya.Shared.Inference;

/// <summary>
/// Reads the Anthropic API key from the <c>ANTHROPIC_API_KEY</c> environment
/// variable. Interim for Milestone 1 (docs/security.md); never written to a
/// config file in the repo.
/// </summary>
public sealed class EnvironmentApiKeyProvider : IApiKeyProvider
{
    public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
}
