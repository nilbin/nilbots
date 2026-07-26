# Notifications plan

How nilbots tells a player something happened, across the site, the mobile app, and
push. Incremental — the durable half already exists and ships entitlements today.

Read [`DECISIONS.md`](DECISIONS.md) #108 first; it is the standing architecture and this
document does not replace it. In short: **a notification is a durable product record.
SignalR is a delivery channel, not the inbox.**

## What exists

- `UserNotification` rows, written in the same PostgreSQL transaction as the thing they
  announce, with natural dedupe keys so worker retries are silent.
- PostgreSQL `NOTIFY` after commit wakes every web process
  (`PostgresNotificationListener`); each forwards to its own connected authenticated
  SignalR clients (`UserNotificationsHub`). This is what makes delivery scale across web
  nodes without a broker — new nodes need no wiring.
- `GET /api/notifications` for unread-on-startup and as a poll-based recovery path, so an
  offline user, a reconnect, or a dropped transient event cannot lose the event.
- One kind: `entitlement-earned`. The site renders it as a toast
  (`web/src/site/components/NotificationCenter.tsx`) and acknowledges it explicitly or
  after visible presentation.

So the fan-out, durability and recovery story is already built and already scales. What
follows is mostly *kinds*, *presentation*, and *one blocker*.

## The blocker: the payload is not yet a union

`UserNotificationResponse.Payload` is typed as `EntitlementEarnedPayload` — deliberately,
so it reaches the OpenAPI document and every generated client rather than degrading to
`unknown` in TypeScript and a colliding `JsonElement` class in C#.

That types exactly one kind. Adding a second **compiles perfectly well** and would
deserialize into an all-default `EntitlementEarnedPayload`, serving plausible empty data
to every client. `UserNotificationContracts.ToResponse` therefore throws
`NotSupportedException` on an unrecognized kind — that throw is the guard rail, and it is
the first thing any new kind hits.

**Do this first:** make `Payload` a discriminated union — `[JsonPolymorphic]` on a base
payload type keyed by `Kind` — then regenerate clients
(`bash scripts/generate-api-clients.sh`). Do not widen it back to `JsonElement`. Every
kind below depends on this and nothing else in the plan is safe until it lands.

## Kinds

| Kind | Fires when | Tap target |
|---|---|---|
| `entitlement-earned` | *(exists)* a grant commits | the cosmetic |
| `match-challenged` | someone creates a match against your bot | watch it live |
| `match-settled` | a match your bot fought finishes **broadcasting** | the result |
| `set-settled` | a ranked set your bot fought is revealed | the set, with rating delta |

### Broadcast secrecy is the hard constraint here

A result notification must be written when the match **finishes broadcasting**, not when
it completes. `Match.BroadcastComplete(now)` is the gate everywhere else in this codebase
and a notification is not exempt: emitting on completion would push "your bot won" to a
phone while the replay is still playing out for everyone watching, which is precisely
what broadcast secrecy exists to prevent. The same applies to a set — `Revealed`, not
`Completed`.

This is the single easiest thing to get wrong in this feature.

### One set is one notification

A ranked set is six games. Six `match-settled` records for one set is spam, and it leaks
the set's shape game by game. A set emits **one** `set-settled` when revealed; its games
emit nothing. Unranked matches emit `match-settled` individually because there is no
containing unit.

### Supersession: a challenge becomes its result

The interesting case. You are told "Pincer challenged hunter — watch". You do not look.
By the time you do, the fight is over, and "watch" is a lie.

Keep **one record per subject** (the match or set), not two, with the subject id as the
dedupe key. On settlement the same row's payload is rewritten from `challenged` to
`settled` and re-announced, rather than appending a second row:

- the inbox never accumulates a stale "watch this" beside its own result;
- the dedupe key stays natural, so retries remain silent (#108);
- `ReadAt` clears on the rewrite, because an outcome is genuinely new information — a
  player who read the challenge has not read the result.

A record that is still `challenged` when opened resolves live: if the match has since
settled, the client shows the result rather than a dead "watch" button, and the corrected
row arrives moments later anyway.

## Mobile delivery

Two channels, one record. Neither is invoked by achievement or match code — both consume
the durable notification, per #108.

**In-app (foreground): SignalR.** The same hub the site uses. The app already holds an
authenticated session for the garage; the hub connection follows it. On resume, the app
reloads unread from `GET /api/notifications` — same recovery path as the site, so a
backgrounded app that missed a transient event catches up.

**Background: push.** Needs work that does not exist yet:

- a device registration table (account, platform, token, last seen) and an endpoint to
  register/refresh — Expo rotates tokens;
- per-channel delivery records and preferences, so "seen in-app" suppresses a redundant
  push and a player can turn result pushes off without losing entitlements;
- delivery from a **durable job**, not inline in the request that created the
  notification. APNs/FCM are network calls that fail, retry, and rate-limit; putting them
  in the transaction that finalizes a set would make match settlement depend on Apple
  being up. `BackgroundJobs` with `FOR UPDATE SKIP LOCKED` is already the pattern.

`expo-notifications` is the client library. Decide early whether to send via Expo's push
service or straight to APNs/FCM; the former is far less setup, the latter avoids a third
party in the delivery path.

## In-app presentation

Push can be plain. **In-app must not be** — this is the moment the game pays the player
back, and a system-style banner squanders it.

**Entitlement toast.** The reward, large, with its catalog art — stable catalog IDs, not
image URLs (#108), so the client renders it from its own assets. Celebratory: it should
feel like the game noticed. Tap opens the cosmetic; it acknowledges on tap or after
visible presentation, matching the site.

**Result toast.** The one that has to land: *my bot won and gained 25 rating.* Bot sprite,
outcome in the app's existing result colours, and the rating delta as the headline —
`+25` in `Arena.ok`, `−25` in `Arena.live`. `BotRecord` already fixes what those colours
mean, and `OutcomeText` already decides that a broadcasting match reads LIVE; a toast
reuses both rather than inventing a third vocabulary.

**Challenge toast.** Terse and actionable — who, which bot, and a watch affordance that
opens the arena viewer directly. This is the one with a deadline; it is worth interrupting
for precisely because tapping it leads somewhere live.

**Queueing.** Entitlements arrive in bursts — a set can settle and grant at once. One
toast at a time, queued, newest last, with the inbox as the overflow. Never stack three
over the arena.

**Never over playback.** A toast that covers the canvas mid-replay, or that spoils a
result for a match the player is currently watching, is worse than no toast. Suppress
result toasts for the match on screen and let the viewer's own ending deliver it.

## Order of work

1. Payload discriminated union + client regeneration. Nothing else is safe first.
2. `match-settled` / `set-settled` on the broadcast-complete boundary, with the set rule.
3. Mobile SignalR + in-app toasts (needs the garage's auth session).
4. `match-challenged` and supersession.
5. Device registration, preferences, delivery records, and push from a durable job.

## Open questions

- Does a challenge notification go to both players, or only the challenged one?
- Should an unranked loss notify at all, or only ranked results and wins? Notifying every
  outcome is the fastest way to teach players to mute the app.
- Expo push service or direct APNs/FCM.
- Whether the site grows the same result toasts, or keeps entitlements only.
