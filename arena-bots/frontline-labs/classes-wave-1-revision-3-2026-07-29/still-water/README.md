# Still Water — patient interceptor (striker)

Lineage `still-water-v1` · revision 3 · class `striker` · role `verdict-doctrine` ·
Frontline Labs classes arm, wave 1.

## The idea in one sentence

Do not walk into the duel. Stand one bend's reach behind the contested point,
put the gun across the approach, and make the other side spend tiles and tempo
coming to you — then take the ground last, but never later than the clock can
still pay for.

## Why that is the right shape for this chassis

The striker's exclusive verb is a private 45-degree bend committed at the
moment of firing. Read straight off the resolved attack profile, that envelope
is: aim along facing, travel *k* tiles with `k` inside the declared bend
window, then turn once and run out the rest of the declared range. Written as
map geometry, a target at longer-axis distance `a` and shorter-axis distance
`b` is coverable exactly when `a` is inside range and `a - b` lands inside the
bend window. Two consequences fall out, and both are doctrine:

- **The fan is widest in the middle of the range.** Very close and very far,
  the coverable lateral band collapses to a couple of offsets; around one tile
  past the latest legal bend point it is at its widest. That distance is Still
  Water's *standoff band* — the still water it holds.
- **A bend is a lie that stays true until it turns.** The heading a defender
  reads off a bolt in flight is evidence, not the path. A body that dodges the
  visible line at the right range steps *into* the arc, not out of it.

So the whole positional argument reduces to one number: distance to the nearest
enemy, measured against the band. Closing is penalised harder than yielding,
which is what makes conceding ground the cheap option — a tile given up to
restore the band is a good trade, every time.

The band is not a constant. It is resolved from both chassis at match start: if
the opposing chassis can be out-ranged with a tile to spare, the band sits one
past *their* reach; otherwise it sits where our own fork is widest. Against a
short-gunned, short-sighted opponent that means standing outside its world
entirely. Against a mirror it means the fork band.

## What revision 3 changed: patience has a price, and the contract sets it

Everything above assumes ground given up comes back. On a mean-reverting
frontline it does, and standing off is close to free. Three declared policies
can each withdraw that assumption, and all three are readable from
`gameMode.capture` and `lifecycle` at match start.

### A hold makes the same push worth a position or worth nothing

When `capture.ratchetHoldTicks` is declared, a completed advance is locked:
a capture finished inside the holder's window is **spent** — the claim resets
exactly as a successful capture does, and the objective does not move. So the
identical fifteen ticks of presence buy a position or buy nothing, depending
on a clock that is *not* an observation field.

Still Water reconstructs that clock four ways, so a life born at any point in
the match has an answer: the position change it watched happen (exact, and the
sign against this team's declared index delta names the owner);
`controlResumesAtTick` minus the declared redeploy pause (dates the advance to
the tick for a life that opens its eyes inside the pause); a near-threshold
claim that resets while the index stays put (only a spent capture looks like
that, which proves the *other* side owns a live hold); and the front's
displacement from the centre of the chain (which names the side that last
advanced whenever the front is not level). Every branch defaults to *unknown*,
and unknown plays exactly the baseline.

Two rules follow, and the second is the one that is easy to get backwards:

- Inside a hold **we** own, an opposing capture that completes inside the
  window is spent — letting them buy it is better than contesting it, because
  they pay fifteen ticks of presence for a reset. The protected window is the
  cheapest attacking tempo the contract offers, so it is spent on the next
  position rather than on ground that is already guaranteed.
- But a hold protects the **advance**, not the scoreboard, and it does not
  delete progress. A claim still standing when the window drops converts the
  instant it does. So the claim is ignored only while it would *complete*
  inside the hold; the moment it would survive the boundary it is contested
  like any other. Treating a hold as blanket immunity turns a protected window
  into a handover.

### Where you respawn changes what a body is worth — but not what a death costs

`lifecycle.automaticReturnPlacement` can put returns and activations on your
own side of the *active objective* instead of at your spawn. That makes a body
cheap to replace positionally, and the obvious conclusion — spend bodies more
freely — is wrong. Capture wants **uninterrupted sole presence**, and a death
interrupts it wherever the replacement appears; the return clock is unchanged.
Measured against this lineage's own predecessor, discounting danger on a
rallying contract cost 193 ticks to breach as the attacker. So the discount is
not taken. What *is* taken: the withdrawal posture switches off, because
conceding ground to save a body that reappears in position pays a real price
for a saving that is no longer real.

### Whether a second body on the point is pressure or decoration

`capture.controlPolicy` decides whether surplus objective weight scales
capture pressure. Where it does, one body no longer nulls two, denial becomes
quantitative, and every additional weight on the point subtracts from their
rate — so holding ground pays for the exposure, and Still Water stands on the
point even while its own capture is spent. Where control is binary, the first
body already nulls them and the second has merely stopped covering the
approach, so the doctrine keeps the standoff it was tuned for. The same
artifact plays both; the branch is a policy string, not a flag.

## The three things revision 2 changed

Same doctrine, three corrections — two fundamentals the first freeze got wrong
and one clause the doctrine had simply never written down.

### 1. A tile you cannot leave is not still water, it is a coffin

Evasion used to ask one question: *does a bolt cross this tile during the
coming resolution?* In an open field that is enough. In a corridor it is a
trap, because the answer is "no" right up to the tick where every remaining
answer is "yes". Bolts are now projected in **time** — each tile stamped with
the tick offset the bolt reaches it, and each tile the bolt comes to rest on
stamped with the offset after, because walking onto a resting bolt hurts
exactly as much as standing in its path — and a candidate tile is scored by
whether *some* legal sequence of decisions is still alive three ticks later.

This does not soften the doctrine's commitment rule. Crossing a choke on
purpose rather than handing the tile back to a bolt that will simply arrive
again is still correct, and still outranks distance-to-goal. What the horizon
adds is that the crossing has to be survivable: a step into a dead end is not a
commitment, it is a concession with extra steps.

### 2. A bend is only a lie worth telling if the target is past the turn

The trajectory planner enumerated every legal program and gave curved ones a
tie-break bonus over straight ones, on the honest grounds that a curve hides
its intent. But a bend that a wall — or a strict diagonal corner — eats before
the turn sweeps *exactly* the tiles the straight shot sweeps. It buys nothing,
and the tie-break was firing it anyway. Trajectories whose turn never happens
inside the tiles they truly reach are no longer enumerated at all, and the
bonus applies only when the tile actually aimed at lies past the bend point.
A striker that spends its private commitment on a corner has told the lie and
paid for it without anyone being deceived.

### 3. The ledger: territory at the cap is decided one point at a time

At the tick cap the ranking is signed displacement plus the residual claim on
the live point, and the only thing that removes an opposing claim is a body
standing there. The old contest rule ignored any claim below a fifth of the
threshold — cheap patience, since a small claim is usually reversible. Both of
this lineage's recorded defeats landed inside exactly that dead band, decided
by one and two points of residual progress in matches that were otherwise dead
level.

So the dead band now closes on a clock read from the contract. Still Water
computes how many ticks of presence it would take to neutralise the current
opposing claim — the slower of the declared decay clock and the sole-presence
erosion rate — adds the walk to the point, and contests **any** adverse point
once the remaining ticks no longer cover that bill. Patience is a bet that
there is still time to buy the ground back later; when the clock can no longer
pay, the bet has already lost.

## Movement coupling: three worlds, one doctrine

An experiment arm may couple facing to movement, and the resolved movement
profile says which world this body lives in. Backpedal-kiting — retreating
while the gun stays on the approach — is free only under `preserve-facing`.
Every clause that trades ground for a line now reads the coupling instead of
assuming the historical strafe:

| Coupling | What a step costs | What Still Water does |
| --- | --- | --- |
| `preserve-facing` | nothing; the gun stays put | unchanged doctrine: concede tiles freely to restore the band |
| `move-sets-facing` | the step *is* the turn | candidate tiles are scored with the facing they leave you in, so a retreat is priced at the coverage it throws away; withdrawal narrows to a genuine breach of the band |
| `facing-locked` | you may only walk where you point | rotation becomes the steering wheel as well as the gun — candidate steps come from map geometry, and a step the mask refuses is bought with an explicitly priced rotation; the withdrawal clause disappears, because turning away and then walking costs two ticks a hurt body does not have |

Revision 3 rewrote how that steering is chosen, and the arithmetic matters.
Candidate steps are enumerated from map geometry, never seeded from the
movement mask: under `facing-locked` the mask offers exactly one direction, so
a search seeded from it can only continue in a straight line — the chassis
stops turning rather than turning slowly. A refused step becomes a rotation
scored where the body actually stands, wearing the facing it will have, plus
the route the lane unlocks. Charging that rotation a whole tick of route
instead cancels the gain against itself exactly, and the body stands still
forever; that mistake cost 24 straight losses before it was found.

The three-tick escape horizon models the same distinction: under
`facing-locked` its search must spend a whole tick to change direction, so a
corridor is genuinely more lethal there and the scoring says so.

## How a tick is spent

1. A bolt that will cross this tile during the coming resolution outranks
   everything — and the step that answers it must still be alive three ticks
   later. In a choke, every tile is the line; giving the ground back to a bolt
   that will simply arrive again is a loop, not an evasion, so Still Water
   takes the trade and crosses — provided the crossing survives.
2. Companions, whichever way the contract hands them over. Some resolved
   contracts activate child slots on a clock; some require the Prime to ask.
   Both routes come from the legality mask; a body is worth more than a bolt.
3. The gun, if the trajectory arrives on a tile some prediction actually names:
   where the body is (weighted up the longer it has stood still), where its
   last observed heading takes it, or where the objective is pulling it. A
   curve that a wall or a strict corner will eat is a wasted tempo beat, and
   tempo is the whole point of standing off.
4. Otherwise the feet: hold the band, open the fan by rotating, or walk.

## Postures

| Posture | When | What it does |
| --- | --- | --- |
| **Deny** | contact, and a body is on or beside the point | hold the station, cover the approach, shoot |
| **Seize** | no contact, nobody contesting, we already claim, they are worn down, or the clock has run out of patience | take the ground |
| **Contest** | they hold a claim worth breaking — any claim at all once the ledger closes | walk in and break it |
| **Reposition** | loaded gun, visible body, no legal line from here | take the angle — but never off ground already held |
| **Withdraw** | one hit from death, gun cold, inside their reach, the coupling still makes a retreat worth its turn, and returns do *not* rally to the front | give exactly two tiles back |

The station is one standoff behind the near edge of the contested point, offset
laterally by this slot's rank in its team's stable ordering, so allied bodies
hold different fans instead of stacking one line. Two fans that cross mean a
dodge out of one is a step into the other. When the front sits against our own
base there is no ground left behind it, and the search takes a flanking tile
beside the point rather than walking into the back wall.

Late is not never. The endgame commit reads the capture arithmetic and the
territorial channel out of the contract: when the ticks left no longer cover a
capture — doubled while not ahead — patience stops being a strategy, and Still
Water goes and stands on the point.

## Contract discipline

Nothing above is written into the source as a number. Forms, ranges, cooldowns,
the bend window, launch and travel cadence, capture threshold, decay amount and
interval, redeploy pause, the ratchet hold length, the control policy, the
decay clock, the automatic-return placement, the front axis, objective regions,
unit slots and their unlock clocks, the movement profile's facing coupling, and
the opposing chassis's stats all come from `StartLife.Contract`; action codes
and argument domains come from that tick's legality mask. Every one of the
pendulum policies is read through the starter template's own readers
(`ArenaBasics.Capture`, `ArrivalsRallyForward`, `ObjectivePresence`), so an
absent field means the declared inert default rather than a gap. The opposing form catalogue is
derived by subtracting our own slots' reachable forms — closed over declared
transition routes — from the catalogue, so a mirror correctly concludes the
opponent is exactly as dangerous as we are.

Equal-scoring directions are broken by `ArenaBasics.OrderedDirections`: our own
advance first, retreat last, and the two perpendiculars ordered by this life's
deterministic random stream. An absolute compass preference is a measured
team-side bias on a mirror-symmetric map, because both teams share it. The
enemy-approach forecast uses the same trick with the axis reversed.

## Files

| File | What it holds |
| --- | --- |
| `StillWater.cs` | the tick ladder and every posture decision |
| `Ratchet.cs` | the hold clock, reconstructed from position changes, the live redeploy pause, spent captures, and front displacement — plus what a push is worth given whose hold is running |
| `Doctrine.cs` | one-time contract reading: front axis, capture and ledger maths, the hold/control/placement policies, both chassis profiles, the standoff band, the movement coupling |
| `ForkPlanner.cs` | trajectory algebra — enumeration, impact timing, realised-bend filtering, and wall/corner-exact coverage |
| `Quarry.cs` | life-scoped enemy tracking and the per-tick forecast that turns a bend into a prediction |
| `ThreatField.cs` | incoming bolts across every continuation still legal, projected in time, plus the coupling-aware escape horizon and muzzle coverage |
| `Field.cs` | walkability, cost fields, ray clearance |
| `ActionBook.cs` | this tick's legality mask indexed by the contract's own action kinds |
| `ArenaBasics.cs` | vendored verbatim from the current starter template; `OrderedDirections`/`AdvanceDirection` are live, and the tactical helpers are superseded by the files above |
