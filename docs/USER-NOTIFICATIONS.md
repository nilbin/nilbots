# User notifications

Entitlement unlocks are the first notification kind. The notification system is
deliberately split into durable product truth and replaceable delivery channels:

```text
source event (build, challenge, ranked achievement)
  -> entitlement ledger grant
  -> durable UserNotification in the same PostgreSQL transaction
  -> PostgreSQL NOTIFY after commit
  -> every web node forwards to its local SignalR clients
  -> browser presents and acknowledges the notification
```

The web app also loads unread notifications on sign-in/startup and polls as a
fallback. SignalR therefore controls latency, not correctness: closing the app,
losing a connection, or changing devices cannot lose an unlock.

## Entitlement semantics

- Notification creation happens only when the account transitions from not
  owning a catalog item to owning it.
- Multiple grant sources may still be recorded for one item, but a redundant
  source does not pretend that the item was unlocked again.
- One source event that grants several items creates one notification containing
  all newly owned items. The 100-ranked-match chassis/projectile pair is the
  initial example.
- A PostgreSQL lock on the account serializes concurrent entitlement transitions.
- Grant, notification, and `pg_notify` participate in one transaction. PostgreSQL
  emits the wake-up only after commit.
- Natural grant uniqueness plus the notification dedupe key make worker retries
  idempotent.

## Public contract

`GET /api/notifications` returns only the authenticated account's unread inbox,
oldest first within the latest page. `POST /api/notifications/{id}/read` is
idempotent and returns no information about another account's notification IDs.

`/hubs/notifications` is an authenticated SignalR hub. It emits the same named
notification response as the inbox through the `notification` client method.

The initial `entitlement-earned` payload contains:

- source kind and source ID;
- accomplishment copy suitable for a delivery channel;
- stable catalog key, kind, ID, and label for every unlocked item.

Rendering stays client-owned. The web client resolves the stable IDs to its
chassis/projectile assets; the backend does not store image URLs.

## Later channels

Mobile push, email, or an in-app notification history should consume the same
durable notification, not be called from achievement code. Before adding a
channel that needs retries or delivery receipts, add a per-notification,
per-channel delivery record/outbox and user preference policy. Do not overload
`ReadAt`: it means the user acknowledged the in-app notification, not that every
external channel delivered it.

PostgreSQL `NOTIFY` is appropriate for the current web realtime bridge because
all web nodes receive it and each node owns its local SignalR connections. It is
not durable and is never treated as such; the `UserNotifications` table is the
recovery path.
