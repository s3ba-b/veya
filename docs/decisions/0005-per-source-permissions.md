# ADR-0005: Per-source permission model

- Status: accepted
- Date: 2026-06-14

## Context

Milestone 2 (docs/roadmap.md) introduces the first non-read-only tools
(clipboard writing). docs/security.md states a product pillar: every context
source — clipboard, files, notifications, screen, personal index — has an
independent permission the user grants per source, defaults are deny, and
decisions are audit-logged. Until now every tool was read-only and required no
such gate, so no permission machinery existed.

Before building the clipboard tool we need the gate it rides on, plus two
decisions:

1. **Where the grant lives in this milestone.** Options ranged from a config-
   based default-deny store, to runtime `Grant`/`Revoke` D-Bus methods, to an
   interactive per-action consent prompt.
2. **Who audits the decision.** Either each tool logs its own
   `permission.decision`, or a central gate does it for all callers.

## Decision

A small permission layer in `Veya.Shared.Permissions`:

- **`PermissionSource`** — an enum of the sources named in docs/security.md
  (`Clipboard`, `Files`, `Notifications`, `Screen`, `PersonalIndex`).
- **`IPermissionStore`** — a pure, side-effect-free lookup
  (`bool IsGranted(PermissionSource)`), **default-deny**: any source not
  explicitly granted is denied. `PermissionStore` holds an immutable grant map.
- **`IPermissionGate`** — the single checkpoint every gated action passes
  through (`Task<bool> CheckAsync(source, requester, ct)`). `PermissionGate`
  consults the store and writes a `permission.decision` audit event for **every**
  check, granted or denied.

**Grant mechanism for Milestone 2: config-based, default-deny.** The grant map
is bound from configuration by the host (e.g. a `Permissions` section read in
McpServer `Program.cs`). Nothing is granted unless the user opts in there.

Reasons:

- **Central auditing.** Folding the `permission.decision` write into the gate
  means "decisions are audit-logged" is enforced once, not re-implemented (and
  forgotten) per tool — the same reason all shell execution goes through one
  `ISafeExecutor`.
- **Default-deny by construction.** A missing entry denies; you cannot forget to
  deny a source you didn't think about.
- **Config-based grant fits the current surface.** There is no interactive UI to
  drive consent yet (the overlay is a minimal `Ask` window), and adding
  `Grant`/`Revoke` D-Bus methods now would expand the `org.veya.Veya1` contract
  before anything can call them. Config is headless-testable (hard rule 3) and
  enough to ship the first write tool safely.
- **Framework-agnostic core.** `Veya.Shared` deliberately avoids a configuration
  dependency, so the store takes a plain grant map; the configuration binding
  lives in the host project that already has `IConfiguration`.

## Consequences

- The clipboard tool (and every future gated tool) takes an `IPermissionGate`
  and calls `CheckAsync` before acting; a denied check means the tool refuses
  and the model is told it lacks permission.
- New audit event type `permission.decision` (fields: `source`, `requester`,
  `granted`) — already named in docs/security.md, now emitted by the gate.
- Tests fake `IPermissionStore` / `IPermissionGate` and a recording audit log;
  no desktop session or real grant UI is needed.
- **Deferred** (later milestone, with the overlay/UI): runtime grant/revoke
  (likely D-Bus on `org.veya.Veya1`), interactive per-action consent, and
  per-app scoping. This ADR does not preclude them — the config-bound map is the
  initial source of truth and can later be backed by a mutable, user-driven
  store behind the same `IPermissionStore`.
