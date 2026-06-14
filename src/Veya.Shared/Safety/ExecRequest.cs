namespace Veya.Shared.Safety;

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
/// <param name="Detached">
/// When true, the command is run fire-and-forget: stdout/stderr are not
/// captured, and the executor waits only a bounded time for the foreground
/// process to exit, leaving any persistent helper it spawns running. Use this
/// for tools whose helper is <em>meant</em> to outlive the call (e.g.
/// <c>wl-copy</c>, which must stay alive to serve the Wayland clipboard) —
/// capturing their output would hold a pipe open and hang the call. The
/// default (false) captures output and runs to completion.
/// </param>
public sealed record ExecRequest(string Tool, string Binary, IReadOnlyList<string> Arguments, string? StandardInput = null, bool Detached = false);
