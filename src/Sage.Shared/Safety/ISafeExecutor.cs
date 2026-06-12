namespace Sage.Shared.Safety;

/// <summary>
/// Central gateway for all shell execution (docs/security.md). No tool may
/// call <c>Process.Start</c> directly — every command runs through here, where
/// it is checked against an allowlist, given a hard timeout, capped on
/// output size, and audit-logged.
/// </summary>
public interface ISafeExecutor
{
    /// <summary>
    /// Runs <paramref name="request"/> if it is allowlisted.
    /// </summary>
    /// <exception cref="CommandNotAllowedException">
    /// The binary is unknown to the allowlist, or its arguments were rejected.
    /// </exception>
    public Task<ExecResult> RunAsync(ExecRequest request, CancellationToken cancellationToken = default);
}
