# Arc Relay — player rules

Arc Relay is the real game now, not a side experiment (DECISIONS #247). It is
still not the shipped Duel ruleset — the two coexist, each with its own
ladder — and the promotion was of status, not of reach. Read the next two
paragraphs as one fact each, because they are the two things that get confused.

**Locally it is first-class.** `nilbots arc-relay` is a top-level command with
its own help. `nilbots experiment arc-relay` remains a permanent alias, so
every script, brief and report that already spells it that way keeps working.
The command defaults to `--loop-profile broadloom-4` (rules
`arc-relay-broadloom-4`, map `arc-relay-broadloom-01`): the 37x27 Broadloom
geometry, complete signature grammar 4, promoted depth rules, neutral
veterancy Workshops, 50% stable-slot XP retention, and harmless allied-body
projectile blockers. Its joint movement step permits an acyclic convoy to
advance into a pre-tick vacancy; same-target claims, swaps and closed cycles
still block. Every historical profile remains an explicit immutable
compatibility selection.

**Hosted entrant playlist v36 pins Broadloom-4.** It uses the
`arc-relay-orders-v2` sheet format, Tempered Broadloom stock participant,
authored movement urgency, body-local fight contact, repeatable Workshop
training, and atomic EPIBT squad
planning on the direct ATL2 executor. Playlist v35 preserves Broadloom-3,
v34/v33 preserve Broadloom-2 and first Broadloom respectively, and every older definition, artifact,
rating and match remains readable history. Geometry is never projected between
maps.

## Match shape

- Head-to-head, one participant-scoped mind per team.
- Eight fixed unit slots per side, all active at tick 0. A destroyed body is
  eligible after 15 ticks and returns on the next public 30-tick wave.
- A sheet fills all eight slots from the 16 launch classes. A class may appear
  once or twice and at least seven distinct classes must be fielded.
- There is no five-class stable. The sheet chooses from whatever the player
  has unlocked. A stable is only a future roster-scale or format lever.
- There is no economy and no score-to-power. Deliveries never improve stats,
  production, cooldowns, vision, or respawns.
- The horizon is 600 ticks. Timeout ranks Pulses, then stored reactor charge;
  the remaining tie is a draw.

## Competitive visibility

Entrant name, kind, crest, rating, revision, opaque content/artifact hash, and
ordered eight-class composition are public. A sheet's exact routes, roles,
policies, rally lines, and gambits are private to its owner and the hosted
executor. Match pages and replays expose what happened on the field, not the
sheet document or linked stock-mind data. Custom minds receive the same public
facts and causal observations; they cannot fetch an opponent's sheet.

## Objective

Three public Wells stand at north `(11,6)`, centre `(15,13)`, and south
`(19,20)`. Tapline-12 keeps the current opening: all three are nominally tick
50 with public ±6-tick seed jitter (the rotational flank pair shares one
draw). After that opening, supply is source-local. A Well has exactly one
active Core until that Core is banked into either reactor; banking starts a
public 46-tick recharge and the next Core is born on its exact due tick.
Pickup, carrying, Arc Toss, dropping, and stealing do not start or advance a
separate birth timer. While the Core remains active there is no Well birth
countdown. Subsequent global 65-tick births and pending beats do not exist in
Tapline-8, so at most three Cores are active.

A body ending movement on a loose Core picks it up. A Core can relocate only
once per two ticks through carrying, handoff, or forced displacement. The
clock belongs to the Core and survives possession changes. A carrier cannot
use its basic gun. It may wait, turn, drop, or use a legal non-movement
signature while the Core recovers.

Bring a surviving carrier to its own delivery shoulder—west `(6..8,6..8)`,
east `(22..24,18..20)`—to deliver. The reactor itself sits at west `(2,5)` or
east `(28,21)`. A Core from the Well nearest your reactor is worth 1 charge,
the centre Well 2, and the enemy-side Well 4. Every three accumulated charge
creates one arena-wide Pulse; a single bank may therefore create two Pulses.
The third Pulse destroys the opposing reactor and wins. A delivery grants
score only.

`handoff-core` names an adjacent allied unit. The receiver must remain alive,
carry no Core, and submit `Wait`; the source spends its action. `drop-core`
leaves the Core neutral on a legal adjacent tile toward home and temporarily
prevents the dropper from reclaiming it. Pickup, delivery, handoff, drop,
flight, recharge, and birth are all authoritative replay facts.

## Tapline

Coordinates are zero-based. The 31×27 map is fair under exact 180-degree
rotation `(x,y) -> (30-x,26-y)`, but deliberately not under north/south or
east/west reflection. Its three Wells lie on a diagonal axis of play: west's
near Well, the contested centre, and east's near Well are progressively farther
from west and exactly rotate for east. Chunky interior cover and the top/bottom
shadow corridors keep multiple approaches and rear interception legible.

West spawns by unit are `(1,4)`, `(2,4)`, `(3,4)`, `(4,4)`, `(1,5)`,
`(3,5)`, `(4,5)`, `(1,6)`; east spawns are exact 180-degree rotations. The
reactor pads are spawn-protected against hostile ground entry, not damage, while
the separate scoring shoulders are not protected. Wells and reactor sockets
refuse placed constructs.

The authoritative tile rows are exported by the engine in every replay header
and by `/api/sheets/catalog`; the sheet editor plots that export directly.

## Shared body rules

Every accepted movement is one adjacent tile. Tapline-11 and Tapline-12 make chassis names
public deterministic rates: `swift` gets 1/1 movement opportunities,
`standard` 4/5, and `deliberate` 2/3. Closed ticks are staggered by stable unit
id and never bank. Every chassis uses Combat Strafe: forward and lateral travel
can preserve facing; successful rear or rear-diagonal travel turns the body,
preventing free reverse kiting. Tapline-10 and older profiles retain their
historic handling rules and universal 1/1 movement.

All bodies have facing-quadrant vision range 5 plus adjacency sight. A mobile
basic gun may fire in three headings relative to the body's cardinal facing:
straight and the two adjacent diagonals. The legality mask publishes those
exact headings each tick. An off-cone target therefore costs orientation; a
shot does not silently rotate the body. Short/fast range is 3 with cooldown 2,
medium/steady range 4 with cooldown 3, and long/slow range 6 with cooldown 5.
Team perception is an immediate union of allied sensors, so Longshot may use a
teammate's contact to fire the sixth tile beyond its own five-tile sight.
A basic hit deals 1 hull. Signature targeting is unchanged, and the deployed,
faceless Sentinel remains an omnidirectional automated exception.

## Launch classes

| Class | Hull | Handling | Gun | One signature |
| --- | ---: | --- | --- | --- |
| Kestrel | 3 | swift | short | **Vector Dash** — 1-tick tell, straight surge up to 4; drops a carried Core; cooldown 12 after completion. |
| Palisade | 5 | deliberate | short | **Prism Wall** — 3 projectile-blocking segments for 8 ticks or 3 contacts; bodies pass; cooldown 16. |
| Towline | 4 | standard | medium | **Tractor Hook** — straight range 6, pull first body up to 3 tiles; carrier/Core follow recovery; cooldown 12. |
| Patchbay | 4 | standard | short | **Repair Beam** — ally range 4, 1 hull per 2 uninterrupted ticks, max 2; cooldown 10 from end. |
| Lantern | 3 | swift | short | **Survey Flare** — target within 8, travel 2/tick, reveal radius 4 through smoke for 8 ticks; cooldown 16. |
| Mortar | 3 | deliberate | medium | **Falling Star** — visible target within 8, 2-tick reticle, 1 damage on centre and cardinal neighbours; cooldown 12. |
| Minesmith | 4 | standard | short | **Trip Node** — adjacent node, hull 1, 2 trigger damage, proximity reveal; cooldown 12. |
| Hush | 4 | standard | medium | **Null Field** — 1-tick actionable tell, then radius 3 for 5 ticks; blocks starts and suppresses hostile signatures; cooldown 18. |
| Relay | 4 | swift | short | **Arc Toss** — carrier targets straight landing within 5; no tell, surviving carrier releases after that tick's combat, Core flies 3/tick; cooldown 12 from launch. |
| Switchback | 3 | standard | medium | **Exchange** — visible ally within 6, 1-tick tell and target Wait; carrier drops first; cooldown 16. |
| Longshot | 3 | deliberate | long | **Rail Line** — fixed heading, 2-tick charge, range 12, 2 damage through bodies; cooldown 18 (8 if interrupted). |
| Mason | 5 | deliberate | short | **Hardlight Block** — adjacent hull-3 block for 12 ticks; one active; cooldown 14. |
| Sunder | 4 | standard | medium | **Target Paint** — 1-tick tracked acquisition on enemy within 7; then 8 ticks or 3 allied hits, each +1 damage; cooldown 16 (8 on move-abandon). |
| Repulsor | 5 | standard | short | **Kinetic Burst** — 1-tick actionable tell, then push bodies still adjacent after response movement; cooldown 12 (6 on move-abandon). |
| Veil | 3 | swift | short | **Smoke Canister** — target within 6, public flight at 3 tiles/tick, then radius-2 sight blocker for a full 10 ticks; cooldown 18 from launch. |
| Nest | 4 | deliberate | medium | **Sentinel Seed** — adjacent hull-2 sentry, range 4, 1 damage, fires every 3 ticks for up to 30; cooldown 18. |

The action legality mask is authoritative. Read its typed constraints every
tick instead of reconstructing targets or cooldowns. Exact static values are
available from the public CLI:

```bash
nilbots experiment arc-relay \
  --loop-profile forward-combat --print-contract
```

Run a native mind locally with:

```bash
nilbots experiment arc-relay \
  --bot <project-or-wasm> --opponent <project-or-wasm> \
  --sheet0 <sheet.json> --sheet1 <sheet.json> \
  --loop-profile tapline-5 --seed 42
```

Current sheets pair an `arc-relay-orders-v2` Orders document with an
`arc-relay-map-plan-v2` map plan. Roles cast the eight class bodies and carry
one nested priority tree built only from move, fight, use, handoff, and drop.
See `docs/SHEET-AUTHORING.md` for the complete authoring contract.

## Signature grammar 2 (`arc-relay-forward-combat-02`, 2026-08-05)

The `forward-combat-2` loop profile selects the same map and combat rules
with three signature changes, so that every enemy-affecting effect gives
its victim an out (the audit in `ARC-RELAY-SIGNATURE-AUDIT.md`):

- **Sentinel Seed** fires a real bolt (`sentinel-bolt`, 2 tiles/tick)
  along one of the eight rays only — step off the ray and it misses,
  walls and constructs eat it — and fires every **2** ticks instead of 3.
- **Tractor Hook** launches a grapple bolt (`hook-bolt`, 2 tiles/tick,
  range 6). The pull lands on contact; a Prism Wall between you and the
  Towline eats the hook.
- **Null Field** charges for one visible tick before the suppression
  lands.

Grammar-2 contracts also carry designed-role metadata on every signature
(`category`, `argumentKind`, `engagementRange`), so a generic executor
can play a new class without hand-written dispatch tables. Grammar-1
contracts, fingerprints, and replays are unchanged and remain the record
for everything frozen before this date.


## Foundations (`arc-relay-forward-combat-03`, 2026-08-05)

The `forward-combat-3` loop profile keeps grammar-2 combat and adds the
robust-play foundations (DECISIONS #210):

- **Seed variance.** Each scheduled well birth round shifts within a
  seed-derived window of ±6 ticks (`wellBirthJitterTicks: 6`). The
  jittered schedule is public — `nextScheduledBirthTick` in the mode
  observation reflects it — and the same seed always reproduces the same
  schedule. Distinct seeds now produce genuinely distinct games; plan for
  a birth *window*, not a fixed beat.
- **Side fairness** (`alternatingResolutionOrder: true`). The
  order-dependent slices of resolution — contested projectile
  consumption, projectile advancement, same-target hook pulls — alternate
  direction by tick parity instead of always favouring team 0, and a
  sentinel breaks equal-distance target ties toward its own reactor
  rather than toward the lowest enemy slot.

Grammar-1 and grammar-2 contracts, fingerprints, and replays are
unchanged. Under -03, evaluate strategies over a seed distribution: a
single game is one draw, not a verdict.

## Signature grammar 3 (`arc-relay-tapline-02`, 2026-08-12)

`tapline-2` advances the complete sixteen-class Tapline roster to grammar 3 on
the unchanged `arc-relay-tapline-01` map. Every signature contract now carries
`initialPhase`, `maturityPhase`, and `cooldownAnchor`; four envelopes are
versioned where the prior chronology contradicted their public read:

- **Null Field** keeps its frozen radius-3 footprint and one-tick tell, but
  activates after response movement. A body that leaves can act safely; one
  that remains inside has its signature revalidated and blocked. Maintained
  channels end and constructs suppress on that same boundary.
- **Target Paint** has a one-tick tracked acquisition. It confirms after
  response movement only while the target remains alive, within range 7, and
  in team vision. No cast-tick attack receives the bonus. Moving Sunder
  abandons acquisition and pays an 8-tick partial cooldown.
- **Kinetic Burst** resolves after response movement against the bodies still
  adjacent to its frozen origin. Moving Repulsor abandons it and pays a 6-tick
  partial cooldown.
- **Smoke Canister** publishes a 1–2 tick `in-flight` phase at 3 tiles/tick,
  passes over walls without projectile collision, and begins its full
  radius-2, 10-tick field on landing. The launch-anchored cooldown remains 18,
  and a thrown canister survives its caster.

The other twelve signatures keep their established mechanics inside the same
grammar-3 profile. Only mechanically changed definitions receive a new
concrete version. `tapline-1`, its rules fingerprint, hosted playlist v20,
stock artifacts, and ratings remain immutable.

This distinction is the repository extension rule, not a Tapline exception:
grammar generations cover the complete roster, while concrete definition
versions exist only for real mechanic or chronology changes. Hosted v23 now
pins grammar 3 and Tapline-5; v22 remains immutable Tapline-4 history. The end-to-end
class/signature checklist is in
[`ADDING-ARC-CLASS-AND-SIGNATURE.md`](ADDING-ARC-CLASS-AND-SIGNATURE.md).

## Atomic convoy movement (`arc-relay-tapline-03`, 2026-08-13)

`tapline-3` keeps Tapline-2's map, cadence, combat and complete signature
grammar. It versions one collision rule: when A moves into B's source tile and
B moves onward toward a tile that was empty before the tick, the whole acyclic
chain advances atomically. A contested destination, two-body swap, longer
closed cycle, terrain block, projectile contact, or stationary head blocks the
dependent chain. This is the engine contract required by coordinated squad
planning; it is not permission to overlap bodies or resolve moves in command
order.

## Broadloom-4 signature depth (`arc-relay-broadloom-4`, 2026-08-23)

`broadloom-4` is the current contract. It keeps Broadloom-3's exact map,
Workshop cadence, scoring, supply, movement, and combat numbers while moving
to signature grammar 4:

- Kestrel Dash retains a carried Core and Smart follows custody/home/phase
  route intent.
- Towline Hook has range 6, deals 10, then pulls a survivor up to five tiles
  to Towline's front. Smart is blockable and prioritizes carrier, weakest,
  nearest. Only a two-point player-drawn line may breach one wall or hostile
  Hardlight tile.
- Repulsor Burst deals 10 to and pushes every adjacent hostile; allies are
  untouched. Aggressive posture closes to adjacency while Burst is ready.
- Lantern Flare supplies radius-4 team vision for its eight-tick lifetime.
- Switchback Exchange retains a targeted carrier's Core and Smart advances a
  carrier only for a safe gain of at least two homeward walking steps.
- Sunder receives its own Paint bonus; Smart Paint prioritizes carrier,
  veteran, weakest, nearest.

All Smart behavior is ordinary compiled Instruction priority plus existing
signature lifecycle. There is no signature scheduler or second tactical state
machine. Hosted playlist v36 pins the rebuilt mind; v35 remains exact
Broadloom-3 history.

## Broadloom and veterancy Workshops (`arc-relay-broadloom-3`, 2026-08-23)

`broadloom-3` is the historical no-dormancy Workshop contract. It keeps Broadloom-2's exact map,
combat, logistics and durable-career rules while removing only the Workshop
post-channel cooldown under ruleset `arc-relay-broadloom-3`. Playlist v34
preserves Broadloom-2's 45-tick dormancy and playlist v33 preserves first
Broadloom without 50% stable-slot XP retention or allied-body projectile
blockers. All three use three Wells (outer Wells in roomed
theatres at (10,19) and (26,7), the centre Well under a covered crossing),
heal alcoves, and two rotationally symmetric neutral **Workshops** at (18,4)
and (18,22).

A Workshop is a public neutral objective, never terrain: both teams always
see its exact state, and fog hides nothing about it (only about bodies).
Both unlock at tick 25. To train, one body stands on the training tile and
holds a channel for **15 consecutive qualifying ticks**: same living body of
one team, on the tile, not carrying a Core, not moving or displaced, not
attacking or casting a signature, taking no damage, with no living enemy
anywhere inside the Workshop's authored contest region (the room, its
mouths, and the skylight). Waiting and rotating in place are free. Any
violation resets progress to zero — the channel identity is that exact life,
so dying, respawning, or swapping bodies never inherits progress. The
training tile refuses authored signature placement, heals nothing, and
scores nothing.

Completion grants exactly **1 veterancy XP** through the ordinary capped
veterancy system (visible on the next tick). The next 15-tick channel may
begin on the following tick with **no Workshop cooldown**. A fresh level-1 bot
therefore reaches level 5 after eight successful channels, or 120 qualifying
ticks, if the team can protect it continuously.
The two Workshops are independent: contesting or completing one never
touches the other. A body already at maximum veterancy cannot train.

Protecting a trainee never speeds the channel up — guards only keep the
contest region empty. The intended tension is public: a Training signal
invites a contest route or a raid elsewhere, and the contest region is wide
enough that a disruptor never needs the exact training tile.

Tactics authors the loop with the **Train at Workshop** job (`kind:
"train"`, workshop `north`/`south`/`either` — global ids that do not mirror
with the seat) and the public-state rows `workshopReady`,
`workshopTraining`, `workshopContested`, `workshopDormant`, `workshopDone`,
and the existing multi-bot `veterancy` row. Orders v2 carries the matching facts, the `{workshop}`
move target, and the historical `friendly-workshop-completions` condition.
