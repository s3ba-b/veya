using Veya.Shared.Safety;

namespace Veya.McpServer.Tools;

public static class ToolAllowlist
{
    public static IReadOnlyDictionary<string, CommandSpec> Combine(params IReadOnlyDictionary<string, CommandSpec>[] allowlists)
    {
        var combined = new Dictionary<string, CommandSpec>();
        foreach (var allowlist in allowlists)
        {
            foreach (var (binary, spec) in allowlist)
            {
                combined.Add(binary, spec);
            }
        }

        return combined;
    }
}
