# D-Bus interfaces (draft)

Status: **draft** — this is the Milestone 1 contract plus sketches of what comes
next. Anything marked *(planned)* is not stable and may change; changes to the
shipped surface require an ADR or roadmap note.

## Bus identity

| | |
|---|---|
| Bus | session bus |
| Well-known name | `org.sage.Sage1` |
| Object path | `/org/sage/Sage1` |
| Interface | `org.sage.Sage1` |
| Activation | systemd user service (`sage-daemon.service`); D-Bus activation considered later |

The `1` suffix versions the whole contract: breaking changes ship as
`org.sage.Sage2` alongside, never as in-place edits.

## Interface `org.sage.Sage1`

### Methods

```
Ask(in s prompt, out s reply)
```
**Implemented**: registered at `/org/sage/Sage1` via Tmds.DBus
(`Sage.Daemon.Sage1Service`). Routes the prompt through the model router
(`Sage.Daemon.ModelRouter`, currently always `ClaudeBackend`), which sends the
tools discovered from `Sage.McpServer` (`Sage.Daemon.Mcp.IMcpToolGateway`) and
drives any resulting tool calls via `ToolUseLoopRunner` before returning the
model's final reply. If the McpServer process is unavailable, no tools are
sent and the reply is plain text.
If no Anthropic API key is configured, `ClaudeBackend` throws
`BackendUnavailableException`, which `Sage1Service` currently turns into a
plain-text error reply; mapping this to a dedicated
`org.sage.Sage1.Error.BackendUnavailable` D-Bus error name is still planned,
along with `org.sage.Sage1.Error.Busy`.

If no D-Bus session bus is available (e.g. headless CI), the daemon logs a
warning and continues running without this endpoint — see
`Sage.Daemon.IDBusSessionConnector`.

```
AskSession(in s sessionId, in s prompt, out s reply)        (planned)
```
Multi-turn variant; empty `sessionId` creates a session and the reply is paired
with a `SessionCreated` signal carrying the new id.

```
CancelSession(in s sessionId)                               (planned)
```
Cancels in-flight inference/tool calls for the session.

```
GetStatus(out a{sv} status)                                 (planned)
```
Daemon status: version, active backend (`"claude"` / `"local"`), MCP server
health, counts of pending requests.

### Signals

```
ResponseChunk(s sessionId, s chunk)                         (planned)
```
Streaming tokens for frontends that render incrementally; `Ask`/`AskSession`
still return the full reply.

```
CloudUsage(s sessionId, s backend, u sentBytes)             (planned)
```
Emitted whenever data leaves the machine — this is the user-visible cloud usage
hook (a product pillar; see docs/security.md).

```
ToolExecuted(s sessionId, s toolName, b allowed)            (planned)
```
Mirrors safety-layer audit events so UIs can show live activity.

### Properties

```
Version          s   read   daemon semantic version
ActiveBackend    s   read   "claude" | "local"              (planned)
```

## Conventions

- Frontends (Overlay, GNOME Shell shim, CLI) are pure clients of this interface;
  no intelligence or system access on the client side.
- Methods never block on user permission prompts; a denied permission returns an
  error (`org.sage.Sage1.Error.PermissionDenied`) so UIs can react.
- Tests must not assume a session bus exists; the daemon's D-Bus surface is
  wrapped behind an interface and faked in tests.
