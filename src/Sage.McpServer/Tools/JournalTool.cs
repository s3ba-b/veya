using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using Sage.Shared.Safety;

namespace Sage.McpServer.Tools;

[McpServerToolType]
public sealed class JournalTool(ISafeExecutor executor)
{
    private const int MinLines = 1;
    private const int MaxLines = 200;
    private const int DefaultLines = 50;

    private static readonly Regex UnitPattern = new(@"^[A-Za-z0-9:_.\-@]+\.service$", RegexOptions.Compiled);
    private static readonly HashSet<string> PriorityNames = new(StringComparer.Ordinal)
    {
        "emerg", "alert", "crit", "err", "warning", "notice", "info", "debug",
    };

    private static readonly Regex EntryPattern = new(
        @"^(\d{4}-\d{2}-\d{2}T\S+)\s+\S+\s+([^\s\[:]+)(?:\[\d+\])?:\s?(.*)$",
        RegexOptions.Compiled);

    public static IReadOnlyDictionary<string, CommandSpec> Allowlist { get; } = new Dictionary<string, CommandSpec>
    {
        ["journalctl"] = new CommandSpec("/usr/bin/journalctl", ArgumentsAllowed),
    };

    [McpServerTool(Name = "query_journal")]
    [Description("Returns recent journald log entries, optionally filtered by systemd unit and minimum priority.")]
    public async Task<IReadOnlyList<JournalEntry>> QueryJournalAsync(
        [Description("Systemd unit to filter by, e.g. \"ssh.service\". Optional.")] string? unit = null,
        [Description("Minimum priority: emerg, alert, crit, err, warning, notice, info, debug, or 0-7. Optional.")] string? priority = null,
        [Description("Maximum number of log entries to return (1-200). Defaults to 50.")] int lines = DefaultLines,
        CancellationToken cancellationToken = default)
    {
        if (lines < MinLines || lines > MaxLines)
        {
            throw new ArgumentOutOfRangeException(nameof(lines), lines, $"lines must be between {MinLines} and {MaxLines}.");
        }

        var arguments = new List<string> { "--no-pager", "-o", "short-iso", "-n", lines.ToString(CultureInfo.InvariantCulture) };

        if (unit is not null)
        {
            if (!UnitPattern.IsMatch(unit))
            {
                throw new ArgumentException($"unit must match \"{UnitPattern}\", got \"{unit}\".", nameof(unit));
            }

            arguments.Add("-u");
            arguments.Add(unit);
        }

        if (priority is not null)
        {
            if (!IsValidPriority(priority))
            {
                throw new ArgumentException($"priority must be one of emerg|alert|crit|err|warning|notice|info|debug or 0-7, got \"{priority}\".", nameof(priority));
            }

            arguments.Add("-p");
            arguments.Add(priority);
        }

        var result = await executor.RunAsync(new ExecRequest("query_journal", "journalctl", arguments), cancellationToken);
        return ParseJournalEntries(result.StandardOutput).ToList();
    }

    internal static IEnumerable<JournalEntry> ParseJournalEntries(string journalOutput)
    {
        foreach (var line in journalOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var match = EntryPattern.Match(line);
            yield return match.Success
                ? new JournalEntry(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value)
                : new JournalEntry(null, null, line);
        }
    }

    private static bool ArgumentsAllowed(IReadOnlyList<string> args)
    {
        if (args.Count < 5
            || args[0] != "--no-pager"
            || args[1] != "-o"
            || args[2] != "short-iso"
            || args[3] != "-n"
            || !IsValidLineCount(args[4]))
        {
            return false;
        }

        var index = 5;

        if (index < args.Count && args[index] == "-u")
        {
            if (index + 1 >= args.Count || !UnitPattern.IsMatch(args[index + 1]))
            {
                return false;
            }

            index += 2;
        }

        if (index < args.Count && args[index] == "-p")
        {
            if (index + 1 >= args.Count || !IsValidPriority(args[index + 1]))
            {
                return false;
            }

            index += 2;
        }

        return index == args.Count;
    }

    private static bool IsValidLineCount(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= MinLines && n <= MaxLines;

    private static bool IsValidPriority(string value) =>
        PriorityNames.Contains(value) || (value.Length == 1 && value[0] >= '0' && value[0] <= '7');
}
