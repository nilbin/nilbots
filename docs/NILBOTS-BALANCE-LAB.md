# Nilbots Balance Lab

Status: pilot architecture slice 3 implemented and frozen for population
work; no candidate promoted.

## Purpose

The Balance Lab replaces isolated tuning runs with a reproducible,
progressively more expensive evaluation pipeline. A candidate is always:

`mode + ruleset + map + match format`

Those four product components remain independently selectable, immutable,
fingerprinted, and present in every replay. Execution additionally pins the
exact topology fingerprint: participant/controller assignment, scoring teams,
stable body capacity, and initial lives. The resulting subject is a
**resolved candidate**: `candidate + topology profile`. A candidate ID or
topology-profile label is never a substitute for its fingerprint. A playlist
may pin a resolved candidate later; a ladder belongs to that playlist and is
not part of the candidate itself.

The intended operating split is approximately 80% deterministic automation
and 20% model/human judgment. Automation owns enumeration, execution,
provenance, statistics, rejection gates, and evidence retention. Agents and
people design useful abstractions, challenge weak populations, inspect
anomalies, and decide whether play is understandable, exciting, and enjoyable
to program.

## Existing infrastructure audit

| Layer | Existing reusable pieces | Missing after slice 1 |
| --- | --- | --- |
| Candidate identity | Typed mode/rules/map/format definitions; explicit topology profile/fingerprint; aggregate match fingerprint; replay-v3 contract; engine-derived Frontline spec generation | Mode-neutral generator and hosted catalog |
| Static/map analysis | Exact map model; deterministic map validators; duel map enumeration in `FrontlineLabsDuelTheoryTests`; one-bend route/fork scripts | Mode-neutral map feature extractor, symmetry/fairness report, generated-map rejection |
| Exact tactics | Engine chronology validation; projectile and collision kernels; `shot-theory-lab.py`; bounded duel proofs | General microstate DSL, dominance solver, turret-standoff and objective-suppression catalog |
| Restricted play | Action legality and data-driven rules make restrictions representable; checked-in ablation-debt registry | Versioned ablation definitions and equivalent restricted bot wrappers |
| Bot tiers | T1–T8 and C0–C5 definitions; profile-scoped qualification provenance; immutable suite-1 T4 component; suite-2 WASM determinism/automatic-life component; retained source/WASM/reference evidence | Complete cumulative T1/T2 and higher probes, multiple independently competent bots per tier, planning-budget declarations |
| Population execution | Study-scoped mirrored cohort runner; optional self-play; all-bot retention; replay verification; frozen executable bundle; no champion-only pruning; hard balance-verdict eligibility gate | Qualified multi-tier population and cross-tier blocks |
| Replay metrics | Rich generic Frontline dynamics report; activity, combat, population, objectives, phases, deadlocks; finite-population paired contrasts and lineage sensitivity | Mode adapters for deathmatch/FFA and coordinated-play classifiers |
| Game theory | Exact local payoff examples and full empirical match rows | Deferred: payoff-tensor analysis, equilibrium support, best-response search, exploitability |
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
5. **Empirical game theory — deferred beyond the Frontline pilot**
   builds payoff matrices/tensors, detects dominance and counter-strategy
   cycles, estimates equilibrium support, and searches/trains best responses
   to measure exploitability.
6. **Automated candidate search — deferred beyond the Frontline pilot**
   proposes numeric rules and generated maps, promotes only candidates passing
   cheaper layers, and evaluates final proposals on committed private
   seeds/maps plus new adversarial best responses.

Two-team 1v1, 2v2, and 3v3 are team-level two-player zero-sum environments.
FFA is general-sum and receives separate coalition, kingmaking, survival, and
placement metrics, playlists, and ladders.

An `evaluationProfileId` owns lineup construction, participant/team
assignment, payoff interpretation, and its compatible dynamics adapter. Slice
2 implements `two-team-zero-sum-v1`. Planned profiles such as
`team-lineup-zero-sum-v1` and `ffa-general-sum-v1` extend this seam when their
first real experiment is scheduled; FFA will not be bolted onto the duel
payoff matrix.

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

At the present population size, uncertainty intervals do not imply
generalization to unseen policies. Paired seed estimates describe the exact
frozen entrant-pair/seed matrix only. Policy diversity is the important
generalization axis, so reports expose authoring lineages and
leave-one-lineage-out sensitivity. A voting pilot requires at least four
independent lineages; the two-lineage threshold is diagnostic plumbing only.

## Implemented slices

[`scripts/balance-lab-drive.py`](../scripts/balance-lab-drive.py) implements
the smallest complete orchestration path:

- validate that candidates cover the declared factor product exactly once;
- require exact mode/rules/map/format/topology and aggregate fingerprints;
- freeze every entrant source tree and WASM hash;
- freeze the schema, orchestrator, cohort runner, evaluator, and gameplay
  checkout identities, and refuse a drifted resume;
- run complete within-tier cross-play in both assignments on paired seeds;
- verify every replay and reject contract, artifact, runtime, fault, or
  disqualification drift;
- emit per-cell payoff matrices;
- adapt generic Frontline replay v3 into the existing rich dynamics report;
- emit a vector report and same-artifact factorial contrasts.

Slice 2 adds:

- a checked evaluation-profile identity, currently
  `two-team-zero-sum-v1`;
- explicit topology profile and independently replay-verified topology
  fingerprint;
- exact qualification provenance fields on every entrant and population;
- a profile-scoped qualification-contract fingerprint so a Ground result
  cannot silently qualify an Air or FFA population;
- an explicit `balanceVerdictEligible` gate. Diagnostic metrics and contrasts
  may still be calculated for smoke populations, but cannot select or promote
  a candidate. `candidatePromotionEligible` additionally remains false while
  required evaluation layers are explicitly unmeasured;
- the WASM-only `frontline-qualification-2`
  `contract-auto-determinism` component, which repeats both assignments and
  requires identical replay hashes, zero faults, and the declared automatic
  child life. It awards no tier while the cumulative profile is incomplete.

Slice 3 closes the correctness seams needed before population work:

- one spec may contain separate study blocks for compatibility sentinels,
  same-population mechanic causality, rules-native product evidence,
  infrastructure smoke, and adversarial sentinels;
- paired causal arms must declare the same `seedProfileId`. The manual and
  automatic progression arms now share `frontline-labs-duel-depth-1`;
- `--print-candidate-contract` emits the authoritative resolved contract, and
  `scripts/frontline-balance-candidates.py` checks or updates all six spec
  blocks;
- the runner publishes once, fingerprints the complete executable bundle,
  verifies it before and after every cell, and rejects source or toolchain
  drift on resume;
- a decision profile declares voting tier, lineage, coverage, and required
  evidence-layer gates. Missing layers remain explicit and block promotion;
- entrants declare authoring lineage, doctrine, equal-budget packet, and
  packet hash. Duplicate artifact or source identities are rejected;
- hidden seeds use the nonce-backed commit/reveal/consume workflow in
  `scripts/balance-holdout.py`; visible seeds are never called sealed;
- `balance/frontline-ablation-debt-v1.json` is frozen with each run so promised
  isolation experiments remain visible.

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
It declares one disclosed paired seed, no holdout, all six factor cells, and
two explicitly unqualified retained artifacts. Its sole
`infrastructure-smoke` study block proves orchestration, identity,
common-randomness shape, and report generation, not balance.

## Seasonal mechanism extension

Most new forms and transforms are data-defined ruleset changes and enter the
same pipeline as new candidate arms. A genuinely new semantic capability
(Air traversal, shield, mine, heal, and so on) is a closed typed engine/SDK/
replay addition, followed by a new qualification profile and named metric or
ablation evidence. Historical artifacts and replays remain valid; only their
eligibility for the new profile changes.

Season identity never changes simulation semantics. A season curates immutable
playlist versions, and each playlist pins its resolved candidate, admission
profile, qualification requirement, execution policy, and ladder. This lets a
season intentionally obsolete old bots without rewriting the Lab or erasing
historical results.

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
mechanic diagnostics. They are not yet a calibrated population, and their
results cannot choose a balance winner. The immediate milestone is the cheap
cumulative T1–T4 WASM probe suite, followed by at least four independent
T4-or-better lineages under an equal authoring budget. T5-capable policies are
desirable in that first pilot population, not a reason to postpone all play
evidence until the research-heavy T7/T8 ceiling exists.

The first causal population must satisfy the union of the manual and automatic
contracts so the same bots can run in every factorial cell. Native manual and
native automatic product populations remain separate study blocks when
rules-aware adaptation is being judged.

The four-lineage T4 floor starts a pilot; it is not the durable calibration
population. Balance Population v1 targets at least four independent lineages
at each of T2, T3, T4, and T5. Preserve each lineage's passing revisions as
skill-gradient checkpoints, but do not count revisions of one lineage as
independent policies. Add T6 only after the first pilot calibrates the cheaper
tiers. Run complete within-tier and adjacent-tier cross-play; do not multiply
every artifact across every ruleset, season, and format when that block does
not answer the registered hypothesis.

## Automation guardrails

Automated optimization will exploit weak metrics and shared bot blind spots.
Every promising candidate therefore needs:

- committed, privately revealed holdout seeds and later holdout maps;
- independently authored capability-qualified policies;
- restricted-play ablations;
- newly authored adversarial sentinels; automated best-response search is
  deferred until a credible population exists;
- small parameter perturbations;
- header-only, outcome-blind replay sampling before aggregate disclosure;
- human notes on tension, legibility, repetition, endings, and bot-authoring
  experience.

A numerical pass with a dull or incomprehensible replay remains a hold.

## Slice-3 smoke result

The corrected all-WASM run completed and verified all 12 planned replays with
zero faults or disqualifications. It used a frozen 30-file toolchain and a
shared seed profile across progression arms. With only two unqualified
entrants, one paired seed, and two games per cell, it remains plumbing
evidence only.

The earlier slice-1 smoke used ruleset-specific seed profiles, so its
cross-progression numbers were never valid common-random-number causal
evidence. They are superseded by the corrected run. Even the corrected run is
too small and population-poor for map or progression conclusions: no candidate
is promoted, no population-generalization interval is claimed, and every
product evidence layer remains unmeasured.

## Current execution order

Further Lab hardening is paused. The critical path is:

1. finish cumulative, deterministic T1–T4 qualification;
2. retain every independently authored bot and revision;
3. qualify the four-lineage T4+ pilot floor, then continue building the
   four-per-tier T2–T5 population pyramid;
4. run tier calibration and the registered six-cell causal block with
   adequate paired seeds plus
   separate rules-native product blocks;
5. lock the outcome-blind replay sample and author-DX notes;
6. decide what to tune from that evidence.

All duel conclusions are provisional for 2v2 and 3v3. Duel competence
transfers; team-information, assignment, crossfire, reinforcement, and map
capacity conclusions require team-native populations and study blocks.
