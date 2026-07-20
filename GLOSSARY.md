# Glossary

Shared vocabulary for Veya — the terms used across the code, ADRs, and docs.
Use these as defined rather than inventing synonyms (a rule that applies to
humans and AI agents alike — see [CLAUDE.md](CLAUDE.md)). Kept short; widely
understood terms aren't listed.

## Core concepts

| Term | Meaning |
|---|---|
| **Veya** | The project: an open-source, privacy-transparent, system-wide AI assistant for Ubuntu/Linux — an Apple Intelligence alternative. |
| **Daemon** | The long-running, unprivileged user service (`Veya.Daemon`) that owns sessions, context, and the model router, and exposes the D-Bus contract. The single brain any frontend talks to. |
| **McpServer** | The system-action layer (`Veya.McpServer`) exposing Ubuntu system tools over the Model Context Protocol; spawned by the Daemon. |
| **Shared** | The common library (`Veya.Shared`) holding contracts, the safety layer, permissions, inference backends, and context — code both the Daemon and McpServer depend on. |
| **Overlay** | The minimal GTK4/libadwaita window (`Veya.Overlay`, Gir.Core) — one frontend among several ([ADR-0002](docs/decisions/0002-ui-toolkit-gircore.md)). |
| **Frontend** | Any UI that talks to the Daemon over D-Bus: the Overlay, the GNOME Shell extension, a CLI, or a third-party client. The Daemon is frontend-agnostic. |
| **Local-first** | The design principle that local models and on-device processing are first-class; the cloud is a fallback, never the default, and cloud usage is always user-visible. A product pillar, not a feature. |

## Technical terms

| Term | Meaning |
|---|---|
| **D-Bus contract** | The one interface every frontend uses: `org.veya.Veya1` (object path `/org/veya/Veya1`), served on the session bus via Tmds.DBus. See [docs/dbus-interfaces.md](docs/dbus-interfaces.md). |
| **`Ask` / `AskVoice`** | The primary D-Bus methods: answer a typed question, or record + transcribe + answer a spoken one ([ADR-0015](docs/decisions/0015-voice-io.md)). |
| **MCP (Model Context Protocol)** | The protocol (official C# SDK, stdio transport) by which the Daemon discovers and calls the McpServer's tools. |
| **Tool** | A single system capability exposed by the McpServer (e.g. `get_system_info`, `read_screen_text`). Phase 1 tools are read-only; writes are permission-gated. |
| **Safety layer** | The central shell-execution gateway (`Veya.Shared.Safety`, `ISafeExecutor`) every command must pass through: allowlist, argv-only (no shell), timeouts, output caps, and audit logging. No tool calls `Process.Start` directly. See [docs/security.md](docs/security.md). |
| **Allowlist** | The set of known binaries with validated argument shapes the safety layer permits; anything else is refused and audit-logged. |
| **Audit log** | The append-only JSON Lines record under `~/.local/state/veya/audit/` of every tool execution, cloud/local inference request, context/notification/screen/voice access, and permission decision — metadata (who, what, when, counts, timing) but **never captured content** unless the user opts in. |
| **Per-source permission** | An independent, default-deny grant the user gives per context source (clipboard, files, notifications, screen, personal index, microphone). Enforced at ingestion *and* query time via `IPermissionGate` ([ADR-0005](docs/decisions/0005-per-source-permissions.md)); every check writes a `permission.decision` event. |
| **Context source** | A source of user data Veya can draw on for context — clipboard, files, notifications, screen, personal index. Each is gated by its own per-source permission. |
| **Personal context index** | A local SQLite + sqlite-vec embedding store over user-approved sources, queried for retrieval-augmented answers, gated at ingest and query time ([ADR-0009](docs/decisions/0009-personal-context-index.md)). |
| **Notification intelligence** | Capturing, storing, summarizing, prioritizing, and answering questions about desktop notifications ([ADR-0011](docs/decisions/0011-notification-intelligence.md)). |
| **Screen awareness** | On-demand, permission-gated `read_screen_text` — an XDG-portal screenshot is OCR'd (tesseract) and immediately discarded; nothing is persisted ([ADR-0013](docs/decisions/0013-screen-awareness.md)). |
| **`IInferenceBackend`** | The abstraction over an LLM provider. Implementations: `ClaudeBackend` and `MistralBackend` (cloud, [ADR-0008](docs/decisions/0008-mistral-cloud-backend.md)) and `OllamaBackend` (local, [ADR-0004](docs/decisions/0004-local-backend-ollama.md)). |
| **Model router** | The Daemon component that picks a backend per request under a local-first policy, falling back to cloud only when needed (`FallbackInferenceBackend`), and surfaces cloud usage. |
| **Cloud usage** | The user-visible signal, mirrored from the `cloud.request` audit event, that a request left the machine — the router makes "this went to the cloud" impossible to miss. |
| **GNOME Shell extension** | The one sanctioned JavaScript (GJS) component (`src/gnome-shell-extension/`, [ADR-0014](docs/decisions/0014-gnome-shell-extension.md)): keyboard summon + a panel mic button that calls the Daemon. |
| **polkit** | The mechanism through which future **privileged** actions will be authorized ([ADR-0003](docs/decisions/0003-privilege-model-polkit.md)) — a small polkit-authorized helper for specific declared actions. No general "run as root" path exists. |

## Process & project terms

| Term | Meaning |
|---|---|
| **ADR (Architecture Decision Record)** | A numbered decision record under [docs/decisions/](docs/decisions/). Decisions aren't relitigated in a PR — a change means a new ADR that supersedes the old one. |
| **Safety layer / audit log / permission gate** | Together, the three enforcement points behind the local-first privacy pillar — see [docs/security.md](docs/security.md) and [PRIVACY.md](PRIVACY.md). |
| **Milestone** | A working, demoable slice of the system defined by a "definition of done", not a calendar date. See [ROADMAP.md](ROADMAP.md). |
| **`verify.sh`** | The single canonical build + test + format + license check (`./scripts/verify.sh`); CI runs exactly it. |
| **AGPL-3.0** | The project license; running Veya as a network service obliges the operator to offer the complete corresponding source to its users (§13). See [LICENSE](LICENSE). |
| **CLA / DCO** | The Contributor License Agreement ([CLA.md](CLA.md)) every contributor signs once (via the CLA Assistant bot) before a first merge. |

---

**Related:** [CHARTER.md](CHARTER.md) · [PRIVACY.md](PRIVACY.md) · [docs/architecture.md](docs/architecture.md) · [docs/security.md](docs/security.md)
