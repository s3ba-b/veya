# Running the daemon

Status: the daemon is a skeleton exposing a stub `org.sage.Sage1.Ask` on the
D-Bus session bus (see docs/dbus-interfaces.md and docs/roadmap.md). This
covers running it directly, as a systemd user service, and trying `Ask`.

## Run directly (development)

```sh
dotnet run --project src/Sage.Daemon
```

Runs in the foreground, logs to the console. Ctrl-C to stop. If a session bus
is available, it registers `org.sage.Sage1` at `/org/sage/Sage1`; otherwise it
logs a warning and runs without the D-Bus endpoint.

## MCP tools in development

`Sage.Daemon` discovers tools from `Sage.McpServer` (docs/architecture.md,
"Model router") by spawning it as a child process over stdio
(`Sage.Daemon.Mcp.McpToolGateway`). By default it looks for an `Sage.McpServer`
executable next to its own binary — a layout that only exists after
`dotnet publish` places both projects side by side (see
packaging/systemd/sage-daemon.service).

For `dotnet run --project src/Sage.Daemon`, build `Sage.McpServer` separately
and point the Daemon at it:

```sh
dotnet build src/Sage.McpServer
export Mcp__ServerPath=$(pwd)/src/Sage.McpServer/bin/Debug/net9.0/Sage.McpServer
dotnet run --project src/Sage.Daemon
```

Without this, `McpToolGateway` logs a warning and continues with zero tools —
`Ask` still works, but the model has nothing to call and can only answer from
its own knowledge (it may suggest commands for *you* to run instead of running
them itself).

## Try `Ask`

With the daemon running and a session bus available:

```sh
gdbus call --session --dest org.sage.Sage1 \
  --object-path /org/sage/Sage1 --method org.sage.Sage1.Ask "hello sage"
# ('Sage received: hello sage',)

busctl --user call org.sage.Sage1 /org/sage/Sage1 org.sage.Sage1 Ask s "hello sage"
# s "Sage received: hello sage"
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
   dotnet publish src/Sage.Daemon -c Release -o ~/.local/lib/sage
   ```

2. Install the unit file:

   ```sh
   mkdir -p ~/.config/systemd/user
   cp packaging/systemd/sage-daemon.service ~/.config/systemd/user/
   systemctl --user daemon-reload
   ```

3. Enable and start:

   ```sh
   systemctl --user enable --now sage-daemon
   ```

4. Check status and logs:

   ```sh
   systemctl --user status sage-daemon
   journalctl --user -u sage-daemon -f
   ```

The service runs unprivileged, as your user (ADR-0003). `WantedBy=default.target`
starts it with your user session (also works under `loginctl enable-linger` for
running without an active login).

## Uninstall

```sh
systemctl --user disable --now sage-daemon
rm ~/.config/systemd/user/sage-daemon.service
systemctl --user daemon-reload
```

## Overlay window

`Sage.Overlay` (ADR-0002) is a minimal GTK4/libadwaita window — a prompt entry
and a response area — that talks to `org.sage.Sage1` over D-Bus. It has no
intelligence of its own; it just calls `Ask` and shows the reply.

Requires the GTK4/libadwaita runtime libraries (`libgtk-4-1`,
`libadwaita-1-0`) and a desktop session (X11 or Wayland).

With the daemon running and registered on the session bus:

```sh
dotnet run --project src/Sage.Overlay
```

Type a prompt and press Enter. If the daemon isn't running or no session bus
is available, the response area shows "Sage is unreachable: ...".
