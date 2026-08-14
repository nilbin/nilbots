# Arc Relay — player rules

Arc Relay is the real game now, not a side experiment (DECISIONS #247). It is
still not the shipped Duel ruleset — the two coexist, each with its own
ladder — and the promotion was of status, not of reach. Read the next two
paragraphs as one fact each, because they are the two things that get confused.

**Locally it is first-class.** `nilbots arc-relay` is a top-level command with
its own help. `nilbots experiment arc-relay` remains a permanent alias, so
every script, brief and report that already spells it that way keeps working.
The command defaults to `--loop-profile tapline-4` (rules
`arc-relay-tapline-04`, map `arc-relay-tapline-01`): the accepted Tapline
geometry, declared strikes, veterancy, rear-arc damage, and the complete
signature grammar 3, plus the promoted combined depth rules. Its joint movement step also permits an acyclic convoy to
advance into a pre-tick vacancy; same-target claims, swaps and closed cycles
still block. `h0`, `tapline-1`, `tapline-2`, and every historical profile
remain explicit immutable compatibility selections.

**Hosted entrant playlist v22 pins Tapline-4.** It freezes the
`arc-relay-orders-v2` sheet format, Tempered Standard stock participant, authored
movement urgency, body-local fight contact, and atomic EPIBT squad planning on
the direct ATL2 executor, together with team-relative Core value, veteran
command, public respawn waves, roster variety, and durable deployments. The
promoted rules and stock gameplay start a fresh rating book; v21's Tapline-3
ratings and matches remain readable history.
Earlier playlist definitions and artifacts remain immutable, and geometry is
never projected between maps.

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
`(19,20)`. Their nominal first beats are centre 25, north 50, and south 75;
the seed-phased lead and ±6-tick birth jitter below choose the exact order and
tick. Each then repeats on the 65-tick schedule for nine rounds. A Well has at most one unresolved
Core and one visible pending charge, so at most three Cores are live.

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
leaves the Core neutral on the carrier's tile. Pickup, delivery, handoff, drop,
flight, and recovery are all authoritative replay facts.

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

Every body moves one tile per accepted movement. Handling changes facing
commitment, not speed:

- `swift`: ordinary movement projects the eight-way travel heading onto a
  cardinal facing. The mind may instead choose the separately published
  `strafe-eight-way` action to preserve facing for that move;
- `standard`: forward and lateral movement preserve facing; successful rear
  or rear-diagonal travel turns the chassis around, preventing free reverse
  kiting;
- `deliberate`: movement is allowed only in the current cardinal facing, so
  changing travel direction costs a rotation.

All bodies have facing-quadrant vision range 7 plus adjacency sight. A mobile
basic gun may fire in three headings relative to the body's cardinal facing:
straight and the two adjacent diagonals. The legality mask publishes those
exact headings each tick. An off-cone target therefore costs orientation; a
shot does not silently rotate the body. Short/fast range is 4 with cooldown 2,
medium/steady range 6 with cooldown 3, and long/slow range 9 with cooldown 5.
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
| Relay | 4 | swift | short | **Arc Toss** — carrier targets straight landing within 5; 1-tick tell, Core flies 2/tick; cooldown 12 from launch. |
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
  --loop-profile tapline-4 --seed 42
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
versions exist only for real mechanic or chronology changes. Hosted v22 now
pins grammar 3 and Tapline-4; v21 remains immutable Tapline-3 history. The end-to-end
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
