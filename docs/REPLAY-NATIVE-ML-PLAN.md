# Replay-native ML support: engine-rewrite integration plan

Status: shared proposal for the concurrent engine rewrite, 2026-07-26;
reconciled with the Frontline public-contract foundation on 2026-07-27. This
document is the authoritative generic ML/data plan. It records investigation
conclusions and an implementation plan; it does not yet pin replay-v2, corpus,
or model-asset product decisions in `DECISIONS.md`.

Frontline does not create a second ML stack. Where older examples below say
`slot`, that is the legacy-duel actor identity. The common v2 design must also
represent a Frontline runtime life as `teamId + unitId + lifeId`, carry the
exact public match manifest/fingerprint, and encode variable entity
collections plus masks rather than assuming two bodies.

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
3. make replay v2 observation-complete without requiring historical engine
   logic to reconstruct training inputs;
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

## Current gap

The current replay has much of the underlying information, but not the exact
input record a trainer needs.

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

## Rewrite inclusion boundary

### Must land in the engine rewrite

1. A canonical public-only engine observation model.
2. A single observation construction point.
3. Direct observation snapshots in replay v2.
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

## Replay v2 schema

Replay v2 adds an explicit `observation` under each actor tick. The replay DTO
is separate from the internal observation so replay stability does not freeze
all engine implementation types.

The actor identity is discriminated:

- legacy duel: submitted-participant/body `slot`;
- Frontline: `teamId + unitId + lifeId`, with the stable unit slot preserving
  fabrication lineage across lives.

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

Recommended axes:

| Axis | Change |
| --- | --- |
| Game rules | no change |
| Runtime protocol | no change if the wire observation is unchanged |
| Runtime configuration | no change |
| Replay format | `1 -> 2` |
| Engine version | minor bump |
| SDK | no change for engine/replay work alone |
| CLI | bump because the replay viewer/CLI compatibility surface changes |

Requirements:

- detect replay version before deserializing;
- keep a dedicated v1 reader/verifier so historical hashes remain valid;
- write only v2 for new matches after rollout;
- do not normalize a v1 document to v2 before verifying its stored hash;
- make web, mobile, and CLI viewer code normalize v1/v2 into their internal
  playback model;
- keep representative v1 fixtures permanently;
- update `docs/REPLAY-FORMAT.md` rather than silently changing its version-1
  contract;
- follow the CLI release guard: publish the compatible CLI before deploying a
  server that emits v2 replays.

Exact dataset export should support v2 only. A v1 replay may remain viewable
and analyzable, but official tooling should label it `legacy-partial` rather
than pretending reconstruction is exact.

## Work package A — observation and replay foundation

This is the engine-rewrite package.

### Implementation

- replace authoritative `GameEvent` exposure inside `BotObservation` with an
  observed/redacted event type;
- centralize the public observation projection;
- define replay v2 observation DTOs;
- snapshot observations immediately before runtime execution;
- retain post-tick authoritative replay sections;
- implement v1/v2 serializer dispatch and hash verification;
- update the TypeScript replay mirror and all viewer consumers;
- document the format and timing rules.

### Primary current surfaces

- `src/BotArena.Engine/BotObservation.cs`
- `src/BotArena.Engine/MatchSession.cs`
- `src/BotArena.Engine/MatchEngine.cs`
- `src/BotArena.Engine/Replay.cs`
- `src/BotArena.Engine/GameEvent.cs`
- `src/BotArena.Runtime/InProcessBotRuntime.cs`
- `src/BotArena.Runtime.Wasm/WasmProtocol.cs`
- `src/BotArena.Guest/GuestProtocol.cs`
- `web/src/types.ts`
- web and mobile replay playback adapters
- `docs/REPLAY-FORMAT.md`
- engine, determinism, WASM contract, and viewer fixture tests

### Exit criteria

- the runtime input is a lossless projection of the replay observation plus
  static header inputs, with tick and explicit actor identity supplied by their
  replay parents;
- in-process and WASM behavior parity remains;
- same inputs still produce byte-identical v2 replays;
- v1 fixture verification remains byte-identical;
- actor observations pass the leakage audit;
- no gameplay result changes on frozen artifacts/maps/seeds unless the rewrite
  separately and deliberately versions gameplay.

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
- accept observation-complete replay v2 only by default;
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
  "sdkVersion": "0.9.0",
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

- replay-v2-only data loading;
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
filenames, manifest, and output ordering must be deterministic. Its only
training output is canonical replay v2; it must not expose a privileged
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

- v2 canonical golden files;
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
- deterministic reruns produce identical v2 JSON and hashes;
- current in-process/WASM contract tests pass;
- v1 verify/view remains supported;
- actor observation leakage audit passes;
- raw and compressed representative replay-size changes are measured and
  recorded before rollout;
- compatible CLI is published before a server emits v2.

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
| Replay payload growth | Store static inputs once in the header; measure raw and compressed v1/v2 corpora before rollout; do not duplicate global post-state inside each observation |
| Replay DTO freezes engine internals | Use an explicit projection DTO rather than serializing internal state types |
| Adapter/replay drift | Build one public-only engine observation and add reflection/field parity tests |
| Historical replay breakage | Version-dispatched v1 reader/verifier plus permanent golden fixtures |
| Hidden information leaks into training input | Separate per-actor observation from omniscient replay truth and test forbidden fields |
| Old artifacts ignore newer optional wire sections | Replay records the canonical host observation; participant/toolchain provenance identifies older adapters, and current official tooling can filter by compatibility |
| Model assets weaken build isolation | Treat bytes as bounded inert resources, validate before workspace creation, and preserve the controlled build/import validator |
| Official example becomes the only viable strategy | Keep raw replay/data contracts framework-neutral and inference helpers optional |
| Players overfit deterministic seeds | Starter uses disjoint seed blocks, mirrored sides, all ranked maps, and historical opponent/checkpoint pools |
| Public corpus creates pre-broadcast leakage | Index only `BroadcastComplete` matches through the existing broadcast-safe projection boundary |

## Proposed delivery order

1. **Engine rewrite integration:** canonical observation, direct replay
   snapshot, v2 schema, v1 compatibility, parity/leakage tests.
2. **Replay-only dataset CLI:** inspect/export and clean-environment proof.
3. **Public corpus access:** cursor API, generated clients, dataset pull.
4. **Model-asset spike and pipeline:** local/server/resource/provenance path.
5. **Starter ML bot and rollout:** replay-only trainer, exporter, inference,
   high-throughput replay generation.
6. **Dogfood report:** held-out evaluation, fuel/artifact measurements, DX
   findings, and final decision entries.

The first item is the part worth absorbing into a major engine rewrite.
Items 2–6 should remain vertical follow-ons unless the rewrite explicitly
owns those product surfaces.

## Documentation and decision surfaces

If adopted, update:

- `docs/DECISIONS.md` with the observation-complete replay invariant, replay
  v2 choice, asset limits, and public corpus policy;
- `docs/REPLAY-FORMAT.md` with a version-dispatched v1/v2 contract;
- `docs/PLAN-SUMMARY.md` status;
- `CLAUDE.md` invariants and compatibility/version surfaces;
- site and template documentation;
- CLI help and packaged README;
- `web/src/types.ts` plus all replay fixtures;
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
