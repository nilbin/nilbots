# march-wall — A WALL THAT CAN BE PICKED UP IS A WALL THAT CAN ADVANCE

**Class:** bulwark · **Lineage:** march-wall-v1 (revision 5) · **Role:**
verdict-doctrine · **Target:** cumulative T4 · **Arm:** the open game —
`--pendulum keel --skills kit --bend universal --aim offset --stance-ground open`,
facing-locked (`frontline-labs-1-bulwark-vs-bulwark-sail-open-facing-locked`)

Revision 2 found the geometry: a bulwark's gun fires along its facing, so the
wall is the set of straight lanes our guns close, not the tiles we stand on.
Revision 3 priced the pendulum on top of it — a hold clock, a weight-scaled
election, a forward rally. Revision 4 spent the kit: a lane we are losing is a
lane we can turn around, because a contact arriving inside a guarded arc dies
and is relaunched from our tile along the exactly reversed heading under our own
ownership. All three still decide most ticks and none of them changed.

Revision 5 is about a door that used to be one-way, and about a floor that used
to be a wall.

## What the arm changed, and what that is worth

Two contract facts moved, and both are read rather than assumed:

- **The turret is a true cycle.** `irreversibleForLife` is `false` on the
  routes in *both* directions, and there is no entry heal any more: health maps
  by `preserve-ratio-floor-minimum-one`, so a full-health body cycles
  losslessly (4/4 ⇄ 7/7 for a child, 5/5 ⇄ 7/7 for the Prime) and a wounded one
  loses the remainder to the floor on every round trip. `Cycle.cs` computes that
  from the declared policy IDs rather than from this arm's table, so the same
  reader gets the neighbouring arm right — where the anchor is reversible, the
  *mobilize* is not, and the entry pays a flat +2.
- **Placement is open.** Every route's `placement.forbiddenTileTags` is empty.
  The objective tiles and the whole central corridor are legal ground for a
  segment and for a shield, for the first time in five revisions.

## The doctrine, in four decisions

**Mobilize to advance.** This is the verb that did not exist and the half of the
cycle that pays. A turret has objective weight **zero**; a fortified body
watching an enemy build a claim on ground nobody of ours stands on is a gun
losing a match it could walk into. So it stands up — but only where it cannot
shoot the point instead (a turret's gun fires every tick at eight headings and
clears a body off the point in four, a quarter of a capture window), and the
floor prices the trip: a repositioning walk is bought only at full health, a
scoring emergency at any.

**Fortify anywhere; ration unchanged.** A one-tile corridor upstream of the
point is legal now, and bodies block bodies, so a segment there is a physical
**gate** as well as a gun — the one placement that denies ground without
standing on it, and therefore the only one the presence ration can afford. It is
taken only when our own side can still reach the objective without it, tested by
one walk with the tile removed. The objective itself is legal too and usually
wrong: priced, not forbidden. What did **not** change is the ration, and that is
a measurement rather than a preference — relaxing it because the door is now
two-way cost **ten wins in sixteen cells**, because presence is not a tactic on
this ruleset, it is the scoring channel.

**Decline the shell to a swarm.** The shell is opponent-shaped. Against one body
poking one lane it is the best trade this class owns; against several arriving on
different bearings it is a body that has agreed to stand still while being
flanked, because it guards one quadrant and cannot rotate. So the raise is
declined once more than one body can reach our tile and any of them can arrive
from outside the arc. Only survival overrides it. Worth **twelve wins**.

**Keep the shield where it now belongs.** With placement open, a shell can stand
**on the point** — objective weight 1, deflecting, and impossible to shoot off.
That is the property the class card advertises and that revision 4 measured as
unreachable. It is now the wall's scoring engine: 3 064 of 5 500 shell-ticks are
spent on contested ground, against **zero** on the arm one flag away.

## What the ±45° offsets needed: nothing

`FireControl.cs` and `Lane.cs` are **byte-identical to revision 4**. They
enumerate the declared `shotProgram` envelope — `minInitialAimSteps` /
`maxInitialAimSteps` and the bend bounds — so the moment the arm set the offsets
to ±1 the wall began firing diagonals without a line of new code, and an
aim-only diagonal (zero bends) became the cheapest way to arrive from a bearing
an enemy arc does not cover, ahead of a bend, because the cost order is bends
then path length. 198 diagonal launches over sixteen matches say so.

The one posture rule invented on top of them — choose the resting facing whose
three-bearing fan covers the most of the approach, and never owe a rotation to a
flanker — is **not** in this artifact. It reads well, it was implemented, and it
cost six wins and 153 territorial and got twenty-five more bodies killed, because
the approach set moves every tick and under `facing-locked` a rotation is also
the unlock for a step: a body re-posturing every tick never steps and never
shoots. The bearing is back. See `DX.md`.

## Contract-driven, not arm-driven

Nothing here names a form, a transition, an action code, a map tile, a class or a
token. A stance is recognized by what its target form declares; a cycle by
`irreversibleForLife` on both legs and the health policy on each; a gate by tile
shape; the capture window by the declared threshold and gain. One repair this
wave came from the same discipline: "cannot move" stopped meaning "is a gun
emplacement" when the kit added a second immobile form, and revision 4 got the
right route only because `anchor-…` sorts before `shell-…`. A guard declares
itself; an emplacement is what is left.

That the artifact plays an arm whose doors are one-way is measured, not claimed:
on `sail` — the same flags without `--stance-ground open` — it reads a mobilize
that is irreversible and a shell whose tags are restored, keeps revision 4's
ladder, and lands 8-7-1 against the same opponent.

## Reading it

- `MarchWall.cs` — the doctrine ladder for a mobile body, a turret, and a shield.
- `Cycle.cs` — what a fortify/un-fortify round trip costs, from the declared
  health policy. **New this revision.**
- `AnchorPlanner.cs` — where the next segment goes now that everywhere is legal.
- `Stance.cs` — what a stance is, its budget, and which bearings an arc covers.
- `Pendulum.cs` — the published hold.
- `Lane.cs` — what a gun could reach from a pose we do not hold yet, and which
  headings it would arrive on. Unchanged.
- `FireControl.cs` — every tile this body can put a bolt on this tick, and from
  which bearing. Unchanged.
- `Threat.cs` — where hostile bolts are going, when, and what they cost.
- `ContractView.cs` — one immutable reading of the resolved contract.
- `Navigation.cs`, `Geometry.cs` — stepping, tile geometry, and the corridor test.
- `ArenaBasics.cs` — the shared template helper, synced verbatim.
