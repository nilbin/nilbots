# Arc Relay: mechanics for a second strategic axis

**Consulting report — game design, experimental Arc Relay mode (2026-08-05).**
Produced by a design-survey agent on owner request ("how do we make it a
deeper game"); grounded in `docs/EXPERIMENTAL-ARC-RELAY.md`,
`docs/ARC-RELAY-SIGNATURE-AUDIT.md`, `src/BotArena.Engine/ArcRelayH0Definition.cs`,
`src/BotArena.Engine/ArcRelayLoopProfile.cs`,
`src/BotArena.Engine/ArcRelayActorMatchModeDriver.cs`, `docs/DECISIONS.md`
#199–#209, `docs/reports/GATE-2-MECHANICS-BRIEF.md` §9,
`docs/reports/ARC-RELAY-DEPTH-AND-MAP-DESIGN-REPORT.md` §9,
`balance/arc-relay-felt-degeneracy-bars-v4.json`. Ideas only — nothing here
is implemented or ruled on.

---

## 1. Diagnosis: the mode has ~4,800 body-ticks and only ~234 of them can score

BFS on the shipped map geometry (8-way walkable):

| Fact | Value |
|---|---|
| Walkable distance, **every** Well → **either** reactor | **exactly 13 steps** |
| Courier round trip home→well→home | **26 ticks** |
| Mean interval between Core births (3 wells, cadence 75, staggered 25) | **25 ticks** |
| Total scheduled Core births in a match | **21** (t25 → t525) |
| Cores needed to win | **9** (43% of all supply) |
| Team capacity | 8 bodies × 600 ticks = **4,800 body-ticks** |
| Raw carriage cost of a *winning* 9 cores | 9 × 26 = **234 body-ticks = 4.9%** |
| Even a lavish 3-slot standing well-coverage posture | ~1,800 body-ticks = **37%** |

`GATE-2-MECHANICS-BRIEF.md` §9 registered the designed defence against "one
convoy is always correct": *"one beat every 25 ticks while ordinary minimum
Well-to-reactor carriage is 26 ticks"* — the intent was that servicing one
beat concedes another. **That defence does not bind, because a team has
eight bodies and needs about two.** The allocation constraint was designed
for a force that was fully employed; the force is 60–95% unemployed.

So the real structural statement is:

> **The objective consumes at most 3 of 8 slots, and the other 5+ slots
> have no channel through which their labour can become score — except by
> subtracting from the enemy's 2–3 courier slots.**

That is a complete explanation of the finding. Zoning, blockade, hooks,
mines, siege and racing are not six strategies; they are six ways to spend
the same idle 5 bodies on the same single verb: *modulate the opponent's
courier rate*. Razor-thin 9–8 margins follow mechanically (both sides mine
a 21-core pool at near-equal efficiency), and so does the absence of
comebacks: banked Pulses are a monotone ratchet over a shrinking, fixed
supply. `DECISIONS` #201 records the measured consequences — **75%
first-Pulse conversion** (the Gate-2 registered alarm threshold was 70%),
**zero behind-to-ahead Pulse reversals**, 14/16 mirrored sweeps, **zero
directed cycles in four counter-webs**.

**Two corollaries that drive every proposal below:**

1. **Nothing you do at tick 100 exists at tick 300, except the Pulse
   counter.** Units respawn at full hull in 20 ticks; constructs expire in
   8–30 ticks; cores respawn on a fixed clock; the map never changes. A
   game with no persistent contestable state cannot have long-horizon
   strategy — only a repeating 26-tick tactical loop. Depth requires
   **state that accumulates and can be taken**.
2. **Arc Relay is the inverse of the Frontline SCRAP situation.**
   `DESIGN-SCRAP-ECONOMY-2026-07-30.md` Part 2 proved harvesting was
   *strictly dominated* because a 28-tick harvest round trip competed with
   a 28-tick capture that was worth more; "Stay" was the unique
   equilibrium. In Arc Relay the front costs ~5% of capacity, so a
   diversion is not competing for scarce labour — **there is 60%+ free
   capacity waiting for a second sink.** Any new axis here is far more
   likely to be *used* than the Frontline family was. The same Part-2
   payoff-matrix test still applies (price in **body-ticks per charge**;
   the courier benchmark is **26–120 body-ticks per core** depending on
   escort).

---

## 2. Inherited constraints these proposals respect

| Constraint | Source | Consequence for design |
|---|---|---|
| **No economy, no score-to-power.** Deliveries never improve stats, production, cooldowns, vision, respawns. | DECISIONS #199 (owner ruling); `EXPERIMENTAL-ARC-RELAY.md` | No upgrade ladders. A Pulse may be *spent on a different strategic effect*, but never on combat stats. Nothing may pay respawn ticks or slot counts. |
| **Not a comeback bonus; cannot convert score into power; must affect both sides identically.** | `ARC-RELAY-DEPTH-AND-MAP-DESIGN-REPORT.md` §9 | Anti-snowball must be **structural** (changes the integral over the match), never rubber-banded to the scoreboard. |
| **No energy/resource pool gating actions.** | DECISIONS #47/#48 | Skills stay priced in windups and tenure. |
| **Every enemy-affecting effect gives its victim an out** — travel time, tell, or positional warning. | `ARC-RELAY-SIGNATURE-AUDIT.md`, grammar-2 | New hazards need bolts or telegraphs. |
| **Felt-degeneracy bars v4 are frozen and never loosened to admit a tactic.** | `balance/arc-relay-felt-degeneracy-bars-v4.json`; DECISIONS #202 | Nothing may reward *parking*: `stuckCarrier` trips at 30 same-position carrier ticks; `homeCarrierNonProgress` at 30 uncontested ticks without new best distance; `sustainedPassivity`/`formationFreeze` at 60/75 ticks ≥75% Wait. **Any "hold value" mechanic must attach to loose objects and defended ground, never to a parked carrier.** |
| **A side objective must not pay the timeout channel by itself** (Frontline law L2 — the purest camping incentive available). | `DESIGN-SIDE-OBJECTIVES-2026-07-30.md` | Prefer secondary routes that *modulate the exchange rate* of couriering over routes that bypass the courier entirely; if a bypass route exists, it must be contestable at a fixed public location and priced above the courier. |
| **One published observation fact per concept, two as an absolute cap.** | Frontline law L3; confirmed by code: a new `ArcRelay*State` field is a **~12–15 file** change (hand-written `Equals`/`GetHashCode`, positional reconstruction in the fog filter, twin wire codec, `ReplayV3Serializer` exact `RequireProperties`, three web files). | **The published-fact count is the real cost driver.** Every proposal states it. |
| **Immutable-beside minting.** Anything that moves `ActorRulesDefinition` bytes is a new `ArcRelayLoopProfile` with a new `RulesetId`; new contract fields are canonically written **only when non-default** so historical fingerprints stay byte-exact. | `ArcRelayLoopProfile.cs` (ForwardCombat2 pattern), `ArcRelayGameModeDefinition.cs` | Every sketch assumes a new profile minted beside the current line, `Registered` appended, fingerprint tests extended with a "new fingerprint AND old fingerprint unchanged" assertion. CLI needs **zero** edits — `Resolve()` picks up any registered profile. |

**Cost calibration used throughout** (verified against the code):

| Size | Meaning | Files |
|---|---|---|
| **XS** | Numbers only, already a profile/definition parameter | 2–3 |
| **S** | New map geometry / new well schedule entry / driver-local tick logic with no new published fact | 3–5 |
| **M** | New action of an existing `ActorActionKind` | ~6–8 |
| **M/L** | New `ArcRelayEvent` kind (projection → ReplayV3 → serializer → chronology validator → SDK wire codec ×2 → web ×3) | ~9–12 |
| **L** | New published observation field | ~12–15 |
| **L/XL** | New persistent map entity + its own state record, events and rendering | ~25–35 |

---

## 3. The enabling primitive (buy once, then four mechanics are nearly free)

Six of the twelve proposals need exactly one thing the mode does not have:
**a Core is worth a variable amount.**

**`ChargeValue`** — one new int on `ArcRelayCoreState`, and
`ArcRelayGameModeDefinition.CoresPerPulse` → `ChargePerPulse`.

- Set `ChargePerPulse = 6`, base `ChargeValue = 2`. **The baseline game is
  bit-for-bit unchanged in feel:** 3 base cores per Pulse, 9 per win.
- Reactor charge is already published (`ArcRelayReactorState.ChargePips`)
  and is already a score channel — no change there.
- Cost: **L (~15 files)**, driven entirely by the one published field.
  Driver logic is trivial: banking adds `core.ChargeValue` instead of 1.
- Once paid: **Ripening (#1), Contested Birth (#2), Conduit multiplier
  (#7), Salvage (#8) and Late Surge (#9) all become XS–S additions.**

This is the single highest-leverage architectural decision in the report:
spend the one expensive published fact once, on the right abstraction, and
the rest of the design space opens at S cost.

---

## 4. Twelve mechanics

### 1. Ripening Cores — "the prize grows while nobody dares take it"

A loose Core gains **+1 ChargeValue per 45 ticks it remains untouched at
its Well** (2 → 3 → 4, cap 4). Value **freezes on first pickup** and never
resets. A carrier killed drops a Core at its frozen value; ripening resumes
only after 20 uninterrupted loose ticks.

**New axis: investment timing / greed vs. tempo.** For the first time a
team must answer "*when* do we take this?" — not just "who gets it". A
well holds at most one outstanding Core, so a ripening Core suppresses that
well's entire production: a 90-tick ripen buys +2 charge and forgoes ~1.2
beats (~2.4 charge) — greed is **slightly negative on raw supply and
positive on positional leverage**. Invents the **denial pickup** (grab at
value 2 purely to stop enemy ripening) and the **ripe steal** (a 4-charge
carrier kill is 67% of a Pulse).

Archetypes: hooks biggest winner (displacement gets an economic target);
zoning becomes an investment with a payoff clock; the three dead classes
(mines/smoke/null-field) get discrete scheduled high-stakes moments; race
stays viable but must beat the clock; siege weakened.

Comeback: strong and structural — one ripe-core fight moves two-thirds of
a Pulse, symmetrically, scoreboard-blind.

Implementation: enabling primitive + `ripenIntervalTicks`/`ripenMaxSteps`;
ripening in `PrepareTick`; **no new event kind** (mode-state diff carries
it). New published facts: **1** (`ChargeValue`). **Size: L (~15 files), of
which ~13 are the primitive; marginal S.**

Risk: over-swinginess — cap at +2 and hold `ChargePerPulse = 6`. A pure
turtle around a ripening Core trips `formationFreeze`, which forces active
guarding (a feature).

### 2. Contested Birth (Prime Cores) — "show up, or it's worth less"

At a Well's birth tick, evaluate presence within Chebyshev 3: **both teams
present → born at 3 charge; one team → 2; neither → 1.** Attendance
becomes a priced, game-theoretic decision; free-farming is devalued; fights
near wells get bigger. Race nerfed hardest; siege punished (its all-in
makes every enemy birth a freebie). Comeback mild (compresses efficiency
differences). **XS on top of the primitive.** Risk: mutual-avoidance
equilibrium — keep uncontested at 2 (never below baseline) and pair with
#1 so skipping leaves a ripening prize.

### 3. Well Migration — "the map has phases, on a published timetable"

Each Well has a **finite yield of 3 Cores**; exhausted, production moves to
the next site in a **fixed, published, rotationally-fair rotation** —
static contract data, so every mind can compute the whole match's geography
at MatchStart. `WellChanged` fires on relight.

**New axis: spatial planning across time.** Construct placement becomes an
investment with a known expiry and a known next target; static route
solutions die permanently; the sheet grammar's route/rally machinery gets a
real workload. Zoning transformed (pre-siting becomes its signature skill);
blockade re-contests corridors each phase; siege significantly weakened;
mines become first-class (pre-seeding a site guaranteed to light in 30
ticks).

Comeback: structural and symmetric — entrenchment expires on a schedule,
identically for both sides. The strongest non-rubber-band anti-snowball in
the report.

Implementation: pre-declared candidate sites as map regions + ordered site
list/per-site yield on the schedule definition + driver switch of
`WellRuntime.Position`. **New published facts: 0.** **Size: M (~8–10
files).** Risk: site fairness — enforce `distW == distE` for every site
and exact 180° closure with a BFS test; keep coarseness at 3 cores/site
(~200-tick phases).

### 4. Pulse Ledger — "score is a currency with more than one sink"

Pulses stop auto-firing; a banked Pulse is spent at the socket by a new
objective action naming a sink:

| Sink | Cost | Effect |
|---|---|---|
| **Strike** | 1 Pulse | −1 enemy reactor segment (today's behaviour; 3 Strikes win) |
| **Mend** | 1 Pulse | +1 own segment, never above 3, **max 2 per match** |
| **Lock** | 1 Pulse | Freeze one named Well's rearm for 90 ticks |
| **Sweep** | 2 charge | Reveal every enemy body for 15 ticks; 90-tick cooldown |

Timeout tiebreak ranks **Strikes landed**, then charge — a Mend is not a
Strike. New axis: conversion choice (tempo vs. insurance vs. denial vs.
information) plus a bluff/timing layer on visible hoarded Pulses. Siege/
blockade gain Lock; zoning gains Sweep; a losing team gains Mend — the
first mechanic that buys time rather than losing slower. Comeback: the
most direct and legal (symmetric, capped, self-selecting to the trailing
side). **Size: M/L (~12–15 files), 0 new published facts** (+1 event kind
for legibility). Risk: Mend-stalling — contained by the cap, the tiebreak,
and full-Pulse pricing. **Sinks must never touch combat stats, cooldowns,
own-unit vision, or respawns** (DECISIONS #199 boundary, to be stated at
minting).

### 5. Reactor Vent (Overcharge) — "greed that opens your own door"

Voluntary, telegraphed (3-tick tell, 60-tick duration, 120-tick cooldown):
while vented, every banked Core is +2 charge — and the home pad's
hostile-entry protection lapses, with basic-gun fire on the socket removing
a segment per 8 hits. Siege/blockade get their first genuine win condition
that isn't throughput denial, and they cannot choose it unilaterally.
High-variance symmetric comeback. **Size: L (~15–18 files)** (one new
published field `VentEndsAtTick`). Risk: if the rate beats the exposure,
venting becomes mandatory — register an ignore-vs-commit edge target in
[0.15, 0.35] and tune the multiplier.

### 6. Reactor Tap — "your stockpile is not safe"

An enemy body at your socket channels 6 uninterrupted telegraphed ticks to
remove **1 charge** and birth a loose Core of that value at the socket
(requires the pad be enterable — pairs with #5 or a socket-only
relaxation). Banked score stops being a one-way ratchet; "we are ahead"
becomes a position that must be held. Comeback direct and dramatic (the
contest lands in the leader's base). **Size: M/L (~10–12 files), 0 new
published facts.** Risk: grief loops and the camping alarm — bound by the
charge ≥ 1 requirement, single-hit interruption, and shipping together
with #11's reactor ward.

### 7. Conduits — "ground control sets the exchange rate"

Two neutral Conduit tiles at rotationally fair mid-map positions. At the
instant a Core is banked, if the banking team had ≥2 living bodies within
radius 2 of a Conduit, that Core is +1 charge (both Conduits: +2). The
L2-safe form of a capture objective: pays nothing on its own, nothing at
timeout — it only makes couriering better. Priced (~120 body-ticks per
marginal charge) at or slightly above the courier. Zoning becomes a
first-class win route; siege punished hard. **Size: M (~6–8 files), 0 new
published facts** in the cheap form (presence check at bank time). Risk:
static solve — mitigate with mid-column siting, the ≥2-body requirement,
and pairing with #3.

### 8. Salvage Cells — "kills become an economy, away from the wells"

A destroyed body leaves a Cell (30-tick decay, either team collects, 1 per
carrier, gun stays usable) worth **1 charge** at the socket; no drops
within 6 tiles of the dead body's own reactor. Combat becomes a scoring
channel anywhere on the map; death carries its first persistent cost; a
genuine seventh archetype (skirmish/attrition) becomes viable. Priced
(~220+ body-ticks per charge) above the courier. **Size: L/XL (~25–35
files).** **⚠ Ruling flag:** a second scoring channel, not an upgrade
ladder — but close enough to the DECISIONS #199 family that it needs an
explicit owner ruling before building. Risk: deathball convergence —
mitigated by the home exclusion, decay, carry cap, and never shipping in
the same wave as another concentration-rewarding mechanic.

### 9. Late Surge — "the last third is worth more, for both sides"

From t=350, cadence 75 → 40 and each Well may hold 2 outstanding Cores
(optionally: post-t400 Cores worth 3 charge — but prefer rate-only). The
cleanest structural anti-snowball: changes the integral, never reads the
scoreboard, fully legible. Race nerfed; slow-building archetypes buffed.
**Size: XS–S (2–4 files)** — three additional schedule entries
(`centre-late` etc.) bound to the same sites; the 2-outstanding behaviour
falls out of two schedules sharing a site. Risk: trivialising the early
game — keep late Cores at base value, change only the rate.

### 10. Pulse Drives — "each Pulse ends a drive and resets the field"

The owner's registered hypothesis (DECISIONS #201), sharpened: on any
Pulse, a neutral kickoff reset (cores cleared, wells to a common beat,
bodies to spawn anchors, 10 neutral ticks); sheets may select a fresh
authored play at the boundary. Episodic strategy with per-episode plans;
the strongest cure for lead-compounding (deletes accumulated field
position). **Size: M/L (~10–12 files), 0 new published facts** (+
`DriveReset` event). **Competes with #1/#3** (they accumulate state, it
deletes state) — run as its own arm, never bundled, per #201's
preregistration requirement.

### 11. Bank Windup + Reactor Ward — "the last meter is a fight"

(a) Delivery requires the carrier at its socket for 2 consecutive ticks (a
1-tick tell); any interruption denies the bank. (b) Your own socket deals
1 damage per 2 ticks to enemy bodies within radius 2 (positional out,
grammar-compliant). The final tile becomes contestable and the defender
owns it — the structural answer to the "home camping dominates" alarm.
Hooks gain the game's highest-value single moment. **Size: M (~6–9
files), 0 new published facts.** Risk: keep windup at exactly 2 ticks and
the Ward at 1/2-tick, or delivery becomes a coin flip / home becomes
unattackable (which would kill #5/#6).

### 12. Resonance — "two signatures make a third thing"

A short published table (6 entries) of signature pairs with combined
effects on area overlap (Smoke ∩ Null Field = Dead Zone; Paint + Prism →
Rail shatters the wall; Hardlight adjacent Prism = Sealed Gate; Flare ∩
Trip Node = radius-4 reveal). Adds pre-match counter-drafting depth to the
public composition; rescues the three inert classes via partners rather
than buffs. **Size: M (~6–8 files), 0 new published facts.** Prefer
combos triggered by static area overlap, never 2-tick timing coordination
(the 0.13%-usage turret precedent). Lowest-confidence item: class design,
not mode design; will not by itself move reversal or cycle metrics.

### Appendix idea (not counted): Mandates

Pre-match public win-condition declarations (Courier: Pulse at 6 charge;
Siege: Pulse at 8 but basic-gun hits damage the enemy reactor; Warden:
Pulse at 8 but Cells/Taps available). The fastest route to directed
cycles, but L/XL and multiplies every balance question by three. Register;
don't build yet.

---

## 5. Deliberately not proposed

Energy/action-point pools (DECISIONS #47/#48); in-match upgrades or
score-to-power (DECISIONS #199, Frontline L7); catch-up pricing or
scoreboard-keyed anything ("a balance patch wearing a mechanic's
clothes"); a hold-the-zone objective paying the timeout channel (Frontline
L2 — #7 is the safe form); map shrink/destructible terrain (no seam,
rejected arm; #3 achieves "the map changes" at M cost); more shot-program
parameters (proven worthless in the sibling study); hidden information as
a reward (Sweep prices *removing* fog instead — the safe direction).

---

## 6. TOP-3 recommendation

> **Ripening Cores (#1) + Well Migration (#3) + Pulse Ledger (#4)**,
> with **Late Surge (#9)** as a free XS rider.

Three orthogonal axes — **WHEN** you take value, **WHERE** the game will
be, **WHAT** your score buys — forming one coherent sport: *the Well is a
place with a schedule and a growing prize, the schedule moves, and what
you do with what you bank is a choice.*

Cost is dominated by the single `ChargeValue` published fact (~15 files);
#3 needs ~9 and #4 ~13, all inside the mode driver, mode definition and
one observation record. Degeneracy is bounded by constants (ripen cap +2,
Mend cap 2 + tiebreak forfeit, BFS-asserted site fairness, rate-only Late
Surge), not by behavioural hope.

Addresses all four measured pathologies: 75% first-Pulse conversion
(target <60%), zero behind-to-ahead reversals (target >0), zero
counter-web cycles (target ≥1 — greedy ripener beats racer, racer beats
controller, controller beats ripener), 9–8 margins (granularity tripled).

Archetype differentiation under the trio: race becomes the tempo deck with
denial pickups; zoning becomes the ripening specialist with a 200-tick
planning horizon; blockade becomes read-driven and gains Lock; hooks
graduate to the classes that decide who owns the clock; mines/denial
become scheduled weapons with computable high-stakes moments; siege must
solve an eight-body allocation problem instead of an all-in.

**Do not ship Pulse Drives (#10) alongside the trio** — competing
hypotheses (accumulate vs. delete field state); #201 already requires
independent testing.

Sequencing (one axis per wave, per the balance-harness discipline):
**Wave A** Ripening + primitive (with a no-ripen control arm at
`ChargePerPulse = 6, base 2` to prove the primitive alone is inert);
**Wave B** Migration (with the BFS fairness test as a prerequisite);
**Wave C** Ledger (Mend and Sweep separately ablatable); **Late Surge** as
an XS rider wherever there is capacity.

Registered metrics per wave: first-score→win conversion, behind-to-ahead
reversals, directed counter-web cycles, charge-source mix, mean Core age
at pickup, denial-pickup rate, Mend usage and post-Mend win rate — and
all six felt-degeneracy v4 bars unchanged and unloosened.
