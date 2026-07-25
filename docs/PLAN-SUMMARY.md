# nilbots — plan summary and status

Condensed from the full product/implementation plan (drafted with Sol, 2026-07).
This file exists so a fresh working session can continue without the original
document. `docs/DECISIONS.md` records where open choices were pinned.

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

## What exists in this repo (2026-07-24)

- `src/BotArena.Engine` — pure engine: rules, maps, FOV, RNG, tick resolution,
  replay + SHA-256 hash. No web/DB/WASM dependencies.
- `src/BotArena.Sdk` — developer API (`IBot`, `BotContext`, `Actions`,
  `IBotRandom`, `IBotDebug`); engine-independent.
- `src/BotArena.Bots.BuiltIn` — Idle/Wander/Hunter/Coward against the SDK only.
- `src/BotArena.Runtime` — in-process `IBotRuntime` (diagnostic).
- `src/BotArena.Runtime.Wasm` — canonical runtime: Wasmtime 44 host, fuel +
  memory limits, trap isolation, runtime protocol 0.1.
- `src/BotArena.WasmGuest` — guest program (built-ins as WASM;
  `scripts/build-wasm-guest.sh` → `artifacts/wasm/builtin-bots.wasm`).
- `src/BotArena.Guest` — reusable guest loop (`GuestHost.Run`) + protocol.
- `src/BotArena.Cli` — `new` / `build` (cached) / `play` / `watch` / `replay` /
  `verify` / `doctor` / `cache` / `bots` / `maps`.
- `templates/botarena-bot` — the player project template.
- `src/BotArena.App` — the monolith: cookie/OpenIddict auth,
  bots/submissions/challenges, PostgreSQL-backed compile and match jobs,
  stable artifact/replay object keys, and the SPA. Explicit web, compile,
  match, and migration roles share the same model without becoming services.
- `deploy/` — the scale-ready single-VPS baseline: Caddy, role-separated
  Docker Compose, commit-tagged images, provisioned certificates, a one-shot
  migration, private Garage S3 storage at replication factor 3, and
  backup/deploy runbooks. Garage replicas remain co-located until additional
  VPS nodes justify a private multi-zone layout.
- `web/` — one React build, two modes: the nilbots site (router) and the
  standalone replay viewer the CLI embeds replays into.
- `tests/` — engine, determinism, and WASM contract suites (195 tests, incl.
  DocDriftTests pinning docs/mirrors to the engine).
- `scripts/` — setup.sh (fresh container → working), setup-wasi-sdk.sh,
  build-wasm-guest.sh, test.sh, play.sh, dev-viewer.sh, e2e.sh, plus the
  balance/dynamics/control/arc/replay-review evaluation tools.

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
   0.5** (DECISIONS #49/#53/#75). Pincer gen-10 is the launch champion. Next
   improve delayed-projectile visual causality without changing simulation,
   then let future challengers test the 45% diversity policy on the 0.5 ladder.
3. Sprites/appearances (§33); logotype (§31).
4. SignalR as the live transport (timeline model already in place, DECISIONS #33).
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
