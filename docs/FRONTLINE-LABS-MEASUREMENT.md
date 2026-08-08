# Frontline Labs measurement framework

Status: provisional measurement contract for population and bot-quality work.
It does not declare `frontline-labs-1` balanced or ready for ranked play.

The reusable, mode-independent orchestration and evidence-vector architecture
now lives in [`NILBOTS-BALANCE-LAB.md`](NILBOTS-BALANCE-LAB.md). This document
defines the Frontline adapter's domain metrics and historical thresholds; new
multi-factor runs use a Balance Lab spec rather than an ad-hoc directory
convention.

## Why the old scorecard is insufficient

The first scorecard mixed engine activity, bot competence, match pacing, and
watchability. In particular, `firstMeaningfulInteractionTick` counted ordinary
movement, so a bot leaving spawn made most games report tick zero even when
combat or a strategic confrontation came much later. Aggregate active time
also hid a large doctrine-specific failure: Pressure left child slots Ready
for the rest of the match.

Balance numbers are not interpretable while the calibration bots routinely
miss direct shots, ignore immediate projectile paths, or fail to activate
available bodies. The scorecard therefore has four ordered gates.

## Capability ladder

The canonical ladder is
[`BOT-CAPABILITY-AND-SOLVABILITY.md`](BOT-CAPABILITY-AND-SOLVABILITY.md):
eight cumulative individual tiers (`T1`–`T8`) and six independent
coordination grades (`C0`–`C5`). Do not compress projectile skill, navigation,
population strategy, adaptation, and teamwork into one unexplained label.
The replay-native probe plan is
[`BOT-QUALIFICATION-SUITE.md`](BOT-QUALIFICATION-SUITE.md).

For continuity with older evidence, the former bands map only as follows:

- **floor** means T2 reactive fundamentals;
- **capable** was too broad and must be replaced by an exact T/C claim,
  normally T4–T6;
- **expert** was also too broad and must be replaced by T7–T8 plus the
  applicable coordination grade.

Invalid, random, or mechanically broken bots are `R0` robustness probes, not
balance entrants. The game must be active and legible at T2. The first
internal pilot may vote with four independent cumulative T4+ lineages and
should include T5-capable policies; a later public/ranked balance population
centers on T5–T6. T7–T8 tests whether strong play becomes solved, degenerate,
or deadlocked and is deferred until the lower population is credible.
Multi-participant evidence must also declare its coordination grade.

The post-reveal shared-fundamentals cohort is mechanically credible T2
evidence, not a claim that all real entrants meet that standard. A one-pass
independently authored cohort is a T1/T2 and developer-experience screen
unless its entrants pass higher qualification probes. Tournament records do
not establish bot quality: a sweep can mean a strong mechanic, a weak
opponent, or both. Never select a numeric rule value from a one-pass win table
alone.

For T4+ balance evidence, qualify behavior before opening the final
comparison:

- demonstrate the mechanic the entrant claims to evaluate;
- meet declared direct-fire, evasion, objective, population, and inactivity
  gates relevant to the rules hypothesis;
- use an equal, pre-registered improvement budget;
- preserve every authored revision rather than retaining only a champion;
- freeze holdout seeds before result-informed iteration;
- run the same final artifacts under every numeric A/B arm and mirror
  participant assignments.

Report failed qualifications as bot findings, not game-balance findings.

Coordination-grade evidence should add diagnostics alongside the ordinary
action counts: the share of active bodies that contribute attacks or
objective weight, multi-body attacks on one target within a short window,
multi-attacker eliminations, attacks enabled by allied visibility, and useful
damage from bent projectiles. These describe strategic headroom; they are not
minimum T2 requirements.

## Duel-depth falsification screen

Do not assume that adding bodies can rescue a trivial 1v1 foundation. Before
using the companion phase as the primary balance target, isolate the opening
Prime-versus-Prime phase and test the smallest intended mind game:

- observations and decisions remain deterministic and simultaneous;
- an existing projectile advances after movement;
- its current heading and advance timing are public, but its committed future
  curve remains private;
- an ordinary straight-path dodge is available often enough to be baseline
  behavior;
- the active objective makes different safe destinations carry different
  territorial and firing-tempo costs.

Use a retained, mirrored, rules-native micro-cohort with at least an obvious
geometric dodger, a territory holder, a private-curve predictor, and an
adaptive or seeded mixed shooter. Start with straight or one hidden bend so
the action space is watchable. A one-pass version first tests authorability
and obvious T1/T2 behavior; it does not establish that the named policies are
strong examples of their doctrines. The 1v1 foundation fails a
capability-qualified screen if one
simple evasive policy avoids damage and preserves control against every legal
trajectory family, or if private-curve policies cannot outperform straight
fire from both assignments. It passes provisionally when no universal
response dominates, stronger prediction creates either damage or measurable
territorial concessions, and the same situation is legible in outcome-blind
replay review.

The point is not to make a single deterministic matchup produce different
results when replayed. Reproducibility remains mandatory. Strategic
uncertainty comes from the opponent's private committed choice, unknown
policy, and optional deterministic private random stream.

## Gate 1: validity

Every evidence replay must be complete replay v3, hash-valid, deterministic
under replay verification, and generated by the declared rules, map, runtime,
artifact, and seed identities. Any runtime fault or disqualification fails the
arm.

## Gate 2: calibration-bot adequacy

Record each entrant separately; cohort totals may not hide a weak doctrine.

- `directAttackOpportunityTurns` counts an available attack with a visible
  enemy on a contract-range eight-way ray reachable by the current
  omnidirectional aim or initial programmed-aim allowance.
- `directAttackOpportunityUseShare` is the share of those turns on which the
  bot submits an attack. It is narrower than “enemy visible and attack
  available”; the latter does not imply a credible shot.
- `imminentProjectileThreatTurns` counts a hostile visible projectile whose
  current heading reaches the body on its next advance.
- `imminentThreatMovementResponseShare` is the share of movement-available
  imminent-threat turns on which the bot submits movement. This is a response
  diagnostic, not proof that the selected tile is a successful dodge and not
  evidence of strategic depth.
- Record imminent threats that coincide with an obvious direct shot, threats
  received while holding the active objective, and turns with multiple
  imminent projectiles. These expose whether projectile pressure creates an
  action or territory tradeoff instead of merely testing routine evasion.
- Record successful and non-successful decision shares, launched-shot to
  damage conversion, and movement toward/lateral/away from the active
  objective.

For the mechanically credible native bot cohort, use these provisional adequacy
warnings:

- direct-shot use below 70% with at least 25 observed opportunities;
- imminent-threat movement response below 40% with at least 20 observed
  movement-available threats;
- non-successful decisions above 2%;
- an entrant that never demonstrates one of its declared doctrine mechanics.

These are authoring warnings, not gameplay balance targets or tier
boundaries. A policy may justify a particular non-response, but the
explanation and replay examples must be recorded before the cohort can
support balance claims.

## Gate 3: population and tempo

Locked future slots are excluded from the denominator. Once a slot unlocks,
its active, Ready, fabrication-pending, destruction-recovery, and automatic
return time is eligible population time.

Record per entrant:

- active body ticks and average bodies per match tick;
- active eligible-slot share and full-eligible-population ticks;
- Ready slot ticks, Ready share, Ready-to-active median/p90 latency, and
  terminal Ready episodes;
- average bodies in contract-derived phases: before the first unlock, after
  unlock 1, after unlock 2, and so on;
- attacks, damage, destructions, and life creation per 100 match ticks.

A fabrication-transport or automatic-reinforcement arm must, against the same
candidate-aware policies:

- reduce Ready-slot share by at least 50%, or bring it below 5%;
- increase average bodies after the first unlock by at least 5%;
- not reduce attacks or damage per 100 ticks by more than 10%;
- not increase games with a 75-tick combat-free run.

The absolute alternatives matter because a cohort that already fabricates
reasonably cannot produce a 75% debt reduction or 25% population increase
within a three-slot ceiling. This is a causal population gate, not yet a
shipping gate.

## Gate 4: objective pacing and watchability

Record first combat event, longest combat-event-free run, first contest,
contested/sole-control ticks, contested-to-sole evictions, pushes, push
reversals, lead changes, duration, and terminal reason. Keep the older
stagnation/repetition diagnostics, but do not use them as substitutes for
combat or objective interaction.

Report post-combat active-objective occupancy as empty, sole-team,
equal-weight contest, and unequal-weight contest ticks. Also report how often
a global objective-capable body advantage becomes the same team's
active-objective weight advantage. These measures distinguish a control rule
that has no effect from bots that never concentrate their companions where
the rule applies.

Treat projectile pressure as useful even when it does not deal damage if it
forces a credible concession: leaving objective control, giving up an
otherwise available attack, entering a worse lane, or consuming movement
while an ally advances. A lone straight projectile should normally be
avoidable. Strategic depth must come from the cost of that response and from
creating multi-body or curved-path situations with no universally safe
choice. Future scorecards should therefore report threat/attack coincidence,
threatened objective holds and retreats, multi-projectile threats, and
multi-attacker sequences in addition to raw damage conversion.

The intended early/mid/late shape is provisional and must be evaluated across
declared matchup bands, not inferred from one equal-budget round robin:

- at least 10% of games can end before the first unlock;
- at least 25% end between the first and final unlock;
- at least 25% reach the fully unlocked phase;
- no more than 25% contain a combat-event-free run of 75 ticks;
- at least half of cross-tier games end by objective breach;
- a materially stronger calibration policy can win cross-tier games from both
  participant assignments;
- same-tier T5–T6 games may end by timed territorial ranking, but no more
  than 60% should reach MaxTicks and their median should not simply equal the
  cap.

These bands deliberately allow an early win without making early events
deterministic, allow close games to reveal late composition, and avoid
punishing the game merely because evenly matched bots use a legitimate timed
finish. Same-tier T2, same-tier T5–T6, T2-versus-T5+, and later T6-versus-T8
evidence should be reported separately. Revisit these values after
outcome-blind review at 1x on mobile and desktop; statistical motion is
necessary but not sufficient for an entertaining replay.

## Current baseline diagnosis

Across the 24 verified baseline-v2 WASM matches:

- median duration is 430.5 ticks, with 10 MaxTicks endings;
- attacks run at 26.3 and damage at 14.9 per 100 ticks;
- 12 matches contain a combat-event-free run of at least 75 ticks;
- direct-shot use is 85.3% Adapter, 45.9% Bastion, 52.0% Fabricator, and
  100% Pressure;
- imminent-threat movement response is 22.6% Adapter, 50.5% Bastion, 24.6%
  Fabricator, and 14.5% Pressure;
- active eligible-slot share is 58.4% Adapter, 79.6% Bastion, 60.8%
  Fabricator, and 33.2% Pressure;
- Pressure accumulates 3,518 Ready-slot ticks and never activates a child.

The next work is therefore a native bot-competence pass plus an isolated
fabrication-transport experiment. Numeric capture, turret, projectile, or map
balance should wait until that population produces credible tactical play.

## First competence and control screen

The four retained mechanically credible T2 bots add prompt fabrication,
narrow direct-fire execution, imminent-path dodging, and
objective-preserving dodge selection equally to the four doctrines. Across
the 12 hosted WASM matches,
direct-shot use is 79–87%, imminent-threat movement is 66–84%, every game has
reciprocal multi-tick damage, and there are no faults, stalls, loops, or
75-tick combat gaps. Bot adequacy is therefore high enough to expose rules
behavior, but the dodge rates do not establish T4+ play.

The screen also changes the diagnosis:

- hosted reaches MaxTicks in 10/12 games and every match reaches the fully
  unlocked phase;
- remote explicit fabrication reduces aggregate Ready share from 10.3% to
  3.6%, raises post-first-unlock average bodies from 1.71 to 1.86, increases
  attacks/damage per 100 ticks, and reaches MaxTicks in 9/12;
- late capture gain `300:2` remains at 10/12 MaxTicks, so clock speed is not
  the primary problem;
- net objective-weight control raises pushes from 63 to 74 but remains at
  10/12 MaxTicks and 2/12 breaches; reversals stay at 42;
- all 12 net-control games reach the final unlock, and only 102 contested
  ticks carry unequal objective weight after policies adapt.

Remote fabrication passes the revised causal population gate but is not a
standalone pacing solution. Net control is retained as a generic contract
experiment because it makes companion advantage mechanically meaningful, but
it is not promoted as a pacing fix. The next screens must include the
competent generated starter, explicit cross-tier matchups, and at least one
teamwork-aware T5/C2+ policy rather than asking independently competent bodies
to demonstrate coordination they do not implement.

## Balance Lab progression smoke

The corrected mode-independent Lab slice ran the complete
`3 map topologies × 2 progression policies` product with identical artifacts,
shared seed profile, mirrored assignments, exact replay verification,
full cross-play, a frozen executable bundle, and retained reports. All 12 WASM
replays were valid and fault-free.

This was explicitly `infrastructure-smoke` evidence. Its GeometricDodger and
InitiativePlanner artifacts have no cumulative tier, so their outcomes cannot
choose a production map or lifecycle. The older smoke had ruleset-specific
private seed profiles and therefore did not establish common-random-number
causality; its apparent topology/progression effects are superseded. The
corrected two-game cells validate plumbing only, not a tuning verdict or a
population confidence interval.

The automatic cell is a coherent progression-policy bundle—declared first-life
activation, automatic child return, assigned spawns, and no Prime
Fabricate/Split—not a single-flag causal ablation. Future reporting must retain
that label until a capacity-safe restricted-play design holds the other
mechanics constant.

The next balance work is not more orchestration: finish the cheap cumulative
T1–T4 probes, retain at least four independent qualified lineages under an
equal improvement budget, and then run the registered six-cell block plus
outcome-blind replay review. Empirical equilibrium work, automated
best-response training, and candidate search are deferred until that
population exists.
