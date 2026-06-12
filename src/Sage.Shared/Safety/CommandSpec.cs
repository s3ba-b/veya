namespace Sage.Shared.Safety;

/// <summary>
/// An allowlist entry: the resolved binary to execute and a predicate that
/// validates the argument shape for a given command name.
/// </summary>
/// <param name="Path">Absolute path to the executable.</param>
/// <param name="ArgumentsAllowed">Returns true if the given argv is permitted for this command.</param>
public sealed record CommandSpec(string Path, Func<IReadOnlyList<string>, bool> ArgumentsAllowed)
{
    /// <summary>Allows any arguments (or none) for <paramref name="path"/>.</summary>
    public static CommandSpec AllowAnyArguments(string path) => new(path, _ => true);

    /// <summary>Allows only an empty argument list for <paramref name="path"/>.</summary>
    public static CommandSpec AllowNoArguments(string path) => new(path, args => args.Count == 0);
}
