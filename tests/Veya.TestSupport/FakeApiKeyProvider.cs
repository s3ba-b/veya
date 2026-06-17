using Veya.Shared.Inference;

namespace Veya.TestSupport;

/// <summary>
/// Returns a fixed (or absent) API key without touching real configuration or
/// environment variables, shared across the cloud-backend test suites.
/// </summary>
public sealed class FakeApiKeyProvider(string? apiKey) : IApiKeyProvider
{
    public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(apiKey);
}
