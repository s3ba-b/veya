# ADR-0012: Capturing real notifications — monitor the session bus, non-invasively

- Status: accepted
- Date: 2026-06-15

## Context

ADR-0011 shipped the notification pipeline behind `INotificationSource` but no
real source. To capture actual desktop notifications, Veya must observe the
freedesktop notification protocol on the **session bus**: applications call
`Notify(...)` on `org.freedesktop.Notifications` (object `/org/freedesktop/Notifications`),
which the desktop's notification server (GNOME Shell, etc.) owns.

There are three ways to see those calls:

1. **Become the notification server** — own the `org.freedesktop.Notifications`
   bus name. Only one owner is allowed, and the desktop already owns it; taking
   it stops notifications from being shown unless we re-implement and forward a
   full server. Invasive and fragile.
2. **Proxy** — take the name with replacement and forward every call to the
   original server. Even more invasive; we become a single point of failure for
   all notifications.
3. **Monitor** — passively observe `Notify` traffic via the bus's monitoring
   facility (`org.freedesktop.DBus.Monitoring.BecomeMonitor`), without owning the
   name or altering delivery.

## Decision

**Monitor (option 3), read-only and tightly scoped.** A
`SessionBusNotificationSource : INotificationSource` in `Veya.Daemon` becomes a
session-bus monitor with a match rule scoped to the notification protocol and
maps each observed `Notify` call to a `Notification`.

This follows the project's least-privilege, least-disruption stance: Veya watches
notifications, it does not intercept or replace them, so the user's desktop keeps
working exactly as before even if Veya is stopped.

### Plan (implementation-ready)

- **Class:** `SessionBusNotificationSource` in `Veya.Daemon` (D-Bus/desktop
  coupled, like `DBusSessionConnector` — not `Veya.Shared`). Implements
  `INotificationSource.ReadAsync`.
- **Connection:** connect to `Address.Session`; if null/empty, log and yield
  nothing — the same graceful no-session-bus degradation as
  `DBusSessionConnector` (hard rule 3). Do **not** connect until `ReadAsync` is
  called; the `NotificationCaptureService` only calls it after the `Notifications`
  permission is granted, so a denied user causes no bus connection at all.
- **Match rule:** monitor only
  `type='method_call',interface='org.freedesktop.Notifications',member='Notify'`
  via `BecomeMonitor` (a tight rule so we observe notification calls, not
  arbitrary bus traffic). If `BecomeMonitor` is unavailable/denied, log and yield
  nothing.
- **Streaming:** a bounded `System.Threading.Channels.Channel<Notification>`;
  the monitor callback maps and writes to the channel, `ReadAsync` yields from
  it via `ReadAllAsync(cancellationToken)`. Bounded + drop-oldest so a flood
  can't grow unbounded (the store is capped anyway).
- **Mapping — pure and unit-tested.** A static `NotifyMessageMapper` turns the
  `Notify` argument list into a `Notification`:
  - args order: `app_name (s)`, `replaces_id (u)`, `app_icon (s)`, `summary (s)`,
    `body (s)`, `actions (as)`, `hints (a{sv})`, `expire_timeout (i)`.
  - `Id`: `replaces_id` when non-zero, else a generated id.
  - `Urgency`: from `hints["urgency"]` (byte 0/1/2 → `Low`/`Normal`/`Critical`),
    defaulting to `Normal` when absent/unparseable.
  - `Timestamp`: capture time (`DateTimeOffset.UtcNow`).
  This mapper is the only part with logic worth testing; it has no D-Bus
  dependency, so it is covered by unit tests. The bus wiring around it is thin
  and exercised manually / in integration, not in CI.
- **DI:** register `INotificationSource` → `SessionBusNotificationSource` and add
  `NotificationCaptureService` as a hosted service. Both degrade to no-ops with
  no session bus or no permission.

## Consequences

- **The user's notifications are never disrupted.** Monitoring is observation
  only; stopping Veya changes nothing about how notifications are delivered.
- **Permission still gates everything.** The capture service checks
  `Notifications` before calling `ReadAsync`, so without a grant Veya never even
  opens a monitor connection — privacy by construction, and the
  `permission.decision` / `notification.capture` audit trail is unchanged
  (ADR-0011).
- **Monitoring is broad by nature; the match rule keeps it narrow.** We scope to
  `Notify` on the notifications interface so Veya does not observe unrelated bus
  messages. (Becoming a monitor is a same-user session-bus capability; it does
  not grant cross-user visibility.)
- **Tmds.DBus may not expose `BecomeMonitor` directly.** If the high-level API
  can't install a monitor, the implementation issues the
  `org.freedesktop.DBus.Monitoring.BecomeMonitor` call on the bus connection and
  handles incoming messages at a lower level. This risk is isolated to
  `SessionBusNotificationSource`; the rest of the pipeline (mapper, store,
  digest, capture service) is unaffected and already tested.
- **CI stays headless.** Only the pure mapper is unit-tested; the bus-coupled
  source is not exercised in CI (hard rule 3), mirroring how `DBusSessionConnector`
  is treated.
- **Deferred (own issues, unchanged):** model-driven natural-language summaries
  and a D-Bus surface to expose the digest to frontends.
