# Architecture

Sage is a set of cooperating processes in one .NET 9 solution. A central daemon
owns all intelligence; frontends are thin clients over D-Bus; system access is
isolated in an MCP server behind a safety layer.

## Components

| Component | Project | Responsibility |
|---|---|---|
| **Daemon** | `Sage.Daemon` | Long-running user service (Generic Host + `Microsoft.Extensions.Hosting.Systemd`). Exposes D-Bus interface `org.sage.Sage1` via Tmds.DBus. Owns session/context management, per-source permissions, the audit log, and the model router. |
| **McpServer** | `Sage.McpServer` | MCP server on the official ModelContextProtocol C# SDK, stdio transport, spawned and owned by the Daemon. Exposes Ubuntu system tools. Phase 1 tools are read-only: system info, processes, memory/disk, journald logs, APT package queries, systemd service status. All shell execution goes through the central safety layer (docs/security.md). |
| **Shared** | `Sage.Shared` | Common models and contracts shared by Daemon, McpServer, and frontends: request/response records, tool result shapes, audit event types, `IInferenceBackend`. |
| **Overlay** | `Sage.Overlay` | (Later phase) GTK4/libadwaita overlay window via Gir.Core. Pure D-Bus client of `org.sage.Sage1` — no intelligence of its own. |

A future GNOME Shell extension shim (JavaScript) is another thin D-Bus client and
is out of scope for now.

### Model router

Inside the Daemon, the router selects an inference backend per request behind the
`IInferenceBackend` abstraction:

- **ClaudeBackend** — Claude API (first implementation). Every cloud call is
  audit-logged and user-visible.
- **LocalBackend** — Ollama / LLamaSharp (later). Preferred when capable enough
  for the request (local-first).

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
│     └── LocalBackend  ──────────────► Ollama/LLamaSharp (later)       │
└───────────────┬───────────────────────────────────────────────────────┘
                │  MCP over stdio (child process)
                ▼
┌──────────────────────────── McpServer (Sage.McpServer) ───────────────┐
│  Read-only tools: system info · processes · memory/disk ·             │
│                   journald · APT queries · systemd status             │
│  Central safety layer: allowlist · timeouts · output caps · audit log │
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
src/    Sage.Daemon/  Sage.McpServer/  Sage.Shared/  (Sage.Overlay/ later)
tests/  Sage.Daemon.Tests/  Sage.McpServer.Tests/  Sage.Shared.Tests/
```

Tests must not assume a desktop session (no session bus, no display); D-Bus and
process execution are abstracted behind interfaces and faked.

## Related docs

- D-Bus contract: [dbus-interfaces.md](dbus-interfaces.md)
- Privilege model & safety layer: [security.md](security.md)
- Milestones: [roadmap.md](roadmap.md)
- Decisions: [decisions/](decisions/)
