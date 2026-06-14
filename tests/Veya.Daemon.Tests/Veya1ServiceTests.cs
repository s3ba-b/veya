using Tmds.DBus;
using Veya.Shared;
using Veya.Shared.Inference;
using Xunit;

namespace Veya.Daemon.Tests;

public class Veya1ServiceTests
{
    private sealed class FakeModelRouter(Func<string, Task<string>> respond) : IModelRouter
    {
        public Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default) => respond(prompt);
    }

    [Fact]
    public async Task AskAsync_ReturnsRouterReply()
    {
        var service = new Veya1Service(new FakeModelRouter(prompt => Task.FromResult($"Veya received: {prompt}")));

        var reply = await service.AskAsync("ping");

        Assert.Equal("Veya received: ping", reply);
    }

    [Fact]
    public async Task AskAsync_WhenBackendUnavailable_ReturnsErrorMessageInsteadOfThrowing()
    {
        var service = new Veya1Service(new FakeModelRouter(_ => throw new BackendUnavailableException("no API key configured")));

        var reply = await service.AskAsync("ping");

        Assert.Contains("no API key configured", reply);
    }

    [Fact]
    public void ObjectPath_MatchesDocumentedContract()
    {
        var service = new Veya1Service(new FakeModelRouter(prompt => Task.FromResult(prompt)));

        Assert.Equal(new ObjectPath(VeyaDBus.ObjectPath), service.ObjectPath);
    }
}
