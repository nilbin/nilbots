DECISION NEEDED: Re-watch the fresh twelve-match outcome-blind gallery and rule
whether Gate 3 passes or loops again. The loop work is complete, but the
first-Pulse conversion alert did not clear: the recommended geometry variant
reduced the same-cohort read from 100% to 83.3%, still above the registered 70%
bar. The gallery is the authority on legibility and felt degeneracy. No fun
claim is made here.

# Gate 3.2 — Phase C/D loop report

## RESULT

The Gate 3 loop now has a tracked, concurrent, resumable sweep harness; six
byte-exact golden replay cells; one-build/many-sheet evaluation delivery; felt
degeneracy eligibility bars; a redesigned evaluation-grade gambit grammar; two
native campaign-seed reads; an independently re-authored Convoy doctrine; and
a fresh outcome-blind gallery using the provisionally recommended
`home-gates-wide` geometry.

Determinism remained the product invariant. The final six-cell WASM golden run
reproduced all six canonical replay SHA-256 hashes byte-identically and retained
zero canonical replays. Parallel execution reduced that sweep from `121.518 s`
at one worker to `41.259 s` at four workers (`2.95x`). A later three-worker
release check again matched all six in `49.614 s`.

The first-Pulse isolation did not find a registered factor that clears the 70%
alert. `home-gates-wide` is recommended only as the least-damaging directional
candidate: it produced 5/6 conversion (`83.3%`) rather than H0's 6/6, removed
the MaxTicks cell, and improved mean end tick from `407.8` to `384.3`. No
comeback mechanic was introduced. This recommendation is provisional pending
the owner's re-watch.

The previous gallery's degenerate play is now mechanically visible. The old
Convoy trips the new handoff ping-pong bar; the original Information/Route
Control trips sustained passivity. Neither enters a new population read or
gallery. The fresh clean-room Convoy reduced six-match handoffs from 439 to 3
and passes both bars, but went 0-6 on each of two campaign seeds. The silly
handoff loop was implementation-specific; the competitive Convoy collapse was
not repaired by this independent implementation.

Spectator storytelling is a functional part of this loop build. Both renderers
now emphasize the carrier; the viewer names Core birth, steal, drop, bank, and
Pulse beats; a persistent cue says what currently matters; and Arc Relay starts
at `0.5x` (`2.5` presentation ticks/second) rather than `1x`. Browser smoke
confirmed the live cue and event banners. Whether those changes make a match
legible is reserved to the gallery ruling.

The sheet schema used below remains **provisional evaluation data**. It is
designed for audit coverage, deterministic hashing, and reproducibility. It is
not the player-facing sheet schema and does not settle what a human draws,
edits, or unlocks.

## EVIDENCE

### 1. Runtime and study boundary

| Surface | Final evidence |
| --- | --- |
| Engine | generic-mind engine `1.0.5` |
| Outcome-claim runtime | WASM for every Pulse, gambit, cohort, and gallery claim |
| Golden set | six completed Phase D cells, frozen in `balance/arc-relay-gate3-golden-v1.json` |
| Sweep harness | `scripts/arc-relay-sweep.py`: N concurrent cells, immutable per-cell attempts, replay count and verification, resume, and kill/fix/relaunch |
| Plan freeze | `scripts/arc-relay-sweep-plan.py`: hashes entrants, sheets, contracts, topology, eligibility bars, and optional outcome-blind review order before outcomes |
| Verification | batched end-to-end canonical regeneration; durable broadcasts gzip-compressed; verified canonical replays pruned |
| Eligibility | a doctrine or sheet side that trips either felt bar is excluded before any cohort read or gallery |
| Final native reads | two 12-match mirrored round robins, campaign seeds `104729` and `130363` |
| Fresh gallery | 12 WASM matches, seed `161803`, blind order seed `20260802`; plan frozen before outcomes |

The golden set deliberately includes old ineligible doctrine behavior because
its job is regression detection, not population admission. Eligibility is
evaluated separately and gates all new reads.

### 2. Speed and golden-hash proof

#### Parallel sweep

| Run | Workers | Cells | Wall time | Relative | Exact expected hashes | Runtime |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| Serial baseline | 1 | 6 | `121.517983 s` | `1.00x` | 6/6 | WASM |
| Parallel measurement | 4 | 6 | `41.259070 s` | `2.95x` | 6/6 | WASM |
| Final release check | 3 | 6 | `49.614319 s` | `2.45x` | 6/6 | WASM |

Every cell stores its runtime, attempt, match-record hash, broadcast hash,
scorecard hash, expected and actual canonical hash, verification count, and
eligibility result. Interrupted work resumes at completed cells; a failed cell
gets a new immutable attempt rather than overwriting evidence.

#### Byte-identical golden set

| Frozen cell | Expected canonical SHA-256 | Final actual SHA-256 |
| --- | --- | --- |
| Convoy–Information/Route, assignment 0 | `1661522b6eb3af8f05834f74c6665c69618ca142c5bba4dee26c7b190edd2f0e` | `1661522b6eb3af8f05834f74c6665c69618ca142c5bba4dee26c7b190edd2f0e` |
| Convoy–Interception, assignment 1 | `b0433312f8f2188435b086bce139eabb9d5618411d12cde53b40584a4a9eafbb` | `b0433312f8f2188435b086bce139eabb9d5618411d12cde53b40584a4a9eafbb` |
| Convoy–Split Control, assignment 0 | `37d7f726b992b606745246a493e93f93d6a0608608f993fde2421645c4dfa27c` | `37d7f726b992b606745246a493e93f93d6a0608608f993fde2421645c4dfa27c` |
| Information/Route–Interception, assignment 1 | `28acb15cadb60ecdf2fd0988e794af0c44d69893e5d1374a580031b39d399966` | `28acb15cadb60ecdf2fd0988e794af0c44d69893e5d1374a580031b39d399966` |
| Information/Route–Split Control, assignment 1 | `1680c38dea521c2b2951f63bc025c0b28f61836d39625b5b644368ab27987605` | `1680c38dea521c2b2951f63bc025c0b28f61836d39625b5b644368ab27987605` |
| Interception–Split Control, assignment 0 | `cda2fdb628ef71e5a523cd196031c490d2dc4b2d696b8e4f0376af2be50e2b20` | `cda2fdb628ef71e5a523cd196031c490d2dc4b2d696b8e4f0376af2be50e2b20` |

The final run verified 6/6 scratch canonical replays and retained 0. An
optimization that changes any value in this table is a defect.

#### Build once, vary sheets as data

The frozen stock algorithm now builds once. Each validated evaluation sheet is
canonicalized, hashed, encoded as deterministic `ARS1` participant-local start
data, and supplied through the additive `MindStart.EvaluationData` SDK field.
The match record still binds both algorithm artifact hash and sheet hash. A new
sheet therefore requires validation, encoding, and hashing, not a WASM rebuild.

| Build-once proof | Value |
| --- | --- |
| Build count for eight variants | `1` |
| Build time | `9.097785 s` |
| Stock source SHA-256 | `c8182e133a202733ef7c6b43367097eb118d2295a91dcdbf592e6fe13ff48f79` |
| WASM SHA-256 | `c574c09a832d0a28cd1be8fd645a02685ad9c24a02543bce5c9819d5e1fd65f9` |
| WASM bytes | `3,665,765` |
| Data-linker SHA-256 | `3ca71ee3752776c5ba4d0236ced865d67939259bc60ab2d274f2b8a533390f14` |

The refreshed seed-42 stock preflight reproduces canonical SHA-256
`79577c3d46c3bc88d5ad9be29a6e709cf1ead134ec26f7243600ac004633325a`
from its `2,064 B` record and `182,181 B` broadcast.

#### Profile and in-process screening decision

One real 525-tick cell was sampled before and after the observation-array
change. Both runs reproduced canonical hash
`1661522b6eb3af8f05834f74c6665c69618ca142c5bba4dee26c7b190edd2f0e`.
Avoiding a forced `ToArray` reduced sampled inclusive time in the specific
generic observation-array encoder from `281.706 ms` to `276.079 ms`. The whole
`FormatObservation` sample changed from `693.120 ms` to `726.535 ms` while the
sampled-thread denominators differed, so no unsupported end-to-end speedup is
claimed. Wasmtime remained the dominant cost; streaming reduce-to-broadcast was
not justified by this profile.

An in-process/WASM sample produced byte-identical gameplay broadcasts after
removing the canonical replay hash and runtime-provenance participant fields:
normalized SHA-256
`94c2608def410051b1cf8cc48762bfa204e92ae4b9a1eab2e7152429e4e10dc2`
over `6,255,848 B`. Their full canonical hashes differ because runtime
provenance is deliberately bound. Consequently, in-process screening was **not
used** for any outcome claim in this report.

### 3. Felt-degeneracy eligibility bars

The old 20-tick repeated-frame probe scored 0/12 stalls and 0/12 loops while the
owner saw obvious handoff ping-pong and passive sides. It measured world-frame
repetition, not the felt failure. The replacement registration is frozen in
`balance/arc-relay-felt-degeneracy-bars-v1.json`:

| Bar | Frozen definition | Trip point |
| --- | --- | --- |
| Handoff ping-pong | Consecutive `A→B`, `B→A` handoff reversals for one Core and same life pair, each no more than four ticks apart | at least 3 reversals in one episode |
| Sustained passivity | After first scheduled Core birth, a quiet tick has at least 75% of the side's live bodies waiting, no carried Core, and no body within Chebyshev 4 of a live Core | at least 60 quiet ticks in a rolling 75-tick window |

These are eligibility bars, not fun scores. One trip excludes that doctrine or
sheet variant from population reads and galleries until repaired.

| Entrant/read | Handoffs / Wait | Bar result | Disposition |
| --- | --- | --- | --- |
| Original Convoy, six native matches | 439 handoffs; `60.7%` Wait; rapid same-pair episodes in every pairing, maximum episode 230 reversals | ping-pong trips | excluded |
| Original Information/Route Control | one passive window reached 73/75 quiet ticks | passivity trips | excluded |
| Fresh clean-room Convoy, six matches | 3 handoffs; `66.7%` Wait | both clean | admitted |
| Repaired Information/Route Control | no tripping window | both clean | admitted |

Fresh Convoy's higher raw Wait share is why raw Wait was not made the bar: its
waiting mostly occurred while carrying, contesting, or maintaining theater
presence. All cells in the two final native reads, gambit re-audit, Pulse
isolation, and fresh gallery passed both bars.

### 4. First-Pulse isolation

The isolation used the same eligible three-doctrine population
(repaired Information/Route Control, Interception, Split Control), campaign
seed `104729`, all three unordered pairs, and both assignments. Each row is six
deterministic WASM cells. Factors were tested one at a time in the registered
order: geometry, respawn return, then cadence. The two extra geometry variants
were follow-ups after neither first geometry candidate cleared the alert.

| One-factor profile | First-Pulse side won | Conversion | End tick min / median / mean / max | MaxTicks |
| --- | ---: | ---: | --- | ---: |
| H0 baseline | 6/6 | **100%** | 285 / 382 / 407.8 / 599 | 1 |
| Home gates wider | 5/6 | **83.3%** | 274 / 402 / 384.3 / 453 | 0 |
| Cover trim | 6/6 | **100%** | 276 / 379.5 / 390.0 / 541 | 0 |
| Return 16 | 5/6 | **83.3%** | 355 / 410 / 411.2 / 460 | 0 |
| Return 24 | 6/6 | **100%** | 350 / 432 / 444.2 / 599 | 1 |
| Hot cadence 60 | 6/6 | **100%** | 281 / 395 / 404.0 / 599 | 1 |
| Spacious cadence 90 | 6/6 | **100%** | 365 / 434 / 439.5 / 528 | 0 |
| Three home gates | 5/6 | **83.3%** | 347 / 416 / 418.5 / 505 | 0 |
| Home concourse | 6/6 | **100%** | 303 / 358 / 374.5 / 498 | 0 |

No row is under the `70%` alert. `home-gates-wide` is the recommendation for
the re-watch because it is tied for the only directional conversion change,
has the least pacing damage among those directional candidates, and removes
the MaxTicks cell. This is a cautious next-watch choice, not a fix claim. The
fresh four-doctrine gallery on that profile still measured 10/11 first-Pulse
conversion (`90.9%`), which keeps the alert live.

### 5. Gambit grammar re-audit

The prior Phase D aggregate was `6-1-25` from the gambit side across all
gambit-versus-static pairings. Re-reading the actual within-family cells gives
`2-0-6`; the loop re-audit is deliberately within-family and mirrored.

The old grammar overrode 74,682 of 213,778 live body-ticks (`34.9%`), put the
full squad into one role for 13,642 ticks, and sustained a chained override for
as long as 483 ticks. Its conditions were persistent levels rather than edges;
any single blocked body could trigger a global route release; conditions could
retrigger or chain back-to-back; and all eight bodies were repeatedly rallied
or role-replaced. This was not tactical adaptation—it often erased the authored
sheet.

The replacement evaluation grammar is:

- enemy Pulse: reserve bodies become interceptors for 24 ticks, cooldown 60;
- double enemy possession: screen and reserve bodies become interceptors for
  16 ticks, cooldown 32;
- own Pulse: reserve bodies become carriers for 18 ticks, cooldown 45;
- rising-edge activation, scoped roles, bounded windows, and no wipe/route
  failure gambits.

The first replacement (`v2`) exposed a shared low-health handoff ping-pong and
was excluded rather than read. The final `v3` adds a prior-carrier guard and
passes both eligibility bars in all eight matches.

| Within-family audit | Gambit W-D-L | Overridden body-ticks | Full-squad one-role ticks | Longest window |
| --- | ---: | ---: | ---: | ---: |
| Old grammar | `2-0-6` | `34.9%` | 13,642 | 483 |
| Final v3 grammar | `1-1-6` | `6.31%` | 71 | 24 |

By family from the gambit side: Balanced `0-0-2`, Convoy Safe `1-0-1`,
Interception Switch `0-0-2`, Split Fast `0-1-1`. The redesign repaired the
execution grammar's global override and thrashing failure. It did **not** show
that the remaining adaptive rules outperform static sheets. The schema and
grammar remain evaluation-grade, not a player-facing UX proposal.

### 6. Two-seed native cohort and Convoy diagnosis

Fresh Convoy was independently re-authored from the clean brief with an equal
budget and no access to the failed implementation. The old Convoy and original
Information/Route Control were excluded before plan freeze; the latter was
mechanically repaired before re-entry. Both 12-cell plans use the recommended
`home-gates-wide` profile and passed eligibility in every match.

| Doctrine | Seed `104729` | Seed `130363` | Combined |
| --- | ---: | ---: | ---: |
| Interception | 6-0 | 6-0 | **12-0** |
| Split Control | 4-2 | 4-2 | **8-4** |
| Information/Route Control repaired | 2-4 | 2-4 | **4-8** |
| Fresh Convoy | 0-6 | 0-6 | **0-12** |

Fresh Convoy's source SHA-256 is
`02b6a2f60306c03a4d303d3d1b015b471959ec9914a036b403cccd6c50d32176`;
its sheet SHA-256 is
`129a2b0e582b45df56d4404d32681f2a5865c9b77e9cd2982db7099a4d6ac216`;
its WASM SHA-256 is
`2188598a1243d8c201e9ad0449327d86e1849b85bef5eeb24409b43293e63ee7`.

The two seeds produce the same records because these four frozen doctrines make
no seed-sensitive choices in these cells. The second campaign therefore proves
reproduction under a distinct campaign seed, but it does not provide
independent stochastic evidence. Interception is 12-0 across the registered
two-seed read. The old handoff loop was implementation-specific; fresh
Convoy's 0-12 indicates that convoy-cell collapse remains structural to the two
authored implementations or to this rules/cohort interaction. It is not a
universal theorem about every future convoy doctrine.

### 7. Spectator storytelling

The owner's failed legibility read is treated as a loop target, not polish:

- Canvas2D and 3D show a large, animated, team-accented carrier ring/beacon;
- 3D now gives Wells, Reactors, Core state, integrity, and charge distinct
  objective geometry;
- top-centre banners announce Core birth, steal, drop, bank, and Pulse and
  linger for five ticks;
- a persistent “what matters now” cue prioritizes a Pulse-ready Core, current
  carrier and distance, loose Core, or next birth;
- the event feed translates Arc Relay facts into actor, Core source, team,
  charge, and reactor-integrity language;
- default Arc Relay playback is `0.5x`, or `2.5` presentation ticks/second.

The full 353-test web suite passes. A production-build browser smoke reached
tick 27 with the current-carrier/distance cue and `CORE STOLEN`; a second frame
showed `LOOSE CENTRE CORE` and `CORE DROPPED`, with no page errors. These prove
the storytelling surfaces render; only the blind re-watch can prove they tell
the story well enough.

### 8. Fresh outcome-blind gallery

The plan was frozen before outcomes at
`balance/arc-relay-gate3-2-gallery-v1.json`, SHA-256
`dab5b9b99992a893b9e837f9796144eea1dcf25a2e464664fcbae817d6b9473c`.
It uses seed `161803`, blind order seed `20260802`, the four eligible doctrines,
both assignments for all six unordered pairs, WASM runtime, and
`home-gates-wide`. All 12 cells verified and passed both eligibility bars.

The outcome-blind gallery is:

`sandbox/arc-relay-gate3-2-gallery-blind-v1-2026-08-01-engine-1-0-5/`

| Budget | Actual | Ceiling |
| --- | ---: | ---: |
| Matches | 12 | 12 requested |
| Largest replay | `145,278 B` | `300 KiB` |
| Replay corpus | `1,213,170 B` | — |
| Whole gallery | `5,424,510 B` | `8 MiB` |

Its index contains no outcomes, scores, or durations. Serve from the repository
root with:

```bash
python3 scripts/serve-gallery.py 8932 \
  --directory sandbox/arc-relay-gate3-2-gallery-blind-v1-2026-08-01-engine-1-0-5
```

Then open `http://localhost:8932/`. The decisive checks are whether a human can
identify the carrier and important Core, understand birth/steal/drop/bank/Pulse
beats, follow why a side acts, and find any handoff ping-pong or sustained
passivity in twelve blind matches. If silly play is still visible, the frozen
bar is set wrong even if every scorecard says eligible.

### 9. Verification and disclosures

| Check | Result |
| --- | --- |
| .NET solution | SDK 84, Guest 36, Determinism 17, Runtime.Wasm 67, CLI 110, Engine 1,354, App 181 passed; 77 external-integration tests skipped |
| Web | 353/353 tests passed; Gate 3 production build passed |
| Sweep harness tests | 4/4 passed |
| Felt-scorecard tests | 4/4 passed |
| Final golden release run | 6/6 verified, 6/6 exact, zero canonical replays retained |
| Final gallery | 12/12 WASM, 12/12 verified, 12/12 eligibility-clean |

Disclosures:

- no comeback mechanic was added;
- every gameplay/outcome claim in this report ran under WASM;
- in-process parity was evaluated but not used for claims because full
  provenance-bound canonical hashes differ;
- gambit `v2` was excluded after the new ping-pong bar fired;
- the original Information/Route Control and old Convoy were excluded before
  cohort reads, with reasons preserved in the cohort archive;
- campaign seed variation did not change behavior for these deterministic,
  seed-insensitive doctrines;
- the initial 6-1-25 gambit aggregate and the 2-0-6 within-family baseline are
  different scopes, not conflicting counts;
- no fun, balance, or final legibility claim is made.

## NEXT

1. Serve and watch the fresh twelve-match blind gallery without opening outcome
   evidence.
2. Export the review JSON and rule **PASS GATE 3** or **LOOP PHASE C/D**.
3. Treat any visible silly play as a failed eligibility bar, even when the
   mechanical scorecard is clean.
4. If the alert remains a loop target after the watch, continue geometry work
   from `home-gates-wide`; it is the least-damaging candidate, not a cleared
   alert.
5. Only after Gate 3 passes, run the separate player-facing sheet UX design.

Stop at the owner's ruling. No DECISIONS number is minted here.
