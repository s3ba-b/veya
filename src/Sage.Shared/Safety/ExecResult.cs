namespace Sage.Shared.Safety;

/// <summary>
/// Result of an allowlisted command execution.
/// </summary>
/// <param name="ExitCode">Process exit code (meaningless if <paramref name="TimedOut"/> is true).</param>
/// <param name="StandardOutput">Captured stdout, possibly truncated.</param>
/// <param name="StandardError">Captured stderr, possibly truncated.</param>
/// <param name="Duration">Wall-clock time the process ran.</param>
/// <param name="TimedOut">True if the process was killed for exceeding the timeout.</param>
/// <param name="StdoutTruncated">True if stdout exceeded the output cap.</param>
/// <param name="StderrTruncated">True if stderr exceeded the output cap.</param>
public sealed record ExecResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration,
    bool TimedOut,
    bool StdoutTruncated,
    bool StderrTruncated);
