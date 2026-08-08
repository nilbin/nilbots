# march-wall — A WALL THAT MARCHES IN ORDER IS A WALL THAT ARRIVES

**Class:** bulwark · **Lineage:** march-wall-v1 (revision 6) · **Role:**
verdict-doctrine · **Target:** cumulative T4 · **Arm:** the deck game —
`--pendulum keel --skills kit --bend universal --aim offset --stance-ground open`,
facing-locked (`frontline-labs-1-bulwark-vs-bulwark-sail-open-facing-locked`)

Revision 2 found the geometry: a bulwark's gun fires along its facing, so the
wall is the set of straight lanes our guns close, not the tiles we stand on.
Revision 3 priced the pendulum on top of it. Revision 4 spent the kit: a lane we
are losing is a lane we can turn around. Revision 5 spent the open ground and
the reversible door — the shell holds the point, and a wall that can be picked
up is a wall that can advance. **All four still decide most ticks and none of
them changed.**

Revision 6 is not about the wall. It is about the fact that there are three of
them, and that until now each one was written as if it were alone.

## The bug the owner watched

Five revisions of this lineage reasoned about one body at a time. A wall is
several bodies, the approaches to the centre run through two-tile pinches, and
the resolved contract is explicit about what that costs:

```json
"actorsBlockActors": true,
"sameDestinationMovesBlockAll": true,
"swapMovesBlocked": true,
"followingVacatedActorAllowed": false
```

That last field is the whole thing, and it is the one nobody expects: **a column
cannot advance in lockstep.** When the leading segment steps out of a corridor
tile, the follower's step into that tile is refused *anyway* — the tile is only
free from the next tick on. So a wall marching nose-to-tail does not merely move
slowly; it spends every second tick submitting a move the engine has already
decided to reject. Measured on the rebuilt wave-5 artifact: **40 refused steps per
sixteen matches whose destination was one of our own bodies**, and 1 665 ticks
spent waiting beside a sibling.

Worse, and invisible in the block counter: the wave-5 gate doctrine put a *turret*
in a pinch on purpose, because bodies block bodies and a segment in a corridor is
a physical gate. It tested that the map still connected our side to the point.
It never tested whether the segment behind it still had a route. With that one
test added, this artifact breaches the enemy base in **11 of 16 cells**; without
it, in **0 of 16** — the wall used to seal its own advance and then win on
territorial or not at all.

## What coordination can be, given the rules

There is no channel. Private memory is life-scoped, a fresh body inherits
nothing, observations are frozen before any decision executes, and the rules card
says a life never sees an ally's current action. So a march order cannot be
negotiated — it can only be **re-derived**. Every rule in `Column.cs` is a pure
function of the frozen shared observation plus the contract: each life runs the
same code over the same inputs, derives the same total precedence order over the
same bodies, and therefore agrees with its siblings about who yields without
anyone being told.

A sibling's *route* is the shortest wall-only walk from its tile to the contested
objective, computed with the same search and the same tie-break its own
pathfinder uses, widened by the continuation of its last accepted move — which
the observation publishes. "This tick or next" is the first two tiles of that.

**The precedence order, in four terms, all published:** an occupant of a pinch
outranks everyone outside it; then nearer the objective goes first, because the
front of a column is the only body whose step unblocks the ones behind it; then a
scorer outranks a weight-zero gun; then stable slot and life break the tie.

## The five rules, and what each is worth

Measured over 32 cells — two independent eight-seed sets, both team sides —
against the wave-5 source rebuilt with the current SDK. The control is this exact
source with every clause off, which is **behaviourally byte-identical** to the
predecessor: 1 642 turns on a shared seed, zero divergences.

| | record | territorial | breaches |
| --- | --- | ---: | ---: |
| **revision 6** | **29-1-2** | **+700** | 20 |
| the same source, coordination off (= revision 5) | 5-5-22 | +0 | 0 |

| rule | what it refuses | worth |
| --- | --- | --- |
| **choke precedence** | a step onto a tile a sibling stands on, and a second body inside one corridor run | +5 wins, +205 |
| **route yield** | standing or stepping where a better sibling walks this tick or next; vacate if we already do | +6 wins, +308 |
| **gate discipline** | anchoring or shielding on a tile a sibling's route needs, or sealing a run our side still walks | +5 wins, +389 |
| **rally traffic** | our own feet and any emplacement on the tile a due arrival lands on | +1 win, −42 |
| **spacing** | two bodies inside one accepted enemy action — a fan, or the bolt a guarded arc sends back | +12 wins, +647 |

The one written exemption, and it is written rather than inferred: a body that is
our side's only objective weight on a contested objective **does not** step off it
to unblock a gun. Presence is the scoring channel; the sibling routes around.

Spacing being the largest is the finding this pass did not expect, and the
mechanism is this class's own: poking a guarded arc relaunches our bolt from the
shell's tile back down the lane it went out on, so the tile behind our shooter is
a queue for our own fire. In a bulwark mirror both sides field shells, and a rule
written for a striker's fan turned out to be a rule about us.

## Contract-driven, not arm-driven

Nothing here names a form, a class, a transition, a tile or a map. A corridor is a
shape test. A fan is `projectilesPerAttack > 1`. A guard declares itself. An
arrival's landing tile is derived from the contract's rally policy and our own
signed advance delta, never from a compass. And every exclusion is gated on the
declared collision policy: a ruleset with `actorsBlockActors: false` gets no march
order at all, and one with `followingVacatedActorAllowed: true` gets only the
corridor allocation, because there a queue simply flows.

One clause was cut for being measurably nothing: "an immobile sibling is a wall,
not a transient blocker" is byte-identical to not having it while choke precedence
is on, and two wins *worse* in isolation. It survives only as the floor of choke
precedence, for the arm that allows the follow.

## Reading it

- `Column.cs` — the march order: precedence, routes, chokes, arrivals, spacing.
  **New this revision, and the only new policy in it.**
- `MarchWall.cs` — the doctrine ladder for a mobile body, a turret, and a shield.
  `StepAside` and `Spread` are new; everything else is revision 5's.
- `Navigation.cs` — stepping, route prediction, and corridor runs.
- `AnchorPlanner.cs` — where the next segment goes, now that a sibling's route is
  one of the things that decides it.
- `Cycle.cs` — what a fortify round trip costs, from the declared health policy.
- `Stance.cs` — what a stance is, its budget, and which bearings an arc covers.
- `Pendulum.cs` — the published hold.
- `Lane.cs`, `FireControl.cs` — firing geometry from a pose we do not hold yet,
  and every tile this body can put a bolt on now. Both unchanged since revision 4.
- `Threat.cs` — where hostile bolts are going, when, and what they cost.
- `ContractView.cs` — one immutable reading of the resolved contract.
- `Geometry.cs` — tile geometry and the corridor shape test.
- `ArenaBasics.cs` — the shared template helper, synced verbatim. Note that its
  `OrderedDirections` draws from `context.Random`; `Column` does not use it, and
  `DX.md` explains why that mattered more than it looks.
