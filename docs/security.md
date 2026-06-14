# Security & privacy model

Local-first privacy is a product pillar: per-source permissions, a complete audit
log, and user-visible cloud usage. This document defines the privilege model and
the safety layer that every system action must pass through.

## Privilege model

- **Daemon and McpServer run unprivileged**, as systemd user services in the
  user's session. They never run as root and never assume root.
- **Phase 1 tools are read-only** (system info, processes, memory/disk, journald,
  APT queries, systemd status) and require no elevation.
- **Privileged actions go through polkit** (ADR-0003), later: a small polkit-
  authorized helper performs specific, declared actions after explicit user
  authentication. No general "run as root" path will ever exist.
- Setup scripts (`scripts/setup-dev.sh`) may use sudo only for declared package
  installs; runtime code never invokes sudo. Hard rule: never require sudo
  without asking the user.

## Safety layer (central shell-execution gateway)

**All shell execution goes through this one abstraction.** It is built before any
tool that shells out (roadmap M1 step 4), lives in `Sage.Shared.Safety` so the
Daemon and McpServer share one implementation, and no tool may call
`Process.Start` directly — enforced in code review and, later, by an analyzer.

Design (interface and implementation live in `Sage.Shared`):

```csharp
public interface ISafeExecutor
{
    Task<ExecResult> RunAsync(ExecRequest request, CancellationToken ct);
}
```

Responsibilities:

1. **Allowlist** — only known binaries with validated argument shapes
   (e.g. `ps`, `free`, `df`, `journalctl`, `apt-cache`, `systemctl status`).
   Arguments are passed as argv arrays — never through a shell, no string
   interpolation, no `sh -c`.
2. **Timeouts** — every execution has a hard timeout (default a few seconds);
   the process tree is killed on expiry.
3. **Output caps** — stdout/stderr are capped (bounded buffers); truncation is
   flagged in the result rather than silently dropped.
4. **Audit log** — every request is logged (allowed or denied): timestamp, tool,
   binary, argv, exit code, duration, truncation flag.

Tools that read files or `/proc` directly (no subprocess) still produce audit
events through the same logging sink.

## Audit log

- Append-only JSON Lines under `$XDG_STATE_HOME/sage/audit/` (default
  `~/.local/state/sage/audit/`), rotated by size.
- Event types: `tool.exec` (safety layer), `tool.read` (direct reads),
  `cloud.request` (backend, model, byte counts — never the prompt content
  itself unless the user opts in), `local.request` (same shape as
  `cloud.request`, written by `OllamaBackend`, ADR-0004; nothing leaves the
  machine so this does not trigger `CloudUsage`), `permission.decision`.
- Readable by the user, surfaced in the UI later; the `CloudUsage` and
  `ToolExecuted` D-Bus signals (docs/dbus-interfaces.md) mirror it live.

## Per-source permissions

Every context source — clipboard, files, notifications, screen, personal index —
has an independent permission the user grants per source (and later per app).
Defaults are deny. Decisions are audit-logged. No source is read "because it was
convenient"; ingestion and query both check permissions.

Implemented in `Sage.Shared.Permissions` (ADR-0005): `PermissionSource` (the
sources above), `IPermissionStore` (pure, default-deny grant lookup), and
`IPermissionGate` — the single checkpoint every gated action passes through,
which writes a `permission.decision` event for every check. For Milestone 2 the
grant map is **config-based, default-deny** (bound from a `Permissions` section
by the host); interactive/runtime grant UX is deferred to a later milestone.

## Cloud transparency

- The router prefers local backends when they are capable enough (local-first).
- Every cloud call produces a `cloud.request` audit event and a user-visible
  signal; the UI must make "this left your machine" impossible to miss.
- API keys are stored via libsecret/keyring, never in config files in the repo.

## Threat notes (initial)

- **Prompt injection via tool output** (e.g. a hostile process name or journald
  line): treat all tool output as untrusted data; the safety layer's allowlist
  means a manipulated model can still only run read-only, capped commands.
- **D-Bus callers**: the session bus is per-user, but the daemon should not
  trust callers with privileged operations; polkit covers that when write
  actions arrive.
- **Dependency surface**: MCP tools take no third-party binaries beyond stock
  Ubuntu utilities.
