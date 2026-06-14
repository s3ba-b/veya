# Running the daemon

Status: the daemon is a skeleton exposing a stub `org.veya.Veya1.Ask` on the
D-Bus session bus (see docs/dbus-interfaces.md and docs/roadmap.md). This
covers running it directly, as a systemd user service, and trying `Ask`.

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
export Mcp__ServerPath=$(pwd)/src/Veya.McpServer/bin/Debug/net9.0/Veya.McpServer
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

## Try `Ask`

With the daemon running and a session bus available:

```sh
gdbus call --session --dest org.veya.Veya1 \
  --object-path /org/veya/Veya1 --method org.veya.Veya1.Ask "hello veya"
# ('Veya received: hello veya',)

busctl --user call org.veya.Veya1 /org/veya/Veya1 org.veya.Veya1 Ask s "hello veya"
# s "Veya received: hello veya"
```

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
