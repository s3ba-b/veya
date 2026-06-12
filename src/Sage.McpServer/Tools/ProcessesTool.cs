using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Server;
using Sage.Shared.Safety;

namespace Sage.McpServer.Tools;

[McpServerToolType]
public sealed class ProcessesTool(ISafeExecutor executor)
{
    private const int MaxLimit = 50;
    private const int DefaultLimit = 10;

    private static readonly IReadOnlyList<string> CpuSortArguments = ["-eo", "pid,pcpu,pmem,rss,comm", "--sort=-pcpu", "--no-headers"];
    private static readonly IReadOnlyList<string> MemorySortArguments = ["-eo", "pid,pcpu,pmem,rss,comm", "--sort=-pmem", "--no-headers"];

    public static IReadOnlyDictionary<string, CommandSpec> Allowlist { get; } = new Dictionary<string, CommandSpec>
    {
        ["ps"] = new CommandSpec("/usr/bin/ps", args => args.SequenceEqual(CpuSortArguments) || args.SequenceEqual(MemorySortArguments)),
    };

    [McpServerTool(Name = "list_processes")]
    [Description("Lists running processes sorted by CPU or memory usage.")]
    public async Task<IReadOnlyList<ProcessInfo>> ListProcessesAsync(
        [Description("Sort order: \"cpu\" or \"mem\". Defaults to \"cpu\".")] string sortBy = "cpu",
        [Description("Maximum number of processes to return (1-50). Defaults to 10.")] int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        var arguments = sortBy switch
        {
            "cpu" => CpuSortArguments,
            "mem" => MemorySortArguments,
            _ => throw new ArgumentException($"sortBy must be \"cpu\" or \"mem\", got \"{sortBy}\".", nameof(sortBy)),
        };

        if (limit < 1 || limit > MaxLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, $"limit must be between 1 and {MaxLimit}.");
        }

        var result = await executor.RunAsync(new ExecRequest("list_processes", "ps", arguments), cancellationToken);
        return ParseProcesses(result.StandardOutput).Take(limit).ToList();
    }

    internal static IEnumerable<ProcessInfo> ParseProcesses(string psOutput)
    {
        foreach (var line in psOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.TrimStart().Split(' ', 5, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 5)
            {
                continue;
            }

            yield return new ProcessInfo(
                int.Parse(fields[0], CultureInfo.InvariantCulture),
                double.Parse(fields[1], CultureInfo.InvariantCulture),
                double.Parse(fields[2], CultureInfo.InvariantCulture),
                long.Parse(fields[3], CultureInfo.InvariantCulture) * 1024,
                fields[4]);
        }
    }
}
