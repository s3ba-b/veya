using Veya.McpServer.Tools;
using Veya.Shared.Safety;
using Xunit;

namespace Veya.McpServer.Tests.Tools;

public class PackageToolTests
{
    private sealed class FakeAuditLog : IAuditLog
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private const string FixedFormat = "${Package}\t${Version}\t${Architecture}\t${binary:Summary}\n";

    [Fact]
    public void Allowlist_AllowsOnlyTheFixedFormatWithValidPackageNames()
    {
        var spec = PackageTool.Allowlist["dpkg-query"];

        Assert.True(spec.ArgumentsAllowed(["-W", "-f", FixedFormat, "bash"]));
        Assert.True(spec.ArgumentsAllowed(["-W", "-f", FixedFormat, "lib32-foo.bar+baz"]));
    }

    [Fact]
    public void Allowlist_RejectsMalformedArgumentsOrPackageNames()
    {
        var spec = PackageTool.Allowlist["dpkg-query"];

        Assert.False(spec.ArgumentsAllowed([]));
        Assert.False(spec.ArgumentsAllowed(["-W", "-f", FixedFormat]));
        Assert.False(spec.ArgumentsAllowed(["-W", "-f", "${Package}\n", "bash"]));
        Assert.False(spec.ArgumentsAllowed(["-l"]));
        Assert.False(spec.ArgumentsAllowed(["-W", "-f", FixedFormat, "Bash"]));
        Assert.False(spec.ArgumentsAllowed(["-W", "-f", FixedFormat, "-bash"]));
        Assert.False(spec.ArgumentsAllowed(["-W", "-f", FixedFormat, "bash; rm -rf /"]));
        Assert.False(spec.ArgumentsAllowed(["-W", "-f", FixedFormat, "bash", "extra"]));
    }

    [Theory]
    [InlineData("Bash")]
    [InlineData("-bash")]
    [InlineData("bash; rm -rf /")]
    [InlineData("")]
    public async Task QueryPackageAsync_RejectsInvalidPackageNames(string name)
    {
        var executor = new SafeExecutor(PackageTool.Allowlist, new FakeAuditLog());
        var tool = new PackageTool(executor);

        await Assert.ThrowsAsync<ArgumentException>(() => tool.QueryPackageAsync(name));
    }

    [Fact]
    public async Task QueryPackageAsync_ReturnsMetadataForInstalledPackage()
    {
        var executor = new SafeExecutor(PackageTool.Allowlist, new FakeAuditLog());
        var tool = new PackageTool(executor);

        var info = await tool.QueryPackageAsync("bash");

        Assert.Equal("bash", info.Name);
        Assert.True(info.Installed);
        Assert.False(string.IsNullOrEmpty(info.Version));
        Assert.False(string.IsNullOrEmpty(info.Architecture));
        Assert.NotNull(info.Description);
    }

    [Fact]
    public async Task QueryPackageAsync_ReturnsNotInstalledForUnknownPackage()
    {
        var executor = new SafeExecutor(PackageTool.Allowlist, new FakeAuditLog());
        var tool = new PackageTool(executor);

        var info = await tool.QueryPackageAsync("this-package-definitely-does-not-exist-xyz");

        Assert.False(info.Installed);
        Assert.Null(info.Version);
        Assert.Null(info.Architecture);
        Assert.Null(info.Description);
    }
}
