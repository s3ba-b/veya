<!-- Draft for issue #1 — file with:
     gh issue create --title "Solution scaffolding + CI green" --label enhancement --body-file .github/issue-1-draft.md
     Delete this file afterwards. -->

## Summary

Create the .NET 9 solution skeleton so `./scripts/verify.sh` does real work and CI runs green. First step of Milestone 1 (docs/roadmap.md).

## Motivation

Everything else in Milestone 1 (daemon skeleton, D-Bus stub, MCP server) needs projects to land in. This issue establishes the build/test/format pipeline end to end.

## Proposed approach

- `Sage.sln` at the repo root
- `src/`: `Sage.Shared` (classlib), `Sage.Daemon` (worker template), `Sage.McpServer` (console) — all net9.0
- `Directory.Build.props`: TargetFramework, Nullable=enable, TreatWarningsAsErrors, LangVersion=latest
- `tests/`: `Sage.Shared.Tests`, `Sage.Daemon.Tests`, `Sage.McpServer.Tests` (xUnit), one real smoke test each
- No feature packages yet (no Tmds.DBus, no MCP SDK) — those belong to later issues

## Privacy & security impact

None — no runtime behavior, no shell execution, no context sources, no cloud calls.

## Acceptance criteria

- [ ] `Sage.sln` with Daemon, McpServer, Shared under `src/` and matching `.Tests` projects under `tests/`
- [ ] `Directory.Build.props` enforces net9.0, nullable, warnings-as-errors
- [ ] At least one passing test per test project
- [ ] No new runtime dependencies beyond the test stack
- [ ] `./scripts/verify.sh` passes locally
- [ ] CI green on the PR
