# Loomshears Counter — a replay-backed answer to Loomsplit

`loomshears-v1` is a deliberately narrow tactical counter to
`loomsplit-v1`. It attacks all three costs Loomsplit accepts while raising its
veteran Rail ace; it is not another eight-body centre march.

## Tactical idea

- **Workshop shears:** Kestrel and Relay run a closed aggressive patrol around
  the seat-relative enemy Workshop. They reach the channel room at ticks
  25–26 and attack the exposed training Longshot before its first 15-tick
  session completes. The loop revisits the room after public respawn waves.
- **Centre wedge:** Longshot, Mortar, Sunder and Patchbay take four close
  aggressive posts. Their group hunts Cores, so winning centre becomes custody
  and a bank instead of passive nearby control.
- **Well ward:** Mason and Palisade hold the own-Well approach. Loomsplit's two
  outer raiders therefore arrive into a prepared two-body defence rather than
  an undefended four-value route.

The allocation is intentionally legible: two bodies answer the veteran
investment, four convert centre, and two protect the predictable outer raid.
Loomsplit can answer by escorting or abandoning its trainer, collapsing more
bodies onto a scoring lane, or clearing the Workshop patrol. Against a sheet
that never trains, Loomshears pays the very real cost of leaving two bodies on
a scoreless patrol.

## Motivating trace and change

The existing Broadloom disruption proof established the physical timing but
was not a finished counter sheet. In the west diagnostic, its Kestrel and
Relay killed Loomsplit's trainer at tick 29 and suppressed every completion,
yet its four middle bodies held distant posts and produced only one late centre
bank at tick 511. It won that cell at tick 531 but lost the mirrored cell at
tick 435 despite repeatedly killing the trainer.

Loomshears keeps the honest Workshop interception and Well guard, then changes
the middle assignment to close aggressive posts with `coreRule: hunt`. In the
two focused cells it won from west at tick 265 and east at tick 271. In both,
the middle group banked each successive centre Core rather than parking near
it. No executor or combat number was changed.

## Six-cell exhibit

Opponent: `loomsplit-v1`. Profile: `broadloom-3`. Standard review cells, with
Loomshears on west for `w-*` and east for `e-*`.

| Cell | Result | End | Loomshears pulses | Candidate banks | Loomsplit training completions |
|---|---:|---:|---:|---|---:|
| `w-9001` | win | 265 | 3 | own ×3, centre ×3 | 0 |
| `w-9002` | win | 268 | 3 | own ×3, centre ×3 | 0 |
| `e-9005` | win | 271 | 3 | own ×3, centre ×3 | 0 |
| `e-9009` | win | 268 | 3 | own ×3, centre ×3 | 0 |
| `w-9012` | win | 278 | 3 | own ×3, centre ×3 | 0 |
| `e-9004` | win | 271 | 3 | own ×3, centre ×3 | 0 |

Verdict: **6–0 in this named counter exhibit**, with reactor destruction from
both seats and all six raw replays passing the movement and felt-eligibility
gates. This proves the authored counter interaction is deterministic enough to
review; six cells do not establish population balance or general strength.

The first trainer life died at tick 29 in both west cells and tick 31 in all
east cells. Loomsplit never reached its veteran-gated second phase. Its Kestrel
and Relay also died at or near Loomshears' own Well in every cell. Loomshears
then converted the first centre Core at ticks 90–93 and continued the same
own/centre cadence through reactor destruction.

## Authoring friction found

No missing Tactics primitive blocked the counter: one persistent phase using
Patrol, Posts, Hold/Aggressive posture, `ignore`/`hunt`/`grab` Core intent, and
ordinary signature policy expressed the whole play.

One useful refinement remains. `coreRule` belongs to a Group for the entire
Tactic, not to a phase job. That means the Workshop pair cannot naturally say
"ignore Cores while severing the first training attempt, then hunt the nearby
enemy Core while continuing the patrol." A future phase-local override that
inherits the Group default could express that without a second scheduler or
runtime priority system. It was not added merely to improve this hard counter;
the permanent scoreless pair is both a real cost and clear counterplay.

The conformance reader also reports a deviation when a Hold defender begins a
fight one tile outside its post, even though Hold's authored short leash and
the post's Chebyshev-one readiness tolerance permit that response. Four cells
showed only this finding, with maximum excursion one; two cells had no
deviations. This is checker severity to refine, not replay evidence that the
Well ward abandoned its assignment.

## Reproduction and artifacts

- Source: `arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/tactics/loomshears-v1.json`
- Tactics SHA-256: `cb76f3e54b45e959795a041d1ae8c06e40261fb7b20ed081bafc8a3f5a933265`
- Materialized Orders SHA-256: `d054adea0ec90103ea3e55609eac9fd77dfe4be0359f4b72a5401a2bc9478669`
- Map-plan SHA-256: `a810cc287d90372a2350ee4019b70aba251cc5e5f44ace213df1307f924137af`
- Linked package/content hash: `9de2702a3fdf36ec0d7832e9ff5350386362dd669dcc6de39ceab3e524c6cf07`
- Gallery: <https://carb-created-literary-playback.trycloudflare.com>
- Local account row: `Loomshears Counter`, id
  `a88bc8c5-59a8-4a65-ac2c-4f36630f2b87`, revision 1, ladder disabled.

Validation: deterministic Tactics materialization, two focused diagnostic
cells, the standard six-cell mirrored exhibit, six clean raw-replay movement
audits, six felt-eligibility passes, and six Tactics conformance reports.
The public gallery index and one replay were also exercised at a 390×844
viewport; playback advanced with no browser warnings or errors.
