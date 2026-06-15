using Microsoft.Extensions.Configuration;
using Veya.Shared.Inference;
using Xunit;

namespace Veya.Shared.Tests.Inference;

public class ConfigurationApiKeyProviderTests
{
    private sealed class StubApiKeyProvider(string? apiKey) : IApiKeyProvider
    {
        public bool WasCalled { get; private set; }

        public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(apiKey);
        }
    }

    private static IConfiguration Config(params (string Key, string? Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))
            .Build();

    [Fact]
    public async Task GetApiKeyAsync_ReturnsConfigurationValue_WithoutConsultingFallback()
    {
        var fallback = new StubApiKeyProvider("from-env");
        var provider = new ConfigurationApiKeyProvider(Config(("Mistral:ApiKey", "from-config")), "Mistral:ApiKey", fallback);

        var key = await provider.GetApiKeyAsync();

        Assert.Equal("from-config", key);
        Assert.False(fallback.WasCalled);
    }

    [Fact]
    public async Task GetApiKeyAsync_FallsBackWhenConfigKeyMissing()
    {
        var fallback = new StubApiKeyProvider("from-env");
        var provider = new ConfigurationApiKeyProvider(Config(), "Mistral:ApiKey", fallback);

        var key = await provider.GetApiKeyAsync();

        Assert.Equal("from-env", key);
        Assert.True(fallback.WasCalled);
    }

    [Fact]
    public async Task GetApiKeyAsync_FallsBackWhenConfigValueEmpty()
    {
        var fallback = new StubApiKeyProvider("from-env");
        var provider = new ConfigurationApiKeyProvider(Config(("Mistral:ApiKey", "")), "Mistral:ApiKey", fallback);

        var key = await provider.GetApiKeyAsync();

        Assert.Equal("from-env", key);
    }

    [Fact]
    public async Task GetApiKeyAsync_ReturnsNullWhenNeitherSourceHasKey()
    {
        var fallback = new StubApiKeyProvider(null);
        var provider = new ConfigurationApiKeyProvider(Config(), "Mistral:ApiKey", fallback);

        var key = await provider.GetApiKeyAsync();

        Assert.Null(key);
    }
}
