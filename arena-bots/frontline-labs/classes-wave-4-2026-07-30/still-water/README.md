# Still Water — patient interceptor (striker)

Lineage `still-water-v1` · revision 4 · class `striker` · role `verdict-doctrine` ·
Frontline Labs classes arm, wave 4 (phase-2 cells: keel × kit × bend).

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
bend window. Three consequences fall out, and all three are doctrine:

- **The fan is widest in the middle of the range.** Very close and very far,
  the coverable lateral band collapses to a couple of offsets; around one tile
  past the latest legal bend point it is at its widest. That distance is Still
  Water's *standoff band* — the still water it holds.
- **A bend is a lie that stays true until it turns.** The heading a defender
  reads off a bolt in flight is evidence, not the path. A body that dodges the
  visible line at the right range steps *into* the arc, not out of it.
- **There is exactly one hole in the envelope, and it is the exact diagonal.**
  A one-bend program needs a dominant axis to bend away from, so a target at
  `a == b` is unreachable at every range. Revision 4 is the first to care,
  because the skill kit hands the striker a verb whose outer bolts fly down
  precisely those diagonals.

So the whole positional argument reduces to one number: distance to the nearest
enemy, measured against the band. Closing is penalised harder than yielding,
which is what makes conceding ground the cheap option — a tile given up to
restore the band is a good trade, every time.

## What revision 4 changed: the kit, priced

Revision 3 priced the keel counterweights. Revision 4 prices the **verbs** —
and the honest headline is that the smaller half of the kit paid and the
headline verb did not.

### The volley is a coverage instrument, not a bigger gun

Everything about the cast is read from the contract: a same-life route whose
target form's attack profile declares `volley`, its windup and the wait-only
policy of that windup, the tile tags that refuse the route, the stance's own
cooldown, and the `automaticReturn` budget on the return route that fires the
route for you. Three of those facts decide the whole doctrine.

- **The bolts are simultaneous and straight.** A fan cannot reach anything a
  bend could have curved onto, and it never concentrates damage: each bolt
  spends its one damage on its own tile. Its entire edge is *breadth at a single
  arrival tick* — which buys two things a cheaper single bolt cannot. Two
  separate bodies answered by one decision. Or the front rank of a contested
  point denied a tile at a time. Without one of those, the cast is a worse bolt.
- **The entry is blind.** The windup is `wait-only` and lethal damage cancels
  it, so a cast is a commitment made without a dodge. Still Water refuses it at
  one health, refuses it where anything already in flight reaches the tile, and
  refuses it under a gun that is already *pointed* at the tile. Refusing every
  tile a gun could turn to would refuse every tile within eight of a body,
  which is the whole board.
- **The stance is refused on transition-forbidden tiles**, and on this map that
  tag covers every objective tile and the entire central lane. A cast is
  therefore never fired from the point: it is fired from the shoulder, across
  it. Measured over twenty matches, `transform` is legal on 18% of this bot's
  ticks, an enemy is visible on 42%, and both hold together on **5.9%** —
  almost all of them on the two shoulder tiles north of the centre objective.

Inside the stance there are no feet. The ladder aims by rotating, fires when
the fan arrives on a named prediction, and leaves early — an ordinary
`mobilize` below the budget — when a bolt is coming that an immobile body
cannot dodge, or when there is nothing left to fire on. Sitting in a one-shot
stance is pure loss.

### Coming from two bearings is the part that paid

The addendum's other sentence about the kit — *envelopment from multiple
bearings beats both the shell's arc and volley lanes; clumping in a lane feeds
the fan* — turned out to be worth more than the verb. A fan sprays one facing's
neighbourhood, a guard protects one quadrant, and an aimed gun covers one lane;
none of them tracks. Two allied bodies inside the same cone are two bodies one
enemy decision answers.

So a candidate tile is charged for every ally that shares an enemy cone with
it. Against the rebuilt predecessor this single term is the difference between
a dead-even mirror and **60 wins from 60** at about +20 mean territory. It is
also the most delicately tuned number in the bot, and `DX.md` says so with the
measurements that show why.

### A bend is not a tie-break against a shell

Where a form declares a projectile guard, a bolt that arrives inside its facing
quadrant dies on the arc and a new one — the bulwark's, along the exactly
reversed heading, from the shell's own tile — comes back. The guard reads the
**arrival** heading, not the shooter's tile, and a bend changes the arrival
heading. That is the one place in this bot where a curve is not a tie-break but
the whole shot. The alternative is deliberate: count the deflections the shell
has already made, and when the next one spends its declared budget, feed it,
because the break is a forced return with an exit and a fresh entry windup to
punish. Both branches are inert on a contract with no guarded form.

### Five slots is an expectation, not a symmetry

Slot counts are counted, never assumed — for both sides. An arm that gives the
opposition more bodies is a reason to expect pressure from more bearings at
once, which is exactly what the cone rule above already charges for. It is not
a reason to match the body count.

## What revision 4 stopped guessing

`holdOwnerTeamId` and `holdEndsAtTick` are now published on the Frontline mode
observation, travelling together, in the same grammar as `controlResumesAtTick`.
Revision 3 reconstructed both from four independent signals and could not
soundly recover the *owner* at all — the only derivation was the sign of front
displacement, which is wrong the first time an opponent regresses from a lead
and is unavailable to a life born inside the hold, because private memory is
life-scoped. `Ratchet.cs` now asks. The reconstruction survives as a fallback
for a half-populated pair only, and a published null pair is treated as the
real answer it is: no hold binds this tick.

Three more facts moved from inference to reading:

- **Per-projectile timing and damage.** `ticksPerAdvance` and `damagePerHit`
  are on every visible bolt, so the threat map no longer borrows the cadence of
  a guessed attack profile. It has to be per bolt: a volley bolt, an ordinary
  bolt, and a bolt a shell has turned around need not agree on either. A
  deflected bolt is hostile because its owning *team* is not ours, which is why
  a bolt of ours that comes back off an arc was already handled correctly.
- **Mobility is an action mask, not an objective weight.** Revision 3 asked
  whether a form carried objective weight to decide whether a body could walk.
  A stance carries weight 1 and cannot move, so that proxy predicted a parked
  stance as if it were about to stroll away. A body that cannot move is an exact
  aim point, and a body inside a wait-only windup cannot move *or* shoot and
  dies to lethal damage before its form arrives — the cheapest target the
  ruleset offers.
- **Both sides' form catalogues are declared.** Revision 3 derived the
  opposition's forms by subtracting its own from the catalogue, which on a
  mirror left nothing and fell back to "assume everything". Each team's
  lifecycle assignments are in the contract; reading them is exact.

## Movement coupling: three worlds, one doctrine

An experiment arm may couple facing to movement, and the resolved movement
profile says which world this body lives in. Backpedal-kiting — retreating
while the gun stays on the approach — is free only under `preserve-facing`.

| Coupling | What a step costs | What Still Water does |
| --- | --- | --- |
| `preserve-facing` | nothing; the gun stays put | unchanged doctrine: concede tiles freely to restore the band |
| `move-sets-facing` | the step *is* the turn | candidate tiles are scored with the facing they leave you in, so a retreat is priced at the coverage it throws away |
| `facing-locked` | you may only walk where you point | rotation is the steering wheel as well as the gun; a refused step is bought with an explicitly priced rotation, and the withdrawal clause disappears |

Every phase-2 cell is `facing-locked`.

## How a tick is spent

1. A bolt that will cross this tile during the coming resolution outranks
   everything — and the step that answers it must still be alive three ticks
   later. In a choke, every tile is the line; giving the ground back to a bolt
   that will simply arrive again is a loop, not an evasion.
2. Companions, whichever way the contract hands them over. Some resolved
   contracts activate child slots on a clock; some require the Prime to ask.
   Both routes come from the legality mask.
3. The cast ledger, when a fan exists, the feet have nowhere better to be, and
   the fan answers two bodies or seals a contested point.
4. The gun, if the trajectory arrives on a tile some prediction actually names.
   A curve that a wall or a strict corner will eat is a wasted tempo beat; so
   is a bolt fed to a shell's arc that is not the one that breaks it.
5. Otherwise the feet: hold the band, open the fan by rotating, or walk — and
   never into a cone an ally is already standing in.

## Postures

| Posture | When | What it does |
| --- | --- | --- |
| **Deny** | contact, and a body is on or beside the point | hold the station, cover the approach, shoot |
| **Seize** | no contact, nobody contesting, we already claim, they are worn down, or the clock has run out of patience | take the ground |
| **Contest** | they hold a claim worth breaking — any claim at all once the ledger closes | walk in and break it |
| **Reposition** | loaded gun, visible body, no legal line from here | take the angle — but never off ground already held |
| **Withdraw** | one hit from death, gun cold, inside their reach, the coupling still makes a retreat worth its turn, and returns do *not* rally to the front | give exactly two tiles back |
| **Cast** | a fan answers two bodies or seals a contested point, and the windup is affordable | raise the stance, aim, fire once |

The station is one standoff behind the near edge of the contested point, offset
laterally by this slot's rank in its team's stable ordering, and — where a fan
exists — preferring a tile the stance can actually be raised on, because on this
map the natural station is in the central lane and the central lane refuses
every transition.

## Contract discipline

Nothing above is written into the source as a number. Forms, ranges, cooldowns,
the bend window, launch and travel cadence, capture threshold, decay amount and
interval, redeploy pause, the ratchet hold length, the control policy, the decay
clock, the automatic-return placement, the front axis, objective regions, unit
slots and their unlock clocks, the movement profile's facing coupling, the
stance windups and budgets, the volley's bolt count and spread, the projectile
guard, the transition-forbidden tile tags, and both chassis's stats all come
from `StartLife.Contract`; action codes and argument domains come from that
tick's legality mask. Class identity comes from `Topology.Teams[].ClassId` and
`ClassId` on self, allies and enemies — never from a form-ID prefix.

Equal-scoring directions are broken by `ArenaBasics.OrderedDirections`: our own
advance first, retreat last, and the two perpendiculars ordered by this life's
deterministic random stream. An absolute compass preference is a measured
team-side bias on a mirror-symmetric map, because both teams share it.

## Files

| File | What it holds |
| --- | --- |
| `StillWater.cs` | the tick ladder, every posture decision, the cast ledger, the stance ladder, and the guard-aware shot planner |
| `Stance.cs` | reversible stances read from the contract — windups, budgets, bolt counts, fan geometry, and what a cast is worth |
| `Ratchet.cs` | the published hold, plus the revision-3 reconstruction kept as a fallback, and what a push is worth given whose hold is running |
| `Doctrine.cs` | one-time contract reading: front axis, capture and ledger maths, the pendulum policies, both chassis profiles, class identity, slot counts, tile tags, the standoff band, the movement coupling |
| `ForkPlanner.cs` | trajectory algebra — enumeration, impact timing, realised-bend filtering, and wall/corner-exact coverage |
| `Quarry.cs` | life-scoped enemy tracking (including observed fire, for cooldown, and observed deflections, for shell budgets) and the per-tick forecast |
| `ThreatField.cs` | incoming bolts projected in time from their own published cadence and damage, fan and guard cones, the coupling-aware escape horizon, and the shared-cone test |
| `Field.cs` | walkability, cost fields, ray clearance |
| `ActionBook.cs` | this tick's legality mask indexed by the contract's own action kinds, and by stable ID where a kind is ambiguous |
| `ArenaBasics.cs` | vendored verbatim from the current starter template; `OrderedDirections`, `AdvanceDirection`, `Capture`, `LiveHold`, `ObjectivePresence` and `ArrivalsRallyForward` are live |
