# Frontline Labs cohort baseline

Status: pre-registered exploratory balance run. This document does not declare
`frontline-labs-1` balanced or ready for ranked play.

## Question

Does the first hosted generic Frontline contract produce enough strategic
variety, interaction, and match-length variation to justify a narrow numeric
tuning pass?

The baseline must expose obvious failures before any values change:

- a spawn or seat advantage;
- a dominant rush, Fabricate, Split, or Anchor doctrine;
- passive turret or objective deadlocks;
- routine max-tick endings;
- mechanics that a competent bot never finds worth using;
- runtime faults or non-deterministic results.

## Frozen baseline

- playlist: `frontline-labs`, version `1`
- ruleset: `frontline-labs-1`
- map: `frontline-labs-01`, version `1`
- format: `head-to-head`
- runtime contract profile: `generic-actor-match-2`
- engine source: the commit recorded in the run manifest
- candidate artifacts: the exact WASM hashes recorded before the first match

Persisted playlist version 1 is immutable. A changed rule or map arm receives a
new ruleset/map identity and playlist version; it must never reinterpret
already-created version-1 matches or replays.

## Candidate cohort

Four bots are independently authored with the same information and budget:

1. pressure — seeks early territorial advantage and breach opportunities;
2. fabricator — values additional active bodies and coordinated crossfire;
3. bastion — uses Anchor as area denial without abandoning the mobile objective;
4. adapter — changes doctrine from the visible team, score, and objective state.

Each author gets one implementation pass. Compile errors, contract
misunderstandings, deterministic crashes, and immediately faulting actions may
be repaired without strategic feedback. There is no normal improvement pass.
If one shared mechanic is misunderstood, exactly one equal-budget repair pass
may be granted to every author and all versions remain archived.

These bots are a calibration cohort, not official champions. Every entrant is
retained outside `champions/` with:

- source and `botarena.json`;
- canonical submitted WASM and SHA-256;
- doctrine and authoring-budget manifest;
- exact contract, rules, map, engine, and seed identities;
- results plus links to preserved replay evidence;
- every submitted revision, when a repair pass was necessary.

## Match matrix

The initial matrix is one mirrored round robin:

- 4 bots;
- 6 unordered pairings;
- seeds `104729`, `130363`, and `155921`;
- both participant/side assignments for every pairing and seed;
- 36 total matches.

Artifacts are frozen before results are read. A replay is valid for the
all-WASM result matrix only when both participants use the final archived WASM,
the replay is complete replay v3, the payload hash verifies, and neither
participant faults.

The local in-process runner may be used for authoring and mechanical repair.
All reported outcome data is reproduced with the WASM runtime.

This 36-match matrix describes the historical first baseline. Later cohort
sprints default to seed `104729` and both assignments: 12 matches for four
bots. The first run established that its deterministic policies behaved
identically across the three original seeds. Add seeds only when a candidate
bot consumes private randomness or the mechanic hypothesis requires them.

## Recorded outcome measures

For the full matrix record:

- win, loss, draw, terminal reason, score, and tick count;
- result by participant slot and map side;
- head-to-head record for every pair;
- runtime fault and disqualification counts;
- median, p10, p90, and maximum match duration;
- breach, score/tiebreak, and max-tick ending rates.

This run raises a balance warning when any of the following occurs:

- any runtime fault or deterministic replay mismatch;
- either participant slot wins more than 65% of decisive matches;
- one bot earns more than 50% of all available non-draw match points;
- any bot has no win or draw against the rest of the cohort;
- more than 25% of matches reach the tick cap;
- fewer than 50% of matches end through an objective breach.

The thresholds are diagnostic gates, not proof that results inside them are
balanced. A 36-game matrix is too small for a production verdict.

## Recorded dynamics

Replay-v3 analysis records, by match and doctrine:

- submitted and successful actions;
- direct-shot opportunities/use and imminent-projectile movement responses,
  treated as mechanical diagnostics rather than strategic skill;
- imminent threats coinciding with an available attack, threats on the active
  objective, and multi-projectile threats;
- Fabricate attempts/completions and child active time;
- eligible population, Ready-slot debt, Ready-to-active latency, and
  contract-derived phase body density;
- Split attempts/completions and replica active time;
- Anchor attempts/completions/cancellations and turret active time;
- movement, attacks, damage, destruction, returns, and objective transitions;
- score-lead changes and the ticks of first engine activity and first combat;
- longest windows without movement, attack, damage, fabrication, replication,
  form change, capture progress, or score change;
- longest windows without an attack or damage event;
- periods where both teams have a turret but make no territorial progress.

The ordered adequacy, population, pacing, and watchability gates are defined
in [`FRONTLINE-LABS-MEASUREMENT.md`](FRONTLINE-LABS-MEASUREMENT.md). The
original thresholds below remain historical baseline diagnostics; they are not
a substitute for the narrower bot-quality and combat-tempo measures.

The baseline raises a dynamics warning when:

- Fabricate, Split, or Anchor never completes in the cohort;
- an available mechanic succeeds only once, making it impossible to inspect;
- the first meaningful interaction is later than tick 120 in over half the
  matches;
- a no-interaction window lasts at least 75 ticks;
- both teams maintain turrets with no territorial progress for at least 60
  ticks;
- one action or form explains nearly all victories regardless of opponent.

## Replay review

At least 12 valid replays are sampled without exposing bot names or outcomes:

- short, median, and long duration bands;
- every doctrine;
- every terminal-reason class present in the run;
- examples containing Fabricate, Split, and Anchor;
- any stalled or max-tick match.

The reviewer records only observable claims: readability, visible strategic
turns, dead time, comeback potential, mechanic legibility, and whether the
ending feels earned. Bot identity and outcome are revealed only after notes are
locked.

## Tuning rule

The baseline is measured before selecting a value to change. If it reveals a
material problem, the first comparison changes one coherent numeric mechanism
only. The same frozen cohort, seeds, and mirrored assignments run against both
arms.

Examples of one coherent mechanism are:

- Anchor durability/windup/placement;
- capture threshold/decay/redeploy pause;
- Fabricate unlock/delay/placement;
- Split health/output/windup;
- projectile cadence/range;
- lifecycle return timing.

Map geometry, several mechanic families, and bot strategy are not changed in
the same causal comparison. A promising arm still needs a fresh,
candidate-aware cohort and a separate holdout before it can become a balance or
shipping recommendation.
