using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using ModelContextProtocol.Server;
using Veya.Shared.Safety;

namespace Veya.McpServer.Tools;

[McpServerToolType]
public sealed class SystemInfoTool(ISafeExecutor executor)
{
    private const string UptimePath = "/proc/uptime";

    public static IReadOnlyDictionary<string, CommandSpec> Allowlist { get; } = new Dictionary<string, CommandSpec>
    {
        ["uname"] = new CommandSpec("/usr/bin/uname", args => args is ["-r"]),
    };

    [McpServerTool(Name = "get_system_info")]
    [Description("Reports basic host information: hostname, operating system, kernel version, CPU architecture, and uptime.")]
    public async Task<SystemInfo> GetSystemInfoAsync(CancellationToken cancellationToken = default)
    {
        var kernelResult = await executor.RunAsync(
            new ExecRequest("get_system_info", "uname", ["-r"]),
            cancellationToken);

        return new SystemInfo(
            Environment.MachineName,
            RuntimeInformation.OSDescription,
            kernelResult.StandardOutput.Trim(),
            RuntimeInformation.OSArchitecture.ToString(),
            ParseUptime(await File.ReadAllTextAsync(UptimePath, cancellationToken)));
    }

    internal static TimeSpan ParseUptime(string procUptimeContents)
    {
        var secondsField = procUptimeContents.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return TimeSpan.FromSeconds(double.Parse(secondsField, CultureInfo.InvariantCulture));
    }
}
