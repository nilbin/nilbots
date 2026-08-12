# Arc Relay squad movement planner

Status: implementation contract, 2026-08-12. This replaces the five-tick
prioritized WHCA* experiment as the product movement architecture.

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
   candidates. Cost includes terrain travel, authored posture, urgency,
   continuity with the previous assignment, firing/support utility, passage
   obstruction, and route departure. No class, role, side, sheet, coordinate,
   or random special case is permitted.
5. Solve collision-free first actions for the whole team. The solver may use
   paths longer than five steps, wait intentionally, and move a dependency
   chain toward a vacancy. It must never submit two bodies to one tile, submit
   a body into an occupied source tile, or rely on simultaneous swaps—the
   engine forbids all three.
6. Persist assignments and routes across ticks. Replan only when the selected
   order/target/life changes, the assigned tile becomes invalid, a material
   battlefield event changes contribution, or measured progress stops. A
   follower never reserves its leader's current or planned next tile.

Reactive projectile evasion may pre-empt the planned first action for one tick;
the persistent assignment remains and is resumed afterward. A committed strike
remains rooted. Both are physical/safety constraints, not tactical priority.

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
