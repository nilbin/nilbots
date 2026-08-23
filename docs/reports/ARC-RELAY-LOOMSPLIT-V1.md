# Loomsplit Vanguard — Broadloom-3 tactical sheet

`loomsplit-v1` is a deliberately split-map answer to Demolition March. It is
not another variation on putting eight bodies at mid.

## Tactical idea

- **Rail ace:** one Longshot trains at the seat-relative own Workshop until it
  reaches veteran level 3, then rotates to a protected mid firing post.
- **Outer raiders:** Kestrel and Relay continuously pressure the long outer
  route and the enemy Well. This makes the four-value Core a live threat while
  the opponent is concentrating at mid.
- **Map screen:** Mortar, Sunder and Patchbay cover mid while Mason and Palisade
  shape the own-side Workshop approach. Their signatures remain contextual;
  an unavailable skill never replaces ordinary movement or combat.
- **Break contact:** four losses in 20 ticks pre-empt the main tactic. Survivors
  regroup in a forgiving home area for 30 ticks, then restart. This is an
  escape hatch, not an exact eight-post reassembly puzzle.

The point is a three-way strategic tension: contest short-value scoring at
mid, threaten the highest-value enemy Core, and invest one temporarily absent
body in a persistent veteran advantage. Demolition can still punish the split
by converting its initial concentration quickly.

## Authoring friction found and fixed

1. Workshops previously accepted only global `north`, `south`, or `either`.
   One authored "train behind our Well" tactic therefore changed meaning when
   seated east. Tactics and Orders now accept `own` and `enemy`, resolved by
   map walking distance from that team's own Well. Editor preview, runtime,
   conditions, docs, and replay conformance use the same relation.
2. Tactics Posts previously invented a traversable adjacent anchor solely to
   encode facing. A valid post looking toward a wall could therefore fault at
   tick zero. Orders `move.face` now accepts cardinal `n/e/s/w`; the direction
   mirrors with the seat without creating fake map geometry.
3. The tactic replay checker now resolves relative Workshop selectors and
   labels a training interruption caused by a phase exit as expected rather
   than as a failed channel attempt.

## Six-cell exhibit

Opponent: `demolition-march-v2`. Profile: `broadloom-3`. Standard review cells,
with Loomsplit on west for `w-*` and east for `e-*`.

| Cell | Result | End | Loomsplit pulses | Training | Candidate banks |
|---|---:|---:|---:|---|---|
| `w-9001` | win | 394 | 4 | south ×4 | own ×4, enemy ×2 |
| `w-9002` | win | 264 | 3 | south ×4 | own ×3, centre ×1, enemy ×1 |
| `e-9005` | loss | 385 | 2 | north ×4 | own ×4, enemy ×1 |
| `e-9009` | win | 275 | 3 | north ×4 | own ×3, centre ×1, enemy ×1 |
| `w-9012` | win | 286 | 3 | south ×4 | own ×3, centre ×1, enemy ×1 |
| `e-9004` | loss | 385 | 2 | north ×4 | own ×4, enemy ×1 |

Verdict: **4–2**, with a win from both seats. Every cell completed by reactor
destruction before tick 400. All six raw replays passed the movement sanity
gate with zero closed cycles, recovery stalls, false move traces,
contradictory traffic traces, local orbits, held yields, or carrier-route
traps.

Every cell completed exactly four Workshop sessions on the correct own-side
room and advanced from observed veterancy rather than a fixed timer. Every
cell banked at least one enemy-Well Core. Candidate signature facts include
Rail Line, Hardlight Block, Prism Wall, Arc Toss, Falling Star, Target Paint,
and Repair Beam; Rail Line and both defensive screens activated from both
seats. In `e-9005`, the early four-loss window visibly aborts at tick 64,
finishes its reset at tick 94, resumes training, and advances at tick 143.

This is replay-backed evidence of a working, distinct tactic, not a population
balance claim. The two losses are useful: concentrated mid pressure remains
real counterplay, and this has only been tested against one named opponent.

## Reproduction and artifacts

- Source: `arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/tactics/loomsplit-v1.json`
- Tactics SHA-256: `43e6ff4b3da26bff423340cc57f0fb9c41bf44b789c56a80c988b022e7f071fd`
- Materialized Orders SHA-256: `ae87b067e481bf8fee23c66026202e94a2f815fceb7074bc61f25e3ae20fc596`
- Map-plan SHA-256: `1d887fe49aa02b9fb6523f594dcc4b91db55fa3f16509effee107ac61b181fa2`
- Linked package/content hash: `b94736e4644e522f8820849df105754be76be5a7801a5a61697f7335678d51f1`
- Gallery: <https://larger-assuming-mats-returned.trycloudflare.com>
- Local account row: `Loomsplit Vanguard`, id
  `0357f0a2-3a6a-48e3-980e-df3345a17b28`, revision 1, ladder disabled.

Validation passed: 381 CLI/compiler tests, 106 tactical-mind tests, 219
application tests (101 environment-dependent integration tests skipped), 649
web tests, the production web build, deterministic tactic-read output, six
replay conformance reports, and the exhibit's six raw-replay sanity gates.
