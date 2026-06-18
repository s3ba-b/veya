using Veya.Shared.Safety;
using Xunit;

namespace Veya.Shared.Tests.Safety;

public class CommandSpecTests
{
    [Fact]
    public void AllowAnyArguments_AllowsEmptyArgv()
    {
        var spec = CommandSpec.AllowAnyArguments("/bin/rm");

        Assert.True(spec.ArgumentsAllowed([]));
    }

    [Fact]
    public void AllowAnyArguments_AllowsAnyArgv()
    {
        var spec = CommandSpec.AllowAnyArguments("/bin/rm");

        Assert.True(spec.ArgumentsAllowed(["-rf", "/"]));
    }

    [Fact]
    public void AllowNoArguments_AllowsEmptyArgv()
    {
        var spec = CommandSpec.AllowNoArguments("/usr/bin/espeak-ng");

        Assert.True(spec.ArgumentsAllowed([]));
    }

    [Fact]
    public void AllowNoArguments_RejectsAnyArgv()
    {
        var spec = CommandSpec.AllowNoArguments("/usr/bin/espeak-ng");

        Assert.False(spec.ArgumentsAllowed(["--version"]));
    }
}
