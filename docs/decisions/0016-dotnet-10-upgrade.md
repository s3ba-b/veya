# ADR-0016: Upgrade target framework to .NET 10

- Status: accepted
- Date: 2026-06-17

## Context

ADR-0001 chose C# / .NET 9 for all core components. .NET 9 is a Standard Term
Support release; .NET 10 is the next Long Term Support release and is already
the SDK installed in the dev environment. `Directory.Build.props` already set
`RollForward=Major` specifically to let dev machines running only a newer SDK
build and run the net9.0 binaries, anticipating this move.

This ADR does not revisit the choice of C#/.NET itself (ADR-0001's language
decision stands) — it only updates the target version.

## Decision

1. **`TargetFramework` moves from `net9.0` to `net10.0`** in
   `Directory.Build.props`, the single source of truth all projects inherit
   from.
2. **Platform NuGet packages tied to the framework version** —
   `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.Systemd`,
   `Microsoft.Extensions.Configuration`,
   `Microsoft.Extensions.Configuration.Abstractions`, `Microsoft.Data.Sqlite`
   — move from `9.0.0` to the latest stable `10.0.x` (`10.0.9` at the time of
   this change). Third-party packages (Tmds.DBus, ModelContextProtocol,
   Whisper.net, GirCore.Adw-1, Anthropic SDK, sqlite-vec) are left as-is; they
   are not framework-versioned and already build against `net10.0`.
3. **CI and dev setup install the .NET 10 SDK**: `.github/workflows/ci.yml`
   (`actions/setup-dotnet` `dotnet-version: 10.0.x`) and
   `scripts/setup-dev.sh` (`dotnet-sdk-10.0`, version check raised to `>= 10`).
4. `RollForward=Major` is kept as forward cover for the next major SDK bump,
   not removed.

## Consequences

- No component code changes were required; `./scripts/verify.sh` (build,
  format, license scan, tests) passes unchanged on `net10.0`.
- Supersedes ADR-0001 **only for the version number** in its title/decision
  text — the rationale for choosing C#/.NET over Rust/Go/Python is untouched
  and still lives in ADR-0001.
- Future framework bumps should follow the same pattern: bump
  `Directory.Build.props`, bump framework-tied Microsoft packages, bump CI/dev
  SDK install, add a new ADR.
