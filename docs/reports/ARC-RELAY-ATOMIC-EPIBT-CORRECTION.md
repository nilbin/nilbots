# Arc Relay atomic EPIBT correction

Status: in-process design-review correction, 2026-08-13. This is exhibit
evidence, not a battery or balance verdict. No WASM artifact, frozen stock,
playlist, rating book, CLI version, or release surface changed.

## Why the first report is rejected

The owner found four horizon games and three immediately visible defects in
the first six-cell gallery. G3 (`e-9005`) contained:

- two west carriers moving among equal-distance tiles near their own reactor;
- Hunter, its follower and a third ally submitting a closed three-body move
  cycle that the authoritative engine correctly blocked; and
- several one-health bodies orbiting a healing tile without entering it.

The existing Core-level home-progress detector already measured 206 west and
106 east uncontested ticks without new reactor progress in G3. The exhibit
script nevertheless passed `--skip-arc-relay-eligibility` unconditionally, so
that evidence never gated the gallery. Aggregate traffic reads also reported
no long flap or squad loop because those definitions did not cover either a
closed simultaneous movement cycle or a moving orbit around an objective.

## Root causes and corrections

1. The planner offered movement as the first action even when the body's
   authoritative movement action was unavailable that tick (notably the Core
   relocation interval). It now emits a first-tick wait while retaining future
   movement in the length-three operation.
2. Pairwise vertex/swap collision checks cannot see a closed rotation of three
   or more bodies. Candidate joint operations now apply the same acyclic-follow
   rule as authoritative atomic movement at every operation timestep.
3. Operation scoring considered only end-of-horizon distance, then preferred
   more motion. `FFF` could beat `FWW` even when both ended at the healer. The
   score now ranks final distance, earliest arrival, then intermediate path
   distance before action tie-breaks.
4. Wells, healers and reactors are single-tile services. One selected body now
   owns that exact contribution tile while same-team peers receive stable,
   distinct staging tiles; they no longer evict one another before service.
5. Active hardlight blocks stop bodies in the engine but were absent from the
   mind's durable-obstacle set. The planner now treats active, unsuppressed
   hardlight as solid to both teams, matching the engine; hostile trip-node
   handling is unchanged.

## Evaluation correction

`scripts/arc-relay-motion-sanity.py` now rejects raw replays containing closed
allied movement cycles or uncontested recovery episodes with no health or
geographic progress. `scripts/arc-relay-exhibit.sh` runs it before broadcast
generation, no longer skips frozen felt eligibility, and refuses a review
gallery with more than one horizon result unless the run is explicitly marked
as a horizon-focused diagnostic. The sheet-tuning skill records all three
rules.

The old G3 deterministically fails the new gate: seven closed-cycle ticks and
three recovery no-progress episodes (42, 63 and 64 ticks). The corrected G3
passes the raw gate and frozen eligibility.

## Corrected six-cell exhibit

| Cell | Result | End tick | Motion sanity | Felt eligibility |
| --- | --- | ---: | --- | --- |
| `w-9001` | Wellwright win | 374 | clean | pass |
| `w-9002` | Wellwright win | 441 | clean | pass |
| `e-9005` | Wellwright win | 455 | clean | pass |
| `e-9009` | Hunter win | 379 | clean | pass |
| `w-9012` | Wellwright win | 430 | clean | pass |
| `e-9004` | Wellwright win | 451 | clean | pass |

There are no horizon games, closed allied cycles, or recovery no-progress
trips in the corrected set. Every broadcast passes the frozen eligibility
bars. Corrected G3's maximum Core home non-progress is five ticks on either
team, down from 206/106. Its Hunter records 244 moves across 88 tiles and its
Medic 192 moves across 110 tiles; the old follower cycle is absent.

These figures make the gallery reviewable. They do not establish that the
movement looks good or that the game is balanced; the owner watch remains the
acceptance layer.

## Focused verification

- `dotnet test tests/ArcRelayTacticalPlaybookMind.Tests --no-restore`: 7 pass
- `dotnet test tests/BotArena.Cli.Tests --no-restore --filter
  FullyQualifiedName~OrdersMovementSceneTests`: 5 pass
- `git diff --check`: pass

Public review gallery (phone-smoked at 844x390, six cards, G3 playback advanced
with no page/console/request errors):
`https://particularly-medicare-solutions-benjamin.trycloudflare.com/`

Final correction commit: `b5eba370`.
