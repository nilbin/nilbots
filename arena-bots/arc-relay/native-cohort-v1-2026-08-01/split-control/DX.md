# SplitControlMind authoring DX

This report was completed after the entrant's permitted seed-314159 mechanical
self-play and before any cohort outcome was disclosed. I did not inspect the
Engine, CLI implementation, stock mind, another entrant, cross-entrant replay,
or cohort result.

## What worked well

- The participant-scoped mind is a natural fit for distributed pickup
  assignment. A single dictionary makes Core-to-runner ownership unique, and
  a shared destination claim set prevents same-tick sibling convergence.
- `MindBody.ClassId`, stable unit IDs, sticky role tags, and the full per-body
  legality mask are enough to express mixed composition roles without
  importing numeric action codes or form-name conventions.
- The typed Arc Relay mode state answers the strategy questions that matter:
  public Well cadence/capacity, visible Core disposition and carrier identity,
  Core relocation timing, reactor charge, and visible signature state.
- Participant-relative region bindings and facing make side mirroring direct.
  The source contains no west-team/east-team coordinate branch.
- Movement handling is available through form-to-profile lookup. The router
  can use eight-way headings for standard/swift bodies and cardinal
  move-plus-rotate behavior for deliberate bodies without class-specific
  movement constants.
- The fresh scaffold's command-buffering and role-tag APIs were immediately
  useful, even though its bundled tactical examples target Frontline rather
  than Arc Relay.

## Friction and ambiguity

- The generic-mind scaffold is strongly Frontline-shaped: its roles, objective
  helpers, economy recall, and movement helper assume Frontline structures and
  direction arguments. Arc Relay's movement action uses a projectile-heading
  constraint, so the tactical half of the scaffold could not be adapted
  incrementally and was replaced.
- The provisional sheet's four required fields were clear from the packet,
  but its `composition` value shape is not described in public CLI help. The
  natural array of class IDs was accepted by the runner.
- `experiment arc-relay --help` currently prints broad experiment help rather
  than Arc Relay-specific sheet and runtime help.
- The replay summary reports objective dynamics but not participant fault
  counts or eligibility. The run record reports both eligible team IDs; under
  this contract's zero-fault allowance, that is sufficient to infer zero
  runtime faults, but printing those facts directly would make the mechanical
  admission check much easier.
- A host-side validation abort said only that a signature state had an empty
  position shape. It named no tick, team, unit, operation, or signature. The
  CLI correctly refused to write/score a replay, but locating the mechanical
  trigger required temporary stderr-only action tracing and repeated
  non-measuring reruns.

## Mechanical repair history

The implementation doctrine was written once. Every change below addressed
the same abort before any replay existed; none used a winner, score, or tactical
observation. Each pre-repair source hash and reconstruction note/reverse patch
is archived under `repair-history/`.

1. Suppressed Null Field as the only parameterless submitted signature. The
   abort persisted; this was a conservative first hypothesis.
2. Temporary signature tracing showed the first reproducible abort on a tick
   where a live Trip Node could reach an empty-position projection. Suppressed
   Trip Node; the abort persisted.
3. Suppressed Prism Wall because an individually legal direction can lose all
   placed segments during joint placement. The abort persisted.
4. Suppressed Smoke Canister, the remaining field signature on the diagnosed
   tick. The abort persisted.
5. Suppressed Survey Flare, the remaining travel/field signature on that tick.
   The match advanced substantially farther before the same abort.
6. New temporary tracing isolated Arc Toss immediately before the later abort.
   First excluded the reactor socket as a toss endpoint; the abort persisted.
7. Suppressed Arc Toss entirely. The next seed-314159 run completed and wrote
   the only measured replay.

Temporary trace statements were removed before source freeze. The final bot
still submits Vector Dash selectively on a distant clear aligned pickup route.
The required smoke summary confirms its attempts and completions.

## Frozen mechanical evidence

- Profile: native `IGenericMindBot`, one participant-scoped runtime commanding
  every live body.
- Seed: `314159` only.
- Runtime: in-process diagnostic.
- Completion: 505 ticks, replay written.
- Eligibility: teams 0 and 1 both eligible.
- Runtime faults: zero, inferred from both teams remaining eligible under the
  public contract's zero-fault allowance.
- Replay hash: `7a7e18472476d1141104fc055d0eab95a5d9b0b43ea198f41332541ff099672b`.
- Sheet hash recorded by the run:
  `4f4eb72ff253218cf9e1a5bdf69f6a4002f96bb1b7a19f909d30bd06be7a4374`.

The winner and tactical counts were not used to tune the frozen source.
