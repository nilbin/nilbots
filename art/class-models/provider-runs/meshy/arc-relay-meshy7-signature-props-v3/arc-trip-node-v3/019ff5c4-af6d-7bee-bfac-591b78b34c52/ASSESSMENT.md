# Trip Node Meshy 7 gameplay-first assessment

Task `019ff5c4-af6d-7bee-bfac-591b78b34c52` succeeded on 2026-08-12. It
consumed 30 credits, taking the account from 1370 to 1340. This is a generated
base mesh and is not approved for runtime promotion.

## Preserved request

- Meshy 7 Multi-Image to 3D
- four separate 1040×1040 PNGs; strict front first
- 2K PBR textures
- remesh and image enhancement disabled
- lighting removal requested
- no pose mode, rigging, or extra output formats

`request-record.json` pins the input order and hashes. `task.sanitized.json`,
`artifacts.json`, and `timing.json` record the result, artifact hashes, timing,
and credit ledger without credentials or input data URIs.

## Raw result

- GLB: 65,112,900 bytes; SHA-256
  `3c086868cdc4025f5db44d2622b986eba6b7c4897a184395223f2e5b79f6921a`
- one node, monolithic mesh, primitive, and material
- 1,033,479 uploaded vertices; 1,979,928 triangles
- four embedded 2048² JPEG maps: base color, metallic/roughness, normal, and
  emission
- source bounds: `[-0.951333, -0.231129, -0.839679]` to
  `[0.950302, 0.229951, 0.839846]`
- validator: zero errors, one generated-tangent-space portability warning, and
  the default node matrix as informational data
- `structure-audit.json`: 656 indexed components; three components after a
  deterministic `1e-5` positional grouping, with no collapsed or zero-area
  triangles

The normalized review derivative is a reversible 1K-WebP inspection tier,
not a shipping tier: 12,921,328 bytes, 542,978 triangles, and a recorded
`-90°` yaw so the generated `+Z` front becomes canonical `+X`/East. The
maximum planform span is 0.99 and its normalized height is about 0.240. At the
intended 0.44-tile footprint, the proportional height is about 0.107 tile.

## Visual gate

Geometry pass:

- the three broad jaws, broken-triangle planform, large open centre, central
  plate, and low planted profile survived;
- no wheels, tracks, legs, turret, barrel, eye, or hover affordance appeared;
- the canonical/provider top-silhouette IoU is `0.87784` after rigid facing
  registration and equal maximum-span normalization.

Material hold:

- the requested dusty brick-rose drifted toward fixed orange;
- orange is one of Arc Relay's team colors, so this cannot become neutral
  world-prop armor;
- the surface reads worn industrial rather than glossy plastic, but the hue
  conflict alone blocks promotion.

## Next gate

Retain this geometry as the base candidate. Use Meshy's no-charge gallery retry
only after the owner approves a revised material-only request, or correct the
base color locally with the raw UVs pinned. Then create an actual mobile review
tier and run the real gameplay/replay gate. No retry or runtime change is part
of this assessment.
