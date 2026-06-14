namespace Sage.Shared.Safety;

/// <summary>
/// A request to run an allowlisted command. <see cref="Arguments"/> is an
/// argv array — it is never interpreted by a shell.
/// </summary>
/// <param name="Tool">Name of the calling MCP tool, for the audit log.</param>
/// <param name="Binary">Logical command name, looked up in the allowlist.</param>
/// <param name="Arguments">Argument vector passed directly to the process.</param>
/// <param name="StandardInput">
/// Optional text written to the process's stdin (then closed). Use this for
/// content that must not appear in the audit log — only argv is recorded, so
/// e.g. clipboard text passed here stays out of the trail. <c>null</c> leaves
/// stdin unredirected.
/// </param>
public sealed record ExecRequest(string Tool, string Binary, IReadOnlyList<string> Arguments, string? StandardInput = null);
