# Frontline Labs v1 bot contract

Status: player-facing rule card for immutable playlist `frontline-labs`,
version 1. It is experimental, setless, and unranked. This document describes
`frontline-labs-1`; it does not describe the older `frontline-alpha-1`
`IActorBot` experiment or shipped Duel.

The exact schema-2 resolved match contract delivered to
`IGenericActorBot.StartLife` and embedded in replay v3 is authoritative. Bots
should discover catalog entries, counts, map geometry, and legality from that
contract instead of turning the current values below into structural
assumptions.

## Objective and ending

Two teams contest one active objective along five ordered Frontline positions.
The match starts at the centre position.

- Sole mobile presence builds capture progress by 1 per tick.
- Empty or contested control decays existing progress by 1 every 2 ticks.
- Stacking multiple allied bodies does not accelerate capture.
- A capture completes at 15 progress and advances the Frontline one position
  toward the opponent.
- After an advance, capture pauses for 5 ticks before the new active position
  can progress.
- Three advances in one direction breach the opposing base and win
  immediately.
- The match executes at most 500 ticks.

An early breach is a complete win. Fabrication, Split, and Anchor are options
for games that remain close, not phases every match must reach.

At the tick cap, teams are ranked by signed territorial progress: displacement
from the centre multiplied by the capture threshold, plus current capture
progress signed toward the controlling team's advance direction. Health,
damage, body count, and kills do not break a territorial tie.

## Current topology and lifecycle

Playlist v1 has two submitted participants, one per team. Each team has three
stable unit slots:

- unit 0 starts active as one `prime-mobile`;
- unit 1 unlocks at tick 120;
- unit 2 unlocks at tick 260.

The Prime returns automatically 18 complete absent-decision ticks after
destruction. An unlocked child slot becomes Ready rather than spawning
automatically. A destroyed child becomes Ready again after 30 complete
absent-decision ticks and must be fabricated explicitly.

These are values, not array-shape guarantees. The contract separately declares
teams, participants, unit slots, initial lives, lifecycle assignments, and
their participant ownership.

## Forms

All mobile forms have 3 maximum health, ground movement, objective weight 1,
range-6 mobile vision, and the same mobile projectile profile.

| Form | How it appears | Available capability |
| --- | --- | --- |
| `prime-mobile` | initial life or automatic Prime return | move, rotate, shoot, Fabricate, Split |
| `child-mobile` | explicit Fabricate completion | move, rotate, shoot, Anchor |
| `replica-mobile` | Split completion | move, rotate, shoot |
| `turret` | same-life Anchor completion | wait or absolute-heading turret fire |

The turret has 5 maximum health, objective weight 0, no movement or rotation,
range-6 omnidirectional vision, 360-degree firing, and a faster attack
cooldown. It cannot capture or contest an objective.

Current mobile vision is a facing quadrant plus omnidirectional proximity 1.
Turret vision is fully omnidirectional. Allied perception is an immediate
union: every life receives current allied body state and the union of what
declared allied sensors see, including `observedBy` provenance for enemies.
Hearing has radius 8 for attacks, damage, and destruction.

## Actions

Every decision pairs the stable action ID with the numeric code from that
tick's `GenericActorActionLegality`. The current stable IDs are:

- `wait`;
- `move`, with an absolute cardinal `Direction`;
- `rotate`, with an absolute cardinal `Direction`;
- `shoot`, with an optional mobile `ShotProgram`;
- `fabricate`, with one stable team/unit target;
- `split`;
- `transform`, with a form target;
- `shoot-direction`, with one absolute eight-way projectile heading.

Three facts authors repeatedly rediscover the hard way:

- **`move` does not change your facing** and `rotate` does not move you;
  `rotate` sets an absolute facing in one action (no 90° stepping). A body
  can therefore move any cardinal direction while facing another — that is
  the contract, not the historical strafe actions. Experiment arms may
  couple facing to movement; read the contract, not this sentence.
- **Within one tick, movement resolves before combat.** The enemy you
  observed has already moved by the time projectiles advance and new shots
  launch — aim at where bodies will be after movement, not where the
  pre-tick observation drew them.
- **Omnidirectional vision and turret fire are eight rays, not a filled
  radius.** A tile "in range" is only seen or hittable along one of the
  eight headings. The same discipline applies harder to a straight-only
  mobile gun: it fires along exactly the four cardinals from your tile,
  so at any distance half the tiles are permanently unreachable — stand
  on a lane or you are not armed.
- **"Range" is three different numbers.** Vision range, projectile
  travel, and hearing radius are independent per-form values (a bulwark
  sees 4, shoots 6, and hears 8); read each from its own profile.
- **Under `facing-locked`, rotate to unlock a step.** The movement
  legality mask offers only your current facing each tick — a route
  search seeded from currently-legal directions makes your bot immobile,
  not slower. Plan on map geometry and spend the rotation.

Availability and typed constraints are authoritative. An action may be absent
from a future form or contract. `Available` includes source-local and stable
slot prerequisites: for example, Fabricate is unavailable away from its
declared source region, and Split is unavailable without enough health and
compatible Ready slots. It deliberately cannot promise the result of
simultaneous physical resolution. Another body's move or lifecycle claim can
still make an individually available action succeed or block.

Mobile shots deal 1 damage, have cooldown 2, travel at most 8 tiles, and
advance 2 tiles per projectile tick after a one-tile launch. The default
program fires straight forward. An optional program may offset initial aim by
one 45-degree sector and bend left or right after 1–4 tiles, every 1–3 tiles,
for 1–3 bends. Strict diagonal corners apply.

Turret shots deal the same damage and use the same range/travel cadence, but
have cooldown 1, choose an absolute eight-way heading, and cannot curve.

Damage is simultaneous. Actors and walls block actors; allied actors also
block movement. Same-destination moves all block, swaps block, following a
vacated actor blocks, and projectiles block movement. Moving onto a projectile
causes a hit. Walls consume projectiles. Allied projectiles pass through
allies; enemy projectiles stop on the first enemy actor. Projectiles do not
collide with each other.

## Fabricate

Only a `prime-mobile` on its own protected home pad may Fabricate. The action
targets one own child slot that is currently Ready.

A successful action reserves the first legal free pad tile within the
contract's bounded placement offsets and creates a fresh `child-mobile` life
at the next tick start, facing the team's authored home direction. Placement
is evaluated after movement, so a same-tick vacancy can allow an attempt that
was initially crowded. A full pad blocks the attempt.

The authored Prime spawn remains reserved against own child movement. Enemy
ground units cannot enter the opposing protected pad, but protected does not
mean immune to projectiles or damage.

## Split

Only an eligible generation-0 `prime-mobile` that has not previously changed
form may Split. Playlist v1 retires the source life and creates two fresh
`replica-mobile` descendants in reserved legal cardinal placements.

The source's current health is divided equally with floor rounding and a
minimum of 1 per descendant; any remainder is discarded. Thus a full-health
3-HP Prime produces two 1-HP replicas. Descendants have independent fresh
runtime instances and private memory. Their `StartLife.Origin` identifies the
source life, transition, shared operation, and generation.

Split has a one-tick windup. The legality mask exposes source health/state and
whether enough compatible Ready slots exist. Placement and cross-operation
claims resolve jointly after movement, so an available Split may still block.
Replicas cannot Fabricate, Split again, or Anchor.

## Anchor

Only a fabricated `child-mobile` may submit `transform` targeting `turret`.
Anchor is illegal on every contract-tagged transition-forbidden tile,
including all objective and protected-pad tiles.

Anchor consumes the tick and has a one-tick windup. During the windup the life
remains a targetable, tile-occupying mobile child, still contributes objective
weight, and may only wait. Lethal damage cancels the transition; nonlethal
damage does not.

Completion is irreversible for that life. It preserves exact actor identity,
runtime/private memory, position, facing, cooldown, energy, and accumulated
damage. Health becomes:

```text
min(5, current health + 2)
```

Because the Prime always returns automatically and turrets have objective
weight zero, two turrets do not by themselves create a rules-level terminal
deadlock. Poor positioning can still produce long passive stretches, which
the balance cohort measures explicitly.

## Map

`frontline-labs-01` is 23 by 15 tiles. Coordinates start at `(0,0)` in the
top-left; x grows east and y grows south. `#` is a wall:

```text
#######################
#.....................#
#..##.....#.#.....##..#
#.........#.#.........#
#...#......#......#...#
#....#.....#.....#....#
#....#..##...##..#....#
#.....................#
#....#..##...##..#....#
#....#.....#.....#....#
#....#.....#.....#....#
#.........#.#.........#
#..##.....#.#.....##..#
#.....................#
#######################
```

The initial Prime spawns are `(2,7)` facing east and `(20,7)` facing west.
The ordered objective regions are:

```text
position 0: (3,8) (4,8) (3,9) (4,9)
position 1: (6,5) (7,5) (6,6) (7,6)
position 2: (10,7) (11,7) (12,7) (10,8) (11,8) (12,8)
position 3: (15,5) (16,5) (15,6) (16,6)
position 4: (18,8) (19,8) (18,9) (19,9)
```

Each home pad is a six-tile region around its Prime spawn. The complete
protected and transition-forbidden tile sets are delivered as map tags; use
those tags rather than copying their coordinates.

## Runtime, memory, and determinism

One submitted artifact controls all of a participant's body lives, but each
active life receives its own isolated bot instance, deterministic random
stream, and private memory.

- A same-life form change preserves private memory.
- Destruction disposes the instance.
- Prime return, Fabricate, and Split create fresh instances.
- Fresh lives do not inherit private fields from a parent.
- `StartLife.Origin` and current allied observations let code assign roles
  without hidden shared state.

Observations are frozen before any same-tick decisions execute. A life never
sees an ally's current action. Use only the delivered contract, observation,
and `context.Random`; wall clocks, ambient entropy, file/network access,
threads, and environment state are outside the deterministic sandbox contract.

### Under the mind profile, every clause above inverts

`nilbots experiment frontline-labs --profile mind` runs this exact contract on
`generic-mind-match-1`. The rules, map, forms, actions, transitions, lifecycle,
mode and economy are unchanged — it is the same game — and what changes is who
drives it:

- **One bot instance per PARTICIPANT, for the whole match.** It is constructed
  before tick 0 and disposed after the terminal tick. Its fields ARE the
  persistent memory; there is no memory API, and nothing is cleared when a body
  dies.
- **One deterministic private stream for the mind**, derived in the participant
  domain, plus the team stream. Inside a single mind the team stream is
  pointless — you do not need to agree with yourself — and it becomes
  load-bearing only when a format with allied minds is admitted.
- **A form change, destruction, return, fabrication and Split change nothing
  about memory.** They change which bodies exist, which is a data question
  (`mind.Bodies`) rather than a lifetime question.
- **`Think` is called exactly once per tick, unconditionally**, from tick 0 to
  the terminal tick, including ticks on which the mind owns no live body at all.
  "Am I alive?" stops being a control-flow question.
- **Commands are written onto bodies, not returned.** Every own live body is
  pre-filled with `Wait` and the mind overwrites what it wants moved, so
  forgetting a body costs that body one tick, visibly, in the replay — not the
  match. Commanding a body that is not live, or not yours, is `Rejected` and
  recorded. Commanding the same body twice is a fault.
- **A mind that traps forgets the match.** There is no snapshot: a runtime fault
  discards the Store and its entire match-long memory, and under this contract's
  zero fault allowance it also disqualifies the participant. Robustness is part
  of the doctrine.
- **Team perception is unchanged and stays team-scoped.** The observable union
  is computed per scoring team exactly as above and delivered to the mind once
  rather than once per body. Observations are still frozen before any same-tick
  decision. Nothing about fog, provenance, or `observedBy` moves. What the mind
  adds is TIME, not space: the union was already shared within a tick, and a
  mind pools it ACROSS ticks.
- **What a mind is additionally told about its own bodies**, because it is
  entitled to it and a per-life bot was not: `MovedLastTick`, `PreviousPosition`,
  `LifeStartedTick`, `Origin`, the published role tag, and the participant's
  complete slot table every tick.
- **Role tags are public.** `SetRole` publishes a free-vocabulary label of at
  most 24 bytes on your bodies and on any of your bodies an enemy can see. The
  engine never reads it, so labelling your channeler a screen is a real move.

An artifact whose author only wrote `IGenericActorBot` still plays this profile
with no source edits — the guest hosts it as one sub-brain per live body,
reproducing per-life memory semantics exactly, including a fresh instance on
respawn and the same private random sequence. All it costs is a rebuild.

The default final authoring check is:

```bash
nilbots build <project>
nilbots experiment frontline-labs \
  --bot <project>/out/bot.wasm \
  --opponent <other>/out/bot.wasm \
  --seed 104729
nilbots verify <replay.json>
```

Use in-process execution only for fast mechanical diagnosis. Frozen cohort
outcomes use the controlled WASM runtime.

### Local candidate contracts

The CLI may expose separately identified local experiments without changing
this hosted v1 contract. `--capture-threshold`, `--capture-gain-phase`, and
`--mobilize-turrets` each produce a content-descriptive experimental ruleset
and new fingerprints embedded in replay v3. In the Mobilize arm, a turret can
return to `child-mobile` with the same actor/runtime memory and capped health,
then cannot Anchor again during that life. That arm proved the generic
same-life transition architecture but failed its initial pacing gate; it is
not a hosted v1 rule.

The duel-depth map experiments and declared automatic-companion progression
arm are specified in
[`EXPERIMENTAL-FRONTLINE-DUEL-DEPTH.md`](EXPERIMENTAL-FRONTLINE-DUEL-DEPTH.md).
In the automatic arm, dormant child slots create fresh lives at their declared
ticks and report `StartLife.Origin.Reason == AutomaticActivation`; Fabricate
and Split are absent from that isolated candidate. Bots must follow the
resolved contract and current legality rather than infer that every local
experiment has hosted-v1 actions or manual child readiness. None of those map
or lifecycle arms changes this immutable hosted rule card.
