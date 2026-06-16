# ADR-0014: GNOME Shell extension as the keyboard-summon frontend

- Status: accepted
- Date: 2026-06-16

## Context

Milestone 4 (docs/roadmap.md) calls for "GNOME polish: shell extension shim
(JavaScript), keyboard summon, theming". The existing frontend is
`Veya.Overlay` (GTK4/Gir.Core, ADR-0002) — a standalone window the user
launches manually. We need a way to summon Veya from anywhere in GNOME without
switching applications.

GNOME Shell extensions are the only first-class mechanism for:
- registering global keyboard shortcuts that work regardless of which window has
  focus, and
- rendering UI elements that float above all windows (the Shell's `uiGroup` layer).

GNOME Shell mandates that extensions are written in **GJS (GNOME JavaScript)**,
the JS runtime built into every GNOME Shell process. There is no GJS .NET
binding; this is the one place where the project's C# rule (ADR-0001)
explicitly does not apply. ADR-0001 already anticipated this: "JavaScript only
for a future GNOME Shell extension shim".

### GNOME version target

GNOME 45 (released September 2023) introduced ES-module syntax (`import …
from`) as the mandatory extension API, replacing the old `function enable() {}`
globals. Ubuntu 24.04 LTS ships GNOME 46; Ubuntu 22.04 LTS ships GNOME 42.

Targeting GNOME 45+ (ES modules) means:
- Ubuntu 24.04 and later: fully supported.
- Ubuntu 22.04: not supported by this extension; users on 22.04 keep the
  Overlay window.

The ES module API is cleaner, forward-compatible, and avoids maintaining two
incompatible code paths.

## Decision

**A GJS ES-module GNOME Shell extension (`src/gnome-shell-extension/`,
UUID `veya@veya.org`) targeting GNOME 45+.**

The extension is a thin D-Bus client of `org.veya.Veya1` — no intelligence,
no system access, no permission logic of its own:

1. **Keyboard summon.** A global keybinding (`<Super><Shift>v` by default,
   stored in a GSettings schema so GNOME can surface it in keyboard settings)
   toggles a floating panel via `Main.wm.addKeybinding`.
2. **Panel UI.** An `St.BoxLayout` in `Main.uiGroup` (above all windows):
   - `St.Entry` for the prompt; Enter submits, Escape closes.
   - Status row: "Thinking…" label while the D-Bus call is in flight;
     "☁ cloud" badge (subscribed to `CloudUsage` signal) when data left the
     machine.
   - `St.Label` for the reply, with word-wrap and selectable text.
3. **D-Bus call.** `Gio.DBus.session.call()` to `org.veya.Veya1/Ask` with a
   60 s timeout and a `Gio.Cancellable` (cancelled on Escape or panel close).
4. **`CloudUsage` signal.** Subscribed via `Gio.DBus.session.signal_subscribe`
   for the lifetime of the extension; shows the cloud badge in the current
   panel session. This is the user-visible cloud transparency hook (product
   pillar) surfaced in the same extension that triggers the call.
5. **Theming.** `stylesheet.css` uses GNOME's dark-panel palette: translucent
   dark background, system font, subtle focus ring.

Install for development: `./scripts/install-gnome-extension.sh`.

## Consequences

- **Language exception.** `src/gnome-shell-extension/` is JavaScript only;
  it is not part of the .NET solution and not compiled or tested by
  `./scripts/verify.sh`. CI stays headless (hard rule 3) and the extension
  requires a live GNOME Shell session to test — exercised manually.
- **GNOME 45+ only.** Ubuntu 22.04 users cannot use the extension; the
  standalone Overlay (ADR-0002) remains their path.
- **GSettings schema required.** The extension ships a
  `org.gnome.shell.extensions.veya.gschema.xml` compiled by the install
  script via `glib-compile-schemas`. Without it the keybinding registration
  crashes at enable-time.
- **Deferred:** streaming tokens (`ResponseChunk` signal, dbus-interfaces.md),
  preferences UI (keybinding editable via `gsettings` for now), and GNOME 42
  compat shim.
