using System.ComponentModel;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using Sage.Shared.Safety;

namespace Sage.McpServer.Tools;

[McpServerToolType]
public sealed class PackageTool(ISafeExecutor executor)
{
    private const string OutputFormat = "${Package}\t${Version}\t${Architecture}\t${binary:Summary}\n";

    private static readonly Regex PackageNamePattern = new(@"^[a-z0-9][a-z0-9+.\-]*$", RegexOptions.Compiled);

    public static IReadOnlyDictionary<string, CommandSpec> Allowlist { get; } = new Dictionary<string, CommandSpec>
    {
        ["dpkg-query"] = new CommandSpec(
            "/usr/bin/dpkg-query",
            args => args.Count == 4
                && args[0] == "-W"
                && args[1] == "-f"
                && args[2] == OutputFormat
                && PackageNamePattern.IsMatch(args[3])),
    };

    [McpServerTool(Name = "query_package")]
    [Description("Reports whether an APT package is installed, with its version, architecture, and description.")]
    public async Task<PackageInfo> QueryPackageAsync(
        [Description("Debian package name, e.g. \"bash\".")] string name,
        CancellationToken cancellationToken = default)
    {
        if (!PackageNamePattern.IsMatch(name))
        {
            throw new ArgumentException($"name must match \"{PackageNamePattern}\", got \"{name}\".", nameof(name));
        }

        var result = await executor.RunAsync(new ExecRequest("query_package", "dpkg-query", ["-W", "-f", OutputFormat, name]), cancellationToken);

        if (result.ExitCode != 0)
        {
            return new PackageInfo(name, Installed: false, Version: null, Architecture: null, Description: null);
        }

        var fields = result.StandardOutput.TrimEnd('\n').Split('\t', 4);
        return new PackageInfo(
            Name: fields[0],
            Installed: true,
            Version: fields[1],
            Architecture: fields[2],
            Description: fields.Length > 3 ? fields[3] : string.Empty);
    }
}
