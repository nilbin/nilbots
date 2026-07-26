# Frontline rewrite — exploratory product and architecture plan

Status: **brainstorm / exploratory**, 2026-07-26. Nothing in this document is
shipped, pre-registered, or a decision-log entry. Numeric values are starting
hypotheses for experiments, not balance conclusions.

This is a possible major successor to the current duel rules. It preserves
nilbots' deterministic tile combat while changing the match from one body
versus one body into a territorial contest between two submitted
intelligences that may each replicate into several independent runtime
instances.

## Relationship to current plans

These documents have separate jobs:

- [`PLAYER-GUIDE.md`](PLAYER-GUIDE.md) is the current shipped rules 0.5
  contract. Frontline does not supersede it while this proposal is
  experimental.
- This document owns the proposed game, its public contract, and the
  Frontline-specific architecture constraints.
- [`FRONTLINE-IMPLEMENTATION-PLAN.md`](FRONTLINE-IMPLEMENTATION-PLAN.md) owns
  code sequencing, ownership, compatibility gates, and documentation
  migration.
- [`REPLAY-NATIVE-ML-PLAN.md`](REPLAY-NATIVE-ML-PLAN.md) remains the
  authoritative generic plan for canonical public observations,
  observation-complete replay v2, dataset/corpus tooling, model assets, and
  starter inference. This document specifies only the extra identity,
  variable-entity, rules-manifest, and reward-fact requirements introduced by
  Frontline.
- [`EVALUATION-METHODOLOGY.md`](EVALUATION-METHODOLOGY.md) owns the evidence
  standard for a product verdict.

In particular, Frontline must not grow a second replay exporter, Python
environment, asset format, or inference stack. The replay-native ML work is
one platform capability shared by duel and Frontline rules.

The product goals, in order, are:

1. fun to write;
2. fun to watch;
3. simple enough that a first bot can play competently;
4. strategically expandable across later maps and seasons;
5. first-class support for scripted, ML, and neural-network policies;
6. exact deterministic replay and training-data provenance.

## 1. Game hypothesis

### 1.1 Frontline

Five objective positions connect the two bases. One position is active,
starting in the centre. Sole team control pushes the active position one step
toward the opponent. Pushing through the final position breaches the base and
wins.

Destruction creates a respawn/rebuild window rather than ending the match.
Kills create time and territorial opportunity; they are not points by
themselves.

Early and midgame wins must remain possible. Escalation is what close games
experience, not three chapters every replay is forced to complete.

### 1.2 Replication

Each team begins with one Prime. Equal, fixed-time fabrication slots unlock
during the match:

- opening: Prime only;
- midgame: one child may exist;
- late game: a second child may exist;
- initial hard cap: three active bodies per team.

Every body is an independent runtime instance of the exact same artifact and
policy. Each has its own identity, action, runtime state, and recurrent
memory. All allied instances receive the engine-merged team perception for
that tick.

This is not an RTS controller returning a joint action. It is one submitted
intelligence instantiated in several bodies.

### 1.3 Anchoring

A child may irreversibly Anchor for its current life and become a turret.
Anchoring changes the child's form; it does not replace the submitted policy
with host AI.

The anchored instance continues to execute the same artifact, but with a
different legal-action set and body:

- stationary;
- substantially tougher than a mobile child;
- 360-degree line-of-sight perception;
- may select a firing direction without spending a separate turn;
- does not capture or contest the frontline;
- continues contributing its perception to the whole team;
- occupies its fabrication slot until destroyed and rebuilt.

The intended first relationship is deliberately strong: one turret should
beat one mobile body in open direct combat; two coordinated mobile bodies
should normally be able to dismantle it with losses or route around it.

## 2. First balance envelope

All values below are test-arm seeds.

### 2.1 Timing

Viewer presentation remains approximately five simulation ticks per second.

| Value | Starting hypothesis |
| --- | ---: |
| Capture threshold | ±15 sole-mobile occupancy ticks |
| Empty/contested decay | 1 toward zero every 2 ticks |
| Post-capture redeploy pause | 5 ticks |
| Prime respawn | 18 ticks |
| Destroyed child/turret rebuild | 30 ticks |
| First fabrication unlock | tick 120 / 24 seconds |
| Second fabrication unlock | tick 260 / 52 seconds |
| Maximum | tick 500 / 100 seconds |
| Target native-field median | 250–375 ticks / 50–75 seconds |
| Target p90 | below tick 500 |

An unopposed three-position sweep should take roughly 60–90 ticks including
travel and redeploy pauses. That leaves room for a genuine early win before
the first fabrication unlock. A defended early win should require repeated
outplay rather than one lucky hit.

Early success moves the frontline but grants no XP, damage, health, or faster
fabrication. The position can be pushed all the way back. Fixed equal unlocks
are the main anti-snowball rule.

### 2.2 Unit arms

| Unit/form | HP | Vision | Fire cadence | Objective weight |
| --- | ---: | --- | --- | ---: |
| Prime | 3 | current cone, range 6 | current cooldown 2 | 1 |
| Mobile child | 3 | current cone, range 6 | current cooldown 2 | 1 |
| Turret control arm | 3 | 360°, range 6 | current cooldown 2 | 0 |
| Strong-turret arm | 5 | 360°, range 6 | cooldown 1 | 0 |

For a turret, 360-degree firing means one chosen shot direction per tick, not
simultaneous radial fire. Anchoring consumes the whole tick, is visibly
telegraphed, resolves at tick end, and permits no shot on that tick. The
strong-turret arm adds two structural HP on successful conversion and has no
passive repair.

Do not initially add splash damage, armor formulas, ammunition, turret
subclasses, or a separate build catalogue.

### 2.3 Objective occupancy

- Capture is binary by team presence, not body count.
- Three mobile allies capture no faster than one.
- Any active enemy mobile contests.
- Turrets neither capture nor contest.
- Anchoring is illegal on objective and protected spawn tiles.

These rules prevent numerical advantage and static bunkers from directly
multiplying objective income.

### 2.4 Rebuild and team survival

- A destroyed Prime enters its respawn timer; surviving children keep acting.
- Losing every current body is not terminal while a Prime respawn is queued.
- Fabrication unlocks persist while a Prime is destroyed.
- A destroyed child or turret makes its fabrication slot unavailable for the
  rebuild interval, then the Prime may instantiate it again.
- No friendly damage initially, and allied bodies do not absorb allied
  projectiles.

## 3. Initial map envelope

Start with one purpose-built map rather than adapting every current map.

| Property | Starting envelope |
| --- | --- |
| Dimensions | 21–23 × 13–15 |
| Frontline footprints | five disjoint 2×2 or 3×2 regions |
| Spawn-to-centre BFS | 8–10 steps, side delta ≤1 |
| Adjacent objectives | 4–6 BFS steps |
| Spawn-to-own inner objective | 4–6 steps |
| Attacker spawn-to-enemy inner objective | 12–15 steps |

Topology requirements:

- a central route plus two short reconnecting flank loops;
- at least two vertex-distinct approaches to every objective;
- approach route-length difference no greater than three;
- at least two nearby hard-cover tiles per objective;
- no mandatory one-tile doorway;
- two-tile-wide convergence routes where bodies are expected to meet;
- 2×2 or 2×3 protected spawn pads;
- no legal turret position that continuously covers an enemy spawn;
- no protected spawn or objective tile may accept Anchor.

The increasingly short defender journey as the front approaches its base is
the physical comeback mechanism.

## 4. Public match contract

Bots need the exact effective public rules for their match. Do **not** expose
the Engine's internal `GameRules` class directly across the SDK boundary.
Instead, derive an immutable, versioned `PublicRulesManifest` from the
resolved engine rules and typed map profile.

`GameRules` remains the authoritative engine configuration. Tests must prove
that every public gameplay value has an exact manifest projection and that no
bot-relevant value is omitted.

The neutral foundation separates the complete match contract from its rules
and map projections. Frontline extends these types rather than creating a
parallel manifest:

```text
PublicMatchContractManifest
  SchemaVersion
  MatchContractFingerprint
  Rules: PublicRulesManifest
  Map: PublicMapManifest

PublicRulesManifest
  RulesetId
  RulesFingerprint
  Limits
  Objective
  Energy
  Fabrication
  Forms[]
  Actions[]
  Projectiles
  ShotPrograms
  Vision
  Collisions

PublicMapManifest
  MapId / MapVersion / FormatVersion
  MapFingerprint
  Width / Height / TileRows
  Spawns[]
  ObjectiveTiles[]
```

It should describe at least:

- maximum ticks and team/unit limits;
- objective positions, capture threshold, decay, redeploy, and pushes to win;
- Prime and fabrication timings;
- respawn and rebuild semantics;
- every available form and transformation;
- max HP, movement layer, vision model/range, objective weight, and allowed
  actions by form;
- projectile damage, range, travel timing, traversal order, cooldown, and
  collision;
- programmed-shot initial aim and curve/bend restrictions;
- all collision and friendly-fire rules;
- every action's stable identity, parameters, bounds, and availability.

Static public rules are separate from hidden match state. A future procedural
map may publish the generation rules without revealing seed-resolved unseen
terrain. Each engine value should be classified as:

- public at match start;
- public when a capability/form appears;
- observation-gated state;
- spectator/replay-only truth;
- internal/runtime-only mechanics.

### 4.1 Rulesets, maps, seasons, and fingerprints

Maps and seasons may select different values, but matches must resolve one
complete effective manifest before tick zero. Replays store that exact
manifest, not a later catalog lookup.

Use separate mechanic-content rules and map fingerprints plus one exact
aggregate match-contract fingerprint. Component hashes may exclude aliases and
presentation, but the aggregate includes its stored schema, ruleset ID, map ID,
and map version because those are public bot inputs. Canonicalization must
explicitly distinguish sets from sequences: sort true sets and catalogs, while
preserving authored order and duplicates wherever bots can observe them. It
must not depend on reflection or declaration order. Each fingerprint excludes
its own field.

Avoid arbitrary per-map imperative exceptions. A map may supply typed profile
values permitted by the manifest schema. The platform publishes curated,
named combinations; it does not promise that every theoretical combination
is balanced or ranked.

This enables domain-randomized training while keeping official competition
rules frozen and reproducible.

## 5. Extensible forms and transformations

Forms are typed capabilities, not hard-coded `if turret` branches spread
through the engine.

```csharp
public sealed record FormDefinition(
    string FormId,
    int MaxHealth,
    MovementLayer MovementLayer,
    VisionDefinition Vision,
    int ObjectiveWeight,
    IReadOnlySet<string> LegalActionIds);

public sealed record TransformDefinition(
    string ActionId,
    string FromFormId,
    string ToFormId,
    int WindupTicks,
    bool Reversible,
    HealthTransition Health,
    IReadOnlySet<TileTag> ForbiddenTiles);
```

Initial forms are `prime-mobile`, `child-mobile`, and `turret`. A later
`flight` form can be introduced through data plus typed engine capabilities:

- its movement layer ignores ground walls under declared traversal rules;
- shooting is absent from its legal-action set;
- health, vision, objective weight, windup, and reversibility are manifest
  values;
- replay and viewer show the transition explicitly.

Do not build a general gameplay scripting language. When a genuinely new
physical semantic appears, add one small typed engine capability and publish
it through the catalog.

Unit identity survives a form transition. Respawn/rebuild creates a new
`lifeId`; fabrication slot and lineage remain stable.

## 6. Actions and legality

The current closed enum will not scale cleanly to transformations and
form-dependent directional fire.

Protocol vNext should carry a stable action ID plus typed parameters. The SDK
may continue offering ergonomic typed helpers:

```csharp
Actions.MoveForward()
Actions.Shoot(program)
Actions.ShootDirection(direction)
Actions.Transform("turret")
Actions.Transform("flight")
Actions.Fabricate(slot)
```

The manifest contains the action catalog and parameter bounds. Every runtime
observation contains the exact legal-action kinds and dynamic parameter masks
for that body on that tick.

For ML, actions have a documented factorization:

- action-kind head;
- optional direction head;
- optional shot-program heads;
- optional fabrication/form target head;
- legality masks applied before sampling.

Never reuse an existing action ID with changed semantics. A new semantic is a
new ID even if the UI label is similar.

## 7. Runtime model

Every body runs one independent instance of the same artifact.

Each pre-tick observation contains:

- a match-local handle to the immutable public rules manifest;
- stable `teamId`, `unitId`, `lifeId`, fabrication slot, and form;
- the body's own state;
- public allied states;
- engine-merged team-visible tiles, entities, projectiles, and events;
- provenance identifying which ally observed each fact;
- current frontline/fabrication/respawn state;
- legal-action masks;
- optional previous-tick team signals if that feature later ships.

Decisions remain simultaneous. Freeze all observations before invoking any
runtime, then resolve the joint action in canonical entity order. No runtime
may observe another allied instance's same-tick decision.

Do not introduce shared mutable bot memory whose result depends on invocation
order. If explicit communication is later useful, test a fixed-size signal
emitted with the action and delivered to all allies on the next tick in stable
unit-ID order.

Destruction disposes that runtime instance. A respawn/rebuild creates a fresh
instance and private memory for a new `lifeId`. A form transition keeps the
same instance and memory.

### 7.1 Protocol vNext

The current line protocol 0.1 remains the historical duel path. Frontline's
team identity, manifest, variable entities, action parameters, and legality
masks require a separate protocol major rather than additive reinterpretation.

Use framed messages with explicit message and field identities:

- `MatchStart`, including manifest and unit identity;
- `ObservationFrame`;
- `DecisionFrame`;
- `MatchEnd`.

Unknown tagged fields must be skippable. Structural breaking changes bump the
protocol major version. The controlled guest adapter continues hiding wire
details from ordinary bot authors.

Choose the concrete encoding only after a NativeAOT/WASI spike measuring guest
size, allocations, throughput, code generation, and forward-field skipping.

## 8. Replay-native ML seam

[`REPLAY-NATIVE-ML-PLAN.md`](REPLAY-NATIVE-ML-PLAN.md) owns the generic
invariant:

> Build one canonical public observation, pass that same object to the
> runtime, and snapshot it directly into the replay before resolving the tick.

Frontline extends that invariant; it does not replace it:

- current duel `slot` identity generalizes to
  `(teamId, unitId, lifeId)` for each active runtime;
- one joint tick groups every frozen actor observation, chosen/validated
  action, resolution, and authoritative post-state;
- the public manifest is stored once in the replay header and joined to actor
  observations without re-running rules logic;
- lifecycle events cover fabrication unlock/spawn, anchor start/complete, form
  change, destruction, respawn/rebuild, frontline movement, and base breach;
- legal actor information, submitted decisions, and omniscient
  spectator/critic truth remain physically distinct;
- team-perception provenance is part of the exact actor observation.

In the replay-native plan, `slot` means the current duel actor key. If replay
v2 and Frontline are designed together, v2 should use a discriminated actor
identity that represents both legacy slots and Frontline lives. If replay v2
ships first with a closed slot-only schema, Frontline must use the next replay
format; it must not silently mutate shipped v2 bytes.

The generic dataset CLI, public corpus, bounded model assets, starter
inference, rollout command, and clean-environment ML gate stay in the
replay-native plan. Frontline contributes only:

- variable entity collections and presence masks;
- public rules-vector inputs;
- per-life recurrent sequence identity;
- joint-tick grouping;
- raw reward facts such as frontline delta, capture, damage, destruction,
  fabrication, base breach, termination, and truncation.

Reward shaping remains outside the canonical replay.

## 9. Neural-policy implications

The same policy weights execute once per active body. Each body always returns
one action, so later expanding a map to four or five allied bodies does not
change the policy output shape.

Official examples should use entity-based inputs:

- encode self;
- encode variable ally, enemy, projectile, and objective collections;
- aggregate with masked pooling, attention, or a graph encoder;
- combine with spatial features and the public rules vector;
- maintain recurrent state per unit life;
- apply legal-action masks.

Do not standardize a flattened three-unit observation. Dataset/replay schemas
use variable collections; dense training adapters may pad to a declared
maximum with presence masks.

Training should randomize team counts, unit IDs, sides, maps, and supported
rules values. A model trained only on one-to-three bodies is structurally
runnable with five but is not guaranteed to coordinate well without
four/five-body training or fine-tuning.

Rules vectors include scalar limits/timings, form statistics,
action/capability bits, parameter bounds, projectile/shot restrictions,
objective topology, and the win threshold. This supports one policy across
curated variations. It does not promise zero-shot competence on a new action;
new output heads or model versions may still be needed.

Inference and packaging remain bounded by the measured path in the
replay-native plan. Frontline does not require a general ONNX/PyTorch runtime
inside submitted WASM.

## 10. Compatibility and intentional obsolescence

Separate three concepts in product and API language:

1. **Executable:** the artifact and protocol can run correctly.
2. **Eligible:** the artifact declares the minimum capabilities required by
   the ruleset.
3. **Competitive:** its strategy is strong under those rules.

Fresh seasons may intentionally make old strategies uncompetitive. They must
not make old actions silently change meaning.

Rulesets declare minimum SDK/runtime/action-schema capabilities. An artifact
that cannot express a required action is either explicitly allowed with its
smaller action set or rejected as ineligible before matchmaking. Never
silently convert an unknown action or observation contract into `Wait`.

Keep ladders and replays pinned to rules versions and exact match contracts.
A new season may open a new ladder while historical rules remain reproducible.

Frontline necessarily touches the CLI compatibility surface: engine/runtime,
SDK/guest, maps, packaged bot inputs, replay format, CLI summaries, and replay
viewer. Before any server emits or admits Frontline, bump `CliVersion` and the
CLI package version, publish/tag that exact compatible revision through
`publish-cli`, then deploy. A server-only revision may reuse a published CLI
only when `scripts/assert-cli-release.sh` proves the enumerated compatibility
surface byte-identical.

## 11. Viewer requirements

The first 3v3 viewer must make causality legible before adding presentation
effects:

- five-position spatial frontline score;
- active objective and capture direction/progress;
- stable team/unit numerals and distinct Prime/mobile/turret silhouettes;
- fabrication slot and rebuild timers;
- anchor/form-transition animation;
- respawn countdown;
- team-vision mode with observation provenance;
- projectile source/traversal trails;
- team-collapsed event/debug panels by default;
- focus-fire and destruction attribution;
- phase/unlock announcements.

The renderer currently serves four consumers: the site, the CLI's
self-contained viewer, hosted review, and the mobile app's WebView. The mobile
WebView renders the canvas while native controls/cards consume
`web/src/replayPresentation.ts` through the hosted-viewer bridge. Frontline
therefore needs one replay-version normalization and presentation layer, not
a second native replay interpreter.

Any hosted-viewer bridge change requires matching protocol/native changes
under `mobile/src/components/arena/` in the same integration commit and a
mobile release. Changing the replay viewer also triggers the CLI release guard
even when no native mobile code changes.

Blind replay review must specifically score:

- tracking which instance is acting or destroyed;
- why a point is or is not capturing;
- focus-fire/projectile causality;
- the spatial value and counterplay of turrets;
- whether replication phases feel like escalation rather than clutter.

Frontend implementation remains deferred while the active frontend refactor
is in flight; these are contract and presentation requirements only.

## 12. Experiment sequence

After deterministic scripted acceptance passes, run causal arms separately:

1. Frontline with Prime-only respawns;
2. Frontline plus timed mobile children;
3. 3-HP/current-cadence 360° turret;
4. 5-HP/faster-cadence strong turret;
5. only if needed, range-6 versus range-7 turret vision;
6. fabricate adjacent to Prime versus at the home pad;
7. later, previous-tick team signals as a separate experiment.

Do not bundle every proposed feature into the first causal comparison.

For the native product verdict, commission at least four independently
authored Frontline doctrines with equal iteration budgets. Include scripted
and neural baselines as diagnostics, not privileged competitors. Freeze WASM
hashes and apply the outcome, dynamics, doctrine-diversity, determinism, and
outcome-blind viewer gates from `EVALUATION-METHODOLOGY.md`.

Historical duel bots are compatibility sentinels, not a veto on the
redesigned product.

## 13. Scripted acceptance before balance

- An unopposed sweep wins before the first fabrication unlock but not
  implausibly close to tick zero.
- One elimination normally enables meaningful capture progress, not an
  automatic two-position sweep.
- Exact mobile contest never accumulates capture.
- Body count never multiplies capture rate.
- One strong turret defeats one naïve mobile in open ground.
- Two coordinated mobiles can defeat one turret.
- A turret cannot capture, Anchor on a zone/spawn, move, or fire during
  conversion.
- Moving the frontline outside turret range measurably reduces its usefulness.
- Mirrored map/orientation blocks remain side-neutral.
- No legal turret continuously covers an enemy spawn.
- Same-policy clones can deterministically choose different roles from stable
  identity.
- Six-body collision, swap, convoy, focus-fire, full-spawn-pad, simultaneous
  destruction, and respawn cases are deterministic.
- Representative policies can win before the first unlock, between unlocks,
  and after the second unlock.
- Replay records every exact legal actor observation without consulting
  omniscient-only state.

## 14. Open decisions

- Is team perception an immediate union of sight and hearing, or does some
  information require an explicit signal?
- Does a new child begin with empty private memory, an explicit spawn message,
  or a bounded team-state snapshot?
- Does fabrication occur adjacent to the Prime or at the home pad?
- Is Anchor irreversible for a life, or may a turret self-destruct to begin
  its rebuild timer?
- Are mobile child and Prime HP initially identical?
- What exact shoot-direction action and telegraph keeps 360° turret fire
  readable?
- Can flying forms contest the frontline, and on which collision layer may
  projectiles hit them?
- Which ruleset changes merely obsolete an old bot, and which make it
  ineligible?
- What runtime budget is per body and what, if anything, is capped per team?
- Are public ranked replays automatically licensed for aggregate training
  datasets, or is separate consent/provenance required?
