# Contributing to Veya

Thanks for your interest in Veya — an open-source, privacy-transparent,
system-wide AI assistant for Ubuntu/Linux. This project is **pre-alpha**, so
expect churn. The conventions below apply to everyone, human and AI agents alike;
[CLAUDE.md](CLAUDE.md) is the canonical, more detailed version.

## Ground rules

- **One issue per branch.** Branch names are `<issue-number>-short-slug`
  (e.g. `47-apache-license`). Open or find an issue first.
- **Decisions live in ADRs.** Architectural decisions are recorded in
  [docs/decisions/](docs/decisions/). Don't relitigate a decision in a PR — if a
  decision should change, write a new ADR that supersedes the old one and update
  [docs/architecture.md](docs/architecture.md) and CLAUDE.md to match.
- **Keep docs of record consistent:** architecture.md (components/data flow),
  dbus-interfaces.md (D-Bus contract), security.md (privilege model, safety
  layer), roadmap.md (milestones).

## Development setup

```sh
./scripts/setup-dev.sh   # one-time: Ubuntu dev dependencies (uses sudo for apt)
```

See [docs/running.md](docs/running.md) to run the daemon, including as a systemd
user service.

Prefer not to install locally? Open this repo in **GitHub Codespaces** (or any
devcontainer-compatible editor) — `.devcontainer/devcontainer.json` gives you
a .NET 10 environment that mirrors CI and can build/test Daemon, McpServer, and
Shared. It's headless, so it can't run the GNOME extension, Overlay UI, or
anything needing a display server or D-Bus session bus.

## Before you open a PR

Run the single canonical check — CI runs exactly this script, so if it passes
locally it should pass in CI:

```sh
./scripts/verify.sh
```

It runs, in order: dependency license scan, `dotnet format` (verify-only),
`dotnet build` (warnings as errors), and `dotnet test`.

- **Code style** is enforced by `.editorconfig` + `dotnet format`. Run
  `dotnet format Veya.sln` to fix formatting before committing.
- **Tests** live in `tests/`, mirroring `src/` project names with a `.Tests`
  suffix. Tests must **not** assume a desktop session — no D-Bus session bus, no
  display server, no GNOME. CI is headless `ubuntu-latest`; abstract such
  dependencies behind interfaces and fake them.
- **New dependencies** must carry a permissive, Apache-2.0-compatible license.
  The license scan (`scripts/license-scan.sh`) enforces this; if a legitimate
  dependency can't be auto-resolved, add its package id to
  `scripts/license-allowlist.txt` with a comment recording the reviewed license.

## Security and privacy (please read)

- **All shell execution goes through the central safety layer** (allowlist,
  timeouts, output caps, audit log — see [docs/security.md](docs/security.md)).
  Never spawn a process directly from a tool; extend the safety layer first if it
  doesn't cover what you need.
- **Never require sudo without asking the user first.** Runtime code must never
  assume root.
- **Local-first privacy is a product pillar.** Every new context source needs a
  permission gate and audit-log coverage — call this out explicitly in your PR
  description.
- Found a vulnerability? Don't open a public issue — see
  [SECURITY.md](SECURITY.md).

## Commit and PR conventions

- Write focused commits with clear messages explaining the *why*.
- PR descriptions should note any impact on the privilege model, permissions, or
  audit log (or state explicitly that there is none).
- Be respectful and constructive.

## License of contributions

By contributing, you agree that your contributions are licensed under the
[Apache License 2.0](LICENSE), the same license as the project.
