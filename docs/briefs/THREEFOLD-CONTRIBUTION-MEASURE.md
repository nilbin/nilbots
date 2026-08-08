# Threefold Pulse: pre-registered direct-objective-contribution measure

**Registered 2026-08-05, BEFORE any Threefold evaluation game was played or
analyzed.** This definition is frozen for the prototype: results are
reported against it as written, and any change after the first evaluation
run would be disclosed as a protocol break. Owner brief:
`THREEFOLD-PULSE-PROTOTYPE-BRIEF.md`.

## Unit of analysis

The unit is one **completed Pulse cycle** of one team: the half-open tick
interval `(previousPulseTick, pulseTick]` for that team, with the first
cycle starting at tick 0. Incomplete trailing cycles are reported
separately and never count toward the primary target.

A body participates in a cycle if it satisfies **at least one** qualifying
predicate below during the cycle's interval. Each body counts at most once
per cycle (its contribution *types* are all recorded). The primary metric
is, per completed cycle: the number of distinct own-team bodies (unit
slots, not lives — a respawned body is the same slot) with at least one
qualifying contribution. The pre-registered target: **median ≥ 6 of 8**
across completed cycles, reported per team and pooled, with the full
distribution and per-body type breakdown.

## Qualifying predicates (all derived from replay facts, no intent guessing)

Let a **required Core** for team T at tick t be any Core whose origin
socket is unfilled for T at t. A **contest window** around a Core event is
the interval `[event - 8, event + 8]` ticks, and a **contest radius** is
Chebyshev ≤ 3 of the Core's position at the event.

1. **CARRY** — the body carried any Core for ≥ 8 ticks within the cycle,
   or made net home-ward map-distance progress ≥ 3 tiles while carrying
   (`core-relocated` facts with `relocationKind: carried-movement`).
   Carrying a Core whose origin socket is already filled qualifies only if
   predicate 6 (denial hold) also holds.
2. **BANK** — the body banked a Core that filled a socket
   (`core-banked`).
3. **PICKUP-CONTEST** — the body picked up any required Core
   (`core-picked-up`), or was the killer of record (`sourceActorId` on a
   `destruction`) of an enemy body within 8 ticks after that enemy picked
   up a Core required by the enemy (a steal-by-kill), or its kill caused a
   `core-dropped` within the same window.
4. **COMBAT-IN-CONTEST** — the body dealt or received damage
   (`damage` events, either direction), or STARTED a combat signature
   (`signature-changed`, reason `started`, any category except the medic
   repair channel), inside the contest radius and window of any
   `core-picked-up`, `core-dropped`, `core-banked`, or carrier
   `destruction` event involving a Core required by either team.
5. **CARRIER-SUPPORT** — while an own carrier held a Core required by the
   own team: the body healed that carrier (`repair-beam` targeting it),
   or damaged / was damaged by / killed an enemy within Chebyshev ≤ 3 of
   that carrier's position at the moment of the interaction.
6. **DENIAL HOLD** — the body held a Core whose origin the ENEMY still
   requires while the own socket for that origin is filled, for ≥ 20
   ticks, AND the enemy banked no Core of that origin during the hold.
   (This is the deliberate contest-a-duplicate play; a shorter incidental
   hold does not qualify.)

## Explicit non-qualifying behavior (from the brief, made concrete)

- Proximity to a Core, Well, carrier, or theater without a predicate above.
- Waiting, formation membership, standing orders, posture, or facing.
- Transit through a theater, including route-following toward a Well,
  until a qualifying event occurs.
- Damage dealt or received with no required-Core event in window and no
  carrier within radius (pure brawling is not objective work).
- Being alive, respawning, or occupying a defensive position near the own
  reactor, unless predicate 4 or 5 fires there.

## Reporting requirements

For every evaluation arm: per-cycle participant counts (distribution, not
just median), per-body predicate types across cycles, per-class
aggregation, and side/seed splits. The same script computes the identical
measure for the pre-Threefold comparison cohort (socket-derived predicates
degrade gracefully: under -03 rules every Core is "required" until the
generic charge is full).

## Audit script contract

One script (`scripts/` promotion from scratchpad once stable) takes a
replay directory and emits per-cycle JSON: `{team, cycleIndex, pulseTick,
participants: [{unitId, classId, predicates: [...]}]}`. It reads only
replay facts listed above; it never reads mind internals. Its output is
committed beside each evaluation round.
