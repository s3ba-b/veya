# ADR-0007: Project name — Veya (renamed from Sage)

- Status: accepted
- Date: 2026-06-14

## Context

The project shipped under the working name **Sage**, surfaced everywhere as the
`Sage.*` project/namespace prefix, the D-Bus contract `org.sage.Sage1` (object
path `/org/sage/Sage1`), and runtime paths such as `~/.local/state/sage/`. The
name was never recorded in an ADR; it was asserted in CLAUDE.md and grew through
the codebase.

A naming review found "Sage" is unviable for a public open-source launch:

- **Direct, on-the-nose collision.** Gen Digital's open-source **Sage** (March
  2026) is "a security layer between AI agents and the OS" with a local-first
  privacy model — almost exactly this project's safety-layer pillar
  (docs/security.md), same OS, same space.
- **Active trademark + competing AI product.** The Sage Group plc holds
  registered "SAGE" software marks and is actively shipping "Sage AI" / "Sage
  Copilot" assistants — the same product category.
- **Saturated namespace.** SageMath, Storia-AI/sage, sage.is and others already
  occupy the OSS/AI "Sage" space; GitHub org, domain, and search would all be
  uphill.

A coined-word search was run to find a name that clears three hard filters at
once: no AI-product collision, a free GitHub org, and an apparently-open
software/SaaS trademark class. Of 18 candidates screened (dictionary,
Latin/myth, and coined), only **Veya** cleared all three.

## Decision

**Rename the project to Veya.** The component names stay exactly as they are
(Daemon, McpServer, Shared, Overlay); only the brand prefix and D-Bus identity
change.

- Project/namespace prefix: `Sage.` → `Veya.` (e.g. `Veya.Daemon`,
  `Veya.Shared`).
- D-Bus name and interface: `org.sage.Sage1` → `org.veya.Veya1`.
- Object path: `/org/sage/Sage1` → `/org/veya/Veya1`.
- Runtime paths: `~/.local/state/sage/`, `~/.local/lib/sage`, and the
  `sage-daemon.service` unit → `veya` / `veya-daemon.service` equivalents.

This supersedes the implicit "Sage" naming in CLAUDE.md and updates every ADR and
doc that cites the old D-Bus contract (notably ADR-0005, docs/architecture.md,
docs/dbus-interfaces.md, docs/running.md, docs/security.md).

Reasons Veya wins:

- **No AI-assistant collision** in the screen (only the differently-spelled,
  unrelated "Veyra").
- **GitHub org is free** (`github.com/veya` returns 404).
- **Software trademark class appears open** — existing "VEYA" marks sit in lab
  robotics, retail, and merch (different Nice classes), not software/AI.

## Consequences

- **Breaking D-Bus contract change.** `org.veya.Veya1` is a new, incompatible
  bus name/path/interface. Any frontend (including the Overlay) must move in
  lockstep; there are no external consumers yet, so this is the cheapest moment
  to do it. Per CLAUDE.md the D-Bus name is fixed everywhere, so the rename is
  all-or-nothing.
- **On-disk migration.** Audit logs live under `~/.local/state/sage/audit/`
  (docs/security.md, `AuditPaths`). Renaming the directory orphans existing
  trails. Decision: **no automatic migration** — the one-time manual move
  (`mv ~/.local/state/sage ~/.local/state/veya`) is documented in
  docs/security.md. Acceptable because no non-developer installs exist yet.
- **Wide but mechanical edit.** ~110 files reference "sage" (namespaces, file
  names, csproj/sln, the `Sage1Service`/`ISage1`/`SageDBus` types, systemd unit,
  docs). It is a rename, not a redesign; see the rollout list below.
- **Pre-launch diligence still required.** Confirm `veya.io`/`veya.dev`/`.app`
  via WHOIS (the `.com`/`.co` are taken by unrelated firms) and obtain formal
  trademark clearance in Class 9/42 before public announcement.
- **Process.** Per hard rule 4 (one issue per branch), this lands on its own
  issue/branch (e.g. `NN-rename-veya`), not folded into a feature change.

## Rollout (identifier inventory)

| What | From → To |
|------|-----------|
| Namespace / project prefix | `Sage.*` → `Veya.*` |
| Solution / project files | `Sage.sln`, `src/Sage.*/Sage.*.csproj`, `tests/Sage.*.Tests/*` → `Veya.*` |
| Directory names | `src/Sage.Daemon` … `tests/Sage.*.Tests` → `Veya.*` |
| Public types | `ISage1`, `Sage1`, `Sage1Service`, `ISage1Client`, `Sage1Client`, `SageDBus` (+ their `*Tests`) → `Veya1`/`VeyaDBus` forms |
| D-Bus name/iface | `org.sage.Sage1` → `org.veya.Veya1` (`SageDBus.cs`, `Sage1Service.cs`, `DBusSessionConnector.cs`, `Sage1Client.cs`, tests) |
| D-Bus object path | `/org/sage/Sage1` → `/org/veya/Veya1` (`SageDBus.ObjectPath`, docs) |
| systemd unit | `packaging/systemd/sage-daemon.service` → `veya-daemon.service`; `ExecStart` `%h/.local/lib/veya/Veya.Daemon` |
| Runtime paths | `~/.local/state/sage/` (`AuditPaths`), `~/.local/lib/sage` (publish dir) → `veya` |
| Docs | CLAUDE.md, README.md, docs/architecture.md, docs/dbus-interfaces.md, docs/running.md, docs/security.md, docs/roadmap.md |
| ADR cross-refs | ADR-0005 `org.sage.Sage1` mentions (and any later ADR) |
| CI / templates | `.github/workflows/ci.yml`, PR/issue templates |
