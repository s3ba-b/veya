# Roadmap

Status: pre-alpha. Milestones 1–4 are implemented.
Milestone 1 was the walking skeleton; everything after it builds on the same
daemon + D-Bus + MCP spine. Completed milestones below keep their step lists as
a record of what shipped.

## Milestone 1 — MVP: end-to-end answer on screen — **Done**

Goal: a question typed into a minimal overlay window gets answered by the Claude
API with real system data from MCP tools.

1. **Scaffolding + CI** — .NET 10 solution with Daemon, McpServer, Shared (+ test
   projects); `./scripts/verify.sh` green in GitHub Actions.
2. **Daemon skeleton** — Generic Host + systemd integration
   (`Microsoft.Extensions.Hosting.Systemd`), runs as a user service.
3. **D-Bus `Ask(string)` stub** — `org.veya.Veya1` registered on the session bus
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

## Milestone 2 — Local models + first write actions — **Done**

- LocalBackend (Ollama and/or LLamaSharp) behind `IInferenceBackend`; router
  policy for local-vs-cloud with user-visible cloud usage.
- Cloud backend is config-selectable (ADR-0008): Mistral ("La Plateforme",
  default) or Claude, behind the local-first `FallbackInferenceBackend`.
- Clipboard writing tools — the first non-read-only tools, gated by per-source
  permissions and fully audit-logged.

## Milestone 3 — Personal context + notification intelligence — **Done**

- Personal context index: SQLite + sqlite-vec embeddings over user-approved
  sources, per-source permissions enforced at ingestion and query time.
- Notification intelligence: summarize, prioritize, and answer questions about
  desktop notifications.

## Milestone 4 — Voice, screen awareness, GNOME polish — **Done**

- Voice input/output: local Whisper STT + espeak-ng TTS via `AskVoice`
  (ADR-0015, #77), with a GNOME shell extension mic button calling it (#81).
- ~~Screen awareness (explicitly permission-gated, local-first).~~ **Done** (ADR-0013, #63)
- ~~GNOME polish: shell extension shim (JavaScript), keyboard summon, theming.~~ **Done** (ADR-0014, #65)

## Non-goals (for now)

- Non-Linux platforms; non-GNOME desktops are untested but not deliberately
  broken.
- Privileged write actions before the polkit integration (ADR-0003) is built.
