using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Server;
using Sage.Shared.Safety;

namespace Sage.McpServer.Tools;

[McpServerToolType]
public sealed class MemoryDiskTool(ISafeExecutor executor)
{
    private static readonly IReadOnlyList<string> FreeArguments = ["-b"];
    private static readonly IReadOnlyList<string> DfArguments = ["-B1", "--output=source,target,fstype,size,used,avail,pcent"];

    public static IReadOnlyDictionary<string, CommandSpec> Allowlist { get; } = new Dictionary<string, CommandSpec>
    {
        ["free"] = new CommandSpec("/usr/bin/free", args => args.SequenceEqual(FreeArguments)),
        ["df"] = new CommandSpec("/usr/bin/df", args => args.SequenceEqual(DfArguments)),
    };

    [McpServerTool(Name = "get_memory_info")]
    [Description("Reports system memory and swap usage in bytes.")]
    public async Task<MemoryInfo> GetMemoryInfoAsync(CancellationToken cancellationToken = default)
    {
        var result = await executor.RunAsync(new ExecRequest("get_memory_info", "free", FreeArguments), cancellationToken);
        return ParseMemoryInfo(result.StandardOutput);
    }

    [McpServerTool(Name = "get_disk_usage")]
    [Description("Reports size, usage, and available space for each mounted filesystem.")]
    public async Task<IReadOnlyList<DiskUsage>> GetDiskUsageAsync(CancellationToken cancellationToken = default)
    {
        var result = await executor.RunAsync(new ExecRequest("get_disk_usage", "df", DfArguments), cancellationToken);
        return ParseDiskUsage(result.StandardOutput).ToList();
    }

    internal static MemoryInfo ParseMemoryInfo(string freeOutput)
    {
        long[]? mem = null;
        long[]? swap = null;

        foreach (var line in freeOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 0)
            {
                continue;
            }

            if (fields[0] != "Mem:" && fields[0] != "Swap:")
            {
                continue;
            }

            var values = fields[1..].Select(f => long.Parse(f, CultureInfo.InvariantCulture)).ToArray();
            if (fields[0] == "Mem:")
            {
                mem = values;
            }
            else
            {
                swap = values;
            }
        }

        if (mem is not { Length: >= 6 } || swap is not { Length: >= 3 })
        {
            throw new FormatException("Unexpected 'free -b' output format.");
        }

        return new MemoryInfo(
            TotalBytes: mem[0],
            UsedBytes: mem[1],
            FreeBytes: mem[2],
            SharedBytes: mem[3],
            BuffCacheBytes: mem[4],
            AvailableBytes: mem[5],
            SwapTotalBytes: swap[0],
            SwapUsedBytes: swap[1],
            SwapFreeBytes: swap[2]);
    }

    internal static IEnumerable<DiskUsage> ParseDiskUsage(string dfOutput)
    {
        foreach (var line in dfOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 7)
            {
                continue;
            }

            yield return new DiskUsage(
                Source: fields[0],
                MountPoint: string.Join(' ', fields[1..^5]),
                FilesystemType: fields[^5],
                SizeBytes: long.Parse(fields[^4], CultureInfo.InvariantCulture),
                UsedBytes: long.Parse(fields[^3], CultureInfo.InvariantCulture),
                AvailableBytes: long.Parse(fields[^2], CultureInfo.InvariantCulture),
                UsePercent: int.Parse(fields[^1].TrimEnd('%'), CultureInfo.InvariantCulture));
        }
    }
}
