# ConvoyFresh — North-Lane Catch Ladder

ConvoyFresh is an Arc Relay-native participant-scoped mind for the **convoy**
coverage cell. It commits seven bodies to one primary route and assigns one
swift picket to the two off-route Wells.

## Composition

| Slot | Class | Stable role tag | Job |
| ---: | --- | --- | --- |
| 0 | Relay | `pickup-carrier` | Acquires the primary Core and starts the return. |
| 1 | Repulsor | `forward-catcher` | First durable catch station. |
| 2 | Palisade | `armored-catcher` | Mid-route catch and conditional Prism Wall. |
| 3 | Patchbay | `home-catcher` | Final catch, repair support, reactor delivery. |
| 4 | Hush | `null-screen` | Suppresses clustered denial near the convoy. |
| 5 | Towline | `hook-screen` | Pulls a visible enemy carrier out of its route. |
| 6 | Sunder | `paint-screen` | Focus-marks enemy carriers and screens with fire. |
| 7 | Lantern | `far-well-picket` | Minimal contest of the two non-primary Wells. |

All eight classes are distinct, inside the public one-or-two-copy rule.

## Doctrine

The earliest-producing Well is the primary Well. The mind derives a mirrored
home-to-Well route from the public map and biases equal paths through the row
two tiles north of that Well. Four bodies occupy ordered stations along it:

`Relay -> Repulsor -> Palisade -> Patchbay -> reactor`

A handoff is submitted only when the receiver is adjacent, empty, waiting,
strictly closer to home, and at a strictly later ladder stage. Each
Core/source/receiver tuple is submitted at most once. The construction cannot
bounce a Core back to an earlier stage or repeat the same-pair handoff loop.
When a catcher is absent, the carrier may skip forward but never backward.

Hush, Towline, and Sunder travel with the current friendly carrier. They prefer
visible enemy carriers for suppression, displacement, paint, and gunfire;
otherwise they occupy adjacent screen tiles. Palisade walls only when a
friendly carrier and a nearby threat justify it, Patchbay repairs actual hull
loss, and Repulsor bursts only without an adjacent ally. A one-hull Relay may
use Arc Toss to move a recovered Core toward home when no immediate catch is
available. Lantern uses Survey Flare around an off-route Well when its Core is
outstanding or its public birth clock is near.

Movement is chosen only from each body's typed legality mask. Destinations
exclude walls, current bodies, visible hostile projectiles, already claimed
destinations, and every visible spawn reservation. Static routing and all
spatial decisions mirror automatically for either participant assignment.

## Authoring boundary

This doctrine was authored from the Gate 3 public author packet, experimental
rules brief, public SDK comments/types, public CLI contract printout, and the
fresh generic-mind scaffold. No cohort opponent, result, replay, gallery,
scorecard, engine implementation, or prior Convoy source was inspected.

Only build/static/load validation is permitted for this fresh artifact. No
match outcome was used to choose or tune the composition, stations, route, or
action priorities.
