# Running the daemon

Status: the daemon is a skeleton (no D-Bus endpoint yet — see
docs/roadmap.md and issue tracking the `Ask` stub). This covers running it as
a systemd **user** service, which is how it will run in normal use.

## Run directly (development)

```sh
dotnet run --project src/Sage.Daemon
```

Runs in the foreground, logs to the console. Ctrl-C to stop.

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
