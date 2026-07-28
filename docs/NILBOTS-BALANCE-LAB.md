# Nilbots Balance Lab

Status: architecture and slice 1 implemented; no candidate promoted.

## Purpose

The Balance Lab replaces isolated tuning runs with a reproducible,
progressively more expensive evaluation pipeline. A candidate is always:

`mode + ruleset + map + match format`

Those four components remain independently selectable, immutable,
fingerprinted, and present in every replay. A candidate ID is an experiment
label, never a substitute for the component fingerprints. A playlist may pin
a candidate later; a ladder belongs to that playlist and is not part of the
candidate itself.

The intended operating split is approximately 80% deterministic automation
and 20% model/human judgment. Automation owns enumeration, execution,
provenance, statistics, rejection gates, and evidence retention. Agents and
people design useful abstractions, challenge weak populations, inspect
anomalies, and decide whether play is understandable, exciting, and enjoyable
to program.

## Existing infrastructure audit

| Layer | Existing reusable pieces | Missing after slice 1 |
| --- | --- | --- |
| Candidate identity | Typed mode/rules/map/format definitions; independent fingerprints; aggregate match fingerprint; replay-v3 contract | General candidate generator and hosted catalog |
| Static/map analysis | Exact map model; deterministic map validators; duel map enumeration in `FrontlineLabsDuelTheoryTests`; one-bend route/fork scripts | Mode-neutral map feature extractor, symmetry/fairness report, generated-map rejection |
| Exact tactics | Engine chronology validation; projectile and collision kernels; `shot-theory-lab.py`; bounded duel proofs | General microstate DSL, dominance solver, turret-standoff and objective-suppression catalog |
| Restricted play | Action legality and data-driven rules make restrictions representable | Versioned ablation definitions and equivalent restricted bot wrappers |
| Bot tiers | T1–T8 and C0–C5 definitions; qualification suite; retained source/WASM/reference evidence | Complete cumulative probes, multiple independently competent bots per tier, planning-budget declarations |
| Population execution | Mirrored cohort runner; all-bot retention; paired seeds; replay verification; no champion-only pruning | Multi-tier population manifest, cross-tier blocks, automatic holdout sealing |
| Replay metrics | Rich generic Frontline dynamics report; activity, combat, population, objectives, phases, deadlocks | Mode adapters for deathmatch/FFA; confidence intervals; coordinated-play classifiers |
| Game theory | Exact local payoff examples and full empirical match rows | Payoff-tensor analysis, equilibrium support, best-response search, exploitability |
| Candidate search | Immutable numeric/map experiment factories | Constrained/Bayesian/evolutionary search with cheap-layer promotion |
| Human review | Deterministic blind replay sampling and replay viewer | Structured mobile/desktop rubric ingestion and report join |

The architecture is therefore ready for a Lab without replacing the engine or
replay stack. The main risk is population validity, not missing match
provenance.

## Evaluation layers

Candidates advance in cost order. Failure at a cheap hard gate prevents an
expensive tournament; it does not get averaged away.

1. **Static/map analysis**
   validates reachability, rotational/reflection symmetry, spawn and objective
   distance fairness, independent route count, choke width, objective capacity,
   line-of-fire exposure, crossfire potential, reinforcement distance, and
   obvious deadlocks.
2. **Exact tactical analysis**
   enumerates bounded local states for dominant actions, forced damage,
   universal dodges, curve counterplay, turret standoffs, and objective-entry
   suppression.
3. **Restricted-play evaluation**
   compares otherwise equivalent policies with curves, dodging, transforms,
   team information, or planning depth removed. A mechanic must produce a
   measurable marginal effect before its complexity is credited.
4. **Tiered bot population**
   uses concrete qualification results and declared planning budgets. Every
   authored revision, source tree, WASM, manifest, result, and replay is saved.
   Final evidence uses full cross-play rather than champion-only retention.
5. **Empirical game theory**
   builds payoff matrices/tensors, detects dominance and counter-strategy
   cycles, estimates equilibrium support, and searches/trains best responses
   to measure exploitability.
6. **Automated candidate search**
   proposes numeric rules and generated maps, promotes only candidates passing
   cheaper layers, and evaluates final proposals on sealed seeds/maps plus new
   adversarial best responses.

Two-team 1v1, 2v2, and 3v3 are team-level two-player zero-sum environments.
FFA is general-sum and receives separate coalition, kingmaking, survival, and
placement metrics, playlists, and ladders.

## Balance vector

There is no composite “fun” or “balance” score. Every report preserves:

- side/spawn fairness;
- exploitability;
- strategic diversity and equilibrium support;
- adjacent-tier skill gradient;
- early/mid/late occurrence and duration;
- early wins separately from opening snowballs;
- match-duration distribution;
- activity, damage pressure, quiet intervals, and lead changes;
- comeback and counterplay opportunities;
- robustness across seeds and small parameter changes;
- safety, deterministic verification, and provenance.

An unsupported dimension is `not-measured`, never zero and never silently
omitted.

## Slice 1

[`scripts/balance-lab-drive.py`](../scripts/balance-lab-drive.py) implements
the smallest complete orchestration path:

- validate that candidates cover the declared factor product exactly once;
- require exact mode/rules/map/format and aggregate fingerprints;
- freeze every entrant source tree and WASM hash;
- freeze the schema, orchestrator, cohort runner, evaluator, and gameplay
  checkout identities, and refuse a drifted resume;
- run complete within-tier cross-play in both assignments on paired seeds;
- verify every replay and reject contract, artifact, runtime, fault, or
  disqualification drift;
- emit per-cell payoff matrices;
- adapt generic Frontline replay v3 into the existing rich dynamics report;
- emit a vector report and same-artifact factorial contrasts.

Run it from a repository checkout:

```bash
python3 scripts/balance-lab-drive.py \
  --spec balance/frontline-duel-progression-v1.json \
  --output /tmp/nilbots-balance/frontline-duel-progression-v1
```

`--dry-run` freezes the plan without executing it. `--resume` revalidates the
frozen spec, sources, artifacts, commands, and existing replays before
continuing. Output directories are never overwritten.

The intended stable facade is:

```text
nilbots balance run --spec <spec>
```

The repository script remains the implementation while the schema and
adapters settle. A packaged CLI facade must not make Python a hidden runtime
dependency for ordinary play/build/submit commands.

The checked-in smoke spec is
[`balance/frontline-duel-progression-v1.json`](../balance/frontline-duel-progression-v1.json).
It declares one disclosed paired seed, two sealed holdout seeds, all six
factor cells, and two explicitly unqualified retained artifacts. Its evidence
class is `infrastructure-smoke`: it proves orchestration, identity, causal
comparison shape, and report generation, not balance.

## Immediate Frontline experiment

Pre-register a full:

`map topology × companion policy × calibrated bot tier`

matrix. The map factor starts with `current`, `thin-fronts`, and
`outer-shoulder-bypass`; the lifecycle factor is
`manual-fabrication` versus `automatic-activation`. Run identical artifacts,
pairings, assignments, and paired seeds in all six cells. Automatic activation
and every map remain experimental arms.

The implemented automatic cell is intentionally an isolated
**progression-policy bundle**: child lives activate at ticks 120/260 and
automatically return after destruction, while Prime Fabricate and Split are
absent and child spawns are assigned independently. It is not a one-boolean
mechanic ablation. The current factorial estimates that coherent product
bundle and its interaction with topology. A later restricted-play experiment
must allocate non-conflicting replica capacity before claiming an isolated
automatic-activation effect with Split held constant.

The retained duel-depth bots and InitiativePlanner are infrastructure and
mechanic diagnostics. They are not yet a calibrated T5–T6 population, and
their results cannot choose a balance winner. The next population milestone
is at least two independently competent artifacts in each evaluated tier,
with a larger strategy mixture at the primary T5–T6 band.

## Automation guardrails

Automated optimization will exploit weak metrics and shared bot blind spots.
Every promising candidate therefore needs:

- sealed holdout seeds and later holdout maps;
- independently authored capability-qualified policies;
- restricted-play ablations;
- newly searched or trained adversarial best responses;
- small parameter perturbations;
- header-only, outcome-blind replay sampling before aggregate disclosure;
- human notes on tension, legibility, repetition, endings, and bot-authoring
  experience.

A numerical pass with a dull or incomprehensible replay remains a hold.

## Slice-1 smoke result

The first all-WASM run completed and verified all 12 planned replays with zero
faults or disqualifications. With only two unqualified entrants, one paired
seed, and two games per cell, the numbers are directional diagnostics:

| Map | Progression | Median ticks | Active | Damage / 100t | Stalled / looped | Longest no interaction |
| --- | --- | ---: | ---: | ---: | --- | ---: |
| current | manual | 500 | 3.2% | 0.0 | 2 / 2 | 484 |
| current | automatic | 355.5 | 70.7% | 3.0 | 2 / 2 | 104 |
| thin fronts | manual | 339.5 | 83.5% | 17.5 | 0 / 0 | 14 |
| thin fronts | automatic | 238 | 82.4% | 16.6 | 0 / 0 | 14 |
| outer shoulder | manual | 500 | 3.2% | 0.0 | 2 / 2 | 484 |
| outer shoulder | automatic | 500 | 78.2% | 7.9 | 2 / 2 | 104 |

This validates the intended attribution structure and shows a large
topology-policy interaction. Thin fronts alone created activity for these
policies; automatic progression created bodies and combat on the otherwise
passive maps but did not by itself remove their long repeated tails. No
candidate is promoted, no tier gradient or exploitability was measured, and
the sealed seeds remain unused.
