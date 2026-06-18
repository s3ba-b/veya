using Veya.Shared.Safety;
using Xunit;

namespace Veya.Shared.Tests.Safety;

public class ToolAllowlistTests
{
    [Fact]
    public void Combine_MergesDistinctAllowlistsFromMultipleTools()
    {
        var clipboard = new Dictionary<string, CommandSpec> { ["xclip"] = CommandSpec.AllowAnyArguments("/usr/bin/xclip") };
        var voice = new Dictionary<string, CommandSpec> { ["espeak-ng"] = CommandSpec.AllowNoArguments("/usr/bin/espeak-ng") };

        var combined = ToolAllowlist.Combine(clipboard, voice);

        Assert.Equal(2, combined.Count);
        Assert.Same(clipboard["xclip"], combined["xclip"]);
        Assert.Same(voice["espeak-ng"], combined["espeak-ng"]);
    }

    [Fact]
    public void Combine_ReturnsEmpty_WhenGivenNoAllowlists()
    {
        var combined = ToolAllowlist.Combine();

        Assert.Empty(combined);
    }

    [Fact]
    public void Combine_Throws_WhenTwoToolsRegisterTheSameBinary()
    {
        var first = new Dictionary<string, CommandSpec> { ["rm"] = CommandSpec.AllowAnyArguments("/bin/rm") };
        var second = new Dictionary<string, CommandSpec> { ["rm"] = CommandSpec.AllowNoArguments("/bin/rm") };

        Assert.Throws<ArgumentException>(() => ToolAllowlist.Combine(first, second));
    }
}
