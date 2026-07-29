# Replay-native ML support: engine-rewrite integration plan

Status: shared ML/data plan, 2026-07-26; reconciled on 2026-07-28 with both
the frozen Frontline-alpha replay-v2 proof and the generic actor-match
architecture. Replay 2 remains observation-complete evidence for the local
Frontline experiment. Replay 3 is the target for all new generic modes,
formats, topology, scores, transitions, and datasets. Dataset export, corpus
access, model assets, starter inference, and hosted product decisions remain
planned.

Frontline does not create a second ML stack. Where older examples below say
`slot`, that is the legacy-duel actor identity. The generic replay-3 design
represents a runtime life as `teamId + unitId + lifeId`, carries the exact
resolved rules/map/format/topology/match fingerprints, and encodes variable
entity collections plus masks rather than assuming two bodies.

The frozen Frontline replay-2 checkpoint proves that seam: it constructs one
canonical public observation per active life, supplies that observation to the
life runtime, snapshots the same projection into experimental replay v2, and
records variable topology, lifecycle, fabrication, form transitions, dynamic
legality masks, and authoritative result facts. Actor SDK/Guest 0.9.0 and
protocol/configuration 1.0 now deliver the same contract to isolated per-life
WASM instances. The explicit local CLI can now generate and view that v2
format. This is not a claim that App/server admission, general replay
summary/verification, dataset tooling, or a hosted product can consume it
yet.

The current SDK/Guest 0.11.0 boundary negotiates the exact
`generic-actor-match-3` profile. It parses the canonical resolved contract and
exposes variable entity sets, score channels, tagged mode state, typed action
arguments, lifecycle lineage, explicit class identity, ratchet-hold state,
projectile cadence/damage, and visible spawn reservations without assuming two
players. This completes the bot-facing half of the generic ML seam. The neutral Engine host now records
the same inputs, decisions, lifecycle causality, post-state, standings, and
typed Deathmatch/Frontline terminal facts in strict replay 3. The
off-by-default hosted Frontline Labs slice now proves replay-3 persistence and
broadcast delivery; dataset export, public corpora, starter models, broader
mode admission, and ranked product layers remain separate follow-ons.

## Executive conclusion

nilbots does not need different game rules or larger runtime limits to support
neural-network bots. It needs a better data and packaging contract.

A scratch bot using a 128 → 96 → 48 → 5 MLP (17,136 scalar weights) was compiled
through the official NativeAOT/WASM pipeline and run for 80 ticks:

| Measurement | Result | Current limit |
| --- | ---: | ---: |
| Peak fuel per tick | 1.1 M | 200 M |
| WASM artifact | 2.54 MiB | 16 MiB |
| Weight storage at float32 | about 67 KiB | 64 MiB runtime memory |
| Faults | 0 | 3 faults disqualify |

The weights were deterministic scratch values, so this proves execution
feasibility, not playing strength. The result is nevertheless decisive:
inference capacity is not the blocker. Training-data ergonomics, public access,
and model packaging are.

The engine rewrite should include the durable seam now:

1. construct one canonical, public-only observation per active actor and tick;
2. pass that same observation to the runtime and snapshot it directly into the
   replay before the tick resolves;
3. make generic replay v3 observation-complete without requiring historical
   engine logic to reconstruct training inputs, retaining replay v2 as the
   frozen Frontline-alpha proof;
4. enforce field parity and information-leakage tests.

Dataset commands, corpus download, model assets, and a starter trainer can land
after the rewrite. They depend on the seam above; the seam does not depend on
them.

## Product and fairness contract

The intended product invariant is:

> Every state- or rules-derived gameplay input supplied to a runtime actor is
> represented in the public replay for that exact actor identity and tick.

The surrounding fairness rules are:

- actor inputs in official training examples come only from the recorded
  per-actor observation and the referenced immutable public match contract;
- decisions, outcomes, and omniscient post-match state may be used as
  labels/rewards because they are public replay facts;
- every broadcast-complete ranked replay and submitted WASM artifact is
  publicly discoverable and downloadable;
- source, model-asset, artifact, memory, and fuel limits are identical for
  every participant;
- official tooling never requires direct access to live `GameState`;
- adding an observation field is incomplete until replay and dataset surfaces
  carry it;
- private compute cannot be equalized, but information, tools, inputs, and
  runtime budgets can be.

This is an information-parity contract, not a promise that all players own the
same training hardware or use the same technique.

## Shipped replay-v1 gap and local experimental checkpoint

The shipped replay v1 has much of the underlying information, but not the
exact input record a trainer needs. That gap remains for historical duel
matches and is why official dataset export must not pretend v1 is
observation-complete.

`MatchSession.BuildObservation` constructs a pre-tick `BotObservation`.
`MatchEngine.BuildReplayTick` later records:

- chosen and validated actions;
- action result and fault state;
- visible tile positions;
- visible enemies;
- non-empty heard sounds;
- omniscient post-tick state, events, and projectiles.

It does not directly record:

- the bot's complete pre-tick own state;
- `PreviousActionResult`;
- visible projectiles in the exact public shape;
- visible events in the exact public shape;
- empty-versus-unavailable capability semantics;
- dynamic pressure limit and all historical optional observation fields.

A sophisticated consumer can reconstruct most of this by combining the
previous tick's post-state, the current visible-tile set, global events,
projectiles, map header, and the matching historical `GameRules`. That is not
a fair developer contract. It rewards engine reverse-engineering and creates
off-by-one and version-drift traps.

There is a second divergence to remove during the rewrite:
`BotObservation.VisibleEvents` currently carries engine `GameEvent` records,
while the WASM and in-process adapters independently reduce them to the
player-visible kind, acting slot, and primary position. The engine/runtime
boundary should already be public-only.

The separate Frontline path closes both gaps on the local experiment with
`ActorObservation`, `FrontlineObservationProjector`, `ActorRuntime`, and
replay v2. It deliberately leaves the shipped duel `BotObservation`,
protocol 0.1, and replay-v1 bytes untouched. Experimental actor protocol 1.0
now consumes the new public-only path rather than reconstructing inputs from
omniscient state; later product integration must preserve that boundary.

## Rewrite inclusion boundary

### Must land in the engine rewrite

1. A canonical public-only engine observation model.
2. A single observation construction point.
3. Direct observation snapshots in the observation-complete replay generation
   (Frontline-alpha v2 evidence; generic v3 target).
4. Explicit pre-tick/post-tick semantics.
5. Observation/replay/runtime parity tests.
6. Information-leakage tests.
7. Replay v1 read/verify compatibility.

### Helpful to prepare in the rewrite

- a stable `ReplayObservationProjection` or equivalent mapper;
- a tick-frame structure that naturally groups pre-tick observations,
  decisions, resolution, and post-tick state;
- an observation schema that preserves `null` versus empty collections;
- replay DTOs isolated from mutable internal simulation types.

### Defer from the engine rewrite

- Python, PyTorch, ONNX, or other training-framework integration;
- public corpus pagination and bulk download;
- high-throughput rollout orchestration;
- model-asset upload and controlled-build support;
- reusable neural inference primitives;
- any game-rules or sandbox-limit change.

The rewrite agent should preserve the invariants, not necessarily the type
names proposed below.

## Target engine flow

The preferred control flow is:

```text
authoritative pre-tick state
    -> build one public actor observation per active actor identity
        -> snapshot ReplayBotObservation
        -> execute that runtime with the same observation instance/projection
    -> collect one decision per active actor identity
    -> resolve the tick
    -> snapshot resolution, events, and post-tick state
```

If the rewrite introduces a tick-frame object, its conceptual shape should be:

```text
TickFrame
  Tick
  PreTickObservations[actorIdentity]
  Decisions[actorIdentity]
  Resolutions[actorIdentity]
  Events
  PostTickState
```

Replay assembly must not recompute visibility, hearing, projectile exposure,
event exposure, or previous state. It serializes the observations already sent
to runtimes. Actor collections use explicit identities and deterministic
ordering; array position is never identity.

## Canonical public observation

`BotArena.Engine` remains independent of `BotArena.Sdk`; the two-model boundary
stays. The engine-side observation must, however, contain only information the
runtime is allowed to deliver.

Introduce an engine-side public event record such as:

```csharp
public readonly record struct ObservedEvent(
    GameEventType Type,
    int? Slot,
    Position Position);
```

`MatchSession` performs event visibility filtering and redaction once.
Runtime adapters map `ObservedEvent` to their duplicated SDK/wire type; they no
longer receive the authoritative `GameEvent`.

The same rule applies to projectiles. An observed projectile contains:

- current position;
- public launch direction;
- owner actor identity (legacy slot or Frontline team/unit/life as applicable);
- tiles per advance;
- ticks until advance;
- remaining tiles;
- currently manifested heading.

It never contains its committed future programmed path.

### Capability semantics

Collections and capabilities must distinguish:

- `null`: the active rules/runtime do not provide this capability;
- `[]`: the capability exists, but nothing is currently observed.

This distinction matters for `VisibleProjectiles`, `HeardSounds`, future
capabilities, and training masks. Replay serialization must preserve it.

`Random` and `Debug` are services, not observable values. They are explicit
exceptions to observation parity. The replay already carries match seed,
rules/version axes, chosen action, and public debug output.

## Observation-complete replay generations

Replay v2 adds an explicit `observation` under each Frontline-alpha actor tick
and is now frozen as experimental evidence. Replay v3 retains that chronology
but replaces Frontline-specific objective, event, world-state, and result
fields with the generic resolved contract, score map, standings, lifecycle
lineage, and tagged mode state/result. Each replay DTO remains separate from
internal observations so replay stability does not freeze engine types.

Across generations, actor identity is explicit:

- legacy duel: submitted-participant/body `slot`;
- actor replay 2/3: `teamId + unitId + lifeId`, with the stable unit slot and
  parent/generation facts preserving fabrication and replication lineage.

Replay 3 also records the exact life-origin reason. A parentless declared
automatic activation is therefore distinguishable from tick-zero deployment,
post-destruction return, fabrication, and replication. Dataset exporters
should retain this categorical lifecycle input rather than infer it from the
life ID or current body count.

The schema must not encode one fixed body count. Allies, enemies, projectiles,
objectives, forms, and future action targets are ordered collections with
presence/legality masks. Counts such as teams, submitted participants, and
stable unit slots come from the exact public match contract, not from the
currently visible ally collection.

Suggested dynamic shape:

```text
ReplayBotObservation
  x
  y
  facing
  health
  cooldown
  energy?
  myZoneTicks?
  enemyZoneTicks?
  controlPressure?
  controlPressureLimit?
  previousActionResult
  visibleTiles[]          { x, y, isWall }
  visibleEnemies[]        { actorIdentity, x, y, facing, health, form? }
  visibleProjectiles[]?   exact public projectile fields
  visibleEvents[]         { type, actorIdentity?, x, y }
  heardSounds[]?          { type, bearing, distance }
```

`ReplayActorTick` becomes:

```text
actorIdentity
observation
chosenAction
shotProgram?
validatedAction
result
faulted
debug?
```

Static observation inputs remain in the header and are joined by the dataset
exporter without invoking rules logic:

- exact public rules/map/match-contract manifests and fingerprints;
- exact scoring-team, submitted-participant, stable-unit-slot, and initial-life
  topology;
- map width, height, and tile rows;
- objective geometry;
- action/form catalogs, programmed-shot limits, and other public rule inputs;

The header also carries non-observation provenance needed to interpret and
group episodes:

- rules and protocol versions;
- participant/artifact and spawn/provenance data.

If the rewrite introduces new static per-match bot inputs, place them in the
header and include them in the parity test.

### Timing semantics

For replay tick `t` and actor `a`:

- `actors[a].observation` is the complete pre-tick observation used to choose
  the action on tick `t`;
- `actors[a].chosenAction` is the runtime reply to that observation;
- `actors[a].validatedAction` and `result` describe validation/resolution on
  tick `t`;
- `state`, `events`, `projectiles`, `projectileTraversals`, and shared control
  values at tick level remain post-tick truth.

This removes the current requirement to use tick `t - 1` post-state to infer
tick `t` actor input.

### No-leakage rules

The per-actor observation must never include:

- the opponent's pending action or private bot memory;
- unseen opponent position, facing, health, or exact event location;
- a programmed projectile's unrevealed future path;
- omniscient projectiles outside the visible set;
- future events, outcome, or post-tick state;
- a more detailed event than the SDK/WASM bot received.

Omniscient replay-level state may remain outside `observation` for playback,
verification, labels, and reward construction.

## Replay compatibility and versioning

Adopting this plan changes replay shape and hash bytes. It does not change
gameplay.

Pinned axes:

| Axis | Change |
| --- | --- |
| Game rules | no change |
| Runtime protocol | no change for replay-only duel work; experimental Frontline delivery uses separate actor 1.0 |
| Runtime configuration | no change for replay-only duel work; experimental Frontline actor limits are configuration 1.0 |
| Replay format | `1` shipped historical; `2` frozen Frontline-alpha; `3` generic actor match |
| Engine version | minor bump |
| SDK | Frontline-alpha remains SDK 0.9.0; the separate generic actor-match profile is SDK 0.10.0 |
| CLI | bump because the replay viewer/CLI compatibility surface changes |

Requirements:

- detect replay version before deserializing;
- keep dedicated v1 and v2 readers/verifiers so historical and alpha hashes
  remain valid;
- write v3 for new generic actor matches; do not relabel replay v2;
- verify every stored generation in its original bytes before normalization;
- make web, mobile, and CLI viewer code normalize v1/v2/v3 into one internal
  playback model;
- keep representative v1 and v2 fixtures permanently;
- update `docs/REPLAY-FORMAT.md` rather than silently changing its version-1
  contract;
- follow the CLI release guard: publish the compatible CLI before deploying a
  server that emits a newer replay generation.

Exact dataset export may support observation-complete v2 and v3 with an
explicit generation tag; all new generic rollout output is v3. A v1 replay
may remain viewable and analyzable, but official tooling labels it
`legacy-partial` rather than pretending reconstruction is exact.

## Work package A — observation and replay foundation

Status: **implemented for the frozen local Frontline replay-v2 proof and the
generic actor-match replay-3 seam**. The remaining work begins with
dataset/product delivery. Replay v1 remains a dedicated historical
reader/verifier and replay v2 remains a dedicated alpha reader/verifier.

### Implementation

- define an observed/redacted actor event without changing legacy
  `BotObservation` or replay v1;
- centralize the Frontline public observation projection;
- preserve replay-v2 DTOs and define generic replay-v3 DTOs;
- snapshot observations immediately before runtime execution;
- retain post-tick authoritative replay sections;
- implement strict v3 serialization/validation beside unchanged v1/v2 hash
  verification;
- update the TypeScript replay mirror and all viewer consumers;
- deliver the same typed observation through actor SDK/Guest and canonical
  per-life WASM;
- document the format, timing, and runtime protocol rules.

### Primary current and later-generalization surfaces

- `src/BotArena.Engine/ActorObservation.cs`
- `src/BotArena.Engine/ActorRuntime.cs`
- `src/BotArena.Engine/FrontlineObservationProjector.cs`
- `src/BotArena.Engine/ReplayV2.cs` and `ReplayV2Serializer.cs` (frozen);
- generic replay-3 DTO, projection, serializer, and validator files;
- `src/BotArena.Sdk/IActorBot.cs`
- `src/BotArena.Engine/BotObservation.cs`
- `src/BotArena.Engine/MatchSession.cs`
- `src/BotArena.Engine/MatchEngine.cs`
- `src/BotArena.Engine/Replay.cs`
- `src/BotArena.Engine/GameEvent.cs`
- `src/BotArena.Runtime/InProcessBotRuntime.cs`
- `src/BotArena.Runtime.Wasm/WasmProtocol.cs`
- `src/BotArena.Guest/GuestProtocol.cs`
- `src/BotArena.Runtime/InProcessActorRuntime.cs`
- `src/BotArena.Runtime.Wasm/WasmActorRuntime.cs`
- `src/BotArena.Guest/ActorGuestProtocol.cs`
- `src/BotArena.Sdk/ActorWireProtocol.cs`
- versioned `web/src/replayWireV1.ts`, `replayWireV2.ts`,
  `replayWireV3.ts`, and shared `replayModel.ts`;
- web and mobile replay playback adapters
- `docs/REPLAY-FORMAT.md`
- engine, determinism, WASM contract, and viewer fixture tests

### Exit criteria

- the runtime input is a lossless projection of the replay observation plus
  static header inputs, with tick and explicit actor identity supplied by their
  replay parents;
- legacy and actor in-process/WASM behavior parity remains;
- same Frontline-alpha inputs still produce byte-identical v2 replays and
  generic inputs produce byte-identical v3 replays;
- v1 and v2 fixture verification remains byte-identical;
- actor observations pass the leakage audit;
- no gameplay result changes on frozen artifacts/maps/seeds unless the rewrite
  separately and deliberately versions gameplay.

The local experimental Frontline implementation meets these engine/replay
criteria with deterministic fixtures, strict Engine and TypeScript semantic
validators, a version-neutral viewer model, and frozen replay-v1
compatibility shields. This does not complete the end-to-end ML-friendly gate:
dataset/corpus commands, model packaging, starter inference, and hosted
release remain Work packages B–E.

## Work package B — replay-only dataset CLI

This can follow the rewrite without touching simulation.

Add:

```bash
nilbots dataset inspect <replay-or-directory>
nilbots dataset export <replay-or-directory> --out trajectories.jsonl
nilbots dataset export <path> --perspective <actor|team|all>
```

Dataset format 1 should be raw, model-neutral JSONL. Each row contains:

```text
datasetVersion
replayHash
rulesVersion
mapId
seed
tick
actorIdentity
observation
chosenAction and shotProgram
validatedAction
actionResult
terminal outcome
```

Rules:

- verify replay hashes before export;
- accept observation-complete replay v2 and v3 with an explicit source
  generation; prefer v3 for new generic corpora;
- never instantiate `MatchSession` or recompute visibility/rules;
- produce deterministic row order and byte-identical output for identical
  ordered inputs;
- preserve episode identity as `(replayHash, actorIdentity)` and sequence
  order by tick;
- export raw game values, not normalized tensors.

The implementation belongs in a new focused CLI file rather than a
grandfathered grouped command file.

Exit test: run the packaged CLI in a clean directory containing only public
replays; it must produce complete trajectories without the repository or
engine source.

## Work package C — public corpus access

Individual replay and artifact reads are already public after broadcast.
Discovery must become equally usable.

Add a broadcast-safe paginated match/replay index:

```http
GET /api/matches?rules=0.5&status=completed&cursor=...&take=100
```

Then add:

```bash
nilbots dataset pull --server https://nilbots.com --rules 0.5 --out replays/
```

Requirements:

- only broadcast-complete matches;
- filters for rules, map, bot, and creation window;
- opaque stable cursor, preferably `(CreatedAt, Id)` encoded by the server;
- resume-safe downloads and replay-hash validation;
- deduplication by replay hash;
- named response DTOs and regenerated API clients;
- no sources, private build logs, account-private data, or pre-broadcast
  outcomes;
- long-term replay retention or immutable periodic corpus manifests.

At larger volume, publish versioned corpus snapshots containing replay hashes
and object keys/URLs. Do not make every trainer rediscover the archive by
scraping the UI.

## Work package D — bounded model assets

Today submission accepts only C# source (16 files, 256 KiB total). Embedding
Base64 weights in source is feasible but poor product ergonomics.

Proposed project manifest:

```json
{
  "name": "NeuralBot",
  "entryType": "NeuralBot",
  "sdkVersion": "0.10.0",
  "assets": ["model.nilmodel"]
}
```

Proposed limits:

- at most four assets;
- at most 1 MiB combined;
- normalized relative paths only;
- assets are embedded resources, never host-executed inputs;
- assets participate in source/build identity and provenance;
- existing 16 MiB artifact and 64 MiB runtime ceilings remain.

### Required spike

Before designing the whole submission path:

1. embed deterministic 1 KiB and 1 MiB resources in the controlled project;
2. load and hash them from a NativeAOT/WASM bot;
3. measure artifact size, startup fuel, per-tick fuel, memory, trimming, and
   local/server artifact parity.

Preferred implementation order:

1. NativeAOT embedded resource;
2. toolchain-generated chunked byte source that does not count against player
   source quota;
3. a new WASM ABI import only if both fail.

### Build and server shape

- replace source-only build input with explicit text-source and binary-asset
  collections;
- hash relative paths, kinds, lengths, and raw bytes into the build-cache key;
- make `BotProject` load only manifest-listed assets;
- generate controlled-project resource entries;
- extend local build, CLI submit, server admission, compiler queue, and
  compiler runner in lockstep;
- store a deterministic immutable submission bundle in `IObjectStore` by
  hash rather than adding large Base64 payloads permanently to PostgreSQL;
- snapshot the bundle key/hash on `BotVersion`;
- retain owner-only access to submitted inputs and public access to the
  compiled WASM artifact;
- cover path traversal, duplicate names, oversized inputs, malformed payloads,
  queue recovery, cache invalidation, and hostile binary contents.

This requires a build-pipeline and CLI version bump. It does not require a
runtime-protocol change when resources compile into the module.

## Work package E — starter inference and rollout

Do not make a large ML framework part of the submitted artifact.

Start with:

- a documented asset loader;
- example allocation-free dense and recurrent inference source;
- int8/fixed-point weights with explicit scales;
- action masking;
- deterministic argmax or sampling through `context.Random`;
- reusable buffers allocated once per match.

Keep the first inference implementation in an example/template rather than
locking a broad neural API into `BotArena.Sdk`. Promote small primitives only
after more than one real bot needs the same stable abstraction. Trimming the
two-assembly controlled-build closure remains valuable.

Add a reference `examples/ml-bot/` containing:

- observation-complete replay-v2/v3 data loading with explicit generation;
- a small recurrent behavior-cloning trainer;
- held-out seed splitting;
- an int8 exporter;
- C# model loading/inference;
- fuel, determinism, and artifact-size checks.

Add high-throughput replay generation:

```bash
nilbots rollout \
  --bot ./MyBot \
  --opponents pincer.wasm,echo.wasm \
  --maps ranked \
  --seed-range 1:10000 \
  --out replays/ \
  --no-viewer
```

The rollout command may parallelize matches, but its seed allocation,
filenames, manifest, and output ordering must be deterministic. New generic
rollouts emit canonical replay v3; the tool must not expose a privileged
side-channel.

## Test matrix

### Observation parity

- engine observation equals replay observation;
- in-process and WASM receive equivalent public fields;
- capability `null`/empty states round-trip;
- previous action result and pressure timing are pre-tick correct;
- visible events use the same primary-position semantics as the SDK/wire;
- visible projectile heading is current-only.

### Replay compatibility

- v2 and v3 canonical golden files;
- unchanged v1 golden fixtures and hashes;
- draw/null optional fields;
- oldest supported rules;
- current 0.5 capabilities;
- maximum-length match;
- web, CLI, and mobile fixture loading.

### Leakage

- unseen enemies absent;
- unseen projectiles absent;
- future programmed paths absent from actor observation;
- pending opponent actions absent;
- post-tick damage and outcome absent from the pre-tick observation;
- broadcast partial responses contain no later tick or final result.

### Dataset

- replay hash required and verified;
- v1 rejected or explicitly marked partial;
- deterministic JSONL bytes;
- every selected actor preserves an independent life sequence, and team views
  preserve stable unit/life lineage;
- clean-directory export uses no engine simulation;
- new observation fields fail a drift test until the exporter handles them.

### Model assets

- local/server cache identity includes bytes and paths;
- asset-only change rebuilds and changes artifact hash;
- embedded bytes survive trimming;
- exact size/file/path limits;
- compiler isolation and allowed WASM imports unchanged;
- public artifact remains executable under current memory/fuel limits.

## Release and acceptance gates

### Engine/replay gate

- all frozen gameplay outcome tests pass;
- deterministic reruns preserve frozen v2 JSON/hashes and produce identical
  generic v3 JSON/hashes;
- current in-process/WASM contract tests pass;
- v1 and v2 verify/view remain supported;
- actor observation leakage audit passes;
- raw and compressed representative replay-size changes are measured and
  recorded before rollout;
- compatible CLI is published before a server emits v3.

### End-to-end ML-friendly gate

Prove the journey in a clean environment:

1. discover and download only public replays;
2. export trajectories with the packaged CLI;
3. train the reference model without engine/repository access;
4. export a bounded model asset;
5. build locally and server-side with expected artifact parity;
6. run the official WASM artifact with zero faults and deterministic reruns;
7. keep peak fuel below a conservative 20 M/tick target;
8. stay below 256 KiB source, 1 MiB assets, and 16 MiB final artifact;
9. outperform the same model's random initialization on held-out maps/seeds;
10. mechanically prove that every actor feature originated in
    `ReplayBotObservation` plus static replay header data.

This gate proves accessibility and integrity, not championship strength.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Replay payload growth | Store static inputs once in the header; measure raw and compressed v1/v2/v3 corpora before rollout; do not duplicate global post-state inside each observation |
| Replay DTO freezes engine internals | Use an explicit projection DTO rather than serializing internal state types |
| Adapter/replay drift | Build one public-only engine observation and add reflection/field parity tests |
| Historical replay breakage | Version-dispatched v1/v2 readers and verifiers plus permanent golden fixtures |
| Hidden information leaks into training input | Separate per-actor observation from omniscient replay truth and test forbidden fields |
| Old artifacts ignore newer optional wire sections | Replay records the canonical host observation; participant/toolchain provenance identifies older adapters, and current official tooling can filter by compatibility |
| Model assets weaken build isolation | Treat bytes as bounded inert resources, validate before workspace creation, and preserve the controlled build/import validator |
| Official example becomes the only viable strategy | Keep raw replay/data contracts framework-neutral and inference helpers optional |
| Players overfit deterministic seeds | Starter uses disjoint seed blocks, mirrored sides, all ranked maps, and historical opponent/checkpoint pools |
| Public corpus creates pre-broadcast leakage | Index only `BroadcastComplete` matches through the existing broadcast-safe projection boundary |

## Proposed delivery order

1. **Engine/runtime integration — implemented for local experimental
   Frontline:** canonical observation, direct replay snapshot, v2 schema, v1
   compatibility, actor protocol/WASM delivery, local CLI generation/viewing,
   and parity/leakage tests.
2. **Generic actor delivery and replay 3 — implemented architecture
   prerequisite:** typed SDK/Guest contract, common host, generic
   scores/results/events, and version-dispatched normalization.
3. **Replay-only dataset CLI — next ML slice:** inspect/export and
   clean-environment proof.
4. **Public corpus access:** cursor API, generated clients, dataset pull.
5. **Model-asset spike and pipeline:** local/server/resource/provenance path.
6. **Starter ML bot and rollout:** replay-only trainer, exporter, inference,
   high-throughput replay generation.
7. **Dogfood report:** held-out evaluation, fuel/artifact measurements, DX
   findings, and final decision entries.

The first two items are the durable seam worth absorbing into a major engine
rewrite. Items 3–7 should remain vertical follow-ons unless the rewrite
explicitly owns those product surfaces.

## Documentation and decision surfaces

If adopted, update:

- `docs/DECISIONS.md` with the observation-complete replay invariant, replay
  generation policy, asset limits, and public corpus policy;
- `docs/REPLAY-FORMAT.md` with version-dispatched v1/v2/v3 contracts;
- `docs/PLAN-SUMMARY.md` status;
- `CLAUDE.md` invariants and compatibility/version surfaces;
- site and template documentation;
- CLI help and packaged README;
- versioned web wire types, the normalized replay model, and all replay
  fixtures;
- OpenAPI contracts/generated clients for corpus and submission changes.

Do not record these as final decisions until the rewrite owner/product owner
accepts them.

## Non-goals

- changing official rules 0.5;
- increasing fuel, memory, source, or artifact limits except for the separate
  bounded model-asset allowance;
- server-hosted training;
- GPU inference;
- arbitrary player NuGet/native dependencies;
- exposing private bot source;
- privileged live-state training APIs;
- guaranteeing equal private compute;
- making the engine depend on an ML framework.
