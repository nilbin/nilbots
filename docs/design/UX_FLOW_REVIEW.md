# Web UX and flow review

Reviewed: 2026-07-28
Scope: routed React web app on `agent/design-overhaul-frontend`
Design reference: `climb.html`

This review follows player jobs across routes rather than scoring pages in isolation.
Backend observations are recorded where they constrain the experience, but this design
branch does not change backend code.

## Product jobs, in priority order

### 1. Improve a bot

**Intent:** write locally, submit a generation, learn whether it improved.

The CLI-first hand-off is coherent and the bot page gives generations, build detail,
statistics, match history, appearance, and the exact local commands. The largest missing
piece is still the question Climb puts first: rating by generation. The backend records a
current bot rating, not a generation rating history, so the chart must remain an honest
empty state.

Important follow-ups:

- project rating history or snapshots per generation;
- make build/admission failures stable, actionable problem codes;
- decide whether browser source submission remains a supported primary path or becomes a
  developer convenience beside the CLI-first path.

### 2. Play in the Arena

**Intent:** put a ready bot into a meaningful fight with minimal uncertainty.

This was the weakest top-level journey before this review:

- the Bots directory linked to a missing `#challenge` anchor;
- a direct visit to another player's bot disabled the owned-bot query it needed, so the
  challenge UI depended on a warmed cache;
- the action was hidden in the phone roster and buried below most bot content;
- ranked mode showed a disabled opponent and an enabled Map control even though the
  server chooses both;
- signed-out, no-build, no-owned-bot, no-opponent, and query-failure states often rendered
  nothing.

The frontend now treats play as one global, reusable action:

```text
global top bar / Bots / Garage / bot hero / result
                         |
                         v
              shared Arena composer
                    /             \
ranked set        challenge
6 games           1 game
matchmade         chosen opponent + map
rating moves      unranked
       \             /
                    \             /
                     set or match
                          |
                  watch / inspect / play again
```

The composer is a single native modal dialog owned at the app shell. Contextual launchers
pass a typed bot/opponent/map intent; they do not manufacture URLs or mount query and
mutation hooks in every roster row. The global trigger asks which ready owned bot should
play, while a public-bot trigger already knows the challenge target.

The remaining material blocker is allowance visibility. Ranked and unranked defaults are
server-configurable and no endpoint projects used, remaining, in-progress, or
next-available values. The UI therefore explains that admission is checked at start
without inventing a counter.

Backend work required after the active balance work lands:

1. Reject unranked self-challenges (`botId === opponentBotId`). A direct API caller can
   currently create one and later break history/statistics projections that expect one
   participant per bot.
2. Expose an authenticated Arena capability projection: authoritative playability,
   ranked/unranked usage and limits, current concurrency, next availability, and stable
   refusal codes.
3. Add idempotency to creation so a retry cannot consume allowance twice.
4. Keep settlement and unlock publication behind the same reveal boundary as match/set
   results.

### 3. Watch, understand, and retry

**Intent:** find a live or completed fight, understand the outcome, then choose what to do
next.

Watch is the strongest existing journey. Its filters live in the URL, empty states are
recoverable, and match/set detail makes the deterministic record inspectable.

This pass closes the main continuation gaps:

- Match and Set now return to Watch.
- Set standings link to both bots.
- Match records link to both bots even while a result is held or the fight failed.
- An owner can challenge again with the previous unranked opponent/map, or start another
  independently matchmade ranked set.
- Replay/detail/set query failures no longer masquerade as permanent loading.
- Result/unlock toasts stay queued rather than covering match playback on a phone.

Still worth doing:

- provide stable public-safe failure reasons and failure notifications;
- expose ranked-set progress as an explicit `n of 6` summary;
- add cursor pagination/live queries before match volume makes offset polling expensive;
- project complete historical rules, versions, seeds, and starting ratings rather than
  leaving typed presentation seams empty.

### 4. Customize and collect

**Intent:** understand what exists, what is owned, how something unlocks, and where to
equip it.

The correct split is:

- **Shop:** commercial catalog and account upgrades;
- **bot Appearance:** rich owned/locked preview and equipping;
- **Garage Unlocks:** only unowned items that can actually be earned.

A standalone Looks destination would duplicate the Shop and remove appearance from the
bot whose identity is changing. `/looks` therefore redirects to `/store`.

This review also fixes purchase-only and already-owned items being described as “to
earn,” makes Shop discoverable from secondary navigation, preserves exact Shop anchors
through sign-in, and gives the closed-Shop appearance copy a real Garage destination.

The Store response still has no price or checkout contract. The UI must continue to avoid
fabricated prices, Buy actions, or ownership states.

### 5. Join and return

**Intent:** sign in once and resume the task that required it.

Protected links now carry a local `returnUrl` through Garage, Appearance, Arena, and Shop.
The frontend mirrors the backend's single-slash sanitizer, so protocol-relative or
backslash-based URLs cannot become an authenticated open redirect.

The Docs quick start now matches the product's CLI-first first-bot journey and links to
the actual Garage and Watch destinations. The previous “browser only” instructions sent a
new account to a create form that the empty Garage intentionally does not render.

## Route closure

| Route | Primary ways in | Contextual ways out |
| --- | --- | --- |
| Season `/` | logo, primary nav | bot detail |
| Bots `/bots` | primary nav | bot detail, shared Arena composer |
| Garage `/garage` | account name, footer, auth return | bot detail, shared Arena composer, Shop |
| Bot `/bots/:key` | Season, Bots, Garage, matches | Bots, shared Arena composer, Appearance, matches, Watch |
| Appearance `/bots/:key/appearance` | owned bot | bot, exact Shop pack |
| Shop `/store` | footer, Garage, Appearance | Garage/sign-in |
| Watch `/watch` | primary nav, Arena/result continuation | Match/Set, Bots through filters/results |
| Match `/matches/:id` | Watch, bot history, notification, Set | Watch, Set, both bots, repeat through the shared composer |
| Set `/sets/:id` | ranked creation, Match, Watch, notification | Watch, all games, both bots, repeat ranked through the shared composer |
| Docs `/docs` | primary nav | sign-in, Garage, Watch |
| Login `/login` | protected action, header | sanitized originating task or Garage |

All async fragment destinations use one shared hash-scrolling behavior, so an Appearance
link to a Shop pack remains reliable even when the catalog arrives after route render.
The former review-only `/bots/:key/play` URL redirects to bot detail; it is not a second
product surface.

## Automatic Arena package

Automatic Arena is a quality-of-life purchase, not capacity. V1 schedules one daily
ranked set per configured owned bot:

- one occurrence is one six-game ranked set;
- it consumes the same rolling account allowance and concurrency as manual play;
- the server still chooses opponent, maps, seeds, rules, and active generation;
- it grants no extra games, schedule slots, concurrency, queue priority, or opponent
  choice;
- temporary admission failures defer only inside a bounded execution window; missed days
  never catch up by running twice later;
- time is stored with an IANA zone and one durable occurrence per local date.

Use a non-stackable account feature entitlement and a provider-supplied price. The current
permanent-grant/refund model is not safe for a recurring subscription, so the least
surprising first shape is a one-time license with kind-aware revocation of future
automation on refund.

The full frontend states, future API proposal, quota/failure policy, DST rules, rollout,
and acceptance criteria are in [Automatic Arena](AUTO_ARENA.md). It is deliberately a
contract, not a production Shop fixture.

## Validation

The review build uses schema-checked fixtures isolated from the production bundle.
Automated coverage checks wide and phone layouts, horizontal overflow, API escapes,
console/page errors, and closure of every rendered internal link. Mutation endpoints are
not faked, so design review cannot be mistaken for a working backend.
