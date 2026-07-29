# SparkLine — TEMPO ENGINE

Class: **fabricator**. Lineage `spark-line-v1`, revision 2. Role:
verdict-doctrine.

## The idea in one sentence

Win the objective clock with **bodies per tick**: queue every companion the
instant the contract makes it legal, put those children in the field beside the
prime rather than behind it, and keep the fragile prime alive only to the
extent that keeps it spending fabricate actions.

## What revision 2 changed, and why

Revision 1 counted presence. It did not ask whether presence was *paying*.
Those are the same thing only while nobody is standing next to you, and the
gap between them produced every one of wave 1's characteristic results: a
27-loss column against guns that outranged the sensor, and 400-tick mirrors
at exactly zero progress.

So the doctrine now names the distinction: **presence only pays while it is
sole presence**, which splits every tick into one of three regimes.

**Holding alone — stand where the walls do the work.** Exposure is a static
count of the firing lines the map allows onto a tile, computed once per life
from the widest gun the *ruleset* declares. It needs no sighting, which is the
point: a body being shot from eight tiles by a gun its six-tile facing quadrant
cannot see still knows which tile of its own region is sheltered, and steps
there without giving up a single capture tick. On the standard centre region
that is the difference between the open corridor row and a pocket the walls
close off entirely.

**Holding alone and safe — spend nothing.** A step out of every declared reach
into one inside it buys a shot the opponent can answer, and the clock was
already running my way. The bolt already in flight is judged by its *declared
remaining travel*, never by the direction it happens to point: a shot two
advances out that expires one tile short is not a threat, and dodging it is
strictly worse than ignoring it, because the dodge is the thing that walks into
the range the shot could really cover.

**Contested — tiles are cheap, lines are everything.** When the ground is
shared, nobody is scoring, so the arithmetic inverts: a survivable hit is a
fair price for a firing line, and the tile-safety vetoes that are correct while
winning are exactly what turns a shared objective into a 400-tick draw. The
body spends steps to align its gun, and takes them during cooldown too, because
the tick was worth nothing anyway.

**And when the contest cannot be seen, look for it.** The contract reports the
contest even when the quadrant cannot: sole presence would have made the claim
mine, so an absent claim while I stand on the region *is* another body. Wave 1's
zero-progress mirrors were two blind bots three tiles apart, each facing the
enemy base and therefore away from each other. One rotation toward the part of
the region the observation reports as unseen ends that.

## What the doctrine actually does

**Queue first, always.** Fabrication is the first thing every tick asks for,
ahead of shooting, evading, and walking. The fabricator's slots open earlier
and rebuild faster than anyone else's, and the contract declares that a
source's death does not cancel a queued bundle — so a prime that is about to
die should still spend its last action on a companion. A fabricator that never
queues has no army at all; that is the whole risk of the class and this bot
takes the opposite side of it.

**Place children forward.** The host takes the *first eligible declared offset
relative to the source pose at queue time*, and the declared order starts
behind the source. Facing is therefore the only lever a fabricator has over
where its child appears. When the front is quiet, SparkLine spends one tick
turning so that the "behind" offset points at the objective, and the child
materialises between the prime and the contested ground instead of behind it.
Under contact it skips the flourish and queues immediately — tempo beats
polish.

**Presence over marksmanship.** The team elects one occupier — the nearest
body that is *not* still able to fabricate, so the prime screens while a child
holds the tiles — and that body walks onto the contested region before it
trades a long-range shot. Capture progress is the only thing that moves the
score; a bolt fired from six tiles at a body that will dodge is worth less than
the capture ticks spent firing it. Surplus bodies take the tiles between the
objective and the enemy approach and suppress entries from there.

**Suppression, not concession.** Evasion is a comparison, not a reflex. Each
candidate tile is rated by how many projectile advances away its first contact
is, whether it keeps a legal trajectory on a visible enemy, and how many escape
hatches it leaves. A body standing on the objective will only step to another
objective tile; it concedes ground only when the alternative is death. When
nothing is safer than standing still, it stands still and answers — and a shot
whose declared remainder cannot reach this tile is not a reason to move at all.

**One doctrine, three movement arms.** The movement profile's declared facing
coupling is read from the contract, never assumed. Where a step turns the body,
every trajectory is scored with the facing the step will actually leave behind
instead of the four it might reach with a further rotation, and the last step of
an approach is tie-broken by where the child it enables would land. Where
movement is locked to the facing, a direction the mask does not offer is
reached by turning first — an explicit, priced "turn to walk" rather than a body
that silently refuses to path.

**Replace, don't mourn.** Losses are expected. The rebuild clock is the
shortest in the slate, so a destroyed companion is re-queued the tick its slot
returns to Ready; the prime walks back to its fabrication source whenever the
contract binds one to a region and a slot is ready or nearly due.

## Deliberate omissions

SparkLine never anchors and never splits, even on contracts that offer them. A
turret has objective weight zero, and a split trades one healthy fabricator for
two fragile bodies that cannot fabricate again. Both are the opposite of a
doctrine whose currency is mobile presence and rebuild rate.

## Contract-driven, not class-coded

Nothing above is keyed to a ruleset name. Fabrication route, placement offsets,
source and output regions, forbidden output tags, unlock and rebuild clocks,
form health and objective weight, projectile range, cadence and bend envelope,
objective regions and advance direction, protected pads and reserved spawn
tiles are all read from `StartLife.Contract` and the per-tick legality mask.
The same policy runs unchanged where fabrication happens anywhere in the field,
where it is bound to a home pad, and where companions activate automatically
and `fabricate` is absent from the catalog entirely.

## Contract-driven, not class-coded (revision 2 additions)

Movement facing coupling, the widest declared projectile travel in the
catalog, the corner rule, per-tile exposure derived from those, and the mode's
own claim/pause state are all read the same way — from `StartLife.Contract` and
the per-tick observation. Direction tie-breaks go through the shared
`OrderedDirections` helper, which orders by this team's advance direction and
randomises the two laterals from the per-life deterministic stream; an absolute
order is a measured team-side bias on a mirror-symmetric map, and wave 1's
seed-identical results across three seeds were that bias showing.

## Files

- `SparkLine.cs` — the policy and its priority order.
- `ContractLens.cs` — every static fact resolved once per life from the
  contract, including the per-tile exposure map and facing coupling.
- `Tactics.cs` — reachability fields, the host's own shot-path rule replayed
  locally, declared-remaining projectile projection, and clear-ray geometry.
- `ArenaBasics.cs` — the current template helper, synced verbatim; the policy
  uses its `OrderedDirections` for every direction tie-break.
