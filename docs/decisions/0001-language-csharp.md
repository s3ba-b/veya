# ADR-0001: C# / .NET 9 for all core components

- Status: accepted
- Date: 2026-06-12

## Context

Veya needs one language for a long-running daemon, an MCP server, shared
contracts, and later a GTK4 UI — on Linux, with good D-Bus and systemd support,
strong async I/O, and an official MCP SDK. Candidates considered: Rust, Go,
Python, C#/.NET.

## Decision

All core components (Daemon, McpServer, Shared, Overlay) are **C# / .NET 9**, in
one solution. JavaScript is used only for a future GNOME Shell extension shim,
where GJS is mandatory (out of scope for now).

## Consequences

- One toolchain and one CI pipeline (`dotnet build/test/format` via
  `./scripts/verify.sh`); contracts shared as plain C# types in `Veya.Shared`.
- First-class libraries for every component: Generic Host +
  `Microsoft.Extensions.Hosting.Systemd` (daemon), Tmds.DBus (D-Bus), the
  official ModelContextProtocol C# SDK (MCP), Gir.Core (GTK4/libadwaita), and
  LLamaSharp for in-process local inference later.
- .NET runtime dependency on end-user machines; mitigated by self-contained
  publishing when packaging becomes a topic.
- GC pauses and memory footprint are acceptable for an assistant daemon; no
  component is latency-critical at the microsecond level.
- The GNOME Shell shim is a deliberate exception, kept as a thin D-Bus client so
  the JavaScript surface stays minimal.
