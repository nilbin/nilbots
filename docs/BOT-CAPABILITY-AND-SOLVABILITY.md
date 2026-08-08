# Bot capability and practical solvability

Status: design and evaluation framework for Frontline and later match formats.
It defines evidence grades, not ranked divisions or a claim that the current
experimental ruleset is balanced.

## The actual design target

Nilbots is a finite, deterministic, bounded-horizon game. In principle, every
frozen ruleset, map, format, and seed has a solution or mixed-strategy
equilibrium. “Impossible to solve” is therefore not a defensible literal
requirement.

The product target is **practical intractability without spectator noise**:

- no short, static policy dominates the useful state space;
- strong local tactics have contextual counters and opportunity costs;
- private commitments remain hidden until the opponent has committed a
  response;
- a stronger bot wins by modelling more state and planning better, not by
  discovering one mechanical dodge;
- increasing team size adds coordination and assignment problems rather than
  merely multiplying identical duels;
- the visible action language remains small enough for a viewer to understand.

A great bot may approximately solve one frozen 1v1 playlist. That is an
acceptable ceiling if its strategy is mixed, positional, opponent-aware, and
still vulnerable to a new best response. A 1v1 solution must not transfer into
a complete 2v2 or 3v3 solution.

“1v1”, “2v2”, and “3v3” refer to match-format participant slots, not the peak
number of lives. Fabrication, replication, and respawn can create more bodies.
A playlist's admission policy may place distinct artifacts or repeated
instances of one artifact in those participant slots. The fixed product
terminology remains:

- a **game mode** owns objective and scoring semantics;
- a **ruleset** is one immutable mechanic and tuning revision;
- a **match format** maps participants onto teams and unit capacity;
- a **playlist version** pins mode/ruleset, format, map pool, scheduling,
  matchmaking, and admission;
- a **ladder** rates one playlist-version population;
- a **match contract** is the fully resolved tick-zero input.

## Why team size helps without bloating the shot language

The current map has 233 walkable tiles. Merely assigning distinct positions,
facings, and three health states gives the following deliberately low state
lower bounds:

| Format | Ordered actor placements | With facing and health |
| --- | ---: | ---: |
| 1v1, two actors | 54,056 | 7,784,064 |
| 2v2, four actors | 2,871,995,280 | 59,553,694,126,080 |
| 3v3, six actors | 149,952,617,559,360 | 447,756,116,790,368,010,240 |

These omit projectiles, cooldowns, score, active objective, forms, transition
state, life lineage, additional fabricated bodies, observations, and private
bot memory.

The one-bend experiment has nine mobile shot programs: straight, or left/right
after one to four tiles. Including Wait, four moves, and four rotations gives
roughly 18 ordinary mobile choices before transformations or lifecycle
actions. The simultaneous action-profile count then grows as follows:

| Format | Choices per team | Two-team profiles per tick |
| --- | ---: | ---: |
| 1v1 | 18 | 324 |
| 2v2 | 324 | 104,976 |
| 3v3 | 5,832 | 34,012,224 |

The broad curve language has 219 shot programs and roughly 228 ordinary
mobile choices. It explodes the corresponding two-team profile counts to
51,984, 2,702,336,256, and 140,478,247,931,904. That is syntactic complexity,
not guaranteed strategy: many programs collapse to the same wall-terminated
path, and most are too subtle for a viewer to distinguish.

The design implication is “small verbs, combinatorial interactions.” Team
size, target assignment, crossfire, shared vision, body roles, objectives,
forms, and timing should provide most of the ceiling. Do not use hundreds of
near-duplicate projectile programs as a substitute.

The engine-level local proof also shows why 2v2 adds a qualitatively new
problem. At open range four, one straight projectile leaves a lateral
response. Two synchronized straight projectiles arriving from perpendicular
directions jointly cover Hold and every remaining non-suicidal cardinal move.
The defender must disrupt the formation or cadence before the last response.
That is a readable coordination payoff with setup cost; it does not require a
new attack action. Maps for 2v2/3v3 should make such crossfire achievable but
not spawn-guaranteed.

## Eight cumulative individual tiers

These are capability discontinuities, not arbitrary percentiles. `R0` faults,
invalid actions, random motion, and inert policies are robustness probes below
the skill ladder. They never vote on balance.

| Tier | Name | New capability boundary | Qualification evidence |
| --- | --- | --- | --- |
| T1 | Contract-safe | Reads the resolved contract, emits legal deterministic decisions, and survives unfamiliar counts and IDs. | Zero faults/disqualifications; no hard-coded participant, unit, map, or rule counts in holdouts. |
| T2 | Reactive fundamentals | Moves toward the mode objective, takes obvious direct attacks, evades an obvious next-advance projectile, and activates an immediately useful unlocked body. | Deterministic direct-fire, evade, pathing, and population micro-probes. |
| T3 | Tactical geometry | Predicts exact wall/corner/projectile paths, respects cooldown and action opportunity cost, and rejects locally dominated shots or moves. | Choke suppression, open intercept, collision, cooldown, and transform safety probes. |
| T4 | Positional doctrine | Distinguishes lanes, approach timing, objective chambers, escape routes, form locations, and favorable exchanges. | Reaches and uses both suppression and prediction states; avoids a pre-registered camp/loop suite. |
| T5 | Predictive policy | Uses private shot commitments and seeded mixtures, models likely responses, and cannot be answered by one fixed dodge. | Performs against hold/lateral/biased/mixed response probes from both assignments without one universal counter. |
| T6 | Strategic planner | Trades health, fire tempo, objective pressure, population, transformation, and phase timing over a longer horizon. | Early/mid/late scenario probes plus held-out complete matches; no single mechanic-use quota is sufficient. |
| T7 | Adaptive exploiter | Identifies an opponent's policy, exploits bias, changes policy when the opponent changes, and resists simple counter-exploitation. | Hidden opponent families and mid-match policy-switch tests, followed by untouched holdout seeds. |
| T8 | Robust equilibrium-grade | Approaches a low-exploitability policy across held-out maps, tuning values, topologies, and opponent families within the public compute budget. | Bounded best-response search, adversarial policy pool, cross-rules holdouts, and reported exploitability interval. |

The tier is the highest cumulative band passed. Keep the full probe vector as
well: a bot can be T5 in projectile play and only T3 in navigation. A headline
tier must use the lowest required axis for the claim being made, so excellent
aim cannot hide broken population play.

T8 does not mean mathematically solved. It means that the declared public
best-response battery found only a small advantage against that artifact on
the frozen evaluation distribution.

## Six cumulative coordination grades

Individual skill and team coordination are independent. Record a bot or team
as `Tn/Cm`; do not assume a T8 duelist coordinates at all.

| Grade | Coordination boundary |
| --- | --- |
| C0 | Instances act as unrelated duelists and may block or duplicate one another. |
| C1 | Stable role assignment, collision avoidance, target deconfliction, and deterministic ownership of tasks. |
| C2 | Shared-perception targeting, focus fire, ally-enabled attacks, and basic body covering. |
| C3 | Joint crossfire, forced movement, pincer timing, safe firing lanes, and deliberate local sacrifice. |
| C4 | Team-wide composition, fabrication/transform timing, role reassignment after death, and objective rotation. |
| C5 | A shared opponent model and joint mixed policy that adapts without collapsing into synchronized predictability. |

Qualification must work with the actual runtime boundary: independently
invoked lives coordinate from resolved rules, stable identities, observations,
and visible/shared team state, not an undeclared process-global channel.

## Difficulty target by format

| Format | Fun floor | Balance-grade evidence | Intended ceiling |
| --- | --- | --- | --- |
| 1v1 | T2 is active; T3 creates readable tactics. | Internal pilot: four independent T4+ lineages, including T5-capable policies where possible. Public/ranked: T5–T6 from both assignments. | T7–T8 may approach one playlist's equilibrium, but no short pure policy should dominate. |
| 2v2 | T3/C1 avoids chaos; T4/C2 creates teamwork. | T6/C3–C4 teams. | Exact online solution should be impractical; duel mastery transfers, coordination mastery does not. |
| 3v3 | T3/C2 remains readable. | T6/C4 teams. | T8/C5 is still an approximation; assignment, crossfire, composition, and opponent beliefs remain combinatorial. |

This deliberately makes the entry floor much lower than the balance ceiling.
A starter should already dodge, path, shoot, and use bodies competently. New
authors improve strategy rather than repairing an embarrassing example.

T7/T8 are useful long-term ceiling definitions, not prerequisites for the
first Frontline pilot. Implement and calibrate them only after cumulative
T1–T4 and a genuinely diverse voting population exist.

## Topology and phase scaling

Do not multiply the current three-life team by the participant count. Two
participants each receiving a Prime plus two companions would make 2v2 peak
at six bodies per team; 3v3 would peak at nine. That increases clutter faster
than meaningful roles.

The first reference topology family should add two team bodies across phases,
not two bodies per participant:

| Match format | Early bodies/team | Mid bodies/team | Late bodies/team |
| --- | ---: | ---: | ---: |
| 1v1 | 1 | 2 | 3 |
| 2v2 | 2 | 3 | 4 |
| 3v3 | 3 | 4 | 5 |

The match format/topology assigns each stable unit slot to a participant; a
playlist may repeat one artifact or admit distinct artifacts. Every instance
still receives dynamic topology, team units, rules, and action legality.

This preserves the current phase language while keeping the viewer below
roughly ten active bodies in 3v3. It also makes 4- and 5-body maps a planned
contract case instead of an accidental multiplication. A later large-format
playlist may exceed the cap, but it needs a dedicated map, viewer density
test, and replay review.

The current unlocks at ticks 120 and 260 correspond to 24 and 52 seconds at
normal five-tick playback. Retain those as the first cross-format hypothesis:
an early breach can finish before reinforcements, a mid game exposes one new
team relation, and only close games reach full composition.

Maps should scale relations rather than raw area. The duel map needs a
suppression approach, a prediction chamber, and at least one timing/flank
alternative. A 2v2 map needs at least three useful approaches and orthogonal
crossfire staging that one turret cannot cover simultaneously. A 3v3 map
needs enough separated approach/cover tasks that three bodies cannot reduce
to a single synchronized firing line. Exact tile counts come from
reachability, travel-time, objective-capacity, and viewer-density analysis,
not a blanket “double the map.”

## Ruleset and map acceptance gates

Before a bot tournament can vote on a rule value, static and local-game
analysis should establish:

1. **No ordinary neutral-state forced damage.** Forced early movement may
   exist in a deliberate choke, but not across the scoring surface.
2. **No zero-cost universal response.** A safe response should surrender
   position, objective pressure, fire tempo, information, or another scarce
   resource.
3. **Private choice survives until response.** At least two useful attacks
   share an identical public prefix when the defender commits.
4. **Choices are strategically distinct.** Removing dominated programs leaves
   a small readable matrix, not dozens of aliases.
5. **Prediction states are reachable.** Rational policies can enter them
   without first accepting unavoidable damage.
6. **Non-engagement cannot dominate.** A permanent camp must lose objective
   value or be broken by a clear, contract-declared state transition.
7. **Every form has a contextual job.** Mobile, turret, split descendants, and
   future forms need strengths, costs, and at least one mode-relevant counter.
8. **More participants add relations.** 2v2 and 3v3 maps need multiple useful
   lanes/roles; they must not collapse into two or three independent copies of
   the same duel.
9. **The viewer can read commitment and consequence.** Hidden program choice
   is valid; the later bend, threatened escape, coordinated attack, and
   objective concession must be visible.
10. **The contract remains learnable.** Rules, counts, topology, action
    legality, objectives, and dynamic values are inputs. New seasons may make
    old bots uncompetitive without making their old replays invalid.

Introduce a new mechanism only when it creates a missing payoff topology:
private commitment, a scarce resource, a positional trade, or a team relation.
Adding action variants merely because the game tree becomes larger fails this
gate.

## Recommended reference hypothesis

This is the smallest coherent candidate to falsify next, not a promoted
ruleset:

| Layer | Reference candidate | Reason |
| --- | --- | --- |
| Decisions | Deterministic simultaneous turns and private per-life seed. | Replays remain exact; uncertainty comes from hidden policy/commitment. |
| Mobile combat | HP 3, vision 6, range 8, speed 2, cooldown 2, nine one-bend programs. | Current local matrices are non-trivial and readable; range exceeds vision for prediction/sniping. |
| Population | Automatically activate/rebuild an unlocked companion after its declared delay when placement is available. | With no resource price or alternative use for a Ready slot, repeatedly spending the Prime's action is a dominant chore, not strategy. |
| Objective control | Higher total mobile objective weight controls at the configured base rate; the size of the margin does not multiply gain. | A 2v1 can break a sacrificial contest, while a 3v1 does not snowball three times faster. This constant-gain net policy is a new experimental policy, not the current binary or scaling-net policy. |
| Turret | HP 5, objective weight 0, omnidirectional vision/fire, straight cooldown 1, plus one declared remobilization opportunity. | Strong lane support earns stationary commitment; zero weight and remobilization prevent permanent objective/turret standoffs. |
| Split | Prime-only, pre-transform, data-defined descendant count and health distribution. | Trades durability for action economy and objective bodies without adding a new controller contract. |
| Geometry | Current map plus isolated `thin-fronts` and `outer-shoulder-bypass` arms. | Change positional cost or approach timing before adding combat verbs. |

Automatic companion return and constant-gain net control need isolated
ruleset identities and same-artifact A/B evidence. They should remain
data-declared lifecycle/mode policies visible to bots. The core projectile
physics should stay common across 1v1, 2v2, and 3v3 initially; formats gain
complexity through topology and maps rather than different aiming rules.

If automatic return is rejected because spawning itself should be strategic,
it needs a real scarce input or alternative: energy, mutually exclusive form,
placement choice, or timing risk. An otherwise free body should not require
repetitive boilerplate merely to appear.

The objective-control recommendation follows directly from the three useful
weight functions (`A` is the locally stronger team):

| Objective weights A:B | Current binary presence | Existing scaled net | Recommended capped net |
| --- | ---: | ---: | ---: |
| 1:0 | A gains 1 | A gains 1 | A gains 1 |
| 1:1 | contested | contested | contested |
| 2:1 | contested | A gains 1 | A gains 1 |
| 3:2 | contested | A gains 1 | A gains 1 |
| 2:0 | A gains 1 | A gains 2 | A gains 1 |
| 3:1 | contested | A gains 2 | A gains 1 |

Binary presence lets one fragile body freeze any larger force. Scaled net
makes an uncontested population lead accelerate the clock and compounds a
snowball. Capped net makes every additional body tactically relevant only
until it establishes a majority. It is identical to binary control in the
opening 1v1, so it adds team depth without changing the isolated duel being
measured.

Automatic return has a similar dominance argument. Under the current remote
fabrication experiment, a Ready child has no resource price, no mutually
exclusive use, and a fixed output policy. Delaying it cannot improve the
future state except through incidental pad occupancy, while queueing consumes
one Prime action and adds a useful body earlier. The meaningful engine policy
is therefore automatic queue/retry when eligible. If designers want timing to
remain a decision, they must first add a genuine cost or placement choice.

## Current one-bend/map verdict

`FrontlineLabsDuelTheoryTests` now enumerates every clear cardinal engagement
at two-to-five-tile range on the actual 23×15 map, using the engine path
generator and only projectile positions that can actually appear after
launch tile 1 and a sequence of two-tile advances. Moving onto the currently
visible projectile is removed as damage-dominated. The 1,750 last-mile states
classify as:

| State class | All map states | Objective-centred states |
| --- | ---: | ---: |
| Full three-choice private fork | 488 | 43 |
| Partial private fork | 354 | 38 |
| Universal last response | 836 | 82 |
| Forced early evasion | 72 | 0 |

Thus 842/1,750 (48%) of these local map states and 81/163 (50%) of the
objective-centred states preserve a private prediction fork. In 132/163 (81%)
objective-centred states, at least one shot leaves both a safe response that
stays in the objective and a safe response that leaves it. The scoring
surface contains no last-moment forced-damage state under this model.

The speed-2 cadence creates a useful map-design constraint. Forks occur in
448/604 range-2 and 268/374 range-4 states, but only 88/480 range-3 and 38/292
range-5 states. Odd ranges often admit a universal one-step move away from the
shooter. This is not a free defense: all 836 universal-response states require
movement, so Hold is always punishable and the defender gives up its attack
action. Objective geometry should additionally make that away-step lose
ground where possible. Important prediction chambers should stage likely
engagements at even range; approach lanes can use odd ranges as readable
forced-movement pressure.

The implemented `thin-fronts` map arm rotates the five objective regions into
three-tile strips perpendicular to the east-west advance axis. On primary-axis
engagements, full-fork coverage rises from 26/48 (54%) to 20/30 (67%), while
states where a universal response can stay inside the objective fall from
16/48 (33%) to 4/30 (13%). This is a promising objective-payoff A/B, not a
promotion: a three-tile strip may be too small after fabrication, and
2v2/3v3 maps need proportionally more lateral capacity and lanes.

The implemented `outer-shoulder-bypass` arm opens `(8,6)`, `(8,8)`,
`(14,6)`, and `(14,8)`. It shortens the left-side route around the direct
`(9,7)` choke from eight moves to six, so a bot can branch later without
receiving a free last-moment dodge. The inner walls at `x=9` and `x=13`
remain closed, preserving the exact range-five suppression discontinuity.
Opening those inner walls is not the first candidate because it would remove
that skill test rather than price an alternative.

This is strong theoretical support for the minimal one-bend language. It does
not prove full-game balance: the enumeration excludes diagonal initial
engagements, simultaneous multi-projectile fields, actor occupancy, and
multi-tick reachability. It does prove that another 219-program curve envelope
is not presently justified.

The known `(8,7)` versus `(14,7)` first-contact state is a six-tile
suppression choke outside the enumerated one-bend last mile. Its lateral exits
are blocked, and neither bot occupies the active objective. Weak authored
bots stopped there; that is not evidence against the objective-chamber
matrix. The next analysis must determine whether T4–T6 policies can time
entry into the central chamber without conceding a forced hit.

Keep deterministic simultaneous decisions, one hidden bend, range 8, speed 2,
and cooldown 2 as the reference hypothesis for that test. If qualified
policies still rationally camp:

1. change approach geometry first so a scoring entry has multiple viable
   timings or lanes;
2. if open geometry still has a non-engagement equilibrium, test one explicit
   stalemate-pressure mechanism, such as a slowly expanding active objective;
3. reject random accuracy, invisible catch-up modifiers, and more curve
   parameters as substitutes for positional incentives.

The expanding-objective idea is only a fallback experiment, not a settled
mechanic. It would need its own ruleset identity, contract data, reachability
analysis, viewer treatment, and same-artifact A/B.

## Evidence order

For each material rules or map candidate:

1. enumerate paths, local matrices, dominant choices, and symmetry;
2. test short-horizon reachability into and out of the useful states;
3. qualify artifacts against the relevant T/C probes;
4. freeze artifacts, arms, maps, assignments, seeds, and holdouts;
5. run the same qualified artifacts under every A/B arm;
6. inspect dynamics and outcome-blind replays;
7. use a native authored cohort for product quality only after the mechanic
   passes the controlled screen.

One-pass authored bots remain valuable T1/T2 and developer-experience
evidence. Their win table cannot select a numeric rule.
