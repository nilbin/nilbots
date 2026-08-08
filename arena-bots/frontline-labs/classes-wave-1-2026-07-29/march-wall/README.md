# march-wall — ADVANCING WALL

**Class:** bulwark · **Lineage:** march-wall-v1 · **Role:** verdict-doctrine ·
**Target:** cumulative T4

A wall is not a place you stand. It is a place you *move*.

## The idea

Most Frontline bots treat bodies as interchangeable fighters that all walk at
the contested tile. march-wall splits the team in two and never mixes them up:

- **The companions are the wall.** Every automatic or fabricated child walks to
  a choke beside the live objective and anchors into a turret there. A turret
  cannot capture — objective weight zero — and that is the point. Its job is to
  make the tile behind it unusable for the other team while somebody else does
  the capturing. When a segment is out of the fight and the contract offers a
  route back, it mobilizes, walks forward, and re-anchors at the new front. The
  line creeps; it does not garrison.
- **The Prime is not a wall segment.** It stays mobile behind the line, uses
  the chassis' durability to hold contested ground instead of trading it away,
  and is the only body that finishes a push. It fortifies exactly twice in a
  match's worth of situations: to run out a lead in the last stretch, or when
  the other team is one advance from breaching us. Both are worth the long,
  visible, punishable windup. Nothing else is.

## What that means tick to tick

**Choosing a choke.** A site must be a tile the declared anchor route would
actually accept, with an unobstructed turret ray onto the live objective, on
our side of the contested tiles, with few open neighbours — a pinch, not a
field. Distance one is penalised: a turret pressed against the objective plugs
its own team's approach lane. Two children of the same team split onto opposite
flanks by stable unit id, so the wall widens instead of stacking.

**Suppression over concession.** A turret shoots whatever stands on the
objective, and when there is no body to shoot it denies the tile they are
walking into — but only with a straight bolt whose arrival is no earlier than
they could get there. A curve is a commitment; it is spent on bodies, never on
guesses.

**Absorbing versus dodging.** A body on the objective steps off an incoming
bolt only to another tile of the same region. If the region has nowhere to
stand, it eats the hit and keeps the ground. It abandons the tile only when the
batch would kill it.

**Bodies before position.** When the contract has explicit fabrication and a
slot is Ready, the Prime walks back to its declared source region and raises
the companion, because the wall needs substance more than it needs one more
tick of presence. It refuses that trip only while it is the single weighted
body on a contested objective, and even then only for a bounded number of
ticks.

## Contract-driven, not class-driven

Nothing here names a form, an action code, a map tile, or a class. A form is
"fortified" when its declared action mask contains no movement action. Anchor
and mobilize are whichever same-life transitions run from this form into a
fortified form and back. Companions arrive by explicit Fabricate or by declared
automatic activation, and the same code covers both. Aim envelopes, bend
bounds, projectile range, cadence, launch distance, strict corners, reserved
spawn anchors and transition-forbidden tiles are all read from the resolved
contract — which matters, because the qualification profile has no aim offset
at all, and there the facing *is* the firing envelope.

Where a route is absent, the doctrine step simply does not fire and the body
falls back to taking and holding ground.

## Reading it

- `MarchWall.cs` — the doctrine ladder for a turret and for a mobile body.
- `ContractView.cs` — one immutable reading of the resolved contract.
- `AnchorPlanner.cs` — where the next wall segment goes.
- `FireControl.cs` — every tile this body can put a bolt on this tick.
- `Threat.cs` — where hostile bolts are going to be.
- `Navigation.cs`, `Geometry.cs` — stepping and tile geometry.
