# Arc Relay squad movement planner

Status: implementation contract, revised 2026-08-13. This replaces the
five-tick prioritized WHCA* experiment as the product movement architecture.

## Product boundary

The sheet chooses each body's order, objective, target, urgency, posture, and
follow relationship. The planner owns only the physical realization of all
eight already-selected intents. It may choose routes, distinct contribution
tiles, passing lanes, waits, and deterministic shuffles. It may not change an
order, invent an objective, recruit a body, or change combat policy.

## Rejected architecture

`CoordinatedArenaStepper` plans one body at a time for five ticks and rebuilds
those plans every tick. It cannot see the team's complete intent set before the
first command, assign distinct useful positions, or move a chain as one plan.
The replacement product path must not instantiate or call that class and must
not fall back to `GreedyArenaStepper`. Those implementations may survive only
as explicitly selected historical controls while review evidence is gathered.

## Planning model

One planning pass runs after Orders has selected all body actions and before
any body command is emitted.

1. Project every selected action into a movement intent: fixed route course,
   objective goal set, follow ring, fight contribution set, evade set, or hold.
2. Group compatible intents by semantic objective. Bodies do not need a shared
   authored object: equal destination identity and action episode are enough.
3. Generate legal contribution candidates. A candidate must preserve the
   authored objective and, for combat, let the body attack/support or make
   measurable progress toward doing so. A choke, occupied exact target, or
   leader course is not automatically a contribution tile.
4. Solve a deterministic minimum-cost assignment from bodies to distinct
   candidates. Authored posture and action semantics shape the candidate set;
   the cost itself is terrain travel plus exact-target staging and continuity
   with the previous assignment. Authored urgency orders otherwise-equal bodies.
   Route continuity is a separate operation preference in the temporal solve.
   No class, role, side, sheet, coordinate, or random special case is permitted.
5. Solve collision-free first actions with Enhanced Priority Inheritance with
   Backtracking (EPIBT): three-action operations, bounded revisits and shifted
   operation inheritance. Three actions are the minimum that lets a
   facing-locked blocker turn and then vacate without returning to the rejected
   five-tick WHCA* window. The solver may move an acyclic dependency chain into
   a pre-tick vacancy. It must never submit two bodies to one tile or rely on a
   swap/closed cycle; the engine resolves the same dependency graph atomically.
6. Carry contribution assignments and shifted operations across ticks. The
   online solver observes the field again every tick, but stable inputs favour
   the previous assignment and inherited operation. A changed order, target,
   life, invalid tile or blocked operation invalidates that continuity. A
   follower never reserves its leader's current or planned next tile.

The contribution assignment is a deterministic minimum-cost Hungarian match.
It chooses distinct useful ground for bodies sharing one objective; EPIBT then
realizes those authored intents in time and space. Reactive projectile evasion
may pre-empt the planned first action for one tick;
the persistent assignment remains and is resumed afterward. A committed strike
remains rooted. A body merely resting at its objective prefers to hold, but can
be pushed to an adjacent legal tile by priority inheritance; that is physical
yielding, not a change of order. These are physical/safety constraints, not
tactical priority.

## Why EPIBT

This is online lifelong multi-agent pathfinding rather than a one-shot puzzle:
bodies die, see threats, change order rung, begin a strike, acquire a Core, or
receive a new contribution tile while the match is running. EPIBT was selected
because its published model natively combines priority inheritance,
backtracking, multi-action operations and rotation in a rolling online solver.
The three-action operation is long enough for a deliberate chassis to turn,
vacate, and preserve continuity without reviving the rejected five-tick
reservation window.

Conflict-Based Search and rolling-horizon CBS are strong choices for fixed
start/goal plans and bounded-horizon global optimization, but repeatedly
solving their constraint trees for goals that can change every tick adds
complexity and latency without giving this eight-body executor better intent
ownership. Plain PIBT is lighter, but its one-action inheritance lacks the
rotation-aware operation continuity that motivated EPIBT. This is a fit claim,
not a universal “best MAPF algorithm” claim; the movement-scene lab and watched
replays remain the product evidence.

References: Okumura et al., “Priority Inheritance with Backtracking for
Iterative Multi-agent Path Finding” (IJCAI 2019); Okumura et al., “Rolling
Horizon Collision Resolution” (AAAI 2021); Li et al., “Pairwise Symmetry
Reasoning for Multi-Agent Path Finding Search” (ICAPS 2020); Yamauchi et al.,
“Enhanced Priority Inheritance with Backtracking for Lifelong Multi-Agent
Pathfinding” (AAAI 2026).

## Determinism and evidence

Every assignment, planned move, planned wait, replan, and rejected alternative
has a non-empty stable reason. Canonical order is team-relative geometry, then
urgency/debt/activation, then stable unit id. No randomness is used.

The primary gate is a fast movement-scene lab around the production planner:

- eight-body reactor exit;
- four-body choke where the contact body fights and allies deploy;
- leader and follower reversing through a corridor;
- returning carrier through outbound traffic;
- recovery beside a busy heal tile;
- two-wide passing, one-wide unavoidable waiting, and dependency-chain shuffle;
- sticky patrol detour and forward rejoin;
- mirrored, rotated, and unit-id-permuted variants.

A deliberately slow test-only joint solver determines whether progress is
possible on the small scenes. If it finds a collision-free schedule, the
production planner may not remain stationary beyond the scene's bound. Tests
also assert intent preservation, no collisions/swaps/corner squeezing,
bounded path overhead, eventual progress, stable assignments, and reasons.

Real matches are integration evidence only. The first gallery gate is the
reported Tapline G3: the Hunter/follower pair may never evict one another, the
centre-bound Mason must clear deployment, and ordinary congestion must not
produce sustained stationary runs when an alternate route or contribution
tile exists.
