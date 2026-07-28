# Game-rules evaluation: native generations and watchable dynamics

This is the shared product-evaluation policy for Claude Code and Codex. The
executable workflows are `.claude/skills/balance-harness/SKILL.md` and
`.claude/skills/agent-arena/SKILL.md`.
Multi-factor experiments use the mode-independent spec and runner in
[`NILBOTS-BALANCE-LAB.md`](NILBOTS-BALANCE-LAB.md).

The goal is not the shortest match or the highest count of one result label.
The goal is a deterministic programming game whose matches are strategically
varied, visibly active, understandable, and worth watching.

## Separate the three questions

Every experiment first identifies its immutable candidate as:

`mode + ruleset + map + match format`

It then separates evidence into study blocks with one of these roles:

1. **Regression/compatibility.** Frozen historical artifacts run under the
   candidate to catch faults, determinism changes, degenerate exploits, and
   accidental breakage. This is a safety screen.
2. **Mechanic causality.** The same artifacts, maps, spawns, seeds, and random
   streams run under two arms. This isolates what a mechanic changed for those
   policies.
3. **Product quality.** Bots authored or substantially adapted for a ruleset
   fight one another under that ruleset. This is the primary balance and
   entertainment evidence.
4. **Infrastructure smoke.** Unqualified retained artifacts exercise candidate
   enumeration, provenance, runtime, verification, metrics, and report
   generation. This proves the evidence pipeline, never the game balance.
5. **Adversarial sentinel.** A deliberately hostile policy attacks one metric,
   mechanic, or suspected degeneracy. It diagnoses counterplay and metric
   validity but does not substitute for a diverse native population.

The first two are valuable, but old rules-unaware bots do not veto a
substantial redesign. They cannot reveal a strategy space they were never
written to use. Conversely, comparing different bot generations does not
isolate one mechanic; it compares the resulting products. Report both facts
plainly.

A change is substantial when it changes available actions, observations,
objective economics, projectile/combat timing, survival rules, or ranked map
geometry. A narrow number tune whose strategies remain valid may use a
same-cohort A/B as primary evidence.

Every causal block must share a declared random-stream profile across arms;
equal numeric seeds with different seed profiles are not paired randomness.
Every voting block pins a versioned decision profile, frozen published
toolchain, exact qualification profile, and population coverage. Compatibility
or smoke failures remain visible without silently invalidating a complete
native-product block.

## Native cohort requirements

For a substantial rules verdict:

- commission at least four independently authored or substantially adapted
  candidate-aware doctrines;
- authors receive only player docs, public SDK, CLI, and the experiment brief,
  never engine/design internals or another bot's source;
- give every author the same iteration budget (normally one loss-forensics
  iteration);
- run a native round-robin under the candidate, including mirrors where useful;
- keep historical champions as named sentinels, but report their records
  separately from the native-cohort gate;
- freeze artifact hashes, maps, seed blocks, runtime, and evaluation criteria
  before the final/holdout run;
- use canonical WASM with zero faults for the final evidence.

The minimum four doctrines must also be independent authoring lineages.
Variants from one source lineage are useful ablations, not four independent
samples. Statistical summaries over repeated pair/seed rows are conditional
on the exact frozen population. Generalization comes primarily from policy
lineages, so retain lineage identifiers and inspect leave-one-lineage-out
sensitivity; do not present a pair-seed bootstrap as a population confidence
interval.

The generational product comparison is:

`previous native cohort + previous rules` versus
`current native cohort + candidate rules`.

It is deliberately not described as a paired causal A/B. A same-population A/B
may accompany it as a mechanic diagnostic, never as an old-bot veto.

## Dynamics scorecard

`scripts/replay-dynamics-eval.py` reads canonical replays and reports these
dimensions without combining them into an arbitrary “fun score.”

### Outcomes and safety

- draw, Elimination, Domination, and MaxTicks counts remain separate;
- distinct winning doctrines and the leading bot's share of decided games;
- deterministic replay hashes, WASM runtime, and zero faults are mandatory.

### Combat

- **damage games:** matches containing at least one Damage event;
- **damage per game / per 100 ticks:** volume and length-normalized tempo;
- **reciprocal damage:** both bots dealt damage;
- **multi-damage-tick games:** damage landed on at least two distinct ticks;
- shots and hit-per-shot, interpreted with suppression/eviction evidence.

### Motion and repetition

- **active world tick:** movement, turning, shooting, damage/destruction,
  projectile presence/traversal, or objective-pressure change occurred;
- **stagnant tick:** no such activity and no bot state changed;
- **recent repeat tick:** the complete visible tactical frame (bot
  position/facing/health/status, action families, public projectile state, and
  pressure) appeared in the previous 20 ticks;
- **stalled game:** at least 20 consecutive stagnant ticks;
- **looped game:** at least 20 consecutive recent-repeat ticks;
- movement and turning per 100 ticks;
- Wait share, action-family switch rate, normalized action entropy, and the
  share of decisions inside runs of four or more identical action families.

No single action is inherently bad. A deliberate Wait or repeated pursuit can
be correct. The metrics locate replays to inspect; exact loops, low entropy,
and long inactivity together are the warning.

### Objective interaction

- contested-zone ticks;
- contested-to-sole transitions (physical evictions);
- evictions coinciding with damage;
- rule-specific evidence such as non-Wait scoring, suppression, curved ranged
  hits, and attacks crossing a tile the defender just vacated.

Duration is a viewing-budget guardrail, not an optimization target. Always
report median and p90 at the viewer's five ticks per second, but do not reject
a tense 80-tick game merely because an older instant-ray game ended in 40.
Long repetitive tails should fail through loop/stall evidence and replay
review, not through a blanket “new median must be lower” rule.

## Frontline replay-v2 dimensions

Frontline uses `scripts/frontline-replay-eval.py` rather than weakening the
historical slot-based evaluator with optional v2 fields. It accepts only
complete replay-version-2 documents with contiguous ticks and emits a
versioned JSON report containing every per-match row. The report is
descriptive evidence, not a causal or product verdict.

A repeated group label must resolve to one rules version and fingerprint or
the analyzer rejects it. Maps, match fingerprints, participant artifacts, and
runtime kinds remain explicit cohort metadata so a mixed diagnostic/WASM run
or accidental map mixture cannot hide behind one aggregate label.

Its phase boundary is contract-owned:

- `durationTicks = result.endTick + 1`;
- `durationSeconds = durationTicks / 5`, matching normal viewer playback;
- the ending phase index is the count of
  `frontlineDefinition.lifecycle.fabricationUnlockTicks` at or before the end
  tick;
- the current two-unlock contract names phase 0/1/2 early/mid/late; a future
  unlock count is reported as `phase-N` rather than forced into those names.

Replication and Anchor metrics use authoritative contracts/events:

- fabrication opportunities come from per-actor action masks; attempts from
  chosen actions; successful queues from `fabrication-queued`; births from
  tick-start `fabricated`;
- unlock-to-queue latency is measured against the targeted stable child
  slot for its initial `fabrication` queue; later `rebuild` queues are counted
  but do not get falsely measured from the original unlock. Child actor-ticks
  and peak simultaneous bodies are reported separately;
- Anchor attempts/starts/completions/cancellations use the contract action and
  form-transition events;
- turret actor-ticks use the effective observation form; turret shots use the
  turret-fire action; damage and kills follow exact projectile identity so an
  old life retains attribution.

The generic replay-v3 analyzer classifies same-life routes from their declared
source/target objective weights: weighted mobile to weight-zero is `anchor`,
and weight-zero to weighted mobile is `mobilize`. Other routes retain their
contract action ID. Only Anchor targets count as fortified forms for the
dual-turret no-progress metric; a Mobilize target must never make ordinary
mobile actor-ticks look like turret occupancy.

Territorial score is re-derived from every post-state using the public
contract:

```text
(activePositionIndex - centreIndex) * captureThreshold
  + claimingTeamAdvanceDelta * captureProgress
```

The analyzer reports pushes, opposite-direction push reversals, non-zero lead
changes, and whether a winner was previously behind by any or at least one
full capture threshold. A same-rules replay report can describe these facts;
calling them effects of a turret or tune still requires a frozen paired arm.

An actorless tick has no active life for the named team. A stagnant tick has
identical tick-start and post-state after removing only
`objective.nextTick`, no tick-start lifecycle event, and no projectile
traversal. Run counts, lengths, histograms, and weighted tick shares stay
visible; no new cutoff is smuggled into the evaluator.

The built-in `frontline-rusher`, `frontline-swarm`, `frontline-bastion`, and
`frontline-counterpunch` policies are one-author smoke/calibration fixtures.
They exercise the pipeline but do **not** count toward the four independently
authored native doctrines required for a substantial product verdict.

## Baseline holdout guardrails

These thresholds governed the first prospective territorial-v8 holdout and
remain the starting defaults for substantial-rules evaluations. Each future
holdout must copy, extend, and freeze its exact gates before final data; these
defaults never retroactively promote an already observed arm:

- zero WASM faults and deterministic verification;
- draw rate at or below 10%;
- at least four winning native doctrines and no bot above 45% of decided wins;
- damage in at least 75% of games;
- reciprocal damage in at least 40%;
- multiple damage ticks in at least 60%;
- at least 75% active world ticks;
- stalled games at or below 5%;
- looped games at or below 10%;
- median normalized action-family entropy at least 0.60;
- mechanic-specific evidence proves the advertised interaction at population
  scale.

These are guardrails, not a weighted score. A candidate that barely passes
numbers but looks confusing or repetitive on replay remains a hold. The first
territorial holdout used 35%; after it produced an otherwise excellent field
with a 42.5% leader, the product owner changed the future policy to 45% and
promoted it as an explicit override (DECISIONS #74–#75). The original 35%
failure remains in the record. Future threshold changes should normally be
documented and frozen before their holdout.

Holdouts are commit/reveal artifacts: publish only a cryptographic commitment
before the run, keep the nonce and seed list outside the repository, verify
the reveal, and atomically mark it consumed. A checked-in “holdout seed” is
already disclosed and must be treated as an ordinary development seed.

## Frontline pilot scope

For the first Frontline pilot, cumulative T1–T4 and four independent T4+
lineages are the minimum voting floor. Include T5-capable policies where
available, but do not block the pilot on T7 adaptive exploitation, T8
equilibrium-grade evaluation, empirical equilibrium estimation, automated
best-response training, or candidate search. Those are explicitly deferred
until the population can feed them.

Population size is measured in effective doctrines, not artifact files. At
T1/T2 and most of T3, keep a small canonical set of exact-boundary instruments:
each passes Tn, fails T(n+1), and represents a genuinely different elementary
archetype. Retain lower-tier and intermediate revisions instead of replacing
them with a champion; they calibrate the fun floor, adjacent-tier gradient,
and where a mechanic begins to matter.

At the T5/T6 verdict band, target at least six independently authored effective
doctrines spanning predeclared strategy cells. Continue authoring when payoff
rows, dynamics/action signatures, and restricted-play response show missing
coverage; stop counting new artifacts when those signals show redundancy and
leave-one-doctrine-out conclusions are stable. Run full within-tier and
adjacent-tier cross-play; only expand to distant-tier or all-candidate products
when a registered hypothesis needs them.

Balance Lab currently reports a diagnostic
`payoff-action-form-objective-redundancy-v1` estimate per candidate cell. It
uses normalized payoff-row, accepted-action, form-occupancy, and
objective-residence distances and retains all pairwise evidence. Its fixed
thresholds are not a promotion gate until calibrated on known redundant and
known distinct boundary instruments; until then, use the estimate to request
more doctrines, never to prune archived bots.

Duel results establish duel behavior only. They may guide 2v2/3v3 hypotheses,
but map and rule promotion for team formats requires team-native coordination
qualification and evidence. FFA uses a separate general-sum evaluation
profile, playlist, and ladder.

## After a holdout

Apply every frozen gate literally. One failure is a HOLD even when every other
dimension is excellent; do not average gates into a composite score or weaken
one after opening the results.

Diagnose the narrowest next experiment:

- a safety, determinism, or mechanic failure returns to implementation;
- a dull or confusing representative sample returns to mechanics or viewer
  presentation, depending on the recorded cause;
- a repetition/degenerate-play failure returns to rules or counterplay only
  after inspecting the exact loops;
- an isolated strategy-diversity failure with healthy dynamics freezes the
  rules and leading artifact first, then gives counter-doctrines an equal,
  bounded adaptation on fresh seeds.

That adaptation is a new pre-registered holdout, not permission to reuse the
opened seeds or erase the original result. Reopen rule tuning only if
candidate-aware counterplay cannot meet the frozen diversity target without
destroying the previously passed dynamics.

A product owner may still accept a failed **product** gate after seeing the
result. Treat that as an explicit policy override, never as a retroactive pass:
preserve the failed pre-registration, record who changed the acceptance policy
and why, update the future default, and keep every safety/determinism hard gate
non-overridable.

## Outcome-blind replay study

Numbers cannot certify entertainment. Before reading aggregate outcomes:

1. Select at least 12 replays from the native candidate tournament with
   `scripts/replay-review-sample.py`. It uses headers only, balances maps and
   bot pairings, and orders by a recorded deterministic seed.
2. For replay v1, convert each selected replay to self-contained HTML with
   `scripts/botarena replay <replay.json> --out <sample-dir>`. For Frontline
   v2, preserve the `viewer.html` emitted beside each replay by
   `nilbots experiment frontline`.
3. Watch at normal five-ticks-per-second presentation, preferably on both
   phone and desktop. Do not reveal winner, reason, damage, or duration first.
4. Record 1–5 ratings for legibility, sustained tension, visible
   action/counter-action, repetition/downtime, and whether the ending felt
   earned. Record the tick/range for every confusing or dull passage.
5. Only after the blind notes are locked, inspect summaries and dynamics
   metrics. Diagnose every stalled/looped game and all low-rated samples.
6. Publish a separately labeled 3–5 replay highlight gallery. Highlights
   demonstrate the ceiling; they never replace the outcome-blind sample.

The review notes, sample manifest, artifact hashes, metrics table, and explicit
ship/hold rationale are part of the decision record.

## Commands

```bash
# Outcome/mechanic A/B for one explicitly named cohort.
python3 scripts/balance-eval.py --bots a=... b=... \
  --rulesets <reference>,<candidate> --seeds 101,202,303

# Merge tournament blocks by repeating a group label.
python3 scripts/replay-dynamics-eval.py \
  --group current=/tmp/run/block1/<candidate> \
  --group current=/tmp/run/block2/<candidate>

# Run and measure Frontline replay-v2 calibration/holdout blocks.
nilbots experiment frontline --bot <actor-a> --opponent <actor-b> \
  --runtime wasm --seeds 101,202,303 --out /tmp/frontline/block-1
python3 scripts/frontline-replay-eval.py \
  --group current=/tmp/frontline/block-1 \
  --group current=/tmp/frontline/block-2 \
  --json /tmp/frontline/report.json

# Freeze a reproducible header-only review sample.
python3 scripts/replay-review-sample.py /tmp/run/block*/<candidate> \
  --count 12 --seed 20260724 --output /tmp/review-sample.json
```
