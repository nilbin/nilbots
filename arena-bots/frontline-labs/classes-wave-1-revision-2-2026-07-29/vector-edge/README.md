# VectorEdge — pressure duelist (striker)

**Class:** striker · **Lineage:** vector-edge-v1 · **Revision:** 2 ·
**Role:** verdict-doctrine

> Ground is the only score. Every tick either takes a tile, holds a tile, or
> removes the body standing on it.

## The idea

Frontline pays for *sole* presence. Two bodies on one objective cancel out and
the claim rots; one body alone banks a point a tick. So the whole match reduces
to a single question — *can I be the only one standing here?* — and there are
exactly two ways to answer it: arrive first, or shoot the other one off.

VectorEdge does both, in that order, and never trades the second for the first.
It walks at the active objective from tick zero, stands on it, and spends its
firing tempo on the body contesting it rather than on whatever happens to
wander into the crosshairs.

## Why a striker fires the way it does

The striker's private verb is a committed trajectory: straight, or one 45°
bend after a chosen number of tiles. A bend is not free — it is the same tick
and the same cooldown as a straight bolt, spent on a path that arrives
somewhere the target might not be. So the choice is a read on what the target
can still do:

- **Corridors get straight suppression.** A body with two or fewer open
  neighbours cannot step aside. It can only advance or retreat, and both keep
  it on the line. A plain bolt down that line threatens every tile it could
  occupy for the next several ticks; a bend would just find a wall.
- **Open chambers get the bend.** Where a target has lateral escapes, the
  straight bolt covers exactly one of them. A bent path sweeps tiles no
  cardinal shot from any facing can reach, which is the only way to threaten a
  target that is not on a line from the gun.

Every legal program the contract declares is traced against the declared
projectile rules — launch tiles, tiles per advance, travel budget, strict
diagonal corners, wall termination — and scored against a dodge model of the
target: where can it be, at the tick each path tile is actually occupied,
weighted by the fact that it wants the objective too, and by the fact that a
body cannot react to a shooter outside its own declared sight envelope. A bend
must beat the best straight answer by a clear margin before it is taken, so
bends stay a weapon rather than a habit.

## Revision 2: the dodge model is measured, not assumed

Revision 1 wrote its opponent model down as a constant — a watching target was
assumed to hold its tile a little under half the time. Against most opponents
that is close enough. Against one that refuses to close and steps off every
single bolt, it is catastrophic, and the striker mirror showed exactly what it
costs: two duelists two tiles apart, both firing a shot the model valued above
one full point, both stepping aside, both stepping back, for hundreds of ticks
with the objective contested and neither side scoring a thing. A shot that has
stopped working keeps looking like the best thing on the board, because the
model that priced it never finds out it was wrong.

So the rate is counted now. Every tick, VectorEdge compares where each visible
body was with where it is, ignoring any body that could not see the gun — a
target that cannot see a shooter is not choosing to stand still, and its
stillness is not evidence about anything. The count starts at revision 1's
assumption, carries a few observations' worth of weight behind it, and moves as
evidence arrives. It is life-scoped, like every other private memory here: a
fresh life meets its opponent again from the prior.

What follows from that is the whole revision. Against a body that dodges
everything, the straight bolt's value collapses toward the tiles the target
will actually be standing on — which is where the bend was already looking, so
the bend wins the comparison it used to lose, and when neither wins, the tick
goes back to the ground. Against a body that holds its line, nothing changes at
all. And it generalizes across the movement arms without being told about them:
where the contract couples facing to movement a sidestep costs the target its
own aim and its own sight quadrant, and where the contract locks movement to
facing a sidestep is not even legal. Both make targets stickier, so both raise
the prior the ledger starts from — and then let the bodies on this map settle
it.

## Tempo

A shot costs the tile the tick would have bought, so fire is rationed by
expected value: high while advancing, cheap while already standing on the
objective (the tick was free anyway), cheapest when an enemy body is on the
objective and shooting it is how the ground gets taken.

Aim is rationed the same way, but against the weapon's cadence rather than
against position. A tick on which the gun is ready is worth one whole bolt, so
turning on one is nearly always a mistake; a tick inside the cooldown window
costs nothing at all. Revision 1 could not tell those two apart — it was only
able to ask "what would I hit facing there?" on a tick that could also fire —
so it paid for all of its aim with shots, and measurably lost two of every five
of those turns to a life that ended before the shot ever came out. The gun is
laid on the last tick of the cooldown window now, where the shot it buys is the
very next one.

## Suppression over concession

The reflex to step out of an inbound bolt is how positions get given away for
free. VectorEdge dodges *within* the objective when it can, and when it cannot
— and it can survive the hit — it answers the shot instead of surrendering the
tile. Health is a resource; the frontline is the score.

Where the contract couples facing to movement, that calculation gets sharper
rather than different: a dodge is also a turn, so it spends the aim and the
sight quadrant along with the tile, and a bolt already in hand that beats every
post-dodge answer is worth taking the hit for. Where movement is locked to
facing, a sideways dodge is a rotation this tick and a step the next — two
ticks against a bolt that lands on the first — so the escape set collapses on
its own and holding the ground is very nearly the only honest answer left.

## Bodies

Companions are a force, not a formality. Where the contract activates them on a
schedule they are simply used; where it requires an explicit fabrication, a
lone body will walk back to a declared source and build one — but only while
the front is far enough from home that a conceded push is not a conceded match.
The nearest body to the objective owns the capture; the others take the
supporting ring rather than stacking on a tile that pays nothing extra. Every
life sees the same frozen picture, so when two of them want the same step, the
one whose identity sorts later yields: coordination with no shared state.

Where a tougher, objective-neutral emplacement exists, a spare body that is not
carrying the capture may take it to lock an approach. It is deliberately a rare
commitment — an emplacement cannot take ground, and taking ground is the plan.

## Not in this doctrine

- **Splitting.** Halving a duelist into two one-hit bodies trades the thing
  that wins duels for presence that evaporates on contact.
- **Camping.** Distance is not safety in a mode that scores position.
- **An absolute sense of direction.** Every tie between two equally good
  directions is broken advance-first, retreat-last, with the two
  perpendiculars ordered by this life's own deterministic stream. An absolute
  preference is a systematic edge for whichever team happens to advance that
  way on a mirror-symmetric map, and both sides sharing it does not cancel out.
- **Walling off its own corridor.** A bolt of this team's own is only an
  obstacle where the contract says allied contact stops it. It does not here,
  and treating it as one anyway is how a duelist blocks the lane it just fired
  down.
- **Hard-coded anything.** Participant and team IDs, slot counts, unlock ticks,
  form names, action codes, map tiles, objective coordinates, projectile
  constants, shot-program bounds, collision policy, and movement facing
  coupling are all read from `StartLife.Contract` and the per-tick legality
  mask. The same source plays the base Labs contract, the duel-depth arms, the
  striker class arm and all three movement arms without branching on any of
  them.

## Layout

| File | What it holds |
| --- | --- |
| `VectorEdge.cs` | the doctrine: one ordered priority list per tick |
| `Doctrine.cs` | everything resolved once from the match contract |
| `Field.cs` | the per-tick read: threat, occupancy, objective, roles |
| `ShotSolver.cs` | which legal shot program to commit to, and why |
| `DodgeLedger.cs` | what the watched bodies actually did, tick by tick |
| `Ballistics.cs` | local replay of the declared projectile path rule |
| `ArenaBasics.cs` | the generated starter helper, synced from the template |
