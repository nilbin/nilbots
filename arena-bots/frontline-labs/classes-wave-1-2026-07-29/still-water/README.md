# Still Water — patient interceptor (striker)

Lineage `still-water-v1` · class `striker` · role `verdict-doctrine` ·
Frontline Labs classes arm, wave 1.

## The idea in one sentence

Do not walk into the duel. Stand one bend's reach behind the contested point,
put the gun across the approach, and make the other side spend tiles and tempo
coming to you — then take the ground last.

## Why that is the right shape for this chassis

The striker's exclusive verb is a private 45-degree bend committed at the
moment of firing. Read straight off the resolved attack profile, that envelope
is: aim along facing, travel *k* tiles with `k` inside the declared bend
window, then turn once and run out the rest of the declared range. Written as
map geometry, a target at longer-axis distance `a` and shorter-axis distance
`b` is coverable exactly when `a` is inside range and `a - b` lands inside the
bend window. Two consequences fall out, and both are doctrine:

- **The fan is widest in the middle of the range.** Very close and very far,
  the coverable lateral band collapses to a couple of offsets; around one tile
  past the latest legal bend point it is at its widest. That distance is Still
  Water's *standoff band* — the still water it holds.
- **A bend is a lie that stays true until it turns.** The heading a defender
  reads off a bolt in flight is evidence, not the path. A body that dodges the
  visible line at the right range steps *into* the arc, not out of it.

So the whole positional argument reduces to one number: distance to the nearest
enemy, measured against the band. Closing is penalised harder than yielding,
which is what makes conceding ground the cheap option — a tile given up to
restore the band is a good trade, every time.

The band is not a constant. It is resolved from both chassis at match start: if
the opposing chassis can be out-ranged with a tile to spare, the band sits one
past *their* reach; otherwise it sits where our own fork is widest. Against a
short-gunned, short-sighted opponent that means standing outside its world
entirely. Against a mirror it means the fork band.

## How a tick is spent

1. A bolt that will cross this tile during the coming resolution outranks
   everything — but only if stepping aside actually costs little. In a choke,
   every tile is the line; giving the ground back to a bolt that will simply
   arrive again is a loop, not an evasion, so Still Water takes the trade and
   crosses.
2. Companions, whichever way the contract hands them over. Some resolved
   contracts activate child slots on a clock; some require the Prime to ask.
   Both routes come from the legality mask; a body is worth more than a bolt.
3. The gun, if the trajectory arrives on a tile some prediction actually names:
   where the body is (weighted up the longer it has stood still), where its
   last observed heading takes it, or where the objective is pulling it. A
   curve that a wall or a strict corner will eat is a wasted tempo beat, and
   tempo is the whole point of standing off.
4. Otherwise the feet: hold the band, open the fan by rotating, or walk.

## Postures

| Posture | When | What it does |
| --- | --- | --- |
| **Deny** | contact, and a body is on or beside the point | hold the station, cover the approach, shoot |
| **Seize** | no contact, nobody contesting, we already claim, they are worn down, or the clock has run out of patience | take the ground |
| **Contest** | they hold a claim worth breaking | walk in and break it |
| **Reposition** | loaded gun, visible body, no legal line from here | take the angle — but never off ground already held |
| **Withdraw** | one hit from death, gun cold, inside their reach | give exactly two tiles back |

The station is one standoff behind the near edge of the contested point, offset
laterally by this slot's rank in its team's stable ordering, so allied bodies
hold different fans instead of stacking one line. Two fans that cross mean a
dodge out of one is a step into the other. When the front sits against our own
base there is no ground left behind it, and the search takes a flanking tile
beside the point rather than walking into the back wall.

Late is not never. The endgame commit reads the capture arithmetic and the
territorial channel out of the contract: when the ticks left no longer cover a
capture — doubled while not ahead — patience stops being a strategy, and Still
Water goes and stands on the point.

## Contract discipline

Nothing above is written into the source as a number. Forms, ranges, cooldowns,
the bend window, launch and travel cadence, capture threshold and pause, the
front axis, objective regions, unit slots and their unlock clocks, and the
opposing chassis's stats all come from `StartLife.Contract`; action codes and
argument domains come from that tick's legality mask. The opposing form
catalogue is derived by subtracting our own slots' reachable forms — closed
over declared transition routes — from the catalogue, so a mirror correctly
concludes the opponent is exactly as dangerous as we are.

## Files

| File | What it holds |
| --- | --- |
| `StillWater.cs` | the tick ladder and every posture decision |
| `Doctrine.cs` | one-time contract reading: front axis, capture maths, both chassis profiles, the standoff band |
| `ForkPlanner.cs` | trajectory algebra — enumeration, impact timing, and wall/corner-exact coverage |
| `Quarry.cs` | life-scoped enemy tracking and the per-tick forecast that turns a bend into a prediction |
| `ThreatField.cs` | incoming bolts across every continuation still legal, plus muzzle coverage |
| `Field.cs` | walkability, cost fields, ray clearance |
| `ActionBook.cs` | this tick's legality mask indexed by the contract's own action kinds |
