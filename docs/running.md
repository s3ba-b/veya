# Running the daemon

Status: the daemon exposes `org.veya.Veya1.Ask` on the D-Bus session bus,
routing the prompt through the model router and MCP tools (see
docs/dbus-interfaces.md and docs/roadmap.md). This covers running it directly,
as a systemd user service, and trying `Ask`.

## Run directly (development)

```sh
dotnet run --project src/Veya.Daemon
```

Runs in the foreground, logs to the console. Ctrl-C to stop. If a session bus
is available, it registers `org.veya.Veya1` at `/org/veya/Veya1`; otherwise it
logs a warning and runs without the D-Bus endpoint.

## MCP tools in development

`Veya.Daemon` discovers tools from `Veya.McpServer` (docs/architecture.md,
"Model router") by spawning it as a child process over stdio
(`Veya.Daemon.Mcp.McpToolGateway`). By default it looks for an `Veya.McpServer`
executable next to its own binary — a layout that only exists after
`dotnet publish` places both projects side by side (see
packaging/systemd/veya-daemon.service).

For `dotnet run --project src/Veya.Daemon`, build `Veya.McpServer` separately
and point the Daemon at it:

```sh
dotnet build src/Veya.McpServer
export Mcp__ServerPath=$(pwd)/src/Veya.McpServer/bin/Debug/net10.0/Veya.McpServer
dotnet run --project src/Veya.Daemon
```

Without this, `McpToolGateway` logs a warning and continues with zero tools —
`Ask` still works, but the model has nothing to call and can only answer from
its own knowledge (it may suggest commands for *you* to run instead of running
them itself).

### Clipboard writing (permission-gated)

The `set_clipboard` tool (the first write tool, ADR-0005/0006) is **default-deny**:
the model can call it, but the permission gate refuses unless you've granted the
clipboard source. Grant it by setting the permission in the McpServer's
environment (it binds the `Permissions` config section):

```sh
export Permissions__Clipboard=true
```

Set this in the same environment the Daemon launches McpServer from. On Wayland
it uses `wl-copy` (install `wl-clipboard`); on X11, `xclip`. The clipboard text
is passed via stdin, so it never appears in the audit log — only the
`permission.decision` and `tool.exec` events do. With the permission unset or
`false`, every attempt is logged as a denied `permission.decision` and nothing
is written.

### Screen text (permission-gated)

The `read_screen_text` tool (ADR-0005/0013) is also **default-deny**. Grant it
the same way:

```sh
export Permissions__Screen=true
```

When granted, a call still triggers the XDG Desktop Portal's screenshot
prompt — a second, per-call consent the user can decline. On success, the
screenshot is OCR'd with `tesseract` (install the `tesseract-ocr` package) and
the temp file is deleted immediately; nothing is persisted. With the
permission unset or `false`, every attempt is logged as a denied
`permission.decision` and no screenshot is taken.

## Try `Ask`

With the daemon running and a session bus available:

```sh
gdbus call --session --dest org.veya.Veya1 \
  --object-path /org/veya/Veya1 --method org.veya.Veya1.Ask "hello veya"

busctl --user call org.veya.Veya1 /org/veya/Veya1 org.veya.Veya1 Ask s "hello veya"
```

The reply is the model's answer. With no inference backend reachable (no Ollama
running and no cloud API key configured), `Ask` returns a plain-text error
instead: `Veya can't reach its model backend right now: …`.

## Voice (permission-gated)

`AskVoice` (ADR-0015) is the voice equivalent of `Ask`: it records a question,
transcribes it locally, answers it, and speaks the reply aloud. It's
**default-deny**, like every other source:

```sh
export Permissions__Microphone=true
```

Set this in the Daemon's own environment (voice runs in the Daemon, not
McpServer — there's no compositor/portal involved, just `arecord`). You also
need:

- `alsa-utils` and `espeak-ng` installed (`./scripts/setup-dev.sh` does this).
- A local Whisper model fetched once: `./scripts/download-whisper-model.sh`
  (downloads the multilingual `ggml-base.bin` to
  `~/.local/share/veya/models/` by default). Without it, `AskVoice` still
  records but reports that it didn't catch any words — no crash.

Try it:

```sh
gdbus call --session --dest org.veya.Veya1 \
  --object-path /org/veya/Veya1 --method org.veya.Veya1.AskVoice 8000
```

Speak within the given window (milliseconds; capped by `Voice:MaxRecordingMs`,
default 15000). The call returns `(transcript, reply)`, and you should hear
the reply spoken back. With the permission unset or `false`, nothing is
recorded — `AskVoice` returns immediately with an explanatory reply and an
empty transcript.

## Run as a systemd user service

The daemon integrates with systemd via
[`Microsoft.Extensions.Hosting.Systemd`](https://www.nuget.org/packages/Microsoft.Extensions.Hosting.Systemd)
(`Type=notify`): it signals readiness on startup and stopping on shutdown, and
switches its console logging to the systemd journal format automatically when
launched under systemd. Outside systemd (e.g. `dotnet run`, or in CI) these
calls are no-ops — no `NOTIFY_SOCKET`, no behavior change.

1. Publish a build:

   ```sh
   dotnet publish src/Veya.Daemon -c Release -o ~/.local/lib/veya
   ```

2. Install the unit file:

   ```sh
   mkdir -p ~/.config/systemd/user
   cp packaging/systemd/veya-daemon.service ~/.config/systemd/user/
   systemctl --user daemon-reload
   ```

3. Enable and start:

   ```sh
   systemctl --user enable --now veya-daemon
   ```

4. Check status and logs:

   ```sh
   systemctl --user status veya-daemon
   journalctl --user -u veya-daemon -f
   ```

The service runs unprivileged, as your user (ADR-0003). `WantedBy=default.target`
starts it with your user session (also works under `loginctl enable-linger` for
running without an active login).

## Uninstall

```sh
systemctl --user disable --now veya-daemon
rm ~/.config/systemd/user/veya-daemon.service
systemctl --user daemon-reload
```

## Overlay window

`Veya.Overlay` (ADR-0002) is a minimal GTK4/libadwaita window — a prompt entry
and a response area — that talks to `org.veya.Veya1` over D-Bus. It has no
intelligence of its own; it just calls `Ask` and shows the reply.

Requires the GTK4/libadwaita runtime libraries (`libgtk-4-1`,
`libadwaita-1-0`) and a desktop session (X11 or Wayland).

With the daemon running and registered on the session bus:

```sh
dotnet run --project src/Veya.Overlay
```

Type a prompt and press Enter. If the daemon isn't running or no session bus
is available, the response area shows "Veya is unreachable: ...".

## GNOME Shell extension

The GNOME Shell extension (`src/gnome-shell-extension/`, ADR-0014) adds a
keyboard-summoned floating panel and a top-bar button. Like the Overlay it is a
thin D-Bus client of `org.veya.Veya1` — it calls `Ask` (or `AskVoice` via the
panel's mic button, ADR-0015) and subscribes to `CloudUsage` for the in-panel
cloud badge; no intelligence runs on this side.

Requires **GNOME Shell 45+** (Ubuntu 24.04 or later) — it uses ES-module
syntax that earlier shells don't support.

Install (copies the extension into `~/.local/share/gnome-shell/extensions/`,
compiles its GSettings schema):

```sh
./scripts/install-gnome-extension.sh
gnome-extensions enable veya@veya-project.org
```

Then reload GNOME Shell so it picks up the extension: **Alt+F2 → `r`** on X11,
or **log out and back in** on Wayland.

With the daemon running, summon the panel with **`Super+Shift+V`** (toggles it),
or click the Veya button in the top bar. Type a prompt and press Enter; the
panel shows "Thinking…" while the `Ask` call is in flight, then the reply. If a
request reaches a cloud backend, a cloud badge appears in the panel.

Click the mic button next to the entry to ask by voice instead (requires
`Permissions__Microphone=true` and the Whisper model — see "Voice" above). The
panel shows "Listening…" while `AskVoice` is in flight (it blocks for the
recording window plus transcription/answering/speaking), then shows both the
heard transcript and the reply.

Change the summon shortcut:

```sh
gsettings set org.gnome.shell.extensions.veya summon-shortcut "['<Super><Shift>v']"
```
