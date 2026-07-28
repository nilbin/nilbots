# ArcApprentice author and qualification DX

## Scope and budget

- Role: `boundary-instrument`
- Target: cumulative T3
- Doctrine: exact local projectile geometry before ordinary tempo
- Derived from: `house-apprentice-t2-v1`
- Strategy changes: one curve-preview helper and one priority-order change
- Balance improvement passes: zero

## What worked

- The public SDK `ShotPaths.Preview` exactly matched the engine's strict
  corner behavior; the bot did not need engine source or hidden paths.
- The cumulative command automatically reran and hash-linked the T2
  prerequisite, preventing a tactical specialist from skipping fundamentals.
- The paired odd/even cadence cases were readable directly from
  `remainingTiles`, `tilesPerAdvance`, and the resolved projectile range.
- One controlled WASM build supported every T2 and T3 run.

## Friction and findings

- The first positive bend geometry was invalid for the same strict-corner
  reason the probe intended to teach: wall `(9,6)` blocked the diagonal from
  `(9,7)` to `(10,6)`. A static path/visibility audit found the corrected
  visible intercept `(8,7) -> (12,6)` with a bend after three tiles.
- An attempted defensive strict-corner scenario was impossible under the
  shared corner-strict vision model: the blocking corner also blocked direct
  sight of the allegedly threatening projectile. The final probe instead
  presents a visible target whose tempting lax curved intercept is invalid,
  paired with a separate positive curve case.
- The ordinary starter wasted the cooldown probe by firing first and then
  treating its own projectile tile as occupied. ArcApprentice's deliberate
  tempo ordering crosses the objective approach before taking the routine
  exchange.
- The controlled publish again completed with
  `--disable-build-servers -m:1 -p:UseSharedCompilation=false` in about
  34 seconds. Toolchain source remains outside this slice.
- Full cumulative evidence is 141 MB for 26 replay files. The local bytes and
  tracked content manifest exist, but durable artifact-store upload is still
  required before release use.

## Measured upper boundary

The cumulative T4 profile retains T3 and fails only current-map
`entry-initiative`: one assignment enters and establishes control, while the
mirror repeatedly takes the generic two-advance dodge and retreats from the
choke. It passes suppression, objective-preserving response, front rotation,
and thin-front holdout. This is now a useful exact T3 calibration boundary,
but it remains one lineage revision and cannot vote on numeric balance.
