using Sage.Shared.Inference;
using Xunit;

namespace Sage.Shared.Tests.Inference;

public class EnvironmentApiKeyProviderTests
{
    [Fact]
    public async Task GetApiKeyAsync_ReturnsEnvironmentVariableValue()
    {
        var previous = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-test-123");
            var provider = new EnvironmentApiKeyProvider();

            var key = await provider.GetApiKeyAsync();

            Assert.Equal("sk-test-123", key);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", previous);
        }
    }

    [Fact]
    public async Task GetApiKeyAsync_ReturnsNullWhenUnset()
    {
        var previous = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
            var provider = new EnvironmentApiKeyProvider();

            var key = await provider.GetApiKeyAsync();

            Assert.Null(key);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", previous);
        }
    }
}
