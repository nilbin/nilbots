# Carrier traffic deadlock repair

Date: 2026-08-10<br>
Branch: `codex/no-carrier-deadlock`<br>
Base: `main@12b2a855`<br>
Executor checkpoint: `1b1f85e3`<br>
Hosted-playlist checkpoint: `dd0bae98`

## Result

A loaded carrier no longer gives up because its own squad temporarily occupies
the route home, and a packed allied queue now clears outward to the nearest
legal vacancy. The repair is generic: no sheet, role, class, route, map side,
coordinate, or random choice appears in the policy.

The unchanged `tapline-1`, seed-9001 reproduction moved from a permanent
two-sided jam to bounded traffic:

| Tracked route diagnostic | Team 0 before | Team 0 after | Team 1 before | Team 1 after |
| --- | ---: | ---: | ---: | ---: |
| Longest single-tile stall | 540 ticks | 34 ticks | 530 ticks | 29 ticks |
| Bodies over 100 stationary ticks | 4 | 0 | 4 | 0 |
| `custody:delivery-timeout-*` body-ticks | 421 | 0 | 407 | 0 |

The before count is the exact-life definition now tracked by
`scripts/arc-relay-route-read.py`: a death, respawn, or observation gap ends a
run. It replaces the earlier visual estimate of five or six bodies with a
reproducible count.

The final in-process and sandboxed-WASM replays have the same gameplay-state
digest:

`21b242b16a1d982217cb639835594a180d3bf42d43abf784c4ae85816c96234f`

## What was wrong

Three individually reasonable systems formed a terminal cycle:

1. custody treated prolonged lack of movement as proof that the delivery
   destination was unreachable;
2. the carrier entered `guard`, which stopped requesting movement; and
3. allied right-of-way only helped a mover that was still asking for a tile.

The carrier was therefore blocked by bodies that were themselves blocked by a
carrier that had stopped asking them to move.

A second generic reservation error amplified the Tapline scene: the executor
reserved the reactor tile for a carrier from anywhere on the map. That remote
reservation could cage a newly spawned body even though no carrier was about to
enter the reactor. The reactor is now reserved only when it is the carrier's
actual next step; the routed lane protects the preceding path.

## The repair

### 1. Allies are traffic, not terrain

Custody now uses the map's static terrain graph to decide whether the reactor
is unreachable. Stagnation while terrain still connects the body to home keeps
the delivery objective active. The same distinction now protects an ordinary
route's `onNoPath` seam: bodies, projectiles, lane reservations, and same-tick
claims produce a re-tested `transient-path-retry`, not an authored give-up
fallback.

The reachability audit found:

| Reachability consumer | Posture |
| --- | --- |
| Custody delivery fallback | Fixed: only `StaticDistance` terrain disconnection admits fallback. |
| Authored route `onNoPath` | Fixed: transient occupancy retries; terrain disconnection may use the authored fallback. |
| Focus/target approach | Already uses static distance. |
| Disengage exit test | Occupancy may delay disengagement, but it cannot enter a terminal fallback and is re-evaluated every tick. |
| Route progress/course governor | Observes productivity and transient traffic; it does not declare the final objective unreachable. |

### 2. Giving up is an episode

A genuinely terrain-disconnected custody fallback re-tests the delivery
objective every eight ticks. The retry is named
`custody:delivery-timeout-retest`; the intervening fallback actions keep their
authored, non-empty reasons. A transiently blocked carrier never enters that
fallback in the first place.

### 3. A loaded carrier gets causal right-of-way

The carrier's existing request propagates through lower-priority allies until
the deterministic shortest occupied chain reaches a body with a legal vacant
exit. Unit id breaks equal-length choices. The outer body moves first, so the
vacancy travels back through the queue over legal one-body steps; the executor
does not assume a simultaneous swap.

Reasons expose both the carrier and depth, for example
`make-way:chain-3:u2`. The decisive replay contains chains one through four
bodies deep.

**This is a real behavioral change, not merely a deadlock assertion.** Loaded
carriers now have right-of-way through their own lower-priority squad anywhere
the rule applies. It changes the gameplay-state digest and is the reason hosted
v14 starts a fresh rating book.

### Mutual blocks without a carrier

Existing traffic tickets remain the generic escape for ordinary mutual blocks.
The new cascade is deliberately limited to a loaded carrier: otherwise every
dense formation could be made to shuffle even when its sheet asked it to hold.
A closed occupied component with no legal vacancy is left untouched rather
than displaced speculatively. This is the smallest policy that resolves the
reported causal cycle without inventing a general squad-motion objective.

## Six-cell exhibit

Public, unauthenticated phone gallery:

<https://catherine-truly-situation-amd.trycloudflare.com/index.html>

The fixed review cells are shown in the required order, with outcome-visible
cards explaining trigger, tactic, counterplay, fallback, and what to watch.
They are exhibits, not a battery or a balance sample.

| Cell | Hunter side | Recorded result | End tick | Team 0 / Team 1 longest stall |
| --- | ---: | --- | ---: | ---: |
| `w-9001` | 0 | Hunter loss | 460 | 70 / 34 |
| `w-9002` | 0 | Hunter loss | 525 | 24 / 21 |
| `e-9005` | 1 | Hunter loss | 463 | 17 / 64 |
| `e-9009` | 1 | Hunter win | 482 | 30 / 29 |
| `w-9012` | 0 | Hunter loss, max ticks | 599 | 52 / 31 |
| `e-9004` | 1 | Hunter loss | 525 | 27 / 32 |

Across all twelve side-reads, no body exceeds 100 stationary ticks and every
side records zero `custody:delivery-timeout-*` ticks. The worst remaining
single-tile stall is 70 ticks.

A real Chromium/WebGL smoke loaded the exact public URL at `390x844` with
touch/mobile capabilities, found all six cards, reached the replay-ready UI,
advanced playback, rendered the score and arena, and recorded no console,
page, or request failure. The gallery index is served with content gzip, each
broadcast is a gzip payload, and all six broadcasts remain below the 300 KiB
ceiling: 902.7 KiB total, 164.7 KiB largest.

The detached review processes intentionally remain alive for owner review:

- server: `nilbots-carrier-v14-server-9014`
- tunnel: `nilbots-carrier-v14-tunnel-9014`
- logs: `/tmp/arc-carrier-deadlock-exhibit/server-9014.log` and
  `/tmp/arc-carrier-deadlock-exhibit/tunnel-9014.log`

Tapline still reaches max ticks in one exhibited cell while fighting continues.
That is the separately disclosed map-pacing question; neither map nor sheet was
changed to make a cell finish.

## Controlled artifact and rating era

Playlist v13 and `ambush-warren-11-stock-v7` remain immutable. Their WASM hash
is still:

`0db80102a2dbc902d2bc91dae434c729a39aa0788f8b691bc0ef3d11f5507e85`

The source change was rebuilt through the controlled, platform-matched Docker
path and frozen as `ambush-warren-11-stock-v8` for entrant playlist v14:

- CLI version: `0.9.43`
- build-input SHA-256:
  `9b51321b31b38f3daab2e0f441916b5629635a5725fa31d5ee63660db25bbcb7`
- WASM SHA-256:
  `08205cfdb2da34cbd894a51be89c4b638e6cc40ca16020741602d4739fb2344a`
- WASM bytes: `8,660,235`

The registered Wellwright-vs-Wellwright seed-9001 stock cell changes from the
v13 gameplay-state digest
`3cd9cfdb58e758eb5aa2437e64c7f718e8ddb9a84502cbb5a73852cdcfeb0496`
to v14 digest
`4afb253b831b8d50a2cd04591eedd8b56393961c4d13ba6e72d0656fa5150c5f`.
V14 therefore starts another fresh rating book; it does not carry v13 Elo.
`publish-cli` owes the `cli-v0.9.43` tag before any deploy. No release or deploy
workflow was run.

## Validation

- `dotnet build BotArena.sln --no-restore`: passed, 0 warnings / 0 errors.
- Engine tests: 1,454 passed.
- CLI tests: 284 passed.
- Runtime.Wasm tests: 70 passed, including exact Tapline full-match
  in-process/WASM equality.
- Determinism tests: 17 passed.
- SDK and Guest tests: 122 passed.
- App tests against a disposable PostgreSQL 16 container: 284 passed; three
  unrelated external S3/toolchain-container fixtures skipped. PostgreSQL-gated
  tests, including the v14 fresh-book transition, ran.
- Python script tests: 32 passed.
- API clients regenerated with zero diff.
- Web tests: 438 passed.
- Production web build: passed.
- Public 390x844 browser smoke: passed.
- `git diff --check`: passed.

No battery, sweep, map tuning, sheet tuning, balance verdict, or fun claim was
made.
