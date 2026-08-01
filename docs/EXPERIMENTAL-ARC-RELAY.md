# Arc Relay H0 — experimental player rules

Arc Relay H0 is the current experimental game used for Gate 3. It is not the
shipped Duel ruleset. Its identities are `arc-relay-h0-01` and
`arc-relay-threefold-01`; changed mechanics or map content require changed
fingerprints.

## Match shape

- Head-to-head, one participant-scoped mind per team.
- Eight fixed unit slots per side, all active at tick 0 and automatically
  returning 20 ticks after destruction.
- A sheet fills all eight slots from the 16 launch classes. A class may appear
  once or twice; therefore a legal sheet contains 4–8 distinct classes.
- There is no five-class stable. The sheet chooses from whatever the player
  has unlocked. A stable is only a future roster-scale or format lever.
- There is no economy and no score-to-power. Deliveries never improve stats,
  production, cooldowns, vision, or respawns.
- The horizon is 600 ticks. Timeout ranks Pulses, then stored reactor charge;
  the remaining tie is a draw.

## Objective

Three public Wells stand at north `(15,4)`, centre `(15,11)`, and south
`(15,18)`. Centre first produces at tick 25, north at 50, and south at 75;
each repeats every 75 ticks through 525. A Well has at most one unresolved
Core and one visible pending charge, so at most three Cores are live.

A body ending movement on a loose Core picks it up. A Core can relocate only
once per two ticks through carrying, handoff, or forced displacement. The
clock belongs to the Core and survives possession changes. A carrier cannot
use its basic gun. It may wait, turn, drop, or use a legal non-movement
signature while the Core recovers.

Bring a surviving carrier to its own reactor socket—west `(2,11)`, east
`(28,11)`—to deliver. Three deliveries create one arena-wide Pulse. The third
Pulse destroys the opposing reactor and wins. A delivery grants score only.

`handoff-core` names an adjacent allied unit. The receiver must remain alive,
carry no Core, and submit `Wait`; the source spends its action. `drop-core`
leaves the Core neutral on the carrier's tile. Pickup, delivery, handoff, drop,
flight, and recovery are all authoritative replay facts.

## Threefold

Coordinates are zero-based. `#` is a wall and `.` is floor. The map mirrors by
`x -> 30 - x` and north/south by `y -> 22 - y`.

```text
###############################
#....#...................#....#
#.............................#
#....#....###.....###....#....#
#....#....###.....###....#....#
#....#....###.....###....#....#
#.............................#
#....#...................#....#
#....#...................#....#
#.............................#
#....#....###.....###....#....#
#....#....###.....###....#....#
#....#....###.....###....#....#
#.............................#
#....#...................#....#
#....#...................#....#
#.............................#
#....#....###.....###....#....#
#....#....###.....###....#....#
#....#....###.....###....#....#
#.............................#
#....#...................#....#
###############################
```

West spawns by unit are `(1,8)`, `(2,8)`, `(3,9)`, `(1,10)`, `(1,12)`,
`(3,13)`, `(1,14)`, `(2,14)`; east spawns are exact reflections. The home-pad
rectangles are spawn-protected against hostile ground entry, not damage.
Wells and reactor sockets refuse placed constructs.

## Shared body rules

Every body moves one tile per accepted movement. Handling changes facing
commitment, not speed:

- `swift`: movement faces the moved direction;
- `standard`: movement preserves facing;
- `deliberate`: movement is allowed only in the current cardinal facing, so
  changing travel direction costs a rotation.

All bodies have facing-quadrant vision range 7 plus adjacency sight. Basic
guns are omnidirectional eight-way shots: short/fast range 4 cooldown 2,
medium/steady range 6 cooldown 3, and long/slow range 9 cooldown 5. A basic
hit deals 1 hull.

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
| Hush | 4 | standard | medium | **Null Field** — radius 3 for 5 ticks; blocks starts and suppresses hostile signatures; cooldown 18. |
| Relay | 4 | swift | short | **Arc Toss** — carrier targets straight landing within 5; 1-tick tell, Core flies 2/tick; cooldown 12 from launch. |
| Switchback | 3 | standard | medium | **Exchange** — visible ally within 6, 1-tick tell and target Wait; carrier drops first; cooldown 16. |
| Longshot | 3 | deliberate | long | **Rail Line** — fixed heading, 2-tick charge, range 12, 2 damage through bodies; cooldown 18 (8 if interrupted). |
| Mason | 5 | deliberate | short | **Hardlight Block** — adjacent hull-3 block for 12 ticks; one active; cooldown 14. |
| Sunder | 4 | standard | medium | **Target Paint** — enemy within 7; 8 ticks or 3 allied hits, each +1 damage; cooldown 16. |
| Repulsor | 5 | standard | short | **Kinetic Burst** — 1-tick tell, push every adjacent body one legal tile; cooldown 12. |
| Veil | 3 | swift | short | **Smoke Canister** — target within 6, radius-2 sight blocker for 10 ticks; cooldown 18. |
| Nest | 4 | deliberate | medium | **Sentinel Seed** — adjacent hull-2 sentry, range 4, 1 damage, fires every 3 ticks for up to 30; cooldown 18. |

The action legality mask is authoritative. Read its typed constraints every
tick instead of reconstructing targets or cooldowns. Exact static values are
available from the public CLI:

```bash
nilbots experiment arc-relay --print-contract
```

Run a native mind locally with:

```bash
nilbots experiment arc-relay \
  --bot <project-or-wasm> --opponent <project-or-wasm> \
  --sheet0 <sheet.json> --sheet1 <sheet.json> --seed 42
```

Evaluation sheets in Gate 3 use the provisional
`arc-relay-evaluation-sheet-v0` audit format for coverage and reproducibility.
That is deliberately not the future player-facing sheet schema or drawing UX.
