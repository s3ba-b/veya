namespace Sage.Shared.Inference;

/// <summary>
/// Supplies the API key for a cloud inference backend. The Milestone 1
/// implementation reads an environment variable; a libsecret/keyring-backed
/// provider is a follow-up (docs/security.md).
/// </summary>
public interface IApiKeyProvider
{
    public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default);
}
