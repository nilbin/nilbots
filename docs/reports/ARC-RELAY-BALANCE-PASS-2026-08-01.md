# Arc Relay balance pass — 2026-08-01

## Recommendation

Keep the approved H0 class numbers and the `home-gates-wide` map profile for
the next owner watch. Do not add a comeback mechanic, score-to-power reward,
or a blanket nerf to Towline, Sunder, or any other class.

The apparent class imbalance in the earlier cohort was chiefly an authoring
confound: one doctrine made competent decisions and another did not. A shared
evaluation engine that varied composition and doctrine as deterministic data
produced competitive records and materially different play shapes without a
single class-stat change. The balance candidate is therefore:

1. H0 rules and all 16 launch-class numbers unchanged;
2. the flexible doctrine fallback that lets non-reserves contest an outer Core
   when its preferred carrier cannot;
3. stronger cohort bars that exclude visible nonsense before outcomes are
   read; and
4. a replay-validation fix for Minesmith's specified proximity reveal.

This is a balance recommendation, not a fun claim. The same-engine screen
removes an important policy-quality confound, but it is not independent-author
ship authority. The final blind gallery is the owner-review boundary.

## Guardrails

- Determinism remains the product invariant.
- No rubber-banding, economy, score-to-power, or comeback rule was introduced.
- The approved 16-class roster and eight freely selected sheet slots remain;
  no stable-of-five structure was reintroduced.
- Every outcome claim below used WASM and exact canonical replay verification.
- The sheets are provisional evaluation data for coverage and reproducibility,
  not the player-facing sheet UX.
- No new decision number was minted.

## Why the old result was not a class verdict

A controlled same-engine screen held the Interception policy constant and
changed only its eight-body composition:

| Interception engine | Composition | Mirrored record |
| --- | --- | ---: |
| shared audit policy | original doubled composition | 5-1 |
| shared audit policy | Split's diverse composition | 5-1 |
| shared audit policy | deliberately bruiser-heavy composition | 2-4 |
| old Split implementation | its original diverse composition | 0-6 |

The same diverse composition moved from 0-6 to 5-1 when the policy changed.
That is decisive evidence against interpreting the old 6-0 Interception result
as proof that its classes were overpowered. Composition still matters—the
bruiser-heavy 2-4 is useful evidence that sheets are not cosmetic—but policy
competence dominated this sample.

The shared audit algorithm builds once and consumes each evaluation sheet as
deterministic data. Its final WASM SHA-256 was
`acd71497b5beeece8752c7dc0800c775b2ec715dd6e83311c51782596c9d26b8`.
Its sources and all five study sheets are frozen with this report under
`arena-bots/arc-relay/balance-audit-v1-2026-08-01/`. The final broadcasts,
records, and scorecards remain generated evidence under `sandbox/`.

## Fixing silly play instead of scoring around it

The Gate 3.2 gallery exposed two cases that v1 called clean:

- a roughly 500-tick mutual formation freeze; and
- a 130-tick Core/carrier/tile stall.

The old off-theater passivity test excused a formation merely because it was
near a live Core. That exception did not match what a viewer saw. The frozen
v2 registration in `balance/arc-relay-felt-degeneracy-bars-v2.json` keeps the
existing handoff ping-pong and off-theater checks and adds:

| Bar | Exclusion threshold |
| --- | --- |
| formation freeze | at least 60 high-wait ticks in a 75-tick post-birth window, regardless of Core proximity or possession |
| stuck carrier | one Core held by the same life on the same tile for 30 consecutive spectator worlds |

A doctrine that trips any v2 bar is excluded before a cohort record or gallery
is read. This is eligibility, not an outcome score: it prevents passive or
cyclic behavior from masquerading as balance evidence.

Convoy needed one doctrine correction, not a stat advantage. Its reserves
remain preferred for outer-theater work, but non-reserves may now collect an
outer Core when the preferred unit cannot. In the first shared-engine
round-robin the corrected Convoy moved from 1-5 to 3-3 and all 12 matches
passed v2. Two campaign seeds reproduced these aggregate records:

| Doctrine | Record across two seeds |
| --- | ---: |
| Balanced | 8-4 |
| Convoy | 6-6 |
| Interception | 6-6 |
| Split | 4-8 |

The strategies make no seed-sensitive choices in these cells, so the second
seed proves reproduction, not independent stochastic evidence.

## Full-roster mechanics screen

The final four-sheet cohort collectively fields all 16 approved launch
classes: Hush, Kestrel, Lantern, Longshot, Mason, Minesmith, Mortar, Nest,
Palisade, Patchbay, Relay, Repulsor, Sunder, Switchback, Towline, and Veil.

The control-grid sheet puts Veil in a live screen role rather than counting it
only in composition metadata. Across its 12-cell screen, the formerly sparse
mechanics actually participated:

| Mechanic | Attempts | Useful/effect facts |
| --- | ---: | ---: |
| Falling Star | 126 | 87 |
| Hardlight Block | 115 | 39 |
| Sentinel Seed | 77 | 274 |
| Smoke Canister | 12 | tracked attempts |
| Trip Node | 107 | 71 |
| Survey Flare | 35 | information action |

The screen finished 12/12 exact-verified and 12/12 v2-eligible. Records were
Balanced 4-2, Control Grid 3-3, Interception 3-3, and Split 2-4. This is enough
to reject gross roster collapse; it is not enough to claim every pairwise
class matchup is solved.

## Minesmith replay defect

The full-roster sweep also found a real validator defect. Runtime observation
projection correctly reveals an enemy Trip Node within the contract's
`revealRange` of an observing team's body, even when the node is behind that
body and outside ordinary facing vision. Chronology and replay validators were
reconstructing visible signatures from ownership, tell phase, and ordinary
visible tiles only, so a valid Minesmith observation failed with:

`A mind observation's mode must exactly match the authoritative pre-state.`

Both validators now derive the Trip Node reveal range from the resolved or
embedded contract and apply the same proximity rule as runtime projection. A
focused regression places the node outside the enemy's visible tiles but
inside proximity range, asserts publication, and round-trips the partial
replay. This changes validation parity, not game rules or authoritative match
state.

The sweep harness' kill/fix/relaunch semantics were exercised in practice:
attempt 01 stopped on the defect; attempt 02 rebuilt from one execution surface
and completed all 12 cells.

## Final balance-review cohort

The outcome-blind final set uses seed `314159`, both assignments for each
unordered pair, the shared WASM engine, v2 eligibility, `home-gates-wide`, and
the four doctrines needed for complete launch-roster coverage.

| Doctrine | Record |
| --- | ---: |
| Balanced | 4-2 |
| Control Grid | 4-2 |
| Convoy | 2-4 |
| Interception | 2-4 |

All 12 matches verified exactly and all 12 passed every v2 bar. There were no
draws; two matches reached MaxTicks. End ticks ranged from 326 to 599, with
median 453 and mean 460.8.

First-Pulse conversion was 7/12, or 58.3%, below the 70% alert. An earlier
shared-engine cohort measured 75%, which is useful evidence that this aggregate
is cohort-sensitive. No causal power reward follows a Pulse, so changing rules
merely to force this statistic downward would be balance theater.

The doctrines also retain distinct shapes rather than converging on one safe
script:

| Doctrine | Deliveries | Pulses | Steals | allocation entropy | cross-theater transitions | mean carrier escort | total quiet ticks | max freeze window |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Balanced | 46 | 14 | 14 | .848 | 43.3 | 1.03 | 7 | 19 |
| Control Grid | 50 | 16 | 8 | .857 | 49.5 | .48 | 3 | 8 |
| Convoy | 36 | 10 | 13 | .582 | 30.0 | 1.45 | 154 | 48 |
| Interception | 47 | 14 | 27 | .688 | 68.7 | .59 | 9 | 8 |

Convoy is intentionally concentrated and therefore remains the doctrine to
watch most critically. Its longest high-wait window is 48, below the 60-tick
bar, and its longest stationary carry is four ticks. If the owner can still
find silly play in the blind set, the v2 bar is wrong and must be tightened;
the scorecard does not overrule the watch.

## Review gallery

The fresh outcome-blind gallery is generated from
`sandbox/arc-relay-final-balance-gallery-v1/gallery-input.json` and served from:

`sandbox/arc-relay-final-balance-review-lean-v1-2026-08-01-engine-1-0-5/`

| Budget | Actual | Ceiling |
| --- | ---: | ---: |
| Matches | 12 | 12 |
| Largest compressed replay | 197,491 B | 300 KiB |
| Whole gallery | 5.9 MiB on disk | 8 MiB |

The lean Gate 3 viewer deliberately shares one Canvas2D bundle, one map theme,
and omits the parked 3D renderer and external soundtrack. It retains the
carrier emphasis, event banners, “what matters now” cue, and slower Arc Relay
playback required for the legibility re-watch.

Serve it with:

```bash
python3 scripts/serve-gallery.py 8933 \
  --directory sandbox/arc-relay-final-balance-review-lean-v1-2026-08-01-engine-1-0-5
```

Then put `http://localhost:8933/` behind the repository's Cloudflare review
flow. The owner watch should answer three questions: can a human tell which
Core and carrier matter, do the four doctrines feel materially different, and
is any passivity or handoff behavior still visibly stupid?

## Verification

| Check | Result |
| --- | --- |
| .NET solution build | passed, zero warnings and zero errors |
| .NET tests | SDK 84, Guest 36, Determinism 17, Runtime.Wasm 67, CLI 110, Engine 1,355, App 181 passed; 77 external-integration tests skipped |
| Felt-scorecard tests | 7/7 passed |
| Python harness compilation | passed |
| Gate 3 lean viewer build | passed |
| Final gallery | 12/12 WASM, 12/12 exact-verified, 12/12 v2-eligible |
| Frozen golden release run | 6/6 exact expected hashes in 49.217 s with three workers; zero canonical replays retained |

Only three historical golden cells pass the new eligibility bars. That is
intentional: the golden set preserves previously completed bad behavior to
detect simulation changes, while v2 independently decides whether a new cell
may enter a balance read. Eligibility changed; all six canonical hashes did
not.

## Disposition

Advance the unchanged H0 class/rules baseline plus the validator and evaluation
bar fixes to owner review. Do not tune class stats from this cohort. If the
gallery passes, the next useful balance work is larger, independently authored
class-pair and sheet-diversity coverage; if it fails, fix the specific doctrine
or detector revealed by the replay before touching class power.
