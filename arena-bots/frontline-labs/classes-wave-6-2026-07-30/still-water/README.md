# Still Water — patient interceptor (striker)

Lineage `still-water-v1` · revision 6 · class `striker` · role `verdict-doctrine` ·
Frontline Labs classes arm, wave 6 (the deck game: keel + kit + universal bend +
`wane` + launch offsets + open placement, all facing-locked).

## The idea in one sentence

Do not walk into the duel. Stand one bend's reach behind the contested point,
put the gun across the approach, and make the other side spend tiles and tempo
coming to you — then take the ground last, but never later than the clock can
still pay for.

## What revision 6 is about: the doctrine was right and the traffic was not

Revision 6 changes no doctrine. Every positional argument revision 5 measured is
untouched: the five-family interception table, the aim-widened standoff band, the
cover-quality gradient, the shared-cone tax, the re-priced cast ledger. What it
adds is a coordination layer, because the doctrine was being executed by bodies
that got in each other's way.

Measured on the predecessor's own 50 matches, in the ticks where it had more than
one body on the board: **34% of the time a body's only legal step was onto a
sibling.** Under `facing-locked` the movement mask offers exactly one direction —
the tile you are facing — so a sibling standing there does not slow the body
down, it removes movement from the body's vocabulary until it spends a whole tick
turning somewhere it did not want to point. And the engine gives the tick back to
nobody: a same-destination move blocks both bodies, and *following a vacated
actor blocks*, so a body queued directly behind a sibling cannot even inherit the
tile the sibling has just left.

Two rules answer it, and both are conventions rather than negotiations.

### The convention: one order, computed by everybody

A life never sees an ally's current action — observations are frozen before any
same-tick decision runs — and a fresh body starts with empty private memory. So
there is no channel and nothing to remember. What there *is* is that every one of
my lives receives the **same** frozen team-perception union on the same tick, so
any function of that union alone evaluates identically in every sibling.

`Convoy` builds one list from **self plus allies**, orders it by
`(route cost to the contested point, unit slot, life id)`, and a body yields only
to siblings **above** it. The order is total, so the leader never yields and a
cycle of mutual yielding cannot form — which is the property a politeness rule
has to have before it is safe to add, because "everybody stops" is worse than the
collision it replaced.

### Rule 1 — the lane claim

No body stands on, or steps onto, a tile a better-ranked sibling's route needs
this tick or next. Under `facing-locked` that route is *exact* and not a guess:
the legality mask offers one direction, so the tile a sibling may reach is the
one ahead of its facing and the tile after it is the one ahead of that. Under a
coupling that lets a body step any cardinal there is no single committed tile, so
the claim falls back to the continuation of the step the sibling was last *seen*
to take, and to nothing at all when it has not moved. A claim this bot is not
sure of is not a claim.

Holding such a tile costs more than crossing it: crossing is one tick in the way,
parking is in the way until something else moves.

### Rule 2 — choke precedence

A **choke** is read off the map, not named: an open tile whose two neighbours
across some axis are both walls, so it admits one body and offers no lateral
dodge. This map has 24 of them among its 233 open tiles, and four of those —
`(8,7) (9,7)` and `(13,7) (14,7)` — are the two-tile corridors that are the only
row-7 approach to the centre objective from either side. One body standing on
either tile adds **three tiles** to a sibling's route from the rank-0 station,
and under `facing-locked` a detour that changes axis also costs a rotation, so
three tiles is up to five ticks of a fifteen-tick capture clock.

The rule has three clauses:

1. a body may **cross** a corridor no better-ranked sibling is using;
2. it may not **enter** a corridor run a better-ranked sibling occupies, or whose
   route enters it — the leader clears it first;
3. it may not **park** in a corridor at all while a sibling exists, and the
   station search refuses corridor tiles outright. A standoff doctrine holds its
   station for dozens of ticks, and a corridor held for dozens of ticks is a wall
   across my own team's only route to the point.

Together the two rules cut sibling-blocking by 42% and two-bodies-in-one-corridor
by 96%, and beat the rebuilt predecessor 23–20–7 at +3.5 mean territory over five
independent seed sets. `DX.md` has the per-rule split, and the three coordination
rules that were built, measured, and **rejected** for losing.

## Carried forward from revision 5: the cone got wider, so the table was rebuilt

The doctrine is unchanged. What changed is the geometry it is computed from.
`--aim offset` restores a ±1-sector initial launch offset on the mobile gun, so
one facing now owns **three launch lanes**, and each of them may still spend the
one bend. Written in the facing frame — forward `f`, rightward `g` — the
reachable set is the union of five families, and every one is a different shot:

| family | program | reaches | costs |
| --- | --- | --- | --- |
| **lane** | aim 0, no bend | `g = 0` | nothing; arrives soonest |
| **slip** | aim ±1, **no bend** | `|g| = f` | nothing — the aim-only diagonal is legal with zero bends |
| **fork** | aim 0, bend after `f - |g|` | `|g| < f`, `f - |g|` in the bend window | the bend; the deepest lie |
| **flatten** | aim ±1 outward, bend back inward after `|g|` | `|g| < f`, `|g|` in the window | the bend; reaches long shallow tiles the fork cannot |
| **hook** | aim ±1 outward, bend the same way after `f` | `|g| > f`, `f` in the window | the bend; a near-quarter turn onto ground more lateral than forward |

Two facts fall out of that table and both are doctrine.

- **The hole in the envelope is closed.** Revision 4's README said "there is
  exactly one hole, and it is the exact diagonal" — a one-bend program needs a
  dominant axis to bend away from, so `f == |g|` was unreachable at every range.
  The slip *is* that diagonal, with no bend spent. The volley's outer bolts are
  no longer the only way to reach it.
- **The standoff band moved one tile in.** The band is where the coverable
  lateral run is widest. Without offsets that is one tile past the latest legal
  bend, because the fork's bend budget is what runs out. With offsets the hook
  exists, and the hook is legal only while `f` itself is inside the bend window —
  so the widest band is now the *deepest tile still inside* the window, where all
  five families are available at once. One tile further out the hook is gone.
  Both readings come from `shotProgram`; neither is written down.

Counted on an open field, one facing covers **52 tiles** without launch offsets
and **124 with them**. Revision 4's interception table recognised the 52 — which
was exactly right for its arm and denied 58% of this arm's reach. Everything
that reads the table was therefore wrong in the same direction: where to stand,
which way to turn, whether a body already has a line, and how much of the board
one enemy facing threatens.

### Cover has a quality now, not just a truth value

When a facing owned one lane, "covered" was a decision: either the dominant axis
pointed at you or nothing did, and counting covered bodies told a rotation what
to do. Three lanes end that — almost every pose covers almost every body
somehow, so the count goes flat exactly when it matters. So cover is scored by
kind: a **lane** or a **slip** arrives on the shortest path this chassis owns and
commits nothing; a curve arrives later, spends the bend, and can be eaten by a
wall or a strict corner. Turning is now a choice between qualities of cover.

That single change was worth about +20 mean territory against the wave-4
predecessor, and flattening it back to a truth value was revision 5's largest
single regression. Revision 6 did not re-measure it; it is carried forward
byte-identical (`ForkPlanner.cs` and `Doctrine.cs` are unchanged at the r5
hashes).

### One arithmetic fact worth knowing

Every family's path length is the **Chebyshev distance** to the target — the
lane, slip, fork and flatten all spend `f` tiles, and the hook spends `|g|`. So
the arrival tick of any covering trajectory is `ImpactOffset(chebyshev)`, with no
per-family correction. A patient doctrine needs arrival times to price a
prediction, and this is why it can have them cheaply.

## Carried forward from revision 5: the open ground made the volley a point verb

Revision 4's single loudest complaint was that the map decided where a stance
could exist: the volley entry route carried `forbiddenTileTags:
["transition-placement-forbidden"]`, that tag covers 112 of this map's 233 open
tiles including *every* objective tile and the whole central lane, and the
measured window where the route was legal *and* an enemy was visible was 5.9% of
ticks. It cast **zero** times in its own 120 measured matches.

`--stance-ground open` empties that list. The tag is still on all 112 tiles; no
route refuses any of them. So revision 5 asks the **route** and not the map:

```
placement.ForbiddenTileTags ∩ map tags → refused
placement.RequiredTileTags  ⊄ map tags → refused
```

A bot that reads the map tag instead refuses ground the rules have just handed
it and never notices, because nothing failed. This is the revision's most
important mechanical repair, and it is what turns the volley from a shoulder verb
into a point verb: the stance keeps **objective weight 1**, so a body that raises
it on the point is still holding the point — it stops walking, not scoring.

So the cast ledger was re-priced, and the re-pricing cuts both ways:

- **One body is no longer worth a stance in the open.** The fan's old selling
  point was that its outer bolts reached lanes a single bolt could not. With
  launch offsets that is simply false — every ray of the fan is a lane the
  ordinary gun can aim down for one tick of nothing. The fan's remaining edge is
  *simultaneity*: it denies its rays at the same instant, against a body that
  would otherwise re-decide between two single bolts. The two-body rule therefore
  stays strict, and the margin the displaced bolt is owed scales with how many
  lanes it could have chosen from — 15% per lane, read off the gun's own declared
  aim range, so a straight-aim arm reverts to revision 4's number by
  construction.
- **A new and better case appears: casting the ground you are standing on.**
  Holding the point, the capture clock keeps running through the windup and the
  fan seals the tiles an attacker has to stand on to take it. Revision 4 could
  not express this case at all.

And the entry is gated on a **steady** body — one this life has watched hold a
tile, or one whose form cannot move at all. A fan aimed at a body still choosing
its tile is a bet placed two ticks before the dice land, and half of those
entries reached the stance with nothing left to fire on. Requiring evidence
rather than a prediction is what turned a 25–23–2 record against the predecessor
into 30–9–11.

Measured: with the open placement but revision 4's ledger, casting is **actively
harmful** (−15 mean territory against keeping the on-point rule). With the
re-priced ledger it casts freely and on the point: revision 6, on its own fifty
matches and its own seeds, enters the stance **149** times and **106 of those
(71%) on an objective tile** — ground the wave-4 rule refused outright. The
wave-5 `DX.md` reports the ledger's whole split verdict, including the
configuration that never casts at all.

## What revision 5 stopped guessing (carried forward unchanged)

- **A diagonal bolt is no longer proof that the bend is spent.** Revision 4
  reasoned that a diagonal in flight had either already turned or come from a
  turret, and projected no further curve off it. Launch offsets break that
  outright: a bolt may *launch* diagonally with its bend untouched and hook a
  quarter turn later. Which history produced the bolt in front of you is not
  observable, so where the owner's declared envelope permits both an offset
  launch and a bend, both futures are projected.
- **"Already pointed at this tile" is a question about the whole widened cone.**
  The threat map's aimed tier and the shared-cone tax both used a
  dominant-axis alignment test. They now use the same five-family table the gun
  uses, which is the only way the two agree about the same geometry.
- **A kill is worth the time the slot stays empty.** Revision 4 valued anything
  one hit from death at a flat 2.0. A body's own stable slot declares the delay
  before it can return and whether returning costs its owner a combat action as
  well as the clock; the cheapest return on the contract is the unit those are
  measured in. A patient doctrine converts damage into time, so that is its
  exchange rate — and an arm whose ordinary children rebuild on a slower clock
  than they used to is an arm where killing children is worth more, which this
  bot works out from the lifecycle rather than being told.
- **A fortified form that can mobilize again is still a gun that follows you.**
  The standoff band excludes guns that cannot chase. Wave 5 makes the turret a
  true cycle — `irreversibleForLife` is false on the mobilize routes, health maps
  proportionally floored in both directions, and there is no entry heal — so a
  body that anchors is no longer spent for the life, and its gun belongs in the
  reach you have to stand outside of. Where that reach exceeds your own, the
  out-range bargain is simply not on offer and the doctrine falls back to the
  fork band. Which kind of fortification it is comes from the exit route's
  `irreversibleForLife`, read rather than assumed: the same class was once the
  first kind and is now the second, and nothing in the form or the gun changed
  when it moved.

## Movement coupling: three worlds, one doctrine

| Coupling | What a step costs | What Still Water does |
| --- | --- | --- |
| `preserve-facing` | nothing; the gun stays put | unchanged doctrine: concede tiles freely to restore the band |
| `move-sets-facing` | the step *is* the turn | candidate tiles are scored with the facing they leave you in, so a retreat is priced at the coverage it throws away |
| `facing-locked` | you may only walk where you point | rotation is the steering wheel as well as the gun; a refused step is bought with an explicitly priced rotation, and the withdrawal clause disappears |

Every wave-5 and wave-6 cell is `facing-locked`. Launch offsets make that cheaper than it
was: a facing that owns three lanes needs fewer rotations to keep a gun on a
moving body, which is the one way this arm makes the locked chassis easier to
drive rather than harder.

## How a tick is spent

1. A bolt that will cross this tile during the coming resolution outranks
   everything — and the step that answers it must still be alive three ticks
   later. In a choke, every tile is the line; giving the ground back to a bolt
   that will simply arrive again is a loop, not an evasion.
2. Companions, whichever way the contract hands them over.
3. The cast ledger, when a fan answers two bodies, seals a contested point, or
   guards the point this body is already standing on.
4. The gun, if the trajectory arrives on a tile some prediction actually names.
   A curve that a wall or a strict corner will eat is a wasted tempo beat; so is
   a bolt fed to a shell's arc that is not the one that breaks it.
5. Otherwise the feet: hold the band, turn for a better *quality* of cover, or
   walk — and never into a cone an ally is already standing in, never onto a tile
   a better-ranked sibling's route needs this tick or next, and never parked in a
   one-tile corridor while a sibling is on the board.

## Postures

| Posture | When | What it does |
| --- | --- | --- |
| **Deny** | contact, and a body is on or beside the point | hold the station, cover the approach, shoot |
| **Seize** | no contact, nobody contesting, we already claim, they are worn down, or the clock has run out of patience | take the ground |
| **Contest** | they hold a claim worth breaking — any claim at all once the ledger closes | walk in and break it |
| **Reposition** | loaded gun, visible body, no legal line from here | take the angle — but never off ground already held |
| **Withdraw** | one hit from death, gun cold, inside their reach, the coupling still makes a retreat worth its turn, and returns do *not* rally to the front | give exactly two tiles back |
| **Cast** | a fan answers two bodies, seals a contested point, or guards the point this body already holds | raise the stance, aim, fire once |

## Contract discipline

Nothing above is written into the source as a number. Forms, ranges, cooldowns,
the bend window, **the initial-aim range**, launch and travel cadence, capture
threshold, decay amount and interval, redeploy pause, the ratchet hold length,
the control policy, the decay clock, the automatic-return placement, the front
axis, objective regions, unit slots with their unlock clocks **and their rebuild
delays and destruction policies**, the movement profile's facing coupling, the
stance windups and budgets, the volley's bolt count and spread, the projectile
guard, **each route's own placement tag lists**, and both chassis's stats all
come from `StartLife.Contract`; action codes and argument domains come from that
tick's legality mask. Class identity comes from `Topology.Teams[].ClassId` and
`ClassId` on self, allies and enemies — never from a form-ID prefix. 60/180/300,
rebuild 22, rebuild 30, 120/260 and 18 appear nowhere in the source.

Equal-scoring directions are broken by `ArenaBasics.OrderedDirections`: our own
advance first, retreat last, and the two perpendiculars ordered by this life's
deterministic random stream. An absolute compass preference is a measured
team-side bias on a mirror-symmetric map, because both teams share it.

## Files

| File | What it holds |
| --- | --- |
| `Convoy.cs` | the coordination layer: the right-of-way convention every sibling recomputes, the lane claim, choke detection and precedence, the deflection-return spacing test, and the four rules that were measured and rejected — still present as switches so the rejections rebuild |
| `StillWater.cs` | the tick ladder, every posture decision, the re-priced cast ledger, the stance ladder, cover quality, and the guard-aware shot planner |
| `ForkPlanner.cs` | trajectory algebra: the five-family interception table with launch offsets, enumeration, impact timing, realised-bend filtering, and wall/corner-exact verification walked without allocating |
| `Stance.cs` | reversible stances read from the contract — windups, budgets, bolt counts, fan geometry, and what a cast is worth |
| `Ratchet.cs` | the published hold, the revision-3 reconstruction kept as a fallback, and what a push is worth given whose hold is running |
| `Doctrine.cs` | one-time contract reading: front axis, capture and ledger maths, the pendulum policies, both chassis profiles, class identity, slot counts **and slot economics**, **per-kind tile tags**, the aim-widened standoff band, the movement coupling |
| `Quarry.cs` | life-scoped enemy tracking (observed fire for cooldown, observed deflections for shell budgets) and the per-tick forecast, including what finishing each body is worth in time |
| `ThreatField.cs` | incoming bolts projected in time from their own published cadence and damage, the widened muzzle cone, fan and guard cones, the coupling-aware escape horizon, and the shared-cone test |
| `Field.cs` | walkability, cost fields, ray clearance |
| `ActionBook.cs` | this tick's legality mask indexed by the contract's own action kinds, and by stable ID where a kind is ambiguous |
| `ArenaBasics.cs` | vendored verbatim from the current starter template; `OrderedDirections`, `AdvanceDirection`, `Capture`, `LiveHold`, `ObjectivePresence` and `ArrivalsRallyForward` are live |
