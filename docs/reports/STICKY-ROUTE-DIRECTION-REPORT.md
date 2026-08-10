# Sticky route direction and progress hints

Date: 2026-08-10  
Branch: `codex/sticky-route-direction`  
Implementation checkpoint: `35d26685`  
Base: `main@c34fe104`

## Result

Authored routes now behave as high-level travel hints rather than compulsory
micro-paths. The direct-ladder executor:

1. deterministically commits to one terrain course and keeps it through
   equal-distance alternatives;
2. advances a waypoint only after measured approach, with an arrival radius
   smaller than the next route leg;
3. treats `corridorWidth` only as a local reflow allowance;
4. bypasses a statically unreachable or persistently unproductive waypoint to
   the next reachable authored point without changing rung or route; and
5. applies per-rung posture only on the final approach of an open route, not to
   every intermediate point.

No random choice, sheet/role/map special case, side rule, or coordinate appears
in the executor fix. Exact life, rung, route, lap and waypoint identity scope
the retained course. Every action still has a non-empty reason; new causal
terms are `route-held`, `route-replan-impossible`,
`route-bypass-unreachable-*`, and `route-bypass-stuck-*`.

## The three failure faces

| Failure | Cause | Resolution |
| --- | --- | --- |
| Direction ties | A terrain-shortest alternative was re-derived from adjacent positions on every tick. The oscillation governor correctly rejected the resulting A-B circuit, but nothing retained the choice beneath it. | `TacticalRouteGovernor` owns a deterministic course until a real invalidation. |
| Arrival-ring laps | `corridorWidth` enlarged arrival. Overlapping rings could consume one waypoint per tick without the body approaching either. | Arrival is independent of corridor width, bounded below leg length, and requires new approach (or the exact point). |
| Unproductive point | A route point could remain the target even when the body could not produce progress toward it. | Static no-path and the existing bounded stuck witness transactionally try later authored points and issue the replacement step in the same tick. |

The reported Tapline point `(8,6)` is not itself a wall tile. It is legal ground
flush against the wall column; the approach could still fail to make useful
progress under traffic. On the unchanged pre-workaround fixture the new
`route-bypass-stuck` path fired, while `route-bypass-unreachable` did not. That
distinction is retained rather than rewriting the diagnosis to fit the first
hypothesis.

An adjacent bug was necessary to fix for the stricter progress rule to work:
the executor shifted every route waypoint by the rung's posture but measured
progress against the unshifted route. A body could correctly reach its shifted
tile and wait forever two tiles from the cursor. Intermediate route travel now
uses the authored spine; posture remains a final-destination cue exactly as the
sheet contract describes.

## Decisive Drawbench replay

The original `shadow-hunt-loop`—including the leg back through the owned
Well—is restored. Same side, seed and unit:

| `drawbench-1`, seed 9001, team 1 unit 0 | Before | After |
| --- | ---: | ---: |
| Distinct tiles | 3 | 98 |
| Movement commands | 144 | 228 |
| Shots | 0 | 9 |
| `oscillation-yield` | 59.4% | 1.7% |

The final in-process and sandboxed-WASM gameplay-state digest is byte-identical:

`40ba90209d1e10b549ea192b3b7002cb50de75c1ea09e8c24e3910b2b418ef2d`

The runtime parity suite also asserts broadly that this exact Hunter uses at
least 20 distinct tiles and yields on less than 20% of commands, so the
stationary move-order regression cannot silently return while still allowing
later tactical evolution.

## Tapline route productivity

`scripts/arc-relay-route-read.py` is now the tracked diagnostic for this class
of failure. It separates selected body-ticks, movement-channel admission,
submitted/successful moves, nearest-own-Well approach and explicit route
bypass/no-path reasons. The sheet-tuning skill now routes this diagnosis to the
script.

On `main@c34fe104`'s corrected Tapline geometry, the sticky-course-only executor
already reached both starting Wells but still spent most `centre-push` ticks
outside the movement channel:

| Current Tapline, seed 9001 | Before progress hints | After |
| --- | ---: | ---: |
| Team 0 movement-channel share | 53.1% | 91.6% |
| Team 1 movement-channel share | 33.2% | 57.2% |
| Closest starting-Well distance, both teams | 0 | 0 |

The pre-workaround layout and sheets from `3a046bcc` were then held unchanged
as the adversarial geography fixture:

| Original Tapline fixture, seed 9001 | Original executor | Final executor |
| --- | ---: | ---: |
| Team 0 movement-channel share | 39.7% | 93.0% |
| Team 1 movement-channel share | 27.9% | 60.4% |
| Team 0 closest starting-Well distance | 3 | 0 |
| Team 1 closest starting-Well distance | 3 | 0 |
| Team 0 traffic flaps | 6.73% | 2.16% |
| Team 1 traffic flaps | 7.98% | 2.75% |

That unchanged-fixture in-process/WASM gameplay-state digest is also identical:

`40b9b4f2c73e59d2e209b6bcf8edcf048e419cf4b4017b523bc50feedbce66fb`

One body per Tapline side remains physically caged at its initial tile by other
bodies' permanent return reservations plus walls. Those reservations are real
engine legality, not transient route traffic. Relaxing them would hide a
map/spawn defect by changing the rules, so this branch leaves it disclosed and
unchanged.

## Six-cell exhibit

Public phone gallery:

<https://supervisors-charter-mail-english.trycloudflare.com/>

The gallery uses the fixed review cells in the required order and shows the
actual outcomes. They are records of six exhibits, not a balance sample or
verdict.

| Cell | Hunter side | Recorded result | End | Hunter u0 tiles | Hunter u0 yield |
| --- | ---: | --- | ---: | ---: | ---: |
| `w-9001` | 0 | loss | 552 | 105 | 1.2% |
| `w-9002` | 0 | loss | 523 | 108 | 0.0% |
| `e-9005` | 1 | loss | 365 | 88 | 0.0% |
| `e-9009` | 1 | loss | 432 | 100 | 0.0% |
| `w-9012` | 0 | loss | 357 | 95 | 0.0% |
| `e-9004` | 1 | loss | 452 | 95 | 2.6% |

Across all twelve side-reads:

| Cell | Team 0 least / worst yield | Team 1 least / worst yield |
| --- | --- | --- |
| `w-9001` | 45 tiles / 6.3% | 64 tiles / 2.8% |
| `w-9002` | 67 tiles / 4.9% | 65 tiles / 3.7% |
| `e-9005` | 56 tiles / 3.8% | 33 tiles / 7.1% |
| `e-9009` | 59 tiles / 4.6% | 50 tiles / 6.7% |
| `w-9012` | 31 tiles / 9.5% | 59 tiles / 3.4% |
| `e-9004` | 61 tiles / 4.5% | 27 tiles / 7.9% |

Overall, the least-travelled body covered 27 distinct tiles and the worst
side-level oscillation-yield rate was 9.5%. A real Chrome 151 smoke against the
public URL at `390x844` loaded the replay payload and visible replay canvas with
no console, page or request failures; document width stayed 390 pixels.

## Diagnostic wording and formation reachability

The ordinary direct-sheet move reason is now `march`, not the false
`formation-move`; `escort-follow` remains unchanged.

`TacticalFormationPrimitives` is still reachable for a current direct sheet.
The direct path reuses its generic terrain entry, local reflow, completion,
facing and lifecycle helpers despite shipping `Formations = []`. Only its
slot/shape-specific branches are legacy-only. It is therefore not merely a
frozen `home-siege-v2` dependency and was not deleted.

## Controlled artifact and hosted posture

The final controlled macOS platform-matched Docker build produced:

- CLI version: `0.9.42`
- build-input SHA-256:
  `f9a96665e9592b865f915c40115619c9f3068df848c0a8f5404c4034a642b129`
- WASM SHA-256:
  `0db80102a2dbc902d2bc91dae434c729a39aa0788f8b691bc0ef3d11f5507e85`
- WASM bytes: `8,603,628`

Main's playlist v12 and frozen stock v6 remain byte-immutable. Playlist v13
and stock v7 existed only as unregistered candidates on this worktree while the
owner's addendum arrived; the final candidate was refreshed before landing and
is now frozen to the hashes above. The registered Wellwright-vs-Wellwright
seed-9001 state digest changes from v12's
`c763b012c0b94077d661723a5036b361e7c18b1ea386c76c70c86ad89bc526a6`
to
`3cd9cfdb58e758eb5aa2437e64c7f718e8ddb9a84502cbb5a73852cdcfeb0496`.
V13 therefore starts a fresh rating book; it does not copy v12 ratings.

The hosted steady-worker integration measured `1122.391 ms`, below its hard
two-second ceiling. No latency threshold was relaxed.

## Validation

- `dotnet build BotArena.sln -c Release`: passed, 0 warnings / 0 errors.
- CLI tests: 275 passed.
- Engine tests: 1,454 passed.
- Runtime.Wasm tests: 70 passed, including Warren/Drawbench/Tapline full-match
  in-process/WASM equality.
- Determinism tests: 17 passed.
- App tests with disposable PostgreSQL databases: 284 passed; three unrelated
  S3/container integration fixtures skipped. All PostgreSQL-gated tests ran.
- Hosted execution integration: passed; steady worker `1122.391 ms`.
- API clients regenerated with zero diff.
- Web tests: 438 passed.
- Production web build: passed.
- `git diff --check`: passed.

No battery or balance sweep was run: the goal explicitly permits exhibits only.
No result above is interpreted as a balance or fun claim.
