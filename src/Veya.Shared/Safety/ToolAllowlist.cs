namespace Veya.Shared.Safety;

/// <summary>
/// Merges several tools' <see cref="CommandSpec"/> allowlists into the single
/// dictionary an <see cref="ISafeExecutor"/> is constructed with.
/// </summary>
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
