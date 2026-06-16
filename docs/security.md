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
tool that shells out (roadmap M1 step 4), lives in `Veya.Shared.Safety` so the
Daemon and McpServer share one implementation, and no tool may call
`Process.Start` directly — enforced in code review and, later, by an analyzer.

Design (interface and implementation live in `Veya.Shared`):

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

- Append-only JSON Lines under `$XDG_STATE_HOME/veya/audit/` (default
  `~/.local/state/veya/audit/`), rotated by size.
- Event types (factories on `Veya.Shared.Safety.AuditEvent`; every event carries a
  timestamp and never any captured content unless noted):
  - `tool.exec` — safety layer; a command that was run or refused (`allowed` flag),
    with binary, argv, exit code, duration, and truncation flags.
  - `tool.read` *(planned)* — direct file/`/proc` reads that use no subprocess,
    routed through the same logging sink; not yet emitted.
  - `cloud.request` — backend, model, input/output token counts, duration — never
    the prompt or response content unless the user opts in; written by `ClaudeBackend`
    and `MistralBackend` (ADR-0008), both of which send data off the machine.
  - `local.request` — same shape as `cloud.request`, written by `OllamaBackend`
    (ADR-0004); nothing leaves the machine, so this does not trigger `CloudUsage`.
  - `context.ingest` — personal-context ingestion (ADR-0009): source, requester,
    indexed-chunk count, duration — never the indexed text.
  - `context.query` — retrieval against the personal index (ADR-0009): requester,
    sources searched, match count, duration — never the query or chunk text.
  - `notification.capture` — notifications taken into the recent store (ADR-0011):
    count and duration only — never app names, summaries, or bodies.
  - `notification.query` — a read of the notification store, e.g. a digest
    (ADR-0011): returned count and duration only — never notification text.
  - `screen.capture` — a `read_screen_text` call (ADR-0013): success flag, extracted
    text length, duration — never the screenshot or the text.
  - `permission.decision` — every per-source permission check, granted or denied:
    source, requester, `granted` flag.
- Readable by the user, surfaced in the UI later; the `CloudUsage` and
  `ToolExecuted` D-Bus signals (docs/dbus-interfaces.md) mirror it live.
- **Upgrading from a pre-rename (Sage) install:** the audit directory moved from
  `~/.local/state/sage/audit/` to `~/.local/state/veya/audit/` (ADR-0007). There
  is no automatic migration; older trails are simply not read. To keep history,
  move them once by hand before first run of the renamed daemon:
  `mv ~/.local/state/sage ~/.local/state/veya`.

## Per-source permissions

Every context source — clipboard, files, notifications, screen, personal index —
has an independent permission the user grants per source (and later per app).
Defaults are deny. Decisions are audit-logged. No source is read "because it was
convenient"; ingestion and query both check permissions.

Implemented in `Veya.Shared.Permissions` (ADR-0005): `PermissionSource` (the
sources above), `IPermissionStore` (pure, default-deny grant lookup), and
`IPermissionGate` — the single checkpoint every gated action passes through,
which writes a `permission.decision` event for every check. For Milestone 2 the
grant map is **config-based, default-deny** (bound from a `Permissions` section
by the host); interactive/runtime grant UX is deferred to a later milestone.

The personal context index (ADR-0009) is a live source behind this gate: the
`PersonalIndex` permission is checked both when content is ingested
(`ContextIndexer`) and when it is queried for retrieval (`ContextRetriever`),
each writing a `permission.decision` event, plus `context.ingest`/`context.query`
events that carry counts and timing but never the indexed text or the query.

Notification intelligence (ADR-0011) is gated the same way: the `Notifications`
permission is checked when notifications are captured into the recent store and
when that store is read for a digest, with `notification.capture` /
`notification.query` events carrying counts and timing only — never app names,
summaries, or bodies.

Screen awareness (ADR-0013) is gated the same way: the `read_screen_text` MCP
tool checks the `Screen` permission before every call. Even when granted, the
XDG portal screenshot prompt is a second, per-call consent layer the user can
still decline. Capture is on-demand and ephemeral — the screenshot is OCR'd and
deleted immediately, nothing is persisted or indexed, and the `screen.capture`
event carries only a success flag, extracted text length, and duration — never
the image or the text.

## Cloud transparency

- The router prefers local backends when they are capable enough (local-first).
- Every cloud call produces a `cloud.request` audit event and a user-visible
  signal; the UI must make "this left your machine" impossible to miss.
- API keys are stored via libsecret/keyring, never in config files in the repo.
  Interim (Milestone 1): resolved by `ConfigurationApiKeyProvider` (ADR-0008) —
  `IConfiguration` first (`Mistral:ApiKey` / `Anthropic:ApiKey`, which in dev
  means `dotnet user-secrets`, stored outside the repo), falling back to
  environment variables `MISTRAL_API_KEY` / `ANTHROPIC_API_KEY`. The installed
  systemd service runs in Production and uses the env var.

## Threat notes (initial)

- **Prompt injection via tool output** (e.g. a hostile process name or journald
  line): treat all tool output as untrusted data; the safety layer's allowlist
  means a manipulated model can still only run read-only, capped commands.
- **D-Bus callers**: the session bus is per-user, but the daemon should not
  trust callers with privileged operations; polkit covers that when write
  actions arrive.
- **Dependency surface**: MCP tools take no third-party binaries beyond stock
  Ubuntu utilities.
