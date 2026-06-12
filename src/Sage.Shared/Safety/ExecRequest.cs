namespace Sage.Shared.Safety;

/// <summary>
/// A request to run an allowlisted command. <see cref="Arguments"/> is an
/// argv array — it is never interpreted by a shell.
/// </summary>
/// <param name="Tool">Name of the calling MCP tool, for the audit log.</param>
/// <param name="Binary">Logical command name, looked up in the allowlist.</param>
/// <param name="Arguments">Argument vector passed directly to the process.</param>
public sealed record ExecRequest(string Tool, string Binary, IReadOnlyList<string> Arguments);
