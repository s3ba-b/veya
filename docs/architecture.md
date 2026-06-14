# Architecture

Sage is a set of cooperating processes in one .NET 9 solution. A central daemon
owns all intelligence; frontends are thin clients over D-Bus; system access is
isolated in an MCP server behind a safety layer.

## Components

| Component | Project | Responsibility |
|---|---|---|
| **Daemon** | `Sage.Daemon` | Long-running user service (Generic Host + `Microsoft.Extensions.Hosting.Systemd`). Exposes D-Bus interface `org.sage.Sage1` via Tmds.DBus. Owns session/context management, per-source permissions, the audit log, and the model router. |
| **McpServer** | `Sage.McpServer` | MCP server on the official ModelContextProtocol C# SDK, stdio transport, spawned and owned by the Daemon. Exposes Ubuntu system tools. Phase 1 tools are read-only: system info, processes, memory/disk, journald logs, APT package queries, systemd service status. Milestone 2 adds the first write tool, `set_clipboard`, gated by per-source permissions (ADR-0005) and writing via `wl-copy`/`xclip` (ADR-0006). All shell execution goes through the central safety layer (docs/security.md). |
| **Shared** | `Sage.Shared` | Common models and contracts shared by Daemon, McpServer, and frontends: request/response records, tool result shapes, audit event types, `IInferenceBackend`. |
| **Overlay** | `Sage.Overlay` | GTK4/libadwaita overlay window via Gir.Core (ADR-0002). Pure D-Bus client of `org.sage.Sage1` — no intelligence of its own. `OverlayViewModel` sends the prompt via `Sage1Client` and returns the reply or a friendly error if the daemon is unreachable. |

A future GNOME Shell extension shim (JavaScript) is another thin D-Bus client and
is out of scope for now.

### Model router

Inside the Daemon, `Sage.Daemon.IModelRouter` (implemented by `ModelRouter`)
selects an inference backend per request behind the `IInferenceBackend`
abstraction and drives the request/response cycle via `ToolUseLoopRunner`:

- **ClaudeBackend** — Claude API. Every cloud call is audit-logged
  (`cloud.request`) and user-visible.
- **OllamaBackend** (ADR-0004) — a local Ollama server's `/api/chat` HTTP API.
  Every call is audit-logged (`local.request`), but since nothing leaves the
  machine this does not trigger `CloudUsage`.

Milestone 2: `IInferenceBackend` is `FallbackInferenceBackend(local: OllamaBackend,
cloud: ClaudeBackend)` — the local-first policy from docs/security.md
("Cloud transparency"). Each request tries Ollama first; if it throws
`BackendUnavailableException` (e.g. Ollama isn't running), the request falls
back to Claude. `ModelRouter` and `ToolUseLoopRunner` are backend-agnostic and
unaffected by which backend actually answered. The `CloudUsage`/`ActiveBackend`
D-Bus surface (docs/dbus-interfaces.md) is a follow-up — in the meantime, the
`local.request`/`cloud.request` audit log entries record which backend handled
each call. Tool definitions come
from `Sage.Daemon.Mcp.IMcpToolGateway` (`McpToolGateway`), which spawns
`Sage.McpServer` as a child process over stdio (via the `ModelContextProtocol`
client SDK), discovers its tools on first use, and executes tool calls
requested by the model. If the McpServer process can't be started or reached,
the gateway logs a warning and returns no tools, so `Ask` falls back to a
plain-text reply with no tool calls — the same graceful-degradation pattern
used for the D-Bus session bus.

## Diagram

```
┌────────────────────────────── Frontends ──────────────────────────────┐
│   Overlay (GTK4/Gir.Core)      GNOME Shell shim (later)      CLI      │
└───────────────┬───────────────────────┬──────────────────────┬────────┘
                │            D-Bus session bus                 │
                │        org.sage.Sage1  /org/sage/Sage1       │
                ▼                                              ▼
┌───────────────────────────── Daemon (Sage.Daemon) ────────────────────┐
│  D-Bus endpoint (Tmds.DBus)                                           │
│  Session & context manager ── per-source permissions ── audit log     │
│  Model router (IInferenceBackend)                                     │
│     ├── ClaudeBackend ──────────────► Claude API   (cloud, logged)    │
│     └── OllamaBackend ──────────────► local Ollama (local, logged)    │
└───────────────┬───────────────────────────────────────────────────────┘
                │  MCP over stdio (child process)
                ▼
┌──────────────────────────── McpServer (Sage.McpServer) ───────────────┐
│  Read-only tools: system info · processes · memory/disk ·             │
│                   journald · APT queries · systemd status             │
│  Write tools:     set_clipboard (permission-gated, ADR-0005/0006)     │
│  Central safety layer: allowlist · timeouts · output caps · audit log │
│  Permission gate: per-source, default-deny, audit-logged decisions    │
└───────────────┬───────────────────────────────────────────────────────┘
                ▼
        Ubuntu system (unprivileged; polkit for privileged actions, later)
```

Both Daemon and McpServer run unprivileged in the user session. `Sage.Shared` is
referenced by all of the above.

## Data flow: "what's eating my RAM?"

1. **Overlay** — the user opens the overlay and types the question. The overlay
   calls `Ask("what's eating my RAM?")` on `org.sage.Sage1`.
2. **D-Bus** — the session bus routes the call to the Daemon, which creates (or
   resumes) a session and appends the query to its context.
3. **Daemon / router** — the router picks a backend (Claude API in Milestone 1)
   and sends the conversation plus the MCP tool definitions it has discovered
   from the McpServer.
4. **Model → tool calls** — the model decides it needs data and requests e.g.
   `list_processes` (sorted by memory) and `get_memory_info`.
5. **MCP tools** — the Daemon forwards each tool call over stdio to the
   McpServer. The safety layer validates the command against the allowlist,
   enforces timeout and output caps, executes, and writes an audit event.
6. **Response** — tool results go back to the model, which produces an answer
   ("Firefox is using 6.2 GB across 14 processes…"). The Daemon audit-logs the
   exchange and returns the answer over D-Bus; the overlay renders it.

## Source layout

```
src/    Sage.Daemon/  Sage.McpServer/  Sage.Shared/  Sage.Overlay/
tests/  Sage.Daemon.Tests/  Sage.McpServer.Tests/  Sage.Shared.Tests/  Sage.Overlay.Tests/
```

Tests must not assume a desktop session (no session bus, no display); D-Bus and
process execution are abstracted behind interfaces and faked.

## Related docs

- D-Bus contract: [dbus-interfaces.md](dbus-interfaces.md)
- Privilege model & safety layer: [security.md](security.md)
- Milestones: [roadmap.md](roadmap.md)
- Decisions: [decisions/](decisions/)
