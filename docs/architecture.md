# Architecture

Veya is a set of cooperating processes in one .NET 9 solution. A central daemon
owns all intelligence; frontends are thin clients over D-Bus; system access is
isolated in an MCP server behind a safety layer.

## Components

| Component | Project | Responsibility |
|---|---|---|
| **Daemon** | `Veya.Daemon` | Long-running user service (Generic Host + `Microsoft.Extensions.Hosting.Systemd`). Exposes D-Bus interface `org.veya.Veya1` via Tmds.DBus. Owns session/context management, per-source permissions, the audit log, and the model router. |
| **McpServer** | `Veya.McpServer` | MCP server on the official ModelContextProtocol C# SDK, stdio transport, spawned and owned by the Daemon. Exposes Ubuntu system tools. Phase 1 tools are read-only: system info, processes, memory/disk, journald logs, APT package queries, systemd service status. Milestone 2 adds the first write tool, `set_clipboard`, gated by per-source permissions (ADR-0005) and writing via `wl-copy`/`xclip` (ADR-0006). All shell execution goes through the central safety layer (docs/security.md). |
| **Shared** | `Veya.Shared` | Common models and contracts shared by Daemon, McpServer, and frontends: request/response records, tool result shapes, audit event types, `IInferenceBackend`. |
| **Overlay** | `Veya.Overlay` | GTK4/libadwaita overlay window via Gir.Core (ADR-0002). Pure D-Bus client of `org.veya.Veya1` — no intelligence of its own. `OverlayViewModel` sends the prompt via `Veya1Client` and returns the reply or a friendly error if the daemon is unreachable. |

A future GNOME Shell extension shim (JavaScript) is another thin D-Bus client and
is out of scope for now.

### Model router

Inside the Daemon, `Veya.Daemon.IModelRouter` (implemented by `ModelRouter`)
selects an inference backend per request behind the `IInferenceBackend`
abstraction and drives the request/response cycle via `ToolUseLoopRunner`:

- **ClaudeBackend** — Claude API. Every cloud call is audit-logged
  (`cloud.request`) and user-visible.
- **MistralBackend** (ADR-0008) — Mistral's hosted API ("La Plateforme")
  `/v1/chat/completions`. A cloud backend like ClaudeBackend: data leaves the
  machine, so every call is audit-logged (`cloud.request`, `backend="mistral"`)
  and user-visible.
- **OllamaBackend** (ADR-0004) — a local Ollama server's `/api/chat` HTTP API.
  Every call is audit-logged (`local.request`), but since nothing leaves the
  machine this does not trigger `CloudUsage`.

Milestone 2: `IInferenceBackend` is `FallbackInferenceBackend(local: OllamaBackend,
cloud: <selected>)` — the local-first policy from docs/security.md
("Cloud transparency"). The cloud tier is config-selectable (ADR-0008):
`Inference:CloudBackend` picks `mistral` (default) or `claude`. Each request
tries Ollama first; if it throws `BackendUnavailableException` (e.g. Ollama
isn't running), the request falls back to the chosen cloud backend.
`ModelRouter` and `ToolUseLoopRunner` are backend-agnostic and
unaffected by which backend actually answered. Which backend served is surfaced
over D-Bus from those same audit entries: `BackendActivityAuditLog` decorates the
audit log, tracking the active backend (read via `GetStatus`) and raising the
`CloudUsage` signal on every `cloud.request` (docs/dbus-interfaces.md) — so the
live D-Bus surface can never drift from the recorded `local.request`/`cloud.request`
trail. Tool definitions come
from `Veya.Daemon.Mcp.IMcpToolGateway` (`McpToolGateway`), which spawns
`Veya.McpServer` as a child process over stdio (via the `ModelContextProtocol`
client SDK), discovers its tools on first use, and executes tool calls
requested by the model. If the McpServer process can't be started or reached,
the gateway logs a warning and returns no tools, so `Ask` falls back to a
plain-text reply with no tool calls — the same graceful-degradation pattern
used for the D-Bus session bus.

### Personal context index

The Daemon can ground answers in user-approved personal content (ADR-0009),
behind the same per-source permission gate as every other source (ADR-0005). It
lives in `Veya.Shared.Context`:

- **`IEmbeddingBackend`** — turns text into vectors. **`OllamaEmbeddingBackend`**
  computes them locally (`/api/embed`, audit-logged `local.request`); a cloud
  embedding backend is deferred, so the index adds no cloud egress and never
  trips `CloudUsage`.
- **`IContextStore`** — **`SqliteContextStore`**, SQLite + the `sqlite-vec`
  extension. KNN search filters to permitted sources inside the query (a `source`
  metadata column on the `vec0` table), so a revoked source cannot surface.
- **`ContextIndexer`** (ingest) and **`ContextRetriever`** (query) — each checks
  the source's permission through `IPermissionGate`, so permission is enforced at
  **both** ingestion and query time. Both degrade rather than break when Ollama
  is down: ingestion skips and re-tries later, retrieval returns nothing and
  `Ask` answers without personal context.

`ModelRouter` folds retrieved context into the system prompt via an
`IContextProvider` (`ContextRetrievalProvider`) before calling the backend.

The first concrete source is **`FileContextSource`** (ADR-0010, `Files`
permission): it walks user-approved root folders (`Context:Files` config), reads
matching text files, and chunks them via `TextChunker`. `ContextIndexingService`
re-indexes the registered sources on daemon startup (`replaceExisting`, so a
re-index leaves no stale chunks). Notification and other sources arrive with
their own ADRs.

## Diagram

```
┌────────────────────────────── Frontends ──────────────────────────────┐
│   Overlay (GTK4/Gir.Core)      GNOME Shell shim (later)      CLI      │
└───────────────┬───────────────────────┬──────────────────────┬────────┘
                │            D-Bus session bus                 │
                │        org.veya.Veya1  /org/veya/Veya1       │
                ▼                                              ▼
┌───────────────────────────── Daemon (Veya.Daemon) ────────────────────┐
│  D-Bus endpoint (Tmds.DBus)                                           │
│  Session & context manager ── per-source permissions ── audit log     │
│  Model router (IInferenceBackend)                                     │
│     ├── ClaudeBackend ──────────────► Claude API   (cloud, logged)    │
│     ├── MistralBackend ─────────────► Mistral API  (cloud, logged)    │
│     └── OllamaBackend ──────────────► local Ollama (local, logged)    │
└───────────────┬───────────────────────────────────────────────────────┘
                │  MCP over stdio (child process)
                ▼
┌──────────────────────────── McpServer (Veya.McpServer) ───────────────┐
│  Read-only tools: system info · processes · memory/disk ·             │
│                   journald · APT queries · systemd status             │
│  Write tools:     set_clipboard (permission-gated, ADR-0005/0006)     │
│  Central safety layer: allowlist · timeouts · output caps · audit log │
│  Permission gate: per-source, default-deny, audit-logged decisions    │
└───────────────┬───────────────────────────────────────────────────────┘
                ▼
        Ubuntu system (unprivileged; polkit for privileged actions, later)
```

Both Daemon and McpServer run unprivileged in the user session. `Veya.Shared` is
referenced by all of the above.

## Data flow: "what's eating my RAM?"

1. **Overlay** — the user opens the overlay and types the question. The overlay
   calls `Ask("what's eating my RAM?")` on `org.veya.Veya1`.
2. **D-Bus** — the session bus routes the call to the Daemon, which creates (or
   resumes) a session and appends the query to its context.
3. **Daemon / router** — the router picks a backend (Claude API in Milestone 1),
   folds in any relevant personal context retrieved from the index (ADR-0009,
   permission-checked at query time), and sends the conversation plus the MCP
   tool definitions it has discovered from the McpServer.
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
src/    Veya.Daemon/  Veya.McpServer/  Veya.Shared/  Veya.Overlay/
tests/  Veya.Daemon.Tests/  Veya.McpServer.Tests/  Veya.Shared.Tests/  Veya.Overlay.Tests/
```

Tests must not assume a desktop session (no session bus, no display); D-Bus and
process execution are abstracted behind interfaces and faked.

## Related docs

- D-Bus contract: [dbus-interfaces.md](dbus-interfaces.md)
- Privilege model & safety layer: [security.md](security.md)
- Milestones: [roadmap.md](roadmap.md)
- Decisions: [decisions/](decisions/)
