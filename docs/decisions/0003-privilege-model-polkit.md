# ADR-0003: Unprivileged services with polkit for privileged actions

- Status: accepted
- Date: 2026-06-12

## Context

Some future Sage actions (package installation, service restarts, system config
changes) require elevation. An AI-driven system must make elevation rare,
explicit, and auditable. Options considered: run components as root, sudo
invocations, a setuid helper, polkit.

## Decision

**Daemon and McpServer always run unprivileged** as systemd user services.
Privileged actions (later milestone) go through **polkit**: a small system
helper exposes specific, declared actions with polkit policy files, and the
desktop's polkit agent prompts the user for authentication. Phase 1 ships
read-only tools only, so no elevation path exists yet.

## Consequences

- A compromised or confused model can at worst run allowlisted read-only
  commands (see the safety layer in docs/security.md); there is no general
  "run as root" capability anywhere in the system.
- Each privileged action is individually declared (its own polkit action id),
  individually authorized, and audit-logged — matching the per-source
  permission philosophy.
- The standard desktop authentication dialog provides the user prompt; we never
  invoke sudo at runtime, and per hard rule never require sudo without asking.
- Cost: privileged features need a separate helper component plus policy files,
  which delays them relative to a sudo shortcut. Accepted deliberately.
- Distribution packaging must install polkit policy files when that milestone
  arrives.
