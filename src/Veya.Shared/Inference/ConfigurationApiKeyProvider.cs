using Microsoft.Extensions.Configuration;

namespace Veya.Shared.Inference;

/// <summary>
/// Resolves a cloud backend's API key from <see cref="IConfiguration"/> first
/// — so <c>dotnet user-secrets</c>, appsettings, and <c>Section__Key</c>
/// environment variables work in development — and falls back to
/// <paramref name="fallback"/> (an <see cref="EnvironmentApiKeyProvider"/>
/// reading e.g. <c>MISTRAL_API_KEY</c>) for the service path. Interim for
/// Milestone 1 (docs/security.md); a libsecret/keyring provider is the
/// follow-up. The key is never written to a config file in the repo —
/// user-secrets live outside the source tree.
/// </summary>
/// <param name="configuration">Application configuration to read the key from.</param>
/// <param name="configurationKey">Config key, e.g. <c>"Mistral:ApiKey"</c>.</param>
/// <param name="fallback">Provider consulted when the config key is absent.</param>
public sealed class ConfigurationApiKeyProvider(IConfiguration configuration, string configurationKey, IApiKeyProvider fallback) : IApiKeyProvider
{
    public async Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var key = configuration[configurationKey];
        return string.IsNullOrEmpty(key)
            ? await fallback.GetApiKeyAsync(cancellationToken).ConfigureAwait(false)
            : key;
    }
}
