# Project Charter

| Field | Value |
|---|---|
| **Project name** | Veya — a privacy-transparent, system-wide AI assistant for Ubuntu/Linux |

## Project objectives (what we want to achieve)

Build an open-source, **local-first** AI assistant that any Linux desktop frontend
can use through a single D-Bus contract — an Apple Intelligence alternative for
Ubuntu/GNOME, where the user, not a cloud vendor, decides what the assistant may
see and when anything leaves the machine.

The project pursues three parallel goals:

1. **Solve a real problem.** Desktop AI today is either a cloud silo bolted onto a
   single app or opaque about what leaves your machine. A system-wide assistant
   that is private by construction — local models first, per-source permissions,
   a readable audit log — is a genuine contribution to the Linux desktop.
2. **Demonstrate a clean architecture.** One intelligence **daemon** exposing a
   stable D-Bus contract (`org.veya.Veya1`), an **MCP server** as the system-action
   layer behind a central safety layer, and hybrid local/cloud inference behind a
   single `IInferenceBackend` abstraction — a design that keeps frontends,
   system access, and models cleanly decoupled.
3. **Serve as a portfolio piece.** A well-documented, openly licensed C#/.NET
   codebase that demonstrates competence in system integration (D-Bus, systemd,
   MCP), safety/audit engineering, and pragmatic local/cloud AI orchestration.

## Results and scope

| | |
|---|---|
| **Expected products** | • A long-running, unprivileged **Daemon** (`Veya.Daemon`) exposing `org.veya.Veya1` over D-Bus, owning sessions, context, and a local-first model router.<br>• An **McpServer** (`Veya.McpServer`) exposing Ubuntu system tools over MCP, every command passing through a central safety layer.<br>• A **Shared** library (`Veya.Shared`) with contracts, the safety layer, per-source permissions, inference backends, and the personal-context index.<br>• Frontends: a minimal GTK4/libadwaita **Overlay** and a **GNOME Shell extension** (the one sanctioned JS component).<br>• Open-source repository under **AGPL-3.0**, with README, architecture doc, ADRs, security/privacy docs, and a project website. |
| **Main functionalities** | • **System-wide access via one contract:** any frontend talks to one daemon over D-Bus (`Ask`/`AskVoice`), no per-app silos.<br>• **Hybrid inference:** local backend (Ollama) preferred, cloud (Claude or Mistral) as a user-visible fallback behind `IInferenceBackend`.<br>• **System-action tools (MCP):** read-only system info, processes, memory/disk, journald, APT, systemd status — plus permission-gated writes (clipboard).<br>• **Personal context:** an on-device index over user-approved sources (files), with per-source permissions at ingest and query.<br>• **Notification intelligence:** capture, summarize, prioritize, and answer questions about desktop notifications.<br>• **Screen & voice awareness:** on-demand, permission-gated, ephemeral OCR and local speech-to-text.<br>• **Transparency:** an append-only audit log of every tool call, inference request, context access, and permission decision — metadata only, never content. |

## Success measures

- A question typed into a frontend is answered end-to-end using real system data
  drawn through MCP tools, with the whole path running as unprivileged user
  services (no root, no display assumptions in the core).
- The model router prefers a local backend and only reaches the cloud when it must
  — and **every** cloud call is visible to the user (a `CloudUsage` signal) and
  recorded (`cloud.request` audit event).
- Every context source (clipboard, files, notifications, screen, microphone,
  personal index) is behind an independent, default-deny permission that is checked
  at ingest **and** query time, and every check is audit-logged.
- No tool ever calls `Process.Start` directly — all shell execution passes the
  central safety layer (allowlist, argv-only, timeouts, output caps, audit).
- The repository builds, tests, and format-checks through one script
  (`./scripts/verify.sh`) that CI runs verbatim, and every architectural decision
  is recorded as an ADR.
- The project is licensed under AGPL-3.0 and documented well enough for a newcomer
  to build and run the daemon locally.

## Stakeholders

Stakeholders include both people and non-human factors whose existence shapes the
system's requirements.

| Stakeholder | Type | Stake / influence |
|---|---|---|
| Desktop user | Human, direct | Primary user and **data controller** of their own machine; grants context sources, sees cloud usage. |
| Frontend authors | Human, direct | Build UIs against the D-Bus contract (overlay, shell extension, CLI, third-party); depend on its stability. |
| Project author / contributors | Human, direct | Build and maintain Veya; bound by AGPL + CLA. |
| Cloud inference providers (Anthropic, Mistral) | Inanimate, indirect | Invoked only on cloud fallback; their APIs, models, and terms constrain that path ([ADR-0008](docs/decisions/0008-mistral-cloud-backend.md)). |
| GNOME / Ubuntu platform (D-Bus, systemd, XDG portals, GTK4, GJS) | Inanimate, direct | Their APIs and release cadence constrain structure and integration ([ADR-0002](docs/decisions/0002-ui-toolkit-gircore.md), [ADR-0014](docs/decisions/0014-gnome-shell-extension.md)). |
| polkit | Inanimate, direct (future) | The sole sanctioned path for privileged actions ([ADR-0003](docs/decisions/0003-privilege-model-polkit.md)). |
| GDPR / RODO (only if ever hosted) | Inanimate, indirect | Not engaged by local-only use; would apply to any future hosted deployment — see [PRIVACY.md](PRIVACY.md). |

## Constraints

- **Solo developer, part-time effort.** Scope stays focused on the daemon, MCP
  tools, hybrid inference, and a small set of GNOME-facing frontends. Breadth is
  bounded by the milestone arc in [ROADMAP.md](ROADMAP.md).
- **Technology constraints:** C#/.NET 10 for all core components
  ([ADR-0001](docs/decisions/0001-language-csharp.md),
  [ADR-0016](docs/decisions/0016-dotnet-10-upgrade.md)); JavaScript (GJS) only for
  the GNOME Shell extension shim ([ADR-0014](docs/decisions/0014-gnome-shell-extension.md)),
  the one sanctioned exception. UI via GTK4/libadwaita through Gir.Core
  ([ADR-0002](docs/decisions/0002-ui-toolkit-gircore.md)).
- **Linux / GNOME first.** Non-Linux platforms are out of scope; non-GNOME desktops
  are untested but not deliberately broken.
- **Headless-testable core.** CI is headless `ubuntu-latest`; tests must not assume
  a D-Bus session bus, display server, or GNOME — such dependencies are abstracted
  behind interfaces and faked (a hard rule, see [CLAUDE.md](CLAUDE.md)).
- **Unprivileged by default.** The daemon and MCP server never run as root and never
  assume it; privileged actions are deferred to a polkit-authorized helper
  ([ADR-0003](docs/decisions/0003-privilege-model-polkit.md)). Setup scripts may use
  sudo only for declared package installs, and never without asking.
- **Non-commercial framing.** No paid infrastructure is required to develop or run
  Veya; cloud inference is optional and uses the user's own API key.

## Working methodology

The project follows an **iterative-incremental** approach, suited to a solo,
part-time effort: work is delivered as a sequence of increments, each a working,
demoable slice of the system rather than a horizontal layer built in isolation.

- **Walking skeleton first.** The opening milestone (M1) put the architectural
  spine in place end-to-end — Daemon + D-Bus + MCP + safety layer + one inference
  backend + a minimal overlay — before breadth. Every later feature hangs off that
  same spine instead of being retrofitted.
- **Vertical slices.** Each subsequent capability (local models + writes, personal
  context + notifications, voice + screen) is built end-to-end as a self-contained
  increment, permission-gated and audit-covered as it lands.
- **Privacy as a continuous gate.** Local-first privacy is a product pillar, not a
  late add-on: every new context source ships with its permission gate and
  audit-log coverage in the same increment. A source without a gate is not "done".
- **Done = running software.** Milestones are defined by working, demoable
  software, not calendar dates, and are ordered by dependency.

The full milestone breakdown — each with its definition of done — is the **single
source of truth** in [ROADMAP.md](ROADMAP.md) (and mirrored as GitHub milestones).
This charter deliberately does not restate those definitions, so the two can't
drift; the arc at a glance:

- **M1 — MVP: end-to-end answer on screen** *(done)* — the walking skeleton.
- **M2 — Local models + first write actions** *(done)*.
- **M3 — Personal context + notification intelligence** *(done)*.
- **M4 — Voice, screen awareness, GNOME polish** *(done)*.
- **M5 — Privileged actions & runtime permission UX** — polkit helper + interactive
  per-source grants.
- **M6 — Local-model parity & offline** — a capable in-process/local path that
  answers most queries with zero cloud egress.
- **M7 — Hardening & first release** — packaging, install docs, security review,
  first tagged release.

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| **Context leaks to the cloud without the user knowing** | Highest severity — it breaks the project's core premise (local-first, transparent). | Local-first router preference; **every** cloud call emits a `cloud.request` audit event *and* a user-visible `CloudUsage` signal; the audit log records metadata only, never content, unless the user opts in. See [PRIVACY.md](PRIVACY.md). |
| **A tool bypasses the safety layer** | A tool spawning a process directly escapes the allowlist/timeout/output-cap/audit guarantees. | Single `ISafeExecutor` gateway; no `Process.Start` in tools (enforced in review, later by an analyzer); build/extend the safety layer before any new shelling-out tool. |
| **Unintended privilege escalation** | A path that runs as root undermines the unprivileged model. | Daemon and MCP server run unprivileged; the only privileged path is a future, narrowly-declared polkit helper ([ADR-0003](docs/decisions/0003-privilege-model-polkit.md)); never require sudo without asking. |
| **Prompt injection via tool output** | Hostile process names / journald lines steer the model. | Treat all tool output as untrusted data; the allowlist means even a manipulated model can only run read-only, capped commands. |
| **Platform churn (GNOME/D-Bus/GTK/Aspire-style deps)** | Time lost to breaking changes across GNOME/GTK releases. | Isolate platform-specific bindings behind interfaces; pin and update deliberately; keep the core headless-testable. |
| **Solo / part-time capacity** | Later milestones may slip. | Incremental milestones with the walking skeleton first, so the project is demoable even if M5–M7 slip. |

## Licensing strategy

Licensing keeps Veya freely usable while preventing any party from capturing it into
a closed product. See [ADR-0017](docs/decisions/0017-license-agpl-and-cla.md).

- **License: AGPL-3.0-or-later.** Strong copyleft with the network-use clause: anyone
  may use, self-host, and modify Veya, but anyone who offers it as a network service
  must publish their complete corresponding source (§13). This removes the incentive
  to fork-and-close.
- **Contributor License Agreement (CLA).** Every contributor signs once (via the CLA
  Assistant bot) so copyright stays consolidated enough to enforce — and, if ever
  needed, relicense — the project as a whole. See [CLA.md](CLA.md).
- **Name / identity.** The project name and identity ("Veya") are protected
  separately from the code; a code license does not authorize passing a fork off as
  the official project.

## Legal & compliance considerations

*Forward-looking checklist, not legal advice.* Veya today is **local-only**
software with no hosted service and no telemetry, so most data-protection
obligations are not engaged — the user is their own controller on their own machine
(see [PRIVACY.md](PRIVACY.md)).

- **GDPR / RODO** would apply only to a future **hosted** deployment (operator ↔
  user as processor ↔ controller). That is out of scope; revisit only if a hosted
  offering is ever proposed.
- **Cloud inference terms.** On a cloud-fallback call, the configured provider's
  (Anthropic / Mistral) data-handling terms govern what is sent. Veya sends nothing
  to a provider the user has not configured, and nothing at all when a local backend
  answers.
- **AGPL network clause.** Any operator who ever runs Veya as a service must offer
  its source to users (§13).
- **Telemetry.** None today; introducing any would require an explicit opt-in
  decision recorded as a new ADR.

---

**Related:** [ROADMAP.md](ROADMAP.md) · [PRIVACY.md](PRIVACY.md) · [GLOSSARY.md](GLOSSARY.md) · [docs/architecture.md](docs/architecture.md) · [docs/decisions/](docs/decisions/)
