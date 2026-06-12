namespace Sage.Shared.Safety;

/// <summary>
/// One entry in the audit log (docs/security.md). <see cref="Fields"/> holds
/// event-specific data; values are primitives, strings, or string lists so
/// they serialize cleanly to JSON.
/// </summary>
public sealed record AuditEvent(DateTimeOffset Timestamp, string EventType, IReadOnlyDictionary<string, object?> Fields)
{
    /// <summary>A <c>tool.exec</c> event for a command that was run.</summary>
    public static AuditEvent ToolExecAllowed(string tool, string binary, IReadOnlyList<string> arguments, ExecResult result) =>
        new(
            DateTimeOffset.UtcNow,
            "tool.exec",
            new Dictionary<string, object?>
            {
                ["tool"] = tool,
                ["binary"] = binary,
                ["argv"] = arguments,
                ["allowed"] = true,
                ["exitCode"] = result.ExitCode,
                ["durationMs"] = result.Duration.TotalMilliseconds,
                ["timedOut"] = result.TimedOut,
                ["stdoutTruncated"] = result.StdoutTruncated,
                ["stderrTruncated"] = result.StderrTruncated,
            });

    /// <summary>A <c>tool.exec</c> event for a command that was refused.</summary>
    public static AuditEvent ToolExecDenied(string tool, string binary, IReadOnlyList<string> arguments) =>
        new(
            DateTimeOffset.UtcNow,
            "tool.exec",
            new Dictionary<string, object?>
            {
                ["tool"] = tool,
                ["binary"] = binary,
                ["argv"] = arguments,
                ["allowed"] = false,
            });
}
