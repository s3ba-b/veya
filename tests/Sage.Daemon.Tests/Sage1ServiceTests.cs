using Sage.Shared;
using Sage.Shared.Inference;
using Tmds.DBus;
using Xunit;

namespace Sage.Daemon.Tests;

public class Sage1ServiceTests
{
    private sealed class FakeModelRouter(Func<string, Task<string>> respond) : IModelRouter
    {
        public Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default) => respond(prompt);
    }

    [Fact]
    public async Task AskAsync_ReturnsRouterReply()
    {
        var service = new Sage1Service(new FakeModelRouter(prompt => Task.FromResult($"Sage received: {prompt}")));

        var reply = await service.AskAsync("ping");

        Assert.Equal("Sage received: ping", reply);
    }

    [Fact]
    public async Task AskAsync_WhenBackendUnavailable_ReturnsErrorMessageInsteadOfThrowing()
    {
        var service = new Sage1Service(new FakeModelRouter(_ => throw new BackendUnavailableException("no API key configured")));

        var reply = await service.AskAsync("ping");

        Assert.Contains("no API key configured", reply);
    }

    [Fact]
    public void ObjectPath_MatchesDocumentedContract()
    {
        var service = new Sage1Service(new FakeModelRouter(prompt => Task.FromResult(prompt)));

        Assert.Equal(new ObjectPath(SageDBus.ObjectPath), service.ObjectPath);
    }
}
