# Arc Relay Shadow Scissors exhibit

Date: 2026-08-14  
Branch: `codex/shadow-scissors-exhibit`  
Base: `main` at `4b3d9f91`  
Loop profile: `tapline-3`  
Runtime: trusted in-process tactical mind

## Question

Before changing Tapline or adding two corner Wells, can a deliberately extreme
four-plus-four strategy use the current map's full outer corridors to approach
from behind and still participate in the game?

This is an exhibit, not a battery or a balance read. It answers what the idea
looks like in six fixed games. It does not estimate a population win rate.

## Controlled comparison

`tapline-shadow-scissors-v1` and `tapline-mid-rush-control-v1` use the same
eight classes:

`Kestrel, Kestrel, Veil, Hush, Towline, Sunder, Patchbay, Relay`.

Shadow Scissors sends four bodies through the full north perimeter and four
through the full south perimeter. Each wing stages behind the opponent. When at
least three bodies from both wings are ready, the condition latches and all
eight collapse on the enemy-side Well. A carrier then takes the direct return
route; the outer route is an ingress, not a compulsory return.

The control keeps all eight in one direct pack, takes centre, and moves to the
enemy-side Well while its Core is outstanding. Composition, map, rules, runtime,
and review cells are held constant. The authored strategic path is the intended
difference.

Both sheets share one explicit carrier safety rule exposed by the first draft:
handoff toward home when a receiver exists; at point-blank enemy contact with no
receiver, drop the Core forward and fight rather than let two unarmed carriers
deadlock head-on.

## What happened

### Geometry viability

The outer strategy is real rather than aspirational:

- all eight Shadow Scissors bodies entered the joint breach at tick 47 in all
  six cells;
- rear-contact fighting began at ticks 49, 50, 51, 50, 49, and 51;
- Shadow Scissors picked up the enemy-side Well's Core at ticks 61, 61, 77, 92,
  66, and 62;
- no subject body reported route-unreachable, no-path, route-bypass, or
  delivery-timeout;
- no subject body exceeded 100 consecutive stationary ticks.

The map therefore permits a readable north/south pincer. The long corridors are
not decorative dead ends, and the planner can execute the authored routes.

### Tempo and conversion

The direct control banked its first Core at ticks 49, 84, 54, 53, 87, and 54.
Shadow Scissors' first banks were absent, 132, 155, 159, absent, and 152. Its
first enemy-side pickup was early in every cell, but converting that theft into
a bank was much less reliable.

| cell | Shadow Scissors result | final standing |
| --- | --- | --- |
| `w-9001` | loss at t353 | pulses 0-3, charge 0-0 |
| `w-9002` | loss at t405 | pulses 0-3, charge 2-0 |
| `e-9005` | loss at t438 | pulses 1-3, charge 1-0 |
| `e-9009` | loss at t334 | pulses 0-3, charge 1-0 |
| `w-9012` | loss at t275 | pulses 0-3, charge 0-0 |
| `e-9004` | loss at t571 | pulses 2-3, charge 1-0 |

The record is 0-6 for this authored pincer against this authored direct control.
That is an honest description of these six games, not a claim that every flank
strategy is weak or that Tapline is imbalanced. The most specific read is:

> the current map allows the maneuver, but this first full-perimeter version
> does not yet earn back its opening travel cost through denial or conversion.

The exhibit separates two questions that would otherwise be conflated. The
geometry works. The strategy's economy does not yet.

## Diagnostic correction retained in the reference

The first six-cell build was refused by the frozen felt-degeneracy bars. In
`w-9012`, opposing loaded carriers met at the central choke and held the same
tiles for 81 and 83 ticks; `e-9004` had an 85-tick subject-carrier hold. Local
tiles that looked open were not legal bankward routes through the corner-strict
map, and neither unarmed carrier could clear the other.

The response was authored symmetrically into both sheets rather than hidden or
excluded: handoff if possible, otherwise drop at Chebyshev distance one and
fight. On the two decisive reruns, maximum same-carrier/same-tile runs fell to
2-4 ticks, and formation-freeze stopped tripping. The final six cells pass the
frozen pickup/drop-cycle, handoff ping-pong, team-dance, team-statue,
stuck-carrier, sustained-passivity, parked-unit, and formation-freeze bars.

This is a useful design finding in its own right: a Core is an objective, not a
license for two carriers to become terrain.

## What this does and does not suggest next

Adding two corner Wells remains a plausible way to pay bodies for occupying the
outer map, but this exhibit is not sufficient reason to change the rules. The
lowest-cost next experiment is sheet-side: keep the current map and outer
routes, but specialize each wing into one return runner plus three denial
bodies so the captured Core has a protected exit. That changes tactical labor
without changing objective supply.

If that still produces early enemy-side pickups and consistently late or absent
banks, the map-level hypothesis becomes sharper: Tapline offers a flank route
but insufficient flank value. At that point a corner objective, shorter outer
connection, or other geographic reward can be compared one at a time.

## Reproducibility

Frozen references live under
`arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/strategy-depth/`.

| reference | orders SHA-256 | map-plan SHA-256 | ATL2 SHA-256 | ATL2 bytes |
| --- | --- | --- | --- | ---: |
| Shadow Scissors | `ea0984acc068bfea63d04264df4a50ffe5372b3cfa97e8b04525c434985dd85b` | `b59c332752e41e6dc5914316ebab727f3fc18083385193e92eadef1bf45df87d` | `283b919be3094a37786c493823fc24d5733224a137f639c963483f80af4a9f59` | 5,480 |
| Direct mid control | `2375c7eb7a1970c63e098d2451b01729e766ee0e1aceca2b892baa79ab25e1bb` | `b9232de776338b22509ca923181b45c4f196c436facd370fc86751d16341d4c7` | `f78997f8d63312726449cce746d8417eed6069a549dbe565e362c3abdef4313c` | 2,897 |

## Gallery and validation

Public gallery:
https://ties-kurt-zope-historic.trycloudflare.com/

- standard cells: `w-9001`, `w-9002`, `e-9005`, `e-9009`, `w-9012`,
  `e-9004`;
- six of six ended by reactor destruction;
- six of six passed replay-visible motion sanity;
- six of six passed the frozen felt-degeneracy eligibility bars;
- both references compiled to the pinned ATL2 hashes and sizes;
- the production web build passed;
- phone-sized Chrome smoke loaded the public index and a real replay with no
  console errors, then advanced playback from tick 000 to tick 006;
- no battery, sweep, WASM build, playlist mint, rating change, map edit, or
  balance verdict was made.
