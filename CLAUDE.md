# CLAUDE.md

## Vision

Veya is an open-source, privacy-transparent, system-wide AI assistant for
Ubuntu/Linux — an Apple Intelligence alternative. A central intelligence daemon
exposes one D-Bus contract (`org.veya.Veya1`) that any frontend can use, with an
MCP server as the system-action layer and hybrid local/cloud inference. Local-first
privacy (per-source permissions, audit log, user-visible cloud usage) is a product
pillar, not a feature.

## Stack (decided — see docs/decisions/, do not relitigate)

- **C# / .NET 10** for all core components. **JavaScript (GJS)** for the GNOME
  Shell extension shim (`src/gnome-shell-extension/`, ADR-0014) — the one
  sanctioned exception to C#. [ADR-0001, ADR-0016]
- One solution, separate projects:
  - **Daemon** (`Veya.Daemon`) — long-running user service. Generic Host +
    `Microsoft.Extensions.Hosting.Systemd`. Exposes D-Bus interface
    `org.veya.Veya1` via **Tmds.DBus**. Owns session/context management and a
    model router behind an `IInferenceBackend` abstraction (cloud backend is
    config-selectable — Claude or Mistral [ADR-0008]; local Ollama backend
    [ADR-0004], LLamaSharp later).
  - **McpServer** (`Veya.McpServer`) — official **ModelContextProtocol** C# SDK,
    stdio transport. Ubuntu system tools. Phase 1 tools are read-only: system
    info, processes, memory/disk, journald logs, APT package queries, systemd
    service status.
  - **Shared** (`Veya.Shared`) — common models and contracts.
  - **Overlay** (`Veya.Overlay`, later phase) — GTK4/libadwaita via **Gir.Core**.
    [ADR-0002]
- **Security:** daemon and MCP server run unprivileged; privileged actions go
  through polkit later. [ADR-0003]

## Build / test

```sh
./scripts/verify.sh
```

That script is the single canonical build + test + format check. CI runs exactly
it. If it passes locally, CI should pass.

## Hard rules

1. **All shell execution goes through the central safety layer** (allowlist,
   timeouts, output caps, audit log — see docs/security.md). Never spawn a
   process directly from a tool. Build/extend the safety layer first if it
   doesn't cover what you need.
2. **Never require sudo without asking the user first.** Code must never assume
   root; setup scripts may use sudo only for declared package installs.
3. **Tests must not assume a desktop session.** No D-Bus session bus, no display
   server, no GNOME — CI is headless ubuntu-latest. Abstract such dependencies
   behind interfaces and fake them in tests.
4. **One issue per branch.** Branch names: `<issue-number>-short-slug`.
5. **Update docs and ADRs when decisions change.** A new decision gets a new ADR
   that supersedes the old one; docs/architecture.md and this file must stay
   consistent with the ADRs.

## Conventions

- Component names are exactly: Daemon, McpServer, Shared, Overlay. Project/
  namespace prefix `Veya.`. D-Bus name everywhere: `org.veya.Veya1` (object path
  `/org/veya/Veya1`).
- C# style is enforced by `.editorconfig` + `dotnet format` (run via verify.sh).
- Tests live in `tests/`, mirroring `src/` project names with a `.Tests` suffix.
- Local-first privacy is a product pillar: every new context source needs a
  permission gate and audit-log coverage; call this out in PR descriptions.
- Docs of record: ROADMAP.md (milestones — source of truth, mirrored as GitHub
  milestones), CHARTER.md (objectives/scope/risks/licensing), PRIVACY.md
  (local-first data model), GLOSSARY.md (shared vocabulary), docs/architecture.md
  (components/data flow), docs/dbus-interfaces.md (D-Bus contract),
  docs/security.md (privilege model, safety layer), docs/decisions/ (ADRs).
- Working by milestone: only the current milestone has issues filed against it.
  When its issues are all closed, break the next milestone down from ROADMAP.md
  into issues before starting (see ROADMAP.md "How to start the next milestone").
