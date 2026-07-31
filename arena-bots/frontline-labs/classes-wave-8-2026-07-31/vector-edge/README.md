# VectorEdge — pressure duelist (striker)

**Class:** striker · **Lineage:** vector-edge-v1 · **Revision:** 8 ·
**Role:** verdict-doctrine

> Ground is the only score. Every tick either takes a tile, holds a tile, or
> removes the body standing on it.

## Revision 8: the channel is a pose, and the economy is a by-product

Two arms landed at once, and they are not the same kind of change. `--capture
channel` rewrites what taking ground IS, for both teams, whatever classes are
in the cell — every capture routine this lineage has ever written was priced
against a rule that no longer holds. `--economy scrap` adds a second currency
beside the front. Revision 8 reads both off the contract and answers each with
one sentence.

**The channel: stillness is positional, so the gun and the ground stopped
competing.** Claim weight counts only bodies whose TILE did not change this
tick; denial counts everyone. Rotating, shooting and entering a stance are
explicitly not tile changes. So a body holding the point banks its gain
whatever it does with its action — the tick is free for the gun — while the
shuffle between two tiles of the same objective, which was free under every
previous contract, now costs the whole tick's gain.

**The economy: the wreckage is free and the deposit run is not.** Every
destroyed body drops a pile where it died, stepping on one banks the assay
instantly with no transport, and a duelist that lives at the objective is
standing in the economy already. The deposit sites are sixteen facing-locked
ticks from home in each direction, and under a channel two defenders who keep
moving hold three stationary attackers — so a harvester is a body the front
notices. This doctrine takes the by-product and declines the errand, and says
so with a number rather than a preference.

### What the contract actually says, and what this revision reads

Every rule below turns on a field that is **absent** on a ruleset without the
arm — `stationaryGainMultiplierCap`, `opposingErosionMultiplier`,
`claimInterrupt`, and the whole `scrapEconomy` block. Absent means the mechanic
does not exist, so `ChannelRules.Read` and `Salvage.Read` returning null delete
every rule below rather than misfiring, and the policy strings are read as
confirmation and never as the test. That property is measured, not asserted:
**on `swell` — neither arm — this revision is identical to revision 7 across
all 64 matches, cell for cell, to the decimal.**

**C1 · Stillness is the capture, and a rotation is not a tile change.** The
reseat is the one thing this doctrine does on the objective that changes a
tile, and under `facing-locked` it comes in two halves: a rotation that arms
the seat and a step that takes it. The rotation keeps the tile and stays free;
only the step is charged. So the seat with a firing line is still bought — a
body holding ground it cannot shoot from is the thing this doctrine never
settles for — and the lane shuffle is refused, because standing under fewer
rays is comfort and comfort is not ground. Refusing BOTH halves was the first
version, and it cost a whole striker cell: emptying the march also makes every
fan cheaper, because `Cast` reads an empty march as "the tick was free", and a
2-health body then cast a 0.85 fan at an adjacent enemy and died on it.

**C2 · An interrupt is ground, priced in the units it is declared in.** Damage
to a body of the CONTROLLING team standing ON the objective reverts that team's
run, one progress per point of health, and nothing else reverts anything. So a
poke at a channeller is not a duel move — it is ground, taken from range 8 by a
body that never walks into the fight over the tile, and the salvo fan's
damage-2 bolts revert two apiece against a threshold of eight. The term enters
the shot solver's PRIORITY, which is where positional weight lives, as
`min(1, reverted / threshold)`.

This is the one place a weapon's declared damage enters a positional weight in
this doctrine, and revision 7 had to learn the hard way why that is normally a
category error. The difference is the unit. "Their run loses two of the eight
ticks it needs" is a statement about ground; "it hits twice as hard" is not,
and scaling coverage by a damage ratio remains the worst idea this lineage has
measured. Two siblings of this rule were built and are NOT here, with their
numbers in `DX.md`: collapsing the fire threshold whenever an enemy run stands
(the interrupt makes a LANDED bolt worth ground and a missing one worth
nothing, and it let a 0.13-coverage bolt outbid a step onto the point), and
collapsing it while this body is channelling (C1 has already emptied the march
on exactly those ticks, so `CommitThreshold` charges the free-fire floor
anyway).

**C3 · The escort.** Gain scales with stationary surplus and stops at the
declared cap of two, so the third body buys nothing by standing on the point —
and a great deal one tile off it, on the line the bolts are coming down, where
the interrupt's own scope says damage reverts nothing. The screen seats are
pure contract geometry: `projectilesStopOnFirstEnemyActor` makes a body eat a
bolt for the ally behind it, `alliedProjectileContact: pass-through` means it
does not block the return fire, and the seat is a tile strictly between a
visible enemy and an allied body on the objective along one of the eight
headings. Nothing was added to the game for this; the arm gave an existing
behaviour a purpose.

**C4 · The cap answers "does stacking help?" with a number.** Revision 7 read
`controlPolicy` for one substring and concluded that surplus weight buys
nothing, which is true of the policy it was written against and false of the
channel: two stationary bodies against a dead defence take the point twice as
fast. So the second body joins the point instead of taking the supporting ring,
and the third screens.

**E1 · The assay is picked up, never fetched.** A pile is a goal only for a
body that is not carrying the capture, on a tick with no ground to take, within
four tiles. Every teammate derives the same ordered pile list from the same
frozen observation and takes the entry its own rank in the team's precedence
points at, so two bodies never race for one assay.

**E2 · A tier is bought with a reloading tick.** `invest` costs the body its
action, so it is cast where the action was already spoken for: the gun on
cooldown, no step that takes ground, nothing inbound. Affordability and every
cap are in the legality mask, so this never prices the ladder. What is left is
WHICH track, and that is answered from the declared *effects* against this
chassis's own declared numbers — never from a track's name:

- **gun travel** is a race, and the enemy's tier is public the tick it is
  bought. The striker's one structural edge over a tougher chassis is a band it
  can shoot into and not be shot in; a tier is worth most exactly when the
  opponent's effective reach has closed that gap;
- **sight** is worth what it converts. A gun that travels eight and an eye that
  sees six is a gun aimed at hearsay, and every tier below that ceiling turns a
  tile of range into a tile a shot can actually be solved on. Above it, nothing;
- **spawn health** never heals, so it is a whole extra contact on every FUTURE
  life — most of the match for a three-health duelist, and nearly nothing once
  the horn is close.

**E3 · A loaded carrier is worth more dead.** `carriedScrap` is published on
every visible enemy, and killing one drops its whole load plus its wreck on a
single tile. The priority it buys is scaled by the load against a tier's
declared price, and it stays below the objective's own weight, because a tier
is a gun and the ground is the score.

**T1 · A shared tie-break is drawn on the shared stream.** Every tie in this
doctrine hangs off one mirror-fair direction order, and several of the things
built on top of it are SHARED derivations — the traffic precedence, the tiles a
sibling is expected to want. On the per-life stream two of this team's bodies
answered the same shared question differently and each acted as though the
other agreed. `context.TeamRandom` is identical for every life on the team
within a tick and re-derives per tick, so a body born mid-match agrees on its
first tick.

### The deposit run, declined with a number

Ignoring the veins is a strategy rather than a forfeit, and the contract says
by how much: the supply is a fixed pot of 4 events x 2 sites x 6 = 48, one
harvester services a whole cycle, and a team that never leaves the front still
banks because corpses fall where it is standing and the assay pays in full at
the tile. Measured across this cohort, that by-product alone reaches a tier or
two in any match that runs long, and **zero** in the ones that break a base
before tick 120 — which on the channel's threshold of eight is most of them.
A revision that sent a body to `(11,1)` would have been buying a tier with a
quarter of its body-ticks in the cells where the match was already over.

### The rule that was built, measured, and deleted

Routing a spare body onto a live pile within four tiles. "The assay is picked
up, not fetched" is a real distinction and four tiles is on the wrong side of
it: on the arm where the rule fires most it turned an eight-seed breach win into
an eight-seed loss, because four tiles off the route is a body the front is
missing for eight ticks and the front is the only thing that scores. It cost
**8 wins and 1.62 territorial points** against the same whole. What survives is
the half that costs nothing at all — a wreck under a body already standing there
is banked anyway, because the engine pays the assay at the tile.

### Headline results

The assigned cell in all four shapes it is read in —
`--movement facing-locked --pendulum keel --skills kit --bend universal --aim
offset --stance-ground open --cooldown ticking --volley salvo`, plus
`--capture channel` and/or `--economy scrap`, with `--five-slots wane` wherever
a fabricator is in the pair. Every opponent is a frozen wave-8-cohort artifact;
`own wave-7 self` is this lineage's revision 7 rebuilt on the same CLI. Sixteen
matches per cell (two disjoint seed sets of eight), paired.

| arm | revision 7 | prog | **revision 8** | prog |
| --- | --- | --- | --- | --- |
| `swell` — neither | 70-42-16 | +4.38 | **70-42-16** | **+4.38** — identical, match for match |
| `siege` — channel | 64-48-16 | +2.00 | **80-48-0** | **+4.25** |
| `forge` — economy | 65-40-16 | +4.55 | **81-40-0** | **+8.87** |
| `bastion` — both | 64-48-16 | +2.00 | **80-48-0** | **+3.00** |

**One cell moved, it moved in every armed cell, and it moved for a different
reason in each.** Against its own wave-7 self this revision converts a 500-tick
stalemate into a base breach — 0-0-16 becomes 16-0-0 on `siege`, on `forge` and
on `bastion` — and every other cell is identical in result on every arm. The
leave-one-out grids name the responsible rule per arm: on `siege` it is **C1**
(remove it and the mirror goes back to a draw and then a loss), on `forge` and
`bastion` it is **E2** (remove it and the mirror returns to 0-0-8 draws, exactly
revision 7). Both grids are complete on the shipped source and agree on the sign
of every rule but one — reading the stationary cap (**C4**) is worth 8 wins on
`siege` and costs a point of progress on `bastion`, because it puts a second
body on the point and those extra tile changes are ticks the channel does not
pay for. That disagreement is the honest uncertainty in this pass, and `DX.md`
names it rather than averaging it away.

That is the honest size of this pass. It is also the size the channel's own
arithmetic predicts: at equal bodies on the point, claim minus denial is zero
and nobody gains, so a mirror between two doctrines that both walk onto the
objective is a draw by construction, and the side that stops shuffling is the
only one that can be ahead. The economy arrives at the same place from the other
end — a stalemate is long, a long match banks wreckage, and a tier of sight on a
chassis that already shoots further than it sees is what ends it.

**Seeds are inert and the table says so.** Every cell resolves identically on
all eight seeds of a set, and the two disjoint sets agree cell for cell: 128
`siege` matches carry **8** distinct outcomes. Read the per-cell rows, not the
totals; a 16-point swing in W−L is one cell.

**Three losses are untouched and predate this pass.** `iron-root`,
`gate-stone` and `spark-line` are losses in revision 7's own numbers on this
cohort in every arm; a channel-and-economy commission is not the one that fixes
them, and the pass claims no credit for them either way.

### Not in this doctrine (revision 8 additions)

- **A harvester.** See above. The economy is played where the bodies already
  are.
- **Squatting still while denying.** Denial counts every body, moving or not,
  and only the CONTROLLING team's bodies revert on damage. So a body that is
  contesting rather than claiming pays nothing to keep moving, and C1 is
  deliberately scoped to the channelling case rather than to "on the objective".
- **Reading the control policy's name.** The channel is recognized by the three
  numeric fields that are inert-absent without it, not by matching a policy
  string. A policy ID is prose; a cap is a number.

## Revision 7: the fan is a kill, not a coverage

Revision 7 is one pass over one thing: what the re-armed volley is for. Nothing
else in this doctrine moved — the aperture, the standoff, the dodge ledger, the
capture arithmetic and the whole coordination layer are revision 6's, byte for
byte. What moved is the contract.

Start with the honest number. On this arm, **revision 6 cast the fan zero
times** against its own wave-6 self and zero times against arc-light, across
eight seeds each — 0.61 casts per match across the whole opponent set, and 2.5%
of its damage. Revision 7 casts 10.89 times per match and takes **40% of its
kills** with the fan. That gap is not a bug and it was not laziness: it is one
line of revision 5 doing exactly what it was measured to do, on a contract that
had since changed underneath it.

### The line, and why it was right

Revision 5 refused any cast whose rays covered fewer than two bodies. The
argument was airtight *on its own arm*: the fan's declared spread is the facing
lane and its two 45-degree neighbours, and `--aim offset` hands the mobile gun
those same three headings one at a time, at better cadence, without giving up
the step. So the fan sold **simultaneity** and nothing else — and simultaneity
is worth nothing while every lane points at one body, because a body can only
absorb one contact however many rays sweep it.

Every word of that turns on one premise: **that a fan bolt and a mobile bolt
cost a body the same health.** That premise is a contract field. So the rule is
not deleted in revision 7; it is given the condition it always had, and reads it:

```csharp
if (report.Bodies < 2 && fanDamage <= gunDamage) return null;
```

Where the two guns hit equally hard, revision 5's doctrine plays tick for tick.
Where the fan's bolt is heavier, the premise is gone and one body under the rays
is an ordinary reason to cast.

### What the fan is now for

Four declared numbers, all read, decide the whole file.

**Damage.** The stance gun's `projectile.damagePerHit` against the mobile gun's.
Equal on the old arm; double here.

**Entry windup.** `windup.durationTicks` on the entry route. One tick — the same
grammar as everything else — so a cast is a **reaction** rather than a forecast,
and the whole pinned window is entry + launch + forced exit rather than the four
ticks revision 4 priced.

**Stance-gun cadence.** The stance gun's `cooldownTicks` against the mobile
gun's. At or below it, this life walks out of the stance **still holding its own
weapon**.

**Entry cooldown.** The entry route's own `cooldownTicks` — a route cooldown,
scoped to the UNIT SLOT, surviving this body's death, with its live clock
published on the observation.

Put together they say something specific, and it is not "cast more". They say
**the fan is how a duel ends**, and the doctrine that ships is a target-selection
rule rather than a frequency rule:

- A body the fan would **remove** and the mobile gun would not is the heavier
  bolt's entire product. That is `damagePerHit >= health > gunDamage` — read off
  declared damage against observed health, never off a rule card — and it clears
  a *lower* bar than an ordinary cast, because a body that is gone answers
  nothing and contests nothing.
- A body the fan **opens** — one it does not finish, but leaves inside the mobile
  gun's one-contact band — is worth nearly as much, and *only* because the stance
  gun's cadence sits at the floor. The third point of damage arrives from the
  ordinary gun on the tick after the stance ends. That credit is claimed only
  where the contract actually leaves the gun in hand; where the stance would tax
  it, the term is simply not taken.

### The honest surprise: "twice as hard" is not a multiplier

The obvious way to spend the damage fact is to scale the fan's score by the
damage ratio. It was built, and it is **the single worst idea in this pass**:
against the same whole it cost **8 wins and 7.38 territorial points**.

The reason is worth writing down. This solver scores a launch as *coverage ×
priority*, and priority is a statement about **ground** — who is standing on the
objective, who is already hurt, who has taken itself out of the capture count.
Multiplying a positional weight by a weapon's damage is a category error, and it
double-counts the one place the damage genuinely belongs. The damage-2 fact
enters this doctrine at **exactly one point — the kill thresholds — and nowhere
else.** Removing that one point costs 16 wins and 10.6 territorial points, and it
turns the eight-seed breach win over revision 6 itself into eight tick-cap draws.
That is the shape of the whole finding: the damage does not make the fan
*better*, it makes the fan *finish things*.

### The tick ledger held, and the entry clock did not buy what it promised

Two rules were reasoned out of the new contract, and the board only agreed with
one of them.

**A marching tick became a price rather than a refusal, and that paid.** Revision
6 refused any cast on a tick that had a step worth taking. With a 1-tick entry
the case stops being obviously wrong, so it is charged a margin instead — **8
wins and 8.6 territorial points** better than refusing. Two refusals stay
absolute, because both are ground *this* tick: a capture this body is
deliberately withholding, and a step that actually takes a tile. This is the
smallest of the four live rules, and it is the one this doctrine was most
prepared to be wrong about: "ground outbids the gun" has been the spine of every
revision, and a 1-tick entry is exactly the sort of change that tempts an author
to weaken it further than the board will pay for.

**"A paid entry is spent, not dropped" was reasoned, built, and lost.** Frequency
is priced on the entry, and the entry clock starts when the route *completes* —
so a body standing in a stance is already inside the window and leaving early
refunds nothing. Firing a worthless fan and mobilizing out cost the same single
tick, and only one of them can hit. It measured **9 wins and 2.99 points worse**.
Sunk is sunk; the tile is not. A body that will not walk out of a form it cannot
move in gives up more ground than a wasted entry is worth.

### A repair the new arm created by accident

Revision 6 refused to enter a stance whenever one worst-case contact could kill
this body — `health <= HardestHit`, where `HardestHit` is the largest damage any
declared attack profile deals. On every arm this lineage had been measured on
that number was **one**, so the rule fired essentially never.

The salvo arm makes the fan itself the largest declared damage in the contract,
and the rule silently became *"no wounded body may ever cast"* — which deletes
the exact window where the fan is worth most, the wounded duel where whoever
lands first wins. The rule was always a **proxy** for a fan aimed at this tile,
and that is something the observation reports directly: an enemy inside a stance,
or inside the windup into one, publishes the form its gun will fire from and
cannot turn while it does. So the proxy is gone and the real question is asked;
the tracked-threat test, scoped to the actual pin length, still carries the
ordinary bolt.

This is the second-largest effect in the pass — **28 wins and 20.9 territorial
points** — and it is not a strategy. It is a rule that was already in the source,
already correct, and quietly re-aimed by an arm that only touched the fan.

### Reading the entry clock is inert here, and ships anyway

`self.routeCooldowns` publishes `{transitionId, readyAtTick}` for every held
route of this body's unit slot. The doctrine reads it and never requests a held
route.

**It measures completely inert on this arm**, and the reason is a platform fact
worth knowing: while a route cooldown is live, the per-tick legality mask already
reports `transform` as unavailable with an *empty* `allowedFormIds`. Measured
over one mirror match: 155 ticks with a live clock, `transform` available on
zero of them; 263 ticks with no clock, available on all 263. A bot that reads the
mask — which this one must, for every other reason — never eats a Blocked tick
whether it reads the clock or not, and removing the read changed nothing to the
decimal across 64 matches.

It ships because the mask's agreement is a convenience rather than a contract,
because the clock survives this body's death and a life born mid-window has no
history to infer it from, and because the published tick is the only thing that
would let a future revision *plan* around the window rather than discover it.
Its measured attribution is zero and is reported as zero.

### Headline results

The assigned cell —
`--movement facing-locked --pendulum keel --skills kit --bend universal --aim
offset --stance-ground open --cooldown ticking --volley salvo`, with
`--five-slots wane` wherever a fabricator is in the pair. Every opponent is a
frozen wave-6 artifact; `wave-6 self` is this lineage's own revision 6 rebuilt on
the same CLI. Eight seeds per pairing.

| cell | revision 6 | prog | casts | fan kills | **revision 7** | prog | casts | fan kills |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| striker mirror vs **wave-6 self** | 0-0-8 | +0.0 | 0.00 | 0.00 | **8-0-0** | **+38.0** | 15.00 | 8.00 |
| striker mirror vs still-water | 8-0-0 | +23.5 | 1.62 | 1.62 | **8-0-0** | **+30.0** | 4.38 | 4.38 |
| striker mirror vs arc-light | 8-0-0 | +30.0 | 0.00 | 0.00 | **8-0-0** | **+30.0** | 9.38 | 5.38 |
| bulwark-vs-striker vs iron-root | 1-7-0 | −21.4 | 0.12 | 0.25 | **2-6-0** | **−21.0** | 8.00 | 1.50 |
| bulwark-vs-striker vs march-wall | 0-8-0 | −8.0 | 0.00 | 0.00 | **8-0-0** | **+9.0** | 13.00 | 4.00 |
| bulwark-vs-striker vs gate-stone | 0-8-0 | −30.0 | 0.00 | 0.00 | **8-0-0** | **+26.0** | 17.00 | 9.00 |
| fabricator-vs-striker (wane) vs spark-line | 0-8-0 | −17.0 | 2.00 | 3.00 | **8-0-0** | **+30.0** | 2.00 | 2.00 |
| fabricator-vs-striker (wane) vs ledger-fly | 5-3-0 | +3.6 | 1.12 | 0.75 | **0-8-0** | **−7.9** | 18.38 | 7.00 |
| **all 64** | **22-34-8** | **−2.41** | **0.61** | **0.70** | **50-14-0** | **+16.77** | **10.89** | **5.16** |

Repeated on a **disjoint** set of eight seeds: revision 7 **48-16-0 / +15.80**,
revision 6 **24-32-8 / −0.80**. Cast rate 10.97 and fan-kill share 41%, against
10.89 and 40% on the first set.

**Seeds are nearly inert and the table says so.** The opponents are deterministic
artifacts, and a seed perturbs only this bot's own tie-break stream. Across the
64 matches above there are **64 distinct replay hashes but 15 distinct
(result, progress, end-tick) outcomes** — five of the eight cells resolve
identically on all eight seeds. Read this as roughly eight correlated cells of
evidence, not 64 independent observations; the per-cell rows are the honest
picture and the totals are a convenience.

**The one regression is real and is reported as one.** Against `ledger-fly` the
revision loses a cell revision 6 won, and it is the cell where it casts most
(18.4 per match). A five-slot fabricator fields more bodies than this chassis
can remove, and a doctrine tuned to end duels spends its entries on a stream of
replacements. Nothing in a fan-integration budget fixes that; naming it is the
alternative to hiding it behind a total.

**Two standing weaknesses are untouched and predate this pass.** `iron-root`
remains a loss (1-7 → 2-6) and is this lineage's oldest open problem in the
bulwark cell; the pass moved it by one match and claims no credit. `gate-stone`
flipping 0-8 → 8-0 is the largest single-cell swing here, and it comes from the
safety re-derivation rather than from any new idea about fans.

## Revision 6: three bodies, one plan

Revision 6 changes nothing about how this bot fights and everything about how
its own bodies get out of each other's way. The doctrine, the sight-band
standoff and the aperture tie-breaks are revision 5's, untouched. What is new is
a coordination layer — `Traffic.cs` — and the honest surprise in it is that the
worst thing three bodies were doing to each other was not blocking.

**They never blocked. They oscillated.** Revision 5's route search returned the
*cheapest* legal first step. With the one tile that shortens the route occupied
by a sibling, the cheapest legal step is a step **away** — and next tick the step
back is cheapest again. Under `facing-locked` each leg also buys a rotation, so a
body spends four ticks arriving exactly where it started, over and over, while
its sibling stands in front of it. Measured on the striker mirror, revision 5
never once blocked a sibling and still lost 152 route steps per thousand
two-body ticks to a tile a sibling was standing on.

So the first rule is the one that matters: **a route step reduces the route, and
yielding means holding the tile, not touring the one behind it.** A body with no
forward step spends the tick on its gun and keeps its ground. That single rule is
worth the whole of the measured improvement.

**Precedence is written down, and it is about the game.** Among this team's own
bodies: the one nearer the contested ground keeps its tile and its route, ties go
to the body with more health — the one that can survive a corridor — and actor
identity is only the last resort that makes the order total. A total order is
what makes the whole scheme deadlock-free without a word of negotiation: the most
senior body claims nothing from anyone, so somebody always moves.

**A sibling's claim is a preference, and that tier is a measurement, not a
shortcut.** A senior body claims the tiles its shortest routes need this tick and
next — the union over tied routes, since a sibling's own tie-break runs on its
private stream — plus any one-tile corridor run it owns. Honouring those claims
*absolutely* was built and measured: it cut jammed route steps by 85% and
rotation thrash by 61%, and lost the striker mirror 0-4-0 at the breach floor,
because a body that will not contest open ground on the chance a sibling wants it
has stopped playing. Binding only the corridors — the principled middle, since a
corridor has no second lane — lost too, and produced *more* corridor jams, because
on this map the chokes **are** the routes to the objective. So the rule that ships
prefers to leave a sibling's ground alone and takes it anyway when it is the only
way forward.

**A corridor still needs an explicit rule for the case waiting cannot fix.** When
two of this team's bodies are already inside one one-tile run, waiting is not a
plan — the senior one may be walking toward the junior. The junior backs out
along the run. Turning in place, which is what revision 5 did there, buys nothing
at all inside a full corridor.

**Two bodies on one ray are one firing seat used twice**, because the target slips
both bolts with one step while the front body absorbs every answer. Among
equal-value seats this body takes a ray no sibling holds — in the **tie-break** and
in the seat score, never as a filter on where a body is going.

**Its sibling rule — spacing — was built, measured, and is not here.** Two of this
team's bodies on two rays of one enemy facing are two hits from one volley, so
refusing that pose is obviously right and it obviously lost. As a filter on
destinations it cost 7.5 points of mirror progress, because a destination chosen for
its bearing to a contact is exactly the construct revision 5 measured at 2-38-0 and
deleted. Narrowed to the free tie-break it stopped being harmful and still did not
earn its place: over twenty seeds it removed about a tenth of the shared-fan poses
and turned thirteen breach wins into thirteen tick-cap wins for 1.95 less progress.
What spacing costs on this map is the tempo that closes a match. A pose may be
preferred; it may not be chased; and a preference still has to pay.

**Nothing rallies into its own traffic — one tile of it.** Where the contract gives
this team placement influence, an imminent arrival's landing tile and its only exit
are left clear, so a fresh three-health body is not born boxed in by its own
family. The tile is read from the reservation where a reserved operation publishes
one, and derived as the rear-most free tile along this team's own advance direction
where it does not. Claiming the whole own-side objective region instead — my first
version — cost 23.5 points of progress and two wins, because that region is
*ground*.

**And a collision that already happened is worth remembering for two ticks.** Two
bodies whose routes meet at one tile from opposite sides both take it and both
block; each remembers the block for exactly one tick through
`PreviousActionResolution`, holds, forgets, and collides again — measured running
to the tick cap. The junior body leaves that tile alone for two ticks while the
senior one keeps going, which turns 110 own-traffic blocked ticks into 8 with every
record unchanged to the decimal. Refusing a sibling's merely *intended* tile fixes
the same thing and loses the mirror; refusing one this body has already lost costs
nothing, because the collision is a fact rather than a forecast.

Every rule is measured with and without on the same seeds, and the attributions —
including the two rules that measured completely inert and the reasons they
shipped anyway — are in `DX.md`.

## Revision 5: the aperture, not the barrel

The mobile gun launches at −1/0/+1 sectors off facing now. Everything in this
revision follows from one asymmetry that fact creates, and the asymmetry is not
about shooting diagonally — it is about which facings can shoot at all.

**A cardinal bearing is armed from one facing; a diagonal bearing from two.**
Face east and only east fires due east, but north-east belongs to the north
facing's three rays and the east facing's alike, because a diagonal is the shared
boundary of two apertures. Under `facing-locked` a rotation is not a flourish —
it is how a body travels — so a contact on a diagonal is the one pose where
turning onto a route does not cost the shot. Steps and objective seats are
credited for it, and the credit is worth exactly nothing to a chassis that never
paid an aperture, which is the point.

**An aim offset is a direction, not a curve.** An aim-only diagonal is one
decision, one cooldown, one committed heading — the shape of a straight bolt, not
of a bend — so it is enumerated beside the straight answer and needs no bend
margin, while offset-plus-bend joins the curved family. Two shots that were
literally unsolvable programs before now exist: the diagonally adjacent body, hit
on the launch tick instead of being unreachable at every distance, and the tile a
target steps *onto* when it slips a straight bolt.

**Dodging became a matter of degree.** One enemy facing lays three rays, so
"out of the lane" mostly stops existing near a contact. The router counts the
rays over a tile and takes the fewest rather than pretending it can find none.

**The fan lost its argument.** Revision 4 declined the cast but granted it
bearings the gun could not buy. The volley's declared spread is the facing lane
and both 45° neighbours — the gun's three aim options exactly — so the fan now
sells only *simultaneity* across lanes, at more than double the cadence, from a
form that cannot move. Since a body can absorb one damage however many rays sweep
it, that is worth nothing while the lanes point at one target: the cast requires
more than one body under the rays, and refuses to feed a raised arc at all,
because every deflected ray returns along the exactly reversed heading to a tile
a stance cannot step off. A fan does break a shell in one cast. The caster is not
who profits.

**And a seat the other body cannot answer from is worth more than a tile.**
"Range" is three different numbers per form, and two of them disagree between
chassis in opposite directions: this one sees six and shoots eight where an
omnidirectional chassis sees four and shoots six. A body whose declared *sight
range* is shorter than this one's is blind in a band this one is not — at every
bearing, from every facing, and no rotation closes it. Free damage in that band
is the only way a three-health duelist beats a five-health one, so the doctrine
stops advancing there. It is a stop, never a chase: it is read off the tile
already occupied, it never applies while standing on the objective or while a
body that can actually score stands on it, and where two envelopes match it is
inert — which is why it does nothing whatever in a mirror. The offsets are what
make it practical, because a standoff seat needs a firing line and three rays
leave every facing instead of one.

Three further ideas were built, measured, and deleted: destinations chosen for
their bearing to a contact (they thrash — under `facing-locked` a change of
destination costs a rotation, and a bearing-derived destination changes every
time the contact steps), routing in actions rather than tiles (correct cost
model, and it arrives *earlier*, which is how a lone body ends up deep in ground
the opponent respawns beside before a companion exists), and buying facings in
the route's own tie-break. Their numbers are in `DX.md`.

## The idea

Ground is the only score, so the whole match reduces to one question — *what
does the front need from this body, this tick?* — and the contract answers it.
A baseline Frontline pays for *sole* presence: two bodies on one objective
cancel out and the claim rots, one body alone banks a point a tick, and there
are exactly two ways to be the only one standing there — arrive first, or shoot
the other one off. VectorEdge does both, in that order, and never trades the
second for the first.

But "sole presence pays" is a rule this contract happens to declare, not a law.
Another one scales capture pressure with surplus objective weight, and then a
second body beside the first is the fastest capture on the board. Another
protects a completed advance for a while, and then a capture finished inside
that window resets its own claim and moves nothing at all. Revision 3 reads
those declarations instead of assuming them — see below.

## Why a striker fires the way it does

The striker's private verb is a committed trajectory: straight, or one 45°
bend after a chosen number of tiles. A bend is not free — it is the same tick
and the same cooldown as a straight bolt, spent on a path that arrives
somewhere the target might not be. So the choice is a read on what the target
can still do:

- **Corridors get straight suppression.** A body with two or fewer open
  neighbours cannot step aside. It can only advance or retreat, and both keep
  it on the line. A plain bolt down that line threatens every tile it could
  occupy for the next several ticks; a bend would just find a wall.
- **Open chambers get the bend.** Where a target has lateral escapes, the
  straight bolt covers exactly one of them. A bent path sweeps tiles no
  cardinal shot from any facing can reach, which is the only way to threaten a
  target that is not on a line from the gun.

Every legal program the contract declares is traced against the declared
projectile rules — launch tiles, tiles per advance, travel budget, strict
diagonal corners, wall termination — and scored against a dodge model of the
target: where can it be, at the tick each path tile is actually occupied,
weighted by the fact that it wants the objective too, and by the fact that a
body cannot react to a shooter outside its own declared sight envelope. A bend
must beat the best straight answer by a clear margin before it is taken, so
bends stay a weapon rather than a habit.

## Revision 4: a special is worth the ticks it costs, and one of them isn't

The class-skill kit hands a striker one new verb and puts two more on the other
side of the board. Revision 4 prices all three the way this lineage prices
everything — against the tick they spend — and one of the answers is *no*.

**What the target can reach stopped being a prior and became geometry.** The
dodge model used to spread a target's probability over a ball of tiles and lean
on a stickiness multiplier where that ball was wrong. Under `facing-locked` it
is very wrong: the movement mask offers the current facing and nothing else, so
a body runs down the line it is already on and pays a whole rotation tick before
it can step anywhere else. Two ticks of that reach five tiles, not thirteen. The
solver now searches *pose* — a tile and a facing, a tick that is either a turn or
a step along it — so the reachable set is the contract's own legality instead of
an approximation of it. This is the largest single effect in the revision: it
re-prices every straight bolt and every bend on the phase-2 board, and it is
where the measured improvement comes from.

**A shield's arc is fixed, so the answer is to go around it.** A form declaring
`projectileGuard` deflects any contact arriving inside its facing quadrant, and
it cannot rotate while raised — so the arc is a piece of map, not a threat that
tracks. A bolt into it buys nothing and launches one back owned by the other
team. So the solver scores that contact as dead, taxes the returned bolt, and
finds the bend that curls to a flank instead; the router treats standing inside
the arc as a cost. Against a shell-raising fixture built from this same source,
revision 3 fed nine bolts into the arc across five matches and revision 4 fed
zero, shifting from 26% bent shots to 42%.

**And the fan is a losing weapon here.** The contract says what a cast costs:
two wait-only ticks to enter, exactly one launch (the return counter is
`attacks-issued-since-entering-source-form` at threshold one), a forced exit
windup, and a stance gun whose cooldown is more than double the mobile gun's.
Four ticks and five ticks of cadence buy one fan; the same window buys two
mobile bolts and two steps. What the fan uniquely sells is *bearings* — three
rays diverging from tile one, covering the diagonals a striker's gun can never
open on, because its shot program declares no initial aim offset and its
earliest bend is a tile out. Against one body that is not enough bearings to pay
for it, because a fan can only take one damage off any single target.
*(Superseded in revision 5: the ±1 initial aim offsets hand the mobile gun those
exact three headings, one at a time, so the fan stopped selling bearings
altogether and the decline was re-derived rather than re-used — see above.)*

That is not an argument, it is a measurement. Three separate gates that let the
cast happen at all — fifty casts, thirty-four casts, and a version with the value
bar effectively removed — all landed on the same record: two wins worse and 1.3
territorial points worse than the identical doctrine with casting compiled out,
and every breach loss in the wave traced to a stance. So the route is read, kept,
and priced, and it is taken only where the ticks were genuinely free: from a seat
the stance's own objective weight holds, on a tick with no step worth taking, or
against a body that cannot move at all. On the measured board those conditions
and a fan worth casting never coincide, and the honest report is a cast count of
zero rather than a special used because it exists.

**Envelopment over concentration.** Both specials in the kit are paid to punish a
shared bearing: a fan sweeps three rays from one gun, and a shield answers
exactly one bearing and nothing else. So a body picks a destination on a bearing
no ally already holds, tie-broken steps avoid fans, lanes and arcs, and a body
standing in a fan that can launch this tick steps out of it — movement resolves
before combat, so the tick a stance first appears is still a tick in which one
step buys the whole launch. It never concedes an objective tile to do it.

**The hold is asked now.** `holdOwnerTeamId` and `holdEndsAtTick` are published
on the mode observation, so revision 3's reconstruction — the redeploy clock run
backwards for the start, and a guess from the front's displacement for the owner
— is gone from the live path. It survives as a fallback for exactly one case the
published fields cannot be told apart from by value: a ruleset that declares a
hold, sitting inside the redeploy pause only an advance can start, reporting no
owner. Everywhere else a null means what it says.

## Revision 3: ground is priced from the contract, not from the rule card

Every tick, VectorEdge asks the capture policy three questions and lets the
answers decide what the tick is worth.

**Does standing here earn anything?** Under binary control an enemy body on the
objective cancels this one and the only capture is to remove it. Under a policy
where surplus weight scales pressure, a contested objective this team
outnumbers still pays every tick — so the same tile is worth holding rather
than clearing, and a spare gun belongs *on* the ground instead of covering it
from the ring.

**Would a capture completed now actually move the front?** Where a completed
advance is protected by a hold, a capture finished inside that hold is
discarded: the claim resets and the objective does not budge. Ticks poured into
it buy a bank, not ground. So while an opposing hold is live, the ground stops
outbidding the duel — the body fights instead of grinding — and if waiting
reaches a real advance sooner than grinding does, it steps one tile off and
lets the claim sit until the hold expires. Both plans are counted in ticks and
compared; which one wins turns over as the hold runs down, and the arithmetic
is all contract values.

The hold's start is not something the observation reports. It is recovered from
something that is: an advance sets the redeploy clock, so running the declared
redeploy arithmetic backwards from it names the tick the front last moved.
*(Superseded in revision 4: the observation publishes the live hold's owner and
end tick, so this derivation is now a fallback only — see above.)*

**Where will the next body appear?** A contract that lands automatic arrivals on
the own-side objective has already delivered the second gun to the front, so
walking home to build one only removes the first gun from it.

None of this branches on an arm's name, and on a contract that declares none of
those things all three readings collapse onto what revision 2 assumed — its
decisions there are identical, tick for tick.

## Revision 2: the dodge model is measured, not assumed

Revision 1 wrote its opponent model down as a constant — a watching target was
assumed to hold its tile a little under half the time. Against most opponents
that is close enough. Against one that refuses to close and steps off every
single bolt, it is catastrophic, and the striker mirror showed exactly what it
costs: two duelists two tiles apart, both firing a shot the model valued above
one full point, both stepping aside, both stepping back, for hundreds of ticks
with the objective contested and neither side scoring a thing. A shot that has
stopped working keeps looking like the best thing on the board, because the
model that priced it never finds out it was wrong.

So the rate is counted now. Every tick, VectorEdge compares where each visible
body was with where it is, ignoring any body that could not see the gun — a
target that cannot see a shooter is not choosing to stand still, and its
stillness is not evidence about anything. The count starts at revision 1's
assumption, carries a few observations' worth of weight behind it, and moves as
evidence arrives. It is life-scoped, like every other private memory here: a
fresh life meets its opponent again from the prior.

What follows from that is the whole revision. Against a body that dodges
everything, the straight bolt's value collapses toward the tiles the target
will actually be standing on — which is where the bend was already looking, so
the bend wins the comparison it used to lose, and when neither wins, the tick
goes back to the ground. Against a body that holds its line, nothing changes at
all. And it generalizes across the movement arms without being told about them:
where the contract couples facing to movement a sidestep costs the target its
own aim and its own sight quadrant, and where the contract locks movement to
facing a sidestep is not even legal. Both make targets stickier, so both raise
the prior the ledger starts from — and then let the bodies on this map settle
it.

## Tempo

A shot costs the tile the tick would have bought, so fire is rationed by
expected value: high while advancing, cheap while already standing on the
objective (the tick was free anyway), cheapest when an enemy body is on the
objective and shooting it is how the ground gets taken.

Aim is rationed the same way, but against the weapon's cadence rather than
against position. A tick on which the gun is ready is worth one whole bolt, so
turning on one is nearly always a mistake; a tick inside the cooldown window
costs nothing at all. Revision 1 could not tell those two apart — it was only
able to ask "what would I hit facing there?" on a tick that could also fire —
so it paid for all of its aim with shots, and measurably lost two of every five
of those turns to a life that ended before the shot ever came out. The gun is
laid on the last tick of the cooldown window now, where the shot it buys is the
very next one.

## Suppression over concession

The reflex to step out of an inbound bolt is how positions get given away for
free. VectorEdge dodges *within* the objective when it can, and when it cannot
— and it can survive the hit — it answers the shot instead of surrendering the
tile. Health is a resource; the frontline is the score.

Where the contract couples facing to movement, that calculation gets sharper
rather than different: a dodge is also a turn, so it spends the aim and the
sight quadrant along with the tile, and a bolt already in hand that beats every
post-dodge answer is worth taking the hit for. Where movement is locked to
facing, a sideways dodge is a rotation this tick and a step the next — two
ticks against a bolt that lands on the first — so the escape set collapses on
its own and holding the ground is very nearly the only honest answer left.

## Bodies

Companions are a force, not a formality. Where the contract activates them on a
schedule they are simply used; where it requires an explicit fabrication, a
lone body will walk back to a declared source and build one — but only while
the front is far enough from home that a conceded push is not a conceded match.
Where control is binary the nearest body to the objective owns the capture and
the others take the supporting ring rather than stacking on a tile that pays
nothing extra; where the contract scales pressure with surplus weight they all
go to the objective, because there the second body is the capture.

Every life sees the same frozen picture and never sees an ally's current action,
so coordination is a shared *derivation* rather than shared state: each body
computes the same precedence from the same observation and they reach
complementary conclusions. The order is written down — nearer the contested
ground first, then more health, then actor identity — and it decides who keeps a
tile, who keeps a corridor, and who spends the tick on its gun instead. See
revision 6 above, and `Traffic.cs`.

Where a tougher, objective-neutral emplacement exists, a spare body that is not
carrying the capture may take it to lock an approach. It is deliberately a rare
commitment — an emplacement cannot take ground, and taking ground is the plan.

## Not in this doctrine

- **Splitting.** Halving a duelist into two one-hit bodies trades the thing
  that wins duels for presence that evaporates on contact.
- **Camping.** Distance is not safety in a mode that scores position.
- **An absolute sense of direction.** Every tie between two equally good
  directions is broken advance-first, retreat-last, with the two
  perpendiculars ordered by this life's own deterministic stream. An absolute
  preference is a systematic edge for whichever team happens to advance that
  way on a mirror-symmetric map, and both sides sharing it does not cancel out.
- **Walling off its own corridor.** A bolt of this team's own is only an
  obstacle where the contract says allied contact stops it. It does not here,
  and treating it as one anyway is how a duelist blocks the lane it just fired
  down.
- **Squatting in a stance.** A stance is entered to be spent. With nothing worth
  a fan from any bearing it may legally take, this body drops the stance rather
  than hold a form it cannot move in — the budget was never the reason to leave,
  and neither is a spent entry clock. Revision 7 reasoned the opposite from the
  contract (the clock starts when the entry *completes*, so leaving refunds
  nothing), built it, and measured it 9 wins worse. Sunk is sunk; the tile is not.
- **Yielding by walking backwards.** A route step reduces the route. A body that
  gives way to a sibling holds its tile and lays its gun; it does not tour the
  tile behind it and come back, which is what four wasted ticks look like on a
  replay.
- **Over-yielding.** A sibling's claim is a preference. Refusing open ground
  because a sibling might want it was built, measured, and lost the mirror at the
  breach floor — coordination that stops contesting the objective is not
  coordination.
- **A pose chased rather than preferred.** A bearing worth standing on is worth
  taking for free — among equally short steps, among equally good seats. It is not
  worth a rotation, because under `facing-locked` a destination derived from a
  contact's bearing changes every time the contact steps, and the body then pays a
  turn per enemy step and arrives nowhere. Measured, not assumed.
- **A special used because it exists — or refused because it once lost.** The fan
  is read, priced and measured every revision, and the answer is whatever the
  contract's numbers make it. Revisions 4, 5 and 6 priced it and declined; on the
  arm that re-armed it, the same arithmetic casts about eleven times a match and
  takes half its kills that way. Neither answer is a preference. A doctrine that
  casts to look like it is using the kit is spending ground; a doctrine that
  keeps refusing because a previous contract said so is reading a memory instead
  of the contract.
- **Hard-coded anything.** Participant and team IDs, slot counts, unlock ticks,
  form names, action codes, map tiles, objective coordinates, projectile
  constants, shot-program bounds, collision policy, movement facing coupling,
  control policy, decay clock, advance-hold length, arrival placement, stance
  routes, stance budgets, fan width, guard arcs and the number of slots either
  side may field are all read from `StartLife.Contract`, the frozen observation
  and the per-tick legality mask. The same source plays the base Labs contract,
  the duel-depth arms, the striker class arm, all three movement arms, every
  pendulum counterweight and the whole class-skill kit without branching on any
  of them by name.

## Layout

| File | What it holds |
| --- | --- |
| `VectorEdge.cs` | the doctrine: one ordered priority list per tick |
| `Doctrine.cs` | everything resolved once from the match contract |
| `Skills.cs` | the class-skill kit recognized by shape: stance routes, budgets, guards, slot counts |
| `Cast.cs` | when a fan beats the gun, whom to aim it at, and how to conduct one once standing in it |
| `Field.cs` | the per-tick read: threat, occupancy, objective, fans, arcs, roles |
| `ShotSolver.cs` | which legal shot program to commit to, and why |
| `DodgeLedger.cs` | what the watched bodies actually did, tick by tick |
| `Advance.cs` | what the declared capture policy makes this ground worth, channel included |
| `Channel.cs` | what a CHANNELLED capture is: the cap, the erosion multiple, the interrupt |
| `Salvage.cs` | the battlefield economy: piles, the assay, and which tier a striker buys |
| `Ballistics.cs` | local replay of the declared projectile path rule |
| `Arms.cs` | the gun's aperture: which headings a facing may launch along, and how many facings buy each |
| `Traffic.cs` | the coordination layer: precedence, route and corridor claims, arrival clearance, pair spacing |
| `ArenaBasics.cs` | the generated starter helper, trimmed to the readers this doctrine calls (see `DX.md`) |
