---
name: balance-harness
description: Evaluate a game-rules candidate with causal A/B diagnostics, rules-native bot generations, replay-dynamics metrics, and outcome-blind viewing before making a ship/no-ship call. Use for any gameplay change (mechanic, tuning, maps) before pinning a rules version.
---

# Balance harness: rules changes prove themselves in play

Canonical methodology: `docs/EVALUATION-METHODOLOGY.md`. Instruments:
`scripts/balance-eval.py`, `scripts/replay-dynamics-eval.py`,
`scripts/frontline-replay-eval.py`, `scripts/labs-replay-eval.py`,
`scripts/balance-lab-drive.py`,
`scripts/replay-review-sample.py`, the CLI's historical `--rules` flag, the
frozen replay-v2 `nilbots experiment frontline` path, and the generic
replay-v3 `nilbots experiment frontline-labs` path. Labs capture-threshold
arms use its local-only `--capture-threshold <positive-n>` option; phased
capture-gain arms use `--capture-gain-phase <start-tick>:<gain>`; the isolated
turret-exit arm uses `--mobilize-turrets`.

For a multi-factor generic candidate, use the mode-independent Balance Lab
contract in `docs/NILBOTS-BALANCE-LAB.md` and a checked-in spec under
`balance/`. Its candidate identity is always
`mode + ruleset + map + match format`, resolved with an exact topology
profile/fingerprint. Never collapse those axes or the body/participant shape
into a nickname or silently promote an experimental arm. Use the declared
evaluation profile for lineup/payoff semantics; the implemented
`two-team-zero-sum-v1` profile is not an FFA evaluator.

## 1. Classify and pre-register the experiment

Before running the final data, write down:

- whether this is a narrow tune or a substantial change to actions,
  information, objective economics, combat timing, survival, or geometry;
- the hypothesis, isolation arms, frozen seed/map blocks, artifact cohorts,
  runtime, and thresholds;
- the mechanics-only diagnostics, native-generation product comparison,
  dynamics scorecard, and replay-review sample size.

Keep study roles separate:

1. frozen historical bots under the candidate = compatibility/regression;
2. the same cohort under paired arms = mechanic causality for those policies;
3. bots authored/adapted for each ruleset under their native rules = primary
   product balance and entertainment;
4. unqualified artifacts = infrastructure smoke only;
5. hostile targeted policies = adversarial sentinels, not population evidence.

Causal arms must share the same declared seed profile. Equal numeric seeds
with different private-stream derivations are not paired randomness.

For a substantial change, **old rules-unaware bots cannot veto the candidate**.
They remain safety sentinels. Comparing different generations does not prove
single-mechanic causality either; label it as a product comparison.

## 2. Implement the candidate behind GameRules flags

Gameplay values live ONLY in `GameRules` (CLAUDE.md invariant). Add the
candidate as flags/values defaulting to off, wire an experiment entry into the
shared rules resolver with a visibly non-official version string
(`0.X-exp-<name>` — it flows into replays and seed derivation). Rules 0.1/0.2
behavior must stay bit-identical: run the full test suite.

## 3. Build the right population

A substantial-rules verdict requires at least four independent authoring
lineages and candidate-aware doctrines using the agent-arena
docs/SDK/CLI-only boundary and equal iteration budgets. Their native
round-robin is the primary verdict. Historical champions may join as named
sentinels, but report their rows separately.

The first internal Frontline pilot may vote at cumulative T4 with four
independent lineages; include T5-capable policies when available. T7/T8,
automated best responses, equilibrium estimation, and candidate search are
deferred until a credible population exists.

The generational reference is:

`previous native bots + previous rules` versus
`current native bots + candidate rules`.

Freeze final WASM hashes before the holdout. Source iteration may use
in-process execution; final evidence is all-WASM with zero faults.

## 4. Run outcomes, dynamics, and replay review

```bash
python3 scripts/balance-eval.py --rulesets <reference>,<candidate> \
  --bots bot_a=path/bot.wasm bot_b=... --seeds 101,202,303

python3 scripts/replay-dynamics-eval.py \
  --group current=/tmp/run/block1/<candidate> \
  --group current=/tmp/run/block2/<candidate>

nilbots experiment frontline --bot <actor-a> --opponent <actor-b> \
  --runtime wasm --seeds 101,202,303 --out /tmp/frontline/block-1

python3 scripts/frontline-replay-eval.py \
  --group current=/tmp/frontline/block-1 \
  --group current=/tmp/frontline/block-2 \
  --json /tmp/frontline/report.json

python3 scripts/labs-cohort-drive.py \
  --manifest arena-bots/frontline-labs/<cohort>/cohort.json \
  --output arena-bots/frontline-labs/<cohort>/evidence/baseline-wasm

python3 scripts/labs-replay-eval.py \
  --group current=arena-bots/frontline-labs/<cohort>/evidence/baseline-wasm/matches \
  --json arena-bots/frontline-labs/<cohort>/evidence/baseline-wasm/dynamics.json

# Pendulum/dullness family + the pre-registered S1-S5 / N1 gates of
# docs/DESIGN-FORENSICS-DYNAMICS-2026-07-29.md, one --group per study cell.
python3 scripts/labs-replay-eval.py --dynamics \
  --group <cell>=<run>/studies/<study>/candidates/<cell> \
  --json /tmp/<run>/pendulum.json

# Reconcile the implementation against the published forensics corpus.
python3 scripts/labs-replay-eval.py \
  --group <cell>=... \
  --verify-against balance/frontline-pendulum-dynamics-baseline-v1.json

nilbots experiment frontline-labs \
  --bot <bot.wasm> --opponent <opponent.wasm> \
  --capture-threshold 12 --seed 104729

nilbots experiment frontline-labs \
  --bot <bot.wasm> --opponent <opponent.wasm> \
  --capture-gain-phase 300:2 --seed 104729

nilbots experiment frontline-labs \
  --bot <candidate-bot.wasm> --opponent <opponent.wasm> \
  --mobilize-turrets --seed 104729

nilbots experiment frontline-labs \
  --bot <candidate-bot.wasm> --opponent <opponent.wasm> \
  --auto-companions --duel-map thin-fronts --seed 104729

python3 scripts/balance-lab-drive.py \
  --spec balance/frontline-duel-progression-v1.json \
  --output /tmp/nilbots-balance/frontline-duel-progression-v1

python3 scripts/frontline-balance-candidates.py \
  --spec balance/frontline-duel-progression-v1.json \
  --cli src/BotArena.Cli/bin/Release/net10.0/botarena

nilbots experiment frontline-labs qualify \
  --bot <candidate-bot.wasm> \
  --suite frontline-qualification-5 \
  --out /tmp/qualification/<bot>

python3 scripts/replay-review-sample.py /tmp/run/block*/<candidate> \
  --count 12 --seed 20260724 --output /tmp/review-sample.json
```

Fixed seeds make same-cohort arms comparable. Preserve every replay in
separate arm/block directories.

For a Labs capture-threshold arm, the CLI creates a distinct
`frontline-labs-1-experiment-capture-<n>` ruleset and fingerprints. Put those
exact values in the cohort manifest and include the same option in
`--runner-command`; never relabel changed values as `frontline-labs-1`.
The same identity rule applies to capture-gain phases. The schedule is
canonical contract data, and candidate-aware bots resolve it with
`frontlineMode.Capture.GainPhaseAtTick(context.Tick)`.
It also applies to the Mobilize arm: that flag exposes a new action under
`frontline-labs-1-experiment-mobilize`; rules-unaware bots are compatibility
sentinels, not product-balance evidence.

The automatic-companion arm uses
`frontline-labs-1-experiment-one-bend-auto-companions` and separately
identified map contracts. Children create fresh lives at declared ticks with
`automatic-activation` origin and return automatically after destruction.
The first isolated arm omits Prime Fabricate and Split, so label its factor a
progression-policy bundle rather than claiming a one-boolean lifecycle
ablation. Keep that and every promised follow-up isolation in
`balance/frontline-ablation-debt-v1.json`.

Balance Lab specs must declare study blocks, exact fingerprints, artifact and
source hashes, topology and evaluation profiles, decision profile, exact
profile-scoped qualification evidence, coverage, and common-randomness
profiles. Voting holdouts use an external nonce-backed commitment/reveal;
checked-in seed values are disclosed development seeds. The driver freezes
the executable toolchain and hashes a balance-eligible entrant's actual
qualification report and requires its artifact/profile/T/C fields to match.
`infrastructure-smoke` with unqualified bots validates plumbing and effect
direction only. It cannot select or promote a candidate or satisfy the
independently competent tier-population requirement.

Entrants must carry independent lineage, doctrine, equal authoring-budget
packet, and packet hash. Pair/seed bootstrap intervals are conditional on the
frozen finite population; inspect lineage diversity and leave-one-lineage-out
sensitivity before making population claims.

Frozen `frontline-qualification-1` is only the historical T4
`entry-initiative` component. WASM-only suite 2 retains the incomplete
`contract-auto-determinism` foundation. Suite 3 is the complete
`frontline-duel-depth-union-t2-v1` profile: contract/count handling, useful
automatic lives, objective path/hold, direct fire, straight evasion, and
explicit Fabricate in both assignments. A suite-3 pass awards T2 but remains
fun-floor evidence rather than a numeric balance vote.

Current suite 4 automatically reruns and hash-links that exact prerequisite,
then tests cumulative T3 legal curves, strict corners, remaining-range
cadence, cooldown tempo, and local transform safety from both assignments.
It returns the retained prerequisite tier on a clean capability failure. A
suite-4 pass awards T3 but also remains below the cumulative T4 directional
pilot voting floor.

Current suite 5 reruns and hash-links exact T3, then tests suppression,
proactive pressure entry, objective-preserving response, front rotation, and
the thin-fronts holdout from both assignments. A suite-5 pass awards T4 and
entrant-level balance eligibility. It does not make a same-lineage revision
an independent doctrine or satisfy the four-doctrine pilot floor.

Read the cell's `strategicDiversity.doctrineRedundancy` block before claiming
population breadth. It reports artifacts, declared doctrines, effective
doctrine estimate, signatures, pairwise distances, and redundancy components.
The current v1 thresholds are diagnostic until calibrated; use them to request
missing doctrine briefs, never to delete archived entrants or promote a
candidate automatically.

The four framework-owned Frontline reference bots are calibration fixtures
from one author. They verify that rush, replication, Anchor/turret, and
defensive paths execute; they never satisfy the independently authored native
cohort requirement.

For the first Frontline Labs exploratory screen, use the agent-arena
**cohort sprint** and the frozen
`docs/FRONTLINE-LABS-COHORT-BASELINE.md` pre-registration. Its four authors
receive `docs/FRONTLINE-LABS-RULES.md`, one implementation pass, and zero
strategy-improvement passes by default. That smaller budget can expose gross
dominance, deadlocks, unused mechanics, participant-assignment bias, and DX
failures; it cannot establish balance or a ship verdict. Mechanical repair is
permitted without opponent results, and a shared repair opportunity must be
equal and retain every revision.

Labs cohorts are neutral evidence under `arena-bots/frontline-labs/`, not
champions. Archive all entrants, source revisions, final WASM hashes,
pre-disclosure `DX.md` reports, match logs, verified replay-v3 files, W/D/L
results, dynamics, and the outcome-blind sample. Synthesize DX before
tournament disclosure and do not turn it into another strategy pass.

The Frontline analyzer rejects mixed rules fingerprints under one group label
and surfaces map, runtime, and artifact cohort metadata. Do not combine
in-process diagnostics with canonical WASM evidence.

Always report:

- outcomes/reasons, draw rate, doctrine diversity, faults, median and p90;
- damage incidence/tempo, reciprocal and multi-tick damage;
- active/stagnant/repeated ticks, stalled/looped games, action entropy/runs;
- objective contests, evictions, and mechanic-specific evidence.

Duration is a viewing-budget guardrail, not a universal minimization target.
Do not require every new ruleset to beat an instant-ray predecessor's median
or Elimination label count. Do not hide a repetitive tail behind a good
average.

Before seeing aggregate outcomes, select at least 12 header-only,
map/pair-balanced replays. Watch them at normal speed on the actual viewer,
record legibility/tension/action-counteraction/repetition/ending notes, and
only then open the metrics. Publish highlights separately and label them:
highlights demonstrate the ceiling, not the typical experience.

## 5. Ship/no-ship

Apply the pre-registered scorecard and the starting holdout guardrails in
`docs/EVALUATION-METHODOLOGY.md`. Safety and determinism are hard gates.
Strategy diversity, combat, non-repetition, and blind replay review are the
product gates. A candidate passing numeric thresholds but producing confusing
or dull replays remains a hold.

Never redefine a completed gate after seeing the table. A revised product gate
is a new, documented holdout.

If exactly one diversity gate fails while safety, mechanics, dynamics, and
blind viewing pass, freeze the candidate rules and leading artifact. The next
experiment is equal-budget counterplay adaptation on fresh seeds, not a
post-hoc threshold waiver or immediate rules tune. Preserve the failed table
in DECISIONS before pre-registering that follow-up.

If the product owner explicitly accepts a failed product gate instead, do not
launder the old result into a pass. Preserve it, add a separate numbered
override decision with the rationale and new future threshold, and update all
policy/docs surfaces. Safety, faults, deterministic verification, and replay
integrity remain hard gates and cannot be waived this way.

## 6. Reporting to the owner (owner ruling, 2026-07-30)

Every result presented to the owner uses this structure, in this order,
with the labels literally present. No exceptions for "obvious" cases —
the owner asked for this after a report that buried the ask.

1. **`DECISION NEEDED:`** — first, or the explicit line
   `DECISION NEEDED: none`. One sentence per decision, phrased as the
   choice itself (not the analysis), with the default named if there is
   one. Never mix a decision into a findings paragraph.
2. **`RESULT:`** — what happened, in plain words, before any numbers:
   which arm won/lost/was adopted and what it means for the game. One
   short paragraph.
3. **`EVIDENCE:`** — the numbers table or gate list, compressed.
   Spell out codenames on first use in every report (the owner does not
   carry arm tokens like `wane` between sessions); prefer "4 slots +
   slower rebuild (`wane`)" over the bare token.
4. **`NEXT:`** — what runs next without input, if anything.

Additional standing rules: entertainment/depth rulings from the owner
outrank measured product gates (record the override per §5, don't
re-litigate); anything ambiguous in an owner message gets one clarifying
question BEFORE hours of work, not after.

## 7. Pinning a version

Winner: add `GameRules.V0_X`, point `GameRules.Current` + 
`BotArenaVersions.GameRulesVersion` at it, update the site rules card +
template README, add the DECISIONS entry with the numbers table, and paste the
outcomes/dynamics/replay-review evidence into GAME-DESIGN. Loser: DECISIONS
entry with the evidence, exact failure mode, and population needed to re-test.
