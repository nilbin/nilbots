# CutlineMind — interception doctrine

CutlineMind is a native `IGenericMindBot` for Arc Relay coverage cell C. Its
first question each tick is whether a visible enemy body carries a Core. When
one does, the mind concentrates its displacement and damage tools on that
body, routes the rest of the line ahead of the carrier toward the opposing
reactor, and assigns a recovery body as soon as the Core becomes loose. A
short-lived last-seen belief keeps the cut moving through a visibility break;
it is not used as an authoritative target.

This is deliberately not reactor camping. With no carrier to intercept, bodies
patrol the public Well schedule and move toward outstanding or upcoming Core
lanes. The hostile protected home region is read from the match contract and
treated as impassable by the router. Once an enemy carrier appears, the target
moves with it; once a friendly body carries a Core, delivery to the reactor
preempts denial work.

## Composition

The evaluation sheet declares eight slots under the public two-copy cap:

| Count | Class | Declared job |
| ---: | --- | --- |
| 2 | Towline | Pull a visible carrier off its return line with Tractor Hook. |
| 2 | Sunder | Target Paint the carrier, then join concentrated basic fire. |
| 1 | Longshot | Put its contract-declared long basic gun across the carrier cut. |
| 1 | Repulsor | Reach the cutline and burst an adjacent carrier out of route. |
| 1 | Lantern | Flare the route of an outstanding Core that is no longer visible. |
| 1 | Relay | Prefer loose-Core recovery and return it toward the friendly reactor. |

Role tags are public, stable doctrine vocabulary: `return-carrier`,
`core-recovery`, `carrier-hook`, `focus-paint`, `cutline-burst`, `rail-cut`,
`flare-watch`, `relay-runner`, and `route-guard`.

## Contract discipline

The bot takes the rules, map, body class/capabilities, action identifiers and
codes, typed action constraints, Wells, reactors, Cores, protected home roles,
and visible spawn reservations from the public contract or observation. Its
traffic claims start with every friendly body tile, reserve commanded
destinations, reject visible reservations and hostile home pads, and avoid the
visible near path of hostile projectiles. Static geometry is mirror-neutral;
participant assignment only selects the reactor and protected opposing region
through their public IDs.

The authored evaluation record is [sheet.json](sheet.json). Its
`arc-relay-evaluation-sheet-v0` schema is provisional Gate 3 audit data, not a
player-facing product schema.
