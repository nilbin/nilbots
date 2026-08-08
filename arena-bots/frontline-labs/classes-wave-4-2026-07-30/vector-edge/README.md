# VectorEdge — pressure duelist (striker)

**Class:** striker · **Lineage:** vector-edge-v1 · **Revision:** 4 ·
**Role:** verdict-doctrine

> Ground is the only score. Every tick either takes a tile, holds a tile, or
> removes the body standing on it.

## The idea

Ground is the only score, so the whole match reduces to one question — *what
does the front need from this body, this tick?* — and the contract answers it.
A baseline Frontline pays for *sole* presence: two bodies on one objective
cancel out and the claim rots, one body alone banks a point a tick, and there
are exactly two ways to be the only one standing there — arrive first, or shoot
the other one off. VectorEdge does both, in that order, and never trades the
second for the first.

But "sole presence pays" is a rule this contract happens to declare, not a law.
Another one scales capture pressure with surplus objective weight, and then a
second body beside the first is the fastest capture on the board. Another
protects a completed advance for a while, and then a capture finished inside
that window resets its own claim and moves nothing at all. Revision 3 reads
those declarations instead of assuming them — see below.

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

## Revision 4: a special is worth the ticks it costs, and one of them isn't

The class-skill kit hands a striker one new verb and puts two more on the other
side of the board. Revision 4 prices all three the way this lineage prices
everything — against the tick they spend — and one of the answers is *no*.

**What the target can reach stopped being a prior and became geometry.** The
dodge model used to spread a target's probability over a ball of tiles and lean
on a stickiness multiplier where that ball was wrong. Under `facing-locked` it
is very wrong: the movement mask offers the current facing and nothing else, so
a body runs down the line it is already on and pays a whole rotation tick before
it can step anywhere else. Two ticks of that reach five tiles, not thirteen. The
solver now searches *pose* — a tile and a facing, a tick that is either a turn or
a step along it — so the reachable set is the contract's own legality instead of
an approximation of it. This is the largest single effect in the revision: it
re-prices every straight bolt and every bend on the phase-2 board, and it is
where the measured improvement comes from.

**A shield's arc is fixed, so the answer is to go around it.** A form declaring
`projectileGuard` deflects any contact arriving inside its facing quadrant, and
it cannot rotate while raised — so the arc is a piece of map, not a threat that
tracks. A bolt into it buys nothing and launches one back owned by the other
team. So the solver scores that contact as dead, taxes the returned bolt, and
finds the bend that curls to a flank instead; the router treats standing inside
the arc as a cost. Against a shell-raising fixture built from this same source,
revision 3 fed nine bolts into the arc across five matches and revision 4 fed
zero, shifting from 26% bent shots to 42%.

**And the fan is a losing weapon here.** The contract says what a cast costs:
two wait-only ticks to enter, exactly one launch (the return counter is
`attacks-issued-since-entering-source-form` at threshold one), a forced exit
windup, and a stance gun whose cooldown is more than double the mobile gun's.
Four ticks and five ticks of cadence buy one fan; the same window buys two
mobile bolts and two steps. What the fan uniquely sells is *bearings* — three
rays diverging from tile one, covering the diagonals a striker's gun can never
open on, because its shot program declares no initial aim offset and its
earliest bend is a tile out. Against one body that is not enough bearings to pay
for it, because a fan can only take one damage off any single target.

That is not an argument, it is a measurement. Three separate gates that let the
cast happen at all — fifty casts, thirty-four casts, and a version with the value
bar effectively removed — all landed on the same record: two wins worse and 1.3
territorial points worse than the identical doctrine with casting compiled out,
and every breach loss in the wave traced to a stance. So the route is read, kept,
and priced, and it is taken only where the ticks were genuinely free: from a seat
the stance's own objective weight holds, on a tick with no step worth taking, or
against a body that cannot move at all. On the measured board those conditions
and a fan worth casting never coincide, and the honest report is a cast count of
zero rather than a special used because it exists.

**Envelopment over concentration.** Both specials in the kit are paid to punish a
shared bearing: a fan sweeps three rays from one gun, and a shield answers
exactly one bearing and nothing else. So a body picks a destination on a bearing
no ally already holds, tie-broken steps avoid fans, lanes and arcs, and a body
standing in a fan that can launch this tick steps out of it — movement resolves
before combat, so the tick a stance first appears is still a tick in which one
step buys the whole launch. It never concedes an objective tile to do it.

**The hold is asked now.** `holdOwnerTeamId` and `holdEndsAtTick` are published
on the mode observation, so revision 3's reconstruction — the redeploy clock run
backwards for the start, and a guess from the front's displacement for the owner
— is gone from the live path. It survives as a fallback for exactly one case the
published fields cannot be told apart from by value: a ruleset that declares a
hold, sitting inside the redeploy pause only an advance can start, reporting no
owner. Everywhere else a null means what it says.

## Revision 3: ground is priced from the contract, not from the rule card

Every tick, VectorEdge asks the capture policy three questions and lets the
answers decide what the tick is worth.

**Does standing here earn anything?** Under binary control an enemy body on the
objective cancels this one and the only capture is to remove it. Under a policy
where surplus weight scales pressure, a contested objective this team
outnumbers still pays every tick — so the same tile is worth holding rather
than clearing, and a spare gun belongs *on* the ground instead of covering it
from the ring.

**Would a capture completed now actually move the front?** Where a completed
advance is protected by a hold, a capture finished inside that hold is
discarded: the claim resets and the objective does not budge. Ticks poured into
it buy a bank, not ground. So while an opposing hold is live, the ground stops
outbidding the duel — the body fights instead of grinding — and if waiting
reaches a real advance sooner than grinding does, it steps one tile off and
lets the claim sit until the hold expires. Both plans are counted in ticks and
compared; which one wins turns over as the hold runs down, and the arithmetic
is all contract values.

The hold's start is not something the observation reports. It is recovered from
something that is: an advance sets the redeploy clock, so running the declared
redeploy arithmetic backwards from it names the tick the front last moved.
*(Superseded in revision 4: the observation publishes the live hold's owner and
end tick, so this derivation is now a fallback only — see above.)*

**Where will the next body appear?** A contract that lands automatic arrivals on
the own-side objective has already delivered the second gun to the front, so
walking home to build one only removes the first gun from it.

None of this branches on an arm's name, and on a contract that declares none of
those things all three readings collapse onto what revision 2 assumed — its
decisions there are identical, tick for tick.

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
Where control is binary the nearest body to the objective owns the capture and
the others take the supporting ring rather than stacking on a tile that pays
nothing extra; where the contract scales pressure with surplus weight they all
go to the objective, because there the second body is the capture. Every
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
- **Squatting in a stance.** A stance is entered to be spent. With nothing worth
  a fan from any bearing it may legally take, this body drops the stance rather
  than hold a form it cannot move in — the budget was never the reason to leave.
- **A special used because it exists.** The fan is read, priced and measured, and
  on the measured board it declines to fire. A doctrine that casts anyway is
  spending ground to look like it is using the kit.
- **Hard-coded anything.** Participant and team IDs, slot counts, unlock ticks,
  form names, action codes, map tiles, objective coordinates, projectile
  constants, shot-program bounds, collision policy, movement facing coupling,
  control policy, decay clock, advance-hold length, arrival placement, stance
  routes, stance budgets, fan width, guard arcs and the number of slots either
  side may field are all read from `StartLife.Contract`, the frozen observation
  and the per-tick legality mask. The same source plays the base Labs contract,
  the duel-depth arms, the striker class arm, all three movement arms, every
  pendulum counterweight and the whole class-skill kit without branching on any
  of them by name.

## Layout

| File | What it holds |
| --- | --- |
| `VectorEdge.cs` | the doctrine: one ordered priority list per tick |
| `Doctrine.cs` | everything resolved once from the match contract |
| `Skills.cs` | the class-skill kit recognized by shape: stance routes, budgets, guards, slot counts |
| `Cast.cs` | when a fan beats the gun, and how to conduct one once standing in it |
| `Field.cs` | the per-tick read: threat, occupancy, objective, fans, arcs, roles |
| `ShotSolver.cs` | which legal shot program to commit to, and why |
| `DodgeLedger.cs` | what the watched bodies actually did, tick by tick |
| `Advance.cs` | what the declared capture policy makes this ground worth |
| `Ballistics.cs` | local replay of the declared projectile path rule |
| `ArenaBasics.cs` | the generated starter helper, synced from the template |
