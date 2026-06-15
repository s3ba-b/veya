using Veya.McpServer.Tools;
using Xunit;

namespace Veya.McpServer.Tests.Tools;

public class PortalScreenshotResponseTests
{
    [Fact]
    public void TryGetFilePath_NonZeroResponse_ReturnsNull()
    {
        var results = new Dictionary<string, object> { ["uri"] = "file:///tmp/screenshot.png" };

        Assert.Null(PortalScreenshotResponse.TryGetFilePath(1, results));
    }

    [Fact]
    public void TryGetFilePath_MissingUri_ReturnsNull()
    {
        Assert.Null(PortalScreenshotResponse.TryGetFilePath(0, new Dictionary<string, object>()));
    }

    [Fact]
    public void TryGetFilePath_NonStringUri_ReturnsNull()
    {
        var results = new Dictionary<string, object> { ["uri"] = 42 };

        Assert.Null(PortalScreenshotResponse.TryGetFilePath(0, results));
    }

    [Fact]
    public void TryGetFilePath_NonFileUri_ReturnsNull()
    {
        var results = new Dictionary<string, object> { ["uri"] = "https://example.com/screenshot.png" };

        Assert.Null(PortalScreenshotResponse.TryGetFilePath(0, results));
    }

    [Fact]
    public void TryGetFilePath_ValidFileUri_ReturnsLocalPath()
    {
        var results = new Dictionary<string, object> { ["uri"] = "file:///tmp/screenshot.png" };

        Assert.Equal("/tmp/screenshot.png", PortalScreenshotResponse.TryGetFilePath(0, results));
    }
}
