# Bot qualification suite

Status: implementation contract for turning the T1–T8/C0–C5 framework into
balance-grade evidence. Immutable suite 1 retains the mirrored
`entry-initiative` T4 component. Suite 2 begins a new profile-scoped,
WASM-only foundation with the historical `contract-auto-determinism`
component. Immutable suite 3 implements the complete cumulative
`frontline-duel-depth-union-t2-v1` profile. A suite-3 pass awards T2 for that
profile, but T2 remains authoring/fun-floor evidence rather than a numeric
balance vote. Immutable suite 4 reruns that exact prerequisite and implements
the cumulative `frontline-duel-depth-union-t3-v1` tactical profile. It is the
first tactical profile. Immutable suite 5 reruns that exact prerequisite and
implements cumulative `frontline-duel-depth-union-t4-v1`; it is the current
highest implemented individual qualification and the first tier permitted to
vote in the directional pilot.

## Purpose

A tournament result cannot tell whether a bot is strong, its opponent is
weak, or a mechanic is dominant. Qualification measures capabilities before
opening the A/B result. It is not a second ladder and does not crown a
champion.

Every suite run records:

- suite ID and version;
- source and WASM artifact SHA-256;
- exact resolved match, rules, map, format, topology, and probe-controller
  fingerprints;
- participant assignment, seed, replay hash, and verification result;
- one row per pass predicate;
- per-axis result, cumulative T/C result, and any unqualified axes.

No aggregate score may hide a failed prerequisite. A T5 claim includes T1–T4.
A coordination claim is separate.

## Execution boundary

Each dynamic probe is an ordinary, complete generic replay-v3 match:

- the artifact under test uses the canonical WASM runtime;
- it receives only the normal resolved contract and legal actor observations;
- a framework-owned deterministic controller supplies the opponent or allies;
- special maps, deployments, tick caps, and rules are declared in that
  probe's resolved contract rather than hidden in callbacks;
- both participant assignments and translated/reflected holdouts are used
  where symmetry matters;
- replay verification, zero faults, and zero disqualifications are mandatory.

Recommended CLI shape:

```text
nilbots experiment frontline-labs qualify \
  --bot path/to/bot.wasm \
  --suite frontline-qualification-5 \
  --out path/to/evidence
```

`frontline-qualification-1` is frozen evidence for `entry-initiative`; it may
still run in-process for diagnostics and cannot be reinterpreted as a
cumulative tier.

`frontline-qualification-2` currently implements
`contract-auto-determinism` under profile
`frontline-h2h-one-bend-auto-foundation-1`. It requires canonical WASM, runs
both participant assignments twice under the same seed, verifies every
replay, requires identical hashes between repeats, zero faults or
disqualification, and the contract-declared tick-120 automatic child life.
It records rules, map, format, topology, match, controller, and qualification
contract fingerprints. It returns `0` when this component passes and `3` for
a clean failure, while `profileComplete` is false, `tierAwarded` remains null,
and
`balanceEvidenceEligible` remains false.

`frontline-qualification-3` is the current cumulative T2 profile. It requires
canonical WASM and runs every probe from both assignments:

- `contract-matrix` uses non-default participant IDs, all three stable unit
  slots per team, accelerated declared automatic activation, and a repeated
  replay-hash check;
- `automatic-life-cycle` requires both independently instantiated children
  to take useful mode-directed action;
- `objective-path` requires prompt objective entry and five consecutive
  effective capture ticks from mirrored approaches;
- `direct-fire` requires a legal clear-lane shot and damage;
- `straight-evade` requires successful movement while an observed straight
  projectile can sweep the body within two projectile advances, with no
  damage;
- `manual-fabrication` requires explicit activation and a functioning child
  life under the manual progression policy.

The report pins probe-specific rules, map, format, topology, match,
controller, analyzer, predicate, artifact, and replay identities. It returns
`0` only for a complete T2 pass, `3` for a clean capability failure, and `2`
for invalid contract/runtime/controller evidence. `balanceEvidenceEligible`
remains false because the first numeric-balance voting floor is cumulative
T4, not because suite 3 is incomplete.

The first retained passing reference is
`arena-bots/frontline-labs/qualification-instruments-v1-2026-07-28/house-apprentice`.
It is T2-qualified with its source, WASM, report, and replay-byte manifest
retained. Suite 4 now measures its upper boundary: it retains T2 while failing
the positive-bend and cooldown-window probes.

`frontline-qualification-4` is the current cumulative T3 profile. It first
executes suite 3 into a nested evidence directory, verifies the exact suite,
version, profile, qualification-contract fingerprint, artifact hash, and
runtime identity, then runs five tactical components from both assignments:

- `wall-terminated-bend` requires every accepted curve to damage an off-axis
  visible target; a straight or aim-only shot cannot reach it and the
  opposite bend terminates at a declared wall;
- `strict-corner` rejects a tempting lax curve whose authoritative strict
  diagonal is wall-blocked while retaining objective control;
- `cadence-parity` presents identical geometry under declared range-three and
  range-four rules; only the latter projectile can reach the tested body;
- `cooldown-window` requires measurable post-action objective progress or
  damage during the opponent's declared missed-shot cooldown;
- `local-form-safety` requires retaining useful objective weight instead of
  taking a locally dominated weight-zero same-life transform.

The suite stores curved-projectile attribution, apparent and real threat
counts, post-state objective distances, capture streaks, exact contract and
controller identities, and the nested prerequisite report hash. It returns
`0` only for a cumulative T3 pass, `3` for a clean capability failure that
retains the prerequisite tier, and `2` for invalid evidence.
`balanceEvidenceEligible` remains false because cumulative T4 is the
directional pilot voting floor.

The first retained passing T3 reference is
`arena-bots/frontline-labs/qualification-instruments-v1-2026-07-28/arc-apprentice`.
Its source, WASM, cumulative report, and replay-byte manifest are retained.
Suite 5 measures its upper boundary: it retains T3 and fails only mirrored
current-map entry initiative.

`frontline-qualification-5` is the current cumulative T4 profile. It first
executes suite 4 into a nested evidence directory and verifies its exact
suite, version, profile, qualification-contract fingerprint, artifact, and
runtime identities. It then runs five positional components from both
assignments:

- `suppression-choke` starts the tested bot on valuable active-objective
  ground and requires immediate wall-aware straight suppression rather than a
  wall-consumed curve or unnecessary concession;
- `entry-initiative` requires crossing the known six-tile straight-pressure
  choke into the central prediction chamber, losing at most one HP before
  entry, and establishing five consecutive effective capture ticks;
- `prediction-chamber` requires a threat response that remains on the active
  objective and takes no damage;
- `front-rotation` uses a short declared capture threshold and requires
  leaving the obsolete centre, entering the newly active objective, and
  establishing residence there;
- `map-holdout` repeats pressure entry on the content-identified thin-fronts
  objective topology.

The report pins all nested prerequisite and case identities plus action,
damage, threat, objective-entry, capture, front-advance, and residence
evidence. It returns `0` only for cumulative T4, `3` for a clean failure that
retains the prerequisite tier, and `2` for invalid evidence. A pass sets the
entrant-level `balanceEvidenceEligible` flag; candidate promotion still
requires the declared population, study blocks, and evidence layers.

The first retained passing T4 policy is
`arena-bots/frontline-labs/qualification-instruments-v1-2026-07-28/breach-apprentice`.
It derives from ArcApprentice and adds one contract-driven initiative rule:
when objective progress improves and the next projectile advance is safe, it
crosses before the generic two-advance dodge window forces retreat. It is an
adjacent-tier instrument and potential launch opponent, not an independent
pilot doctrine.

The report should be deterministic JSON plus a short text summary. The CLI
must not mutate a ladder, season, playlist, or submitted bot.

Public probes teach authors what competent play means. Equivalent private
holdouts vary IDs, reflection, translation, map details, actor counts, and
legal tuning values so passing cannot depend on coordinates or probe names.

## Individual probes

### T1 — contract-safe

| Probe | Requirement |
| --- | --- |
| `contract-identities` | Works with non-default participant/team/unit/life IDs and stable ordering changes. |
| `contract-counts` | Handles one, two, and three participant slots and changing ally/enemy/lifecycle counts. |
| `rules-values` | Reads range, speed, cooldown, health, forms, objectives, topology, and action legality from the resolved contract. |
| `runtime-determinism` | Same artifact/contract/seed produces the same decisions and replay hash with no faults. |

The frozen v2 `contract-auto-determinism` probe covers one portion of
`contract-counts`, declared lifecycle values, and `runtime-determinism` on a
two-team, one-controller-per-team, two-slot topology. It does not yet prove
non-default identities, true team lineups, three slots, or varied rules
values, so it remains a historical component rather than T1. Suite 3's
contract matrix plus the varied T2 micro-contracts supersede it for current
cumulative qualification.

Static source inspection is not a qualification requirement; behavior on
holdouts is. A legacy bot may intentionally fail a new rules generation
without invalidating its historical result.

### T2 — reactive fundamentals

| Probe | Required behavior |
| --- | --- |
| `direct-fire` | Uses a legal wall-clear obvious shot before the opportunity expires. |
| `straight-evade` | Leaves a visible straight projectile's declared two-advance hazard path when movement is legal and a safe tile exists. |
| `objective-path` | Reaches and holds an uncontested active objective without coordinate constants. |
| `population-ready` | Activates or accepts the declared automatic activation of an immediately useful unlocked body. |
| `respawn-reorient` | Resumes mode-directed play after a fresh life with isolated memory. |

These probes should be easy enough for the generated starter. Avoiding one
visible straight projectile is boilerplate, not a game ceiling.

### T3 — tactical geometry

| Probe | Required behavior |
| --- | --- |
| `wall-terminated-bend` | Uses a legal curve to hit an off-axis target while rejecting the opposite bend that terminates at a wall. |
| `strict-corner` | Predicts strict diagonal corner blocking exactly and does not fire the invalid intercept. |
| `cadence-parity` | Distinguishes an apparent two-advance ray from a real threat using declared remaining range. |
| `cooldown-window` | Uses the opponent's missed-shot/cooldown window for a useful move or attack. |
| `local-form-safety` | Avoids a transformation or replication whose declared windup/placement/health consequence is immediately dominated. |

These are deterministic micro-scenarios. Passing does not require winning a
full match.

### T4 — positional doctrine

| Probe | Required behavior |
| --- | --- |
| `suppression-choke` | When already holding valuable ground, uses straight suppression rather than conceding it or spamming wall-consumed curves. |
| `entry-initiative` | From both sides, crosses the `(8,7)`/`(14,7)` approach into the central chamber under declared straight pressure instead of retreating forever. |
| `prediction-chamber` | Recognizes the range-four three-choice matrix and values safe destinations by objective and fire-tempo consequence. |
| `front-rotation` | Relocates after a push/redeploy rather than defending an obsolete objective indefinitely. |
| `map-holdout` | Repeats the doctrine from both assignments on the thin-front objective topology without probe-name or coordinate branching. |

The entry probe must report damage, objective progress, and time-to-entry
separately. Requiring zero damage would select passivity; accepting any damage
would select blind rushing.

### T5 — predictive policy

Use the canonical three-action local game:

- shooter choices: straight, left after three, right after three;
- defender choices: Hold, north, south;
- damage-only equilibrium: uniform mixture, one-third hit probability;
- real probe payoff additionally records objective occupancy and lost attack
  actions.

Controller families include fixed Hold/north/south, declared biased mixtures,
and a balanced private mixture. Qualification requires:

1. exploit each persistent bias after an observation window;
2. avoid one fixed response becoming a universal counter to the bot's own
   attack distribution;
3. preserve performance from both assignments;
4. emit deterministic choices for the same private seed;
5. pass untouched mixture/geometry holdouts.

Final hit/regret thresholds should be pre-registered only after the probe
controller itself is replay-audited. Do not pick them from the candidate
artifacts' result distribution.

### T6 — strategic planning

These are shortened but complete Frontline matches with independently varied
state:

- early breach opportunity versus survival;
- first and final population unlock;
- automatic/explicit rebuild policy;
- mobile/turret objective-weight trade;
- Split health/action-economy trade;
- active-objective rotation and redeploy pause;
- ahead/behind health and territorial score.

Pass predicates describe outcomes and avoided degeneracy, not mandatory
action counts. A strong bot may correctly decline Split or turret in one
state.

### T7 — adaptive exploitation

A controller selects one hidden persistent policy, then changes it once at an
undisclosed legal tick. The artifact must:

- exploit the initial bias after evidence;
- detect that the old model stopped predicting;
- change its mixture or position;
- recover without oscillating on every single observation;
- resist a controller that tries to trigger predictable overreaction.

Run fresh switch ticks and opponent families only after the policy is frozen.

### T8 — robust equilibrium-grade

T8 is an evaluation process rather than one scripted scenario:

1. freeze a public compute and decision-time budget;
2. build a diverse bounded best-response pool against the artifact;
3. include search, learned, exploitative, positional, and deliberately odd
   policies;
4. evaluate on held-out maps, tuning values, formats, and seeds;
5. report the strongest discovered advantage and uncertainty interval;
6. rerun the candidate unchanged after every response is frozen.

The label means “low measured exploitability on this declared distribution,”
never “the game is solved.”

## Coordination probes

| Grade | Probe family |
| --- | --- |
| C1 | Two bodies choose stable roles, avoid blocking, and do not duplicate a single-owner task. |
| C2 | Ally-only visibility enables a useful attack; focus target changes when health/position changes. |
| C3 | Two perpendicular shooters establish the executable crossfire pattern, while a defending team detects and disrupts it before last response. |
| C4 | Three or more bodies choose composition/fabrication/transform timing, rotate roles after death, and move pressure with the objective. |
| C5 | The team shares an opponent model through only legal observations/identities, changes a joint mixture, and avoids synchronized predictability. |

Run C-probes with repeated instances and, where the format permits, distinct
artifacts. This separates “one good program scales across its lives” from
“several entrants can interoperate.”

## Rules and map probes precede artifact probes

The framework must reject a bad scenario before blaming a bot:

- exact path matrices contain the intended choices;
- no ordinary objective state forces last-moment damage;
- universal defense, if present, consumes movement, territory, fire tempo, or
  another declared resource;
- spawn-to-objective paths and first-contact timing are symmetric;
- useful states are reachable within the probe tick budget;
- objective capacity matches the intended active population;
- a turret cannot cover every approach while remaining objective-capable;
- 2v2/3v3 crossfire is achievable only after setup, not guaranteed at spawn;
- translated/reflected holdouts remain strategically equivalent.

`FrontlineLabsDuelTheoryTests` covers projectile chronology, the
range-four matrix, exact speed-2 parity, current map last-mile counts, the
entry choke discontinuity, the perpendicular objective-strip candidate, and
the two-projectile crossfire union.

`FrontlineLabsQualificationDefinition` and the qualification commands run the
ordinary-contract,
opening-only entry probe from both teams against a deterministic public-SDK
straight-pressure sentinel. It records verified replay hashes, eligibility,
sentinel attacks, action counts, first-life health/entry, objective residence,
and initial-objective capture progress. Passing requires entry with no more
than one damage taken before it and at least five ticks of effective capture
progress, so touching the region during a blind run-through is insufficient.

Suite 3 supplies cumulative T2, suite 4 supplies cumulative T3, and suite 5
supplies cumulative T4 as described above. Each cumulative suite reruns and
hash-links its exact prerequisite rather than accepting a copied tier label.
T5's three-policy matrix remains the next individual qualification package.

## Qualification profile boundary

A capability tier is not globally transferable by label alone. Balance Lab
entrants carry:

- suite ID and version;
- qualification profile ID;
- qualification-contract fingerprint;
- evidence-file SHA-256;
- awarded T/C values;
- explicit balance-evidence eligibility.

Profiles group semantic capability families rather than every numeric tune.
Safe parameter changes may reuse a profile; adding strategically necessary
Air, a new action family, FFA coalition play, or a new participant arrangement
requires a new or extended profile. A legacy bot keeps its historical result
but may be intentionally ineligible for a new season.

## Evidence use

- T1/T2 one-pass artifacts vote on authoring quality and the fun floor.
- T4 probes may diagnose map entry and obvious positional loops.
- The first internal Frontline pilot may vote with at least four independent
  effective cumulative T4+ doctrines; if four artifacts collapse to fewer
  payoff/dynamics clusters, author more rather than treating aliases as votes.
- T1/T2 and most T3 entrants are boundary instruments. Keep a small canonical
  archetype set that passes Tn and demonstrably fails T(n+1); do not manufacture
  four reimplementations of the same obvious policy.
- A ranked/public balance verdict should be centered on independently authored
  T5–T6 doctrines. Balance Population v1 targets at least six effective
  verdict-band doctrines spanning predeclared strategy cells, then continues
  authoring until redundancy and leave-one-doctrine-out checks stabilize.
- Retained revisions measure adjacent-tier progression but do not increase
  independent-lineage or effective-doctrine counts.
- 2v2 primary balance needs T6/C3 or better.
- 3v3 primary balance needs T6/C4 or better.
- T7–T8/C5 are ceiling and anti-degeneracy evidence, explicitly deferred
  beyond the first Frontline pilot.

The same final artifact hashes run every numeric/map A/B arm. A failed
qualification remains a bot finding and never silently becomes a rules
verdict.
