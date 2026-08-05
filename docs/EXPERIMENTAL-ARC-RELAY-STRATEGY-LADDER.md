# Arc Relay strategy ladder: Home Siege, counter, adaptation

Status: **draft for owner review**

Planning source branch: `codex/game-redesign`

Planning source worktree:
`/Users/sebastian.lind/hobby-projects/nilbots-wt/game-redesign`

Required execution branch: `codex/arc-strategy-ladder`

Required execution worktree:
`/Users/sebastian.lind/hobby-projects/nilbots-wt/arc-strategy-ladder`

This document is a self-contained handoff for a three-stage strategy experiment.
Only Stage 1 is the proposed next goal. Stages 2 and 3 are registered follow-up
goals and must not be blended into Stage 1 before the owner reviews its result.

## Worktree and goal-runner sequencing

Do not execute this ladder in `game-redesign`. The current Arc Relay
play-awareness work must first be committed and integrated into an
owner-accepted `codex/game-redesign` tip. Only then create the required fresh
`arc-strategy-ladder` worktree and `codex/arc-strategy-ladder` branch from that
exact tip, recording the base commit in the Stage 1 manifest and report.

Only one ladder goal-runner may use the execution worktree at a time. Stages 1,
2, and 3 are sequential owner gates. A later stage branches from the accepted,
frozen predecessor only after the prior runner has stopped; no stage may race a
renderer, awareness, or other strategy runner in the same worktree.

## Purpose

Prove that Arc Relay sheets can express an actual strategy ladder rather than a
collection of fixed routes and isolated set pieces:

1. author a coherent strategy that dominates the current static baseline;
2. author a different sheet that recognizes and counters that strategy from
   legal match evidence; and
3. make the first strategy recognize the counter and adapt without knowing the
   opponent's sheet.

The framework is part of the product value. The desired result is not three
hard-coded minds. It is a general, data-driven sheet grammar rich enough to
express execution, recognition, counterplay, recovery, and second-order
adaptation.

## Fixed information and objective boundaries

A mind does **not** receive the opponent's sheet, intended positions, assigned
theatres, routes, or private state. It receives the team's causal observation
union: currently visible enemies and their public state, visible events and
objectives, its own complete roster state, and persistent memory it derives
from those observations. Visible enemy role tags are non-authoritative and may
be deceptive.

Strategy recognition must therefore use evidence such as observed allocation,
movement direction, formation, signatures, Core handling, zone tenure, and
recently remembered positions. Fog disappearance is not a death, an intended
route, or proof of an empty theatre.

Current Core rules matter to all three stages:

- each Well has at most one outstanding Core;
- a loose or carried Core persists until it is banked;
- a scheduled birth while that Core remains outstanding records one pending
  charge rather than creating or stacking more Cores;
- after the outstanding Core is banked, a pending Well rearms and births its
  replacement after the rules-defined delay;
- pickup is automatic when a body occupies a loose Core's tile; and
- a mind may instead avoid the tile or later use legal drop, handoff, or Arc
  Toss actions.

No stage may gain hidden information, change these rules, change the map or
class balance, weaken an opponent, or key behavior to an opponent entrant,
artifact, sheet, or tactic identifier.

## Frozen eligibility contract

Every ladder stage uses this exact felt-degeneracy registration:

- path: `balance/arc-relay-felt-degeneracy-bars-v4.json`;
- schema: `arc-relay-felt-degeneracy-bars-v4`; and
- SHA-256:
  `be728f90a22c36b087cd056ef4efd8bb6ca8400933ddf7fe277c35824a9cb5ef`.

The file, thresholds, detectors, scorecard interpretation, and eligibility
logic are frozen for the entire ladder. They may not be loosened, replaced, or
special-cased to admit Home Siege, its counter, or its adaptation. A siege that
cannot stay eligible is a failed siege; the tactic must change. A suspected
detector defect requires a separate owner-approved goal and invalidates the
affected ladder evidence rather than authorizing an in-study threshold change.

Both sides of every retained final match must pass the frozen bars. Diagnostic
failure replays may be kept only as explicitly ineligible evidence and never
count toward a result or enter the ordinary review gallery.

## Evaluation discipline shared by all stages

- Development screening may use the tracked in-process batch path when result
  parity is already proven. Final evidence uses the sandboxed WASM runtime,
  verified canonical replays, and the current felt-degeneracy bars.
- Freeze the subject sheet, stock artifact, opponent sheet, map/rules
  fingerprints, seeds, and team assignments before each final cohort.
- Final cohorts use five fresh paired seeds, each played from both team
  assignments: ten raw matches with an exact 5/5 side split. Declare the whole
  block before results. Never select favorable seeds or rerun only losses.
- Preserve failed frozen attempts. A revision receives a fresh complete final
  cohort rather than silently replacing failed cells.
- The owner's original dominance ambition was `9-0`. The balanced final gate
  is deliberately stricter: a required sweep is `10-0`; `9-1` is not
  completion.
- Report runtime faults, eligibility failures, final Core deliveries, Pulses,
  charge, reactor integrity, completion reason, and relevant doctrine traces
  for every match.
- Keep existing canonical golden hashes byte-identical. Sheet-specific match
  hashes may change only when the declared sheet data or frozen artifact
  changes.
- Outcome-visible review galleries must identify the actual opponent, strategy
  premise, observed trigger, response, final score, and victory reason.
- Do not generalize a head-to-head result into a claim about the whole game.

---

# Stage 1 — Home Siege versus the static baseline

## Goal

Build one declarative **Home Siege** sheet that exceeds the owner's requested
`9-0` dominance target by defeating the current frozen static baseline **10
wins to 0 losses** across five paired seeds and an exact 5/5 team-assignment
split, while adding general sheet mechanisms capable of expressing persistent
coordinated strategies.

The opponent remains the current real static 3/2/3 stock doctrine with its
ordinary routes, combat, interception, Core scoring, and recovery. It is not a
passive dummy and may not be changed to make the result easier.

## Part 1 — General standing-strategy grammar

Extend the provisional evaluation-grade sheet format with the smallest general
mechanism capable of expressing persistent strategies rather than only bounded
gambits.

It must support:

- named phases with causal entry, success, failure, and transition conditions;
- minimum phase tenure and hysteresis to prevent oscillation;
- authored routes, rally zones, formations, facing, engagement, signature,
  focus-fire, and support policies;
- dynamic assignment from declared candidate pools;
- essential, replaceable, and optional participants;
- explicit respawn behavior: rejoin, rally, replace, or resume ordinary sheet
  behavior;
- persistent but causal memory of observed enemy destruction, conservative
  enemy-unavailable windows, last-seen enemies, secured Cores, friendly zone
  strength, stable control, and time without objective progress;
- formation-wide target selection so bodies focus the same threat;
- coordinated support allocation so two Patchbays do not waste repairs on the
  same target unnecessarily;
- Core tasks for guarding, avoiding pickup, collecting, transferring,
  dropping, and delivering;
- an authored lane parameter; changing north to south must be data-only; and
- a declared scorer pool so losing the preferred runner does not destroy the
  strategy.

No tactic name, sheet ID, fixed unit ID, class composition, map coordinate, or
threshold may be hard-coded into the stock mind. Demonstrate generality by
loading mirrored north/south variants and at least one threshold or composition
variant without rebuilding the mind.

Keep this schema explicitly provisional and evaluation-grade. The eventual
human sheet editor and player-facing representation require their own UX pass.

## Part 2 — Home Siege doctrine

### 1. Assault

All eight bodies rush one authored outer lane through parallel paths. They
fight blockers but do not divert for unrelated Cores or chase enemies into
other theatres.

The first proof sheet may choose either north or south in its data. The grammar
must support both; the mind does not choose a lane by reading an opponent sheet.

### 2. Breach

The initial full team may breach directly. Later waves must assemble at least
five bodies at a forward rally zone for a stable interval before breaching.

### 3. Occupy

Establish a legal perimeter around the enemy home pad and reactor. Opposing
bodies cannot enter protected home-pad tiles, so the formation must use legal
approach and exit tiles rather than pretend it can stand on a spawn.

- Durable bodies cover exits and important firing lanes.
- Two Patchbays remain separated and cross-support the formation.
- The team focuses one exposed or newly returned enemy at a time.
- Displacement, suppression, and defensive signatures prevent an organized
  breakout.
- Bodies do not scatter in pursuit outside the declared siege area.

### 4. Carrier kill and secured Core

Killing an enemy carrier turns the dropped Core into a secured objective.

- Non-runners avoid accidentally collecting it.
- A nearby occupier guards it without standing on its tile.
- If the wrong body collects it, it legally transfers or drops it.
- The Core remains in causal team memory when it leaves current vision; memory
  must be invalidated by contrary legal evidence rather than treated as truth
  forever.

### 5. Convert control into score

The preferred Relay or Kestrel runner collects a secured Core and scores when:

- at least five enemies are conservatively confirmed unavailable;
- at least five friendly occupiers will remain after the runner departs;
- a secured reachable Core exists; and
- the runner is healthy and has a legal route home.

An enemy counts as confirmed unavailable only after legally observed
destruction, and no later than that unit's earliest legal return. Mere absence
under fog never counts.

Add an explicitly authored anti-stall conversion branch for sustained siege
control without scoring. It must remain visibly distinct from the normal
five-enemy trigger, must be disclosed in the report, and may not silently
redefine the primary trigger.

### 6. Reinforce

A returned friendly body runs directly back while at least six friendlies
still physically occupy the enemy-home zone.

### 7. Collapse and regroup

Below six occupiers, the current siege is broken. Survivors withdraw to the
forward rally instead of feeding into the enemy one at a time. Once five live
bodies have assembled stably, the team launches a new breach.

### 8. Repeat until victory

Continue through occupation, conversion, reinforcement, collapse, and
re-breach cycles until the opposing reactor is destroyed. The strategy must
not settle into indefinite Core guarding, home-side passivity, handoff loops,
or isolated respawn feeding.

Composition may be tuned within the eight-slot and two-copy limits. Start from
a two-Patchbay siege composition with a declared Relay/Kestrel scorer pool,
then let causal evidence determine the remaining slots.

## Part 3 — Stage 1 proof

Use disclosed development seeds for iteration. Once the candidate is frozen,
run five fresh deterministic seeds twice each against the frozen baseline,
swapping team assignments for the second leg. The final cohort therefore has
ten raw matches and an exact 5/5 assignment split.

Completion requires:

- exactly `10-0` in favor of Home Siege;
- zero runtime faults and zero cohort-eligibility failures;
- assault, occupation, and scoring conversion visible in causal traces;
- a secured Core banked by the declared scorer path;
- reinforcement and regroup branches exercised in targeted scenarios and in
  live matches whenever their conditions arise;
- no indefinite Core parking, sustained passivity, pickup/drop cycling,
  ping-pong handoffs, spawn-feed loop, or use of hidden information; and
- verified canonical replays for every final match.

The proof must distinguish tactical execution from match outcome. A Home Siege
phase transition succeeding in a lost match is not a win, and a win caused by
unrelated baseline behavior is not doctrine evidence.

## Part 4 — Dual ruling: grammar proof and camping alarm

Home Siege is a disciplined spawn-camp. A `10-0` result therefore has two
simultaneously true interpretations:

1. **Sheet-grammar success:** the standing-strategy framework expressed and
   executed the requested long-horizon doctrine.
2. **Balance alarm:** the registered Gate 1 / Gate 2 risk **Home camping
   dominates** fired against the current static defender.

The Stage 1 report must run and publish the registered camping diagnostics:

- opponent-final-third body-ticks;
- kills and Core drops by distance to the enemy reactor;
- camp-to-delivery conversion; and
- counter-deliveries conceded while the camp is active.

It must state the dual ruling plainly. A perfect sweep is not evidence that the
game is already broken, nor may it be presented as an ordinary balance pass.
Stage 2's competent evidence-based recognizer is the intended first answer to
the alarm. Pending that counter-sheet test, Stage 1 authorizes no map, rules,
spawn-safety, class, or balance reaction. If a competent Stage 2 counter cannot
answer the frozen siege, the registered Gate 2 response—map gate, cover, and
route geometry under a new map fingerprint—becomes the next design question.

## Stage 1 deliverables

- The general standing-strategy interpreter and schema validation tests.
- The frozen Home Siege sheet and exact opponent/seed manifest.
- Targeted scenarios for carrier death, scorer death, medic death, occupation
  collapse, individual reinforcement, full regroup, secured-Core transfer,
  fog uncertainty, and anti-stall conversion.
- An outcome-visible replay gallery with opponent, trigger, phase timeline,
  final score, and victory reason.
- `docs/reports/ARC-RELAY-HOME-SIEGE-SHEET-PASS.md`, covering the grammar,
  doctrine, all frozen attempts, final 10-0 cohort, frozen-bar proof, camping
  alarm diagnostics, determinism, visible counters, and limitations.

Stage 1 stops after posting its report and gallery. Do not begin Stage 2 in the
same goal.

---

# Stage 2 — Recognizing counter-sheet

Stage 2 begins only after the owner accepts Stage 1 and freezes the exact Home
Siege sheet and artifact as an opponent.

## Intended goal

Create a distinct sheet that recognizes Home Siege from legal evidence and
counters it. It must not simply start in a permanent anti-siege posture and
must not know the opponent identity or sheet.

The recognizer should combine multiple causal signals, for example:

- several enemies observed committing to the same outer approach;
- sustained enemy progress toward the home side;
- an unusual absence of enemy objective contest only when that absence is
  actually observed;
- formation density and support hardware consistent with a siege wave; and
- repeated reinforcement along the same corridor.

It needs confidence, freshness, hysteresis, and a false-positive release path.
It must be tested against non-siege sheets so ordinary pressure does not cause
permanent turtling.

The counter itself should be authored through the same grammar. Candidate
responses include a layered choke defense, separated anti-Mortar spacing,
focus fire on Patchbays, displacement that breaks the occupation ring, route
denial, and one or more scoring bodies that exploit the theatres Home Siege
abandons. Exact composition and response are evidence questions, not hard-coded
requirements.

The provisional ambition is the same balanced head-to-head standard: a fresh
`10-0` final cohort across five paired seeds against the frozen Stage 1 Home
Siege, plus a separate false-positive and ordinary-play read. The exact Stage 2
acceptance gate is confirmed with the owner before that goal starts.

Stage 2 freezes its successful counter before Stage 3. Stage 1 may not be
modified during the counter proof.

---

# Stage 3 — Adaptive Home Siege counters the counter

Stage 3 begins only after the owner accepts and freezes the Stage 2 counter.

## Intended goal

Revise Home Siege through sheet data and general grammar—not opponent-specific
code—so it recognizes the counter's observable posture and chooses an
appropriate response.

Possible authored branches include:

- feinting the original lane before rotating the assembled wave;
- delaying the breach while the counter consumes defensive signatures;
- splitting a small scoring threat from the siege without dissolving the main
  formation;
- changing breach geometry or focus priority when the choke defense is
  confirmed; and
- abandoning a losing occupation early enough to reassemble elsewhere.

Recognition must again use causal evidence with uncertainty and release rules.
It may classify a defensive posture; it may not classify an entrant ID.

Stage 3 must preserve the original strategic purpose rather than merely become
a bespoke anti-counter sheet. Its final proof should therefore re-run both:

- the frozen Stage 2 counter; and
- the original frozen static baseline.

The provisional ambition is `10-0` across five paired seeds against the counter
while retaining the accepted Stage 1 result against baseline. Exact gates and
whether this requires two fresh ten-match cohorts are confirmed with the owner
before Stage 3.

---

# What this ladder is meant to prove

At the end of all three stages, the useful result is not that one composition
is strongest. It is that Arc Relay sheets can express:

- a long-horizon strategic objective;
- coordinated execution and formation;
- causal opponent recognition under fog of war;
- a deliberate counter;
- recognition of that counter;
- conditional adaptation without thrashing;
- casualty, respawn, and objective recovery; and
- visible counterplay a human can understand in a replay.

If a stage can succeed only through a tactic-specific branch in C#, hidden
information, opponent identity, seed selection, weakening the opponent, or
relaxing a frozen eligibility bar, the framework has failed even if the match
table says `10-0`.

## Stage 3 gate proposal: the cohort replaces the parity control (2026-08-05)

The v2 finals false-positive reads exposed the parity control as a soft
yardstick: depth-map cohort strategies beat it comfortably. A full cohort
read of Breakwater v2 (in-process analysis lane, both orientations, all 32
entrants — `evidence/breakwater-v2-cohort-read.json`) scores **50/64**:
twenty entrants swept, ten split by orientation, and exactly two —
`balanced` and `sensor-grid` — win both ways. The parity control would sit
mid-table in its own gate.

Proposed Stage 3 baseline gate, for owner ratification:

- Keep the frozen-champion pairings and blind-holdout discipline unchanged.
- Replace the parity-control cells with a cohort slice: the candidate must
  take at least 75% of games across the full cohort (both orientations)
  AND may not be swept 0-2 by any single entrant.
- The entrants that beat the reigning deliverable both ways (currently
  `balanced`, `sensor-grid`) are named in the next goal as explicit
  counterplay targets, the way `four-down-double-relay` was for v2.

This keeps the gate honest as the population strengthens: every new
deliverable is measured against the field it will actually meet, not
against a control frozen at an earlier meta.
