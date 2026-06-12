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

    /// <summary>
    /// A <c>cloud.request</c> event for a call to a cloud inference backend.
    /// Deliberately carries no prompt or response content (docs/security.md):
    /// only the backend, model, token counts, and duration.
    /// </summary>
    public static AuditEvent CloudRequest(string backend, string model, int inputTokens, int outputTokens, TimeSpan duration) =>
        new(
            DateTimeOffset.UtcNow,
            "cloud.request",
            new Dictionary<string, object?>
            {
                ["backend"] = backend,
                ["model"] = model,
                ["inputTokens"] = inputTokens,
                ["outputTokens"] = outputTokens,
                ["durationMs"] = duration.TotalMilliseconds,
            });

    /// <summary>
    /// A <c>local.request</c> event for a call to a local inference backend.
    /// Mirrors <see cref="CloudRequest"/>'s fields for observability, but is
    /// deliberately a distinct event type (docs/security.md): nothing leaves
    /// the machine, so this does not trigger the <c>CloudUsage</c> signal.
    /// </summary>
    public static AuditEvent LocalRequest(string backend, string model, int inputTokens, int outputTokens, TimeSpan duration) =>
        new(
            DateTimeOffset.UtcNow,
            "local.request",
            new Dictionary<string, object?>
            {
                ["backend"] = backend,
                ["model"] = model,
                ["inputTokens"] = inputTokens,
                ["outputTokens"] = outputTokens,
                ["durationMs"] = duration.TotalMilliseconds,
            });
}
