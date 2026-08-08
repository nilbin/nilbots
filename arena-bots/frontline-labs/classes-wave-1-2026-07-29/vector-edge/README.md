# VectorEdge — pressure duelist (striker)

**Class:** striker · **Lineage:** vector-edge-v1 · **Role:** verdict-doctrine

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

## Tempo

A shot costs the tile the tick would have bought, so fire is rationed by
expected value: high while advancing, cheap while already standing on the
objective (the tick was free anyway), cheapest when an enemy body is on the
objective and shooting it is how the ground gets taken. When the body is
holding and has nothing better to do, the leftover ticks go into fire and
facing — which is why the gun is usually already laid when contact arrives.

## Suppression over concession

The reflex to step out of an inbound bolt is how positions get given away for
free. VectorEdge dodges *within* the objective when it can, and when it cannot
— and it can survive the hit — it answers the shot instead of surrendering the
tile. Health is a resource; the frontline is the score.

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
- **Hard-coded anything.** Participant and team IDs, slot counts, unlock ticks,
  form names, action codes, map tiles, objective coordinates, projectile
  constants and shot-program bounds are all read from `StartLife.Contract` and
  the per-tick legality mask. The same source plays the base Labs contract, the
  duel-depth arms and the striker class arm without branching on any of them.

## Layout

| File | What it holds |
| --- | --- |
| `VectorEdge.cs` | the doctrine: one ordered priority list per tick |
| `Doctrine.cs` | everything resolved once from the match contract |
| `Field.cs` | the per-tick read: threat, occupancy, objective, roles |
| `ShotSolver.cs` | which legal shot program to commit to, and why |
| `Ballistics.cs` | local replay of the declared projectile path rule |
