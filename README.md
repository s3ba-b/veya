# Sage

**An open-source, privacy-transparent, system-wide AI assistant for Ubuntu/Linux.**

> Status: **pre-alpha** — design and scaffolding phase. Nothing here is usable yet.

Sage is an Apple Intelligence alternative for the Linux desktop. The core idea: a
central **intelligence daemon** that any UI can talk to over **D-Bus**, with an
**MCP server** as the system-action layer and **hybrid local/cloud inference**.

## Why

Desktop AI assistants today are either cloud silos bolted onto a single app, or
opaque about what leaves your machine. Sage is built around three commitments:

- **Local-first privacy.** Local models are first-class. Every source of context
  (clipboard, files, notifications, screen) is gated by per-source permissions.
- **Transparency.** An audit log records every tool invocation and every byte sent
  to a cloud backend. Cloud usage is always user-visible.
- **System-wide, not app-bound.** One daemon, one D-Bus contract
  (`org.sage.Sage1`), any number of frontends — overlay window, shell extension,
  CLI, your own client.

## Architecture at a glance

```
Frontends (Overlay UI, GNOME Shell, CLI)
        │  D-Bus (org.sage.Sage1)
        ▼
Daemon  — sessions, context, model router (Claude API now, local later)
        │  MCP (stdio)
        ▼
McpServer — read-only Ubuntu system tools, behind a central safety layer
```

Components live as separate projects in one .NET 9 solution: **Daemon**,
**McpServer**, **Shared**, and (later) **Overlay**. See
[docs/architecture.md](docs/architecture.md) for responsibilities, the full
diagram, and an end-to-end query walkthrough. Key decisions are recorded as ADRs
in [docs/decisions/](docs/decisions/).

## Security model

Daemon and MCP server run **unprivileged** as a user service. All shell execution
goes through a central safety layer (allowlist, timeouts, output caps, audit log).
Privileged actions will go through polkit later. Details in
[docs/security.md](docs/security.md).

## Roadmap

See [docs/roadmap.md](docs/roadmap.md). Milestone 1 is an end-to-end MVP: daemon
skeleton, D-Bus `Ask` stub, MCP server with read-only system tools, Claude API
backend, and a minimal overlay window.

## Building

```sh
./scripts/setup-dev.sh   # one-time: Ubuntu dev dependencies
./scripts/verify.sh      # canonical build + test + format check
```

To run the daemon, including as a systemd user service, see
[docs/running.md](docs/running.md).

## Contributing

One issue per branch; PRs run `./scripts/verify.sh` in CI. Read
[CLAUDE.md](CLAUDE.md) for project conventions (they apply to humans too).

## License

TBD (will be an OSI-approved license before the first release).
