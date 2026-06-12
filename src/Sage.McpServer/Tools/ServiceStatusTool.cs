using System.ComponentModel;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using Sage.Shared.Safety;

namespace Sage.McpServer.Tools;

[McpServerToolType]
public sealed class ServiceStatusTool(ISafeExecutor executor)
{
    private const string PropertyArgument = "--property=ActiveState,SubState,UnitFileState,LoadState";

    private static readonly Regex UnitPattern = new(@"^[A-Za-z0-9:_.\-@]+\.service$", RegexOptions.Compiled);

    public static IReadOnlyDictionary<string, CommandSpec> Allowlist { get; } = new Dictionary<string, CommandSpec>
    {
        ["systemctl"] = new CommandSpec(
            "/usr/bin/systemctl",
            args => args.Count == 5
                && args[0] == "--user"
                && args[1] == "show"
                && UnitPattern.IsMatch(args[2])
                && args[3] == PropertyArgument
                && args[4] == "--value"),
    };

    [McpServerTool(Name = "get_service_status")]
    [Description("Reports a systemd user service's load/active/sub/unit-file state.")]
    public async Task<ServiceStatus> GetServiceStatusAsync(
        [Description("Systemd unit name, e.g. \"ssh-agent.service\".")] string unit,
        CancellationToken cancellationToken = default)
    {
        if (!UnitPattern.IsMatch(unit))
        {
            throw new ArgumentException($"unit must match \"{UnitPattern}\", got \"{unit}\".", nameof(unit));
        }

        var result = await executor.RunAsync(
            new ExecRequest("get_service_status", "systemctl", ["--user", "show", unit, PropertyArgument, "--value"]),
            cancellationToken);

        return ParseServiceStatus(unit, result.StandardOutput);
    }

    internal static ServiceStatus ParseServiceStatus(string unit, string showOutput)
    {
        var lines = showOutput.Split('\n');

        string Field(int index) => index < lines.Length ? lines[index] : string.Empty;

        return new ServiceStatus(
            Unit: unit,
            LoadState: Field(0),
            ActiveState: Field(1),
            SubState: Field(2),
            UnitFileState: Field(3));
    }
}
