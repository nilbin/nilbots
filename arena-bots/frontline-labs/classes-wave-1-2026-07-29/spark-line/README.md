# SparkLine — TEMPO ENGINE

Class: **fabricator**. Lineage `spark-line-v1`. Role: verdict-doctrine.

## The idea in one sentence

Win the objective clock with **bodies per tick**: queue every companion the
instant the contract makes it legal, put those children in the field beside the
prime rather than behind it, and keep the fragile prime alive only to the
extent that keeps it spending fabricate actions.

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
nothing is safer than standing still, it stands still and answers.

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

## Files

- `SparkLine.cs` — the policy and its priority order.
- `ContractLens.cs` — every static fact resolved once per life from the contract.
- `Tactics.cs` — reachability fields, the host's own shot-path rule replayed
  locally, and projectile impact projection.
