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
- **New dependencies** must carry a permissive license compatible with the
  project's AGPL-3.0 license (MIT, BSD, Apache-2.0, etc.).
  The license scan (`scripts/license-scan.sh`) enforces this; if a legitimate
  dependency can't be auto-resolved, add its package id to
  `scripts/license-allowlist.txt` with a comment recording the reviewed license.
- **Coverage report:** `verify.sh` collects coverage into `./coverage`; run
  `./scripts/coverage-report.sh` afterwards for a human-readable summary
  (`coverage/report/index.html` for the line-by-line view). There's no global
  coverage floor enforced in CI — the codebase mixes thoroughly-unit-testable
  logic with thin bindings that need a real desktop/display/D-Bus session and
  are deliberately excluded (hard rule #3); a single repo-wide percentage would
  either be too lax or penalize those documented exclusions. When you touch a
  file, check its coverage didn't regress and that genuinely new logic (not
  composition roots or hardware/display bindings) has tests.

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

Veya is licensed under the [GNU Affero General Public License v3.0 or later](LICENSE).

Before your first contribution can be merged, you must sign the project's
**Contributor License Agreement** ([CLA.md](CLA.md)). The CLA confirms you have
the right to contribute your code and grants the project the rights it needs to
distribute and, if necessary, relicense the project as a whole.

Signing is automated: the **CLA Assistant** bot comments on your first pull
request with a one-line statement to confirm. Once you've signed, the check
passes on all your future PRs — you only sign once. PRs cannot be merged until
the CLA check is green.
