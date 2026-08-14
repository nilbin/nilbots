# Arc Relay Tapline opening balance pass

Date: 2026-08-14  
Status: design screen complete; no balance or human-fun claim  
Rules line: `tapline-3` / `arc-relay-tapline-03`  
Runtime for every measured claim: trusted in-process generic mind

## Outcome

This pass established a reproducible way to compare Arc Relay openings without
pretending that one aggregate Elo number explains them. Four openings with the
same eight-class composition were frozen, trace-reviewed, screened against the
frozen felt-degeneracy bars, and compared as every unordered pair on identical
seeds with both side assignments.

The registered 144-cell matrix found:

- Direct Mid and Distributed Wells are even head-to-head (`12-12`).
- Direct Mid and Distributed Wells each sweep Split Interception (`24-0`).
- Split Interception in turn clearly beats the Four-plus-four Pincer
  (`22-0-2`).
- The Four-plus-four Pincer is cross-matchup dominated: one win, three draws,
  and 68 losses across its 72 games.

That is a deterministic candidate-screen result, not proof of population
balance. It does establish a concrete defect in this candidate set: the long
outer ingress reaches enemy territory, but does not convert that position into
Cores quickly enough to compete with ordinary scoring routes.

The smallest isolated rules probe delayed flank-Well births by 25 ticks. It
failed its pre-registered support bar and made pacing worse, so it was reverted.
Tapline-3 remains unchanged.

## Method

The method is exhibit-first and trace-first:

1. Write one comprehensible strategic intent.
2. Keep composition fixed so class choice cannot explain the result.
3. Put semantic behavior in `orders.json` and Tapline geography in
   `map-plan.json`.
4. Compile and freeze hashes before comparison.
5. Watch and trace six standard exhibit cells; reject silent, degenerate, or
   mechanically broken candidates before measuring outcomes.
6. Register a blocked paired matrix: twelve fresh seeds, every unordered pair,
   and both side assignments for each seed.
7. Require every cell to pass the frozen felt-degeneracy bars before it enters
   the read.
8. Read matchup records together with first contact, Core conversion,
   territorial route, participation, interceptions, stalls, and duration.
9. Only an explicit registered alarm authorizes one causal one-factor probe.

The final matrix is
`balance/arc-relay-tapline-opening-matrix-v8.json`; its candidate registry is
`balance/arc-relay-tapline-opening-cohort-v8.json`. The parallel sweep completed
144/144 verified cells with six workers in 370.551 seconds. All 144 cells were
eligible. `LATEST.json` records result digest
`f6635766932fef7c7a0dc0248d0b8c3bca0d9f1ea93c5e7b0fd106fc843b1647`.

The matrix alarms were registered before the run:

- cross-matchup dominant: point rate at least `0.70` against every opponent;
- cross-matchup dominated: point rate at most `0.30` against every opponent;
- side gap: at least `0.15`;
- horizon alert: at least `0.25`.

No battery or post-hoc seed selection was used. Earlier registered matrix
versions remain as the audit trail for candidates rejected by trace or frozen
eligibility checks; only v8 is the final read.

## Frozen candidates

Every candidate uses exactly:

`kestrel, kestrel, veil, hush, towline, sunder, patchbay, relay`

| Candidate | Intent | Orders SHA-256 | Map-plan SHA-256 | ATL2 bytes |
| --- | --- | --- | --- | ---: |
| Direct Mid v3 | Move as one centre-first pack, convert known Cores by the shortest route, then maintain moving enemy-Well pressure | `e4519d308059456815075b4b46386be65733be5ec0971bc09bfd5943eaa6135b` | `6f87940948690bd133bdee496e0a72526af982b545dd61e83373e6571d476076` | 3,521 |
| Four-plus-four Pincer v3 | Two self-sufficient four-body wings traverse the full outer corridors, meet behind enemy lines, and convert what they win | `3fc29eba6015411a313efd342633cfc8d0cab0b9ce673d5e3433355160128d18` | `16ac36abfce9aa91e729c16d47466e507366499003badc4f31a29d46b2244591` | 3,919 |
| Split Interception v2 | Pair both outer lanes, screen centre, and retain a home logistics pair | `ff042a51e3d699b8b0751b44237233203fb6e2196264464d5bca8f843ecb2df0` | `554907669aed83f069b962a972406bc17ff9b4a6aedd82bd56d9ddd037ac2d66` | 4,678 |
| Distributed Wells v4 | Maintain a continuous `3-3-2` economy across all three Wells, with at most one local worker released per remembered loose Core | `118979b24f93b0c1cae046bcb55ddff8b1255ad1f0bf4f1d48db44c55a226fd5` | `5ed825c06d1ac588b593a40ec0ccda3019dd8c91c7cf91de7e8bbace185cf9e1` | 4,194 |

Each candidate directory contains `orders.json`, `map-plan.json`, and an
immutable `freeze.json` with source, hashes, compiled size, intent, portability
boundary, and reference-exhibit provenance.

## Final paired result

Each row contains 24 games: twelve seeds, both side assignments.

| Pairing | Record | Mean end tick | Horizon games |
| --- | --- | ---: | ---: |
| Direct Mid vs Distributed Wells | `12-12` | 474.4 | 0 |
| Direct Mid vs Pincer | `22-1-1` | 506.6 | 5 |
| Direct Mid vs Split | `24-0` | 448.3 | 0 |
| Distributed Wells vs Pincer | `24-0` | 402.0 | 3 |
| Distributed Wells vs Split | `24-0` | 343.4 | 0 |
| Pincer vs Split | Pincer `0-2-22` | 472.3 | 4 |

The aggregate record is useful only beside the matchup graph:

| Opening | W-D-L | Point rate | Pair point rates |
| --- | ---: | ---: | --- |
| Direct Mid | `58-1-13` | 0.8125 | Distributed 0.5000; Pincer 0.9375; Split 1.0000 |
| Distributed Wells | `60-0-12` | 0.8333 | Direct 0.5000; Pincer 1.0000; Split 1.0000 |
| Pincer | `1-3-68` | 0.0347 | Direct 0.0625; Distributed 0.0000; Split 0.0417 |
| Split Interception | `22-2-48` | 0.3194 | Direct 0.0000; Distributed 0.0000; Pincer 0.9583 |

The only registered cross-matchup alarm is Pincer's dominated alarm. Direct and
Distributed have high aggregate rates because each has two very favorable
pairings, but their `12-12` head-to-head prevents either from satisfying the
registered dominance rule. Side point-rate gaps were at most 0.0417 and no
opening reached the 0.25 horizon alert.

## Mechanism read

| Opening | First attack | First pickup | First bank | Mean banks | Carrier interceptions | Opening enemy-half share | Centre / North / South | Min move/value bodies | Worst stall |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | ---: |
| Direct Mid | 12 | 46 | 65.5 | 8.22 | 1.01 | 0.419 | 0.499 / 0.246 / 0.256 | 8 / 8 | 81 |
| Distributed Wells | 12 | 48 | 77 | 8.06 | 1.06 | 0.197 | 0.245 / 0.376 / 0.379 | 8 / 7 | 333 |
| Pincer | 45 | 69 | 156.5 | 1.96 | 1.14 | 0.677 | 0.121 / 0.443 / 0.436 | 8 / 8 | 343 |
| Split Interception | 12 | 52.5 | 74 | 5.21 | 0.58 | 0.198 | 0.209 / 0.382 / 0.408 | 8 / 8 | 171 |

This read is why “the middle route is shorter” is incomplete. Pincer does reach
the opponent half and uses both flanks; its opening enemy-half share is 0.677,
far above the others. It also gets all eight bodies moving and contributing.
The failure is conversion: its median first bank is 91 ticks later than Direct,
and it averages fewer than two banks. The long route must win a distant Core and
return it; ordinary openings begin beside recurring supply and retain much
shorter conversion loops.

Split is not globally inert. It clearly counters Pincer, but its static lane
coverage loses to both openings that continuously collect and bank. That is the
reason this pass reports a matchup graph rather than flattening the four into an
Elo ordering.

## Causal probe

The registered policy preferred a geometry-only flank timing adjustment. That
path was rejected before mutation for two measured reasons:

- Pincer's sheet explicitly authors the full perimeter route, so opening a wall
  does not shorten its ingress without rewriting the candidate under test.
- An exhaustive rotationally symmetric wall-pair removal check found no edit
  that shortened the terrain-only enemy-Well-to-reactor route by more than one
  tile.

The next smallest isolated lever was Well timing. The probe delayed the north
and south first births from tick 50 to tick 75 while leaving the centre birth at
25 and changing nothing else. The pre-registered support bar required both:

- at least `+1.0` Pincer mean banks and `+0.10` Pincer point rate; and
- no worsening in horizon share.

Both baseline and probe used six fresh seeds, all three Pincer matchups, both
side assignments: 36 cells each, all eligible.

| Pincer metric | Tapline-3 baseline | Flank-delay probe | Delta |
| --- | ---: | ---: | ---: |
| W-D-L | `0-1-35` | `2-0-34` | — |
| Point rate | 0.0139 | 0.0556 | +0.0417 |
| Mean banks | 1.83 | 2.11 | +0.28 |
| Median first pickup | 60 | 90 | +30 ticks |
| Median first bank | 140 | 239 | +99 ticks |
| Horizon share | 0.1389 | 0.2778 | +0.1389 |

The result failed both the required effect size and the pacing guard. The
experimental profile was reverted in `fa126100`; reproducibility survives in
the parent experiment commit `9e1c2b04`, with the paired plans committed in
`9c3922a6`. Baseline result digest:
`751522b28cfa73e53a5820883798da549686386744d2dee1e1f562c1f96e5c6a`.
Probe result digest:
`dfde57dccb1b79d15c7494c8923b6b462824ec258d7dce7eea8fd7f17af92302`.

## Generic defects found before the clean matrix

Two replay-traced executor defects were fixed rather than tuned around in a
sheet:

- Carrier detours scored a denied occupied tile against a distance field that
  did not contain that tile. It therefore kept selecting the same blocked
  square. Denied-step scoring now evaluates legal bypasses against the actual
  destination field, with regression scenes for hostile-Well and shifted-route
  cases.
- The squad planner could inherit a three-step action whose first movement was
  unavailable. The executor emitted a false movement intent and waited instead
  of considering the next action. Unavailable inherited actions are now
  rejected before selection.

Target-capacity allocation is also enforced across fallback allocation, and
every executor action retains a non-empty reason string. These are generic mind
properties; none names a strategy, class, map side, route, or coordinate.

The matrix reducer itself also learned to read deliberately sparse one-factor
plans. It now skips absent pairings instead of dividing by zero, pinned by a
regression test.

## Portability boundary

The strategy is the semantic order ladder; the map plan is its geographic
binding. The four candidates should survive ordinary numeric tuning—damage,
cadence, cooldowns, path costs, and small geometry edits that preserve named
objects—without rewriting strategic intent. A map plan may need coordinates
adjusted when geography moves, but that is not a new strategy.

Rewriting is expected when the game adds or removes objective types, Wells,
semantic sheet verbs/facts, or materially changes topology so the named route
no longer exists. This pass does not pretend otherwise.

## Gallery and browser smoke

Public phone gallery:
https://superb-delivers-buffer-belief.trycloudflare.com

The gallery contains one fixed seed/assignment from each of the six pairings.
Cards deliberately show the observed cell outcome and the full 24-game pairing
record, plus intended tactic, counterplay, and what to watch. It is a viewing
surface, not the evidence store.

Production `web/dist` was rebuilt after rebasing onto current `main`. A Chrome
smoke at 390x844 loaded the index and a real replay, enabled playback after all
eight assets loaded, and reported no console warnings or errors.

## Determinism and provenance

The 144-cell read predates the final rebase over the unrelated Hardlight visual
commit. A fresh current-HEAD rerun of `direct-mid--shadow-pincer--s9801--a0`
changed the canonical replay hash because provenance changed, but its gameplay
state digest remained byte-identical:

`3bbfec74f40b6a8b9356b0c2a9564bd66c8ee58a7eb6fb7981c01c41aaa738f5`

That comparison used `scripts/arc-relay-state-digest.py` on the original matrix
replay and the fresh post-rebase replay. No gameplay evidence was silently
reinterpreted across the rebase.

## Uncertainty and next decision

- These are authored candidate implementations, not optimal strategies.
- Four openings and one shared roster do not span the player population.
- Twelve deterministic seed blocks are enough to expose this candidate graph,
  not enough to claim universal balance.
- Pincer's failure may be primarily the candidate's conversion plan, the map's
  outer-route economics, or both. The failed timing probe rules out only that
  one adjustment.
- Simulation cannot establish whether humans find the game or any opening fun.

The safe conclusion is narrow: keep Tapline-3 unchanged, retain the four frozen
references and paired method, and use the next depth design to create a genuine
payoff or tactical purpose for long-route commitment rather than disguising the
gap with a small timing tweak.
