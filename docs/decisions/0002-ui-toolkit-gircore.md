# ADR-0002: GTK4/libadwaita via Gir.Core for the overlay UI

- Status: accepted
- Date: 2026-06-12

## Context

The overlay UI (later phase) needs to look and feel native on Ubuntu's GNOME
desktop while staying in the C#/.NET stack decided in ADR-0001. Options
considered: GTK4 via Gir.Core, Avalonia, MAUI (no Linux desktop support), Qt
bindings, Electron/web shell.

## Decision

The Overlay component (`Veya.Overlay`) uses **GTK4 + libadwaita through
Gir.Core**, the GObject-introspection-based .NET bindings.

## Consequences

- Native GNOME look, theming, and accessibility for free via libadwaita; the
  overlay behaves like a first-party GNOME app.
- Stays within the single C#/.NET solution and `./scripts/verify.sh` — no second
  UI toolchain (rules out Electron's footprint and Avalonia's non-native look).
- Gir.Core is younger than Gtk# was; we accept occasional missing bindings and
  may contribute upstream. The overlay is deliberately thin (text entry,
  response view, D-Bus client), which bounds that risk.
- UI code cannot run in headless CI; per hard rule, tests must not assume a
  desktop session, so overlay logic is kept in testable, GTK-free classes with a
  thin GTK shell.
- Non-GNOME desktops get a functional but GNOME-flavored window; acceptable for
  an Ubuntu-first project.
