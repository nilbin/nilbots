# Sentinel Seed Meshy 7 gameplay-first assessment

Task `019ff5c8-f17f-7083-a432-71f16c9f1c05` succeeded on 2026-08-12. It
consumed 30 credits, taking the account from 1340 to 1310. This is a generated
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

- GLB: 63,731,588 bytes; SHA-256
  `cc024f1659f2462ee796c09bde8072556a897739459b3f8df9d6f115efe1e1b1`
- one node, monolithic mesh, primitive, and material
- 1,005,784 uploaded vertices; 1,939,406 triangles
- four embedded 2048² JPEG maps: base color, metallic/roughness, normal, and
  emission
- source bounds: `[-0.950235, -0.411599, -0.951183]` to
  `[0.949348, 0.403375, 0.949183]`
- validator: zero errors, one generated-tangent-space portability warning, and
  the default node matrix as informational data
- `structure-audit.json`: 548 indexed components collapsing to one continuous
  spatial component at `1e-5`, with no collapsed or zero-area triangles

The normalized review derivative is a reversible 1K-WebP inspection tier,
not a shipping tier: 13,992,096 bytes and 598,538 triangles. It retains the
generated `+X` front, normalizes the maximum planform span to 0.99, places the
floor at Y=0, and has a normalized height of about 0.425. At the intended
0.62-tile footprint, the proportional height is about 0.266 tile.

## Visual gate

Base-mesh pass:

- four planted lobes, open diagonal notches, compact seed head, flush grille,
  and visible yaw collar survived;
- no gun barrel, wheels, tracks, legs, cockpit, or mobile-body cue appeared;
- the canonical/provider top-silhouette IoU is `0.88783` after equal
  maximum-span normalization;
- pale sage/taupe armor, umber mechanisms, and a restrained dark head inset are
  readable on the dark review background without team-color collision.

Production-contract failure:

- the source is exactly one scene node, mesh, primitive, material, and
  spatially continuous surface;
- therefore the head cannot yaw independently while the four planted lobes
  remain stationary;
- the clear visual collar makes a deliberate DCC or deterministic geometry
  split plausible, but that work must be measured rather than assumed.

## Next gate

Retain the generated base mesh and material. Split it into explicit
`stationary-base` and `yaw-head` parts around the visible bearing, preserve the
UVs/material maps, then create an optimized mobile tier and prove authoritative
head yaw in the real replay viewer. No split or runtime change is part of this
assessment.
