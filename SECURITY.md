# Security Policy

Veya is a system-wide assistant that can read system context and (later) perform
privileged actions, so security reports are taken seriously. Thank you for taking
the time to report responsibly.

## Status

Veya is **pre-alpha** — design and scaffolding. There is no released version and
no production deployment to protect yet, but the architecture is being built
security-first (see [docs/security.md](docs/security.md) for the privilege model,
the central shell-execution safety layer, per-source permissions, and audit log).

## Supported versions

There are no releases yet. Until the first tagged release, only the `main` branch
is in scope; fixes land there.

## Reporting a vulnerability

**Please do not open a public issue for security vulnerabilities.**

Prefer **GitHub private vulnerability reporting**: on this repository, go to the
**Security** tab → **Report a vulnerability**. This keeps the report private until
a fix is available.

If you cannot use that, email **seb.bobrowski@proton.me** with:

- a description of the issue and its impact,
- steps to reproduce (proof-of-concept if you have one),
- affected component (Daemon, McpServer, Shared, Overlay) and commit/branch,
- any suggested remediation.

You can expect an acknowledgement within **7 days**. Because this is a
single-maintainer pre-alpha project, fix timelines are best-effort; we will keep
you updated on progress and coordinate disclosure timing with you.

## Scope

Especially relevant given Veya's design:

- bypasses of the central shell-execution safety layer (allowlist, timeouts,
  output caps) — see [docs/security.md](docs/security.md),
- bypasses of per-source permission gates or gaps in audit-log coverage for a
  context source,
- unintended privilege escalation, or any path that runs code as root,
- leakage of context to a cloud backend without the usage being user-visible,
- secrets (e.g. the Anthropic API key) being logged, persisted, or exposed.

## Out of scope

- Issues requiring an already-compromised local account with the user's full
  privileges.
- Vulnerabilities in third-party dependencies — please report those upstream
  (we run a dependency license scan but do not own those codebases).
- Findings from automated scanners without a demonstrated, realistic impact.

## Disclosure

We follow coordinated disclosure: we will work with you on a fix and credit you
(if you wish) once a remediation is available.
