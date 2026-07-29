# Frontline rewrite — implementation workload

> Follow-on architecture: parallel modes, match formats, generic results,
> Split/replication, playlists, and ladders now live in
> [`GAME-MODE-ARCHITECTURE.md`](GAME-MODE-ARCHITECTURE.md). This document
> remains the implementation/evidence record for the frozen
> `frontline-alpha-1` generation and must not be rewritten as though that
> opened contract used the later generic schemas.

Status: **active experiments / implementation in progress**, 2026-07-28. This
document turns
[`FRONTLINE-REWRITE-PLAN.md`](FRONTLINE-REWRITE-PLAN.md) into an
implementation sequence. It does not mean Frontline has shipped, been
pre-registered, or replaced rules 0.5.

The frontend refactor, replay-v1 participant-identity normalization, and
version-neutral replay presentation are integrated. Web and mobile now mirror
the authoritative experimental Frontline replay-v2 state rather than a
presentation-only substitute.

Current implementation scope: Packages 0–7 and Package 8's local authoring
and measurement slice are implemented for the frozen `frontline-alpha-1`
path. That includes independent per-life runtimes, canonical team
observation, replay v2, replication/fabrication, Anchor/turret forms,
viewer/mobile presentation, the engine-independent actor SDK/Guest contract,
actor protocol/configuration 1.0, canonical isolated WASM life instances, an
explicit local CLI runner, four deterministic calibration doctrines, and a
replay-v2 evaluator.

A separate generic successor is also implemented as an off-by-default hosted
vertical slice: immutable Frontline Labs playlist v1, exact
`generic-actor-match-3` admission, one setless unranked H2H match between two
eligible submitted bots, normalized match-team results, replay 3 with
broadcast-safe prefixes, and the existing direct match viewer. It adds no
ladder, season, rating, series settlement, or product verdict. General
replay-v2 CLI summary/verification, the independently authored product cohort,
and broader rollout remain planned.

## 1. Recommendation

Do not implement the whole rewrite in parallel.

The current code uses `slot` to mean participant, body, runtime, array index,
projectile owner, replay identity, result identity, and database winner. The
rewrite separates two submitted teams from several independently executing
bodies. Engine state, runtime ownership, observations, actions, replay, CLI,
server persistence, and every viewer cross that seam.

Use parallel agents for read-only analysis, isolated spikes, and fixtures.
Give each implementation slice one owner and integrate it before starting
dependants. Shared contract files are integration choke points, not parallel
editing targets.

The completed experimental slices include the public-contract foundation,
historical characterization shield, Frontline definition/kernel, multi-life
session, actor runtime/observation/replay seam, replication, Anchor,
engine-independent SDK/Guest types, protocol/configuration 1.0, canonical WASM
instances, and version-neutral viewers. They leave official gameplay,
protocol/configuration 0.1, replay-v1 bytes, ordinary `play`, App match
selection, and server behavior unchanged.

## 2. Relationship to replay-native ML work

[`REPLAY-NATIVE-ML-PLAN.md`](REPLAY-NATIVE-ML-PLAN.md) is relevant and remains
the authoritative generic ML/data plan. Its packages are not copied into this
workload.

| Replay-native package | Relationship to Frontline |
| --- | --- |
| A — canonical observation and replay v2 | The experimental Frontline engine/replay seam, canonical actor WASM delivery, local CLI, and viewer consumer are implemented through Packages 4, 7, and 8. Hosted product delivery and any later duel generalization must reuse this model without changing replay v1. |
| B — replay-only dataset CLI | The experimental observation-complete schema is now available, but the exporter remains a product follow-on rather than part of Frontline gameplay. |
| C — public corpus access | Independent App/CLI product follow-on after replay v2; preserve broadcast secrecy. |
| D — bounded model assets | Separate toolchain/submission project after its WASM spike; do not overlap protocol/toolchain choke points casually. |
| E — starter inference and rollout | Follow-on proving the shared platform; Frontline supplies variable-entity examples and reward facts. |

The shared seam is:

```text
authoritative pre-tick state
    -> build each canonical public actor observation once
        -> snapshot that exact observation into replay v2
        -> deliver the same observation to its runtime
    -> freeze and collect the joint decision
    -> resolve
    -> snapshot events and authoritative post-state
```

The internal actor identity represents a historical duel slot as a normalized
stable identity and a Frontline actor as `teamId + unitId + lifeId`. Shipped
duels remain on replay v1; a later public duel-v2 path must reuse the same
identity model rather than narrowing or silently reinterpreting v2.

There will be one canonical replay writer, one replay-to-dataset exporter, one
corpus path, and one model-asset path. Frontline adds entity identity,
team-perception provenance, public rules inputs, lifecycle facts, and joint
ticks to those shared contracts.

## 3. Compatibility strategy

Rules 0.1–0.5, map format 1, runtime protocol 0.1, and replay format 1 remain
supported historical paths. They are not migrated in place.

Frontline initially lives behind an explicitly experimental ruleset and a new
typed map profile. New contracts use separate types and adapters until the
experiment proves they should become the default.

Three compatibility claims remain distinct:

1. **Executable** — an artifact can run under its declared protocol.
2. **Eligible** — the selected ruleset permits its capability set.
3. **Competitive** — its policy is effective under those rules.

A later season may intentionally make a legacy strategy uncompetitive. It
must not silently reinterpret an old action or observation. Unknown mandatory
capabilities cause explicit ineligibility; they never degrade silently to
`Wait`.

### 3.1 Release compatibility

Frontline changes the enumerated CLI compatibility surface: engine/runtime,
SDK/guest, controlled compiler inputs, maps, packaged bot inputs, replay
format, CLI summary, and replay viewer.

Before a server emits or admits any such contract:

1. bump `CliVersion` and the CLI project package version;
2. run the full release verifier;
3. publish the CLI from the exact revision;
4. create/push the corresponding `cli-v<version>` tag through `publish-cli`;
5. only then run `publish-and-deploy`.

`scripts/assert-cli-release.sh` may allow a later server-only revision to reuse
that CLI only when its complete enumerated compatibility surface is
byte-identical.

The hosted Labs slice changes the enumerated Engine/runtime/replay surface.
Its feature flag defaults off and must not be enabled in a deployment until
the release verifier passes, the compatible CLI/package version from the
exact revision is published, and its `cli-v<version>` tag exists. Implemented
code and migrations alone do not satisfy this deployment gate.

The mobile app consumes the server-hosted viewer. Web-only viewer changes
therefore reach mobile without a native renderer fork, but a hosted-viewer
bridge or native card/control change requires a coordinated mobile release.

## 4. Decisions that gate code

Freeze these before the package that consumes them, not all at once.

### Before the public-contract foundation

- Fingerprints: separate mechanic-content rules and map fingerprints plus one
  exact aggregate match-contract fingerprint. Component hashes exclude aliases;
  the aggregate includes its stored schema, ruleset ID, map ID, and map version
  because those values are public bot inputs.
- Disclosure: classify every `GameRules` value as public gameplay,
  observation-gated, runtime-only, replay-only, or internal/seed mechanics.
- Canonicalization: explicit property and collection ordering; sort true sets
  and catalogs, but preserve any sequence whose order is observable to bots
  (including legacy objective tiles). Do not rely on reflection or declaration
  order.
- A fingerprint covers the canonical payload without its own fingerprint
  field.

### Package 3 entity and lifecycle contract

- `teamId`: 0 or 1.
- `unitId`: stable, team-local identity tied to Prime or fabrication slot.
- `lifeId`: monotonically increasing for each new runtime life.
- Canonical entity order: `(teamId, unitId, lifeId)`.
- A form change retains runtime memory; destruction disposes the runtime; a
  Prime respawn or successful child (re)fabrication creates fresh memory and a
  new `lifeId`. Rebuilding becoming Ready creates no life.
- `PrepareTick()` applies due lifecycle transitions once and returns the exact
  canonical actor keys required by `Step(...)`; repeated preparation before a
  successful step is idempotent.
- A Prime destroyed while executing tick `D` respawns at the start of
  `D + 1 + PrimeRespawnTicks`, leaving exactly `PrimeRespawnTicks` complete
  ticks with that unit absent.
- Enemy ground movement cannot enter an opposing protected home pad. The pad
  grants no damage immunity and does not block projectiles; only the authored
  `PrimeSpawn` tile is used for Prime respawn.
- Decide fuel, fault, memory, startup, and debug budgets per body versus per
  team.

### Replication decisions frozen in Package 5

- Fabrication targets an own Ready child slot while the Prime stands on its
  protected home pad. Spawn selection uses the first free non-Prime pad tile
  in canonical Y-then-X order after movement.
- A full pad leaves the attempt legal in the mask but resolves it as
  `Blocked`; an ally vacating a tile that tick can make it succeed.
- Every child life starts with fresh private memory, an explicit spawn reason,
  the slot's default form, and authored home facing.
- Team observations use one frozen allied-perception union with exact
  `observedBy` provenance and no same-tick action sharing.
- Runtime failures are attributed to the exact host life; per-body tick
  budgets and a participant-shared match debug budget remain explicit.

### Anchor decisions frozen in Package 6

- Turrets use separate `shoot-direction` action 102 with one absolute
  eight-way heading. They cannot use legacy facing-based Shoot or programmed
  curves, and body facing does not change.
- Anchor action 101 is irreversible for the current life, adds `+2` health
  clamped to the turret maximum, and retains the same runtime/memory.
- A transform submitted on tick `T` completes after objective at
  `T + windupTicks - 1`; the pending source form is Wait-only. Nonlethal
  damage continues and death emits Destroyed then explicit cancellation.
- Every map-authored Anchor-forbidden tile is illegal. Turret objective weight
  is zero; it cannot capture or contest.

### Replay-v2 decisions frozen internally

- Discriminated duel-slot versus Frontline-life actor identity.
- Canonical ordering for actor observations, decisions, events, and state.
- `null` versus empty semantics for every optional capability.
- Manifest placement and exact rules/map/match fingerprint coverage.
- Public actor observation versus omniscient spectator/critic separation.
- Replay-v1 reader/hash behavior and permanent fixtures.

These are implemented and strict on the experimental Frontline path. The
local `nilbots experiment frontline` runner now emits replay v2 and a
self-contained Canvas2D viewer, but replay v2 is still not an App/server,
ranked, or stable public format. Historical `replay --summary` and `verify`
remain v1-only; wider emission still requires the remaining Package 8
compatibility, admission, and release gates.

### Before ranked or public use

- Decide whether the implemented objective-only max-tick territorial score
  remains the experimental/ranked rule after replication and Anchor testing.
- Public eligibility policy for negotiated unsupported capabilities and
  future action/schema requirements. Internal protocol negotiation and exact
  compile-contract attestation are implemented.
- Named season/ruleset identity versus exact contract fingerprint for ladders.
- Replay/debug/training-data privacy and licensing.
- Required CLI and mobile release versions.

## 5. Work packages

### Package 0 — characterization shield

Status: **implemented**.

Goal: prove the refactor did not silently change historical play.

- Add representative fixed replay-hash fixtures for official rules 0.1–0.5.
- Preserve deterministic random golden values.
- Preserve map-format-1 validation behavior.
- Keep replay-v1 serialization and verification tests.
- Record representative in-process/WASM parity fixtures.

Existing tests prove repeatability, but many compare a run with another run
of the same current binary. Fixed expected hashes are needed before changing
the slot/body architecture.

Primary surfaces:

- `tests/BotArena.Determinism.Tests/DeterminismTests.cs`
- `tests/BotArena.Engine.Tests/ReplaySerializationTests.cs`
- `tests/BotArena.Runtime.Wasm.Tests/WasmRuntimeTests.cs`
- new named fixtures under `tests/fixtures/`

Gate: the fixed compatibility suite passes before and after every later
package.

### Package 1 — public contract and fingerprints

Status: **implemented**.

Goal: make the complete effective public rules machine-readable without
changing what bots receive yet.

Implemented additions:

- `src/BotArena.Engine/PublicRulesManifest.cs`
- `src/BotArena.Engine/PublicRulesManifestFactory.cs`
- `src/BotArena.Engine/PublicMapManifest.cs`
- `src/BotArena.Engine/PublicMatchContractManifest.cs`
- `src/BotArena.Engine/GameRuleDisclosure.cs`
- `src/BotArena.Engine/GameRuleDisclosureCatalog.cs`
- `src/BotArena.Engine/RulesManifestSerializer.cs`
- `src/BotArena.Engine/MatchContractFingerprint.cs`
- matching one-class-per-file tests

Narrow existing-file change:

- use a dedicated public-manifest schema version in
  `src/BotArena.Engine/BotArenaVersions.cs`.

Requirements:

- derive the manifest from `GameRules` and the public map contract;
- represent current 0.5 as one mobile form and its current action catalog;
- use a deliberately ordered canonical writer;
- sort true sets and catalogs explicitly while preserving observable sequence
  order and duplicates;
- require every `GameRules` property to have a disclosure classification;
- prove every public gameplay mutation changes the right fingerprint;
- prove runtime/debug-only changes do not change the public rules fingerprint;
- leave replay v1 and every historical match byte-identical.

Do not change SDK, protocol, replay records, `MatchEngine`, CLI, App, `web/`,
or `mobile/` in this package.

### Package 2 — Frontline definition and objective kernel

Status: **implemented**.

Goal: freeze the experimental rules/map input and objective math without
running full matches.

Implemented additions:

- `src/BotArena.Engine/FrontlineRules.cs`
- `src/BotArena.Engine/UnitFormRules.cs`
- `src/BotArena.Engine/FrontlineMapProfile.cs`
- `src/BotArena.Engine/ResolvedMatchDefinition.cs`
- `src/BotArena.Engine/MatchDefinitionResolver.cs`
- `src/BotArena.Engine/FrontlineControlState.cs`
- `src/BotArena.Engine/FrontlineControlSystem.cs`
- `maps/experimental/frontline-01.json`
- focused map, resolution, fingerprint, and control-system tests

Narrow existing-file changes:

- optional disabled Frontline subtree in `GameRules`; no named/CLI experiment
  is exposed before a playable session exists;
- a map-format-2 parser branch in `ArenaMap`; format 1 remains unchanged.
- keep format-v2 assets under `maps/experimental/`, outside the current
  App/CLI top-level map catalog and CLI package wildcard;
- resolve map/rules compatibility before queueing and again before legacy
  execution; legacy `MatchEngine` continues rejecting a Frontline definition,
  while the internal experimental path uses the dedicated
  `FrontlineActorMatchEngine` and `FrontlineMatchSession`.

Acceptance:

- exact five-position ordering and two protected homes;
- deterministic validation before tick zero;
- sole-team binary pressure independent of body count;
- exact contested/empty decay and redeploy timing;
- reversible partial pressure;
- deterministic three-push base breach from the centre;
- stable map/rules/match fingerprints;
- eight-way turret launch headings and every enabled programmed path are
  included in the Anchor-to-Prime-spawn safety proof.

### Package 3 — Prime-only headless Frontline

Status: **implemented headless checkpoint**.

Goal: play a complete two-Prime match with current movement/projectile
mechanics, respawns, Frontline advancement, and base-breach victory.

A separate `FrontlineMatchSession` implements this slice without rewriting
legacy `MatchSession`. It owns no bot runtime, observation, replay, CLI, App,
or viewer integration.

Introduce:

- `FrontlineActorId(teamId, unitId, lifeId)`;
- `FrontlineTeamState`, `FrontlineUnitState`, `FrontlineLifeState`, and
  `FrontlineLifecycleStatus`;
- `FrontlineMatchState`, projectile, event, and result records;
- `FrontlineTickStart`, `FrontlineResetResult`, and `FrontlineStepResult` for a
  stable-keyed joint-decision API suitable for a later training wrapper.

#### Prepared-tick and decision-key contract

`Reset()` creates Prime life `0` in stable unit `0` for each team and returns
the canonical actors `0:0:0`, `1:0:0`. For every later tick:

1. `PrepareTick()` applies respawns due at that tick start exactly once.
2. It returns `ActiveActors` sorted by
   `(teamId, unitId, lifeId)`, plus any respawned actors and lifecycle events.
3. Repeated `PrepareTick()` calls before `Step()` return the same prepared
   object.
4. `Step(IReadOnlyDictionary<FrontlineActorId, BotDecision> decisions)`
   requires keys **exactly** equal to those life-qualified `ActiveActors`.
   Missing, extra, or stale-life keys are rejected atomically; dictionary
   insertion order is irrelevant.
5. A tick with no active lives requires an empty decision dictionary. A
   respawned life is in that tick's key set and may act immediately.

Package 3 rejects runtime-fault decisions because runtime ownership begins in
Package 4. Invalid decision sets do not consume or mutate the prepared tick.

#### Lifecycle, pads, projectiles, and damage

- If life `L` is destroyed while executing tick `D`, its stable unit queues
  the next life for tick `D + 1 + PrimeRespawnTicks`. It is absent for exactly
  `PrimeRespawnTicks` full decision ticks. Respawn keeps the same team/unit,
  increments `lifeId`, restores authored Prime spawn/facing, max health and
  starting energy, clears cooldown/action/life-damage state, and can act on
  the respawn tick.
- Simultaneous destruction of every active Prime does not end the match;
  empty joint steps continue until lifecycle transitions restore actors.
- Enemy ground movement cannot enter the opposing protected home pad. There
  is no spawn or pad damage immunity, projectiles are not blocked, and
  `PrimeSpawn` is the only Prime respawn tile. At the Package 3 checkpoint,
  other pad tiles were not fallback spawns; Package 5 later made the
  non-Prime tiles explicit child-fabrication candidates.
- In-flight projectiles survive their firing life's destruction. Ownership
  remains the exact old `FrontlineActorId`; with the initial no-friendly-fire,
  no-allied-blocking rules they pass through a later allied life, may damage
  an enemy, and credit cumulative damage to the stable firing unit rather than
  the new life.
- Simultaneous pending hits are applied in canonical target/impact order.
  Each hit's credited and emitted amount is capped to remaining health:
  `actualDamage = min(DamagePerHit, remainingHealth)`. Overkill never inflates
  damage events or ledgers.

#### Implemented resolution and completion order

1. `PrepareTick`: apply due tick-start lifecycle and freeze actor keys.
2. Validate the exact joint decision and each action.
3. Resolve turns, then simultaneous movement.
4. Advance existing projectiles.
5. Launch new shots, collect simultaneous hits, apply actual damage, and
   remove/queue destroyed lives.
6. Publish action results and update surviving-life cooldown/energy.
7. Update objective control from **post-damage active lives**; a destroyed
   occupant neither captures nor contests that tick.
8. Increment the next-tick counter, then resolve base breach before max ticks.

At max ticks, only territorial state decides:

```text
centre = FrontlinePositionCount / 2
position = (ActivePositionIndex - centre) * CaptureThreshold
claim = +CaptureProgress for team 0, -CaptureProgress for team 1, else 0
territorialScore = position + claim
```

A positive score wins for team 0, a negative score for team 1, and zero draws.
Health and damage are result facts, not Package 3 tiebreakers. A base breach
created by the final allowed tick takes precedence over `MaxTicks`.

Gate: implemented tests cover keyed/idempotent preparation, deterministic
insertion-order independence, combat/projectile parity, exact respawn gaps,
old-life projectiles, actual-damage overkill, post-damage presence, early and
final-tick breach, max-tick scoring, simultaneous destruction, and
deterministic lifecycle reruns.

### Package 4 — runtime, canonical observation, and replay v2 foundation

Status: **implemented internally**, including the version-neutral web/mobile
consumer seam.

Result: the headless session became a canonical per-life
runtime/observation/replay-v2 vertical slice while implementing the
engine/replay seam of replay-native ML Work Package A once. The viewer
consumer uses the authoritative v2 payload through one normalization layer;
replay v1 remains exact.

Requirements from `REPLAY-NATIVE-ML-PLAN.md`:

- one public-only engine observation construction point;
- snapshot the exact instance/projection sent to the runtime before
  resolution;
- replace authoritative `GameEvent` exposure with redacted observed events;
- preserve exact visible-projectile and capability semantics;
- separate replay DTOs from mutable engine state;
- group pre-tick observations, decisions/resolutions, events, and post-state;
- maintain a dedicated replay-v1 reader/verifier and frozen hashes;
- add observation/runtime/replay parity and information-leakage tests.

Frontline-specific additions:

- actor identity supports duel slot and Frontline team/unit/life;
- static header carries the exact public manifest plus fingerprints;
- variable allies/enemies/projectiles/objectives remain collections, not
  fixed three-body fields;
- team-perception provenance is part of the public observation;
- lifecycle and Frontline reward facts are authoritative post-tick facts;
- no same-tick allied action enters another actor's observation;
- runtime orchestration calls `PrepareTick()`, builds every actor observation
  from the same frozen tick start, obtains one decision for each exact key, and
  submits that keyed joint action to `StepActors(...)`;
- replay v2 records tick-start lifecycle events and the exact actor-key set so
  respawn gaps and new lives are reconstructible without simulation;
- this slice proved the engine/in-process runtime boundary without pretending
  protocol 0.1 could carry Frontline; Package 7 now supplies its separate
  versioned actor transport and canonical WASM path.

Primary current surfaces:

- `src/BotArena.Engine/ActorObservation.cs`
- `src/BotArena.Engine/ActorRuntime.cs`
- `src/BotArena.Engine/FrontlineActorMatchEngine.cs`
- `src/BotArena.Engine/FrontlineMatchSession.cs`
- `src/BotArena.Engine/FrontlineObservationProjector.cs`
- `src/BotArena.Engine/ReplayV2.cs`
- `src/BotArena.Engine/ReplayV2Projection.cs`
- `src/BotArena.Engine/ReplayV2Serializer.cs`
- Engine replay/determinism/runtime contract tests and the TypeScript wire,
  normalization, presentation, and bridge mirrors

At the Package 4 checkpoint, backend-wide version dispatch and public
verification remained deferred. The Engine serializer, TypeScript mirror,
viewer, hosted bridge v2, and mobile native cards were implemented without
making the App emit v2. Package 8 now uses that seam for explicit local
experimental emission; general CLI summary/verification and App/server
emission remain deferred.

The Package 4 gate kept replay v2 internal until its claimed Frontline
action/lifecycle variants were covered. Those variants are now exercised by
the local experiment, but v2 remains an unstable experimental format. After a
replay version is admitted as a stable public product format, any structural
mutation requires another replay-format bump.

Gate:

- the runtime input is a lossless projection of its replay observation plus
  static header data;
- in-process/WASM behavior parity remains for protocol 0.1;
- actor in-process/WASM behavior and replay parity hold for protocol 1.0;
- identical inputs produce byte-identical replay-v2 documents and hashes;
- replay-v1 fixtures verify without normalization;
- the leakage audit passes;
- no gameplay result changes on frozen legacy fixtures.

### Package 5 — entity actions and replication

Status: **implemented internally**.

Goal: instantiate the same submitted artifact independently for each active
body and each new life.

Engine contracts:

- entity collections sorted by stable identity;
- immutable public match-manifest handle;
- public allied state plus merged team-visible facts and provenance;
- stable action code/text ID with typed parameters;
- per-form static action catalog plus per-tick dynamic legality masks;
- no extension or reinterpretation of existing `BotAction` values.

Runtime ownership:

- replace participant ownership of one concrete `IBotRuntime` with a
  team-level runtime factory;
- keep participants as the two submitted policies;
- create one isolated in-process/test runtime per active life;
- dispose it on destruction and create fresh memory on Prime respawn or child
  (re)fabrication;
- keep execution sequential initially over frozen observations.

Add:

- stable team/unit/life seed derivation with golden values;
- fixed fabrication unlocks and slot rebuild timers;
- canonical spawn selection and occupied-pad handling;
- per-unit/per-team resource budgets from the frozen decision.

Keep the old `BotObservation`/`BotAction` route as the protocol-0.1 adapter.

Gate: variable-size observation/action fixtures, independent in-process
memory, exact lifecycle, symmetry breaking by `unitId`,
provenance/legality/order, six-body collision/focus-fire, full-pad respawn,
and allied-projectile tests.

### Package 6 — forms and Anchor

Status: **implemented internally**.

Goal: implement typed capabilities rather than scattered turret branches.

- Mobile Prime, mobile child, and turret definitions.
- Form-derived health, vision, movement layer, objective weight, and actions.
- Same runtime survives Anchor.
- Anchor is telegraphed, consumes the tick, and resolves after objective on
  the exact due tick only if the child survives.
- Turrets cannot move, capture, contest, or Anchor on prohibited tiles.
- Directional turret fire is explicit in action, replay, and observed event.

First causal arms remain:

1. Frontline only;
2. mobile children;
3. 3-HP/current-cadence turret;
4. 5-HP/faster-cadence turret;
5. range 6 versus 7 only if needed.

Gate: scripted open-ground 1v1 and coordinated 2v1 turret tests, map coverage,
five-slot state/observation/replay coverage, exact form-transition causality,
and obsolete-front usefulness diagnostics. These are mechanics diagnostics,
not a balance verdict.

### Package 7 — SDK and actor protocol 1.0

Status: **implemented internally at the Package 7 checkpoint**. That package
established the canonical runtime path without a CLI/App/server selection or
admission claim; Package 8 now selects it only through the explicit local
experiment command.

Goal: deliver the new contract to independent C# bot instances.

Because SDK must not reference Engine, mirror the public types in SDK and map
them in Runtime/Guest adapters.

Implemented result:

- `IActorBot` receives typed per-life `StartLife` and `Tick` calls with the
  immutable public contract delivered once at MatchStart and variable
  entity/action collections thereafter.
- One artifact factory owns one Wasmtime Engine and compiled Module. Every
  active life owns an isolated Store, Instance, thread, memory, globals,
  deterministic shims, and bot object; destruction disposes it and respawn or
  refabrication creates fresh private memory.
- Actor protocol 1.0 uses the dependency-free custom `NBV2` tagged binary
  encoding selected after a NativeAOT/WASI spike. Its 12-byte frame header,
  skippable unknown fields, required/duplicate/truncation checks, null/empty
  semantics, and hard size/depth/count limits are specified in
  [`RUNTIME-PROTOCOL.md`](RUNTIME-PROTOCOL.md).
- Host frames are bounded at 1 MiB and guest replies at 64 KiB. Semantic
  action/form IDs are canonical lowercase kebab case and at most 64 UTF-8
  bytes; the bot selector and opaque handles remain bounded at 256 bytes.
- `Hello` negotiation distinguishes legacy protocol 0.1 artifacts from actor
  artifacts. `Ready` attests the exact actor runtime, MatchStart, observation,
  and decision schemas compiled into the guest. Each released request accepts
  exactly one correlated reply; `Unsupported` is a typed capability failure.
- Runtime configuration 1.0 pins fuel, epoch/wall-clock interruption, 64 MiB
  linear memory, 16,384 table elements, and one memory/table/instance per
  life Store. WebAssembly start sections are rejected; `_start`, every tick,
  and MatchEnd retain an interruption path.

Coordinated version bumps:

- SDK 0.9.0;
- guest adapter 0.9.0 and the controlled-build cache;
- actor runtime protocol/configuration 1.0 while legacy remains 0.1;
- rebuilt tracked built-in WASM artifact;
- CLI/package 0.6.0 at the Package 7 checkpoint under the release guard
  (the local Package 8 consumer raises it to 0.7.0).

Gate: implemented tests cover malformed/unknown/duplicate/truncated fields,
size and collection limits, exact attestation, correlated replies, typed
unsupported capabilities, old-artifact eligibility, startup/tick/shutdown
limits, WASM/in-process and observation/replay parity, and per-life isolation.

### Package 8 — consumers, evaluation, and experimental release

Status: **partially implemented**. Internal web/mobile presentation, the
experimental brief, local CLI authoring loop, reference doctrines,
replay-v2 dynamics evaluator, and version-neutral blind sampler are complete.
The separate generic Frontline Labs successor now has a minimal
feature-gated App/server admission and replay-v3 delivery path. General
replay-v2 summary/verification, the independently authored product cohort,
ranked competition, and rollout remain.

Goal: make Frontline observable and evaluable without claiming it has shipped.

Local CLI/evaluation slice — implemented:

- separate `nilbots experiment frontline` dispatch, never a `play`/ranked
  alias;
- isolated `frontline-alpha-1` rules catalog outside
  `GameRules.Resolve`/shipped names;
- format-v2 map packaging only under `maps/experimental/`;
- actor built-in, player-project, and prebuilt-WASM resolution with
  in-process diagnostic and all-WASM modes;
- canonical complete/partial replay-v2 output, embedded Canvas2D viewer, and
  rules/map/match fingerprints in diagnostics;
- four deterministic smoke/calibration policies covering rush, mobile swarm,
  Bastion/Anchor, and counterpunch behavior;
- `frontline-replay-eval.py` for duration phases, fabrication, Anchor/turret,
  combat, territorial reversals/comebacks, actorless periods, stagnation, and
  action distributions;
- replay-review sampling that dispatches on replay version without reading
  outcomes.

Frontend against experimental replay v2 — implemented:

- one replay-version normalization layer;
- team/unit/life presentation through `replayPresentation.ts`;
- site/CLI/hosted-review viewer updates;
- hosted-viewer bridge and matching native mobile cards/controls;
- v1 golden fixtures plus new Frontline golden frames.

Remaining:

- add general team-aware replay-v2 verification/summary if the format is
  admitted beyond this command;
- keep frozen replay v2 local; the distinct hosted generic arm uses strict
  replay 3 and validated result/hash-withholding prefixes;
- commission at least four independently authored native doctrines under
  equal budgets (the built-ins above are one-author fixtures and do not count);
- freeze all-WASM artifacts and holdout blocks, then run any pre-registered
  causal arms, dynamics analysis, and outcome-blind review;
- record a ship/hold verdict without altering current rules documentation.

The replay-native plan's dataset/corpus/assets/inference packages may proceed
after Package 4 through separate owners. They are not a duplicate Package 9
here and are not required to answer whether Frontline is fun.

## 6. Agent ownership

### First implementation wave — completed

- **Foundation owner:** Packages 0–1, including tests.
- **Fixture/spec owner:** independently freeze compatibility and
  canonicalization vectors without editing the foundation owner's files.
- **Primary integrator:** review disclosure/canonicalization, run the full
  suite, and verify historical hashes.

Do not assign a second agent to `GameRules`, `BotArenaVersions`, or manifest
files during this wave.

### Engine waves — completed through Package 6

- One engine owner for Packages 2–6, integrated in ordered checkpoints.
- One independent adversarial-test owner after public types stabilize.
- The primary integrator owns shared-combat extraction and compatibility
  review.

### Observation/replay wave — completed internally

Package 4 has one contract owner spanning engine observation and replay
serialization. A separate leakage/fixture reviewer may add tests after DTOs
stabilize. Do not split observation construction and replay projection between
agents; their identity is the invariant.

### SDK/protocol wave — completed internally

The actor/action/replay types are frozen internally. Package 7 now has:

- one engine-independent SDK/Guest contract and shared wire codec;
- one runtime factory that compiles per artifact and instantiates per life;
- independent protocol, sandbox, parity, and eligibility tests;
- coordinated SDK/Guest/protocol/configuration/cache/artifact/CLI version axes.

`MatchEngine`, `MatchSession`, `Replay.cs`, `BotArenaVersions`, shared identity
and action types, protocol twins, and version documentation remain choke
points owned by one person at a time.

### Product wave — local alpha and minimal hosted Labs slices complete

- replay/CLI owner retains the separate local dispatch and may add generic v2
  verification/summary without changing v1 verification;
- Backend owner has added exact-profile eligibility, direct admission, and
  broadcast-safe replay-v3 delivery for one setless Labs match. Generic
  series, FFA/2v2 admission, and ranked settlement remain separate work;
- primary integrator owns compatibility/release gates and the experimental
  deployment boundary.

### Replay-native ML follow-ons

After Package 4:

- one CLI/data owner for replay-only export;
- one App/CLI owner for corpus discovery, with generated clients;
- one toolchain owner for the model-asset spike and controlled build;
- one reference-bot owner for training/export/inference dogfood.

Do not run the model-asset/toolchain package concurrently with SDK/protocol
work unless file ownership and version coordination are explicit.

### Product-verdict wave

After SDK, CLI, replay, and the experimental brief are usable:

- commission at least four independently authored Frontline-native doctrines;
- give authors only the public SDK/CLI/player contract;
- use equal iteration budgets and frozen holdout blocks;
- keep historical 0.5 champions as compatibility sentinels;
- run outcome-blind review before opening aggregate outcomes.

## 7. Frontend boundary and staged implementation

The refactor is integrated. Replay-v1 presentation resolves participants by
stable slot rather than array position, and experimental replay v2 normalizes
team/unit/life, lifecycle, forms, fabrication, and Frontline state into the
same playback model. Hosted bridge v1 remains unchanged; bridge v2 carries
stable-unit/team terminal rows and current form/transition presentation.

The hosted Labs site surface is intentionally smaller than a general mode
browser: an owner-facing bot-detail panel appears only when Labs is enabled
and the selected bot supports the required profile, filters opponents to
eligible submitted bots, creates the direct match, and navigates to the
existing match page. It adds no main-navigation mode, series page, ladder
view, or ranked affordance.

The implemented Frontline consumer work touches:

- `web/src/types.ts` and `web/src/replayWireV2.ts` — versioned wire mirrors;
- `web/src/replayNormalize.ts` and `web/src/replayModel.ts` — strict
  version-boundary validation and the shared normalized model;
- `web/src/replayPresentation.ts` — shared per-tick presentation derivation;
- viewer components, playback, and Canvas2D renderer under `web/src/`;
- optional lazy WebGL 2.5D renderer under `web/src/render3d/`;
- site replay pages;
- `mobile/src/components/ArenaViewer.tsx`;
- `mobile/src/components/arena/` native cards, transport, and bridge protocol.

The renderer serves the site, CLI self-contained viewer, hosted review, and
mobile WebView. Mobile renders the arena canvas in the hosted WebView and
native controls/cards outside it. The experimental consumer slice now:

- preserves replay-v1 viewing and normalizes v2 once;
- carries team/unit/life identity, forms, fabrication/lifecycle state, and the
  five-position Frontline;
- derives health, objective, and timing language from the manifest;
- groups events by team while retaining unit attribution;
- extends `HostedViewer` bridge v2 and the native bridge in lockstep.

Canvas2D remains the default. The optional WebGL renderer consumes only the
normalized replay/presentation model, loads Three.js lazily, and is stubbed
out of the self-contained CLI build. Manual GPU/mobile QA remains before any
release claim.

Phone/desktop golden-frame coverage and outcome-blind product review remain
release gates rather than evidence supplied by the mechanics fixture.

The Frontline-specific portion was implemented only after Engine replay v2
supplied stable team/unit/life, lifecycle, form, fabrication, and objective
fields. Hosted-viewer bridge and native mobile arena changes landed in the
same integration slices. The explicit experimental CLI now emits v2 under
CLI 0.7.0. The App does not emit that frozen alpha format; its distinct
off-by-default Labs path emits replay 3 and reuses the direct match viewer.
General CLI replay-v2 summary/verification remains deferred.

## 8. Documentation migration

Do not rewrite historical documents as though Frontline has already shipped.

### Now

- Keep `PLAYER-GUIDE.md`, template docs, site docs, and ordinary `play`
  surfaces on shipped rules 0.5. Isolate any CLI help/README Frontline
  material under the explicit local `experiment` boundary.
- Keep `REPLAY-NATIVE-ML-PLAN.md` as the generic proposal and both Frontline
  plans explicitly experimental rather than shipped.
- Do not add numeric hypotheses to `DECISIONS.md`.
- Keep `PLAN-SUMMARY.md` explicit that Frontline is active experimentally
  while rules 0.5 remains current.

### Commissioned on 2026-07-27

- `GAME-DESIGN.md`, `PLAN-SUMMARY.md`, and `DECISIONS.md` identify Frontline
  as the active experiment while retaining rules 0.5 as current.
- `EXPERIMENTAL-FRONTLINE.md` is now the concise local experimental bot
  contract for lifecycle, fabrication, Anchor, turret-fire, actor WASM, and
  replay v2. It remains explicitly unshipped. That document also distinguishes
  the separate hosted replay-v3 Labs successor, which is unranked and disabled
  by default rather than rewriting the alpha contract.
- Maintain `DOCUMENTATION.md` as the status index; split replay/protocol guides
  by version when v2 creates simultaneously supported formats.
- Continue recording only actually frozen decisions, never numeric
  hypotheses, in `DECISIONS.md`.

### During implementation

- Update `CLAUDE.md` only when manifest, identity, protocol, replay, and release
  invariants become true.
- Extend `EVALUATION-METHODOLOGY.md` and repository balance/agent workflows
  before commissioning Frontline bots.
- Preserve replay format 1 verbatim and discoverably; recommended shape is
  `REPLAY-FORMAT.md` as a version index plus version-specific v1/v2 documents.
- Maintain [`RUNTIME-PROTOCOL.md`](RUNTIME-PROTOCOL.md) as the versioned dual
  protocol contract.
- Keep `WASM-DEVELOPMENT.md` aligned with the selected actor codec/runtime and
  rebuilt tracked artifact.
- Update replay summaries, DocDrift tests, and every mechanical rules-change
  surface in the same package as its contract.
- Follow `REPLAY-NATIVE-ML-PLAN.md` for dataset, corpus, model-asset, and
  example documentation rather than restating them in Frontline docs.

### Only on ship

- Change `GameRules.Current`, default ladder/rules copy, README, site docs,
  template README, and `PLAYER-GUIDE.md`.
- Retain the rules 0.5 guide/design, findings, champions, maps, artifacts, and
  replay fixtures as supported history.
- Publish a release compatibility matrix covering SDK, guest adapter, runtime
  protocol/configuration, replay format, ruleset, manifest schema, map format,
  artifact eligibility, CLI, and mobile viewer.
- Revisit worker CPU/memory, replay/object storage, broadcast bandwidth, and
  client payloads for longer matches and up to six simultaneous runtimes.

## 9. Integration and acceptance gates

Every package must pass:

- `git diff --check`;
- relevant focused tests;
- the fixed historical compatibility suite;
- full `dotnet build BotArena.sln`;
- `bash scripts/test.sh` before integration when SDK/Guest/WASM is touched.

Packages touching web/mobile run the scoped builds/tests from `web/CLAUDE.md`
and `mobile/CLAUDE.md`. Frontline UI work consumes only authoritative replay-v2
fields; it must not invent presentation-only gameplay state.

Before enabling the hosted Labs flag in an experimental server deployment:

- CLI/package 0.9.3 published and `cli-v0.9.3` tagged from the exact final
  compatibility revision before deployment;
- the profile-aware web, compile, and match-worker binary deployed and soaked
  everywhere with the flag still false; the retained `previous` rollback
  bundle must already contain the profile-aware admission/execution checks and
  scoped legacy backfiller;
- the compile queue drained and every compile-worker/compiler-runner stopped
  before propagating and validating the enabled flag and quota values on every
  node;
- compile workers restarted on the intended revision/config before any
  enabled web replica or submission traffic is exposed, followed by a
  generic-only build smoke test;
- no rollback to a pre-profile-aware/pre-scoped-backfiller image while a
  generic-only artifact or Labs match exists;
- deterministic canonical WASM matches and zero faults;
- replay-v1 verify/view compatibility;
- replay-v3 parity, leakage, and broadcast-secrecy gates;
- protocol negotiation and old-artifact eligibility tests;
- raw/compressed replay-size and concurrent-viewer prefix-projection
  measurements;
- six-runtime resource measurements both alone and concurrently with one Duel
  inside the production 1 GiB match-worker limit;
- durable Labs quotas and independently sized generic worker lane configured;
- coordinated mobile release if the hosted bridge/native presentation changed.

Before any ship decision, apply `EVALUATION-METHODOLOGY.md` literally:
regression sentinels, same-cohort causal arms, candidate-native doctrines,
dynamics metrics, and locked outcome-blind viewer notes.

## 10. Immediate implementation wave

Packages 0–7 now establish the historical shield, exact public contract,
format-v2 map, multi-life runtime/session, canonical public team observation,
replication/fabrication, Anchor/turret forms, strict experimental replay v2,
and version-neutral web/mobile presentation, engine-independent SDK/Guest types,
actor protocol/configuration 1.0, and canonical isolated WASM instances.
Package 8's local slice adds explicit experimental rules/map dispatch, actor
project/built-in/WASM execution, replay-v2 output and viewer, reference
doctrines, descriptive dynamics metrics, and version-neutral blind sampling.
Official rules 0.1–0.5, protocol/configuration 0.1, replay v1, and their
canonical hashes remain unchanged. The experimental map remains absent from
the App and ordinary CLI map catalogs but is packaged under the explicit
experimental directory. Legacy `MatchEngine` still rejects it rather than
silently running duel mechanics.

The next dependent implementation slice is Package 8's product-verdict and
broader hosted path:

1. commission the independently authored Frontline-native cohort, freeze
   all-WASM artifacts/maps/seeds/criteria, and lock at least twelve
   outcome-blind reviews before opening the descriptive report;
2. pre-register only the causal balance arms the calibration and blind review
   justify; do not tune the opened `frontline-alpha-1` data post hoc;
3. exercise the off-by-default setless Labs path without treating availability
   as evidence; if the product earns continued rollout, add broad generic
   discovery, FFA/2v2 or series admission, and ranked settlement only behind
   their own release and evidence gates;
4. build the replay-only dataset and corpus follow-ons through the shared
   `REPLAY-NATIVE-ML-PLAN.md`, not a Frontline-specific exporter;
5. record a ship/hold decision while keeping shipped rules 0.5 untouched
   unless every frozen product gate is actually satisfied.
