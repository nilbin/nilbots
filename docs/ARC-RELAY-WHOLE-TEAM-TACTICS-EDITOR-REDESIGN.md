# Arc Relay whole-team Tactics editor redesign

Status: design recommendation  
Date: 2026-08-18

## Decision

Replace the current player-facing Programs editor with a substantially higher
abstraction. Keep the existing deterministic game, compiler vocabulary and
Instructions underneath, but do not ask players to author them.

The player model is:

> **A Sheet is an ordered list of whole-team Tactics.**  
> **A Tactic divides all eight bots into Groups, then coordinates a Stage and
> optional Act phase.**

The editor starts from a blank map and no Tactics. Presets are building blocks,
not a pre-authored strategy.

## Player-facing concepts

### Squad

The player chooses eight actual bot cards. Each bot receives a class-aware
build preset such as Balanced, Durable, Aggressive or Scout; the upgrade path
is generated and not normally shown.

### Tactic

Every Tactic is one complete operation for the entire team. All eight bots must
belong to exactly one tactic-local Group. Normally a Tactic has one to three
Groups.

Groups replace the useful part of Roles without retaining their complexity:

- they exist only inside one Tactic;
- they directly contain visible squad bots;
- they do not own classes, builds, global priority or independent triggers;
- one bot cannot belong to two Groups in the same Tactic; and
- every bot has a visible assignment.

### Two phases

Each Group has:

1. **Stage** — where and how it assembles, spreads, occupies Posts or approaches
   an objective, plus its posture while waiting.
2. **Act** — an optional second plan after release: advance, converge, return,
   follow, patrol or keep holding.

The Tactic owns one shared **Release** and one shared set of **Outcomes**.
Stage-only Groups simply continue their Stage behavior after release.

```text
Tactic
├─ Group A: bots → Stage → optional Act
├─ Group B: bots → Stage → optional Act
├─ Shared release
└─ Shared outcomes / maximum attempt
```

## Creation flow

1. Open a blank Sheet and choose the eight-bot Squad.
2. Tap **Create Tactic**.
3. Choose a named recipe or **Blank Tactic**.
4. The recipe creates sensible Groups and opens one guided wizard.
5. Distribute all eight bot tokens between the Groups.
6. Draw each Group's Stage directly on the map: zone, route, spread or exact
   Posts.
7. Choose the shared Release: immediate, once required Groups are ready, or
   ready plus a simple map/event trigger.
8. Optionally draw each Group's Act movement and choose a posture preset.
9. Confirm the suggested Outcomes and maximum attempt.
10. Review the map and plain-language summary, create the Tactic, then reorder
    it in the Sheet's top-to-bottom list.

Selecting an existing Tactic reopens these same authored answers. The editor
never reverse-engineers generated Instructions.

## Recipes are shortcuts, not different systems

Every recipe fills the same Tactic structure:

| Recipe | Stage | Release | Act | Suggested outcomes |
| --- | --- | --- | --- | --- |
| Hold Area | Occupy a zone or exact Posts | Immediate or enemy contact | Keep holding | Threat gone or persistent |
| Centre Rush | Gather near centre | Ready or immediate | Advance down a route aggressively | N kills, Core acquired, target reached or timeout |
| Pincer | Holders occupy; flankers spread | All required Groups ready plus enemies present | Holders remain; flankers converge | N kills, area clear, Core acquired or timeout |
| Core Raid | Raider approaches a Well; others screen/control | Core available or immediate | Steal and optionally return | Core acquired, banked, lost or timeout |
| Patrol | No Stage or gather first | Immediate | Patrol a route | Persistent |
| Deliver Core | Carrier and support Groups resolve dynamically | Friendly carrier exists | Carrier returns; escorts protect; remainder screen | Core banked or lost |

Defence and Ambush are not separate mechanics. Both are Hold Area recipes;
their geometry, readiness and release preset make them feel different.

## Posture and behavior presets

Each phase uses a small contextual posture such as Wait, Hold, Aggressive,
Steal, Escort or Observe. A posture bundles ordinary Fight, chase, recovery,
Core and signature behavior. Only relevant choices appear, and selecting one
immediately updates the map preview and sentence.

Class-specific actions remain spatial:

- place a Mason wall target;
- place flare, mine or sentry markers;
- mark Dash segments on a route;
- choose facing from a Post; or
- choose a Handoff/escort preset for a carrier Group.

The player never separately configures Roles, capacities, Conditions,
readiness relationships, custody cases, Opportunity hosts, lifecycle fields or
generated Instructions.

## Release and Outcomes replace `until`

The player answers two ordinary questions:

- **When do they go?** This ends Stage and releases every Group.
- **When is this Tactic finished?** Any selected outcome ends the Act.

Suggested Outcomes are intent-aware: target area clear, N confirmed enemy
destructions, Core acquired, Core banked/lost, destination reached or area
observed. The recipe supplies a sensible minimum commitment and maximum
attempt; raw ticks and boolean expressions stay hidden.

Outcomes count after Release by default, so a holding Group cannot accidentally
complete a Pincer while the flankers are still staging.

When a Tactic finishes, the ordered Tactic list is reevaluated. There are no
explicit links or calls. If an aggressive Tactic can finish by acquiring a
Core but no Deliver Core Tactic exists, the editor warns and offers to create
one. Core value handling remains a small preset on that delivery plan.

## Priority and comprehensibility

Tactics are evaluated top to bottom and apply to the whole live team. Their
shared activation is generated from team-visible state; every Group has an
in-Tactic continuation so an individual bot does not silently fall into an
unrelated lower Tactic.

Each Tactic card reads back the complete operation, for example:

> Two bots hold Mid while six stage behind the enemy. Once both Groups are
> ready and enemies occupy Mid, the six converge aggressively. Finish after
> three destructions, Mid clears, a Core is acquired, or the attempt expires.

Design toward a later ranked cap of roughly eight Tactics. Depth should come
from whole-team composition, geometry, phase coordination, posture, outcomes
and priority—not the number of exposed fields.

## Architecture boundary

Do not add another editable layer above Programs. Create one player-owned
Tactics document and deterministically materialize the existing Orders and map
plan. Current Program code may be reused internally, but neither Programs nor
their generated Instructions remain independently editable sources.

Existing advanced sheets may remain in a labelled Legacy editor or receive a
one-way import when recognizable. Perfect round-trip conversion is not a
requirement. No new runtime scheduler, Program call, atomic action system or
second priority model is required.

## Acceptance gates

- Starting blank, a player can create a functional first Tactic without seeing
  engine terminology.
- All eight bots are visibly assigned exactly once in every Tactic.
- Hold Area, Pincer, Centre Rush, Core Raid and Deliver Core are each
  constructible through their guided wizard and editable later through the same
  answers.
- A precise two-Group Pincer takes no more than ten meaningful choices plus its
  map gestures.
- Every Tactic and Group has a plain-language summary and visible map footprint.
- At least three strategically distinct functional Sheets fit within eight
  Tactics.
- Each flagship recipe has deterministic materialization, a real match proof
  and an automated browser interaction test.

This redesign succeeds when creating a Sheet feels like drawing and directing
whole-team operations, not configuring the executor.
