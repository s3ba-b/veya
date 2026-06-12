# Roadmap

Status: pre-alpha. Milestone 1 is the walking skeleton; everything after it
builds on the same daemon + D-Bus + MCP spine.

## Milestone 1 — MVP: end-to-end answer on screen

Goal: a question typed into a minimal overlay window gets answered by the Claude
API with real system data from MCP tools.

1. **Scaffolding + CI** — .NET 9 solution with Daemon, McpServer, Shared (+ test
   projects); `./scripts/verify.sh` green in GitHub Actions.
2. **Daemon skeleton** — Generic Host + systemd integration
   (`Microsoft.Extensions.Hosting.Systemd`), runs as a user service.
3. **D-Bus `Ask(string)` stub** — `org.sage.Sage1` registered on the session bus
   via Tmds.DBus, returns a canned reply.
4. **MCP server with `get_system_info`** — ModelContextProtocol C# SDK, stdio
   transport; the central safety layer lands here, before any tool that shells
   out.
5. **Read-only tool groups** — processes, memory/disk, journald logs, APT
   package queries, systemd service status — all through the safety layer.
6. **Claude API backend** — `IInferenceBackend` abstraction + ClaudeBackend with
   tool-use loop; model router scaffolding; audit log for cloud calls.
7. **End-to-end wiring** — Daemon spawns McpServer, discovers tools, routes
   `Ask` through the backend with tool execution.
8. **Overlay window** — minimal GTK4/libadwaita window (Gir.Core): text entry,
   response view, talking to the daemon over D-Bus.

## Milestone 2 — Local models + first write actions

- LocalBackend (Ollama and/or LLamaSharp) behind `IInferenceBackend`; router
  policy for local-vs-cloud with user-visible cloud usage.
- Clipboard writing tools — the first non-read-only tools, gated by per-source
  permissions and fully audit-logged.

## Milestone 3 — Personal context + notification intelligence

- Personal context index: SQLite + sqlite-vec embeddings over user-approved
  sources, per-source permissions enforced at ingestion and query time.
- Notification intelligence: summarize, prioritize, and answer questions about
  desktop notifications.

## Milestone 4 — Voice, screen awareness, GNOME polish

- Voice input/output.
- Screen awareness (explicitly permission-gated, local-first).
- GNOME polish: shell extension shim (JavaScript), keyboard summon, theming.

## Non-goals (for now)

- Non-Linux platforms; non-GNOME desktops are untested but not deliberately
  broken.
- Privileged write actions before the polkit integration (ADR-0003) is built.
