# ADR-0006: Clipboard write mechanism — wl-copy/xclip via the safety layer

- Status: accepted
- Date: 2026-06-14

## Context

Milestone 2 (docs/roadmap.md) introduces clipboard writing — the first
non-read-only tool. The Daemon and McpServer are unprivileged user services
(ADR-0003); there is no GUI process of Sage's own that owns a desktop
connection. We need a way to put text on the system clipboard from a background
process, on both Wayland (the default on modern Ubuntu/GNOME) and X11.

Options considered:

- **`wl-copy` / `xclip` as allowlisted commands** through the existing
  `ISafeExecutor` (docs/security.md). `wl-copy` (from `wl-clipboard`) serves
  Wayland; `xclip` serves X11. Both read the content from stdin.
- **XDG Desktop Portal `org.freedesktop.portal.Clipboard`** over D-Bus. In
  practice this portal is bound to a RemoteDesktop/screencast session, not
  general-purpose clipboard writes — a poor fit, and it reuses none of the
  safety-layer machinery.
- **Native Wayland/X11 client libraries** linked into the process. Heaviest
  option: new native dependency surface, and it bypasses the safety layer
  entirely.

## Decision

**Write the clipboard by running `wl-copy` (Wayland) or `xclip -selection
clipboard` (X11) through `ISafeExecutor`**, selected at call time by session
type (`WAYLAND_DISPLAY` / `XDG_SESSION_TYPE`).

Reasons:

- **Reuses the one execution path.** Hard rule 1 says all shell execution goes
  through the safety layer; clipboard write becomes just another allowlisted
  command (`wl-copy` with no args; `xclip` restricted to the `clipboard`
  selection), inheriting the allowlist, timeout, output caps, and `tool.exec`
  audit event for free.
- **Content stays out of the audit log.** The text is passed via **stdin**, not
  argv. `ExecRequest` gained an optional `StandardInput` that `SafeExecutor`
  pipes to the process and then closes; only argv is recorded, so clipboard
  contents are never written to the trail. (This also avoids argv mangling of
  whitespace/newlines.)
- **No new dependency or privilege.** `wl-clipboard`/`xclip` are stock packages;
  nothing runs as root.
- **Fits "no desktop in tests" (hard rule 3).** The tool depends on
  `ISafeExecutor` and `IPermissionGate`, both faked in unit tests; the stdin
  plumbing is covered against `cat`. No real clipboard or display is touched in
  CI.

The write is gated by the permission gate from ADR-0005: `set_clipboard` checks
`PermissionSource.Clipboard` (default-deny) before doing anything and refuses —
returning a message to the model — when not granted.

## Consequences

- `set_clipboard` (`ClipboardTool`) is the first write tool. Allowlist entries:
  `wl-copy` → `/usr/bin/wl-copy` (no args), `xclip` → `/usr/bin/xclip`
  (`-selection clipboard` only).
- `ExecRequest.StandardInput` and `SafeExecutor` stdin support are general, not
  clipboard-specific; future tools that must keep payloads out of the audit log
  can reuse them.
- **Clipboard ownership is a persistent process by design.** On Wayland the
  selection is served for as long as the source client lives, so `wl-copy`
  forks a daemon that outlives the call and re-parents away from our process
  tree — while keeping the inherited stderr pipe open. Capturing its output and
  waiting for EOF therefore hangs indefinitely (issue #41). `set_clipboard`
  consequently runs in **detached mode** (`ExecRequest.Detached`): stdout/stderr
  are not captured (no pipe for the survivor to hold), stdin is still piped, and
  `SafeExecutor` waits only a bounded time for the foreground process to exit,
  leaving the daemon running. `xclip` on X11 has the same persistent-owner shape
  and uses the same mode.
- Independently, `SafeExecutor`'s timeout was made hard against this class of
  bug: a detached descendant can no longer block the post-kill reap (#41).
- The user must have `wl-clipboard` (Wayland) or `xclip` (X11) installed. A
  missing binary surfaces as a normal failed execution, not a crash.
- Reading the clipboard (paste) is **not** part of this decision; if added later
  it gets the same permission gate and a `wl-paste`/`xclip -o` allowlist entry.
