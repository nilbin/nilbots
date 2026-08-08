# Frontline duel theory

Status: executable local and map-wide last-mile analysis for the smallest
programmed-shot game. This does not claim that the complete 500-tick
multi-body game is solved. Bot evidence grades and practical solvability
targets are defined in
[`BOT-CAPABILITY-AND-SOLVABILITY.md`](BOT-CAPABILITY-AND-SOLVABILITY.md).

## Method

The shot/dodge interaction is a finite imperfect-information game and should
be checked before bot tournaments:

1. enumerate every legal committed projectile path from the actual rules and
   map;
2. advance it with the real launch and per-tick traversal cadence;
3. group paths that produce the same public projectile position and heading;
4. enumerate every legal hold/cardinal response at each indistinguishable
   decision;
5. apply movement-contact, traversal-contact, wall, and strict-corner rules;
6. remove dominated shot and response choices;
7. separately value damage, objective occupancy, movement/firing tempo, and
   resulting position.

A useful local game has neither an unavoidable shot nor a zero-cost universal
response. At least two private choices must remain observationally identical
when the defender commits, and nominally safe responses must carry different
territorial or tempo costs.

`FrontlineLabsDuelTheoryTests` executes the canonical path/timing claims below
through the engine path generator.

## Current projectile chronology

The mobile projectile has range 8, launches one tile on the firing tick, and
then advances two tiles per tick after actor movement. The target sees current
projectile position, heading, cadence, and remaining range, but not the
committed future bend.

That ordering is important: a bend only creates prediction if its alternatives
share the same public prefix until the target has chosen its movement.

## First-contact suppression choke

A common one-pass-bot engagement is shooter `(8,7)`, facing east, against
target `(14,7)`.

The wall clusters above and below the central lane make this a suppression
choke:

- a four-tile left or right bend terminates at `(12,7)` before turning;
- a five-tile bend reaches `(13,7)`, but its diagonal is then consumed by a
  wall or strict corner;
- the target's immediate north and south tiles are themselves walls.

Straight fire is therefore the rational family. The target must retreat or
change lane before the last moment; a curved shot should not outperform
straight fire in this state. A bot that indiscriminately curves whenever an
enemy is forward is low quality, not evidence that programmed shots are
globally weak.

## Central objective prediction chamber

The existing map and one-bend envelope already contain a clean private game
at shooter `(8,7)` against target `(12,7)`. A bend after three tiles leaves
straight, left, and right paths publicly identical at `(11,7)` when the
target must make its last meaningful response.

The exact hit matrix is:

| Private shot | Hold `(12,7)` | North `(12,6)` | South `(12,8)` | East `(13,7)` | West `(11,7)` |
| --- | ---: | ---: | ---: | ---: | ---: |
| Straight | hit | safe | safe | hit | hit |
| Left | safe | hit | safe | safe | hit |
| Right | safe | safe | hit | safe | hit |

West moves onto the currently visible projectile and is dominated. East has
the same damage row as hold but moves away from the team-1 objective approach.
Ignoring those dominated variants leaves the 3×3 identity game:
straight/left/right against hold/north/south. With damage as the only payoff,
the symmetric mixed equilibrium assigns each choice probability 1/3 and
produces a 1/3 hit probability.

The real payoff is richer:

- hold stays on the active objective and preserves the firing action;
- south stays on the active objective but consumes movement/firing tempo;
- north consumes movement and leaves the objective.

Those unequal costs let positioning, score state, health, and opponent
tendencies shift the mixture without producing one universal answer.

## Design consequence

The minimal one-bend mechanic is theoretically capable of supporting a
non-trivial duel. The failed one-pass cohort did not reach and exploit its
prediction chamber reliably; it stopped at first visibility and overused the
suppression choke.

Do not add more curve parameters merely to rescue those bots. First preserve
the readable three-choice core and validate the state flow:

- the map should contain both straight-suppression lanes and open prediction
  chambers;
- objective value must reward entering the chamber enough that permanent
  long-range suppression is not equilibrium play;
- a competent starter should distinguish a wall-terminated curve from an
  actual intercept path;
- balance specialists must demonstrate both corridor suppression and
  objective-chamber prediction before their match records receive voting
  weight.

## Map-wide one-bend coverage

The executable test now enumerates all clear cardinal engagements whose
target is two to five tiles away. Bending after `distance - 1` tiles leaves
straight, left, and right publicly identical until the defender's final
response. The analysis advances from the last position that can actually
appear in an observation: launch tile 1 followed by two-tile advances. It
removes moving onto the currently visible projectile as damage-dominated,
then classifies the remaining response matrix.

| State class | All map states | Objective-centred states |
| --- | ---: | ---: |
| Full three-choice private fork | 488 | 43 |
| Partial private fork | 354 | 38 |
| Universal last response | 836 | 82 |
| Forced early evasion | 72 | 0 |

Private prediction therefore survives in 842/1,750 (48%) of the map states
and 81/163 (50%) of the objective-centred states. No objective-centred state
forces a last-moment hit. In 132/163 (81%) objective states, at least one
private shot leaves both a safe response that remains in the region and one
that exits it. Every universal response requires movement; Hold is hit by at
least one private path in all 836 such states, so “universal” does not mean
zero-cost.

Projectile cadence creates a strong parity effect:

| Range | Private-fork states | Universal-response states | Forced-early states |
| --- | ---: | ---: | ---: |
| 2 | 448 | 114 | 42 |
| 3 | 88 | 392 | 0 |
| 4 | 268 | 76 | 30 |
| 5 | 38 | 254 | 0 |

At odd ranges, a two-tile advance often leaves a universally safe one-step
move away from the shooter. At even ranges, the last public projectile tile
is adjacent and straight/left/right more often cover Hold and the two lateral
responses. Map geometry should therefore make odd-range escape surrender
objective position or firing tempo, while deliberately staging important
prediction chambers around the even-range interaction.

Every current universal response consumes movement; none permits Hold. The
projectile language therefore passes the no-zero-cost-defense gate even where
it does not form a full private fork.

## Map geometry candidates

The current objectives are generally wider along the east-west advance axis
than across it. That lets an odd-range move away from the shooter sometimes
remain inside the objective. A candidate geometry rotates each objective into
a three-tile strip perpendicular to the advance axis:

- `(4,8..10)`;
- `(7,4..6)`;
- `(11,6..8)`;
- `(15,4..6)`;
- `(18,8..10)`.

This changes no walls or projectile rules. On primary east-west engagements,
the current regions produce 26 full three-choice forks among 48
objective-centred states, and 16 states where a universal response can remain
inside the objective. The perpendicular strips produce 20 full forks among
30 states and only four universal-stay states: 54% to 67% full-fork coverage,
and 33% to 13% universal-stay coverage.

That is the intended payoff change: move away and concede the line, or stay
and play the private shot matrix. It is not yet a map verdict. A three-tile
region also reduces late-body capacity, so the final geometry must scale
lateral capacity with the playlist's expected active population. A 2v2 or
3v3 map should add lateral lanes and contest pockets rather than merely reuse
the duel strip.

The objective-only candidate is implemented as the content-identified
`thin-fronts` map arm.

The wall geometry remains a separate lever. The implemented
`outer-shoulder-bypass` arm opens `(8,6)`, `(8,8)`, `(14,6)`, and `(14,8)`.
On the left approach, excluding the direct `(9,7)` choke, this shortens the
route from `(8,7)` to central entry `(10,7)` from eight moves to six. The inner
walls at `x=9` and `x=13` remain closed: a policy gets an earlier, costly route
choice without receiving a universal last-moment lateral dodge. Opening those
inner shoulders would erase the intended T4 discontinuity and is therefore
not the first wall arm.

## Entry initiative

The exact east-side mirror makes the current skill discontinuity concrete.
Against a shooter at `(14,7)`, a defender that waits at `(9,7)` until the last
public range-five state has one universal response: retreat to `(8,7)`.
North and south are walls. Moving one tile earlier to `(10,7)` reaches the
active objective and converts the next range-four interaction into the full
three-choice private fork.

The current map therefore asks a policy to move before it has final
information, use projectile/cooldown timing, and then make a mixed positional
response. A purely reactive dodger will retreat forever. This is a legitimate
T4 boundary if stronger policies can cross it often enough to keep matches
active; otherwise the outer-shoulder bypass is the cleaner first map test.

## Team-size consequence

An open-grid executable proof places two range-four straight projectiles on
perpendicular approaches. Either projectile alone leaves a lateral response;
together they cover Hold and all non-suicidal cardinal moves at the last
decision. The defender must break the crossfire geometry or cadence earlier.

That is the desired 2v2/C3 discontinuity: a locally sound duelist still needs
formation awareness, threat assignment, and allied timing. More participants
should increase the number of such relations and counter-formations. It does
not justify increasing the shot-program envelope.

These are strong local geometry results, not a full-game solution. They omit
diagonal initial engagements, actor occupancy, multiple projectiles, and
multi-tick reachability. The next theoretical task is a short-horizon entry
game: determine whether a T4–T6 policy can time movement from the known
six-tile suppression choke into the central prediction chamber without
accepting forced damage. Only if that entry game has a rational
non-engagement equilibrium should map geometry or one explicit
stalemate-pressure mechanism change.
