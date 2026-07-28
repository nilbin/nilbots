# Automatic Arena

Status: proposed frontend and product contract

This document specifies the intended Automatic Arena experience. It is not an
implementation plan, an assertion that the API already exists, or permission to
hard-code a Shop product into the web app.

Automatic Arena is a purchasable quality-of-life feature that lets a player
schedule one daily ranked set for an owned bot. A ranked set currently consists
of six games: three map-and-seed pairs played from mirrored starting slots.
Automatic play must enter exactly the same admission, matchmaking, quota, and
execution paths as a manually started ranked set.

## Product principles

- The purchase buys convenience, not competitive priority.
- A scheduled set consumes the account's existing rolling ranked-set allowance.
- It does not add ranked sets, raise concurrency, bypass system capacity, or move
  work ahead of another player's work.
- The server selects the opponent, maps, seeds, current rules, and open ladder in
  the same way it does for manual ranked play.
- The bot's active eligible version at execution time is used. Scheduling does
  not pin a version indefinitely.
- Every state shown in the UI must come from a real capability, entitlement,
  schedule, or run response. A local mock may support isolated development, but
  it must not masquerade as a live Shop purchase or working schedule.

The feature belongs primarily on the bot page. The Shop explains and sells the
entitlement, while the bot page is where a player configures and understands the
schedule. It is not part of Looks, and should not compete with the bot skin
picker or Shop appearance browsing.

The shared global Arena control may expose **Automatic Arena** after a player
selects an owned bot, but only when the real capability response exists. The
contextual control beside an owned bot may open the same schedule panel directly.
Neither launcher owns separate schedule state, and neither should synthesize a
locked package before the Store and capability APIs supply it.

## Terminology

- **Schedule**: the player's recurring local-time preference for one bot.
- **Occurrence**: one local calendar day's attempt to start the scheduled set.
- **Ranked set**: six games whose result is settled together.
- **Execution window**: the bounded period after the selected time in which a
  temporarily blocked occurrence may still start.
- **Needs attention**: a schedule that cannot safely continue without a player
  action, such as selecting an eligible bot build or restoring entitlement.

The interface should say “daily ranked set” and “6 games” rather than the
ambiguous “daily game.”

## User journey

### 1. Discovery in the Shop

The Shop may contain an Automation shelf with an Automatic Arena package. Its
card explains:

- what is unlocked;
- that it schedules one ranked set per configured bot;
- that the set still uses the player's ranked allowance;
- that it gives no queue or matchmaking advantage;
- whether the account already owns it; and
- a provider-supplied price and purchase action when purchasing is available.

If the Store API says the store is closed or the package is unavailable, the UI
must not synthesize a Buy button, price, ownership state, or successful checkout.
It may show a clearly labelled unavailable or coming-later presentation when
product policy asks for it.

### 2. Entry from a bot

The bot page contains an **Automatic Arena** panel near ranked activity and the
primary Play action.

When the feature is not owned, the panel shows a short explanation and a Shop
link. It may preview what scheduling does, but its controls remain disabled and
must not imply that a schedule has been saved.

When owned, the first-use state offers **Set up daily play**. If the server says
the account has reached its active-schedule ceiling, the panel explains that
limit and links to the account-level schedule list; there is no upsell for more
schedule slots.

### 3. Configure

The configuration form contains:

- local start time, at minute precision;
- time zone, defaulted as a suggestion from the device but explicitly saved;
- a summary: “One ranked set · 6 games · starts around HH:mm”;
- the current rolling ranked quota and concurrency limit when supplied by the
  server; and
- a concise explanation that quota or queue pressure can delay or skip a day.

The confirmation action is **Enable daily play** for a new schedule and
**Save schedule** for an existing one. Saving returns and renders the canonical
server response, including the calculated next run. The client must not
calculate and persist its own authoritative next occurrence.

### 4. Understand and control

An active panel prominently shows:

- status;
- bot;
- local time and time zone;
- next attempt in local-friendly and absolute form;
- last occurrence outcome;
- a link to the queued or completed ranked set when one exists; and
- Pause and Edit actions.

Pause stops future, not-yet-admitted occurrences. Resume schedules the next
future occurrence and never catches up missed days. Removing the schedule is a
separate, confirmed action. It does not cancel a ranked set that has already
been admitted.

The account-level view lists all schedules so a player does not have to visit
bots individually to find active automation. Bot detail remains the primary
editing surface.

### 5. Outcomes and recovery

Routine transient delays should not create notification spam. The panel and run
history show them directly. A notification is appropriate only when a schedule
enters a durable Needs attention state or after repeated failures, and should be
deduplicated by schedule and reason.

Needs-attention presentation must state both the problem and the next action:

| Reason | Player-facing treatment |
| --- | --- |
| `bot_unavailable` | Ask the player to activate a successful compatible build. |
| `appearance_locked` | Ask the player to select an appearance they may use. |
| `entitlement_missing` | Explain that automatic play is paused and link to the Shop or account purchase history. |
| `feature_disabled` | Explain that Automatic Arena is temporarily unavailable; do not suggest repeated retries. |

Restoring an entitlement or fixing a bot must not silently resume a paid
automation schedule. The player confirms Resume so that future play remains
opt-in.

## Shop entitlement semantics

The proposed entitlement is:

```text
feature:automatic-play
```

The proposed package identifier is:

```text
automatic-play
```

It is account-wide, non-stackable, and non-repeatable. One schedule may exist
per owned bot, subject to a fixed server-operated active-schedule ceiling. That
ceiling protects the scheduling system and is not another product.

The entitlement grants scheduling only. It must not bundle or imply:

- additional daily ranked sets;
- additional concurrent ranked sets;
- faster queue service;
- preferred opponents;
- extra bots; or
- guaranteed execution at an exact wall-clock minute.

The current codebase is shaped around permanent grants, so the least surprising
first commercial form is a one-time license. A subscription requires explicit
expiry, renewal, cancellation, grace-period, and provider webhook semantics
before it can be shown or sold.

Prices and currencies are payment-provider data. They must not be embedded in
this document, the entitlement catalog, a React component, or a frontend mock.
The future Shop response should provide a localized display price and purchase
availability.

A refund may leave historical cosmetic snapshots alone, but it must revoke
future Automatic Arena access when no other active source grants the feature.
Existing matches remain valid. Affected schedules enter Needs attention rather
than continuing to enqueue paid work.

## Scheduling behavior

### Time zones and daylight saving time

- Save an IANA time zone such as `Europe/Stockholm`, not a numeric UTC offset.
- The browser may suggest its current time zone, but the player sees and chooses
  the value that is saved.
- Each occurrence is keyed by the schedule and its local calendar date. There
  can be at most one occurrence for that bot on that local date.
- If the selected time does not exist during a spring-forward transition, use
  the first valid instant after the gap.
- If the selected time occurs twice during a fall-back transition, choose one
  documented occurrence deterministically and run only once.
- Changing time or time zone supersedes pending stale work. It must not create a
  second occurrence for a date that has already started or completed.
- Resuming after a pause uses the next future occurrence. Missed dates are not
  replayed.

The UI should say “starts around” rather than “starts at.” The saved time is a
not-before preference; shared queue conditions determine actual execution.

### Quota, queue, and failure policy

Manual and automatic starts must be serialized through the same account
admission rule. HTTP rate limits are burst protection and are not a substitute
for durable quota enforcement.

| Condition at the scheduled time | Occurrence behavior |
| --- | --- |
| Ranked concurrency is full | Defer within the execution window. |
| Rolling 24-hour allowance is full | Defer until the next known quota opening when it falls inside the execution window; otherwise skip. |
| No eligible opponent is currently available | Retry within the execution window. |
| Shared system capacity is constrained | Defer without gaining priority over manual work. |
| Bot or appearance is ineligible | Stop retrying and mark the schedule Needs attention. |
| Entitlement is absent or revoked | Stop retrying and mark the schedule Needs attention. |
| Schedule was edited, paused, or removed | Treat stale scheduled work as a successful no-op. |
| A set was admitted and later failed | Count it exactly as a manual set; do not create a replacement. |
| The execution window expires | Mark the occurrence skipped and continue with the next local day. |

A six-hour execution window is a reasonable initial default, capped before the
next local occurrence. Its final value is server policy and should be exposed as
capability data if the frontend needs to explain it.

Once admitted, all six games join the normal match queue. Automatic sets receive
the normal ranked-result experience and link to the existing set detail. The
schedule history should reference that set rather than duplicate its scores,
ratings, or replay data.

### Run statuses

The frontend should be prepared to render:

- `scheduled`: accepted for a future attempt;
- `deferred`: temporarily blocked, with `retryAt` when known;
- `queued`: a ranked set was created, with `matchSetId`;
- `skipped`: the occurrence ended without a set, with a public-safe reason;
- `superseded`: a stale occurrence was neutralized by a schedule change; and
- `failed`: the scheduling operation itself failed unexpectedly.

Raw exceptions, worker details, provider references, and internal queue
information are never public reason text.

## Proposed future API

These routes describe the frontend's desired contract. They do not exist merely
because they are written here.

### Overview

```http
GET /api/automatic-play
```

```ts
type AutomaticPlayOverviewResponse = {
  capability: {
    enabled: boolean;
    unavailableReason: string | null;
    entitlementKey: "feature:automatic-play";
    owned: boolean;
    purchasable: boolean;
    maxActiveSchedules: number;
    activeScheduleCount: number;
    executionWindowMinutes: number;
    rankedSetsStartedLast24Hours: number;
    rankedSetLimit24Hours: number;
    rankedSetsInProgress: number;
    rankedSetConcurrencyLimit: number;
  };
  schedules: AutomaticPlayScheduleResponse[];
};
```

The server remains authoritative for every capability and count.

### Create or update a bot schedule

```http
PUT /api/bots/{botId}/automatic-play
```

```ts
type AutomaticPlaySettingsRequest = {
  localTime: string;       // "HH:mm"
  timeZoneId: string;      // IANA identifier
  expectedRevision: number | null;
};

type AutomaticPlayScheduleResponse = {
  id: string;
  botId: string;
  status: "active" | "paused_by_user" | "needs_attention";
  localTime: string;
  timeZoneId: string;
  nextRunAt: string | null;
  lastRun: AutomaticPlayRunSummary | null;
  needsAttentionReason: AutomaticPlayReason | null;
  revision: number;
};
```

`expectedRevision` protects edits made concurrently on multiple devices. A
conflict returns the latest canonical schedule for the client to present and
refresh.

### Control and history

```http
POST   /api/bots/{botId}/automatic-play/pause
POST   /api/bots/{botId}/automatic-play/resume
DELETE /api/bots/{botId}/automatic-play
GET    /api/bots/{botId}/automatic-play/runs?take=30&before={cursor}
```

Pause, resume, and delete accept an expected revision and return the canonical
schedule state. Delete is a soft cancellation from the product's perspective.

```ts
type AutomaticPlayRunSummary = {
  id: string;
  occurrenceDate: string;  // local YYYY-MM-DD
  scheduledFor: string;    // UTC instant
  attemptedAt: string | null;
  retryAt: string | null;
  status:
    | "scheduled"
    | "deferred"
    | "queued"
    | "skipped"
    | "superseded"
    | "failed";
  reason: AutomaticPlayReason | null;
  matchSetId: string | null;
};

type AutomaticPlayReason =
  | "account_daily_limit"
  | "account_concurrency_limit"
  | "bot_unavailable"
  | "appearance_locked"
  | "no_opponent"
  | "entitlement_missing"
  | "feature_disabled"
  | "system_capacity"
  | "execution_window_expired";
```

There is deliberately no automatic-play endpoint for opponent, map, seed,
rules, arbitrary cron expressions, or “run now.”

### Shop contract

The future Store response should identify the package and feature item through
normal catalog data. If commerce is available, it should also supply
provider-derived purchase information, for example:

```ts
type StorePurchaseOffer = {
  available: boolean;
  displayPrice: string | null;
  currency: string | null;
  amountMinor: number | null;
  purchaseKind: "one_time" | "subscription";
};
```

The frontend does not infer `owned`, `repeatable`, price, or checkout
availability from the package ID.

## Visual and interaction requirements

- Follow the shared Climb-derived design tokens and component system.
- Build the panel from reusable form, status, callout, and action components;
  do not introduce page-specific inline styling.
- Design mobile-first: the complete schedule, status, and recovery action must
  be understandable without hover.
- Use native or accessible time controls with an explicit text fallback.
- Every status has text in addition to color.
- Touch targets, focus order, error association, and destructive confirmation
  meet the app's accessibility conventions.
- Loading preserves panel geometry where practical; it must not flash an
  unowned state before entitlement data arrives.
- Optimistic UI may show a saving state, but ownership, next run, and schedule
  status change only after the server confirms them.
- Dates should be readable locally while retaining an accessible exact
  date-time for ambiguity.

## Rollout

### Phase 0: design and fixtures

- Build visual states only in an explicitly isolated design/demo environment.
- Label fixtures as fixtures and keep them out of production Shop ownership and
  scheduling flows.
- Validate locked, first-use, active, paused, deferred, skipped, and
  needs-attention states on phone-sized screens.

### Phase 1: capability and schedule API

- Ship behind a server capability flag.
- Integrate generated clients from the real API schema.
- Exercise schedules using manually granted test entitlements.
- Keep checkout unavailable.

### Phase 2: limited account rollout

- Enable for a small cohort.
- Measure due occurrences, admitted sets, deferrals, skips by reason, scheduler
  lag, duplicate no-ops, and match queue latency.
- Review opponent repetition and queue impact using the same fairness measures
  as manual ranked play.

### Phase 3: commerce

- Add provider-backed price and checkout only after refund and entitlement
  revocation behavior is tested.
- Confirm product copy and platform-purchase requirements independently for web
  and mobile.
- Expand gradually with an operator kill switch and a documented support path.

## Acceptance criteria

- A player can discover the package in the Shop and configure it from an owned
  bot without visiting Looks.
- Locked, unavailable, purchased, and active states are based on server data,
  not component constants.
- The UI always describes the action as one ranked set containing six games.
- One bot can have at most one schedule and one occurrence per local date.
- Manual and automatic starts share quotas, concurrency, matchmaking, ladder,
  and queue behavior.
- Buying Automatic Arena does not increase any limit or priority.
- Pause, resume, edit, stale-request conflict, cancellation, refund, and lost
  entitlement all have explicit, tested frontend states.
- Spring-forward and fall-back behavior is deterministic and covered by
  contract tests.
- A queued occurrence links to the canonical ranked-set view.
- Skipped occurrences do not silently retry on later days or create catch-up
  sets.
- No public response contains raw job errors, payment references, or sensitive
  account information.
- Web, mobile, and CLI clients are generated from the eventual API source of
  truth rather than maintaining hand-written divergent response types.
- Production does not show a fabricated price, Buy action, ownership state, or
  successful schedule when the corresponding backend capability is absent.
- The implementation uses shared design tokens/components with little to no
  inline styling and remains usable from a phone.

## Explicit non-goals

- Implementing backend entities, jobs, migrations, admission, or billing in
  this design specification.
- Making a bot itself play differently or adding an “AI autoplay” mode.
- Scheduling individual games instead of ranked sets.
- More than one automatic occurrence per bot per local day.
- Arbitrary cron or interval scheduling.
- Pinning bot versions, opponents, maps, seeds, playlists, rules, or ladders.
- Queue priority, reserved workers, quota bypasses, or paid concurrency.
- Automatically replacing failed sets.
- Cancelling a set after it has entered ranked admission.
- Treating Automatic Arena as a cosmetic, a Looks-page feature, or a skin-picker
  responsibility.
- Hard-coding an Automatic Arena Shop card, entitlement, price, or ownership
  state into the production frontend.
