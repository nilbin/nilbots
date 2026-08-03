# Arc Relay strategy ladder: Home Siege, counter, adaptation

Status: **draft for owner review**

Branch: `codex/game-redesign`

Worktree: `/Users/sebastian.lind/hobby-projects/nilbots-wt/game-redesign`

This document is a self-contained handoff for a three-stage strategy experiment.
Only Stage 1 is the proposed next goal. Stages 2 and 3 are registered follow-up
goals and must not be blended into Stage 1 before the owner reviews its result.

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

## Evaluation discipline shared by all stages

- Development screening may use the tracked in-process batch path when result
  parity is already proven. Final evidence uses the sandboxed WASM runtime,
  verified canonical replays, and the current felt-degeneracy bars.
- Freeze the subject sheet, stock artifact, opponent sheet, map/rules
  fingerprints, seeds, and team assignments before each final cohort.
- Final seeds must be fresh, declared as one complete cohort, and represent
  both team assignments. Never select favorable seeds or rerun only losses.
- Preserve failed frozen attempts. A revision receives a fresh complete final
  cohort rather than silently replacing failed cells.
- A required `9-0` is exact. `8-1` is not completion.
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

Build one declarative **Home Siege** sheet that defeats the current frozen
static baseline **9 wins to 0 losses**, while adding general sheet mechanisms
capable of expressing persistent coordinated strategies.

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
run nine fresh deterministic final matches against the frozen baseline.

Completion requires:

- exactly `9-0` in favor of Home Siege;
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

## Stage 1 deliverables

- The general standing-strategy interpreter and schema validation tests.
- The frozen Home Siege sheet and exact opponent/seed manifest.
- Targeted scenarios for carrier death, scorer death, medic death, occupation
  collapse, individual reinforcement, full regroup, secured-Core transfer,
  fog uncertainty, and anti-stall conversion.
- An outcome-visible replay gallery with opponent, trigger, phase timeline,
  final score, and victory reason.
- `docs/reports/ARC-RELAY-HOME-SIEGE-SHEET-PASS.md`, covering the grammar,
  doctrine, all frozen attempts, final 9-0 cohort, determinism, degeneracy
  checks, visible counters, and limitations.

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

The provisional ambition is the same clear head-to-head standard: a fresh
`9-0` final cohort against the frozen Stage 1 Home Siege, plus a separate
false-positive and ordinary-play read. The exact Stage 2 acceptance gate is
confirmed with the owner before that goal starts.

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

The provisional ambition is `9-0` against the counter while retaining the
accepted Stage 1 result against baseline. Exact gates and whether this requires
two fresh nine-match cohorts are confirmed with the owner before Stage 3.

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
information, opponent identity, seed selection, or weakening the opponent, the
framework has failed even if the match table says `9-0`.
