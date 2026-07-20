# Privacy (working reference)

> **Purpose.** This document captures Veya's **privacy-by-design** approach — what
> data the assistant can touch, what (if anything) leaves your machine, and the
> controls that enforce those boundaries. It is the product-level companion to the
> technical [security & privacy model](docs/security.md) and expands the privacy
> pillar named in the [charter](CHARTER.md).
>
> **This is not legal advice, and it is forward-looking.** Veya is **pre-alpha**
> desktop software with no hosted service and no telemetry. Sections about hosted
> deployments, operators, or regulatory obligations describe what *would* apply
> **only if** Veya were ever offered as a service — they are not in scope today and
> would need review by a qualified privacy lawyer before any such launch.

## The core principle: local-first, on-device by default

Veya runs entirely as **unprivileged user services on your own machine**. There is
no Veya account, no Veya server, no analytics, and no phone-home. By default,
nothing you do with Veya leaves your computer. The only path off the machine is a
**cloud inference call**, and only when the router falls back to a cloud model —
which is always surfaced to you (see [What leaves your machine](#what-leaves-your-machine)).

Privacy is enforced by three mechanisms, each described in
[docs/security.md](docs/security.md) and summarized below:

1. **Per-source permissions** — default-deny, per context source, checked at both
   ingestion and query time.
2. **The safety layer** — every command runs through one allowlisted, argv-only,
   timed, output-capped, audited gateway.
3. **The audit log** — an append-only, on-device record of every access, carrying
   metadata only, never captured content.

## Roles & scope

| Role | Who, in Veya |
|---|---|
| **Data subject & controller** | **You**, the person running Veya on your own machine. You decide what Veya may read and when it may use the cloud. |
| **The software** | Veya processes your data locally, on your behalf, under your grants. It stores nothing off-device. |
| **Cloud inference provider** | Anthropic (Claude) or Mistral ("La Plateforme"), invoked **only** on cloud fallback — a sub-processor of *your* request for that call. See [Sub-processors](#cloud-sub-processors-when-cloud-is-used). |

There is deliberately **no operator/processor relationship** in the current model,
because there is no hosted instance — you host yourself. A future hosted or shared
deployment would introduce those roles and the corresponding obligations; that is
out of scope (see [Out of scope](#out-of-scope--forward-looking)).

## What data Veya can touch

Every source below is an independent, **default-deny** permission
([ADR-0005](docs/decisions/0005-per-source-permissions.md)). Veya reads a source
only when you have granted it, and both ingestion and query re-check the grant.

| Source | What it exposes | Gate | Notes |
|---|---|---|---|
| **System info** | processes, memory/disk, journald, APT, systemd status | safety-layer allowlist | Read-only; not personal-context sources, but still audited (`tool.exec`). |
| **Clipboard** | current clipboard contents (read), and clipboard writes | per-source permission | First non-read-only tools; audit-logged ([ADR-0006](docs/decisions/0006-clipboard-write-mechanism.md)). |
| **Files** | user-approved files/paths, indexed for retrieval | `PersonalIndex` permission | Only approved sources are ingested ([ADR-0010](docs/decisions/0010-file-context-source.md)). |
| **Personal context index** | embeddings over approved content (SQLite + sqlite-vec) | `PersonalIndex` permission | Checked at ingest *and* query ([ADR-0009](docs/decisions/0009-personal-context-index.md)). |
| **Notifications** | desktop notifications, captured into a recent store | `Notifications` permission | For summaries/digests ([ADR-0011](docs/decisions/0011-notification-intelligence.md), [ADR-0012](docs/decisions/0012-session-bus-notification-source.md)). |
| **Screen** | on-demand OCR of a screenshot (`read_screen_text`) | `Screen` permission **+** per-call XDG portal prompt | Screenshot is OCR'd then **immediately deleted**; nothing persisted ([ADR-0013](docs/decisions/0013-screen-awareness.md)). |
| **Microphone** | a bounded voice recording, transcribed locally | `Microphone` permission | Recording transcribed (local Whisper) then **immediately deleted** ([ADR-0015](docs/decisions/0015-voice-io.md)). |

**Ephemeral by design.** Screen and voice captures are processed and discarded in
the same operation — the image/audio is never written to disk, persisted, or
indexed. Speaking a reply aloud is not a separate data source (it's Veya's own
answer, the audio equivalent of showing it in the overlay), so it isn't separately
gated.

## What leaves your machine

The **only** egress is a cloud inference call, and only when the model router falls
back to a cloud backend. When that happens:

- The request goes to **Anthropic (Claude)** or **Mistral**, whichever is
  configured ([ADR-0008](docs/decisions/0008-mistral-cloud-backend.md)); the
  local-first router (`FallbackInferenceBackend`) prefers a local model first.
- A **`cloud.request`** audit event is written **and** a user-visible D-Bus
  `CloudUsage` signal fires — the UI must make "this left your machine" impossible
  to miss.
- The audit event records **backend, model, input/output token counts, and
  duration — never the prompt or response content** unless you explicitly opt in.

A **local** inference call (`OllamaBackend`, [ADR-0004](docs/decisions/0004-local-backend-ollama.md))
writes a `local.request` event of the same shape but **does not** trigger
`CloudUsage`, because nothing left the machine.

**What is sent to the cloud** on a fallback call is the prompt the model needs to
answer — which may include context you granted (e.g. a retrieved file chunk or a
notification summary). This is the point at which granted local context can become
cloud-visible, so the router's local-first preference and the visible cloud signal
exist precisely to keep that choice in your hands.

## The audit log: what is recorded (and what never is)

Append-only JSON Lines under `~/.local/state/veya/audit/`
(`$XDG_STATE_HOME/veya/audit/`), rotated by size, readable by you. Every event
carries a timestamp and **never any captured content** unless noted:

| Event | Carries | Never carries |
|---|---|---|
| `tool.exec` | binary, argv, exit code, duration, allowed/refused, truncation | — |
| `cloud.request` | backend, model, token counts, duration | prompt / response content |
| `local.request` | same shape as `cloud.request` | prompt / response content |
| `context.ingest` | source, requester, chunk count, duration | the indexed text |
| `context.query` | requester, sources searched, match count, duration | the query or chunk text |
| `notification.capture` | count, duration | app names, summaries, bodies |
| `notification.query` | returned count, duration | notification text |
| `screen.capture` | success, extracted-text length, duration | the screenshot or the text |
| `voice.capture` | success, transcript length, duration | the audio or the transcript |
| `voice.speak` | success, spoken-text length, duration | the spoken text |
| `permission.decision` | source, requester, granted/denied | — |

The `CloudUsage` and `ToolExecuted` D-Bus signals mirror this log live for
frontends (see [docs/dbus-interfaces.md](docs/dbus-interfaces.md)).

## Storage & retention

| Data | Where | Lifetime |
|---|---|---|
| Audit log | `~/.local/state/veya/audit/` | append-only, rotated by size; you may delete it |
| Personal context index | local SQLite (sqlite-vec) | until you remove the source or delete the store |
| Notification store | in-memory / local recent store | recent window only |
| Screen / voice captures | not stored | discarded immediately after processing |
| API keys | libsecret/keyring (never in repo config) | until you remove them |

**Upgrading from a pre-rename (Sage) install:** the state directory moved from
`~/.local/state/sage/` to `~/.local/state/veya/`
([ADR-0007](docs/decisions/0007-project-name-veya.md)); there is no automatic
migration. Move it by hand to keep history:
`mv ~/.local/state/sage ~/.local/state/veya`.

## Your controls

- **Grant / deny / revoke** any context source independently (default-deny; for
  Milestone 2 grants are config-based, interactive grant UX is a later milestone —
  see [ROADMAP.md](ROADMAP.md) M5).
- **Inspect** exactly what Veya did — the audit log is plain JSON Lines you can read.
- **See** every cloud call as it happens via the `CloudUsage` signal.
- **Delete** the audit log, the context index, and stored API keys at any time;
  they are ordinary files/keyring entries you own.
- **Stay fully local** — configure a local backend and Veya answers without any
  cloud egress.

## Cloud sub-processors (when cloud is used)

Only relevant on a cloud-fallback inference call. You choose the provider and
supply the key.

| Provider (you configure one) | Purpose | Data sent |
|---|---|---|
| Anthropic (Claude API) | cloud inference fallback | the prompt for that request (may include granted context) |
| Mistral ("La Plateforme") | cloud inference fallback (default per [ADR-0008](docs/decisions/0008-mistral-cloud-backend.md)) | the prompt for that request |

Each provider's own data-handling terms apply to what you send them. Veya sends
nothing to any provider you have not configured, and nothing at all when a local
backend handles the request.

## Secrets handling

API keys are stored via **libsecret/keyring**, never in repository config. In
development they may come from `dotnet user-secrets` or the `MISTRAL_API_KEY` /
`ANTHROPIC_API_KEY` environment variables ([ADR-0008](docs/decisions/0008-mistral-cloud-backend.md)).
Keys must never be logged, persisted to the audit log, or committed — a Claude Code
guardrail blocks credential-file access, and leakage of a key is an in-scope
security issue (see [SECURITY.md](SECURITY.md)).

## Threat notes

The technical threat model (prompt injection via tool output, untrusted D-Bus
callers, dependency surface) lives in
[docs/security.md § Threat notes](docs/security.md). The privacy-relevant
takeaway: all tool output is treated as untrusted data, and the safety-layer
allowlist means even a manipulated model can only run read-only, capped commands.

## Out of scope / forward-looking

These do **not** apply to the current local-only, pre-alpha software, and are
recorded here so the boundary is explicit:

- **Telemetry / analytics** — none, now or planned, without an explicit,
  documented, opt-in decision (which would be a new ADR).
- **A hosted or shared multi-user deployment** — would introduce operator/processor
  roles and GDPR/RODO obligations (DPA, records of processing, breach notification,
  data-subject-rights tooling). None of that exists today; revisit only if a hosted
  offering is ever proposed.
- **Cloud provider data retention** — governed by the provider you configure, not
  by Veya.

---

**Related:** [docs/security.md](docs/security.md) (privilege model, safety layer, audit events) · [CHARTER.md](CHARTER.md) · [SECURITY.md](SECURITY.md) · [GLOSSARY.md](GLOSSARY.md)
