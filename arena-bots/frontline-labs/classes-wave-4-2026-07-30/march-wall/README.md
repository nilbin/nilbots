# march-wall — A LANE WE ARE LOSING IS A LANE WE CAN TURN AROUND

**Class:** bulwark · **Lineage:** march-wall-v1 (revision 4) · **Role:**
verdict-doctrine · **Target:** cumulative T4 · **Primary cell:**
`--pendulum keel --skills kit --bend universal` (`rig`), facing-locked

Revision 2 fixed the geometry: a bulwark's gun fires along its facing, so the
wall is the set of straight lanes our guns close, not the tiles we stand on.
Revision 3 priced the pendulum on top of it: a hold clock, a weight-scaled
election, and a forward rally each decide what a tick of presence is worth. Both
still decide most ticks and neither changed.

Revision 4 spends the class-skill kit. Revision 3 had exactly two answers to a
duel it was losing — walk out of the envelope, or stand in it and be shot — and
its own notes record the second one measured as a loss, twice. The declared
shield is a third answer that did not exist before: a contact arriving inside a
guarded arc dies and is **relaunched from our tile along the exactly reversed
heading under our own team's ownership**. A lane we are losing becomes a lane
that shoots back.

## What changed, and why

**Raise.** A shield goes up when the tick would otherwise be spent being shot
at: a batch that kills us with nowhere to step, a body aimed at us while our
three-tick cadence is mid-cycle, or a lane whose health-and-cadence ledger has
turned against us while an ally holds the ground. The route's own
`placement.forbiddenTileTags` decide where that is legal, and on this map they
are decisive: **every objective tile and the whole central corridor carry
`transition-placement-forbidden`**, so a shield is never a way to hold contested
ground. It is what a body does beside the fight.

**Aim before the shot, not at the bolt.** A shield completes at the end of the
tick it is requested, after combat, so a bolt already in the air lands on a
mobile body. At the range this chassis duels at, a bolt is visible for exactly
one tick before impact — so the trigger that matters is geometric and precedes
the shot: a body whose declared firing envelope covers our tile, from bearings
our arc covers. The quantifier is the doctrine: **every** arrival heading it
could use must be inside the arc. Against a straight-only gun that is just "it
is aimed at us"; under the universal bend envelope the same body can curve a
bolt around the arc, and a shield raised into that spends two windups to be hit
anyway.

**Cycle.** The shield drops the tick it stops paying — flanked past the arc,
needed as objective weight, or quiet with the cadence back. What does *not* drop
it is the budget: `automaticReturn` spends itself on the third deflection and the
engine's forced return costs the same one-tick windup our own would, so leaving
early to save a deflection forfeits a bolt for nothing.

**Never poke an arc.** Fire control filters candidate shots by their **arrival**
heading against every visible guard, inside the tracer rather than over its
results, so what survives is the cheapest *accepted* shot. That is where a bend
earns its commitment: the straight bolt and the bent bolt reach the same tile and
only one of them arrives from a bearing the arc does not cover. Feeding three
bolts into an arc to force its break is the alternative the rules offer, and it
is refused — three of our bolts and three returns bought at our own cadence to
win an exit windup, when going around always works.

## Three repairs, all of them contract reads that had become assumptions

1. **A parameterless route was unstartable.** Revision 3 searched transition
   actions for a form-target constraint naming the form it wanted. The return leg
   of every stance and of every turret declares `parameterKinds: []`, so it
   matched nothing: across waves 1–3 a fortified body could never stand up in any
   class arm. The route names its action, the action declares its parameters, and
   `StartRoute` now reads both.
2. **A bolt's cadence and damage were assumed.** `ticksPerAdvance` and
   `damagePerHit` are published per projectile; revision 3 assumed one tick and
   counted bolts instead of summing health. A batch is now priced in health and
   dated by the bolt's own cadence.
3. **The ratchet hold was inferred.** `holdOwnerTeamId` and `holdEndsAtTick` are
   published, which retires the blind spot revision 3 documented at length — a
   life born after the redeploy pause could not see a live hold at all, and under
   a forward rally that is the common case. The derivation survives only as a
   fallback for a contract that declares a hold and never publishes one, and it is
   retired permanently the first time a hold is read.

## Contract-driven, not arm-driven

Nothing here names a form, a transition, an action code, a map tile, a class or a
pendulum token. A stance is recognized by what its target form declares — a
`projectileGuard` is a shield, a `volley` on its attack profile is a fan — and
the budget comes from the return route's `automaticReturn`. The fan is
implemented on the same machinery this chassis uses for the shield, so an artifact
handed the striker's kit would cast; this one owns no fan and never does.
`classId` is read from the topology for identity, never parsed out of a form ID,
and never branched on for tactics: stat- and route-based counters generalize to a
chassis that does not exist yet.

That the kit code is inert where no kit is declared is measured rather than
claimed: on `keel` and on `veer` the full artifact and a kit-doctrine-off
ablation of it produce **identical records, margins and counters across sixteen
matches each**.

## Reading it

- `MarchWall.cs` — the doctrine ladder for a mobile body, a turret, and a shield.
- `Stance.cs` — what a stance is, what its budget is, and which bearings an arc
  covers.
- `Pendulum.cs` — the published hold, and the retired derivation behind it.
- `Lane.cs` — what a gun could reach from a pose we do not hold yet, and which
  headings it would arrive on.
- `FireControl.cs` — every tile this body can put a bolt on this tick, and from
  which bearing.
- `Threat.cs` — where hostile bolts are going, when they arrive, and what they
  cost.
- `ContractView.cs` — one immutable reading of the resolved contract.
- `AnchorPlanner.cs` — where the next wall segment goes.
- `Navigation.cs`, `Geometry.cs` — stepping and tile geometry.
- `ArenaBasics.cs` — the shared template helper, synced verbatim.
