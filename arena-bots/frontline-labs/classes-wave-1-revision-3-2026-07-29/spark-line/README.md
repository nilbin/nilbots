# SparkLine — TEMPO ENGINE

Class: **fabricator**. Lineage `spark-line-v1`, revision 3. Role:
verdict-doctrine.

## The idea in one sentence

Win the objective clock with **bodies per tick**: queue every companion the
instant the contract makes it legal, put those children in the field beside the
prime rather than behind it, and keep the fragile prime alive only to the
extent that keeps it spending fabricate actions.

## What revision 3 changed, and why

Revision 2 was authored for a frontline that always springs back. Two capture
fields change that, and only together: a declared **hold** makes a completed
advance stick, and a **weight-scaled control policy** makes a second body on
the ground worth a second unit of pressure. Revision 3 reads both from
`gameMode.capture` and asks one question with them — **is a body worth more
than the ground it is standing on?**

The answer is not the same on every contract, and this is the whole revision:

- **Where control is binary, it is not.** One enemy body nulls any number of
  mine, so the screen that keeps him off the objective *is* the claim. Nothing
  changes: revision 3 is decision-for-decision identical to revision 2 on the
  unmodified control contract, on the numbers-only contract, and — measured,
  not assumed — on a sticky-plus-forward-rally contract too. A hold on its own
  does not make a body worth more.
- **Where surplus weight scales the gain, it is.** He can subtract one, not
  null me, so the screen's job shrinks to something the occupier absorbs, and
  every surplus body is free to be spent on the two things that now pay:
  walking to the ground the front is about to reach, and staying alive to build
  more bodies.

Three consequences, each derived from a field rather than a habit:

**Take the next ground before you own this one.** Under a contract that locks
an advance, a capture about to complete is as good as completed — the front is
going to move and it is going to stay moved. So the bodies that are not holding
the current objective leave for the next one in the chain, timed so the walk
and the advance land together: leave when the walk left is no shorter than the
claim left, and arrive as the redeploy pause ends instead of starting the trip
then. Where no hold is declared this is refused outright, because ground taken
ahead of a front that can spring back is ground that has to be given back.

**Price the death by the walk home, not by the respawn clock.** A forward-rally
contract puts an automatic return on the own-side objective rather than the
slot's spawn anchor — which for this class is a cost, not a convenience: the
fabricator comes back near the fight and away from the region its fabrication
is bound to. That walk is computed from the map, and it is what the prime's
reluctance to stand on the objective is scaled by. Where arrivals land at the
spawn anchor the walk is zero and the term vanishes.

**A hold is time the front is being decided, not spare time.** The one use of
it this revision keeps is that a trip home to build a companion cannot lose
ground while my own advance is locked — and even that is taken only where a
companion is worth a walk.

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

## Contract-driven, not class-coded (revision 3 additions)

The capture threshold, the base gain, the decay amount and interval, the
redeploy pause, the declared hold length, whether surplus objective weight
scales capture pressure, whether only enemy sole presence erodes a claim, and
whether automatic arrivals rally forward are all read from
`StartLife.Contract` through the scaffold's own `Capture`, `ObjectivePresence`,
`ArrivalsRallyForward` and `ExpectedArrivalTiles` readers. An absent hold is a
real answer, not a gap: it means captures never lock, and every behaviour that
depends on stickiness switches itself off. The objective one step past the
active one comes from the ordered chain and this team's declared index delta,
so "the next ground" is never a map coordinate.

Whose mark a live hold protects is the one fact the sticky arm turns on that
the contract does not publish. A life that watched the active index move knows
it exactly, and dates the advance from the redeploy clock so the attribution
survives the one-tick observation lag. A life created inside the window cannot
derive it and does not guess: every hold-dependent behaviour is gated on
positive knowledge.

## Files

- `SparkLine.cs` — the policy and its priority order.
- `ContractLens.cs` — every static fact resolved once per life from the
  contract, including the per-tile exposure map, facing coupling, the capture
  policy values, and the walk from an expected arrival back to the fabrication
  source.
- `Tactics.cs` — reachability fields, the host's own shot-path rule replayed
  locally, declared-remaining projectile projection, and clear-ray geometry.
- `ArenaBasics.cs` — the current template helper, synced verbatim; the policy
  uses its `OrderedDirections` for every direction tie-break.
