DECISION NEEDED: Review the transparent twelve-match gallery, especially
sample 07, but do not promote the evaluation grammar into the player-facing
sheet editor yet. The experiment proves that sheets can express coordinated,
causal spatial plans; it does not prove a strategically meaningful counter-web
or human fun.

# Arc Relay dynamic strategy pass — 2026-08-02

## RESULT

The proposed evolution fits under the existing sheet and ordered-gambit model.
No new entrant, commander object, rule, map tile, class value, score, economy,
comeback system, or private information channel was needed. Evaluation sheet
schema `arc-relay-evaluation-sheet-v1` can deterministically express drawn
paths, freeform regions, side-relative anchors, formations, staged waits,
rear-line ambushes, feints, pincers, rotations, cutoffs, retreats, and bounded
carrier/escort/interception policy changes.

The mechanical half succeeded:

- all four dynamic families activated on sandboxed WASM in both participant
  assignments;
- all 64 retained matches were cohort-eligible and canonical-replay verified;
- every dynamic sheet cleared the registered `0.12` execution-distinctness
  minimum, with closest distances from `0.199` to `0.249`;
- the rear-ambush doctrine staged two bodies behind the enemy line, held them
  quiet for at least six ticks, contacted a carrier from its enemy-homeward
  side, fought, and exited in both participant assignments; and
- one retained match reproduced the exact same canonical replay and compressed
  broadcast on the trusted in-process and sandboxed WASM lanes.

The depth hypothesis failed its preregistered gates. The dynamic four-family
counter-web remained transitive/tied: it produced neither a matchup reversal
relative to the static controls nor a directed three-cycle. Escort
Counterpunch lost its static twin `0-4`. Two Escort matches exposed ten visible
plan episodes for one team against the cap of eight. Weighted authored-position
adherence was `29.8%` to `68.2%` by plan, below the `75%` gate. The behavior is
different, visible, and costly, but this cohort does not establish that the
new choice layer creates worthwhile opponent-dependent strategy.

No fun claim is made. Keep schema v1 and stock mind v2 as evaluation tooling;
do not design the product editor around them yet.

## EVIDENCE

### 1. Contract and deterministic grammar

The concise experiment contract is
[`docs/EXPERIMENTAL-ARC-RELAY-DYNAMIC-STRATEGY.md`](../EXPERIMENTAL-ARC-RELAY-DYNAMIC-STRATEGY.md).
The frozen hypothesis, pair block, gates, prohibited changes, and disclosed
development repairs are in
[`balance/arc-relay-dynamic-strategy-v1.json`](../../balance/arc-relay-dynamic-strategy-v1.json).

A sheet remains the only authored strategy object. Its v1 additions are:

| Surface | Meaning |
| --- | --- |
| `paths` | ordered side-relative waypoints with hold, region, or base-assignment arrival behavior |
| `zones` | named freeform rectangles used for staging, avoidance, control, and fallbacks |
| default intent | per-body position, engagement, and signature policy outside a gambit |
| scope | explicit stable unit IDs and/or base roles |
| public conditions | integer comparisons over tick, visible possession, loose Cores, visible enemies, region counts/tenure, next birth, Pulses, and Pulse deficit |
| overlay | bounded role, position/formation, carrier, escort, interception, engagement, and signature intent |

Coordinates are authored from the west side and X-mirrored for the east side.
`anchor-offset` supports own/enemy reactor, named or next Well, visible loose
Core, visible own/enemy carrier, visible opponent, partner, or ally role. A
missing public anchor takes its authored fallback region; it never reads an
opponent sheet or hidden state.

Conflict semantics are exact:

1. Ascending integer priority wins; sheet order/ID is the deterministic tie
   breaker after the encoder's canonical sort.
2. The active plan evaluates exit before entry.
3. Minimum tenure prevents exit and preemption.
4. After minimum tenure, an explicit exit or maximum tenure ends the plan; a
   currently eligible higher-priority plan may preempt it.
5. At most one transition occurs per tick. A normal exit blocks replacement
   until the following tick.
6. `rising-edge` requires a false-to-true public condition transition;
   `while-true` may enter whenever its condition remains true and cooldown has
   elapsed.
7. Cooldown is measured from exit. A scoped overlay never changes what an
   action is legally capable of doing.

The JSON is canonicalized into a little-endian `ARS1` envelope and capped at
64 KiB. Stock mind v2 requires `arc-relay-evaluation-sheet-v1`; it cannot
silently consume v0. The frozen v0 interpreter remains separate.

### 2. Versioning and historical proof

| Artifact | SHA-256 / result |
| --- | --- |
| frozen v0 mind source | `c8182e133a202733ef7c6b43367097eb118d2295a91dcdbf592e6fe13ff48f79` exact |
| frozen v0 linker | `3ca71ee3752776c5ba4d0236ced865d67939259bc60ab2d274f2b8a533390f14` exact |
| frozen v0 sheet | `010a64bf4e1241007765fcef2a230fa147a765857a0598acdc8352c0a18b6c61` exact |
| frozen v0 WASM | `c574c09a832d0a28cd1be8fd645a02685ad9c24a02543bce5c9819d5e1fd65f9` exact |
| rejected stock mind v1 WASM | `a99634cd42ad8bbf1762b27a32eabc4fb8558ab1bbd45658a9bb684bd8186774` retained, not overwritten |
| retained stock mind v2 WASM | `bc6429bf013c8fc84974e41327d87d9350e4294189f18a4e77abe78dac75b21c` |
| retained v2 manifest | `9f7d3dcc0b9436b304cab8df1045f21e9002047fff9ad30131c277b950f80d2d` |

The six historical Gate 3 cells reran on WASM in `29.900 s`; all expected
canonical hashes matched byte-for-byte:

`1661522b…`, `b0433312…`, `37d7f726…`, `28acb15c…`, `1680c38d…`, and
`cda2fdb6…`.

For the parity sample
`rear-ambush-dynamic--rear-ambush-static--s196613--a0`, both runtimes produced
canonical SHA-256
`b88586096dbbc6b96784a6f9e2c3bbc44d34a8bfb65f39358c1a74a5afaa2155`
and broadcast SHA-256
`0a86cbe8afaa9f9b24b12207e35879abcea6f8401efbc42753331349c058bd33`.
The compressed broadcasts were byte-identical; their canonical JSON SHA-256
was `d0bcc363b46c50c1d4c80e75f3c6018e67ef48f0a9de70568b0ebe3a4e352517`
over `3,516,777 B`. All outcome claims below still come from WASM.

### 3. Development repairs and rejected evidence

The registration records every opened development result. The sequence was:

1. Stock mind v1's first 64-cell in-process screen completed without faults
   but had no reversal/cycle; Well Rotation and Escort exceeded the transition
   cap, and Escort lost its static twin.
2. A cooldown/scope repair removed `MaxTicks` completions but left a transitive
   counter-web.
3. Rear-collapse produced loose Cores that persistent rear staging ignored.
   `rear-recovery` was added before retained seeds.
4. The first recovery body attacked instead of recovering; recovery was
   narrowed to one hold-fire/conserve body before retained seeds.
5. The first retained stock-v1 WASM block verified `64/64`, but four Escort
   team-1 cells tripped the unchanged stuck-carrier bar; two also tripped home
   non-progress. It is rejected for cohort reads.
6. Diagnosis found formation anchors taking precedence over existing carrier
   lane clearance. Stock mind v2 changes only that precedence. A targeted
   `4/4` WASM probe passed, then a fresh retained seed block was frozen and run.

No failed block was spliced into the retained population, and no gate moved
after an outcome was opened.

### 4. Retained execution and eligibility

The authoritative block is
[`balance/arc-relay-dynamic-strategy-retained-v2.json`](../../balance/arc-relay-dynamic-strategy-retained-v2.json):
64 WASM matches, seeds `196613` and `262147`, both assignments, four dynamic
families, their static same-composition controls, all cross-family pairs, and
the unchanged v3 felt-degeneracy bars.

| Check | Result |
| --- | ---: |
| canonical replay verification | 64 / 64 |
| cohort eligible | 64 / 64 |
| runtime faults / disqualifications | 0 / 0 |
| compressed broadcast total | 8,706,666 B |
| largest compressed broadcast | 181,207 B (limit 300 KiB) |
| largest durable record + broadcast | 183,693 B |
| wall time / jobs | 214.097 s / 4 |
| mean cell execution + verification | 13.004 s |
| stock v2 artifact | 4,199,693 B |
| v1 source sheets | 9,624–13,254 B JSON (64 KiB encoded ceiling) |

This is offline audit cost, not hosted match latency. The fast development
screens completed 64-cell blocks in roughly 65 seconds; only the fresh WASM
block supplies the retained read.

All four new sheets pass the outcome-independent execution-distance gate:

| Dynamic sheet | Closest control | normalized RMS distance |
| --- | --- | ---: |
| Escort Counterpunch | Escort static | 0.217 |
| Feint Pincer | Feint static | 0.199 |
| Rear Ambush | Rear static | 0.249 |
| Well Rotation | Well static | 0.201 |

The minimum was `0.12`. Distinct execution is necessary evidence, not proof of
depth or quality.

### 5. Plan diagnostics

Every required plan activated. The adherence proxy counts a scoped body-tick
as adherent when the body is inside the declared region or reduces shortest
path distance toward it.

| Plan | participants activated | visible episodes | scoped body-ticks | weighted adherence | objective-presence body-ticks surrendered |
| --- | ---: | ---: | ---: | ---: | ---: |
| `deep-cutoff` | 12 / 16 | 22 | 1,390 | 45.3% | 772 |
| `escort-column` | 16 / 16 | 78 | 2,850 | 29.8% | 2,104 |
| `prebirth-rotation` | 16 / 16 | 96 | 1,948 | 68.2% | 460 |
| `rear-collapse` | 16 / 16 | 44 | 2,220 | 54.2% | 1,922 |
| `rear-recovery` | 14 / 16 | 42 | 346 | 52.6% | 228 |
| `south-pincer-release` | 16 / 16 | 74 | 2,698 | 44.6% | 1,414 |

The opportunity cost is measurable for every family, so plans are not merely
labels. Adherence nevertheless misses the registered `75%` bar. Combat and
legal action arbitration frequently consume the same body-ticks the spatial
overlay intends to spend moving or holding. Two Escort-vs-static assignment-1
matches show ten plan episodes (`4` deep cutoffs plus `6` escort columns), so
the eight-transition cap also fails.

The replay-backed rear proof is stronger. Across all 16 retained appearances,
both infiltrators reached rear staging and stayed quiet for six ticks; all 16
activated, moved, and fought. The Feint-vs-Rear cells provide the complete
proof in both assignments:

| Assignment | staging / quiet | first homeward-side contact | action | exit observed |
| --- | --- | --- | --- | --- |
| Rear team 1 | both bodies, >= 6 ticks | tick 174, `(8,13)` against carrier `(10,13)` | tractor hook | yes |
| Rear team 0 | both bodies, >= 6 ticks | tick 57, `(21,6)` against carrier `(17,6)` | tractor hook | yes |

This proves flexible positioning beyond theater labels. It does not prove that
the sacrifice is strategically sound.

### 6. Static controls and counter-web

Each line below covers two seeds and both participant assignments (`4` games).

| Pair | static result | dynamic result |
| --- | ---: | ---: |
| Rear vs Well | Well 4–0 | Well 4–0 |
| Rear vs Feint | Feint 4–0 | 2–2 |
| Rear vs Escort | Escort 4–0 | Escort 4–0 |
| Well vs Feint | Well 4–0 | 2–2 |
| Well vs Escort | Well 4–0 | Well 4–0 |
| Feint vs Escort | Feint 4–0 | 2–2 |

Dynamic play flattened three static edges to assignment-correlated `2–2`, but
it reversed none and created no strict directed cycle. The registered strategy
counterplay gate therefore fails.

Within-family dynamic versus static results were Rear `2–2`, Well `2–2`, Feint
`2–2`, and Escort dynamic `0–4`. The largest visible costs align with the two
weakest facts: Rear Collapse surrendered `1,922` objective-presence body-ticks
while Well continued to win `4–0`, and Escort Column combined `29.8%` adherence
with a `0–4` same-family loss. These are correlations from the registered
block, not causal mechanic claims.

The result supports the null more than the alternative: overlays alter
execution, but the present stock action arbitration and authored plans do not
turn those differences into a robust opponent-dependent payoff structure.

### 7. Transparent review gallery

Owner review is deliberately outcome-transparent:

https://corrections-even-improve-natural.trycloudflare.com

Every card names both doctrines, the active plan(s), winner, terminal reason,
and end tick. This supersedes the original outcome-blind viewing convention at
the owner's request. The URL is a public, unauthenticated temporary Cloudflare
quick tunnel.

The twelve-match order and base selection were frozen before outcomes in
[`balance/arc-relay-dynamic-strategy-gallery-v1.json`](../../balance/arc-relay-dynamic-strategy-gallery-v1.json).
After unblinding, one redundant Rear-vs-static cell was transparently replaced
with a retained WASM Feint-vs-Rear cell that satisfies all five rear-ambush
proof facts. This makes the review set curated and outcome-aware; the gallery
is not used for any population or outcome claim.

| Gallery fact | Result |
| --- | ---: |
| matches | 12 |
| cohort eligible / verified source cells | 12 / 12 |
| compressed broadcasts total | 1,719,942 B (limit 8 MiB) |
| largest compressed match | 181,206 B (limit 300 KiB) |
| transparent input SHA-256 | `9898d5f6e537c14cb59362c144438e0484701b1ff660561477efde84b62e820d` |
| transparent cards SHA-256 | `9f10322c560aac7232984e46595f390b22c8f7c5b317eced16ad99d3d1b2d435` |
| complete rear proof | sample 07 |

### 8. Verification

| Check | Result |
| --- | --- |
| Python script tests | 32 / 32 pass |
| full .NET suite | 1,870 pass, 83 PostgreSQL-gated skips |
| Engine suite including DocDrift | 1,357 / 1,357 pass |
| CLI suite including v1 sheet tests | 116 / 116 pass |
| web tests | 356 / 356 pass |
| production web build | pass; four CLI viewers, hosted viewer, and parked 3D compile |
| historical golden hashes | 6 / 6 byte-identical |
| retained dynamic strategy | 64 / 64 eligible and verified on WASM |
| gallery sources | 12 / 12 eligible and verified |
| public delivery smoke | HTTP 200; transparent result cards visible |

Implementation/evidence commits are `2e3942db` through `a44edc29`. Report
content commit: `REPORT_CONTENT_COMMIT`.

## NEXT

1. The owner watches the transparent gallery and records whether the plans are
   understandable, consequential, and enjoyable to watch. Sample 07 is the
   strict rear-ambush demonstration.
2. Keep v1 evaluation-only. Do not spend a product UX pass on its raw JSON or
   treat it as the human sheet format.
3. If continuing this direction, first repair spatial-action arbitration and
   the Escort transition excess under the same gates; do not move the gates.
4. Commission independently authored, explicitly opponent-conditioned plans
   after that repair. Require a fresh WASM block to produce at least one
   registered reversal/cycle before reconsidering editor promotion.
5. If a second independent cohort still collapses to the same payoff order,
   close the grammar path honestly and only then consider a separately
   registered rules or objective change.
