# ADR-0011: Notification intelligence — capture pipeline, store, and digest

- Status: accepted
- Date: 2026-06-15

## Context

Milestone 3 (docs/roadmap.md) calls for notification intelligence: summarize,
prioritize, and answer questions about desktop notifications. `PermissionSource.Notifications`
already exists (ADR-0005) but nothing produces or consumes it.

Two parts of this feature are inherently hard to build and test in one go:

1. **Capturing real notifications** means talking to the desktop's
   `org.freedesktop.Notifications` interface on the **session bus** — exactly
   what hard rule 3 says tests must not assume. That belongs behind an interface
   and a fake.
2. **Natural-language summaries** ("what did I miss?") mean a round-trip through
   the model router, which is a separate concern from capturing and storing.

So this ADR scopes the **foundation**: the model, an abstract source, a recent
store, the capture pipeline, and a deterministic (no-model) digest — all
desktop-free and headless-testable. The real session-bus source and the
model-driven summaries are deliberate follow-ups, mirroring how the context
index landed its pipeline before any concrete source (ADR-0009).

## Decision

A notification layer in `Veya.Shared.Notifications`:

1. **`Notification`** — `Id`, `AppName`, `Summary`, `Body`, `Urgency`
   (`NotificationUrgency` enum `Low`/`Normal`/`Critical`, matching the
   freedesktop urgency hint 0/1/2), `Timestamp`.

2. **`INotificationSource`** — `IAsyncEnumerable<Notification> ReadAsync(ct)`,
   the stream of incoming notifications. The real
   `org.freedesktop.Notifications` session-bus implementation is **deferred** to
   its own issue; this ADR ships only the abstraction (and a fake in tests).

3. **`INotificationStore` + `InMemoryNotificationStore`** — a **capacity-capped,
   time-ordered** recent store (`Add`, `GetRecent(count)`, `GetByApp`). Capped
   because notifications are transient and unbounded; oldest drop out. In-memory
   because they need not survive a daemon restart (persistence can come later
   behind the same interface). Query is permission-gated by the caller.

4. **`NotificationCaptureService : IHostedService`** (Daemon) — checks
   `Notifications` via `IPermissionGate` once, and if granted streams the source
   into the store, writing a `notification.capture` audit event. If denied, it
   captures nothing (the gate logs the decision). Off the critical path:
   failures are logged, never fatal.

5. **`NotificationDigest`** — a **deterministic** summary/prioritization over the
   store: total count, counts per app, and the most urgent/recent items first.
   No model call, so it is fully testable. It is the "prioritize" half of the
   feature; the model-driven "summarize/answer" half is a follow-up that can
   build on this digest as its input.

Permissions and audit follow the established pattern (ADR-0005): the
`Notifications` source is checked at **capture** (don't ingest what the user
didn't approve) and at **query/digest** (don't surface it later), each writing a
`permission.decision`, plus `notification.capture`/`notification.query` events
carrying **counts and timing only — never notification text** (consistent with
`cloud.request`/`context.*`).

## Consequences

- **Nothing observable ships yet**, by design: no real source is registered and
  no D-Bus surface is added, so with default-deny the feature is inert until the
  follow-ups land. It is verified entirely by tests, as the context-index
  foundation was (ADR-0009).
- **`Notifications` is now a live permission source** at the contract level;
  docs/security.md is updated to say so.
- **In-memory, capped, transient.** A restart loses captured notifications; that
  is acceptable for "what did I miss recently". Persistence, deduplication, and
  expiry-by-age are deferred behind `INotificationStore`.
- **Audit carries no notification content** — app names, summaries, and bodies
  can be sensitive, so only counts/timing are logged, matching the privacy
  stance of every other event type.
- Tests fake `INotificationSource` and use the in-memory store and a recording
  audit log; no session bus, no desktop (hard rule 3).
- **Deferred (own issues):** the real `org.freedesktop.Notifications` session-bus
  source; model-driven natural-language summarize/answer via the model router;
  and a D-Bus surface (`org.veya.Veya1`) to expose the digest to frontends. This
  ADR does not preclude any of them — each plugs into an interface defined here.
