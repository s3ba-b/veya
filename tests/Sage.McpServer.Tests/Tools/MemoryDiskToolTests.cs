using Sage.McpServer.Tools;
using Sage.Shared.Safety;
using Xunit;

namespace Sage.McpServer.Tests.Tools;

public class MemoryDiskToolTests
{
    private sealed class FakeAuditLog : IAuditLog
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private const string SampleFreeOutput =
        "               total        used        free      shared  buff/cache   available\n" +
        "Mem:     32662794240 11055149056  5440569344  1890320384 18042204160 21607645184\n" +
        "Swap:     8589930496      913408  8589017088\n";

    private const string SampleDfOutput =
        "Filesystem     Mounted on                                Type        1B-blocks        Used        Avail Use%\n" +
        "tmpfs          /run                                      tmpfs      3266281472     5021696   3261259776   1%\n" +
        "/dev/nvme0n1p7 /                                         ext4     298786430976 82306969600 201227137024  30%\n" +
        "none           /run/credentials/systemd-journald.service tmpfs         1048576           0      1048576   0%\n";

    [Fact]
    public void ParseMemoryInfo_ParsesMemAndSwapLines()
    {
        var info = MemoryDiskTool.ParseMemoryInfo(SampleFreeOutput);

        Assert.Equal(32662794240, info.TotalBytes);
        Assert.Equal(11055149056, info.UsedBytes);
        Assert.Equal(5440569344, info.FreeBytes);
        Assert.Equal(1890320384, info.SharedBytes);
        Assert.Equal(18042204160, info.BuffCacheBytes);
        Assert.Equal(21607645184, info.AvailableBytes);
        Assert.Equal(8589930496, info.SwapTotalBytes);
        Assert.Equal(913408, info.SwapUsedBytes);
        Assert.Equal(8589017088, info.SwapFreeBytes);
    }

    [Fact]
    public void ParseDiskUsage_ParsesRowsAndHandlesMultiTokenMountPoints()
    {
        var disks = MemoryDiskTool.ParseDiskUsage(SampleDfOutput).ToList();

        Assert.Equal(3, disks.Count);

        Assert.Equal("tmpfs", disks[0].Source);
        Assert.Equal("/run", disks[0].MountPoint);
        Assert.Equal("tmpfs", disks[0].FilesystemType);
        Assert.Equal(3266281472, disks[0].SizeBytes);
        Assert.Equal(5021696, disks[0].UsedBytes);
        Assert.Equal(3261259776, disks[0].AvailableBytes);
        Assert.Equal(1, disks[0].UsePercent);

        Assert.Equal("/dev/nvme0n1p7", disks[1].Source);
        Assert.Equal("/", disks[1].MountPoint);
        Assert.Equal("ext4", disks[1].FilesystemType);
        Assert.Equal(30, disks[1].UsePercent);

        Assert.Equal("/run/credentials/systemd-journald.service", disks[2].MountPoint);
    }

    [Fact]
    public void Allowlist_AllowsOnlyTheFixedFreeAndDfInvocations()
    {
        var freeSpec = MemoryDiskTool.Allowlist["free"];
        var dfSpec = MemoryDiskTool.Allowlist["df"];

        Assert.True(freeSpec.ArgumentsAllowed(["-b"]));
        Assert.False(freeSpec.ArgumentsAllowed([]));
        Assert.False(freeSpec.ArgumentsAllowed(["-b", "-w"]));

        Assert.True(dfSpec.ArgumentsAllowed(["-B1", "--output=source,target,fstype,size,used,avail,pcent"]));
        Assert.False(dfSpec.ArgumentsAllowed(["-h"]));
        Assert.False(dfSpec.ArgumentsAllowed([]));
    }

    [Fact]
    public async Task GetMemoryInfoAsync_ReturnsRealMemoryInfo()
    {
        var executor = new SafeExecutor(MemoryDiskTool.Allowlist, new FakeAuditLog());
        var tool = new MemoryDiskTool(executor);

        var info = await tool.GetMemoryInfoAsync();

        Assert.True(info.TotalBytes > 0);
        Assert.True(info.AvailableBytes >= 0);
    }

    [Fact]
    public async Task GetDiskUsageAsync_ReturnsAtLeastTheRootFilesystem()
    {
        var executor = new SafeExecutor(MemoryDiskTool.Allowlist, new FakeAuditLog());
        var tool = new MemoryDiskTool(executor);

        var disks = await tool.GetDiskUsageAsync();

        Assert.Contains(disks, d => d.MountPoint == "/");
    }
}
