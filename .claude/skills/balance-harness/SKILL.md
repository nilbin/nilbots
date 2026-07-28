---
name: balance-harness
description: Evaluate a game-rules candidate with causal A/B diagnostics, rules-native bot generations, replay-dynamics metrics, and outcome-blind viewing before making a ship/no-ship call. Use for any gameplay change (mechanic, tuning, maps) before pinning a rules version.
---

# Balance harness: rules changes prove themselves in play

Canonical methodology: `docs/EVALUATION-METHODOLOGY.md`. Instruments:
`scripts/balance-eval.py`, `scripts/replay-dynamics-eval.py`,
`scripts/frontline-replay-eval.py`, `scripts/replay-review-sample.py`, the
CLI's historical `--rules` flag, and the separate local
`nilbots experiment frontline` path.

## 1. Classify and pre-register the experiment

Before running the final data, write down:

- whether this is a narrow tune or a substantial change to actions,
  information, objective economics, combat timing, survival, or geometry;
- the hypothesis, isolation arms, frozen seed/map blocks, artifact cohorts,
  runtime, and thresholds;
- the mechanics-only diagnostics, native-generation product comparison,
  dynamics scorecard, and replay-review sample size.

Keep three evidence classes separate:

1. frozen historical bots under the candidate = compatibility/regression;
2. the same cohort under paired arms = mechanic causality for those policies;
3. bots authored/adapted for each ruleset under their native rules = primary
   product balance and entertainment.

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

A substantial-rules verdict requires at least four independently authored or
substantially adapted, candidate-aware doctrines using the agent-arena
docs/SDK/CLI-only boundary and equal iteration budgets. Their native
round-robin is the primary verdict. Historical champions may join as named
sentinels, but report their rows separately.

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

python3 scripts/replay-review-sample.py /tmp/run/block*/<candidate> \
  --count 12 --seed 20260724 --output /tmp/review-sample.json
```

Fixed seeds make same-cohort arms comparable. Preserve every replay in
separate arm/block directories.

The four framework-owned Frontline reference bots are calibration fixtures
from one author. They verify that rush, replication, Anchor/turret, and
defensive paths execute; they never satisfy the independently authored native
cohort requirement.

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

## 6. Pinning a version

Winner: add `GameRules.V0_X`, point `GameRules.Current` + 
`BotArenaVersions.GameRulesVersion` at it, update the site rules card +
template README, add the DECISIONS entry with the numbers table, and paste the
outcomes/dynamics/replay-review evidence into GAME-DESIGN. Loser: DECISIONS
entry with the evidence, exact failure mode, and population needed to re-test.
