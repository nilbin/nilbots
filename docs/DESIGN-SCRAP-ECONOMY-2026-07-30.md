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

---
---

# Part 2 — Strategy lanes

Commissioned after the owner read Part 1: *"You should up front figure out the
strategy lanes before we decide. And ensure enough depth."* Four questions had
to be settled before a build decision, and mid-analysis the owner supplied his
own candidate for the third one plus two riders. This part answers all of it,
and **it changes Part 1's verdict in one important place**: as specced, SCRAP
v1 measures null, because harvesting is strictly dominated. §P2.1 proves that
with the contract's own capture arithmetic, and §P2.4 is the fix.

Everything numeric here is read from the resolved contract rather than
estimated: `FrontlineLabsDefinition.BuildRules` declares **threshold 15, gain 1
per sole team tick, decay 1 every 2 ticks, redeploy pause 5, five positions,
three pushes to breach, maxTicks 500**; the keel level sets
`controlPolicy = NetPositiveObjectiveWeightDifferenceScalesGain…`
(contest-majority), `decayClock = EmptyAndContestedTicksPreserveClaimEnemySole…`
and `ratchetHoldTicks = 40`. Body schedules are
`FrontlineLabsClassDefinition`: **striker/bulwark 1 body at tick 0, 2 at 120, 3
at 260; fabricator 1 / 2 at 60 / 3 at 180 (+ a fourth at 300 under `wane`)**.
Prime respawn is 18; child rebuild 30 (striker/bulwark), 15 native / 22 under
`wane` (fabricator).

---

## P2.1 The dominance proof — why Part 1 as specced measures null

### The capture arithmetic, exactly

Under contest-majority the controlling team is the one with strictly greater
objective weight on the active objective, and the gain multiplier is the
**difference**. Gain per sole team tick is 1. Threshold is 15.

| bodies on the point | net weight | gain/tick | ticks to capture |
|---|---:|---:|---:|
| 3 v 0 | 3 | 3 | 5 |
| 3 v 1 | 2 | 2 | 8 |
| **3 v 2** | **1** | **1** | **15** |
| 2 v 2 / 3 v 3 | 0 | — | never (claim preserved, no decay under keel) |

And under keel's decay clock, **empty and contested ticks preserve the claim**;
only sole enemy superiority erodes it. So a 2-versus-3 deficit does not merely
slow the attacker — the attacker's 15 ticks **accumulate**, because the two
defenders can never push the number back down. They can only stall it, and they
cannot even stall it while outnumbered.

### What one errand costs

Add the redeploy pause and the walk to the next objective (measured: adjacent
objectives are 7–10 facing-locked ticks apart), and one capture costs the
attacker **15 + 5 + ~8 = 28 ticks of sustained +1 superiority**.

A home-banking harvest round trip is **32 ticks** (§4.5). An instant-pickup
round trip from the front is **28**. Either way:

> **Sending one of three bodies to a vein concedes almost exactly one capture.**

### The payoff matrix

Two teams, 3 bodies each, one 60-tick vein window, symmetric:

| | B stays | B harvests |
|---|---|---|
| **A stays** | stall, 0 scrap each | A **+1 capture**, B +6 scrap |
| **A harvests** | A +6 scrap, B **+1 capture** | stall, ~5 scrap each |

A capture is one third of a breach and a 15-point territorial swing. Six scrap
is three fifths of one tier out of a three-tier ceiling. Against B-stays, A's
best reply is stay (0 beats −1 capture +6 scrap). Against B-harvests, A's best
reply is stay (+1 capture beats +5 scrap). **Stay strictly dominates. (Stay,
Stay) is the unique equilibrium and nobody ever harvests.** That is failure
mode #3 from §11, proved rather than feared, and it is the owner's worry stated
exactly: *breach rush wins.*

Published pile state does not rescue it. Full observability makes the deviation
detectable within a tick, but the "punishment" for the enemy harvesting is that
you capture — which is the reward, not a punishment. There is no repeated-game
fix.

### The windows that do exist, and why they are not enough on their own

- **Your own ratchet hold (40 ticks).** After you capture, an enemy capture
  inside your hold is *spent* — the claim resets and the front does not move
  (`IsDeniedByRatchetHold`). A 28–32-tick errand fits inside 40 with 8–12 ticks
  to spare, so during your own hold the errand is genuinely free. This is real,
  it is published (`holdOwnerTeamId` / `holdEndsAtTick`), and it is the mechanic
  #169 shipped that nobody has written a bot to exploit.
- **The enemy is down a body.** A kill buys 18 ticks (prime) or 22–30 (child)
  of parity.

Both windows are **earned by winning the front**, so on their own they make the
economy a pure rich-get-richer amplifier: only the team already winning can
afford to farm. That is not depth; it is a victory tax.

**Conclusion: the economy cannot be fixed by tuning the economy. The front has
to become a place where a smaller force can hold.** That is question 3, and it
is a prerequisite rather than a polish item.

---

## P2.2 Significance math — is each tier actually FELT?

A safe upgrade that changes no breakpoint would make the whole arm measure
null just as surely as a dominated errand. Weapon damage in the contract:
**every mobile gun and every turret gun deals 1**; **salvo fan bolts deal 2**,
and a diverging fan lands at most one bolt per body.

### PLATE — bolts-to-kill (the prime, the upgraded body)

| target | HP → T1 → T2 | vs damage-1 gun | vs damage-2 fan bolt |
|---|---|---|---|
| striker prime | 3 → 4 → 5 | 3 → **4** → **5** | 2 → 2 → **3** |
| bulwark prime | 5 → 6 → 7 | 5 → **6** → **7** | 3 → 3 → **4** |
| fabricator prime | 2 → 3 → 4 | 2 → **3** → **4** | 1 → **2** → 2 |

Every cell in bold is a breakpoint crossed.

- **Against damage-1 guns — which is every gun in the game except the fan —
  every tier of PLATE moves the bolt count for every class.** At cooldown 2
  that is +2 ticks of time-to-kill per tier; at cooldown 3, +3. Felt.
- **Against the fan, PLATE T1 is inert for the striker and bulwark** (2 bolts
  before, 2 after; 3 before, 3 after) — the coordinator's flag is correct — but
  **T2 moves both** (2→3, 3→4).
- **PLATE T1 is the single largest breakpoint in the design for the
  fabricator**: its 2-HP prime currently dies to **one** fan bolt, and T1 makes
  it two. One-shot protection on the class sitting at the bottom of the ladder.

Verdict: **both tiers pass.** No re-spec. The tier-by-weapon asymmetry is a
feature — it is why the fabricator buys PLATE first against a striker and the
striker does not buy PLATE first against a bulwark.

### EDGE — range breakpoints are matchup-conditional, and that is the point

Base gun travel: striker 8, fabricator 7, bulwark 6.

| buyer | T1 (+1) | T2 (+2) | the breakpoint it crosses |
|---|---|---|---|
| striker | 9 | 10 | **T1: out-ranges a mirror striker by 1** — the only strictly-safe firing position that exists against your own class. **T2: covers all 21 columns of a side lane from its centre**, so no carrier can enter the lane out of reach |
| fabricator | 8 | 9 | **T1: matches the striker's 8** — the fabricator can finally answer at the striker's maximum range. Corrective on the leg where the striker leads (fvs −0.222) |
| bulwark | 7 | 8 | **T1: out-ranges a mirror bulwark. T2: erases the striker's entire 2-tile standoff**, the bulwark's sharpest counter-buy |

Free-shot arithmetic: closing 2 tiles under facing-locked costs 2–4 ticks
(steps plus rotations), which at cooldown 2 is 1–2 uncontested shots — **one
third to one half of a kill, free, per engagement** at T2.

Verdict: **both tiers pass**, though T1's value swings hard by matchup
(decisive in mirrors, corrective in fabricator-vs-striker, marginal in
bulwark-vs-striker). A track whose value depends on who you are fighting is
exactly what a *choosable* track should look like; averaged over matchups it
would have looked weak, which is why the table is per-matchup.

### OPTIC — against the actual vision shapes

Striker and fabricator see a facing quadrant; the bulwark sees an
omnidirectional disc at range 4 with proximity 4. The breakpoint that matters
is **can I see the thing that can shoot me**:

| buyer | vision → T1 → T2 | own gun | shot-from-unseen gap → T1 → T2 |
|---|---|---|---|
| striker | 6 → 7 → 8 | 8 | vs enemy striker: 2 → 1 → **0** |
| bulwark | 4 → 5 → 6 | 6 | vs enemy bulwark: 2 → 1 → **0**; also reaches its own gun's range at T2 |
| fabricator | 6 → 7 → 8 | 7 | reaches own gun at **T1**; vs enemy striker 2 → 1 → **0** at T2 |

**Every class closes its see-versus-shoot gap to zero at OPTIC T2, and the
striker and bulwark have the identical 2-tile gap**, so the ladder terminates
at parity for everyone — which is also why max tier 2 is right and a third tier
would be worthless by construction.

The area caveat from §5.2 stands and is quantified: an omni disc at 4→6 grows
observed area 16→36 (+125%) against a quadrant's 36→64 (+78%), so on an area
reading OPTIC tilts toward the bulwark; on the acquisition reading it is
identical for striker and bulwark. Registered, not resolved.

Second breakpoint, specific to this arm: a carrier crossing the middle is
exposed for 22 ticks, and a quadrant watcher at range 6 nets 6 tiles of the
crossing against 8 at T2 — **a third wider interdiction net**, which is the
courier-hunting archetype's enabling stat.

Verdict: **both tiers pass.** No re-spec anywhere in the ladder.

### Ladder simplification that falls out of the above

Because tier 2's *effect* is naturally diminishing on two of three tracks and
inert-against-one-weapon on the third, escalating prices are redundant. Part 1's
8/16 is replaced by **a flat 10 per tier**, which makes deep (2 in one track)
and broad (1 in each of three) cost the same 30 at every point in the match,
removes a tuning knob, and makes volume discounts structurally impossible.

Revised economy constants (superseding §14.1 where they differ):

| constant | Part 1 | **revised** | why |
|---|---:|---:|---|
| vein amount | 3 | **6** | the errand must be worth its risk once §P2.4 makes it affordable |
| wreck amount | 1 | 1 | unchanged — the free front-play channel |
| assay | 1 | 1 | unchanged |
| carry capacity | 4 | **6** | one full vein's remainder plus a wreck |
| vein cadence | 60 (6 events, 120–420) | **80 (4 events: 120, 200, 280, 360)** | one harvester services both veins of a cycle in 64 of the 80 ticks without a greedy double-run; total vein supply drops 36 → 48 while per-trip value doubles |
| pile lifetime | 60 | **80** | still exactly one cadence, so at most two vein piles exist at once |
| tier cost | 8 / 16 | **10 flat** | deep = broad = 30 at all times |

Resulting timeline and ceiling:

| archetype | scrap by end | tiers | first tier at |
|---|---:|---:|---|
| front-only (wreckage) | ~10 | **1** | ~tick 300 |
| balanced (wreck + 2 veins) | ~22 | **2** | ~tick 200 |
| economy-committed (wreck + 6 veins) | ~46, capped | **3** | ~tick 170, capped ~tick 300 |

**Ignore-to-commit delta: 2 tiers = 2 integer stat steps on one body.** §7's
claim survives the re-tune.

---

## P2.3 The 2v3 problem, and the mechanisms evaluated

The requirement, stated as a number: **two defenders must be able to prevent
three attackers from converting a 28–32-tick presence deficit into a capture**,
without making captures impossible when a fight has actually been won.

Four candidates were evaluated. The owner's is the last and it wins.

**(a) Raise the capture threshold while the arm is on.** To make a 32-tick
errand yield zero captures needs threshold ≈ 34, which more than doubles the
game's pace-setting number, halves the meaning of every measured pacing gate,
and does nothing about the underlying asymmetry (3v1 still resolves in 17
ticks). Rejected: blunt, global, and it re-prices the pendulum without adding a
decision.

**(b) Cap the pressure multiplier at 1.** Inert for this problem — 3v2 is
already multiplier 1. Rejected.

**(c) OBJECTIVE KEYSTONE — the granular-tile direction (#176).** One tile per
objective region grants **+1 objective weight** to whoever stands on it. Derived
by rule rather than authored: *the region tile with the most adjacent walls,
ties broken by smallest Chebyshev distance to the map centre (11,7), then
lowest y, then lowest x* — which yields **(4,8) / (7,6) / (11,8) / (15,6) /
(18,8)**, an exact mirror set (22−4=18, 22−7=15, 11 self-mirror), each with one
adjacent wall for cover, and none of them on the open row-7 corridor. Declared
in the rules, not the map, so the map fingerprint does not move.

Numbers: 2 defenders with the keystone = weight 3 versus 3 attackers = 3 →
**stall**, so the errand is free. Attackers who take the keystone = 4 versus 2 →
multiplier 2 → **8 ticks**, so losing the tile is catastrophic. At parity (3v3)
the keystone holder gains 1/tick, which converts a stalemate-prone front into a
decisive one — arguably a pacing improvement on its own, and the first new
capture-economics lever since #168 declared that surface exhausted.

Cost: one field on the capture definition, one branch in `WeightOn`, **zero new
observation facts** (the tiles are static contract data). **S/M.**

Why it loses anyway: it fixes the arithmetic without changing what the front
*is*. It produces a one-tile scramble, not a formation; it gives the AEGIS
SHELL a weight-2 fortress tile (the shell has weight 1 and would double it) on
the class already at the top of the ladder; and it does nothing for the game's
oldest complaint that kills do not convert. **Kept as the registered fallback
`objective-keystone`, to be reached for if (d)'s wave read shows stalls.**

**(d) CAPTURE CHANNEL — the owner's candidate. Adopted.** Full analysis in §P2.4.

---

## P2.4 The capture channel

Owner's words: *"If capturing made you forced to stand still and any damage
taken instantly reverted the progress it would be easy to hold/poke with 1-2
bots — which would force the breacher to hunt down the defenders first."*
Riders: *"It means the team has to defend the capturer"* (confirmed as the
intended pattern, not a side effect) and *"we would need to speed up capture"*.

This is a **capture-core change**. It re-prices every pacing gate, every class
edge, and the whole pendulum campaign from #158 onward. That is acceptable under
dump-then-tune, but it must be said out loud, and it is why the mechanism ships
as **its own registered arm (`--capture channel`) that composes with
`--economy scrap`, not as something buried inside the economy.**

### P2.4.1 The two rules, stated precisely

> **Stillness gates GAIN, not DENIAL.** A team's **claim weight** counts only
> its bodies on the active objective whose position at the end of this tick
> equals their position at the end of the previous tick. A team's **denial
> weight** counts all of its bodies on the active objective. Control resolves
> as today except that the controller's side of the comparison uses claim
> weight: the team whose claim weight strictly exceeds the opponent's denial
> weight controls, with multiplier = the difference.
>
> **Damage on the point reverts the claim, point for point.** Hostile damage
> taken by a body of the **claiming** team that is standing **on the active
> objective region** reduces the claim by the damage amount, floored at zero,
> clearing the claimant at zero — exactly the existing opposing-erosion rule.

Four consequences worth pinning:

- **Stillness is defined by position, not by decision.** A body that requested
  a move and was Blocked did not move. A body that rotated, shot, or started a
  transform did not move. A life created this tick has no previous position and
  counts as stationary. This is unforgeable, replay-derivable, and it means a
  channeler can still aim and fight — the mechanic is not sit-still-and-pray.
- **Damage to a body OFF the objective reverts nothing.** This single scoping
  choice is what makes the escort pattern the intended play: **a screen absorbs
  bolts for free.**
- **Denial does not require stillness**, so a defender may kite inside the
  objective region and still subtract. Both teams run the same rules; the
  asymmetry is between the two *jobs*, not between the two teams.
- **In the everyone-stands-still limit the arithmetic is byte-identical to
  today's**, which is the compatibility story: the arm is a strict refinement,
  not a replacement.

### P2.4.2 Stall risk, quantified

The danger is real and has to be modelled: a stationary body cannot dodge, and
`visibleProjectiles` publishes `tilesPerAdvance`, `ticksPerAdvance`,
`ticksUntilAdvance` and `damagePerHit`, so an exact arrival tick exists and
`ArenaBasics.Threat` already computes it. **Against a stationary target, a
competent poker's hit rate is near 1** subject only to range, heading legality,
and cooldown.

Poke rates against a channel gaining 1/tick:

| interrupt variant | one striker poker (cd 2) | two pokers | one turret (cd 1) |
|---|---|---|---|
| **full reset on any damage** | progress never exceeds 2 → **permanent denial** | denial | denial |
| **pause M ticks after a hit**, M ≥ cooldown | gain suspended permanently → **denial** | denial | denial |
| pause M=1 | gain on 1 tick in 2 → half rate | stall | denial |
| **revert = damage dealt** (adopted) | −0.5/tick vs +1 → **half rate** | −1.0 → stall | −1.0 → stall |

**Full reset is rejected with a number: one poker with a clear heading denies
the objective for the whole match**, which is the pre-keel mean-reversion
problem returned in a worse form. **Pause-for-M is rejected** because it denies
whenever M ≥ the poker's cooldown and is nearly inert below it, and because
"progress paused" is less legible than "progress lost".

**Revert = damage dealt** is adopted. It needs no new number (the damage amount
is already authoritative and already published per projectile), it scales
naturally with the weapon — a salvo fan bolt reverts 2, a turret bolt 1 — and
it produces exactly the requested gradient: **poke delays, sustained
area control denies.**

Does it stall the match? The decisive cases, with the paired speed-up from
§P2.4.3 (threshold 8, gain 1):

| situation | claim vs denial | gain | revert | net | outcome |
|---|---|---:|---:|---:|---|
| 3 stationary attackers vs 2 kiting defenders on the point | 3 vs 2 | 1.0 | ~1.2 (2 guns at cd 2–3 on stationary targets) | **−0.2** | capture never completes; attackers take ~1.2 dmg/tick — **2 hold 3** |
| solo unscreened channeler vs 2 off-point pokers | 1 vs 0 | 1.0 | ~1.2 | **−0.2** | fails, and a 3-HP body dies in ~3 ticks |
| solo **screened** channeler, screens blocking both live headings | 1 vs 0 | 1.0 | ~0 | **+1.0** | **completes in 8 ticks** |
| 3 stationary attackers, one defender left alive and kiting | 3 vs 1 | 2.0 | ~0.6 | **+1.4** | **6 ticks** — inside the 18-tick prime respawn |
| 3 stationary attackers, defenders wiped | 3 vs 0 | 3.0 | 0 | **+3.0** | **3 ticks** — the reward for a wipe |
| 3 v 3 all stationary | 3 vs 3 | — | — | — | stall, exactly as today |

**Captures follow won fights or won formations, and never follow perfect
peace.** That is the target property, met.

### P2.4.3 `channel-speed` — one paired factor, not two knobs

The rider is right that each channeling tick is now riskier and must pay more.
The paired setting is derived from the post-fight window, which is **18 ticks**
(prime automatic return) — the shortest window an attacker can rely on, and the
one every class shares:

> **Threshold 15 → 8. Gain per sole stationary team tick stays 1. The
> multiplier arithmetic is untouched.**

One number moves. It is registered as the single factor **`channel-speed`**
because threshold and gain are not separable claims about the same thing.

Why 8 and not 10 or 6:

- A **screened solo channeler** completes in 8 ticks; one bolt that gets through
  makes it 9. Both fit inside 18 with room for the approach. At threshold 10 a
  single leaked bolt pushes it to 11–12 and a second respawn wave arrives; at 6
  a screened channel completes before a defender can rotate to a firing heading
  at all, which deletes the poke counterplay.
- A **post-single-kill 3v1** completes in 6 ticks, a **post-wipe 3v0** in 3.
  Fast, watchable, and unambiguously earned.
- An **unscreened** channel completes never. The screened/unscreened gap is the
  owner's stated target property and threshold 8 is where it is widest relative
  to the respawn clock.

Side effect to record: `TerritorialProgress = advance × (index − centre) ×
threshold`, so dropping the threshold rescales the reported score channel by
8/15. Nothing about ranking changes; every historical number needs the scale
factor applied before comparison.

Decay is untouched: keel's clock still preserves the claim on empty and
contested ticks, and the damage revert is a **separate** erosion path that does
not consume or reset the decay counter.

### P2.4.4 Skill interactions

**Screening works, for free, because the collision model already does it.**
`projectilesStopOnFirstEnemyActor: true` means a screening body physically
absorbs a bolt aimed at its teammate, and
`AlliedProjectileContactKind.PassThrough` means the screen does **not** block
its own team's return fire. Escort formation is therefore an existing
mechanical behaviour that this arm gives a purpose to; it needs no new rule at
all.

**The AEGIS SHELL becomes the siege tool — and the numbers say depth, not
dominance.** A shell cannot move, so it is *always* stationary: a perfect
channeler. It deflects every bolt arriving in its facing quadrant, so frontal
pokes revert nothing and the poker eats its own bolt back. But the shield
**breaks on its third deflection** (`ShieldBreakBudget = 3`), and three frontal
pokes at cooldown 2 arrive over 6 ticks against an 8-tick channel: **a
solo-channelling shell breaks about two ticks short of completing.** It needs
one screen, or a second body, or a lull. And the arc is chosen on entry and
cannot rotate, so **two pokers on two different headings defeat it outright**.
Add that a shell has no gun, so a channelling shell contributes zero offence
and the escort must do all the fighting. That is a tight, earned, flankable
tool — flanking becomes mandatory, which is the shell's published counter-play
working as designed. Registered as `channel-shell-interaction` with a level in
which a guarding form contributes no claim weight, because this does hand the
class at the top of the ladder the best channeler.

**The turret finally has a job.** Objective weight 0 means it contributes
neither claim nor denial — the turret bargain, unchanged — but its gun is
cooldown 1, travel 8, eight absolute headings, damage 1, which is **−1 progress
per tick** against any channeler in line of sight. A single turret with a clear
heading **denies a solo channel outright** (gain 1, revert 1, net 0) and halves
a 2-body channel. Against measured turret usage of **0.13%**, this is the first
mechanic that makes fortifying a correct move, and it prices the bargain
honestly: you deny the point by permanently forfeiting your presence on it, and
the attacker's answer is to spend 7 bolts killing it.

**The salvo becomes the premium anti-channel weapon.** The fan is three lanes
of damage 2, and against *spread* stationary bodies it can land one bolt in each
lane on three different bodies — **up to 6 progress reverted by one cast**,
which at threshold 8 is three quarters of a capture erased. It is priced by the
8-tick entry route cooldown, so one cast per 9 ticks against an 8-tick channel:
**a single well-timed fan is a full denial, and a mistimed one is nothing.**
This is a large buff to the class #184 left in band but leaning on one
lineage's leg, and it is the interaction most likely to walk bulwark-vs-striker
out the bottom of the band. Registered as `channel-salvo-interrupt`; it is the
first thing to read in the wave-8a results.

**The hunt, timed.** A defender must be *on* the objective region to deny, and
those regions are 4 tiles (outer) or 6 (centre) — so **denial pins the defender
to a small box and makes it huntable.** Three hunters at cooldown 2–3 put ~1.5
bolts per tick into a box that small; a 3-HP striker dies in 2–4 ticks once
focused, a 5-HP bulwark in 4–7. **Two defenders take 6–16 ticks to clear**,
after which the channel takes 3–6. Total ~25–30 ticks per capture including the
redeploy pause and the walk, against today's ~28. **Pacing survives**; the
mechanism reshapes *what* a capture costs rather than how long it takes.

**The synergy loop, named.** Defenders that deny must stand on the objective;
hunted defenders therefore die on the objective; wreckage drops at the death
tile and is assayed in full with no transport; and the attacker is standing
still on that exact tile for the next 3–8 ticks anyway. **Kill the screen,
stand on the corpses, bank the scrap, take the point.** The channel and the
economy are not two mechanics stapled together — the channel is what makes
wreckage the front-play income stream, and wreckage is what pays for the plate
that keeps the next channeler alive.

**TeamRandom's second job.** Who channels and who screens is a common-knowledge
decision with no communication channel, and it must not be predictable: a
deterministic rule ("lowest unit ID channels") is common knowledge to the enemy
too, so the pokers know which body to pre-aim at. `TeamTickRandom` re-derives
from (team root seed, tick), so all three bodies draw the same value while the
enemy — holding a different root seed — cannot. Between this and the lane
assignment of §9, the capability now has two mechanics that need it rather than
merely permit it.

### P2.4.5 Composition and contract shape

- **contest-majority**: replaced, for this arm, by a new closed policy value.
  Surplus-weight scaling now applies to *stationary* claim weight against
  *total* denial weight. In the all-stationary limit the two policies agree
  exactly.
- **enemy-sole-decay (keel)**: unchanged and independent. The revert is a
  separate erosion path; it neither consumes nor resets `DecayTicksElapsed`.
- **ratchet hold (40)**: unchanged. A capture inside an enemy hold is still
  spent. Interaction to watch: captures now follow kills, so holds fire in
  bursts and 40 ticks may be long relative to the new cadence. Registered.
- **redeploy pause (5)**: unchanged.
- **`--economy scrap`**: composes, and is the reason to build both — see
  §P2.4.4's loop and §P2.6's sequencing.

```text
rules.gameMode.capture
    threshold : 8                                    // channel-speed factor
  + controlPolicy :
      StationaryClaimWeightVersusTotalDenialWeightScalesGain…   // new enum value
  + claimInterrupt                                   // whole block ABSENT = inert
      kind                 : DamageToClaimantOnObjectiveRevertsProgress
      revertPerDamagePoint : 1
      scope                : ClaimingTeamBodiesOnActiveObjectiveRegion
```

Additive discipline: the block is absent on every ruleset that does not declare
it, so the canonical writer emits no bytes and every historical fingerprint is
byte-exact; the policy is a new value in an existing closed enum, exactly the
#156 append shape; and `revertPerDamagePoint` / `scope` are single-valued closed
enums today so that a second interrupt shape is a value rather than a schema
change.

**Observation cost: zero new facts.** `captureProgress` already publishes the
claim every tick and a revert is simply that number going down. Damage to your
own bodies is always visible to you, so you always know why your own claim
moved; an enemy claim's movement is partial information exactly as it is today.
A capture-core change that costs nothing in the observation budget is a strong
argument for taking it before the economy.

**Identity tokens** (≤ 8 characters after the worst-case class pair and
`facing-locked`): `swell` + channel = **`siege`**; `swell` + channel + scrap =
**`bastion`**; `tide` + channel + scrap = **`redoubt`**; `swell` + scrap alone
stays **`forge`**. Smaller cells spell their factors and append `channel` /
`scrap`.

**Ablation-debt registrations** (additional to §12.6):

| id | bundled interpretation | required isolation |
|---|---|---|
| `channel-stillness-rule` | claim-weight-requires-stillness while denial does not is one asymmetry doing two jobs (it slows attackers AND lets defenders kite) | run a both-sides-stillness level and a denial-also-requires-stillness level; report attacker stationary-tick share and defender kite distance |
| `channel-interrupt-rule` | revert-equals-damage bundles the interrupt's existence, its magnitude, and its per-weapon scaling | run flat revert-1-per-hit and pause-1-tick levels; report progress-reverted per match and per weapon |
| `channel-speed` | threshold 8 with gain 1 is one paired claim about how much a risky tick must pay | vary threshold alone at fixed gain; report time-from-first-kill-to-capture against the 18-tick respawn window |
| `channel-shell-interaction` | a guarding form is a perfect channeler that also blanks frontal interrupts, on the class at the top of the ladder | run a level where guarding forms contribute no claim weight; also run `--stance-ground` strict, where the shell cannot rise on objective tiles at all; report flank-approach share |
| `channel-salvo-interrupt` | the fan reverts up to 6 across three bodies for one 9-tick cast | report progress reverted per fan cast and the bvs edge with and without the salvo in a channel cell |
| `channel-turret-denial` | a turret with line of sight zeroes a solo channel while forfeiting all objective weight | report turret raise rate and turret-covered objective-tick share; the 0.13% baseline is the comparison |
| `objective-keystone` | the registered ALTERNATIVE 2v3 mechanism (§P2.3c), +1 weight on one derived tile per region | run it alone against the channel and against the unmodified front; it is the fallback if the channel stalls |

---

## P2.5 Strategy lanes

Roles map cleanly onto the three tracks, which is a good sign that the ladder
and the front mechanic were designed against each other: **PLATE is the
channeler's and the screen's stat, EDGE is the poker's and the courier-hunter's,
OPTIC is the hunter's and the finder's.**

### The archetypes

**A — BREACH RUSH.** All three bodies at the front, permanently; 100% of
body-ticks on the objective chain. Per capture: hunt the defenders (6–16 ticks)
+ channel (3–8) + redeploy pause (5) + walk to the next objective (7–10) ≈
**25–40 ticks per capture given you win the fights**, so a clean breach lands
around tick 200–260 and only if it wins essentially every engagement. Income is
wreckage only, ~10 scrap, **1 tier**, typically PLATE (keep the hunters alive).

**B — ECON-SPLIT.** Two at the front, one on the lanes from ~tick 104. Harvester
cost **~396 body-ticks = 26% of a three-body team** (20% at four slots, 16% at
five). Front runs 2-versus-3, which under the channel nets **−0.2/tick for the
attacker** — it holds indefinitely while nobody dies. Bank: ~10 wreck + 4–6
veins ≈ 34–46 → **3 tiers, capped around tick 300**.

**C — DEFEND-AND-POKE.** Three bodies near the objective but deliberately *off*
it, on clear firing headings. Zero claim weight, zero denial weight, full
interrupt. Two pokers on two different headings hold a screened channel at
net ≈ 0. Income: wreckage only, and less of it (you are not standing on the
corpses), ~6–8 scrap, **0–1 tiers**. **This archetype does not exist without the
channel** — it is created by it.

**D — COURIER HUNT.** One body (or two in a window) patrolling rows 1 and 13.
Cost ~26% of team-ticks, same as B. Payoff per interception: up to **8 scrap**
(a 6-load carrier plus its own wreck) plus a kill plus the enemy's wasted 30
ticks. Striker-favoured: a 21-tile straight corridor is the best sightline on
the map, the fan is *worse* there (a fan fired along row 1 loses its northward
bolt into row 0 immediately, and its southward bolt at six of the twenty-one
columns where row 2 is wall — 2 of 3 bolts at best, 1 of 3 at those columns), so
the tool is the mobile gun. **EDGE T2 is the enabler**: range 10 from the lane
centre covers all 21 columns, so no carrier can enter out of reach.

**E — WRECKAGE FRONT.** Fight where the corpses fall and never leave. This is
the *floor* strategy and its existence is what makes ignoring the veins viable:
**~10 scrap, 1 tier, ~tick 300**, versus the economy-committed team's 3. It
loses the economy war by exactly two integer stat steps.

**F — LATE-ECON SPIKE.** Concede the front from ~120 to ~340, cap the tiers,
re-contest with an upgraded prime while the enemy's ratchet holds lapse.
Regaining two captures needs two hunts plus two channels ≈ 60–80 ticks, which
fits in the remaining ~150. A bet that the channel makes three clean captures
hard — which it does.

**G — SCREEN-AND-CHANNEL** is the execution layer of A and B rather than a
separate lane: one body channels, two screen the live headings, TeamRandom
assigns the roles. It is the skill ceiling and the thing wave-8 doctrine should
be briefed on.

### The counter-graph

| archetype | tick budget | beats | beaten by | one-line math |
|---|---|---|---|---|
| **A** breach rush | 3 bodies × 100% front | B, D, F | **C** | 25–40 ticks per capture *if* it wins fights; 0 captures if it does not |
| **B** econ-split | 2 front / 1 lane (26%) | **C**, E | A, D | 2 kiting defenders net the attacker −0.2/tick; 3 tiers by ~300 |
| **C** defend-and-poke | 3 bodies off-point on headings | **A** | B, F | 2 pokers ≈ −1.2/tick against +1.0 gain |
| **D** courier hunt | 1–2 bodies × lanes | **B**, F | A | one interception ≈ 8 scrap + a kill + 30 wasted enemy ticks |
| **E** wreckage front | 3 bodies × 100% front | — (the floor) | B, F on tiers | ~10 scrap → 1 tier at ~300 |
| **F** late-econ spike | 1–2 lane until ~340 | C, a stalled A | a successful A, D | 2 captures back = 60–80 ticks inside the last 150 |

**Cycles:** A → B → C → A (rush beats split, split beats poke, poke beats rush),
and the four-cycle B → C → A → D → B. Every archetype has at least one
archetype that beats it.

### Dominant-strategy verdict

**Without the channel: A dominates outright** — §P2.1's payoff matrix proves it
for B, the same arithmetic kills D and F, and C does not exist because a poked
attacker captures anyway. The design is not done and would measure null.

**With the channel: no dominant strategy.** A is beaten by C; C is beaten by B;
B is beaten by A and D; D is beaten by A; F is beaten by A and D; E is the floor
and is never auto-lost. And the rush is **not over-nerfed** — it still beats
three of the five other lanes, and it remains the correct punishment for a
greedy economy, which is the failure the coordinator warned about in the other
direction.

### Per-matchup build orders (the tracks are bot-choosable, and it shows)

| matchup | the buy, and why |
|---|---|
| striker vs bulwark | An **EDGE race**. The bulwark's T2 (6→8) erases the striker's entire 2-tile standoff; the striker's T2 (8→10) restores it. Whoever declines the race fights at parity in the other's preferred band. The striker's alternative is PLATE (3→5 = 5 bolts, survive the closed gap) |
| striker vs fabricator | The fabricator **must** open PLATE T1: 2→3 HP turns a one-bolt fan kill into a two-bolt one. The striker opens OPTIC (find the prime before it fabricates) or EDGE T1 (9 > 8 in the range duel) |
| bulwark vs fabricator | The bulwark opens OPTIC — vision 4 is its defining weakness and fabricated children arrive beside the prime, anywhere. The fabricator opens PLATE |
| any mirror | **EDGE T1 is decisive**: 9 against 8 is the only strictly-safe firing position that exists against your own chassis |
| any channel-heavy game | PLATE first if you intend to channel or screen; EDGE first if you intend to poke or hunt couriers; OPTIC first if you intend to hunt bodies |

### Depth verdict

By my own standard — *are there at least two archetypes a competent author would
implement as genuinely different code paths, is there a cycle rather than a
ladder, and does the matchup change the correct opening?* — the answer is
**yes, and only with the channel**:

- six archetypes, of which at least four (rush, econ-split, defend-and-poke,
  courier hunt) are different bots, not different parameters;
- one three-cycle and one four-cycle, with no dominant lane;
- a build-order table where the correct first purchase changes in every one of
  the six class pairs;
- an execution layer (channel/screen assignment) with a real skill ceiling and a
  coordination primitive that exists specifically to serve it.

**Worth building.** The honest qualifier: essentially all of that depth is
downstream of the capture channel, and none of it is downstream of the economy
alone. That is the sequencing argument in §P2.6.

---

## P2.6 The purchasable ally

### (a) DRONE as a body — assessed against the monopoly

A 1-HP, gunless, objective-weight-0, vision-only unit is the most attractive
version of "a new ally", and it fails on three specific things rather than on
vibes.

**It is a body where it matters most.** `projectilesStopOnFirstEnemyActor: true`
and `actorsBlockActors: true` mean a drone **absorbs an enemy bolt** and
**denies a tile**. Under the capture channel, a bullet-absorbing body parked on
the firing heading to your channeler is *the single most valuable object in the
game* — it is a free screen that costs no combat presence. That is body count
by another name, aimed precisely at the mechanic the whole design rests on.
Making it transparent requires a new typed form capability (projectile
pass-through for enemy bolts), which does not exist and is not a small addition
to a combat kernel whose invariants are "walls precede actor contact, all
contacts enter one canonical damage batch".

**It breaks topology identity.** *All dynamic entity creation is bounded by
predeclared stable unit capacity*, so a purchasable drone needs a predeclared
slot, and `TopologyProfileIdFor` resolves the profile from per-team slot counts
and **faults rather than borrowing a neighbouring label**. A three-slot striker
team with a drone slot reads as `[4,4]`, which is already registered as the
`trim` four-slot mirror. Every SCRAP cell would need newly registered topology
profiles, and — worse — would stop being topology-comparable to every non-drone
arm. §4.2 spent real design effort keeping the *map* fingerprint constant so the
arm stays comparable; buying a drone throws the same comparability away on
topology.

There is a fix worth recording, because it makes the v2 version tractable:
**predeclare the drone slot in every cell of the arm and have the purchase merely
ready it.** Topology is then constant within the arm whatever anybody buys, and
only five profiles need registering (3+1 mirror, 4+1 mirror, 5+1 mirror, 5+1 vs
3+1, 4+1 vs 3+1).

**It is unmeasurable in this wave.** v1 already carries a new mode subsystem,
three observation facts, a new action family, and — now — a capture-core rewrite.
If the arm moves a class edge, nobody will be able to say whether it was the
tiers, the channel, or the drone.

### (b) DRONE as OPTIC tier 2

Rejected on top of (a). It makes one track heterogeneous — tier 1 an integer
stat step, tier 2 a unit — which breaks the ladder's whole legibility claim
("every tier is one integer step on one axis"), breaks the flat-price
equivalence between deep and broad, and makes the OPTIC significance table in
§P2.2 meaningless because the two tiers are no longer the same kind of thing.

### (c) The ally feeling without a unit

The candidates that reuse existing machinery are weak. A one-shot "clear every
live route cooldown" reuses #181 entirely and costs one line — but only the
volley entry declares a cooldown today, so it is inert in four of the six class
pairs. A one-shot "ready a pending slot" is body count and violates L7
outright. A one-shot map reveal runs into the same non-actor-observer schema
wrinkle (`ObservedBy` is `ImmutableArray<ActorIdentity>`) that priced BEACON at
M/L in the prior memo.

What *is* available at zero cost is **presentation**: name and skin the OPTIC
track as a deployed sensor package so the purchase reads on screen as fielding
something, while remaining an integer stat step. That is honest — nothing is
claimed that is not true — and it is the FOUNDRY ride-along already recommended
in §13.

### (d) Recommendation — not in v1; registered for v2 with a real spec

**Recommend (d).** The drone is a good idea whose three blockers are all
structural rather than numeric, and one of them (projectile transparency) is a
combat-kernel capability that has to exist first. Registered as
**`scrap-drone-tier-3`**, with the v2 shape pinned now so it is a build rather
than a redesign:

```text
prerequisites
  1. a typed form capability: enemy projectiles pass through this form
     (otherwise the drone is a free screen and body count is no longer
     the fabricator's monopoly)
  2. five registered topology profiles with the drone slot ALWAYS declared,
     so topology is constant whether or not the drone is bought

shape
  slot          : one extra predeclared dormant slot per team, always present
  unlock        : a tier-3 purchase; legal only when a track is already at
                  tier 2, cost 20 (double a tier), outside the 3-tier cap
  form          : 1 HP, no attack profile, objective weight 0, movement only,
                  omnidirectional vision range 5, cannot pick up or carry
                  scrap (the L1 economy gate applies)
  lifecycle     : purchase readies the slot; arrival on the team's home pad
                  after 10 ticks; on destruction the slot returns to dormant
                  and the purchase is spent — one drone per match
  observation   : none new. It is an ordinary body in TeamUnits/Allies/Enemies
  spawn reason  : one additive value on GenericActorRuntimeStart.SpawnReason
```

That is buildable in a v2 window and it keeps the fabricator's monopoly intact
by construction, because a body that cannot capture, cannot shoot, cannot
carry, and cannot be shot is not a body in the sense the monopoly protects.

---

## P2.7 Sequencing, and what wave 8 should actually run

**Sequence, do not bundle.**

- **Wave 8a — `--capture channel` alone** (`siege`), on the current candidate
  game, with `channel-speed` (threshold 8, gain 1) as its single paired factor.
  It re-prices the entire pendulum campaign and every class edge, so it must be
  measured against the pacing gates and the #184 triangle with nothing else
  moving. Read `channel-salvo-interrupt` first — the fan reverting up to 6 for
  one 9-tick cast is the most likely way bulwark-vs-striker leaves the band.
- **Wave 8b — `--economy scrap` on the adopted channel** (`bastion`), with the
  registered `scrap-flat-control-arm` running beside it so the `invest` action's
  existence is falsifiable rather than assumed.

The order is forced rather than chosen: §P2.1 proves the economy measures null
without the channel, and #174's fast-iteration rule explicitly does not extend
to two unrelated axes at once. It also has a second benefit — the channel may
improve cap share and leader-extends on its own, in which case the economy's
case has to be argued against the *new* baseline rather than the old one.

**Deferred, recorded, not proposed:** the owner's *"we may have to make longer
games — but hold off on that initially"*. No `maxTicks` change appears in any
spec in this memo. It is registered as the follow-up lever `channel-match-length`
and it is gated on a specific wave-8a symptom, which must be distinguished from
its lookalike:

| symptom | reading | response |
|---|---|---|
| median match ends at tick 500 with the front **still moving** in the last 100 ticks, and non-zero captures in the final quarter | honest games dying at the wall | `channel-match-length` is the right lever |
| median match ends at tick 500 with **zero captures after tick ~200** and near-zero destructions | stall by design — the interrupt is denying rather than delaying | `channel-interrupt-rule` / `channel-speed`, never match length |

Two further failure modes join §11 under the channel arm:

| # | failure | wave-read symptom |
|---|---|---|
| 8 | **Poke-lock** — the interrupt denies rather than delays | captures per match falls below the keel baseline while destructions per match stays flat; progress-reverted-per-match exceeds progress-gained-per-match |
| 9 | **Rush over-nerfed** — the channel makes offence impossible rather than expensive | breach completions fall to ~0 and draws return from ~0 toward the pre-keel rate; leader-extends falls below the #168 plateau |

---

## P2.8 Revised build-ready deltas

Everything in §14 stands except where listed here.

**Economy constants** (§14.1): vein amount **6**; vein spawn ticks **120, 200,
280, 360** (first 120, interval **80**, last 360); pile lifetime **80**; carry
capacity **6**; every tier costs **10** (flat, no escalation); wreck 1, assay 1,
max 2 per track and 3 total unchanged.

**New arm** — `--capture channel`, values `none` (default, inert) | `channel`.
It composes with `--economy scrap` and with every existing arm. Contract shape
in §P2.4.5. Capture threshold moves **15 → 8** under this arm only.

**Zero additional observation facts** for the channel arm; the economy's three
from §14.4 are unchanged.

**Identity tokens**: `siege` (swell + channel), `bastion` (swell + channel +
scrap), `redoubt` (tide + channel + scrap), `forge` / `anvil` / `smelter` for
the economy alone as in §12.5.

**Not built in v1**: the objective keystone (registered fallback), the drone
(registered for v2), any change to `maxTicks`.

---
---

# Part 3 — Owner riders on the capture arm

Four further rulings arrived while Part 2 was being written. Three of them
change the spec; one confirms a design intent that was already latent and makes
it explicit. All four are folded in below, and §P3.5 restates the shipping
numbers.

---

## P3.1 The striker is the premier interrupter — how far does the channel move the triangle on its own?

Owner: *"This would be good for the striker too as it's the best poker
probably."* Correct, and it is a first-class effect of the arm rather than a
side benefit. Four separate striker properties all become systemically valuable
on the same tick the channel ships:

| striker property | what the channel turns it into |
|---|---|
| gun travel 8 (longest) | pokes from outside every other class's reach; a bulwark cannot even *see* it (omni vision 4) |
| cooldown 2 (fastest mobile) | **−0.5 progress per tick** sustained, the best mobile interrupt rate in the game |
| one private bend after 1–4 tiles (its exclusive verb) | **a bent bolt arrives from a heading the screen is not standing on.** Screening is "block the firing line"; the striker is the only chassis that can choose a different line mid-flight |
| the salvo fan (3 lanes, damage 2) | up to **6 progress reverted for one cast**, three quarters of a threshold-8 capture, and the game's only anti-stack weapon |

The bend interaction is the one worth dwelling on, because it converts a
mechanic that has been hard to price into a systemic role: the whole escort
pattern is geometric, and the striker is the only class whose bolts do not
travel in the geometry the escort is defending.

### Estimated movement on each leg

Directional estimates, stated as predictions so wave 8a can falsify them:

| leg | today (#184) | predicted under `siege` (channel only) | why |
|---|---:|---:|---|
| **bvs** | +0.333 | **+0.05 … +0.20** | the striker gains the poke role outright; the bulwark's answers (shell, turret) are *positional commitments* the striker can decline to attack, while poke is available every tick from safety. **Risk: falls below the 0.15 band floor** |
| **fvs** | −0.222 | **−0.20 … −0.35** | both gain — the fabricator gets denial bodies and stacking, the striker gets poke and the anti-stack fan. Stays in band but the magnitude grows |
| **bvf** | +0.333 | **+0.10 … +0.25** | the fabricator's extra body becomes a screen or a denial body, which under the channel is the most valuable marginal body in the game. **Risk: falls below the floor** |

The pattern is that **the bulwark is squeezed from both sides**: the striker
out-pokes it (range 8 and vision 6 against range 6 and vision 4) and the
fabricator out-bodies it. Since the bulwark is the current ladder top on both
its legs, a squeeze is corrective in direction — but two of three legs are
predicted to move *toward and possibly through* the band floor, which is a real
over-rotation risk rather than a rhetorical one.

### Feature or risk — the honest answer

**A feature carrying one nameable over-rotation risk, and the risk is the fan,
not the chassis.**

Feature, because: the striker is the class seven waves failed to fix, #184's
close is explicitly fragile (*"iron-root's leg is the entire remaining bvs
payoff"*, *"freshness is asymmetric"*), and the highest-quality fix this
campaign has produced was #179's — a role the class was already best at, found
rather than granted. The channel gives the striker a *job* (deny the point from
outside it) rather than a number, and jobs generalise where numbers do not.

Risk, because: the buff is not one thing. Range, cadence, bend-around-screens
and fan-as-burst all land on one class simultaneously. That is precisely the
bundling that made FIVE SLOTS unattributable in #171, and the piece most likely
to overshoot is the fan, whose 6-progress cast against a threshold-8 capture is
the single largest number anywhere in this design.

The mitigation is measurement, not a pre-emptive nerf: **the fan is already an
inert-omittable arm with a pinned historical alternative** (`--volley cast`,
the entry-2 fan, byte-pinned since #183), so it can be switched off inside a
channel cell at zero engineering cost.

### Wave-8 read design — attributing channel from scrap

A 2×2 on the two axes, plus a fan sub-control on the channel axis. Every cell
runs the same wave-8 doctrine cohort so freshness is symmetric (the standing
caveat from #184).

| cell | `--capture` | `--economy` | what it answers |
|---|---|---|---|
| `swell` | off | off | the #184 baseline re-run with wave-8 doctrine — the only honest comparison point |
| `siege` | **channel** | off | **the channel's effect on the triangle and the pacing gates, isolated.** The primary read |
| `forge` | off | **scrap** | the economy with the front unchanged — **and the falsification cell for §P2.1**: if harvesting happens here at any material rate, the dominance proof is wrong and the channel is not a prerequisite |
| `bastion` | **channel** | **scrap** | the shipped package. Interaction term = `bastion − siege − forge + swell` |

Sub-control, striker cells only: `siege` with `--volley cast` against `siege`
with `--volley salvo`, which separates *"the striker chassis is the best
poker"* from *"the fan is the best interrupt"*. Registered as
`channel-salvo-interrupt` (§P2.4.5) and it is the first number to read.

---

## P3.2 Recapture economics — the parked reclaim note, executed

Owner ruling: *"recaptures need to be lowered to like 1-1.25"* — the total cost
of flipping an enemy-claimed zone should be 1.0–1.25× a fresh capture.

### Today's cost, exactly

The kernel erodes an opposing claim by the controller's gain and, when it
crosses zero, clears the claim **without starting its own on the same tick**;
overshoot is discarded (`ApplyControl`, and the architecture's own §4 wording).
So flipping a fully-built enemy claim at threshold `T` with gain 1 costs
`(T−1)` erode ticks plus `T` build ticks:

| threshold | flip | fresh | ratio |
|---:|---:|---:|---:|
| 15 (today) | 14 + 15 = 29 | 15 | **1.93×** |
| 8 (channel-speed) | 7 + 8 = 15 | 8 | **1.88×** |

The owner's parked note (*"2x on the recapture is a bit excessive"*) is exactly
right, and the number is the same before and after the channel-speed change.

### The mechanism options, priced against the 1.0–1.25 band

**(a) Erosion-rate multiplier `N` — erode at N× build speed.** Cost becomes
`ceil((T−1)/N) + T`, so the ratio is `1 + ceil((T−1)/N)/T`.

| N | flip at T=8 | ratio | flip at T=15 | ratio |
|---:|---:|---:|---:|---:|
| 1 (today) | 15 | 1.88× | 29 | 1.93× |
| 2 | 4 + 8 = 12 | 1.50× | 7 + 15 = 22 | 1.47× |
| **4** | **2 + 8 = 10** | **1.25×** | 4 + 15 = 19 | 1.27× |
| 8 | 1 + 8 = 9 | 1.13× | 2 + 15 = 17 | 1.13× |

**N = 4 lands on the owner's stated ceiling exactly at the shipping threshold,
and the ratio slides down to 1.0× as the enemy's standing claim shrinks** — a
partial claim of 3 costs 1 erode tick plus 8, i.e. 1.13×; a claim of 1 costs
1.13×; a claim of 0 is 1.00× by definition. **So a single number puts the whole
range of recapture costs inside the owner's stated 1.0–1.25 band, at both
thresholds.**

**(b) Simultaneous erode-and-build.** "Presence decrements their claim AND
increments yours per tick" reaches exactly 1.0× — but only if both claims are
stored, because that is what "two numbers moving at once" means. The Frontline
control state carries **one** `ClaimingTeamId` and **one** `CaptureProgress`,
and the timeout score is derived from that single pair; a per-team claim model
changes the state record, the score derivation, the validator invariants, and —
critically — the published `captureProgress`/`claimingTeamId` pair that every
existing bot reads. Modelled as a *signed* single accumulator instead, it does
**not** reach 1.0×: moving from +7 to −8 at any fixed rate costs 15 units
against a fresh 8, which is the same 1.88× in a different representation.
**Rejected: it costs an observation break and a state-model rewrite to buy
0.25× that (a) already delivers for one integer.**

**(c) Better options considered.** Carrying the overshoot at the crossover
(N=4 with carry gives 1.13×) is attractive but overturns a documented
architecture invariant — *"opposing gain erodes a claim to zero without starting
its own claim on the same tick"* — for a quarter of a capture. Registered as the
alternative level rather than shipped. Decaying an enemy claim while you merely
stand nearby was rejected as a second presence rule with no read.

**Recommendation: (a) with `N = 4`, no overshoot carry.** One integer, zero
observation change, the documented invariant preserved, and the owner's band hit
across every claim state.

### Does eroding also require stillness, and is it interruptible?

**Yes to both, and symmetry is not the main argument — the alternative is
degenerate.** If erosion were exempt from the channel, a single *kiting* body
would wipe a fully-built claim in 2 ticks at N=4 while dodging, which makes a
built claim worth almost nothing and re-opens the mean-reversion problem the
whole pendulum campaign closed. So the rule generalises rather than forks:

> **The CONTROLLING team is whichever team's stationary claim weight strictly
> exceeds the other team's total denial weight. Control moves the number —
> eroding the opponent's claim at `N ×` rate, then building its own at `1 ×`.
> Hostile damage to a controlling-team body standing on the active objective
> reverts the controller's WORK on this run by the damage amount, floored at
> zero work.**

"Work on this run" rather than "the claim" is the precise formulation, and it
matters: if the interrupt moved the raw claim number it could push an
opponent's claim *up* past the threshold and complete a capture for the team
being shot at. Reverting work can never do that — it can only undo what the
controller has done since it took control, and a full revert restores the
position exactly as the controller found it.

Consequence, and it is the owner's escort pattern applied to the other
direction of the pendulum: **taking ground back is also a channel that needs a
screen.** The bulwark defending captured ground is defending against an
*erosion* channel, and a turret with line of sight reverts 1 per tick against a
stationary eroder — which is the class-role read in §P3.4 falling straight out
of the arithmetic.

### Systemic effect, and whether the ratchet hold needs re-tuning

Cheaper recaptures make the pendulum swingier and produce more lead changes,
which is the dynamism the owner has asked for since #158. Two interactions to
check.

**Sticky-frontline / ratchet hold (40 ticks).** More completed enemy captures
land inside a live hold, and every one of them is *spent* — so the hold becomes
**more** valuable, not less, and a naive reading says re-tune it downward. The
careful reading says leave it alone: `RatchetHoldTicksDefault = 40` was
calibrated against the measured **advance-reversal latency**, not against the
capture clock — *"respawn 18 plus transit 12"* — and neither respawn nor transit
changes under any arm in this memo. Forty ticks remains ≈1.33× the
reinforcement wave.

**Do not pre-emptively re-tune it. Register `channel-ratchet-retune` with an
explicit diagnostic instead:** spent captures per match (captures denied by a
live hold). If that count exceeds roughly one per hold on average, the hold is
absorbing more offence than it was priced to absorb and 25–30 is the next value
to try; if it stays near today's level, 40 is still correct and the swingier
pendulum arrived without the hold needing to move.

**Registered factor.** `recapture-cost`, with the owner's stated **1.0–1.25×
target band** recorded as the acceptance criterion, levels `N ∈ {1, 2, 4, 8}`
plus the overshoot-carry variant, and the required report being *flip cost as a
multiple of fresh capture cost, measured from replay rather than derived*, plus
lead changes per match and time-at-each-position-index.

It ships in the same capture-arm contract shape as the channel — see §P3.5.

---

## P3.3 Stacking: multiple channelers capture faster, with a cap

Owner: *"multiple bots capturing at once should capture faster — so Fab has
that going for it too."* Confirmed and pinned: **gain scales with net
stationary objective weight**, which is contest-majority's surplus scaling
carried over and applied to channeling bodies only. That was already the
arithmetic in §P2.4.1; what follows is the part that was not settled.

### The emergent tradeoff, named

Every stacked channeler is stationary and therefore interruptible, and every
stacked channeler is a body that is **not screening**. So the choice each tick
is:

- **Stack for speed** — N stationary bodies gain N per tick, but present N easy
  targets and block zero firing lines.
- **Screen a lone channeler** — 1 per tick, but two teammates standing on the
  live firing headings absorb the bolts (for free, because damage off the
  objective reverts nothing).

**Against a broken defence, stack. Against a live one, screen.** It is a read on
published state — how many enemy bodies are alive, where, and is a fan entry off
cooldown (`routeCooldowns` publishes exactly that) — which is the right shape
for a decision that should be made every tick rather than once per match.

**And the fabricator is the class that can do both at once**, which is its
intended systemic role: four bodies field two channelers plus two screens where
a three-body team must choose. That is the fourth slot's honest value under this
arm, and it is larger than its value under any arm measured so far.

### The cap, and why there has to be one

Uncapped, the arithmetic runs away at exactly the wrong end. At threshold 8 a
post-wipe three-body stack captures in 3 ticks and a five-slot fabricator stack
in 2 — and, worse, the marginal value of bodies 3, 4 and 5 is *pure channel
speed*, which is precisely the "extra bodies buy extra tempo" loop that killed
SURGE and that the five-slot arm was tuned to avoid (*"more bodies, deliberately
not faster bodies"*).

> **Gain multiplier = min(2, stationaryClaimWeight − opponentDenialWeight).**

Checked against every case that matters:

| situation | net stationary | multiplier | gain | outcome |
|---|---:|---:|---:|---|
| solo screened channeler | 1 | 1 | 1.0 | 8 ticks (10 with a leaked bolt) |
| 2 channelers + 1 screen, defence dead | 3 | **2** | 2.0 | **4 ticks** — stacking pays, exactly as the owner asked |
| 3 channelers, no screens, 2 live pokers | 3 | 2 | 2.0 − ~0.75 revert | 7 ticks while taking 0.75 dmg/tick — **screening is the better default under fire** |
| 3 stationary attackers vs 2 kiting defenders | 1 | 1 | 1.0 − ~1.2 | −0.2 → **2 still hold 3** |
| post-wipe 3 v 0 | 3 | 2 | 2.0 | 4 ticks |
| fabricator 3 stacked + 1 screen | 3 | 2 | 2.0 | 4 ticks — **identical to 2 stacked + 2 screens** |

The last row is the point: **the cap means the fabricator's third, fourth and
fifth channeling bodies buy no additional capture speed at all.** They buy
screens and denial weight, which is a positional advantage that has to be
played, not an arithmetic one that accrues. Registered as `channel-stack-cap`
with an uncapped level so the claim is measured.

### Whole-claim revert versus per-body revert

The question rider 4 asks — does one hit on one stacked channeler revert the
whole claim, or only that body's contribution?

**Per-body revert is degenerate under the cap, and the arithmetic says so
immediately.** With three channelers and the multiplier capped at 2, hitting one
body drops claim weight from 3 to 2, which is still capped at 2 — **the hit has
literally no effect**. Per-body revert makes a stack of three *immune* to
interruption, which inverts the entire mechanism.

**Whole-claim revert is adopted.** It moves the one number the observation
already publishes, so a bot reads the interrupt in `captureProgress` with no new
bookkeeping; it makes screening mandatory rather than optional whenever the
enemy has a live gun; and it is the variant that does **not** compound the
fabricator's body advantage. Registered as `channel-stack-interrupt` with the
per-body level recorded as the rejected alternative and the reason.

### Fabricator dominance check across the whole package

The triangle protection has to hold against stacking, numeric harvesting, and
upgrade scope **combined**, not one at a time:

| channel | fabricator gain | bounded by |
|---|---|---|
| stacking speed | **none beyond 2 bodies** | the `min(2, …)` cap |
| harvest capacity | can field a full front *and* a harvester | the vein pot is fixed at 48 scrap and one harvester services a cycle — extra bodies buy security, not income (§P2.5 B) |
| upgrade scope | **none** | prime-only in v1 (§P3.4) |
| screens and denial weight | **real and intended** | the 2-HP prime is the game's worst channeler and worst screen; children are 3 HP; and the class supplies the most wreckage to its opponent |
| exposure | — | the fan is the anti-cluster weapon, aimed precisely at the playstyle |

Net: the fabricator's gain from the channel is **positional and playable**
rather than arithmetic, and it is the largest single reason bvf is predicted to
fall toward the band floor (§P3.1). That is the number to watch in `siege`.

---

## P3.4 Upgrade scope becomes a registered axis

Owner: *"We could also play around with buffs for ALL team bits vs only
prime."* Scope is promoted from a fixed constant to a registered tunable axis.

### The amplification, quantified

The right unit is **upgraded-body-ticks per scrap spent** — how much upgraded
body a tier actually buys. Taking a purchase at tick 200 and a prime uptime of
~0.71 (18-tick returns, ~8 prime deaths), and expressing each scope as the
average number of live bodies it covers from purchase to tick 500:

| scope | striker / bulwark (3 slots) | fabricator `wane` (4 slots) | fabricator `full` (5 slots) |
|---|---:|---:|---:|
| **(a) prime-only** | 0.71 | 0.71 | 0.71 |
| **(b) all-bodies** | ~2.2 | ~2.8 | ~3.2 |
| amplification vs prime-only | **3.1×** | **3.9×** | **4.5×** |
| **fabricator's edge over a 3-slot class** | 1.00× | **1.27×** | **1.45×** |

So all-bodies scope hands the fabricator **27–45% more upgraded body-tick per
scrap at the same price**, on top of the harvest capacity and the screening
value already counted in §P3.3. Prime-only is 1.00× by construction — it is the
same L5 mitigation MUSTER used with `PrimeAutomaticReturnOnly`, for the same
reason.

### Does all-bodies need class-aware pricing?

It can have it — Part 1's rule allows exactly this (*effects are never
class-aware; prices may be class-aware where the effect is asymmetric*) — and
the correction is computable: the fabricator would pay **13 per tier under
`wane` and 14–15 under `full`** to restore body-tick parity.

Two reasons not to make that the v1:

1. **It couples the upgrade ladder to the slot count**, so every registered
   five-slot tuning variant (`full`, `trim`, `boom`, `drag`, `moor`, `wane`)
   needs its own price column. That is a factor explosion across a family that
   is already registered and already carries its own ablation debt.
2. **Prime-only buys the same felt payoff for a quarter of the price and no
   coupling**, and it produces a *hero unit* — one visibly upgraded body — which
   is more watchable than a uniformly slightly-better team and easier to read on
   screen at the moment of purchase.

### (c) Per-track scope — the design favourite, and it is a v2 candidate

The channel analysis produced a third option that is better than either pure
one, and it deserves to be on the record:

> **PLATE all-bodies; EDGE and OPTIC prime-only.**

The rationale is that the channel gives each track a *role*, and the roles do
not live on the same bodies. **PLATE is the screen's stat**, and screens are by
definition the non-prime bodies — a plate that only protects the prime buys
nothing for the job it exists to do. **EDGE is the poker's and courier-hunter's
stat** and **OPTIC is the hunter's**, and a single upgraded poker or hunter is a
coherent hero unit. Under this mix the fabricator amplification applies to one
track of three, so its class edge is ~1.09–1.15× rather than 1.27–1.45× — small
enough to accept without any price table at all.

It is not the v1 recommendation only because it adds a per-track scope field and
because scope should be *priced* before it is *mixed*. It is the first thing to
try if the registered scope read says all-bodies is better than prime-only.

### Registration

**`scrap-upgrade-scope`** is upgraded from a note to a three-level registered
axis, with the owner's interest recorded:

| level | shape | note |
|---|---|---|
| `prime-only` | v1 recommendation | flat per team by construction; amplification 1.00× |
| `all-bodies` | owner's interest | needs class-aware pricing (fabricator ×1.3 `wane`, ×1.45 `full`) or an explicit ruling that the amplification is a deliberate fabricator lever |
| `per-track` | design favourite for v2 | plate all-bodies, edge and optic prime-only; amplification ~1.1× without any price table |

Required report per level: upgraded-body-ticks per scrap by class, tier-vector
distributions, and — the gate — the three class edges, since this axis is a
fabricator lever whichever way it is set.

### The class-role read, folded into the strategy lanes

The owner's three systemic roles fall directly out of existing class machinery
under the channel arm, and §P2.5's archetypes should be read through them:

| class | systemic role | the machinery that makes it true |
|---|---|---|
| **bulwark** | **defends captured ground** | the turret reverts 1 per tick against a stationary eroder at travel 8 and cooldown 1 — the best recapture denial in the game — and the shell is a channeler that blanks frontal pokes for three deflections. Both are positional commitments, which is what defending ground is |
| **fabricator** | **numeric advantage** | screens, harvesters, and denial weight. Its fourth body is worth exactly one screen or one denial body, and under the channel that is the most valuable marginal body in the game — but *not* extra capture speed (§P3.3's cap) |
| **striker** | **poke and interrupt** | longest gun, fastest cadence, bends that arrive off the screened heading, and a fan that reverts up to 6 in one cast (§P3.1) |

Mapped onto §P2.5: the bulwark is the natural **C — defend-and-poke** and the
holder in **A** after a capture lands; the fabricator is the natural **B —
econ-split** and **G — screen-and-channel**; the striker is the natural **C**
and **D — courier hunt**. Three classes, three lanes, no lane empty — which is
the strongest single piece of evidence that the depth verdict in §P2.5 is real
rather than enumerated.

---

## P3.5 Revised capture-arm spec

Supersedes §P2.4.1, §P2.4.3 and §P2.8 where they differ. One arm,
`--capture channel`, carrying the channel, the stack cap, and the recapture
multiplier — they ship together because they are one coherent change to how
ground is taken and kept.

### Rules

1. **Claim weight** counts a team's bodies on the active objective whose
   position at the end of this tick equals their position at the end of the
   previous tick. **Denial weight** counts all of a team's bodies on the active
   objective. A life created this tick counts as stationary. Rotating,
   shooting, and starting a transform do not break stillness; only a change of
   tile does.
2. **Control**: the team whose claim weight strictly exceeds the opponent's
   denial weight controls. Otherwise no team controls and the claim is
   preserved (keel's decay clock, unchanged).
3. **Multiplier** = `min(2, claimWeight − opponentDenialWeight)`.
4. **Erosion**: while an opposing claim stands, the controller reduces it by
   `4 × gain × multiplier` per tick. On reaching zero the claim clears and the
   controller starts no claim on that tick (the documented invariant is
   preserved).
5. **Build**: with no opposing claim, the controller adds `gain × multiplier`
   per tick; reaching the threshold completes the capture, overshoot discarded.
6. **Interrupt**: hostile damage to a **controlling-team** body standing on the
   active objective region reverts the controller's **work on this run** by the
   damage amount, floored at zero work — never past the position the controller
   found. Damage to bodies off the objective reverts nothing. One hit reverts
   the whole run, not one body's contribution.
7. **Threshold 8**, gain 1 (`channel-speed`).
8. Decay clock, redeploy pause (5), and ratchet hold (40) are unchanged.

### Contract shape

```text
rules.gameMode.capture
    threshold : 8                                      // channel-speed
  + controlPolicy :
      StationaryClaimWeightVersusTotalDenialWeightScalesGainCapped…   // new enum value
  + stationaryGainMultiplierCap : 2                    // channel-stack-cap
  + opposingErosionMultiplier   : 4                    // recapture-cost
  + claimInterrupt                                     // whole block ABSENT = inert
      kind                 : DamageToControllerOnObjectiveRevertsWork
      revertPerDamagePoint : 1
      scope                : ControllingTeamBodiesOnActiveObjectiveRegion
      granularity          : WholeRun                  // closed enum; PerBody is the registered alternative
```

Absent-means-inert throughout; every historical ruleset keeps byte-exact
fingerprints. **Observation cost remains zero** — `captureProgress` and
`claimingTeamId` keep their exact published shape and meaning, and every rule
above moves those same two facts.

### Ablation registrations added by Part 3

| id | interpretation | required isolation |
|---|---|---|
| `recapture-cost` | the erosion multiplier sets flip cost as a multiple of fresh capture cost; owner's target band **1.0–1.25×** | levels N ∈ {1, 2, 4, 8} plus the overshoot-carry variant; report flip cost measured from replay, lead changes per match, and time at each position index |
| `channel-stack-cap` | capping the stationary multiplier at 2 simultaneously honours "more bots capture faster" and denies the fabricator a stacking payoff for bodies 3–5 | run uncapped; report captures per match, mean channelers per capture, and the bvf edge |
| `channel-stack-interrupt` | whole-run revert makes screening mandatory; per-body revert makes a capped stack immune | run the per-body level; report screen-adjacency share and progress reverted per cast |
| `channel-ratchet-retune` | the 40-tick hold was priced against the 18+12 reinforcement wave, not the capture clock | **do not pull pre-emptively.** Diagnostic: spent captures per match; if it exceeds ~1 per hold, try 25–30 |
| `scrap-upgrade-scope` | promoted to a three-level axis (§P3.4) | prime-only / all-bodies (with class-aware pricing) / per-track; report upgraded-body-ticks per scrap by class and all three class edges |
| `channel-striker-role` | the channel hands the weakest chassis the best defensive role via four stacked properties at once | the `siege` × `--volley cast`/`salvo` sub-control; predictions in §P3.1 are pre-registered and falsifiable |

### Identity tokens

Unchanged from §P2.4.5 — the recapture multiplier and the stack cap are part of
the same `channel` arm and mint no separate token: `siege` (swell + channel),
`bastion` (swell + channel + scrap), `redoubt` (tide + channel + scrap), and
`forge` / `anvil` / `smelter` for the economy alone.
