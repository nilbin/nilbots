# Bot Arena — plan summary and status

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
| 1 local DX | NuGet SDK, `dotnet new botarena`, real CLI (login/build/watch/submit), build cache, doctor | **NEXT** |
| 2 monolith | ASP.NET Core + Postgres + OpenIddict + accounts/bots/matches/replays modules | not started |
| 3 submissions | Isolated server builds, validation, immutable versions, sprites | not started |
| 4 public matches | Server execution, SignalR live viewing, match pages | **MVP boundary** |
| 5 competitive | Ranked match sets, ratings, leaderboard | later |
| 6 competitions | Seasons/tournaments | later |
| 7 browser dev | In-browser editor on the same pipeline | later |

## What exists in this repo (2026-07-22)

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
- `src/BotArena.Cli` — `play` / `replay` / `verify` / `bots` / `maps`.
- `web/` — React replay viewer, single-file build the CLI embeds replays into.
- `tests/` — engine, determinism, and WASM contract suites (62 tests).
- `scripts/` — setup.sh (fresh container → working), setup-wasi-sdk.sh,
  build-wasm-guest.sh, test.sh, play.sh, dev-viewer.sh, e2e.sh.

## Next session pointers

1. Phase 1: package `BotArena.Sdk` as a NuGet + `dotnet new botarena` template;
   per-player guest compilation (template project → own .wasm artifact);
   `botarena watch`; deterministic build cache keyed on
   source-hash + toolchain versions; `botarena doctor`.
2. Roslyn analyzers for prohibited APIs (plan §6.1) — DX only.
3. Sound out artifact-hash parity local vs server once a second build
   environment exists (§40.5); record reasons where bytes differ.
4. Phase 0B polish: logotype assets, match-start countdown, destruction pause,
   spectator vs developer mode split (plan §32.3/32.4).
