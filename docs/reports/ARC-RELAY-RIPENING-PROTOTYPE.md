# Ripening Cores prototype — evaluation report (2026-08-05)

Binding brief: `docs/briefs/RIPENING-CORES-PROTOTYPE-BRIEF.md` (metrics
pre-registered before any evaluation game). Depth memo proposal #1,
built beside Forward Combat 03. Two parameterizations evaluated:

- **`arc-relay-ripening-01` (interval 45): REJECT** — take-immediately
  dominates and the primitive never fires in real play, the brief's own
  pre-registered failure condition. Full evidence below.
- **`arc-relay-ripening-02` (interval 12, owner-directed tuning): the
  primitive is alive** — organic ripening in every game including stock
  mirrors, value captured by both sides, lead reversals return. The
  clean-slate strategy bar remains unmet (2/10), and the charge
  attribution shows the residual gap is base-lane farming quality, not
  ripening. See "Tuned parameterization" at the end.

## Implemented rules

Two rulesets minted beside `-03`, all prior fingerprints byte-identical
(`ArcRelayRipeningTests`):

- `arc-relay-charge-value-01` (control): `coreBaseValue 2`,
  `coresPerPulse 6`, no ripening. Proves the value plumbing alone
  changes nothing.
- `arc-relay-ripening-01`: control plus `ripenIntervalTicks 45`,
  `ripenMaxValue 4`, `ripenResumeTicks 20`. A loose Core gains +1 per 45
  uninterrupted loose ticks up to 4; pickup freezes the value
  permanently upward; a drop imposes a 20-tick debt before accrual
  resumes; banking adds the Core's value; the Pulse consumes 6 and
  carries any remainder. Wells still hold their outstanding Core, so
  letting one ripen halts that Well's production (greed self-prices).
- Ripening and Threefold sockets are mutually exclusive by validation.

## Control-arm inertness (gate passed)

Stock-vs-stock mirrors on seeds 2001–2004, `charge-value-control` vs
`-03`, fresh CLI: **4/4 games identical** — same winner, end tick, and
full pulse timeline. (An earlier 3/4 reading with a seed-2003 wobble was
an artifact of a stale CLI binary and is superseded.) Doubling base
value and pulse threshold with uniform values is exactly inert.

## Observation, replay, and viewer changes

- `ArcRelayCoreState.ChargeValue` (engine + SDK, default 1): driver,
  runtime mapper, NBV2 observation codec (optional field 8, emitted only
  when ≠ 1), replay-v3 core state (`chargeValue` conditional), web wire
  mirror + strict normalizer.
- Closed fact ledger: `core-born` carries optional `chargeValue`
  (written only when ≠ 1) and a new `core-ripened` fact records every
  accrual step (ordered walk). The replay-v3 chronology validator
  threads `ChargeValue` through every Core transition and enforces the
  loose +1 progression — a forged ripen fails validation. The validator
  correctly aborted the first smoke run whose binary lacked the facts;
  that abort is what surfaced this chain.
- SDK event union, NBV2 event codec (optional field 4 on `core-born`,
  new `core-ripened`), runtime mapper, and the web fact model/normalizer
  mirror both facts. Prior profiles' replay bytes are untouched (all
  emission is conditional on non-default values).
- Sheet grammar: `visible-loose-core-value` and
  `visible-loose-core-value-in-zone` (zone-scoped max loose value;
  value-inert on prior rulesets where every core reads 1).
- Tactical executor discipline (ripening rulesets only): unassigned
  loose Cores and Wells without an open custody gate are no-stand tiles,
  so a body cannot swallow a growing Core at base value by pathing or
  birth-camping. Prior-ruleset behavior is untouched by the gate.
- Deferred to any adoption path: viewer value cue for ripened Cores
  (sphere scale/badge) and charge-pip display sized for 0–5.

## Stock baseline

Stock (value-aware pickup preference in both `AssignPickups` and the
director's nearest-loose-core order) plays take-immediately: mirror
smoke `wrip-mirror-2001` (WASM) banks 12 Cores, all at value 2, median
pickup age 7 ticks, zero ripens. Stock is the tempo-pole null
hypothesis, as intended.

## Clean-slate strategy #1: ripen-harvest-v1

Centre-well orchard (the centre zone is rotation-invariant, so absolute
custody `sourceWells` and team-relative zones agree in both
orientations): a palisade/longshot/switchback/veil wedge guards the
centre well; custody releases a pickup only when the Core is ripe or a
raider enters the 3×3 core zone; four runners farm north/south by
route. Draft trajectory, all in-process screening:

1. Threat zone 5×5, ripe at 4: won its screen but converted everything
   early — own runner birth-camped the well (fixed by the executor
   discipline above).
2. Threat 3×3, ripe at 3: 2/2 screening wins, still zero ripens — every
   centre Core was touched young (by either side), freezing value at 2.
3. Pure patience (ripe-only conversion): **loses 2/2 and still zero
   ripens** — stock takes what the orchard refuses; no Core survives 45
   loose ticks under contest.

## Pre-registered bar: 1/10 (bar not met)

Five seeds × both orientations vs ripening stock, authoritative WASM
runs (`wbar-*`), best draft (threat-convert):

| game | winner | end | banked mix | age med | ripens |
|---|---|---|---|---|---|
| w-3001 | stock | 403 | {2: 15} | 4 | 0 |
| e-3001 | **ripen-harvest** | 366 | {2: 13} | 3 | 0 |
| w-3002 | stock | 378 | {2: 14} | 0 | 0 |
| e-3002 | stock | 354 | {2: 12} | 3 | 0 |
| w-3003 | stock | 441 | {2: 15} | 1 | 0 |
| e-3003 | stock | 400 | {2: 15} | 1 | 0 |
| w-3004 | stock | 402 | {2: 15} | 1 | 0 |
| e-3004 | stock | 327 | {2: 12} | 1 | 0 |
| w-3005 | stock | 333 | {2: 12} | 0 | 0 |
| e-3005 | stock | 333 | {2: 12} | 1 | 0 |

The bar was ≥ 7/10. Result 1/10, and the single win banked every Core
at base value — even in victory the primitive never fired. Across the
entire campaign (screening + bar + mirrors, 25+ games) **not one
core-ripened event occurred in play**.

## Why take-immediately dominates (mechanism)

- **Arithmetic**: a banked Well re-births in ~26 ticks. Extracting
  immediately yields 2 charge / ~26 ticks per Well (~0.077/tick);
  waiting for 3 yields 3 / ~55 (~0.055); waiting for 4 yields 4 / ~100
  (~0.040). Because the Well freezes while its Core is outstanding,
  patience is strictly throughput-negative for whoever controls
  extraction.
- **Touch-freeze**: any pickup freezes value, and a drop adds a 20-tick
  debt. Under contest, some body touches every Core long before 45
  loose ticks elapse, so ripening is unreachable even when both sides
  fight over the same Core for 70+ ticks (observed: a Core loose/stolen
  across 73 ticks still banked at 2).
- **Camping race**: the only way to deny a birth-camper is to camp
  first, which itself swallows the birth at base value. Discipline
  concedes tempo; greed concedes nothing.

This is the brief's pre-registered FAILS condition ("take-immediately
… dominates the value curve"), demonstrated from both directions: the
best tempo-respecting draft wins nothing it wouldn't win on combat
quality alone, and the best patience draft loses while still never
ripening.

## Degeneracy bars

All 10 bar games projected to broadcast and scored against the frozen
v4 bars: **20/20 team-slots cohort-eligible, zero trips**. No detector
was adjusted at any point.

## Canonical-hash proof and suites

`ArcRelayRipeningTests` pins: both new fingerprints distinct, `-03`
re-derives byte-identically beside them, `coreBaseValue`/`ripen*`
appear canonically only in the new documents, and the driver-level
ripen/freeze/bank+remainder behaviors. New test pins the `core-born`
value and single `core-ripened` emission. Suites at report time: engine
**1382/1382 pass** (includes replay verification), web **404/404 pass**
+ clean typecheck, CLI/solution build clean.

Cross-runtime: all 10 bar games and the stock mirror re-run in WASM
with **identical winners, end ticks, and metrics**; replay documents
are byte-divergent from in-process (the known tactical-mind pattern
this session). WASM results are the authoritative numbers above;
in-process runs are labeled screening.

## Gallery

Outcome-visible, curated, current 3D presentation:

**https://minerals-relying-duke-defendant.trycloudflare.com**

- sample-01 — bar w-3002: stock birth-camps to a median pickup age of
  zero; the structural bind in one game.
- sample-02 — stock mirror: the tempo null hypothesis; 12 banks, all
  value 2, zero ripens.
- sample-03 — patience draft (screening, disclosed): the orchard
  refuses unripe Cores and stock simply takes them.
- sample-04 — bar w-3004: the closest loss, two lead reversals, Cores
  stolen back and forth yet always banked at 2.
- sample-05 — bar e-3001: the sheet's only win — earned by combat
  quality, with the primitive still silent.

## Recommendation on -01: REJECT as parameterized

The primitive is inert in its control arm (as designed) and inert in
adversarial play (fatally). Any revival needs a parameterization that
makes patience purchasable rather than punished. The engine plumbing
(value-carrying Cores, the closed ripen fact ledger, value grammar
facts, executor discipline) is sound, fully tested, and reusable; only
the accrual rule failed.

## Tuned parameterization: arc-relay-ripening-02 (owner-directed)

The -01 arithmetic dictates the fix: patience is a real choice only
when the accrual rate sits near the well-cycle rate. Minted beside -01
(fingerprints pinned, -01 re-derives byte-identically):
`ripenIntervalTicks 12` (~0.083 charge/tick waiting vs ~0.077 cycling),
`ripenMaxValue 4`, `ripenResumeTicks 8` so contested standoffs still
escalate. No engine changes — the -01 plumbing is fully parameterized.

**The primitive is alive on -02.** Stock mirror (seed 2001, WASM
outcome-confirmed): banks `{2: 8, 3: 2}` with 2 organic ripens — even
take-immediately now harvests value from delayed pickups. Every
evaluation game shows 1–5 ripens, 3s and 4s banked by both sides, and
lead reversals reappear (3 of 10 bar games).

**Strategy bar: 2/10 (unmet).** `ripen-harvest-v1` draft 6
(micro-patience: centre custody converts at value ≥ 3, i.e. twelve
ticks of guarded waiting; 4-guard wedge; drafts with threat-gating,
value floors under threat, and a thinner 3-guard orchard all screened
worse). Authoritative WASM, all 10 outcomes agreeing with in-process
screening; 20/20 team-slots degeneracy-eligible, frozen bars untouched:

| game | winner | sheet charge (ripe banks) | stock charge (ripe banks) |
|---|---|---|---|
| w-3001 | stock | 11 (1) | 19 (2) |
| e-3001 | stock | 9 (1) | 18 (0) |
| w-3002 | stock | 15 (1) | 18 (0) |
| e-3002 | stock | 8 (1) | 18 (0) |
| w-3003 | stock | 8 (0) | 19 (3) |
| e-3003 | **sheet** | 19 (1) | 12 (1) |
| w-3004 | stock | 15 (1) | 19 (1) |
| e-3004 | **sheet** | 18 (2) | 8 (0) |
| w-3005 | stock | 12 (2) | 18 (2) |
| e-3005 | stock | 16 (2) | 19 (1) |
| **total** | 2/10 | **131 (12 ripe)** | **168 (10 ripe)** |

**Attribution: the sheet wins the ripening game and loses the tempo
war.** It captures more ripe banks than stock (12 vs 10) — guarded
micro-patience works as designed — but trails 37 charge in base
economy, ≈ 18 base cores lost across its two-runner outer lanes against
stock's three-body lane parties. The residual gap is generic farming
competence, not the mechanic.

**Verdict on -02: the mechanic passes its health checks and is worth
keeping on the table.** Ripening now shapes play for both sides without
any dedicated doctrine (value-aware stock harvests it passively), which
is arguably the healthiest possible integration. What remains unproven
is a strategy that *leverages* it to beat tempo play — the clean-slate
bar stays unmet, and by the campaign protocol the next move is either a
stronger base doctrine to attach the orchard to, or the owner accepting
that ripening is texture rather than a strategy axis.

Gallery updated with two -02 games (the sheet's decisive ripe-capture
win and stock's triple-ripe harvest):
**https://minerals-relying-duke-defendant.trycloudflare.com**

### Lane-fix addendum (owner-directed follow-up)

The owner chose one bounded iteration on the outer-lane economy before
re-running the bar. Three allocation variants screened against draft 6's
2N/2S split (which barred 2/10):

- **Rotation** (all 4 runners as one squad, alternating lanes on
  zone-clearance, orientation-safe zone facts only): 0/4, fast losses.
  The rotation itself worked — the squad oscillated cleanly every ~30
  ticks — but wells cycle every ~26, so stock's persistent lane parties
  collected a free birth per lane per swing and a third of squad time
  went to transit. Rotation loses to persistent presence.
- **Concentration** (permanent 4-runner north overload, south
  conceded): 0/4. Stock's north party only needs to delay four runners
  while its south party farms uncontested.
- **Asymmetric 3S/2N with a 3-guard orchard** (tested earlier as draft
  7): 0/4 — thinning the orchard forfeits the centre.

Conclusion: the base-economy gap is not fixable by allocating 4 runners
against stock's 6 lane bodies. The binding constraint is the orchard
concept itself — 4 bodies on centre against stock's 2–3 does not earn
back its cost even with superior ripe capture, and every attempt to
shrink it broke the centre. The bar therefore stands at **2/10** with
draft 6, and further progress requires a different strategy concept
(or accepting -02 as texture), not more tuning of this one. The sheet
is reverted to draft 6 as the campaign's best-of-record.

Campaign evidence commits: `28918c23` (fact ledger), `074c9c90` (stock
value preference + metrics script `scripts/arc-relay-ripening-metrics.py`),
`a4cb3edc` (grammar facts, executor discipline, `ripen-harvest-v1`),
`72c4c271` (-01 report), `0227a223` (-02 mint + draft-6 sheet),
`b30325d7` (-02 report section).
