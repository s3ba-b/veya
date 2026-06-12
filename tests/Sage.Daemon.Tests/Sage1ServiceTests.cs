using Sage.Shared;
using Tmds.DBus;
using Xunit;

namespace Sage.Daemon.Tests;

public class Sage1ServiceTests
{
    [Fact]
    public async Task AskAsync_EchoesPromptInReply()
    {
        var service = new Sage1Service();

        var reply = await service.AskAsync("ping");

        Assert.Contains("ping", reply);
    }

    [Fact]
    public void ObjectPath_MatchesDocumentedContract()
    {
        var service = new Sage1Service();

        Assert.Equal(new ObjectPath(SageDBus.ObjectPath), service.ObjectPath);
    }
}
