# march-wall — THE LANE IS THE WALL

**Class:** bulwark · **Lineage:** march-wall-v1 (revision 2) · **Role:**
verdict-doctrine · **Target:** cumulative T4

v1 said a wall is a place you move. It was half right, and the half it got
wrong cost it thirty-six losses.

## What changed, and why

A bulwark's gun fires along its facing. That is four rays. A body standing one
tile off all four of them is unarmed — not outgunned, *unarmed* — no matter how
much health it is carrying. Reading my own wave-1 replays back, that is the
whole story: on seven out of every ten ticks where my body had a target in
sight and a gun off cooldown, no cardinal ray reached it, and the doctrine's
answer was to stand on the objective and be shot. The wall held ground it could
no longer shoot from.

So the wall is redefined. It is not the tiles we occupy; it is **the set of
straight lanes our guns close**, and every body spends its tick either closing
one or taking ground that lets it.

**Fight on the axis.** A mobile body that has taken its ground and has nothing
to shoot does not stand there. It turns into the lane if a turn opens it, and
steps onto a tile that has it if no turn does. It presses down its own lane
while the gun cycles, because a shorter flight is a bolt that is harder to step
off. When the health-and-cadence ledger has turned against it — ticks-to-kill
both ways, out of declared health, damage and cooldown, with allies who already
cover the target counted in — it spends the tick leaving instead, toward
whichever neighbouring tile that gun can reach from fewer of its facings.

**But ground comes first, and cover is never traded for a lane.** Taking the
contested position outranks every part of the duel: a body that stops in the
approach to trade has lost the thing both teams are actually scoring. And a
tile the enemy gun cannot reach is a tile already won — an enemy parked just
outside our range is parked just outside its own, and the step that fixes our
geometry fixes theirs for free. Hold, and make them come.

**Fortifying is rationed by presence, not by opportunity.** The turret is the
best gun this class owns — seven health, eight tiles, eight headings, and the
fastest cadence on the field — and it has objective weight zero. Every anchor
therefore trades a scoring body for a denying one. v1 let a companion anchor
whenever any other weighted ally existed and duly spent both of them; it ran
about one capturing body against opponents fielding two. The rule now: never
fewer capturing bodies than the other team has shown us, never more guns than
scorers, and never the last body that can take ground — until a lead is already
banked, where denial *is* the win condition and the ration lifts. A fortified
body also stands back up when the team runs out of scorers, or when the front
has moved out of its lanes and nothing is in its face.

**Stalemates are opened, not waited out.** Two bodies contesting one objective
that cannot reach each other is a stable state of these rules: control is
contested, progress decays to zero, and the clock runs out on both. The
doctrine counts its own idle ticks and, past a few of them, forces the position
open — the durable chassis is the one that should pay for that.

## Movement arms

Facing is this chassis' entire firing envelope, so the movement profile's
declared facing coupling is read before any step is planned.

- **face-movement-direction** — a step turns the body. Every candidate step is
  scored on the facing it *would* leave us holding, so a move that arrives
  already aimed beats one that needs a rotation afterwards. This costs a
  bulwark less than it costs a quadrant-vision class: our sight is
  omnidirectional, so turning never blinds us, only re-aims us.
- **facing-locked** — movement offers only the current facing. A path search
  that only ever starts from the legal direction walks a body into a corner and
  leaves it there for the rest of its life, which is exactly what v1 does here.
  Every route now falls back to *turning into the step* when the mask refuses
  it, which makes rotation a first-class movement primitive rather than an
  aiming tweak.

## Contract-driven, not class-driven

Nothing here names a form, an action code, a map tile, or a class. A form is
"fortified" when its declared action mask contains no movement action. Anchor
and mobilize are whichever same-life transitions run from this form into a
fortified form and back. Firing envelopes for both sides — ours and the body we
are fighting — are replayed from the declared aim, bend and travel rules
through the SDK's own path preview, so "can that gun reach this tile" is a
contract question rather than an assumption about a class. Aim envelopes,
projectile range, cadence, launch distance, strict corners, reserved spawn
anchors, transition-forbidden tiles, the allied-projectile collision policy and
the movement facing coupling are all read the same way.

Direction tie-breaks go through the shared template helper's mirror-fair order
— advance first, retreat last, laterals settled by the per-life random stream.
An absolute compass is a measured team-side bias on a symmetric map, and v1
carried it into the anchor-site tie-break as well.

Where a route is absent, the doctrine step simply does not fire and the body
falls back to taking and holding ground.

## Reading it

- `MarchWall.cs` — the doctrine ladder for a turret and for a mobile body.
- `Lane.cs` — what a gun could reach from a pose we do not hold yet.
- `ContractView.cs` — one immutable reading of the resolved contract.
- `AnchorPlanner.cs` — where the next wall segment goes.
- `FireControl.cs` — every tile this body can put a bolt on this tick.
- `Threat.cs` — where hostile bolts are going to be, and how far they can go.
- `Navigation.cs`, `Geometry.cs` — stepping and tile geometry.
- `ArenaBasics.cs` — the shared template helper, synced verbatim.
