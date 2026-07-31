# SparkLine — TEMPO ENGINE

Class: **fabricator**. Lineage `spark-line-v1`, revision 7 (wave 8). Role:
verdict-doctrine, target cumulative T4.

## The idea in one sentence

Win the objective clock with **bodies per tick**: queue every companion the
instant the contract makes it legal, put those children in the field beside the
prime rather than behind it, and keep the fragile prime alive only to the extent
that keeps it spending fabricate actions.

## What revision 7 changed, and why

Wave 8 changed what taking ground **is**, and it changed the fabricator's
opening from difficult to unsurvivable. Three published facts do it between
them, and none of them is about this chassis:

- a fan bolt deals **2**, and this class's prime has **2** health, so one bolt
  from a stance that enters in one tick deletes the only body that can build;
- a capture costs **8**, not 15, so a body that dies buys the opposition a
  whole capture and most of a second during its nineteen-tick absence;
- capture progress now counts only bodies that **did not change tile**, and
  hostile damage to a controller standing on the point **reverts the whole
  run**.

The predecessor walked its two-health prime down the centre corridor into a
three-health gun with longer reach and died at tick 10 of a match that ended at
tick 52. Revision 7 is one sentence long:

> **A body that one contact kills does not hold contested ground alone, and
> stillness is a purchase.**

Everything below is a consequence of it, and every consequence is a stat
comparison rather than a class name — so the same artifact plays the cells that
carry the fan and the cells that do not, and the rules switch themselves off the
moment the arithmetic stops saying what it says here.

### 1. Denial is separable from claim, and it is the cheaper half

Under the channel my presence subtracts from the opposition's claim whether or
not I stand still, and adds to my own only while my tile does not change. The
two are therefore priced separately. Standing still is **bought** — it is worth
the surplus it earns — and the price is being a fixed target for a gun whose
every landed bolt takes the whole run back. So a body on the point stands still
while the surplus is positive and nothing is pointed at the tile, and **walks
the region** the moment either stops being true: full denial weight retained,
a forfeited gain that was going to be reverted anyway, and a target that is not
where the last bolt was aimed.

The interrupt is noticed without ever seeing the gun. There is no revert event
and none is needed: a run that is mine whose published progress went **down**
was reverted, and the only thing that reverts a run is damage landing on my own
body on the point. That read sees a shooter a facing quadrant cannot.

### 2. Lethal ground is ground, not risk

The predecessor answered a threat by dodging the bolt that had already been
fired. Against a gun whose single contact ends this life that is one tick too
late — the bolt crosses two tiles a tick and the dodge is a coin flip. So the
**lane** is what gets broken, and it is broken before the shot: for every
visible body, the heaviest contact it or any form one same-life route away can
deliver is compared against **this body's current health**, and if one contact
kills, the eight clear rays out to that gun's declared travel are ground this
body does not stand on.

Nothing there names a skill, a class, or a form. The same code maps a fan
against a two-health prime, an ordinary bolt against a body already down to one,
and nothing at all against a body the board cannot one-shot — which is why this
class's own **three**-health children cross the corridor the prime refuses.

### 3. The standoff, and the ground it holds instead

A fabricator prime is the only body its team has until its first slot unlocks,
and it is the frailest body on the board. Nine ticks of walking put it on the
centre at the same tick as a longer gun with more health; it loses that duel
every time. So while it is genuinely alone it holds **its own side of the
chain** — the ground the front reaches if this position is lost, resolved from
the ordered chain and this team's declared advance delta — and only while that
ground is the nearer of the two. The opposition then has to come through the
map's geometry to take it, and the clock it spends coming is clock this class
converts into bodies.

The rule ends three ways, all of them contract reads: the tick a child exists
the prime is not alone; a health tier off the ladder makes it not fragile; and a
cell whose heaviest declared gun cannot one-shot it never engages the branch at
all. It is bounded on the other side too — **denial outbids cover**: while the
opposition has a live run, a body one step from the region steps on rather than
sideways, because denial counts every body on the point whatever its feet are
doing.

### 4. Two economies at once, and what they are actually worth

The pot is **fixed** — four events of two deposits — and one courier services a
whole cycle, so a numeric class buys no extra income at all with its extra
bodies. It buys security of collection, and that is a different thing. What is
genuinely free here is the **assay**: it pays at the tile with no transport and
costs no action, so a pile within a step of a route this body was walking anyway
is taken, and nothing else is. A dedicated harvester is not sent; the measured
version of that rule cost more front than it banked.

The ladder is read as effects, never as track names: **health while one contact
kills** (the corrective tier, and the one that switches the standoff off),
**reach while I am outranged** (gap-preserving — it buys the opening shot, not
the kill), **sight last** (naturally terminal). The mask is the price list, so
this policy never does the affordability arithmetic and never guesses at a
Blocked; and exactly one body of the team casts per tick, elected from the
shared observation, because two purchases against a bank that covers one resolve
in canonical order and the second is refused.

`invest` sits below every dodge and every step onto ground and above the gun. A
purchase moves no tile, so a channeling body casts it without breaking the
stillness its claim is made of — but it still costs that body its action, and
the ladder is a match-long asset while a dodge is worth exactly the tick it
happens on.

### 5. What the extra bodies do

The wave-6 coordination layer is unchanged and is the foundation this pass
stands on: distinct objective tiles by minimum total walk, route yield, choke
precedence, no fabricating into own traffic, and a spacing tie-break, all of
them pure functions of the frozen shared observation. Wave 8 adds one term to
it. Against a **broken** defence the gain is the capped surplus of stationary
claim, so two still bodies double the rate and a third buys literally nothing —
the number is the declared cap, not the body count. Against a **live** one the
arithmetic inverts: his denial is subtracted whether he stands or kites, so one
body holds the ground and the surplus goes where it **stops the bolt** — a tile
strictly between a gun and a body of mine on the region, on a clear ray.
Projectiles stop on the first enemy actor and allied bolts pass through allies,
so a screen is a shield that does not blind the team behind it.

Direction tie-breaks now draw from `context.TeamRandom` rather than the per-life
stream, so a randomized choice is a coordinated one: teammates drawing the same
index in the same tick get the same answer, and a life created this tick agrees
with its siblings on its first.

## What was measured and rejected

Recorded here because a rejected rule is evidence too, and the numbers are in
`DX.md`:

- **Stacking to the cap against a live defence** (weight target = enemy denial
  plus the cap). It reads correctly off the gain formula and it is a loss: every
  extra body on the point is another body whose every point of damage reverts
  the whole run, and it also starves the screen rule by never leaving a body
  spare.
- **A dedicated scrap courier.** Elected from the body the front missed least,
  timed to the deposit schedule. The fixed pot means the second walker buys no
  income, and the front notices the absence immediately.
- **Leaving the region for cover.** A body already on contested ground that
  repositions out of it hands over the point to buy tiles it does not score on.
  It repositions inside the region or not at all.
- **"Alone" meaning alone on the tiles.** The first standoff asked whether an
  ally was standing on the region; two bodies both off it then both stood off,
  each waiting for the other, and the point went uncontested for whole matches.
- **`invest` at the very bottom of the order**, spending only a tick that was
  going to be a Wait. It delays the ladder past the point where it pays.

## Deliberate omissions

SparkLine never anchors and never splits, even on contracts that offer them. A
turret has objective weight zero — under this economy it can no longer pick up
or carry either — and a split trades one healthy fabricator for two fragile
bodies that cannot fabricate again. Both are the opposite of a doctrine whose
currency is mobile presence and rebuild rate.

## Contract-driven, not class-coded (revision 7 additions)

Read, never assumed, and every one of them absent-means-inert: the control
policy that identifies a **channel**, its stationary gain cap, its opposing
erosion multiplier and its claim interrupt; the whole **scrap economy** block —
deposit addresses and schedule, wreck and assay amounts, carry capacity, pile
lifetimes, this team's bank region resolved by team ID against the declared
region list, the purchase mode that distinguishes the verb from the automatic
control arm, and every track's declared **effect**, magnitude, depth and price;
the live bank, tier vector and pile list from the mode observation; carried load
on self, allies and visible enemies; the upgraded slot resolved from the
lifecycle assignments rather than assumed to be unit zero; and the heaviest
declared contact in the attack catalog, which is the number this whole revision
turns on.

Nothing here reads a ruleset name, a class name, or a form-ID prefix. A fan is
recognised as damage, travel and straightness; a channel as a control policy; a
store as a purchase mode; and a fragile body as one whose health is not greater
than the heaviest bolt the ruleset declares.

## Files

- `SparkLine.cs` — the policy and its priority order.
- `Channel.cs` — the channel arithmetic, the lethal-ground map, and the screen
  geometry, rebuilt every tick from the frozen observation.
- `Supply.cs` — the economy: what to buy, and who walks.
- `Doctrine.cs` — one switch per wave-8 rule, for leave-one-out attribution.
- `ContractLens.cs` — every static fact resolved once per life from the
  contract.
- `Tactics.cs` — reachability fields, the host's own shot-path rule replayed
  locally, per-bolt tick-of-arrival arithmetic, and clear-ray geometry.
- `Squad.cs` — the wave-6 coordination layer.
- `ArenaBasics.cs` — the current template helper, synced verbatim.

## Earlier revisions

Revisions 1–6 are frozen under `arena-bots/frontline-labs/classes-wave-1…6/`
with their own READMEs; the wave-6 README describes the coordination layer this
revision inherits unchanged.
