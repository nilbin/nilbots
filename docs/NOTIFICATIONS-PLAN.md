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

### Firing on the broadcast boundary needs no new machinery

Nothing runs at the moment a broadcast completes. The match worker sets
`BroadcastStartedAt = CompletedAt + BroadcastDelaySeconds` and finishes; the broadcast
ends later, at `BroadcastStartedAt + EndTick / PresentationTicksPerSecond`, with no
process watching.

`BackgroundJob` already solves this and it was not obvious: it carries `AvailableAt`, and
`BackgroundJobLeaseStore` claims only rows where `AvailableAt <= now()`. So a job can be
*scheduled*. Enqueue an announce job in the same transaction that completes the match,
with `AvailableAt` set to the computed broadcast end. No sweeper, no timer, no polling
loop — and it inherits the retry and `FOR UPDATE SKIP LOCKED` safety every other job has.

Prefer that over a periodic sweeper looking for matches whose broadcast has elapsed: a
sweeper re-scans the table forever to catch an event that is known exactly when the match
completes.

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

1. ~~Payload discriminated union + client regeneration.~~ **DONE.** The union is the
   response contract only; storage stays concrete per kind and is read by `Kind`, so a
   new kind needs no migration. Adding one means a `[JsonDerivedType]` on
   `UserNotificationPayload`, a case in `ToResponse`, and
   `bash scripts/generate-api-clients.sh`.
2. `match-settled` / `set-settled` on the broadcast-complete boundary, with the set rule.
   Emit from an announce job scheduled via `AvailableAt` (above). Open question worth
   settling first: a ranked set's games each finish broadcasting at different times, so
   `set-settled` schedules from the *last* game's boundary — which the finalizer knows and
   an individual match worker does not.
3. Mobile SignalR + in-app toasts (needs the garage's auth session).
4. `match-challenged` and supersession.
5. Device registration, preferences, delivery records, and push from a durable job.

## Who gets told what

One rule generates most of the answers: **you are told about things other people did to
you, and about things that changed your standing.** Everything else you already know,
because you did it.

- **A challenge notifies only the challenged.** The challenger just pressed the button and
  is almost certainly looking at the screen; telling them is an echo, and an app that
  echoes your own actions is one you learn to ignore.
- **Ranked sets notify both players, win or lose.** Rating moved for both, so both have
  news.
- **Unranked results notify only the challenged.** No rating moved, so the only thing that
  makes it worth interrupting for is that someone else started it.

### Losses notify exactly like wins

Tempting to suppress them — the toast is meant to feel rewarding, and "your bot lost −25"
does not. Do it anyway.

An app that only reports good news stops being believed, and players work it out fast:
the rating on the ladder already tells them, so a silent loss reads as the app hiding
something rather than sparing them. The reward has to come from the notification being
*true*, not from it being selectively cheerful. The `−25` gets `Arena.live` and the same
prominence the `+25` gets.

## Push transport: Expo, behind the abstraction

Send through **Expo's push service** rather than straight to APNs/FCM. It removes the
certificate and key management, handles token rotation, and is one API for both
platforms — real setup cost avoided for a project this size, against a third party in the
delivery path and no per-message priority control.

That is affordable precisely because of the shape the plan already requires: device
registrations, per-channel delivery records, and a durable job doing the sending. Nothing
above the job knows which transport is underneath, so moving to direct APNs/FCM later
changes one job handler. Move when per-message priority or delivery telemetry starts
mattering, not before.

## The site gets result toasts too

Same durable records, so the site should render the new kinds rather than staying
entitlements-only. A player who uses both surfaces getting different news from each would
be a bug in every reading. `NotificationCenter` already exists and already acknowledges;
the new kinds are payloads it does not yet have a case for.

The suppression rule travels with it: the site must not toast a result for a match being
watched on screen either.
