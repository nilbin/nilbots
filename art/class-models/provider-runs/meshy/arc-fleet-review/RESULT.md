# Arc Relay Meshy T2 fleet review

## Outcome

The Meshy Smart Topology T2 route completed for all sixteen Arc Relay classes.
Mason was the timed pilot; the remaining fifteen were then run sequentially under the
owner-approved 225-credit cap. These files are review candidates only. No runtime GLB or
manifest has been promoted.

- Pilot Mason: 234.30 seconds, 15 credits, 678,556-byte review GLB.
- Remaining fifteen: 3,196.38 seconds wall-clock (53m 16.38s), 225 credits.
- Fifteen-call provider stages summed to 3,173.52 seconds; mean 211.57 seconds per class.
- Final balance: 295, from 535 before the Mason pilot and 520 before the fifteen-call batch.
- Batch failures: zero. Rerolls: zero.
- Final batch derivatives: 591,668–814,688 bytes, 664,009-byte mean.
- Validation: zero GLB errors for all sixteen candidates. Every candidate retains the known
  `MESH_PRIMITIVE_GENERATED_TANGENT_SPACE` warning for the fused normal-mapped primitive.

## Review contract

- Fixed 58-degree gameplay camera and the canonical 20-tile Arc Relay replay.
- 0.99-tile normalized source planform, followed by each existing look's renderer scale.
- Monolithic provider mesh retained; no geometry splitting or inferred part separation.
- No semantic model-owned team glow. Team truth remains renderer-owned through floor glow,
  health, effects, and other replay-derived cues.
- Lantern and Mortar use deterministic `identity` normalization because Meshy returned those
  two on its alternate floor axis. The other fourteen use `lay-flat-x`.
- Both replay team assignments were captured. The reviewed models remained inside readable
  tile occupancy at the accepted scale.
- Runtime production models were restored and hash-checked byte-for-byte immediately after
  the review-only build.

## Evidence

- `fleet-candidates-3d.png`: all candidates in the real replay; each named class is centered
  in the amber and cyan halves of its crop.
- `team0-2d-vs-3d.png` and `team1-2d-vs-3d.png`: canonical Canvas2D on the left and the T2
  candidate on the right at the same replay scale.
- `stills/arena-primary-3d.png` and `stills/arena-swapped-3d.png`: full 1440x900 arenas.
- `fleet-audit.json`: task IDs, timings, credits, input hashes, final candidate hashes,
  geometry counts, chosen orientation, validator result, and runtime restoration evidence.
- `candidate-build.json`: exact review-only substitutions and before/after production hashes.

## Accepted limitation

These are fused, monolithic base meshes. The normalized files include stable contract nodes,
but mounted hardware is not separate geometry, so renderer-owned hardware lag and class idle
part motion cannot move a turret, dish, pod, or rail independently. Lean, bob, recoil,
cooldown venting, wakes, and other whole-body or renderer-supplied effects remain possible.
Resolving hardware articulation later requires a model/technical-art pass, not another blind
split of these meshes.

## Replay gallery

The review-only candidate bundle carries three complete, hash-matched Arc Relay matches in
one picker:

- Stock roster A versus Flow roster B: 253 ticks, reactor destroyed.
- Stock roster B versus Flow roster A: 284 ticks, reactor destroyed.
- Stock mirror preflight: 600 ticks, max-tick finish.

The first two swap the class rosters across team colors; the longer mirror exercises repeated
respawns, cooldowns, signatures, cores, and late-match density. The gallery provenance and
compressed transports live under `replay-gallery/`. Serve the already-built candidate with
`cd web && npm run review -- --no-build`; rebuilding without staging the candidates would
replace this review bundle with the production fleet.
