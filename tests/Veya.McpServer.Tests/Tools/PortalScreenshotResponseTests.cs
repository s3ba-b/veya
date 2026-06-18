using Veya.McpServer.Tools;
using Xunit;

namespace Veya.McpServer.Tests.Tools;

public class PortalScreenshotResponseTests
{
    public static IEnumerable<object[]> InvalidResponses()
    {
        yield return [1u, new Dictionary<string, object> { ["uri"] = "file:///tmp/screenshot.png" }];
        yield return [0u, new Dictionary<string, object>()];
        yield return [0u, new Dictionary<string, object> { ["uri"] = 42 }];
        yield return [0u, new Dictionary<string, object> { ["uri"] = "https://example.com/screenshot.png" }];
    }

    [Theory]
    [MemberData(nameof(InvalidResponses))]
    public void TryGetFilePath_ReturnsNull_ForInvalidResponse(uint responseCode, Dictionary<string, object> results)
    {
        Assert.Null(PortalScreenshotResponse.TryGetFilePath(responseCode, results));
    }

    [Fact]
    public void TryGetFilePath_ValidFileUri_ReturnsLocalPath()
    {
        var results = new Dictionary<string, object> { ["uri"] = "file:///tmp/screenshot.png" };

        Assert.Equal("/tmp/screenshot.png", PortalScreenshotResponse.TryGetFilePath(0, results));
    }
}
