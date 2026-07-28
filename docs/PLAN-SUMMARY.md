# nilbots — plan summary and status

Condensed from the full product/implementation plan (drafted with Sol, 2026-07).
This file exists so a fresh working session can continue without the original
document. `docs/DECISIONS.md` records where open choices were pinned.
[`DOCUMENTATION.md`](DOCUMENTATION.md) classifies shipped contracts, active
plans, and historical evidence.

## Product

A programming game: developers write autonomous bots in C#, compile them to
WASM, battle them locally with `botarena play`, watch matches in a polished
top-down viewer, inspect exactly what their bot saw and decided, submit
immutable versions, and compete. The validation question: **is the
edit → build → watch → understand → improve loop enjoyable?**

## Non-negotiable architecture

- **Modular monolith**: one ASP.NET Core app, one PostgreSQL DB, embedded
  OpenIddict + SignalR, in-process module communication, DB-backed jobs.
  No microservices/brokers/K8s. Compilation & bot execution may cross process
  boundaries for *isolation*, not as services.
- **WASM is the canonical runtime** locally and server-side; in-process
  execution is diagnostic only. Local play enforces official limits.
- **Server rebuilds submitted source; the server artifact is canonical.**
  Submissions are hostile; analyzers are DX, never a security boundary.
- **Determinism is a product feature**: same versions + artifacts + map + seed
  ⇒ identical actions, events, result, replay hash. Integer-only simulation.
- Four version axes: SDK / runtime protocol / runtime configuration / game
  rules. Bot versions and appearances are immutable; history never changes.
- Ranked play uses mirrored deterministic match sets, not single matches.

## Game rules (0.5 current; 0.1 text below is the initial baseline)

Shipped evolution — every step data-driven via the balance harness
(GAME-DESIGN, DECISIONS #47/#49/#53/#75): 0.2 seed-spawn variation; 0.3 shot
range 8 + lane-safe spawns; 0.4 zone control (exclusive accrual, Domination
at 150 zone-ticks, zone→health→damage tiebreak, zone-distance-fair spawns);
0.5 cone vision + redacted hearing + territorial shared pressure + ordered
speed-two projectiles + private immutable programmed skill shots. Elo is
per-ruleset (one ladder per rules version, DECISIONS #54).

Official 0.5 is the frozen territorial-v8 candidate. Any action scores for the
sole active zone occupant; a contested or empty zone decays existing pressure.
±100 dominates, with a short ±10 / gain-2 pressure overtime from tick 200.
Projectiles launch one tile and then traverse two ordered substeps per tick,
checking every intermediate wall, strict corner, range boundary, and bot.
Future programmed bends remain private while current eight-way heading is
observable.

The four-doctrine all-WASM holdout had 1.9% draws, 100% damage and active-world
incidence, 78.7% reciprocal damage, zero stalled/looped games, and passed its
outcome-blind viewer review. Pincer led at 45-9-0 (42.5% of decided wins).
That exceeded the original pre-registered 35% ceiling, so decision #74 remains
a recorded failure. The product owner then accepted 42.5% as a healthy
champion share, set 45% as the future ceiling, promoted the unchanged rules,
and crowned Pincer gen-10 (decision #75). Detailed player rules:
`docs/PLAYER-GUIDE.md`.

## Game rules 0.1 (initial)

24×18 (prod) or 12×8 (slice) tile arena, 2 bots, 4 facings, 5 actions
(Wait/MoveForward/TurnLeft/TurnRight/Shoot), 3 HP, shot = instant ray, 1 dmg,
cooldown 2, vision range 6 with wall-blocking LOS, max 500 ticks, simultaneous
decisions from pre-tick state, resolution order: observe → execute → validate →
rotate → move → shoot → damage → cooldowns → faults → completion → record.
Collisions: same-target both fail, swaps fail, invalid → Wait. Win: survival,
then health, then damage dealt, else draw. Faults: failed tick = Wait,
3 faults = disqualified.

## Phase status

| Phase | Deliverable | Status |
| --- | --- | --- |
| 0A engine proof | Pure deterministic engine + replay + tests | **DONE** (55 tests) |
| 0B presentation proof | Convincing match page | **DONE (lite)** — React canvas viewer; logotype/design pass still to come |
| 0C WASM proof | Two WASM bots through official engine + limits | **DONE** (7 contract tests) |
| 1 local DX | SDK, template, CLI build/watch/doctor/login/submit, build cache | **DONE (pilot)** — remaining: NuGet/template packaging, analyzers |
| 2 monolith | ASP.NET Core + Postgres + accounts/bots/matches modules | **DONE (pilot)** — cookie auth; OpenIddict/PKCE deferred |
| 3 submissions | Server builds, validation, immutable versions | **DONE (pilot)** — controlled build + smoke test; process isolation & sprites deferred |
| 4 public matches | Server execution, live broadcast, match pages | **DONE (pilot)** — synchronized presentation clock over polling; SignalR transport later |
| 5 competitive | Ranked match sets, ratings, leaderboard | **DONE (pilot)** — 6-game mirrored sets, per-ruleset Elo ladders (#54), leaderboard |
| 6 competitions | Seasons/tournaments | later |
| 7 browser dev | In-browser editor on the same pipeline | later |

## What exists in this repo (2026-07-27)

- `src/BotArena.Engine` — pure engine: rules, maps, FOV, RNG, tick resolution,
  replay + SHA-256 hash, plus the experimental multi-life
  `FrontlineMatchSession`, canonical actor observation/runtime seam, and
  internal observation-complete replay v2. No web/DB/WASM dependencies.
- `src/BotArena.Sdk` — engine-independent developer API: the shipped
  `IBot`/`BotContext` duel contract plus the internal typed
  `IActorBot`/`ActorContext` Frontline contract and shared actor wire codec.
- `src/BotArena.Bots.BuiltIn` — Idle/Wander/Hunter/Coward plus the internal
  Frontline actor probe, against SDK contracts only.
- `src/BotArena.Runtime` — in-process duel and actor runtimes (diagnostic).
- `src/BotArena.Runtime.Wasm` — canonical runtime: Wasmtime 44 host, fuel +
  memory/table/epoch limits and trap isolation; shipped duel protocol 0.1 plus
  internal actor protocol/configuration 1.0 with one compiled artifact Module
  and isolated Store/Instance per life.
- `src/BotArena.WasmGuest` — guest program (built-ins as WASM;
  `scripts/build-wasm-guest.sh` → `artifacts/wasm/builtin-bots.wasm`).
- `src/BotArena.Guest` — reusable legacy and actor guest loops/protocol
  adapters.
- `src/BotArena.Cli` — `new` / `build` (cached) / `play` / `watch` / `replay` /
  `verify` / `doctor` / `cache` / `bots` / `maps`.
- `templates/botarena-bot` — the player project template.
- `src/BotArena.App` — the monolith: cookie/OpenIddict auth,
  bots/submissions/challenges, PostgreSQL-backed compile and match jobs,
  stable artifact/replay object keys, and the SPA. Explicit web, compile,
  match, and migration roles share the same model without becoming services.
- `deploy/` — the scale-ready multi-VPS baseline: primary Caddy/stateful
  services, inventory-driven web/compile workers, role-separated Docker
  Compose, commit-tagged images, provisioned certificates, a one-shot
  migration, private Garage S3 storage at replication factor 3, and
  repository-free versioned deployment bundles plus backup/deploy runbooks.
  Garage replicas remain co-located until additional physical failure domains
  justify a private multi-zone layout. Primary-only PgBouncer provides bounded
  transaction and notification-session pools, PostgreSQL loads
  `pg_stat_statements`, and nightly local dumps are restored weekly into a
  disposable database. Off-site recovery is deliberately deferred; follow
  [`POSTGRESQL-OPERATIONS-PLAN.md`](POSTGRESQL-OPERATIONS-PLAN.md).
- `web/` — one React build, two modes: the nilbots site (router) and the
  standalone replay viewer the CLI embeds replays into. One normalized model
  preserves replay v1 and presents local experimental Frontline replay v2
  through the default Canvas2D renderer or an optional lazy WebGL 2.5D
  renderer. The CLI artifact excludes Three.js.
- `tests/` — engine, determinism, WASM contract, Frontline lifecycle/combat,
  and replay-viewer suites, including DocDrift tests that pin mechanical
  docs/mirrors to the engine.
- `scripts/` — setup.sh (fresh container → working), setup-wasi-sdk.sh,
  build-wasm-guest.sh, test.sh, play.sh, dev-viewer.sh, e2e.sh, plus the
  balance/dynamics/control/arc/replay-review evaluation tools.

## Active experimental program (2026-07-28)

Frontline is now the active successor experiment; official rules 0.5 remains
the current game and ladder. The deliberately small game hypothesis is a
moving five-position frontline, Prime respawns, two fixed-time fabrication
unlocks, and one child-to-turret transformation. The target architecture runs
each submitted policy as independent same-artifact instances, with exact
team/participant/unit/life topology and the complete effective rules exposed
as deterministic public inputs. This keeps future player counts, maps,
seasons, and forms representable without fixing bots or ML models to today's
body count.

Packages 0–7 and the local Package 8 authoring/measurement slice of
[`FRONTLINE-IMPLEMENTATION-PLAN.md`](FRONTLINE-IMPLEMENTATION-PLAN.md) are
implemented on the experimental path. The historical shield and
public fingerprints, explicit team/participant/unit/life topology,
map-format-2 definition, objective kernel, independently instantiated
same-artifact runtimes, canonical team observations, replication/fabrication,
per-life Anchor/turret forms, strict replay v2, engine-independent actor
SDK/Guest types, actor protocol/configuration 1.0, and canonical isolated WASM
life instances are executable and tested. `nilbots experiment frontline`
adds local actor built-in/project/WASM play, replay-v2 output and viewer,
four deterministic calibration doctrines, and a separate descriptive
replay-v2 evaluator/blind-sampling path.

`PrepareTick()` freezes exact life-qualified actor keys and observations before
any runtime acts. `StepActors()` resolves the keyed joint action, including
Prime respawn, child rebuild/refabrication, persistent old-life projectiles,
post-damage objective control, form-transition start/change/cancel, and
absolute eight-way turret fire. Replay v2 snapshots those exact observations,
decisions, masks, lifecycle facts, post-state, and terminal stable-unit rows.
The web viewer and mobile bridge consume the same version-neutral normalized
model and visualize the five-position objective, lifecycle, Anchor windup, and
turret state. Canvas2D remains the default; the optional WebGL 2.5D renderer
shares those derivations, loads lazily, and still requires manual GPU/mobile
QA.

This is still not a shipped gameplay path. Historical `play`, App/server
eligibility/admission, general replay-v2 summary/verification,
dataset/corpus/model tooling, independently authored product evaluation,
rollout, and every ladder remain Package 8 or replay-native follow-ons.
Official rules 0.5, protocol/configuration 0.1, replay v1, and their hashes
remain unchanged. The frozen experimental contract is
[`EXPERIMENTAL-FRONTLINE.md`](EXPERIMENTAL-FRONTLINE.md); the shared ML/data
path remains
[`REPLAY-NATIVE-ML-PLAN.md`](REPLAY-NATIVE-ML-PLAN.md).

The next architecture wave is
[`GAME-MODE-ARCHITECTURE.md`](GAME-MODE-ARCHITECTURE.md). It introduces new
contract generations for typed game modes, match formats, generic
score/results, bounded one-to-many Split, Deathmatch/FFA proof fixtures,
immutable playlists, and opaque ladders. It does not mutate official replay
v1 or the opened `frontline-alpha-1` replay-v2 evidence. Numeric proof values
remain explicitly unbalanced experimental inputs. Its compatibility shield,
typed vocabulary, resolved rules/map/format/topology contracts, and the
profile-negotiated generic SDK/Guest programming boundary are implemented.
One neutral actor host now executes typed Deathmatch and Frontline mode
drivers, bounded Split and source-preserving fabrication, reusable same-life
forms, generic standings, chronology, and strict replay 3. The web viewer
normalizes that generation without assuming Deathmatch, and its hosted bridge
carries the typed presentation to mobile. The additive competition identity
layer now pins legacy Duel series to deterministic playlist versions, seasons,
and opaque ladders while preserving its scheduler, Elo, and public API
behavior. Generic server admission, normalized entrant/team-result storage,
reveal-ordered settlement, generic APIs, and any multiplayer rating policy
remain later work.

## Next session pointers

1. ~~Fast inner loop~~ **DONE** (DECISIONS #44): `--runtime in-process` runs
   player bot projects via a plain ~2 s assembly build — same engine, same
   deterministic RNG; WASM stays the canonical pre-submit verification.
   Measurement note: cold WASM compiles are ~8 s on an idle box (the
   tournament's "2–4 min" was compile contention + first-boot NuGet), so the
   trim-flags companion was dropped as not worth a cache-invalidating
   version bump. Cross-process cache-key locks and named Docker timeout cleanup
   now prevent CLI/server builds from corrupting one workspace (#70).
2. ~~Game design: anti-draw + skill-shot program~~ **SHIPPED through rules
   0.5** (DECISIONS #49/#53/#75). Pincer gen-10 is the launch champion.
   Separate 0.5 follow-up work may improve delayed-projectile visual causality
   without changing simulation and let future challengers test the 45%
   diversity policy on that ladder.
3. ~~Sprites/appearances (§33)~~ **DONE** — four active map-owned themes plus
   three complete staged theme kits, eleven SVG bot chassis, ten SVG projectile
   looks, mutable bot appearance with immutable match snapshots, plus the
   account-owned cosmetic catalog/grant ledger and starter, achievement,
   challenge, and reserved future unlock states. Expand progression only from
   observed product use; payments stay separate and later. Logotype (§31)
   remains.
4. SignalR as the live transport (timeline model already in place, DECISIONS #33).
   Related but separate: notifications beyond entitlements —
   challenge/result kinds, mobile in-app toasts and push — follow
   [`NOTIFICATIONS-PLAN.md`](NOTIFICATIONS-PLAN.md) (DECISIONS #108/#118).
   The durable record, `NOTIFY` fan-out and recovery poll already exist and
   already scale; the blocker is that the notification payload types exactly
   one kind, and `ToResponse` throws on a second until it becomes a
   discriminated union.
5. Roslyn analyzers for prohibited APIs (§6.1) — DX only; the runtime
   already neutralizes clock/entropy (DECISIONS #20).
6. §15.3 completion: network-less submission builds (vendor the ILC packages)
   plus hostile-input/resource-exhaustion verification. The first-VPS Compose
   baseline applies container CPU/memory/PID limits, but this is not yet the
   public-submission security boundary.
7. Phase 6 (competitions/seasons — also the progression container per
   GAME-DESIGN) and browser editing (phase 7).
8. Sound out artifact-hash parity local vs server once a second build
   environment exists (§40.5); record reasons where bytes differ.
9. Phase 0B polish: logotype assets, match-start countdown, destruction pause,
   spectator vs developer mode split (plan §32.3/32.4).
10. Backend maintainability follows
    [`BACKEND-MAINTAINABILITY-PLAN.md`](BACKEND-MAINTAINABILITY-PLAN.md):
    phases 1–5 are done—PostgreSQL integration tests are mandatory in CI, the
    shared application primitives exist, bot appearance is the pilot, and
    shared match admission/snapshots plus broadcast-safe projections are in
    place. Durable jobs now dispatch to typed handlers and ranked finalization
    is transactionally safe under concurrent workers. Next is source-owned
    progression/competition work as product needs appear. This is an
    incremental modular-monolith plan, not a rewrite.
11. Replay-native ML support is proposed in
    [`REPLAY-NATIVE-ML-PLAN.md`](REPLAY-NATIVE-ML-PLAN.md). The engine-rewrite
    seam is implemented for frozen local Frontline-alpha and its generic
    successor: one canonical public observation per prepared actor reaches its
    isolated life runtime and the same observation is snapshotted into replay
    v2 or v3 beside exact rules, topology, actions, lineage, and results.
    Actor protocol 1.0 carries both explicitly negotiated generations.
    Dataset export, public corpus access, bounded model assets, starter
    inference, and hosted-product delivery remain sequenced follow-ons; no
    ML-driven sandbox-limit change is proposed.
