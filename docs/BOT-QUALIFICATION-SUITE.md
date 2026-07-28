# Bot qualification suite

Status: implementation contract for turning the T1–T8/C0–C5 framework into
balance-grade evidence. The mirrored `entry-initiative` T4 component and its
canonical replay/report runner are implemented; the cumulative suite remains
incomplete. Current one-pass cohorts remain T1/T2 and
developer-experience evidence.

## Purpose

A tournament result cannot tell whether a bot is strong, its opponent is
weak, or a mechanic is dominant. Qualification measures capabilities before
opening the A/B result. It is not a second ladder and does not crown a
champion.

The first suite should be `frontline-qualification-1`. Each run records:

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
  --suite frontline-qualification-1 \
  --out path/to/evidence
```

This command is implemented for `entry-initiative`. It returns `0` when both
assignments pass, `3` for a clean probe failure, and `2` for runtime or
contract invalidity. Its report intentionally sets `tierAwarded` to null
until the prerequisite T1–T3 components exist.

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

Static source inspection is not a qualification requirement; behavior on
holdouts is. A legacy bot may intentionally fail a new rules generation
without invalidating its historical result.

### T2 — reactive fundamentals

| Probe | Required behavior |
| --- | --- |
| `direct-fire` | Uses a legal wall-clear obvious shot before the opportunity expires. |
| `straight-evade` | Leaves an exact next-advance straight path when movement is legal and a safe tile exists. |
| `objective-path` | Reaches and holds an uncontested active objective without coordinate constants. |
| `population-ready` | Activates or accepts the declared automatic activation of an immediately useful unlocked body. |
| `respawn-reorient` | Resumes mode-directed play after a fresh life with isolated memory. |

These probes should be easy enough for the generated starter. Avoiding one
visible straight projectile is boilerplate, not a game ceiling.

### T3 — tactical geometry

| Probe | Required behavior |
| --- | --- |
| `wall-terminated-bend` | Does not prefer a curve whose engine/SDK preview terminates before its claimed intercept. |
| `strict-corner` | Predicts strict diagonal corner blocking exactly. |
| `cadence-parity` | Distinguishes the last public state of speed-2 range-three/five shots from range-two/four shots. |
| `cooldown-window` | Uses the opponent's missed-shot/cooldown window for a useful move or attack. |
| `local-form-safety` | Avoids a transformation or replication whose declared windup/placement/health consequence is immediately dominated. |

These are deterministic micro-scenarios. Passing does not require winning a
full match.

### T4 — positional doctrine

| Probe | Required behavior |
| --- | --- |
| `suppression-choke` | Uses suppression or an early exit rather than spamming wall-consumed curves. |
| `entry-initiative` | From both sides, crosses the `(8,7)`/`(14,7)` approach into the central chamber under at least the declared straight and biased-shot controllers instead of retreating forever. |
| `prediction-chamber` | Recognizes the range-four three-choice matrix and values safe destinations by objective and fire-tempo consequence. |
| `front-rotation` | Relocates after a push/redeploy rather than defending an obsolete objective indefinitely. |
| `map-holdout` | Repeats the doctrine on reflected and translated geometry without semantic IDs or coordinates. |

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

`FrontlineLabsQualificationDefinition` and
`FrontlineLabsQualificationCommand` now run the ordinary-contract,
opening-only entry probe from both teams against a deterministic public-SDK
straight-pressure sentinel. It records verified replay hashes, eligibility,
sentinel attacks, action counts, and first-life objective entry. The next
implementation package adds T1–T3 prerequisites and the T5 three-policy
matrix; only then can the report award a cumulative tier.

## Evidence use

- T1/T2 one-pass artifacts vote on authoring quality and the fun floor.
- T4 probes may diagnose map entry and obvious positional loops.
- T5–T6 artifacts are the minimum primary 1v1 balance population.
- 2v2 primary balance needs T6/C3 or better.
- 3v3 primary balance needs T6/C4 or better.
- T7–T8/C5 are ceiling and anti-degeneracy evidence.

The same final artifact hashes run every numeric/map A/B arm. A failed
qualification remains a bot finding and never silently becomes a rules
verdict.
