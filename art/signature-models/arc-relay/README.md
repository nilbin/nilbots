# Arc Relay signature-object model sources

These are the sources and promotion records for renderer-owned Arc Relay
signature props. Source art does not change rules or authorize a provider
result by itself; only the pinned promotion scripts install runtime GLBs.

## Shared contract

- One object per provider task.
- `+X` is forward, `+Y` is up, and floor contact is `Y=0` after deterministic
  normalization.
- The object stays inside its authoritative occupied tile.
- No baked team color, range, telegraph, smoke, projectile, damage, reveal, or
  expiry state. Those remain renderer-owned.
- Provider output is a raw candidate until it passes orientation, bounds,
  gameplay-scale, performance, and fallback review.
- The alpha images named below are the exact proposed Meshy T2 inputs. The
  chroma images are retained as image-generation masters.

## Trip Node

- **Proposed T2 input:** `trip-node/concept-source-v1.png`
- **Input SHA-256:**
  `095bacefe58b0b6cc58d7ffae28defab3923c9f592a363ec05f595fff20c5f1d`
- **Gameplay read:** persistent, destructible, hidden-until-near proximity
  mine; one active per Minesmith.
- **Required anchors:** low circular armored puck, three short rim prongs,
  right-facing forward wedge, recessed triangular sensor with a narrow flat
  light slit.
- **Forbidden cues:** central sphere/dome, loose-Core silhouette, turret,
  wheels/tracks/legs, tall antenna, baked ownership, range, or detonation.
- **Target live envelope:** approximately 0.44 tile wide and 0.12 tile high.

The revised centre deliberately removes the first draft's white dome. A Trip
Node must not reuse the Core's luminous-sphere language.

## Sentinel Seed

- **Proposed T2 input:** `sentinel-seed/concept-source-v2.png`
- **Input SHA-256:**
  `22a8fec31336dde344424e3c7f7b0d9cf0bc7a7ea26477e3f39af8998bc9c728`
- **Gameplay read:** stationary, destructible hull-two sentry with a short
  omnidirectional gun; one active per Nest.
- **Required anchors:** angular planted base, exposed circular yaw bearing,
  compact freely rotating head, low symmetric shoulder guards below its sweep,
  flat sensor slit, short barrel resting right.
- **Forbidden cues:** spherical cargo/pod cluster, circular mine silhouette,
  mobile chassis, wheels/tracks/hover jets/walking legs, long artillery gun,
  tall leaves or a rear wall that imply a fixed firing arc, baked ownership,
  range, target line, projectile, or muzzle effect.
- **Target live envelope:** approximately 0.62 tile wide and 0.55 tile high.

The angular seed-pod language is intentionally unlike both Trip Node and the
similar dark spherical payload racks already visible on Minesmith and Nest.
`+X` is only the canonical rest pose. The approved Meshy 7 model has explicit
`stationary-base`, `yaw-head`, and `yaw-head-mesh` nodes, so only the head turns
toward the latest authoritative shot already present at the playhead. The v1
source remains as rejected provenance because its tall side leaves falsely
implied a forward-only casemate.

## Proposed provider settings

Use the existing `scripts/class-models/run-meshy-smart-topology.mjs` flow with
`meshy-t2`, `smart-topology`, 15,000 target faces, 4K PBR, `auto_size`, bottom
origin, alpha thumbnail, and multi-view thumbnails. Each task is expected to
consume 15 credits based on the sixteen-body fleet runs; confirm the live
balance before submission and record actual consumption.

## Provider review candidates

The owner approved the exact Trip Node v1 and Sentinel Seed v2 inputs and both
returned models on 2026-08-04. Both Meshy T2 tasks succeeded and consumed the
expected 15 credits each (`280` credits before, `250` after). The phone-friendly
provider comparison is `provider-review-v1.png`; `provider-audit.json` pins the
approved candidates and every provider input/result fact used by the runtime
pipeline.

### Trip Node result

- **Task:** `019fc9af-e9ee-70d9-84ac-f5266e7e71a0`
- **Provider stage:** 203.21 seconds; 203.29 seconds wall-clock including the
  timing wrapper.
- **Normalization:** `lay-flat-x`, 0.99-tile planform span, no facing yaw.
- **Review GLB:**
  `art/class-models/provider-runs/meshy/arc-trip-node/019fc9af-e9ee-70d9-84ac-f5266e7e71a0/model-normalized-review.glb`
- **Review SHA-256:**
  `efed3d3fdc016f8b37c86462c52db2ce6065f8da74068d4f301dec43e00e9295`
- **Measured review package:** 565,320 bytes, 15,673 triangles, one material,
  four 1024 WebP textures; normalized bounds `0.99 × 0.99 × 0.34903` tiles
  before the smaller runtime scale.

### Sentinel Seed result

- **Task:** `019fc9b3-1dbf-79e2-98c8-44e5658ddf0b`
- **Provider stage:** 191.76 seconds; 191.83 seconds wall-clock including the
  timing wrapper.
- **Normalization:** provider-native `identity`, 0.99-tile planform span, no
  facing yaw. The source is already +Y-up; laying it flat would be incorrect.
- **Review GLB:**
  `art/class-models/provider-runs/meshy/arc-sentinel-seed/019fc9b3-1dbf-79e2-98c8-44e5658ddf0b/model-normalized-identity-review.glb`
- **Review SHA-256:**
  `811fef88c756a7bc0c4bdb3f077d23057b9a617734ac85e210c3c438b66a06cf`
- **Measured review package:** 697,104 bytes, 13,612 triangles, one material,
  four 1024 WebP textures; normalized bounds `0.99 × 0.89876 × 0.57598` tiles
  before the smaller runtime scale.

Khronos glTF Validator reports zero errors and one accepted generated-tangent-
space warning per model (`MESH_PRIMITIVE_GENERATED_TANGENT_SPACE`).
`gltf-transform inspect` confirms one opaque, double-sided monolithic mesh with
base-color, normal, metallic/roughness, and emissive textures. Provider
multiviews preserve the Trip Node's flat radial read and the Sentinel's exposed
yaw bearing and clear horizontal gun sweep.

## Runtime promotion

The owner approved the larger matte Trip Node and split Sentinel Seed reviewed
in-game on 2026-08-12. The exact Meshy 7 review GLBs now ship as the defaults;
verify the byte-for-byte promotion with:

```sh
node scripts/class-models/promote-meshy7-signature-props-v3.mjs --check
```

Those two runtime files total 3,264,816 transfer bytes: Trip Node is 1,659,704
bytes and 79,196 triangles, while Sentinel Seed is 1,605,112 bytes and 77,573
triangles. Both remain below the approved per-prop ceilings of 2 MB and 100,000
triangles. They use quantized geometry and embedded 512 px WebP PBR maps—the
same lean review profile used for the Meshy 7 bot fleet. The WebGL renderer
loads a prop only when its signature appears; the mobile Canvas renderer does
not import these GLBs.

The renderer scales Trip Node to `0.75` and Sentinel Seed to `0.95` from their
0.99-tile normalized planforms. Trip's Meshy 7 original-UV retexture has a
deterministic roughness floor of `0.72`. Team ownership remains a subtle
renderer-owned floor ring; neither mesh owns teal or orange team paint.
Sentinel starts in its canonical +X rest pose and turns only its approved
`yaw-head` node toward the latest authoritative shot—never a future target.

Survey Flare is the third promoted signature model. Its exact 361,444-byte,
15,550-triangle Meshy 7 artifact uses one quantized mesh and three embedded
256 px WebP PBR maps with a deterministic 0.72 roughness floor. It replaces
only Lantern's rigid physical device; launch flight, landing pose, scan radius,
timing, survey field, team state, Canvas treatment, and load-failure fallback
remain renderer-owned. Verify it together with the flattened Core Well using:

```sh
node scripts/class-models/promote-core-well-survey-flare-v1.mjs --check
```

The previous Meshy T2/KTX2 candidates and their audit remain in this directory
as historical provenance; they are no longer the runtime defaults.

`art/reviews/arc-relay-signature-props/evidence.json` records error-free WebGL
captures from a 253-tick real replay at desktop and phone-landscape gameplay
scale. The procedural tetrahedron remains the asynchronous load/failure and
telegraph fallback; the finished body appears only in authoritative `active`
state.
