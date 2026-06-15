# ADR-0013: Screen awareness — on-demand portal screenshot + local OCR

- Status: accepted
- Date: 2026-06-15

## Context

Milestone 4 (docs/roadmap.md) starts screen awareness: "explicitly
permission-gated, local-first". `PermissionSource.Screen` already exists in the
enum (ADR-0005) but is unused. We need a way for Veya to read what's currently
on the user's screen when that's relevant to a question (e.g. "what does this
error dialog say?"), without becoming a standing privacy liability.

Three shapes were considered:

1. **Continuous/periodic capture, indexed like the personal context store**
   (ADR-0009). Rejected: a searchable history of everything ever shown on
   screen — passwords, messages, anything — is a categorically bigger privacy
   surface than the rest of the index combined, and contradicts "local-first
   privacy is a product pillar, not a feature". Out of scope entirely for now.
2. **Cloud vision model reads the screenshot.** Rejected as the default: it
   would send a screen image off the machine on every use, which is exactly
   the kind of thing the `CloudUsage` signal exists to make visible and rare.
   A cloud OCR/vision fallback could be a future opt-in, but the first
   implementation should work fully local.
3. **On-demand, ephemeral: capture once when asked, OCR locally, discard the
   image immediately, return only text.** This mirrors `set_clipboard`
   (ADR-0006) — a single gated tool call, no standing process, no stored
   artifact.

For the capture mechanism itself:

- **XDG Desktop Portal `org.freedesktop.portal.Screenshot`** (session bus):
  works the same way under X11 and Wayland, and — independent of Veya's own
  permission gate — shows the user a native GNOME "take a screenshot?" prompt.
  That's a second, OS-level consent layer for free.
- **Direct tools** (`grim` for Wayland, `scrot`/`import` for X11): would need
  compositor detection (like ADR-0006's clipboard backend) and skip the portal
  prompt entirely — less defense in depth, more branching.

## Decision

**On-demand, ephemeral capture via the XDG portal, local OCR via `tesseract`,
exposed as the `read_screen_text` MCP tool** (alongside `ClipboardTool` in
`Veya.McpServer`).

Flow for `read_screen_text`:

1. Check `PermissionSource.Screen` via `IPermissionGate` (default-deny,
   ADR-0005). Denied → return an explanatory message, nothing else happens.
2. `IScreenCapture` (`PortalScreenshotClient`) connects to the session bus,
   calls `org.freedesktop.portal.Screenshot.Screenshot(parent_window="",
   options={"interactive": false})`, and awaits the `Response` signal on the
   returned `Request` object. This is where the user sees GNOME's screenshot
   permission dialog. A non-zero response code (cancelled/denied) or no
   session bus → `null`, handled the same as "capture failed".
3. The portal writes a temporary PNG and returns its `file://` URI in
   `results["uri"]`. A pure helper, `PortalScreenshotResponse`, turns
   `(response, results)` into a local file path or `null` — this is the only
   part of the capture path that is unit-tested (hard rule 3: the D-Bus/portal
   call itself needs a session bus and a portal implementation, neither
   available in CI, so it's exercised manually like
   `SessionBusNotificationSource`, ADR-0012).
4. `tesseract <path> stdout` runs through `ISafeExecutor` (hard rule 1), same
   allowlist/timeout/output-cap/`tool.exec` audit machinery as every other
   tool.
5. The temp PNG is deleted immediately after OCR, success or failure — the
   image is never written anywhere else and never reaches the model.
6. A `screen.capture` audit event records success/failure, the OCR'd text
   length, and duration — never the image or the text itself (mirrors
   `notification.capture`, ADR-0011).
7. The extracted text is returned to the model as the tool result, to use like
   any other tool output for that one `Ask`.

## Consequences

- **Two consent layers.** Veya's `Screen` permission (default-deny, like every
  source) plus the desktop's own portal dialog — a user who has granted the
  permission still sees, and can decline, each individual capture.
- **Nothing persists.** No screenshot file, no OCR'd text, and no index entry
  outlive the single tool call. Re-running `read_screen_text` always re-asks
  the portal and OCRs again.
- **New apt dependency: `tesseract-ocr`.** Not auto-installed (hard rule 2);
  documented as a setup prerequisite. A missing binary surfaces as a normal
  failed `tool.exec`, not a crash — `read_screen_text` reports it like any
  other capture failure.
- **CI stays headless.** `PortalScreenshotResponse` (pure parsing) is
  unit-tested; `PortalScreenshotClient` (D-Bus/portal) and the `tesseract`
  invocation are exercised manually, isolated to `Veya.McpServer.Tools`.
- **Deferred:** cloud vision fallback when local OCR is insufficient (e.g.
  handwriting, complex layouts) — would need its own `CloudUsage`-visible
  opt-in, not the default.
