# Veya

**An open-source, privacy-transparent, system-wide AI assistant for Ubuntu/Linux.**

> Status: **pre-alpha** — Milestones 1–4 are implemented (daemon, MCP tools,
> local + cloud inference, clipboard writes, personal context, notification
> intelligence, screen awareness, voice I/O, GNOME shell extension). Expect
> rough edges.

Veya is an Apple Intelligence alternative for the Linux desktop. The core idea: a
central **intelligence daemon** that any UI can talk to over **D-Bus**, with an
**MCP server** as the system-action layer and **hybrid local/cloud inference**.

## Why

Desktop AI assistants today are either cloud silos bolted onto a single app, or
opaque about what leaves your machine. Veya is built around three commitments:

- **Local-first privacy.** Local models are first-class. Every source of context
  (clipboard, files, notifications, screen) is gated by per-source permissions.
- **Transparency.** An audit log records every tool invocation and every cloud
  request (backend, model, token counts — never the content). Cloud usage is
  always user-visible.
- **System-wide, not app-bound.** One daemon, one D-Bus contract
  (`org.veya.Veya1`), any number of frontends — overlay window, shell extension,
  CLI, your own client.

## Architecture at a glance

```
Frontends (Overlay UI, GNOME Shell, CLI)
        │  D-Bus (org.veya.Veya1)
        ▼
Daemon  — sessions, context, model router (local Ollama first, Claude/Mistral cloud fallback)
        │  MCP (stdio)
        ▼
McpServer — Ubuntu system tools (read-only + permission-gated writes), behind a central safety layer
```

Components live as separate projects in one .NET 10 solution: **Daemon**,
**McpServer**, **Shared**, and **Overlay**, plus a GNOME Shell extension (GJS,
not part of the .NET solution). See
[docs/architecture.md](docs/architecture.md) for responsibilities, the full
diagram, and an end-to-end query walkthrough. Key decisions are recorded as ADRs
in [docs/decisions/](docs/decisions/).

## Security model

Daemon and MCP server run **unprivileged** as a user service. All shell execution
goes through a central safety layer (allowlist, timeouts, output caps, audit log).
Privileged actions will go through polkit later. Details in
[docs/security.md](docs/security.md).

## Roadmap

See [ROADMAP.md](ROADMAP.md) (the milestone source of truth, mirrored as
[GitHub milestones](https://github.com/s3ba-b/veya/milestones)). Milestones 1–4
are implemented — daemon, D-Bus `Ask`, MCP read-only tools, local + cloud
inference, clipboard writes, personal context index, notification intelligence,
screen awareness, voice I/O, and the GNOME shell extension. M5–M7 (privileged
actions via polkit + runtime permission UX, local-model parity/offline, and
hardening + first release) are planned.

## Project docs

- [CHARTER.md](CHARTER.md) — objectives, scope, stakeholders, risks, licensing.
- [ROADMAP.md](ROADMAP.md) — milestones and their definitions of done.
- [PRIVACY.md](PRIVACY.md) — the local-first data model: what leaves your machine,
  and the permission/audit controls that enforce it.
- [GLOSSARY.md](GLOSSARY.md) — shared vocabulary (daemon, MCP, safety layer, …).
- [docs/architecture.md](docs/architecture.md) · [docs/security.md](docs/security.md) · [docs/decisions/](docs/decisions/) (ADRs).

## Website

The project website ([veya-project.org](https://veya-project.org)) lives in
[site/](site/) and deploys via
[.github/workflows/deploy-site.yml](.github/workflows/deploy-site.yml).

## Building

```sh
./scripts/setup-dev.sh   # one-time: Ubuntu dev dependencies
./scripts/verify.sh      # canonical build + test + format check
```

To run the daemon, including as a systemd user service, see
[docs/running.md](docs/running.md).

## Built with Claude Code

Veya was developed with [Claude Code](https://claude.com/claude-code), Anthropic's
agentic coding tool, used to design and build a broad, high-quality system
quickly — the daemon, MCP server, D-Bus contract, safety/audit layer, ADRs, and
docs. Every significant decision is recorded as an ADR in
[docs/decisions/](docs/decisions/) and every change ships through
`./scripts/verify.sh` (build, tests, format) in CI, so the result stays
reviewable regardless of how it was produced.

## Contributing

One issue per branch; PRs run `./scripts/verify.sh` in CI. Read
[CLAUDE.md](CLAUDE.md) for project conventions (they apply to humans too).

## License

Licensed under the [GNU Affero General Public License v3.0 or later](LICENSE).
See the [NOTICE](NOTICE) file for attribution. Contributions are accepted under
the same license and require a [Developer Certificate of Origin](DCO.md) sign-off
(`git commit -s`) — see [CONTRIBUTING.md](CONTRIBUTING.md).
