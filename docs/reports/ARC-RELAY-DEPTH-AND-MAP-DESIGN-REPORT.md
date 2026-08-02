DECISION NEEDED: Watch the fresh baseline-versus-counterflow gallery and choose whether counterflow's fair asymmetry is worth another design loop (default: hold the compact symmetric map and do not enlarge it).

RESULT: HOLD. The larger map is rejected by this study. Counterflow is the strongest watch candidate: it is exactly side-balanced, increases contested pickups, and preserves pacing, but it does not meet the preregistered depth gates. This evidence cannot establish that Arc Relay is fun for humans or deep enough for a season.

EVIDENCE: All 96 main cells and all 24 counter-web cells verified exactly under WASM. Counterflow finished 16–16 and raised contested-pickup share from 13.21% to 16.01%, but first-Pulse conversion remained 75%, behind-to-ahead Pulse reversals remained 0, 14/16 mirrored matchups were entrant sweeps, and four complete three-entrant counter-webs produced zero directed cycles. The larger arm made four entrants trip felt-degeneracy bars.

NEXT: No rules or official-map change runs without owner input. After the watch, the recommended next loop is fresh v3-clean doctrine/grammar work; only if momentum still locks should a new preregistered study isolate symmetric objective-flow changes. No leader-specific comeback mechanic, economy, or score-to-power is proposed.

# Arc Relay depth and map-design study

Date: 2026-08-02  
Branch: `codex/game-redesign`  
Study registration: `balance/arc-relay-depth-map-design-v1.json`  
Study registration SHA-256: `df2cf981dab43dfb464e45484b2224eecc2538e4f6a73f619f002aacdae525cd`

## 1. Question and limits

The product question was deliberately broader than balance: does the current
Arc Relay ruleset expose enough understandable, reversible, and varied
strategy to justify confidence that a human could enjoy learning it? The map
questions were treated as competing hypotheses:

- **larger:** more separation might make allocation and rotation meaningful,
  or might merely add empty travel and carrier stalls;
- **counterflow:** fair rotational asymmetry might create route reading and
  lane character, or might simply expose the same transitive strategies on a
  less familiar layout;
- **null:** neither geometry change repairs the underlying depth concern.

This was preregistered before candidate outcomes were opened. It preserved
the approved product invariants: no economy, no score-to-power, sixteen
classes, eight sheet slots, two copies per class, deterministic canonical
replays, broadcast secrecy, and no comeback mechanic. The evaluation sheets
are provisional audit instruments, not the player-facing sheet format.

Passing a metric is not a fun claim. Shared-stock sheets isolate geometry for
one policy; they do not approximate independent human invention. Owner replay
review remains the entertainment evidence.

## 2. Arms and causality

All main arms use the same 16 style-spanning pairs, both assignments, seed
`104729`, the same frozen stock algorithm, and the same rules fingerprint
`f6d3ee9b1bb17d7bd8d0981941fd00a6a96f0e7ef834497d11924c06087174eb`.
Only map geometry and the map-native coordinate data differ.

| Arm | Geometry | Passable | Symmetry / route fact | Map fingerprint |
| --- | --- | ---: | --- | --- |
| Same-cell baseline | 31×23 | 537 | vertical mirror and 180° rotation; every Well is 13 eight-way tiles from either reactor | `06bebb1f24c1d797569c2e7446fa78d61ea25cefe7a68305d799114fb5dbc1c2` |
| Larger | 31×29 | 687 | vertical mirror; centre route 14, outer routes 15, equal between teams | `84873e9a4696e147e0bff467fa2e27da48a0d233781b70030200ae98910cab4e` |
| Counterflow | 31×23 | 537 | exact 180° rotation; 24 vertical-mirror mismatches; every route remains 13 | `5ca7d1a1826791d736465d352c1558793846fc2e3df343d730f1df4c79f47e0c` |

Counterflow is asymmetric in lane character, not in competitive opportunity:
rotating the board 180° swaps the two sides exactly. Larger deliberately
stays inside the existing 32×32 engine ceiling. Both profiles are additive
experimental profiles; neither replaces the official map.

The 32 sheets are distinct compositions/plans selected for style coverage,
not twenty cosmetic aliases. Their labels include convoy, interception,
three-Well race, centre phalanx, outer pincers, fortress counterattack,
displacement control, hook burst, mortar wheel, control grid, smoke convoy,
sensor grid, repair web, rail screen, pod lattice, and feint/switch families.
Distinct declarations are not proof of distinct effective strategy; the
counter-web result below is the relevant check.

## 3. Registered gates

The same-cell baseline below is the valid comparator. The previously opened
160-match aggregate (71.25% first-Pulse conversion) motivated the study but
was not substituted for this paired block after outcomes.

| Gate | Baseline | Larger | Counterflow | Ruling |
| --- | ---: | ---: | ---: | --- |
| Exact WASM verification | 32/32 | 32/32 | 32/32 | pass all |
| Match-level felt eligibility | 32/32 | **28/32** | 32/32 | larger hard fail |
| Entrant-clean cohort retention | 32/32 | **24/32** | 32/32 | larger hard fail |
| MaxTicks | 2/32 | 1/32 before exclusions | 1/32 | pass (≤10%) |
| Team 0 / team 1 / draw | 16 / 15 / 1 | 16 / 12 / 0 eligible | **16 / 16 / 0** | counterflow fairness pass |
| First Pulse converts | 81.25% | 75.00% match-eligible | **75.00%** | both fail `<70%` |
| Behind→ahead Pulse reversals | 0 | 0 | **0** | both fail required ≥2 |
| Matches with any Pulse lead change | 7 | 8 | **10** | counterflow improves, but not enough |
| Coarse openings at ticks 25 / 75 / 125 | 50 / 62 / 63 | 48 / 56 / 54 | **47 / 63 / 64** | counterflow fails no-contraction at tick 25 |
| Theater-allocation entropy, mean | .781 | .798 | **.790** | pass |
| Delivery-source entropy | 1.000 | .995 | **.996** | pass |
| Smallest Well delivery share | 29.8% | at least 20% | **29.7%** | pass |
| Contested-pickup proxy | 13.21% | 13.15% | **16.01%** | pass; +21.2% relative vs baseline |
| Median / p90 end tick | 403 / 463 | 415.5 / 481 | **402.5 / 477** | pass |
| Delivered route-stretch p90 | 1.615 | 1.357 | **1.385** | pass |
| Centre share of body damage | 60.06% | 58.54% | **59.05%** | modest redistribution only |

The larger arm's four match-level failures were not numerical noise:

- `centre-phalanx`: 33 stationary/home non-progress carrier ticks;
- `null-veil`: 159 stationary and 157 home non-progress ticks;
- `pod-lattice`: 32 stationary/home non-progress ticks;
- `sensor-grid`: 370 stationary/home non-progress and 68 high-Wait ticks.

Entrant-level eligibility correctly removes every other cell involving those
entrants, leaving 24/32 for a cohort read. This is why the larger-map verdict
is reject rather than “slightly slower.” It creates more room without creating
recoverable decisions, and makes already-fragile policies fail the felt bar.

Counterflow passes every safety, fairness, pacing, contest, route, theater,
and entropy gate. It fails the gates that directly motivated the work:
opening diversity contracts early, first-Pulse conversion stays above the
alert, and a trailing side never becomes the leader after a Pulse.

## 4. Counter-web and effective diversity

A separately frozen, outcome-blind adversarial block used seed `130363` and
four complete triads (every unordered pair in both assignments, 24 cells):

- centre phalanx / outer pincers / three-Well race;
- control grid / hook burst / mortar wheel;
- courier sprint / fortress counterattack / interception;
- mine crescent / sensor grid / smoke convoy.

All 24 cells were eligible and exactly verified. Team results were 12–11 with
one draw, so the read is not a side-bias artifact. However:

| Counter-web signal | Result |
| --- | ---: |
| Directed three-cycles | **0 / 4 triads** |
| Mirrored entrant sweeps | **9 / 12 pairs** |
| Mirrored splits | 2 / 12 |
| Pairs including a draw | 1 / 12 |
| First-Pulse conversion | 79.17% |
| Behind→ahead Pulse reversals | 0 |
| Median / p90 end tick | 385 / 491 |

This is evidence of strong transitive dominance in the current stock
execution, not proof that all possible human minds are transitive. It does
show why adding more sheet names alone cannot answer the depth question.

## 5. Rules-native compatibility read

The registered four-artifact native cohort (`convoy-fresh`, repaired
information/route control, interception, split control) was rerun on both the
baseline and counterflow maps under felt-degeneracy bars v3.

| Read | Baseline | Counterflow |
| --- | ---: | ---: |
| Exact verified | 12/12 | 12/12 |
| Match-level eligible | 10/12 | 7/12 |
| Entrant-clean retained | **2/12** | **2/12** |
| First-Pulse conversion, match-level eligible | 90% | 100% |
| Pulse lead-change matches | 1 | 0 |
| Behind→ahead Pulse reversals | 0 | 0 |

Convoy Fresh and information/route control trip v3 on both maps, so the
cohort is stale for a product verdict. Counterflow exposes more individual
failures, but it did not create the underlying deficiency. The two retained
cells are too small to support a comparative native-population claim.

## 6. Classes and moment-to-moment activity

The counterflow main block exercised all sixteen launch classes and all
sixteen signatures. Exact opening trajectories were unique for all 64 entrant
sides through ticks 25, 75, and 125; the coarser, identity-free archetype read
is the stricter 47 / 63 / 64 table above. The block contained 1,867 pickups,
137 steals across 29 matches, 15 forced-carrier displacements, 33 Arc Toss
landings, and contested pickups in all 32 matches. This is not an inactive
game.

Signature exposure is nevertheless uneven. Arc Toss was attempted in 28/62
eligible side-games containing Relay, while Tractor Hook was attempted in
34/34 and Vector Dash in 31/32. Repair Beam was countered or replaced on
193/198 attempts. There were zero same-team carrier handoffs in the 32-cell
main block. These are policy-and-composition-confounded observations, not
class-balance verdicts, but they point toward execution grammar as a better
next lever than more map area.

## 7. Runtime, determinism, and performance

The bulk screen used the trusted in-process lane only with the registered
first-party stock source project. It never emitted canonical replays and is
not cohort evidence. Every retained cell was rerun in the sandboxed WASM lane.

| Arm | In-process screen, 32 cells | Canonical WASM, 32 cells | Result parity |
| --- | ---: | ---: | ---: |
| Baseline | 46.494 s | 116.204 s | 32/32 winner/reason/end tick |
| Larger | 51.175 s | 125.491 s | 32/32 winner/reason/end tick |
| Counterflow | 45.267 s | 108.429 s | 32/32 winner/reason/end tick |

The screen therefore remains useful for gross rejection, while WASM remains
the only release evidence. The `.wasm` path was also checked against the
runtime boundary: asking the screen command for in-process execution with a
WASM artifact correctly kept it sandboxed; the true in-process comparison
required the frozen first-party source project.

The six-cell historical golden suite ran in 22.514 s with six workers. It
verified 6/6 expected canonical replay hashes byte-identically and retained
zero canonical files after verification:

| Golden cell | Expected and actual canonical SHA-256 |
| --- | --- |
| Convoy / Information Route, assignment 0 | `1661522b6eb3af8f05834f74c6665c69618ca142c5bba4dee26c7b190edd2f0e` |
| Convoy / Interception, assignment 1 | `b0433312f8f2188435b086bce139eabb9d5618411d12cde53b40584a4a9eafbb` |
| Convoy / Split Control, assignment 0 | `37d7f726b992b606745246a493e93f93d6a0608608f993fde2421645c4dfa27c` |
| Information Route / Interception, assignment 1 | `28acb15cadb60ecdf2fd0988e794af0c44d69893e5d1374a580031b39d399966` |
| Information Route / Split Control, assignment 1 | `1680c38dea521c2b2951f63bc025c0b28f61836d39625b5b644368ab27987605` |
| Interception / Split Control, assignment 0 | `cda2fdb628ef71e5a523cd196031c490d2dc4b2d696b8e4f0376af2be50e2b20` |

Main WASM runs likewise verified 96/96 canonical replays exactly. Their
result manifests are:

| Run | Result SHA-256 |
| --- | --- |
| Baseline | `b1d923a57e4505be896bd7f363876c44c6a5d614f2f5bc9c6adb3bf504163e20` |
| Larger | `996ec10edb48234b3a4e82635b9b8645e9a87a8090815c015bed1c28401bd02f` |
| Counterflow | `34ec805f961c000ae2617a0f7be17704d3fca447183419bf92eeddccceb9c42c` |
| Counter-web | `fb2202ede4a8f869a909e0817003f2f3b6a962cd60ce19b8ca53171eab8c3a7b` |

## 8. Fresh outcome-blind gallery

The first six entries from each arm's independently frozen review order were
selected before outcomes, then hash-shuffled together and materialized only
after all eligibility checks passed. This creates a 6-baseline / 6-counterflow
comparison without choosing matches for their results. The gallery index
contains no outcome, score, duration, or termination reason.

| Gallery fact | Result |
| --- | ---: |
| Matches | 12 (6 baseline, 6 counterflow) |
| Eligible / exact verified | 12 / 12 |
| Runtime | WASM |
| Smallest / largest compressed match | 96,622 B / 213,740 B |
| Per-match limit | 300 KiB — pass |
| Whole hosted gallery | 6,602,721 B (6.30 MiB) |
| Whole-gallery limit | 8 MiB — pass |
| Baseline blind input SHA-256 | `4df74d1b0ad37633e0d956bde7fabb7bc7fa5ccf4414b230211ef5f90563c918` |
| Counterflow blind input SHA-256 | `b0b0bc3e05444a000c00b9e58f8899eee6a6162a9344a9dfd2fb6606158b0daf` |

The gallery uses the tracked Gate-3 production review build: the same
Canvas2D renderer and Ember Forge assets as production, with only the dormant
3D renderer, unrelated map themes, and external adaptive soundtrack excluded
from the review package. Arc Relay event SFX remain present. A headless
production-browser smoke loaded a real sample, drew one canvas at tick 0/357,
and observed zero page, console, or failed-resource errors.

Copy integrity was checked on sample 01: gallery broadcast SHA-256
`61825450505e4087e2d3fedab7e24b88642c506d1308d911231425ad59373855`
matches the source match record exactly. That cell's canonical replay hash
`a720f06c9f49f69fc14bcdc56b45ef53ffa2110538c39e36a227b592e7d463d5`
was verified before pruning and is unchanged in the result manifest.

Local gallery root:
`sandbox/arc-relay-depth-map-counterflow-gallery-v1/site-budget-comparative`.

## 9. Product read

### Is it deep enough to claim human fun?

Not yet. There is meaningful activity, class expression, three-theater use,
and variation in exact openings. Those are necessary. The present evidence
also shows three structural warnings across shared-stock, counter-web, and
native compatibility reads:

1. the first meaningful score event still predicts the winner too often;
2. no tested match turned a Pulse deficit into a later Pulse lead;
3. most mirrored pairings are sweeps and the targeted triads form no cycles.

That combination is consistent with games that look busy while the strategic
answer is already settled. It does not prove that humans would dislike the
game; only the owner watch and later human/custom-mind play can answer that.

### Should the map be larger?

No, not on this evidence. Larger fails eligibility, slows the upper pacing
tail, contracts opening diversity, and creates no reversals. More arena is not
currently more game.

### Should the map be asymmetric?

Potentially, but only in the fair counterflow form tested here. Exact 180°
rotational symmetry preserves equal opportunity while the non-mirrored cover
creates different-looking route decisions. It materially increases contest
without a pacing penalty. That makes it the correct gallery finalist. It does
not earn promotion because it misses the depth gates.

### Recommended design order

If the owner finds counterflow more readable or engaging, keep it as a
candidate and first author a fresh, v3-clean doctrine population with better
adaptive execution grammar—especially carrier release, pressure response,
and plan switching. Rerun the counter-web on new seeds. If reversals remain
zero with independently competent entrants, preregister a rules isolation
around symmetric objective flow (for example, contest/bank interaction or a
neutral post-score objective-state reset). Such a mechanism must affect both
sides identically; it is not a comeback bonus and cannot convert score into
power.

## 10. Verification

| Check | Result |
| --- | --- |
| Candidate map/scorecard targeted tests | 11/11 pass |
| Depth reducer tests | 2/2 pass |
| Engine + DocDrift suite | 1,357/1,357 pass |
| Full solution build | pass, 0 warnings / 0 errors |
| Web tests | 356/356 pass |
| Full production web build | pass, including CLI viewers and parked 3D |
| Gate-3 production review build | pass |
| Six historical golden hashes | 6/6 byte-identical |
| Main canonical WASM verification | 96/96 exact |
| Counter-web canonical WASM verification | 24/24 exact |
| Gallery browser smoke | pass, zero failed resources/errors |

No numbered decision is minted here. The outcome is a study HOLD awaiting the
owner's watch.
