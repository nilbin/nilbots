# Frontline rewrite — implementation workload

Status: **active experiment / implementation in progress**, 2026-07-27. This
document turns
[`FRONTLINE-REWRITE-PLAN.md`](FRONTLINE-REWRITE-PLAN.md) into an
implementation sequence. It does not mean Frontline has shipped, been
pre-registered, or replaced rules 0.5.

The frontend refactor is integrated. A replay-v1 identity-normalization slice
may proceed now; Frontline-specific `web/` and `mobile/` work still waits for
the replay-v2 contract it will consume.

Current implementation scope: Packages 0–2 are present in the accompanying
foundation change. Package 3 is the next active slice; later packages remain
planned.

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

The first code change should be an engine-only public-contract foundation. It
must not change gameplay, replay-v1 bytes, artifacts, SDK, protocol, CLI, App,
or frontend. That foundation is independently useful even if the Frontline
game hypothesis later changes.

## 2. Relationship to replay-native ML work

[`REPLAY-NATIVE-ML-PLAN.md`](REPLAY-NATIVE-ML-PLAN.md) is relevant and remains
the authoritative generic ML/data plan. Its packages are not copied into this
workload.

| Replay-native package | Relationship to Frontline |
| --- | --- |
| A — canonical observation and replay v2 | Its engine/replay seam is absorbed into Frontline Package 4 and its deferred viewer-consumer work completes in Package 8. One implementation must serve rules 0.5 and Frontline. |
| B — replay-only dataset CLI | Follow-on after Package 4's schema is frozen; not on the Frontline gameplay critical path. |
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

For duel rules, the actor key is the historical slot. For Frontline it is
`teamId + unitId + lifeId`. Design one discriminated actor identity in replay
v2 if both efforts share a rewrite window. If a closed slot-only v2 ships
first, Frontline must bump the replay format instead of silently changing v2.

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

### Before entity and lifecycle work

- `teamId`: 0 or 1.
- `unitId`: stable, team-local identity tied to Prime or fabrication slot.
- `lifeId`: monotonically increasing for each new runtime life.
- Canonical entity order: `(teamId, unitId, lifeId)`.
- A form change retains runtime memory; destruction disposes the runtime; a
  respawn/rebuild creates fresh memory and a new `lifeId`.
- Decide fuel, fault, memory, startup, and debug budgets per body versus per
  team.

### Before replication

- Fabrication location: protected home pad or adjacent to Prime.
- Canonical behavior when every eligible spawn tile is occupied.
- Child start state: empty private memory plus an explicit spawn reason is the
  recommended baseline.
- Exact team-perception union, provenance representation, and ordering.
- Whether one faulty body disqualifies that unit or the whole submitted team.

### Before Anchor

- The initial definition exposes eight-way directional fire and keeps
  programmed curves off for turrets. Freeze that arm or explicitly test a
  curve-enabled alternative before implementing Anchor; enabling it changes
  map safety validation and legal Anchor geometry.
- Health transition on Anchor; recommended first arm is `+2`, clamped to the
  turret maximum, rather than an implicit full heal.
- Irreversible for one life versus explicit self-destruction.
- Exact illegal tiles and form-dependent objective weight.

### Before replay v2 is declared stable

- Discriminated duel-slot versus Frontline-life actor identity.
- Canonical ordering for actor observations, decisions, events, and state.
- `null` versus empty semantics for every optional capability.
- Manifest placement and exact rules/map/match fingerprint coverage.
- Public actor observation versus omniscient spectator/critic separation.
- Replay-v1 reader/hash behavior and permanent fixtures.

### Before ranked or public use

- Max-tick team tiebreak.
- Artifact capability declaration and protocol negotiation.
- Named season/ruleset identity versus exact contract fingerprint for ladders.
- Replay/debug/training-data privacy and licensing.
- Required CLI and mobile release versions.

## 5. Work packages

### Package 0 — characterization shield

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

Goal: make the complete effective public rules machine-readable without
changing what bots receive yet.

Likely additions:

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

Goal: freeze the experimental rules/map input and objective math without
running full matches.

Likely additions:

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
  execution; `MatchEngine` must reject a Frontline definition until its
  dedicated session exists.

Acceptance:

- exact five-position ordering and two protected homes;
- deterministic validation before tick zero;
- sole-team binary pressure independent of body count;
- exact contested/empty decay and redeploy timing;
- reversible partial pressure;
- deterministic three-push base breach from the centre;
- stable map/rules/match fingerprints.
- eight-way turret launch headings and every enabled programmed path are
  included in the Anchor-to-Prime-spawn safety proof.

### Package 3 — Prime-only headless Frontline

Goal: play a complete two-Prime match with current movement/projectile
mechanics, respawns, Frontline advancement, and base-breach victory.

Start with a separate `FrontlineMatchSession` rather than rewriting legacy
`MatchSession`. Extract shared combat helpers only after characterization
tests prove legacy behavior remains identical.

Introduce:

- `TeamId`, `UnitId`, `LifeId`, and `FabricationSlotId`;
- `TeamState`, `UnitState`, `FrontlineState`, and lifecycle status;
- a stable-keyed joint-decision step API;
- explicit reset/step results suitable for a later training wrapper.

Version the Frontline tick order:

1. tick-start unlock, respawn, and rebuild transitions;
2. build and freeze all observations;
3. collect the joint decision;
4. validate actions and dynamic legality;
5. turn;
6. simultaneous movement;
7. advance existing projectiles;
8. launch new shots and apply simultaneous damage;
9. queue destruction/lifecycle transitions;
10. update Frontline pressure and breach;
11. apply successful fabrication/form transitions;
12. update cooldowns, faults, and budgets;
13. resolve max-tick result.

Gate: early breach, contested Frontline, repeated death/respawn, simultaneous
destruction, and max-tick fixtures are deterministic.

### Package 4 — canonical observation and replay v2 foundation

Goal: implement the engine/replay seam of replay-native ML Work Package A
once, while the engine contracts are already being rewritten. Its viewer
consumer work completes against the now-open frontend boundary in Package 8,
after replay v2 supplies the data.

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
- no same-tick allied action enters another actor's observation.

Primary current surfaces:

- `src/BotArena.Engine/BotObservation.cs`
- `src/BotArena.Engine/MatchSession.cs`
- `src/BotArena.Engine/MatchEngine.cs`
- `src/BotArena.Engine/Replay.cs`
- `src/BotArena.Engine/GameEvent.cs`
- `src/BotArena.Runtime/InProcessBotRuntime.cs`
- replay/determinism/runtime contract tests

Backend-only version dispatch and verification may land here. Frontend replay
mirrors and consumers remain deferred.

Keep replay v2 internal until the Frontline action/lifecycle variants it claims
to represent are covered. After a replay version is publicly emitted, any
structural mutation requires another replay-format bump.

Gate:

- the runtime input is a lossless projection of its replay observation plus
  static header data;
- in-process/WASM behavior parity remains for protocol 0.1;
- identical inputs produce byte-identical replay-v2 documents and hashes;
- replay-v1 fixtures verify without normalization;
- the leakage audit passes;
- no gameplay result changes on frozen legacy fixtures.

### Package 5 — entity actions and replication

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
- dispose it on destruction and create fresh memory on respawn/rebuild;
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

Goal: implement typed capabilities rather than scattered turret branches.

- Mobile Prime, mobile child, and turret definitions.
- Form-derived health, vision, movement layer, objective weight, and actions.
- Same runtime survives Anchor.
- Anchor is telegraphed, consumes the tick, and resolves at tick end only if
  the child survives.
- Turrets cannot move, capture, contest, or Anchor on prohibited tiles.
- Directional turret fire is explicit in action, replay, and observed event.

First causal arms remain:

1. Frontline only;
2. mobile children;
3. 3-HP/current-cadence turret;
4. 5-HP/faster-cadence turret;
5. range 6 versus 7 only if needed.

Gate: scripted open-ground 1v1 and coordinated 2v1 turret tests, map coverage,
and obsolete-front usefulness diagnostics.

### Package 7 — SDK and protocol vNext

Goal: deliver the new contract to independent C# bot instances.

Because SDK must not reference Engine, mirror the public types in SDK and map
them in Runtime/Guest adapters.

- Keep the manifest once per life after `MatchStart`; do not resend it every
  tick.
- Compile/load a WASM module once per submitted artifact, but create an
  isolated store and instance for each active life.
- Dispose a life instance on destruction and create a fresh one on
  respawn/rebuild.
- Spike binary encodings under NativeAOT/WASI before choosing one.
- Preserve a small negotiation/bootstrap path so the host identifies
  protocol 0.1 versus vNext rather than trusting unused metadata.
- Use framed messages with skippable unknown fields and explicit size limits.
- Preserve null/empty capability distinctions over both wire directions.

Coordinated version bumps:

- SDK;
- guest adapter and build cache;
- runtime protocol;
- runtime configuration if limits change;
- tracked built-in WASM artifact;
- CLI/package version under the release guard.

Gate: malformed frames, unknown fields, size limits, old-artifact eligibility,
WASM/in-process parity, observation/replay parity, and per-life isolation.

### Package 8 — consumers, evaluation, and experimental release

Goal: make Frontline observable and evaluable without claiming it has shipped.

Backend/CLI first:

- team-aware replay verification and summary;
- replay truncation and broadcast secrecy;
- dynamics/balance scripts consuming team/unit/life and lifecycle facts;
- rules/map/match fingerprints in diagnostics;
- clean experimental rules selection.

Frontend after replay v2 is available:

- one replay-version normalization layer;
- team/unit/life presentation through `replayPresentation.ts`;
- site/CLI/hosted-review viewer updates;
- hosted-viewer bridge and matching native mobile cards/controls;
- v1 golden fixtures plus new Frontline golden frames.

Then:

- publish a concise `EXPERIMENTAL-FRONTLINE.md`;
- commission native bot doctrines under equal budgets;
- run causal arms, dynamics analysis, and outcome-blind review;
- record a ship/hold verdict without altering current rules documentation.

The replay-native plan's dataset/corpus/assets/inference packages may proceed
after Package 4 through separate owners. They are not a duplicate Package 9
here and are not required to answer whether Frontline is fun.

## 6. Agent ownership

### First implementation wave

- **Foundation owner:** Packages 0–1, including tests.
- **Fixture/spec owner:** independently freeze compatibility and
  canonicalization vectors without editing the foundation owner's files.
- **Primary integrator:** review disclosure/canonicalization, run the full
  suite, and verify historical hashes.

Do not assign a second agent to `GameRules`, `BotArenaVersions`, or manifest
files during this wave.

### Engine wave

- One engine owner for Packages 2–3.
- One independent adversarial-test owner after public types stabilize.
- The primary integrator owns shared-combat extraction and compatibility
  review.

### Observation/replay wave

Package 4 has one contract owner spanning engine observation and replay
serialization. A separate leakage/fixture reviewer may add tests after DTOs
stabilize. Do not split observation construction and replay projection between
agents; their identity is the invariant.

### Runtime/gameplay wave

After actor/action types freeze:

- runtime/protocol owner for runtime-factory and Package 7 work;
- engine owner for fabrication, entity actions, and Anchor;
- replay owner extends lifecycle facts without changing the canonical
  observation seam;
- primary integrator owns shared identity/action types and version axes.

`MatchEngine`, `MatchSession`, `Replay.cs`, `BotArenaVersions`, shared identity
and action types, protocol twins, and version documentation remain choke
points owned by one person at a time.

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

The refactor is integrated. The first safe slice is to eliminate replay-v1
assumptions that participant array position equals stable slot identity, with
reordered and sparse-slot tests. It must not invent replay-v2 fields or change
the hosted-viewer bridge.

The remaining Frontline work will touch:

- `web/src/types.ts` — hand-maintained replay mirror;
- `web/src/replayPresentation.ts` — shared per-tick presentation derivation;
- viewer components, playback, and Canvas2D renderer under `web/src/`;
- site replay pages;
- `mobile/src/components/ArenaViewer.tsx`;
- `mobile/src/components/arena/` native cards, transport, and bridge protocol.

The renderer serves the site, CLI self-contained viewer, hosted review, and
mobile WebView. Mobile renders the arena canvas in the hosted WebView and
native controls/cards outside it. Later work must:

- preserve replay-v1 viewing and normalize new replay formats once;
- add team/unit/life identity, forms, fabrication/lifecycle state, and the
  five-position Frontline;
- derive health, objective, and timing language from the manifest;
- group events by team while retaining unit attribution;
- extend `HostedViewer` and the native bridge in lockstep;
- retain phone/desktop golden-frame and outcome-blind review coverage.

Implement the Frontline-specific portion only after replay v2 supplies stable
team/unit/life, lifecycle, form, fabrication, and objective fields. Coordinate
any hosted-viewer bridge change with the native mobile arena components in the
same integration slice.

## 8. Documentation migration

Do not rewrite historical documents as though Frontline has already shipped.

### Now

- Keep `PLAYER-GUIDE.md`, template docs, site docs, CLI help, and README on
  shipped rules 0.5.
- Keep `REPLAY-NATIVE-ML-PLAN.md` as the generic proposal and both Frontline
  plans explicitly experimental rather than shipped.
- Do not add numeric hypotheses to `DECISIONS.md`.
- Keep `PLAN-SUMMARY.md` explicit that Frontline is active experimentally
  while rules 0.5 remains current.

### Commissioned on 2026-07-27

- `GAME-DESIGN.md`, `PLAN-SUMMARY.md`, and `DECISIONS.md` identify Frontline
  as the active experiment while retaining rules 0.5 as current.
- Create concise `docs/EXPERIMENTAL-FRONTLINE.md` only after its remaining
  player-facing arms are frozen; an unresolved design plan is not a bot
  contract.
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
- Add equivalent versioned protocol documentation.
- Update `WASM-DEVELOPMENT.md` after the codec/runtime spike.
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
and `mobile/CLAUDE.md`. Replay-v2 fields still gate the actual Frontline UI.

Before an experimental server deployment:

- deterministic canonical WASM matches and zero faults;
- replay-v1 verify/view compatibility;
- replay-v2 parity, leakage, and broadcast-secrecy gates;
- protocol negotiation and old-artifact eligibility tests;
- raw/compressed replay-size and six-runtime resource measurements;
- compatible CLI published/tagged before deployment;
- coordinated mobile release if the hosted bridge/native presentation changed.

Before any ship decision, apply `EVALUATION-METHODOLOGY.md` literally:
regression sentinels, same-cohort causal arms, candidate-native doctrines,
dynamics metrics, and locked outcome-blind viewer notes.

## 10. Immediate implementation wave

Packages 0–2 now establish the historical shield, exact public contract,
format-v2 map, rules/map/topology resolver, and pure objective kernel. Official
rules 0.1–0.5, protocol 0.1, replay v1, and their canonical hashes remain
unchanged. The experimental map is deliberately absent from current App/CLI
catalogs and packages, and legacy execution rejects it rather than silently
running duel mechanics. The web and mobile changes are limited to stable replay-v1
participant lookup; they add no speculative Frontline payload or visuals.

The active dependent slice is Package 3:

1. add a separate Prime-only `FrontlineMatchSession` without changing legacy
   `MatchSession`;
2. introduce stable team/unit/life identity and a keyed joint-decision API;
3. resolve current movement/projectile combat, Prime destruction and respawn,
   binary objective presence, breach, and max-tick completion;
4. freeze only the spawn/lifecycle semantics this headless slice needs and
   leave fabrication, Anchor, shared perception, runtimes, protocol, and replay
   shapes deferred;
5. prove early breach, contest, repeated respawn, simultaneous destruction,
   symmetry, and deterministic reruns.

Package 4 then absorbs the existing replay-native observation seam when the
engine rewrite reaches observations, avoiding both a throwaway
Frontline-only data path and a second ML implementation.
