# SCRAP — a battlefield economy for the frontline (2026-07-30)

Status: **design exploration. Nothing built, nothing registered, no arm token
minted.** This memo is the second pass on the side-objective question,
commissioned after the owner read
[`DESIGN-SIDE-OBJECTIVES-2026-07-30.md`](DESIGN-SIDE-OBJECTIVES-2026-07-30.md)
and rejected the whole hold-the-zone family with three directives, verbatim:

> "I think some kind of buffs on the units would've been more fun"
>
> "Do better. Can we involve RTS aspects? Not necessarily a 'hold the zone'"
>
> "We also need the side lanes of the map to matter! But these ideas are close"

Read before this: the prior memo (its five laws and §2 map findings are load
bearing here and are not restated in full), the class brief
([`EXPERIMENTAL-FRONTLINE-CLASSES.md`](EXPERIMENTAL-FRONTLINE-CLASSES.md)), the
architecture ([`GAME-MODE-ARCHITECTURE.md`](GAME-MODE-ARCHITECTURE.md) §§4, 7,
9, 10), and DECISIONS #171–#184 — especially **#184**, which closed the class
triangle for the first time in the campaign and which every number in §5 is
written to protect.

Everything numeric below was measured against the real map
(`FrontlineLabsDefinition.MapTileRows`, 23×15, the `frontline-labs-01` family)
with a facing-locked shortest-path search, not estimated. The distance table in
§4 is reproducible.

---

## 1. What the directives actually changed

The prior memo's whole family — RELAY, MUSTER, BATTERY, BEACON — shares one
shape: *a tile set, a latch, an owner, and a continuous effect while you hold
it*. The owner rejected the shape, not the tuning. Three things follow, and
they are the design constraints for everything below.

**"Buffs on the units" means the payoff has to be attached to bodies, not to
the team's clock.** MUSTER pays in respawn geometry; BATTERY pays in the
enemy's capture arithmetic. Both are felt at the scoreboard and nowhere on the
screen. A buff is felt when a body you are looking at does something it could
not do a minute ago. That forces the reward channel to be **form stats**, which
in turn forces a currency, which is why SCRAP is an economy rather than a
second flag.

**"RTS aspects, not hold-the-zone" means the mechanic must have an
extraction–transport–conversion loop, not an occupancy timer.** In an RTS the
interesting decisions are *how much force to divert from the front*, *whether
to raid the diversion*, and *what to convert the income into*. Occupancy timers
have exactly one of those. A carried resource has all three, and the transport
leg is the piece that has no analogue anywhere in this game today.

**"The side lanes must matter" is a hard geometric requirement, and it is the
one that decides §3.** Rows 1 and 13 span x=1..21 uncontested and no objective,
spawn, pad, region, or tag touches them; they are 30 of the map's ~230 walkable
tiles doing nothing. A mechanic that merely *puts a prize there* makes the lane
a destination visited once per cycle. A mechanic that makes you *carry
something back through it* makes the lane a place where fights happen in both
directions. Those are different amounts of "matter", and the directive is a
directive.

---

## 2. Laws re-checked, and two new ones

The prior memo's five laws survive; three of them bind harder here.

**L1 (side control is objective weight, so a turret cannot hold one)** becomes
**objective weight gates the economy**: a form declaring weight 0 may not pick
up or carry scrap, and transitioning into one drops the load. Without this
rule, a bulwark child anchors on a vein site and becomes a permanent denial
engine that also banks the assay every cycle for free — the single worst
degenerate this design has, and it is closed by the rule the class slate
already rests on.

**L2 (a side objective must not pay the timeout channel)** is enforced by the
compiler here, not by discipline. `FrontlineGameModeDefinition` hard-validates
`ScoreCatalog.Length == 1 && ScoreCatalog[0].Channel == TerritorialProgress &&
Domain == Signed`, and `FrontlineVictoryDefinition.TimeoutRanking` has exactly
one entry. **The scrap bank therefore cannot be a score channel even if
somebody wanted it to be** — it is mode observation state. That settles half of
§8 before it is argued.

**L4 (absence must be readable)** is why §8 publishes three facts rather than
one, and why I break the prior memo's L3 cap on purpose (§8.4).

**L5 (every side objective is a fabricator buff and a striker nerf)** is
answered structurally rather than by scoping: the vein supply is a **fixed
pot** (§4), so extra bodies buy *security of collection*, not *more income* —
and the fabricator, which dies most, is also the largest single supplier of
wreckage to its opponent. §9 does the arithmetic.

Two laws SCRAP adds:

**L6 — Upgrades may only move class stats along axes where an additive step
preserves or compresses the class gap.** The triangle closed at bvs +0.333,
fvs −0.222, bvf +0.333 (#184), inside the registered [0.15, 0.40]
cycle-magnitude band with roughly 0.07 of headroom at the top and 0.18 at the
bottom on the tightest leg. An upgrade both teams can buy re-prices the
triangle only if the step is *ratio-asymmetric* across classes. Range and
vision differ between classes by a fixed 2 tiles; a +2 step to everyone leaves
the gap at 2. Fire cooldown differs by 1 tick but by a **ratio** of 3:2, and a
−1 step to everyone takes the ratio to 2:1. That is the whole test, it is
arithmetic rather than judgement, and it is why gun tempo is cut from the
working list (§5.2).

**L7 — Upgrades change what bodies ARE, never how many.** The owner's hard
constraint, restated as an engineering rule with teeth: no upgrade may touch
`PrimeRespawnTicks`, `ChildRebuildDelayTicks`, slot counts, unlock ticks, or
fabrication economics. Body count and body tempo stay the fabricator's
monopoly. This also kills the otherwise attractive "respawn −3 ticks" tier:
respawn tempo *is* expected body count, and dressing it as a stat does not make
it one.

---

## 3. Instant bank versus carried scrap — recommendation

### 3.1 The three options, priced

| | INSTANT | CARRIED | **ASSAY (recommended)** |
|---|---|---|---|
| Pickup | banks the full amount | loads the body | banks 1, loads the rest |
| Counterplay | win the race to the tile | intercept on the way home | both |
| Lane traffic | one direction | two directions | two directions |
| Worst case for a harvester | lose the race, gain 0 | die loaded, gain 0 | die loaded, gain 1 |
| New per-body published fact | none | `carriedScrap` | `carriedScrap` |
| New mode state | none (piles still needed for wreckage) | piles + carry | piles + carry |
| Marginal cost over INSTANT | — | one int, one deposit rule | one int, one deposit rule |

The marginal-cost row is the point that decides it. **Wreckage already forces
"scrap sitting on a tile" into mode state** — a destroyed body drops at its
death tile and somebody has to come and get it. Once pile state exists, a
dropped carry is *the same object*: a killed carrier is simply a bigger wreck.
So the machinery gap between INSTANT and CARRIED is one integer on the body and
one rule about where you may deposit. That is not the ten-to-one cost ratio it
looks like from the outside.

### 3.2 Why carrying, in the map's own numbers

Facing-locked travel times, measured (§4.1 has the full table):

- centre objective → north lane vein: **14 ticks**; → south lane vein: **13**
- vein → own home pad: **16 ticks**, identical for both teams and both lanes
- north vein → south vein: **22 ticks**, straight through the contested middle

Under INSTANT, the harvest costs 27 ticks (front → vein → front) and **26 of
them are safe**: after the pickup tick the body is carrying nothing and killing
it recovers nothing. The lane is a vending machine on a public timer.

Under ASSAY, the same body owes a 16-tick loaded walk home, and the greedy
double-run — take both veins before banking — spends **22 consecutive loaded
ticks crossing the middle of the map**, which is where the enemy already is.
Interception is a priced, repeatable play worth up to 4 scrap plus a kill, and
it is available to the team that is behind, which is exactly where a
counterplay should sit. The lane matters going out because that is where the
prize is, and it matters coming back because that is where the prize can be
taken from you. **That is the owner's third directive satisfied by geometry
rather than by assertion.**

### 3.3 Why the middle option rather than pure carry

Pure carry has one bad property: a harvest that gets intercepted returns
exactly zero, so a contested economy is a sequence of coin flips whose loser
paid 30 body-ticks for nothing. That is the DROP concept's failure mode
("reads as a coin flip") wearing a different hat. A fixed **assay of 1 banked
on pickup** puts a floor under every trip: a fully-denied harvester still
converted its walk into 1 scrap, one eighth of a tier. It also makes wreckage
frictionless — a wreck is worth exactly 1, so it is assayed in full at the
front with no transport at all, which is what keeps the economy from pulling
everybody off the objective (§7).

One rule, three consequences:

> **On stepping onto a scrap pile, a body banks `assay` (=1) for its team
> immediately and loads the remainder as carry, up to `carryCapacity` (=4).
> Carry banks in full when the body stands on its own team's home pad.
> Destruction drops the carry at the death tile, merged into that body's own
> wreck.**

### 3.4 Rejected variants, with reasons

- **Bank on crossing into your own half.** The veins are on the centre column
  (x=11) so that they are equidistant by construction; from there "your own
  half" is *one step west or east*. Degenerate. Making it non-degenerate means
  moving the veins off centre, which costs the free mirror-exactness.
- **Deposit at three separate "forge" tiles, one per upgrade track** (invest by
  routing rather than by action). Genuinely cheaper — it removes the entire new
  action family from the build (§8.5) and is arguably *more* RTS. Rejected for
  v1 on two grounds: three forges cannot be made equidistant on this map, so
  the tracks acquire different travel prices and the balance question becomes a
  level-design question; and it deletes liquid reserve, so a team can never
  hold scrap and buy the counter to what it just saw. Registered as the
  alternative level `scrap-forge-delivery`.
- **Carrying costs objective weight** (the RELIC concept's weight-0 carrier).
  Elegant, and it would make escorting a real team shape. Rejected as a second
  large rule in a v1 that already carries one; registered as
  `scrap-carrier-weight`.

---

## 4. The map, measured — and the vein schedule

### 4.1 Distances (facing-locked, one tile or one rotation per tick)

Team-0 home pad `(2,7)`; team-1 home pad `(20,7)`; frontline positions 0–4 at
`(3–4,8–9)`, `(6–7,5–6)`, `(10–12,7–8)`, `(15–16,5–6)`, `(18–19,8–9)`.

| from | to | steps | ticks (facing-locked) |
|---|---|---:|---:|
| centre objective `(11,7)` | north vein `(11,1)` | 10 | **14** |
| centre objective `(11,8)` | south vein `(11,13)` | 9 | **13** |
| team-0 home `(2,7)` | north vein `(11,1)` | 15 | **16** |
| team-0 home `(2,7)` | south vein `(11,13)` | 15 | **16** |
| team-1 home `(20,7)` | north vein `(11,1)` | 15 | **16** |
| team-1 home `(20,7)` | south vein `(11,13)` | 15 | **16** |
| team-0 home `(2,7)` | centre objective | 9 | **9** |
| north vein | south vein | 18 | **22** |
| position 1 `(7,6)` | north vein | 9 | **10** |
| position 3 `(15,5)` | north vein | 8 | **9** |
| position 0 `(4,9)` | south vein | 11 | **14** |

Two structural facts fall out of this table and both are load-bearing.

**The mirror is exact and free.** `x = 11` is the map's centre column and the
tile rows are palindromic about it, so a vein at `(11,1)` or `(11,13)` is 16
ticks from *both* home pads. No map edit, no new region, no symmetry argument
beyond the one the map validator already enforces. **SCRAP needs no new map
generation** — it runs on `frontline-labs-01-classes` unchanged, so it stays
fingerprint-comparable to every arm measured so far. This is a direct
improvement on MUSTER, whose registered ablation debt records that minting
`frontline-labs-02-muster` made it non-comparable on map and topology.

**Which lane is cheap depends on where the front is.** From the outer high
positions (1 and 3, at y=5–6) the north lane is 9–10 ticks away; from the outer
low positions (0 and 4, at y=8–9) the south lane is nearer. The errand price
therefore moves with the pendulum, for free, in both directions, symmetrically.
That is the kind of texture the map was supposed to have and did not.

### 4.2 Vein sites and schedule

- **Sites: exactly two, `(11,1)` and `(11,13)`.** Both are lane tiles with two
  approach headings by construction (east and west along an open row), so the
  prior memo's "any side site must have at least two approach headings" is
  satisfied without widening anything. Both have a 2-tile alcove behind them
  (`(11,2)/(11,3)` north, `(11,12)/(11,11)` south) — a retreat, not a fortress,
  because the alcove is a dead end for the occupant too.
- **Both sites spawn on every event.** Two prizes, two teams: the natural
  equilibrium is one each, taking both costs a second body, and faking one is a
  real bluff. One alternating vein would be a pure race whose loser paid a full
  trip for nothing, and would snowball off the first race.
- **Schedule: first spawn tick 120, every 60 ticks, last spawn tick 420** —
  events at 120, 180, 240, 300, 360, 420. Six events, twelve veins.
  - *Why 120 and not 60*: striker and bulwark receive their first companion at
    tick 120, the fabricator at 60. A vein at tick 60 would ask a striker to
    send its **only** body away from the front, and would hand the fabricator a
    free two-body window. Starting at 120 is the tick at which every class has
    exactly two bodies. This is a class-fairness constraint, not a pacing one.
  - *Why stop at 420*: a vein taken at 420 banks at ~452 and converts to a tier
    at ~455, leaving 45 ticks of payoff. A tick-480 event could not be banked
    (16 ticks home) and converted before the horn; it would be scenery.
- **Deposit size: 3 scrap per vein.** With `assay = 1` that is 1 banked at the
  tile and 2 carried, so two veins exactly fill the carry cap of 4.
- **Deterministic and readable in advance from the contract.** Sites, first
  tick, interval, last tick, and amount are all static `MatchStart` data.
  Nothing is seeded, nothing is drawn from the PRNG. A bot knows every vein's
  address and spawn tick before tick 0 and does not have to discover the
  mechanic by watching it happen. What is *not* derivable is whether a given
  vein is still there — that is published (§8).
- **Displacement rule:** if the site tile is occupied by a live body at the
  spawn tick, the vein appears on the nearest free tile in the same lane row,
  scanning by ascending `|dx|` then ascending `x`. This closes camping-as-denial
  (park a body on the tile and no vein ever spawns) without adding randomness.

**Total vein supply: 6 events × 2 veins × 3 scrap = 36 scrap per match, split
between both teams.** That number is the economy's throttle and it is the first
lever anyone should reach for.

### 4.3 Wreckage

**Every destroyed body drops 1 scrap at its death tile**, merged into any pile
already there. Piles merge by tile, so a killed carrier leaves one pile worth
`1 + carriedLoad`.

Wreckage is doing three specific jobs and each is worth stating:

1. **It makes kills convert.** The slate's own structural diagnosis — "kills do
   not convert (18-tick full-health respawn vs 15-tick capture)" — is attacked
   directly, and at a magnitude that prices *incidental* kills rather than
   sought ones. This is the PYRE concept folded in for free, with no new tag
   kind and no new region.
2. **It keeps the front in the economy.** A team that never leaves the
   objective still banks, because corpses fall where it is standing and `assay`
   pays in full at the tile with no transport. This is the mechanism behind §7.
3. **It is the fabricator's damper.** The class that fields the most bodies and
   loses them fastest is the largest single supplier of scrap *to its
   opponent*.

### 4.4 Pile decay

**Every pile evaporates 60 ticks after it appears** — one number for veins and
wrecks alike. Consequences: an untaken vein disappears exactly as the next pair
spawns, so **at most two vein piles exist at once** and the observation
collection stays small and legible; a winning team cannot stockpile corpses
near its front and cash them later; and a losing team has a real window to come
back for ground it briefly lost. Hard engine bound: at most 16 piles exist
simultaneously; if a 17th would appear the oldest evaporates first, so the
published collection is provably bounded.

### 4.5 The tick budget

One harvest, measured:

| run | route | ticks | banked |
|---|---|---:|---:|
| single | home → vein → home | 16 + 16 = **32** | 3 |
| greedy double | home → N vein → S vein → home | 16 + 22 + 16 = **54** | 6 |
| opportunist | front → vein → front | 14 + 14 = **28** | 1 (assay only) |

- A **dedicated harvester** running greedy doubles banks the entire 6-scrap
  output of a 60-tick cycle in 54 ticks. The vein economy is therefore
  calibrated to cost **exactly one body**, and the supply is exactly one body's
  worth of work. A second harvester adds no income at all, only contest
  security — which is the fixed-pot property that answers L5.
- **Fraction of team-ticks:** a body dedicated from tick ~104 (leaving home to
  meet the first spawn) to 500 spends ~396 body-ticks. Against 3 bodies × 500 =
  1500 that is **26%**; against a 4-slot team **20%**; against a 5-slot `full`
  fabricator **16%**. That spread is the L5 tilt stated numerically, and it is
  bounded because the extra bodies cannot raise income.
- **What the front pays:** while the harvester is out, the front runs 2-of-3.
  Capture threshold is 15 and the `contest-majority` control policy scales gain
  by surplus weight, so a 2v3 front is not merely slower — it can be *negative*.
  That is the allocation cost, and it is severe enough that nobody will harvest
  while the front is genuinely live. Good: the mechanic should be a thing you
  do in the seams, not a parallel game.
- **The economy cannot dominate the pendulum**, arithmetically: the whole
  vein supply is 36 scrap and the tier ladder caps at 3 tiers (§5.4). Even a
  team that takes every vein uncontested converts to a stat delta of 3 integer
  steps on one body. There is no quantity of harvesting that buys a capture.

---

## 5. Upgrades under the closed triangle

### 5.1 The mechanism: modifiers, not form variants

The obvious implementation — pre-declare an upgraded form for every tier
combination and swap slots into it — multiplies the form catalog by the number
of reachable tier vectors. With three tracks at two tiers that is 27 vectors ×
6 body forms × 2 classes in a cell = **324 forms in the resolved contract**
against today's ~12. That is a contract-size disaster and it is unnecessary.

Instead:

- **The ladder is static contract data.** `rules.gameMode.scrapEconomy.tracks[]`
  declares each track's ID, its closed typed effect kind, its per-tier
  magnitude, its max tier, and its tier costs. A bot reads exactly what every
  tier of every track does before tick 0.
- **The team's current levels are dynamic observation state**, published each
  tick (§8). Same discipline as the ratchet hold: *the contract field is the
  DURATION, the observation is the CLOCK.*
- **The engine applies the modifier at the point of use**: effective travel
  tiles = form's declared travel + edge level; effective vision range = form's
  declared range + optic level; effective max health = form's declared max +
  plate level.

Everything a bot already reads stays truthful — `self.Health`, `self.Cooldown`,
the movement and attack legality masks, `visibleTiles` — because those are
authoritative post-application values. The one thing that becomes a *base*
rather than an effective number is the form catalog's declared stat, and the
derivation is one addition with both operands published. That is exactly the
"read the envelope, do not assume it" discipline the bend arms already require.

**Application timing:** immediately on purchase, to every live and future life
of the team's **Prime slot**, and to every form that slot occupies (mobile,
turret, stance). A max-health increase raises the ceiling and **does not heal**
— current health is preserved — so a purchase mid-duel is never a rescue. Range
and vision apply from the next tick.

**Prime-only scope** is the L5 mitigation built into the mechanic rather than
bolted on, and it is the same move MUSTER made with `PrimeAutomaticReturnOnly`:
the reward is **flat per team**, so a five-slot fabricator gets exactly as much
upgraded body as a three-slot striker. It also gives the design a legible
fantasy — you are upgrading a chassis, not a swarm — and it keeps the viewer's
attention on one body. Registered as `scrap-upgrade-scope`, with an all-bodies
level pre-registered.

### 5.2 Which stats are safe — the gap test (L6)

Base class stats (`FrontlineLabsClassDefinition`):

| | striker | bulwark | fabricator |
|---|---:|---:|---:|
| gun travel tiles | 8 | 6 | 7 |
| vision range | 6 (quadrant) | 4 (omni) | 6 (quadrant) |
| prime max health | 3 | 5 | 2 |
| fire cooldown | 2 | 3 | 2 |

Apply the same additive step to everyone and look at what happens to the gap:

| axis | step | striker | bulwark | fabricator | gap before → after | verdict |
|---|---|---|---|---|---|---|
| gun travel | +2 | 8→10 | 6→8 | 7→9 | S−B = 2 → **2** | **preserving** |
| vision | +2 | 6→8 | 4→6 | 6→8 | S−B = 2 → **2** | **preserving** |
| max health | +2 | 3→5 | 5→7 | 2→4 | B/S = 1.67 → **1.40**; F/S = 0.67 → **0.80** | **compressing, toward the floor** |
| fire cooldown | −1 | 2→1 | 3→2 | 2→1 | rate ratio S:B = 1.5 → **2.0** | **widening — rejected** |

**Does gun tempo −1 break bulwark-vs-striker again? Yes, and here is the
arithmetic.** A striker needs 5 hits to kill a bulwark prime (5 HP); at
cooldown 2 that is 8 ticks of sustained fire. A bulwark needs 3 hits to kill a
striker prime (3 HP); at cooldown 3 that is 6 ticks. The base kill race is
**bulwark by 2 ticks** in a 6–8 tick window, and bvs currently sits at +0.333
with only 0.18 of room before it falls out the bottom of the band. Give both
sides −1: striker 5 shots at cooldown 1 = 4 ticks; bulwark 3 shots at cooldown
2 = 4 ticks. **The race becomes a dead tie.** A single tier erases the entire
duel asymmetry that the bvs leg is priced on, and it does so *because both
teams bought it*, which is the property that makes it undetectable as a "buff
one side" problem. Tempo is cut from v1.

It is cut, not abandoned. `scrap-tempo-track` is pre-registered as a future
level with **class-aware pricing** — the general law being *effects are never
class-aware, prices may be class-aware exactly where the effect is
ratio-asymmetric* (bulwark would pay less for −1 because it gains 50% where the
others gain 100%). That is a coherent way to ship the fun-sounding buff later,
measured on its own.

**Optics carries one honest caveat.** Bulwark vision is omnidirectional, so +1
range grows its observed area by 56% (r² 16→25) against a quadrant's 36%
(36→49); on an area reading, OPTIC tilts toward the class at the top of the
ladder. On the reading that matters for target acquisition — *can I see as far
as I can shoot along a heading* — the gap is identical for striker and bulwark
(both see 2 tiles short of their gun) and OPTIC closes it identically. Both
readings are true; the tilt is mild and it is the one place v1 knowingly takes a
risk. Registered as `scrap-optic-vision-shape-interaction`.

### 5.3 The three tracks

| track | effect kind | tier 1 | tier 2 | why |
|---|---|---|---|---|
| **`edge`** | `MobileAttackTravelTilesDelta` | +1 tile | +2 tiles | Gap-preserving. Buys the opening shot rather than the kill — a positional buff, not a DPS buff. Striker 8→10, bulwark 6→8, fabricator 7→9. |
| **`plate`** | `SpawnMaxHealthDelta` | +1 HP | +2 HP | The one deliberately corrective track. Compresses the shots-to-kill ratio toward 1, which helps the fabricator (ladder floor at 2 HP) most and the bulwark (ladder top at 5 HP) least. Never heals. |
| **`optic`** | `VisionRangeDelta` | +1 tile | +2 tiles | Gap-preserving. Every class reaches *see-as-far-as-you-shoot* on this ladder: striker 6→8 = gun 8, bulwark 4→6 = gun 6, fabricator 6→7 = gun 7 at tier 1. Marginal utility is naturally capped, which is why max tier is 2 and a third tier would be worthless by construction. |

The effect kinds are a **closed enum of exactly three values**, in the same
shape as `FrontlineSecondaryControlDefinition.SecondaryEffectKind`. Adding a
fourth track later is one enum value plus one application point, not a new
capability.

**Class-neutral effects, class-neutral prices, for v1.** The three axes were
chosen precisely so that neutral pricing is defensible (§5.2's gap table).
Class-aware branches are the wrong first move: they double the tuning surface
and they make an arm's effect inseparable from its price, which the ablation
registry would then owe an isolation for.

### 5.4 Costs, caps, and the marginal-utility curve

- **Tier 1 costs 8. Tier 2 costs 16.** Tier 2 requires tier 1, so a maxed track
  costs 24.
- **Three tier-1s also cost 24.** That equality is the anti-rich-get-richer
  design: *going deep and going broad cost exactly the same*, so tier 2 is
  never a discount for being ahead. The choice is made on effects, not on
  economics.
- **Hard cap: 3 tiers total across all tracks, maximum 2 in any one track.**
  The reachable maxima are `(2,1,0)` at 32 scrap or `(1,1,1)` / `(2,0,0)` at 24.
  Nothing a team can do reaches a fourth step.
- **Marginal utility is diminishing within a track by construction.** The second
  tile of gun range is worth less than the first (fewer engagements are decided
  at 9–10 tiles than at 8–9), and the second tile of vision is worth close to
  zero once you can see your own gun's reach. Only `plate` is roughly linear,
  which is why `plate` is the track most likely to be bought to tier 2 and the
  one to watch in the ablation.

### 5.5 The telegraph rule

**Every purchase is public on the tick it happens, to both teams, with no
inference.** The mechanism is already in the box: a tier change moves the
Frontline mode state, and a changed mode state publishes a `ModeChanged` fact
carrying the complete post-change state — the same event the ratchet hold's
lapse and the muster flag's turnover ride on. So the enemy sees the bank drop
and the tier rise in the same tick, in `visibleEvents`, with no visibility
requirement (mode facts are not perception-gated), and can read both teams'
complete tier vectors every tick from `mode.scrapTeams`.

**Zero new event kinds.** This is worth stating loudly given the observation
budget: the entire purchase telegraph costs nothing beyond the mode-state
fields that §8 was going to publish anyway.

---

## 6. Anti-snowball

### 6.1 The loops that actually exist

- **(A) Winner farms wreckage.** Winning fights makes corpses near your own
  bodies; corpses are assayed in full with no transport. Real, and the tightest
  loop in the design.
- **(B) Winner controls lane access.** *This loop does not exist*, and that is a
  deliberate structural choice: the veins are on the centre column, 16 ticks
  from both home pads, and lane access is independent of where the front stands.
  A losing team pushed back to position 0 is 14 ticks from the south vein; the
  winning team standing on the same objective is 14 ticks from it too. Compare
  the alternative I rejected — mirrored *pairs* at `(5,y)` and `(17,y)`, one
  deep in each half — which would have handed each team a safe vein and made
  lane access a direct function of front position. That version snowballs; this
  one cannot.
- **(C) Bank size.** A team that is ahead banks more and buys more. Real, and
  bounded by the 3-tier cap.

### 6.2 The minimal damper set (adopted)

1. **Pile decay, 60 ticks.** Caps the *rate* at which map control converts into
   bank. You cannot bank what you cannot reach in time, and a dominant team
   cannot stockpile. One number, visible, and it also bounds the observation.
2. **Carrier drop.** The largest single scrap transfer in the game — up to 5
   scrap on one tile (4 carry + 1 wreck) — is available to the team that
   intercepts, i.e. structurally available to the team that is behind and
   therefore has bodies free to raid rather than bodies committed to holding a
   won front.
3. **The 3-tier cap with 2-per-track.** The ceiling is enforced by rules, not by
   income, so even a total economic wipeout converts to 3 integer stat steps.

That is the whole set: one number, one rule that already exists for other
reasons, one cap.

### 6.3 Rejected dampers, with reasons

- **Death drops a fraction of the BANK.** Rejected. It turns the bank into a
  second health bar and *amplifies* loop (A) rather than damping it — the team
  losing fights loses bank on top of losing bodies. It is also unreadable: a
  bank that drops for a reason you did not see is exactly the "owner without a
  clock" malformation #169 rejected.
- **Bank decay over time.** Rejected. The only interesting decision the bank
  offers is *spend now or save for tier 2*, and a decay clock deletes it by
  making saving strictly worse. It is also invisible — nothing happens on
  screen — which fails the owner's stated criteria the way BATTERY did.
- **Catch-up pricing (cheaper tiers for the team behind on territory).**
  Rejected. It couples the economy to the victory currency, which is precisely
  the coupling L2 exists to prevent; it is opaque to read; and it is a balance
  patch wearing a mechanic's clothes. If the economy needs catch-up, the vein
  schedule is the honest lever.
- **Diminishing tier prices for the poor / escalating for the rich.** Rejected
  as a variant of the above.

---

## 7. Ignoring the economy must be viable-ish

**The mechanism, not just the number:** a team that never leaves the front
still banks, because wreckage falls where it is standing and `assay` pays in
full at the tile. Ignoring SCRAP costs you the *vein* channel, not the whole
economy.

Rough accounting for a 500-tick match (the wreck count is a design target to be
read off wave 8, not a measured fact):

| | vein scrap | wreck scrap | total | buys |
|---|---:|---:|---:|---|
| pure pendulum (never leaves the front) | 0 | ~12 | **~12** | one tier-1 |
| even split (both teams harvest) | ~18 | ~12 | **~30** | three tier-1s, or one maxed track |
| uncontested economy | ~33 | ~12 | **~45** → capped | the 3-tier cap (32) |

**Ceiling, stated exactly: the maximum stat delta a full-economy team can reach
is three integer steps, never more than two on one axis, applied to one body
(the Prime).** In the worst realistic case a pendulum-only team faces an
opponent whose prime has +2 gun range and +1 max health while its own has +1 of
something. That is roughly one class matchup's worth of edge.

**Intended payoff band.** Stated in the campaign's own units so it is a gate
rather than a vibe: the pooled edge of a full-economy doctrine against an
economy-ignoring doctrine of equal quality should land in **[0.15, 0.35]** —
the same cycle-magnitude band the class triangle is held to. Below 0.15 the
economy is decoration and nobody will write the code path (the turret's 0.13%
usage is the precedent). Above 0.40 the economy is the game and the pendulum is
decoration, which is the failure the whole design is arranged to avoid. Both
bounds are pre-registered.

---

## 8. Observation and action surface

### 8.1 What cannot be a score channel

Settled by the compiler, not by argument: `FrontlineGameModeDefinition`
hard-validates a score catalog of exactly one signed `TerritorialProgress`
channel and a timeout ranking of exactly that channel. **The bank is mode
observation state.** Any design that wanted it on the scoreboard would have to
widen Frontline's victory contract, which is the L2 trap.

### 8.2 The three new published facts

**Fact 1 — the public ledger.** On
`ModeObservationState.Frontline`:

```text
scrapTeams : ImmutableArray<ScrapTeamState>     // ordered by teamId
  ScrapTeamState(int TeamId, int Bank, ImmutableArray<int> TierLevels)
```

`TierLevels` is ordered by the contract's declared track order. Empty array on
every ruleset without the economy — the additive inert default, so a bot never
branches on whether the mechanic exists. **One field carries both teams'
complete economic state**, which is what makes the purchase telegraph free.

*Irreducible?* Yes. Bank is not derivable: income arrives from pickups the
opponent cannot see, and a life born mid-match has no history to reconstruct
from (the #169 argument, verbatim). Tier levels are not derivable either — a
bot could try to infer them from observed enemy behaviour, which is the
inference #169 rejected.

**Fact 2 — the loose scrap.** On the same record:

```text
scrapPiles : ImmutableArray<ScrapPile>          // ordered by (y, x)
  ScrapPile(Position Position, int Amount, int ExpiresAtTick)
```

`ExpiresAtTick` reads exactly like `holdEndsAtTick` and `readyAtTick`: the pile
is gone the first tick `tick >= expiresAtTick`. Piles merge by tile so no
`origin` discriminator is needed — a wreck landing on a live vein is one pile.
Bounded by construction: at most 2 vein piles (decay = cadence) plus wrecks
within a 60-tick window, hard-capped at 16.

*Irreducible?* The vein half is derivable from the contract schedule — but
*whether a vein is still there* is not, and neither is a wreck's location for a
body that was not present at the kill. Deriving it would require exactly the
life-scoped historical memory the architecture does not have.

*Accepted consequence, stated plainly:* publishing all piles leaks enemy deaths
that the perception union would otherwise have hidden. It is symmetric, it is
small (a pile says "a body died here within 60 ticks", not which one), and the
alternative — a race you cannot see — is the DROP concept's headline failure
mode. Registered as `scrap-observation-completeness` with a fog-limited level
that would move pile amounts onto `ObservedTile` beside `SpawnReservation`.

**Fact 3 — the load.** One integer, on three records:

```text
ObservedSelfState.CarriedScrap  : int
ObservedAllyState.CarriedScrap  : int    // allies share complete gameplay state
ObservedEnemyState.CarriedScrap : int    // a visible enemy's load is visible
```

Zero when not carrying, and zero for the whole match on a ruleset without the
economy.

*Irreducible?* This is the fact that makes interception a decision rather than
a guess: "is that body worth chasing" is exactly the question, and without it
the harass loop the owner asked for is a coin flip. The ally copy follows the
established precedent (`RouteCooldowns` is published on self and allies for the
same reason). The enemy copy is the L4 case.

### 8.3 Contract additions (static, MatchStart — not observation)

```text
rules.gameMode.scrapEconomy            // whole block absent when the arm is off
  veinSites[]              : [(11,1), (11,13)]   declared order, north then south
  veinFirstSpawnTick       : 120
  veinSpawnIntervalTicks   : 60
  veinLastSpawnTick        : 420
  veinAmount               : 3
  wreckAmount              : 1
  assayAmount              : 1
  carryCapacity            : 4
  pileLifetimeTicks        : 60
  maxSimultaneousPiles     : 16
  bankRegionIds[]          : ["team-0-home-pad", "team-1-home-pad"]
  upgradeScope             : prime-slot-lives-only
  maxTotalTiers            : 3
  tracks[]                 : { trackId, effect, perTierMagnitude, maxTier, tierCosts[] }
```

Absent-means-inert throughout, exactly as `secondaryControl` is absent on a
ruleset without a side objective, so historical rulesets keep their exact
fingerprints and the canonical writer emits no bytes.

### 8.4 The observation budget, and breaking the prior memo's L3

The prior memo set "one published fact per concept, two as an absolute cap".
SCRAP costs **three**. That is a deliberate break and it deserves the argument
rather than a footnote.

L3 was calibrated for a family in which every concept was *one latch with one
effect* — MUSTER shipped two fields and touched roughly thirty-five files.
SCRAP is not one latch; it is a resource, a store, and a spend, and the honest
comparison is not to MUSTER but to **#169/#170, which were run as a schema
window**: a batched observability bump with its own SDK/CLI version bump, its
own codec/replay/mirror sweep, and a closing ruling. SCRAP should be run the
same way and should not be smuggled into an unrelated wave.

The budget was actively minimised, and here is where the savings came from:
bank and tier levels collapsed into one array of team records rather than two
fields; the pile origin discriminator was removed by making piles merge by
tile; the purchase telegraph reuses `ModeChanged` so **no new event kinds** were
added; and the vein schedule went into the contract rather than the observation
so that positions and due ticks are static rather than published per tick (the
DROP concept would have spent two observation fields on exactly that).

### 8.5 The `invest` action

**Shape.**

| | |
|---|---|
| action ID | `invest` |
| action code | `PublicActionCodes.Invest = 106` (100 fabricate, 101 transform, 102 shoot-direction, 103 split, 105 shoot-straight are taken) |
| action kind | `ActorActionKind.ModeInvestment = 7` — an additive append to the closed enum, the #156 discipline |
| parameter kind | `ActorActionParameterKind.UpgradeTrack = 5` — additive append |
| payload | `ActorActionPayload.UpgradeTrackId : string?` — the envelope's doc-comment already says *"New parameter kinds append fields; existing meanings never change"* |
| argument | `GenericActorRuntimeActionArgument.UpgradeTrackArgument(string TrackId)` |
| legality mask | `ArgumentConstraint.UpgradeTrackConstraint(ImmutableArray<string> AllowedTrackIds)` |

**Who may cast it: any live body of the team, on any tile, with no positional
requirement.** Not Prime-only (that would add a denial vector — freeze their
economy by killing one body — which is a gotcha rather than a decision), and
not at a forge tile (that would double-tax an errand that already costs 32
ticks). Both alternatives are registered as `scrap-invest-caster-scope`.

**It costs the body's action for that tick.** The same price `fabricate` pays
and for the same reason: a free verb is not a decision.

**Affordability lives in the mask, not in the bot.** A track appears in
`AllowedTrackIds` only when the team's bank covers that track's next tier and
the caps permit it. A bot that reads its legality masks — which every
contract-driven bot already must — never has to do the arithmetic, and a bot
that guesses gets an ordinary `Blocked`. This is the cheapest possible author
surface for a new verb.

**Resolution and ordering.** `invest` resolves in the action phase against the
bank as of tick start, so the mask a life was handed is honoured exactly. Two
teammates investing on the same tick against a bank that covers only one
resolve in canonical `(teamId, unitId, lifeId)` order; the second is `Blocked`.
That is the existing simultaneous-reservation grammar, not a new rule.

**Why an action at all, rather than an automatic ladder.** An auto-buying bank
removes the only decision the spend side has — *which* branch, and *when*
relative to what you have just seen the enemy buy. The reactive purchase is the
RTS aspect the owner asked for; a threshold that fires by itself is a passive
modifier with a progress bar. The registered control arm in §12.5 makes this
claim falsifiable rather than asserted.

---

## 9. Class and skill interactions

**Striker — the lane is its terrain, but the salvo is not its tool there.**
Rows 1 and 13 are 21-tile straight corridors, the longest clean sightlines on
the map, and the striker owns the longest gun (8, or 10 at `edge` tier 2). It
is the natural interceptor of loaded carriers. But the *fan does not work in
the lane*: a striker at `(x,1)` facing east fires along E, NE, SE, and the NE
bolt leaves the map into row 0 on its first advance and dies. Row 2 is wall at
x ∈ {3,4,10,12,18,19}, so the SE bolt dies immediately at those columns too.
**The fan delivers 2 of 3 bolts at best in the north lane and 1 of 3 at six
columns of it** — so "the salvo executes harvesters" is false, and the answer to
that question is a real finding rather than a guess. Combined with the volley
entry's 8-tick route cooldown, the striker's lane weapon is its ordinary mobile
gun down a straight corridor, and its fan stays a front-line tool. That is a
good separation: the class gets a new job that does not cannibalise the skill
just priced in #184.

**Bulwark — the shell is an escort tool and a directional door, not a
fortress.** An AEGIS SHELL parked in a lane deflects every bolt arriving in its
facing quadrant, and a 1-tall corridor has exactly two sides, so a lane shell
seals one direction absolutely. Its published counter-play — "the shell's arc
never tracks you, so going around it always works" — survives, because row 2 is
open at 15 of 21 columns and an attacker can drop out of the lane and shoot
diagonally, and because approaching from the shell's rear along the lane is a
normal rear contact. So a lane shell is a **door** the enemy must pay to open,
which is a genuinely good escort mechanic and not the unflankable holder the
alcoves would have been. The bulwark is also the slowest harvester in effect:
5 HP survives the walk, but a 3-cooldown gun means it wins fewer of the
interception duels it will be dragged into.

**Bulwark turrets are excluded from the economy by L1.** Objective weight 0
means no pickup, no carry, and a load dropped on anchoring. Without that rule a
turret on `(11,1)` is a permanent denial engine that also banks the assay every
cycle for nothing; with it, a turret on a vein site denies the tile at the cost
of a body forever, and the displacement rule (§4.2) moves the vein anyway.

**Fabricator — more bodies is acceptable, because the pot is fixed.** The vein
supply is 36 scrap whatever the topology, and one dedicated harvester already
services the whole cycle (§4.5). Extra bodies therefore buy **contest security**
— you can afford a harvester *and* an escort *and* a front — not extra income.
Three further dampers land on the same class without being aimed at it:
Prime-only upgrade scope means a five-slot team buys exactly one upgraded body,
same as a three-slot team; the fabricator dies most, so it is the largest
supplier of wreckage to its opponent; and `plate`, the one corrective track,
helps its 2-HP prime most, which is the direction the current ladder (fabricator
at the floor: bvf +0.333, fvs −0.222) wants. Registered as
`scrap-fabricator-amplification` with a Prime-only-earning level pre-registered
in case the read disagrees.

**TeamRandom is the coordination story, and it has a real job here.** Two veins
spawn simultaneously in two lanes; a team must split without collision and
without being predictable. A deterministic tie-break ("lowest unit ID goes
north") is common knowledge to the *enemy* as well, so an interceptor can be
waiting. `TeamTickRandom` re-derives from (team root seed, tick) so every life
on the team draws identical values at the same decision point — the exact
property a shared plan needs — while the enemy, holding a different root seed,
cannot predict the draw. The idiom is one line at each spawn tick:
`bool iGoNorth = (context.TeamRandom.NextBool() == (slot == myHarvesterSlot))`.
This is the first mechanic in the game that *needs* the capability rather than
merely permitting it, and it is worth saying so: SCRAP is the reason TeamRandom
earns its place.

---

## 10. Watchability

The viewer gets a second stage and a clock. Two veins pulse into the dead lanes
on a public 60-tick metronome, so the audience learns the rhythm within two
cycles and starts watching the lane before the bodies do; the follow camera
(#175) already centres action and now has somewhere to cut to. A loaded carrier
reads at a glance — a body dragging a visible load down a long empty corridor is
the most legible tension the game has produced, because the audience can see
both the prize and the 16 ticks between it and safety. An interception spills a
bright pile onto the floor, which is a *reversal* rather than a kill, and
reversals are what replay galleries are made of. The bank race is two numbers on
the scoreboard climbing against a public price. And the purchase is a beat: the
buying team's prime visibly changes — longer bolts, a wider sight cone, a
tougher chassis — on a tick both teams saw coming, which is the difference
between a buff and a surprise.

---

## 11. Failure modes, each with the wave-read symptom that proves it

| # | failure | observable symptom in a wave read |
|---|---|---|
| 1 | **Lane camping** — bodies park in rows 1/13 waiting for spawns rather than harvesting between them | a unit slot spends >40% of its live ticks in rows 1 or 13 while banking <1 scrap per 60 ticks; mean distance-from-active-objective per body rises against the swell baseline |
| 2 | **Front starvation** — the economy pulls both teams off the objective | objective-tile occupancy ticks fall, capture completions per match drop below the keel baseline, cap share climbs back toward the #168 plateau (0.24 → 0.4+), draws return from ~0 |
| 3 | **Economy ignored entirely** — nobody writes the code path | median `invest` casts per match < 1, or end-of-match bank > 8 in more than half of matches (income earned and never spent). The turret's 0.13% usage is the precedent for what this looks like |
| 4 | **Upgrade dominance re-opens the triangle** | any class-pair edge leaves [0.15, 0.40] on the scrap arm while the scrap-off control stays in band. Watch **bvf and fvs** (plate compresses toward the fabricator) and **bvs** (optic's omni-area tilt favours the bulwark) |
| 5 | **The economy beats the pendulum** | the full-economy-vs-ignore edge exceeds 0.40, or matches are decided by tier vector rather than by front position — measurable as territorial progress at end correlating more strongly with total tiers bought than with objective occupancy |
| 6 | **SCRAP deletes the fog** | carrier interception rate > 60% (perfect information about loads and positions makes ambushes free); doctrine that conditions on observed enemies stops outperforming doctrine that conditions only on published mode state |
| 7 | **The single-hoover degenerate** — one body silently takes everything | pickup share by a single unit slot > 80% and the economy edge sits at the ceiling in > 30% of matches, with interception attempts near zero |

---

## 12. Implementation map

### 12.1 Does it reuse the in-flight secondary-control site/latch capability?

**Refuted, with one qualification.** `FrontlineSecondaryControlDefinition` is a
latch: a tile set, sole positive objective weight for N consecutive ticks, an
owner that survives until recaptured, and one continuous tagged effect. SCRAP
has **no latch, no owner, no claim, and no sole-presence rule**, and its sites
are *consumed* rather than *held* — a vein is gone the tick somebody steps on
it. The only genuinely shared thing is "the mode declares some tiles", which is
one line. SCRAP is a second independent typed capability
(`FrontlineScrapEconomyDefinition`) sitting beside the first, and the two are
mutually exclusive arms in v1 so that the factor space stays three-valued.

The qualification: SCRAP *does* reuse two smaller things the muster work built.
`FrontlineActorMatchModeDriver.WeightOn(world, tiles)` is exactly the pattern
pickup resolution needs — read post-damage active lives by position out of
`GenericActorModeWorldView` — and the whole "absent block means inert, canonical
writer emits no bytes" pattern on `FrontlineGameModeDefinition.SecondaryControl`
is copied verbatim for `ScrapEconomy`. Both are patterns, not code.

### 12.2 Sizing against existing machinery

| piece | size | notes |
|---|---|---|
| `FrontlineScrapEconomyDefinition` + `FrontlineLabsScrapEconomy` constants | **S** | pure data, mirrors `FrontlineSecondaryControlDefinition` / `FrontlineLabsMusterSite` line for line |
| Map / regions | **none** | veins are tile addresses in the **rules**, not map regions, so the map fingerprint does not move and the arm stays comparable to every `frontline-labs-01` result. Banking reuses the existing `team-0-home-pad` / `team-1-home-pad` regions |
| Kernel arithmetic (spawn, decay, merge, displacement, assay, deposit, purchase, caps) | **M** | one new state record beside `FrontlineControlState`; all pure functions, all trivially unit-testable |
| Pickup/deposit resolution in the joint tick | **M** | new phase after damage and before the mode objective update, so a body destroyed this tick collects nothing; canonical actor order for simultaneous pickups |
| Mode-driver seam changes | **M, and the riskiest item** | see §12.3 |
| Observation + wire codec + SDK + Guest + `ArenaBasics` + web/mobile mirrors | **L** | the #169 sweep: three facts across roughly thirty-five files |
| `invest` action end to end (kind, parameter kind, payload, argument, mask, resolution, replay decision, validators) | **L** | the only genuinely new player verb since Split |
| Replay v3 + chronology validation | **M** | mode state fields into `ReplayV3` / serializer / projection / TS validator, plus the conservation invariant in §12.4 |
| Viewer presentation | **M** | pile glyphs, carried-load indicator, bank/tier panel, purchase beat |
| CLI arm, identity tokens, guards | **S** | mirrors `--side-objective` exactly |

**Total: L.** It should be run as its own schema window with an SDK/CLI version
bump, and it must not land in a wave that is pricing anything else.

### 12.3 The one architectural seam change

Three internal (non-public) seams move, and one of them is a genuine widening
of the mode-driver contract:

1. `GenericActorModeTickInput` gains `destructions[] { actorId, teamId,
   position }`. It carries `damageContacts` today; wreck placement needs the
   destruction itself, not the contact. Additive, internal type.
2. `GenericActorModeProjection` gains `carriedScrapByActor :
   ImmutableDictionary<ActorIdentity, int>` so the host can stamp loads onto
   self/ally/enemy states. Mode state that annotates *bodies* is new — today the
   driver owns only team-scoped facts — and this is where an implementation is
   most likely to get the canonical ordering or the fog rules subtly wrong.
3. `IGenericActorMatchModeDriver` gains an action surface:
   `IReadOnlyList<string> InvestableTracks(int teamId)` for the legality mask
   and `ActorActionRejectionResult? TryInvest(ActorIdentity actor, string
   trackId)` for resolution. **Today the mode driver has no action hook at all**
   — it only observes post-combat world state — so this is the real
   architectural addition and it should be reviewed as such. It is also the
   thing that makes future modes able to own verbs, which is a good direction.

### 12.4 The invariant worth building the validator around

The economy is exactly conservative, so the chronology validator can *prove* a
replay's economy rather than spot-check it:

```text
Σ spawned  =  Σ banked + Σ carried + Σ on-piles + Σ evaporated
bank(t)    =  bank(t−1) + Σ assays(t) + Σ deposits(t) − Σ purchase costs(t)
tier(t)    =  tier(t−1) + purchases(t),  with tier ≤ maxTier and Σ tiers ≤ 3
```

This is the same class of check that rejects lapsed or unordered route-cooldown
clocks, and it makes an entire subsystem non-forgeable for the cost of one
accumulator. Build it first; it will catch every ordering bug in §12.3.

### 12.5 Arm surface

- **Flag: `--economy scrap`.** A new flag rather than a third
  `--side-objective` value, because SCRAP is not a side objective — it declares
  no site, no latch, no owner. **`--economy scrap` and `--side-objective muster`
  are mutually exclusive** and the CLI rejects the combination, so the factor
  space stays `{none, muster, scrap}` and no cell ever carries both.
- **It needs a cell**, like `--side-objective`: a class pair (explicit or
  manifest-declared) or a `--pendulum` level.
- **Never inert-omitted.** SCRAP changes the game for every class pair whatever
  is in the cell, exactly as MUSTER does and unlike `--volley salvo`. One flag
  set still serves a whole wave.
- **Identity tokens.** The composite budget is 64 characters and the worst cell
  (`fabricator-vs-fabricator` beside `facing-locked`) leaves eight for the arm
  token, so the candidate game plus a spelled-out factor overflows exactly as it
  did for muster. Three registered composites, all within budget:
  `swell` + scrap = **`forge`**, `tide` + scrap = **`anvil`**, and
  `sail-tick-open` + scrap = **`smelter`**. Everything smaller spells its
  factors and appends `scrap`.
- **Map identity does not move.** `MapIdFor(...)` is untouched — this arm adds
  no map generation, which is the concrete improvement over MUSTER and is worth
  protecting in review.

### 12.6 Pre-registration factors for `balance/frontline-ablation-debt-v1.json`

Nine registrations. The first six are bundles that must be split before any
measured effect is attributed; the last three are the levels that make the
design's central claims falsifiable.

| id | bundled interpretation | required isolation |
|---|---|---|
| `scrap-vein-schedule` | site addresses, first tick 120, cadence 60, last tick 420, amount 3, and the two-veins-per-event choice are one factor | vary cadence and amount separately at fixed sites; run a one-vein-alternating level; report banked-scrap-per-team and errand round-trip cost per variant |
| `scrap-carry-model` | assay 1, carry cap 4, home-pad banking, and drop-on-death are four levers in one rule | run pure-instant and pure-carry as separately fingerprinted levels; vary assay at fixed capacity; report interception rate and scrap-lost-to-decay |
| `scrap-wreckage-yield` | wreck = 1 simultaneously prices kills, feeds the ignore-path, and dampens the fabricator | run wreck = 0 as a level; report the share of each team's bank arriving through wreck vs vein, and capture completions per match |
| `scrap-upgrade-ladder` | three tracks, 8/16 costs, 2-per-track and 3-total caps are one bundle | vary costs at fixed caps; run each track alone as its own level; report tier-vector distributions and time-to-first-purchase |
| `scrap-upgrade-scope` | Prime-only application is simultaneously the L5 mitigation and a power level | run an all-bodies level at fixed ladder; read the class edges, not only the pacing gates |
| `scrap-pile-decay` | 60 ticks simultaneously bounds the observation, damps stockpiling, and sets the recovery window | vary decay alone; report scrap-evaporated per match |
| `scrap-observation-completeness` | publishing every pile leaks enemy deaths the perception union hid | run the fog-limited level (`ObservedTile.ScrapAmount`) and compare interception rate and doctrine sensitivity to observed enemies |
| `scrap-invest-caster-scope` | any-body / anywhere / free is one permissive bundle | run Prime-only and at-a-forge levels separately |
| **`scrap-flat-control-arm`** | **the designated control.** Same veins, same carrying, same wreckage, same ladder — but the bank auto-buys a fixed pre-declared tier order with no `invest` action at all | if `scrap-flat` measures the same as `scrap` on both the balance edges and the pacing gates, **the allocation decision is inert and the entire new action family is unjustified**. This is the design's central claim made falsifiable, and it is the one registration that could kill §8.5 outright |

The standing instruction from the muster registrations applies unchanged: read
the class edges, not only the pacing gates. An economy that improves cap share
while walking bulwark-vs-striker out the bottom of the band is a failure with
good-looking numbers.

---

## 13. Comparison with the two runners-up

**FOUNDRY — spend income on what respawns ARE (timers, chassis variants, a
scout unit).** FOUNDRY is the better *fantasy*: a chassis variant is felt far
more than +1 tile of range, and "the striker you kill comes back as something
else" is a stronger viewer beat than any number SCRAP can move. It loses on two
hard points. First, its headline currency is respawn **timers**, which is body
count in disguise and therefore violates the owner's own constraint that body
count stays the fabricator's monopoly — strip the timers and half of FOUNDRY is
gone. Second, chassis variants are *forms*, and forms are contract data: every
variant multiplies the resolved form catalog, the `allowedFormIds` masks, and
the fingerprint surface, which is the 324-form problem §5.1 exists to avoid.
SCRAP's modifier model gets ~70% of the felt effect for ~10% of the contract.
**One piece should ride along:** FOUNDRY's scout is the right *framing* for
`optic` tier 2 — do not add a scout body, but present the tier as "your prime
becomes the scout", because a track whose payoff is legible gets bought and a
track called "+1 vision" does not.

**DOCTRINE TREE — combat fills a meter; teams choose telegraphed branches; no
map content.** It is strictly the cheapest thing on the table: no piles, no
carrying, no new mode entities, no map involvement, and it cannot be camped
because there is nowhere to camp. If the owner's constraint were budget rather
than fun, it would win. It loses on the directive that actually binds: **it does
nothing whatsoever for the side lanes**, which was stated as a requirement and
not a preference. It also has an anti-snowball problem with no geometric fix —
"combat fills a meter" means the team winning fights fills its meter faster and
there is no map position from which the loser can contest the income, which is
exactly the loop SCRAP dampers with pile decay and carrier interception. **One
piece rides along and already has:** DOCTRINE TREE's telegraph discipline —
every branch public, every purchase visible to both sides the tick it happens —
is adopted wholesale in §5.5, and it costs nothing because `ModeChanged` was
going to fire anyway.

---

## 14. Build-ready v1 specification

Everything below is a decision, not a suggestion. An engineering agent should
be able to implement this without a design call.

### 14.1 Constants — `FrontlineLabsScrapEconomy`

```csharp
VeinSites            = [(11, 1), (11, 13)]   // declared order: north, south
VeinFirstSpawnTick   = 120
VeinSpawnInterval    = 60                    // spawns at 120,180,240,300,360,420
VeinLastSpawnTick    = 420
VeinAmount           = 3
WreckAmount          = 1
AssayAmount          = 1
CarryCapacity        = 4
PileLifetimeTicks    = 60
MaxSimultaneousPiles = 16
BankRegionIds        = ["team-0-home-pad", "team-1-home-pad"]
UpgradeScope         = PrimeSlotLivesOnly
MaxTotalTiers        = 3
ArmToken             = "scrap"
```

Tracks, in declared order (`trackId`, effect kind, per-tier magnitude, max
tier, tier costs):

```csharp
("edge",  MobileAttackTravelTilesDelta, +1, maxTier: 2, costs: [8, 16])
("plate", SpawnMaxHealthDelta,          +1, maxTier: 2, costs: [8, 16])
("optic", VisionRangeDelta,             +1, maxTier: 2, costs: [8, 16])
```

### 14.2 Rules, exactly

1. **Vein spawn.** On each tick in `{120,180,240,300,360,420}`, for each vein
   site in declared order: if the site tile holds a live body, the vein appears
   on the nearest free floor tile in the same row, scanning by ascending `|dx|`
   then ascending `x`; otherwise on the site tile. Amount `3`, expiry
   `tick + 60`. If a pile already occupies the target tile, amounts merge and
   the expiry becomes the later of the two.
2. **Wreck.** Every destruction of a live body creates or merges a pile at the
   death tile with amount `1 + the destroyed body's carried load`, expiry
   `tick + 60`.
3. **Decay.** A pile is removed the first tick where `tick >= expiresAtTick`.
   If a 17th pile would exist, the oldest (then lowest `y`, then lowest `x`) is
   removed first.
4. **Pickup.** Resolved after damage and destruction finalisation, before the
   mode objective update, in canonical `(teamId, unitId, lifeId)` order. A live
   body whose form has **objective weight > 0** standing on a pile: banks
   `min(1, pileAmount)` for its team immediately; then loads
   `min(pileAmount − banked, carryCapacity − currentCarry)`; the remainder stays
   on the tile with its original expiry. Forms with objective weight 0 do not
   pick up.
5. **Deposit.** A live body standing on its own team's `bankRegionId` tiles
   banks its entire carried load; carry becomes 0. Automatic, no action cost.
6. **Weight-zero drop.** Completing a transition into a form with objective
   weight 0 drops the whole carried load as a pile at the body's tile, expiry
   `tick + 60`.
7. **Invest.** `invest` with a `upgradeTrackId` argument. Legal when the track
   is in the tick's `AllowedTrackIds`, i.e. the team's tick-start bank ≥ the
   track's next tier cost, that track's level < 2, and the team's total tiers
   < 3. On success: bank decreases by the cost, the track's level increases by
   1, and the mode state publishes a `ModeChanged` fact carrying the complete
   post-change state. Simultaneous same-team invests resolve in canonical order;
   later ones that can no longer afford it are `Blocked`.
8. **Application.** For every life of the team's **Prime unit slot** (live and
   future), for every form that slot occupies: effective gun travel tiles =
   declared + `edge` level; effective vision range = declared + `optic` level;
   effective max health = declared + `plate` level. **Max-health increases never
   heal**: current health is unchanged by a purchase. All three apply from the
   tick after the purchase resolves.
9. **Nothing else moves.** No change to capture, decay, redeploy, ratchet,
   respawn ticks, rebuild delays, slot counts, unlock schedules, fabrication,
   damage per hit, projectile speed, movement speed, or objective weight.

### 14.3 New contract facts

The `rules.gameMode.scrapEconomy` block of §8.3, in full, absent when the arm is
off.

### 14.4 New observation facts — exactly three

```text
ModeObservationState.Frontline
  + scrapTeams : ImmutableArray<ScrapTeamState>   // ordered by teamId; empty when inert
        ScrapTeamState(int TeamId, int Bank, ImmutableArray<int> TierLevels)
  + scrapPiles : ImmutableArray<ScrapPile>        // ordered by (y, x); empty when inert
        ScrapPile(Position Position, int Amount, int ExpiresAtTick)

ObservedSelfState  + CarriedScrap : int           // 0 when inert
ObservedAllyState  + CarriedScrap : int
ObservedEnemyState + CarriedScrap : int
```

`TierLevels` is ordered by the contract's declared track order. `ExpiresAtTick`
uses the established clock grammar: the pile is gone the first tick
`tick >= expiresAtTick`. **No new event kinds** — every state change publishes
the existing `ModeChanged` fact carrying the post-change state.

### 14.5 New action facts

```text
ActorActionKind.ModeInvestment          = 7      // additive append
ActorActionParameterKind.UpgradeTrack   = 5      // additive append
PublicActionCodes.Invest                = 106
action id                               = "invest"
ActorActionPayload.UpgradeTrackId       : string?
GenericActorRuntimeActionArgument.UpgradeTrackArgument(string TrackId)
ArgumentConstraint.UpgradeTrackConstraint(ImmutableArray<string> AllowedTrackIds)
canonical id strings: "mode-investment", "upgrade-track"
```

### 14.6 Arm surface

```bash
nilbots experiment frontline-labs \
  --bot <generic-spec> --opponent <generic-spec> \
  --classes bulwark-vs-striker --pendulum keel --skills kit \
  --bend universal --economy scrap \
  --seed 42 --runtime wasm --out /tmp/scrap
```

- `--economy` values: `none` (default, writes no bytes) | `scrap`.
- Requires a cell (class pair or `--pendulum` level), like `--side-objective`.
- **Rejects** `--side-objective muster` in the same invocation.
- Never inert-omitted.
- Registered composite identities: `swell`+scrap = **`forge`**, `tide`+scrap =
  **`anvil`**, `sail-tick-open`+scrap = **`smelter`**; smaller cells spell their
  factors and append `scrap`.
- Map identity unchanged: the arm runs on the existing `frontline-labs-01`
  family, so it is fingerprint-comparable to every arm measured to date.

### 14.7 Surfaces that must agree before this is done

Engine (`FrontlineScrapEconomyDefinition`, `FrontlineModeKernel`,
`FrontlineActorMatchModeDriver`, `FrontlineControlState`,
`FrontlineControlProjection`, `ActorRulesCanonicalWriter`,
`ActorContractCanonicalIds`, `ActorResolvedMatchDefinitionValidator`,
`GenericActorMatchSession`), observation (`GenericActorRuntimeObservation`),
codecs (`GenericActorWireObservationCodec`, `GenericActorWireContractCodec`),
SDK (`GenericActorContext`, `GenericActorRulesContract`,
`ActorCanonicalContractReader`, doc-comments naming the inert defaults),
mappers (`GenericActorSdkModelMapper`), Guest, `BotProject`, the `ArenaBasics`
template, replay (`ReplayV3`, `ReplayV3Serializer`, `ReplayV3Projection`,
`GenericFrontlineChronologyEvidence`), CLI (`FrontlineLabsExperimentCommand`,
`Program`, help text, `replay --summary`), `web/src/types.ts` plus the viewer's
Frontline panel, `mobile/src/components/arena/protocol.ts`, this document, the
class brief, and `balance/frontline-ablation-debt-v1.json`. `DocDriftTests`
pins the mechanical mirrors; prose accuracy is on the author. CLI and SDK
versions bump, and `publish-cli` runs before any deploy.
