# Roadmap

Veya is delivered **iteratively-incrementally**: each milestone is a working,
demoable slice of the system — daemon → D-Bus → MCP tools → inference → frontend —
rather than a horizontal layer built in isolation. Milestones are defined by
*working software*, not calendar dates, and are ordered by dependency.

- **Walking skeleton first** (M1). The architectural spine — Daemon, the
  `org.veya.Veya1` D-Bus contract, the MCP server, the central safety layer, one
  inference backend, and a minimal overlay — is built end-to-end before breadth, so
  every later feature hangs off the same spine instead of being retrofitted.
- **Vertical slices** (M2–M4). Each capability is built end-to-end as a
  self-contained increment: local models + first writes, personal context +
  notifications, then voice + screen + GNOME polish.
- **Privacy as a continuous gate.** Local-first privacy is a product pillar, not a
  late add-on: every new context source ships with its per-source permission gate
  and audit-log coverage *in the same increment*. A source without a gate is not
  "done". See [PRIVACY.md](PRIVACY.md) and [docs/security.md](docs/security.md).

## Milestones

Status: **pre-alpha. M1–M4 are implemented.** Completed milestones keep their step
lists as a record of what shipped.

| Milestone | Definition of done |
|---|---|
| **M1 — MVP: end-to-end answer on screen** *(Done)* | A question typed into a minimal overlay window is answered by a cloud model using real system data from MCP tools. The walking skeleton — Daemon (Generic Host + systemd), the `org.veya.Veya1` D-Bus contract (Tmds.DBus), the MCP server (stdio), the central **safety layer** before any shelling-out tool, read-only system tools, an `IInferenceBackend` cloud backend with a tool-use loop, and a GTK4/libadwaita overlay — is wired end-to-end and green through `./scripts/verify.sh` in CI. |
| **M2 — Local models + first write actions** *(Done)* | A local backend (Ollama, [ADR-0004](docs/decisions/0004-local-backend-ollama.md)) behind `IInferenceBackend`, with a local-first router policy and user-visible cloud usage; the cloud backend is config-selectable — Mistral (default) or Claude ([ADR-0008](docs/decisions/0008-mistral-cloud-backend.md)) behind `FallbackInferenceBackend`. Clipboard writing ([ADR-0006](docs/decisions/0006-clipboard-write-mechanism.md)) lands as the first non-read-only tools, gated by per-source permissions ([ADR-0005](docs/decisions/0005-per-source-permissions.md)) and fully audit-logged. |
| **M3 — Personal context + notification intelligence** *(Done)* | A personal context index (SQLite + sqlite-vec) over user-approved sources, with per-source permissions enforced at ingestion *and* query ([ADR-0009](docs/decisions/0009-personal-context-index.md), [ADR-0010](docs/decisions/0010-file-context-source.md)); and notification intelligence — capture, store, summarize, prioritize, and answer questions about desktop notifications ([ADR-0011](docs/decisions/0011-notification-intelligence.md), [ADR-0012](docs/decisions/0012-session-bus-notification-source.md)), folded into `Ask`. Every new source is permission-gated and audit-covered. |
| **M4 — Voice, screen awareness, GNOME polish** *(Done)* | Voice I/O — local Whisper STT + espeak-ng TTS via `AskVoice` ([ADR-0015](docs/decisions/0015-voice-io.md)), with a GNOME Shell extension mic button ([ADR-0014](docs/decisions/0014-gnome-shell-extension.md)) — plus screen awareness (`read_screen_text` via XDG portal + tesseract OCR, [ADR-0013](docs/decisions/0013-screen-awareness.md)), and the GNOME Shell extension shim (keyboard summon + panel UI). Screen and voice captures are ephemeral (OCR'd/transcribed then discarded), permission-gated, and audit-covered. |
| **M5 — Privileged actions & runtime permission UX** *(Planned)* | The first **privileged** system actions land through a small **polkit-authorized helper** ([ADR-0003](docs/decisions/0003-privilege-model-polkit.md)) — each a specific, declared action authorized after explicit user authentication; no general "run as root" path is introduced, and the daemon/MCP server stay unprivileged. In parallel, per-source permissions move from config-based default-deny to an **interactive runtime grant/revoke UX** (grant on first use, revoke any time), so a user can see and change what Veya may access without editing config. Every privileged action and every grant/revoke is audit-logged; a bypass of either the polkit boundary or a permission gate is a release blocker. |
| **M6 — Local-model parity & offline** *(Planned)* | A capable local path — an in-process backend (LLamaSharp) and/or a tuned Ollama router policy behind `IInferenceBackend`, plus local embeddings for the personal index — that answers the large majority of everyday queries **with zero cloud egress**. The router policy is documented and demoable: with no API key configured, the assistant still answers typed and voice questions and uses the personal-context, notification, and screen tools end-to-end. Cloud remains an explicit, user-visible fallback, never a silent default. |
| **M7 — Hardening & first release** *(Planned)* | Veya is installable and runnable by someone who isn't the author: a distributable package (`.deb` and/or the systemd user unit in [packaging/](packaging/)) with documented install/uninstall, a security review of the safety layer + permission gates + audit coverage, meaningful test coverage on the core logic (composition roots and display/D-Bus bindings excepted per hard rule #3), and the **first tagged release**. The overlay and shell extension are polished enough for daily use. |

Each milestone above also exists as a **[GitHub milestone](https://github.com/s3ba-b/veya/milestones)**
with this same definition of done.

## Versioning: MVP and V1

"MVP" and "V1" are kept distinct in their standard software-engineering sense —
they are not synonyms, and neither is a rename of a milestone:

- **MVP (minimum viable product)** — the earliest point at which Veya does something
  genuinely useful end-to-end: a typed question is answered on screen using real
  system data (reached at **M1**, broadened by M2–M4 into local inference, personal
  context, notifications, voice, and screen). In this solo, non-commercial project
  the MVP is a *demoable increment used to validate the architecture and UX* — it is
  **not a public release**, because the release-readiness work (M7) is deliberately
  deferred.
- **V1 (first release)** — the first version actually fit for someone other than the
  author to install and run daily. V1 is the **M1–M7 arc** plus the release gate that
  lives in **M7**: a documented install, a security review, the privileged-action and
  runtime-permission UX from M5, the offline path from M6, and the first tagged
  release. Until those land, Veya is demoable but not releasable.

Delivery focus is **V1**. Earlier milestones are demoable increments on the way to
it, not separately shipped releases.

## Possible further directions

Candidates, not commitments, and not yet broken into milestones with a definition
of done. Revisit once M7 lands.

- **More frontends** — a CLI client and/or a tray applet against the same
  `org.veya.Veya1` contract; a documented client SDK so third parties can build UIs.
- **Broader write tools** — beyond clipboard: files, window/session actions, app
  launching — each permission-gated and, where privileged, behind the M5 polkit
  helper.
- **Richer context sources** — calendar, email, browser tabs — each default-deny and
  audit-covered, following the same gate pattern.
- **Non-GNOME desktop support** — KDE/others, currently untested but not deliberately
  broken.
- **Model/agent improvements** — multi-step tool planning, on-device model
  management, per-task routing policies.

## How to start the next milestone

Only the **current** milestone has issues filed against it — the backlog is
deliberately not pre-populated end to end. When the current milestone's issues are
all closed:

1. Take the next milestone's row from the table above.
2. Break it into a small set of issues, each deliverable as a single
   issue → branch → PR → merge unit (see [CONTRIBUTING.md](CONTRIBUTING.md)). Order
   them by dependency — foundation/safety before features.
3. Every new context source or privileged action needs its permission gate (or
   polkit authorization) **and** audit-log coverage in the same increment
   (non-negotiable — see [CLAUDE.md](CLAUDE.md) and [PRIVACY.md](PRIVACY.md)).
4. Record any decision as an ADR under [docs/decisions/](docs/decisions/) before
   building on it.
5. File the issues against the milestone in GitHub before starting work on it.

## Non-goals (for now)

- **Non-Linux platforms.** Non-GNOME desktops are untested but not deliberately
  broken.
- **Privileged write actions before polkit.** No action requiring elevation ships
  before the polkit integration ([ADR-0003](docs/decisions/0003-privilege-model-polkit.md)) lands in M5.
- **A hosted / multi-user service.** Veya is local-only; a hosted deployment (and its
  GDPR/operator obligations) is out of scope — see [PRIVACY.md](PRIVACY.md).
- **Telemetry / analytics.** None, unless ever introduced by an explicit opt-in
  decision recorded as a new ADR.
