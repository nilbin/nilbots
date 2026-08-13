# Arc Relay atomic convoy and EPIBT movement report

Status: in-process design-review candidate, 2026-08-13. This report records an
exhibit, not a battery or a balance verdict. No WASM artifact, frozen stock
artifact, hosted playlist, rating book, or release version was changed.

## Outcome

Tapline-3 now resolves team movement as one atomic dependency graph. A body may
enter a teammate's source tile when that teammate moves onward and the complete
chain ends at a pre-tick vacancy. The engine still rejects a shared destination,
a two-body swap, every longer closed cycle, a stationary head, and a head
blocked by terrain, reservation, signature, or projectile. Authoritative
execution and replay chronology use the same pure resolver. Tapline-1 and
Tapline-2 retain the historical closed occupancy rule.

The former five-tick `CoordinatedArenaStepper` was deleted. The product path is
now a deterministic, three-action Enhanced Priority Inheritance with
Backtracking (EPIBT) planner. This is an adaptation of the published algorithm,
not a renamed home-grown five-step window:

- all eight already-selected Orders intents are available before command one;
- a deterministic Hungarian assignment gives bodies sharing an authored
  objective distinct contribution tiles;
- EPIBT evaluates length-three move/rotate/wait operations with priority
  inheritance, bounded revisits (`L = 10`), backtracking, and shifted-operation
  inheritance;
- equal-distance ties prefer the earliest move, then rotate, then wait;
- authored urgency/debt controls priority, while the planner owns only physical
  realization and never selects an order;
- a satisfied body normally holds, but can yield one legal tile without
  changing its authored rung when a higher-priority operation needs its ground;
- pending strikes, attacks or aim-preparation, skills, handoffs, drops,
  upgrades, and Arc Toss catch windows are pinned physical commitments;
- the historical greedy stepper remains only behind explicit
  `--mind-stepper greedy`.

EPIBT is the best fit found for this particular problem, not a claim that it is
the universally best MAPF solver. Arc Relay is online and rotation-aware, with
goals that can change every tick. Plain PIBT lacks multi-action rotation
continuity; rolling-horizon CBS is attractive for stable global plans but adds
constraint-tree cost and policy complexity for eight rapidly changing intents.
The EPIBT paper's own ablation supports bounded revisits and operation
inheritance, and reports that increasing the operation length beyond the short
window raises runtime without reliably improving throughput.

## Engine contract proof

Focused pure and session-level tests pin the following table:

| joint request | Tapline-3 result |
| --- | --- |
| `A→B source, B→C source, C→empty` | all advance atomically |
| `A→B source, B→A source` | both block |
| `A→B→C→A` | full cycle blocks |
| two actors claim one destination | both and their dependants block |
| the head waits or is statically blocked | every follower blocks |
| same chain under Tapline-2 | historical follower block |

The planner unit tests independently pin follow operations as non-conflicting,
swaps as conflicting, global minimum-cost assignment, move/rotate/wait ordering,
and the fact that the satisfied-body hold preference cannot outweigh one real
step of goal progress. Product scene tests run untagged ATL2—the actual default
form—and assert EPIBT reasons, so they cannot silently bless the greedy control.

## Six-cell exhibit

Public phone gallery:
<https://particularly-medicare-solutions-benjamin.trycloudflare.com>

Inputs were `hunter-tapline-v2` versus `wellwright-tapline-v2` on Tapline-3,
the standard six side/seed cells, in-process. Records are stated only so the
exhibit is reproducible:

| cell | recorded result | end tick | replay hash |
| --- | --- | ---: | --- |
| w-9001 | Hunter, horizon | 599 | `35a9a0c5fc58c9dbbc715dfbe9be0b3134eff48240e4ac3db3dc9d49a15aa73e` |
| w-9002 | Hunter, reactor destroyed | 444 | `a3aecc2b73ff6e65dd264d91f2eb18533753bfb32457a7b033aec2fe901e4a3a` |
| e-9005 | Hunter, reactor destroyed | 546 | `d95ea5e6b9314220d40dbeec5f040e85064525de710651b87be30a8d72723458` |
| e-9009 | Hunter, horizon | 599 | `7638cd1d9c00138006342302488ad259d00c534fa0bba884ba2e1e9404c07591` |
| w-9012 | Hunter, horizon | 599 | `a36c15f70ddf74d42a328c25c79f69f7d8fcdc902f491d971fe7fbf5338838c4` |
| e-9004 | draw, horizon | 599 | `0c8ce3e93ee2612deafff276468cd8e65bfb7c624bd4f38fb86e5fa238ef099c` |

Replay chronology contains 1,895 successful ally follow edges: the following
body entered a teammate's pre-tick source while that teammate also moved
successfully. These include 219 edges in three-body chains, 53 in four-body
chains, 13 in five-body chains, and 6 in six-body chains. This is direct match
evidence for the new atomic rule, not an inference from planner reasons.

The tracked route read reports, across 43,349 comparable body-ticks:

- 19,674 stationary body-ticks (45.39%);
- 1,144 traffic-blocked stationary ticks (5.81% of stationary ticks);
- zero delivery-timeout ticks;
- 20 oscillation-yield ticks;
- one body with a single-tile run over 100 ticks; the worst was 134 ticks at
  its assigned home-Well contribution tile in e-9009, not a traffic stall.

The traffic read reports no long flaps and no squad loops in any cell. The
Arc Toss catch reservation fires in every cell; it was added after a diagnostic
run showed a receiver correctly moving under simultaneous planning while the
already-thrown Core landed on its old tile.

## Validation

The fast-loop validation completed at the reported checkpoint:

- 18 focused engine tests: dependency graph, live-session convoy, and
  Tapline-3 identity;
- 4 EPIBT planner tests;
- 12 CLI product-scene/default-command tests;
- full solution build;
- web production build;
- `git diff --check`;
- phone-sized Chromium smoke against the public gallery and HTTP 200 through
  the standing Cloudflare tunnel.

The gallery uses the in-process design loop intentionally. A hosted promotion
still requires a separately authorized controlled WASM build, in-process/WASM
gameplay-state equality, a new immutable stock artifact and playlist version,
and an explicit rating-book continuity ruling.

## References

- Yamauchi et al., “Enhanced Priority Inheritance with Backtracking for
  Lifelong Multi-Agent Pathfinding,” AAAI 2026.
- Okumura et al., “Priority Inheritance with Backtracking for Iterative
  Multi-agent Path Finding,” IJCAI 2019.
- Okumura et al., “Rolling Horizon Collision Resolution,” AAAI 2021.
