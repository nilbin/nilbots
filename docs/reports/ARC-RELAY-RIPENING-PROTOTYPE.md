# Ripening Cores prototype — evaluation report (2026-08-05)

Binding brief: `docs/briefs/RIPENING-CORES-PROTOTYPE-BRIEF.md` (metrics
pre-registered before any evaluation game). Depth memo proposal #1,
built beside Forward Combat 03. **Recommendation: REJECT as
parameterized** — the evidence below shows take-immediately dominates
and the primitive never fires in real play, which is the brief's own
pre-registered failure condition.

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

## Recommendation: REJECT as parameterized

The primitive is inert in its control arm (as designed) and inert in
adversarial play (fatally). Any revival needs a parameterization that
makes patience purchasable rather than punished, e.g. one or more of:
accrual driven by Core age regardless of touches (no freeze), an
interval well below the Well cycle, accrual while carried, or Well
rearm decoupled from the outstanding Core. Each of those is a new
prototype with its own control arm — not a tweak to this one. The
engine plumbing (value-carrying Cores, the closed ripen fact ledger,
value grammar facts, executor discipline) is sound, fully tested, and
reusable by any future value-bearing mechanic; only the accrual rule
failed.

Campaign evidence commits: `28918c23` (fact ledger), `074c9c90` (stock
value preference + metrics script `scripts/arc-relay-ripening-metrics.py`),
`a4cb3edc` (grammar facts, executor discipline, `ripen-harvest-v1`).
