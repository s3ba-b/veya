namespace Veya.Shared.Safety;

/// <summary>
/// Thrown when <see cref="ISafeExecutor"/> is asked to run a command that is
/// not in the allowlist, or whose arguments are rejected by the allowlist's
/// validator. The denial is audit-logged before this is thrown.
/// </summary>
public sealed class CommandNotAllowedException(string binary)
    : Exception($"Command '{binary}' is not permitted by the safety-layer allowlist.")
{
    public string Binary { get; } = binary;
}
