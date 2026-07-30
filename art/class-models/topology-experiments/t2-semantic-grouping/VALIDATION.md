# Validation verdict

Verdict: **the T2 turret is usable as a high-fidelity rigid-animation
candidate**. It should be retained as an alternate/store Turret skin candidate,
not discarded or replaced by the lower-fidelity procedural shell.

This does not make it a shipping runtime asset. The renderer event binding,
both-team mutation, replay-scale motion, transfer tier, and owner visual review
remain open.

## Geometry and grouping

- Provider master SHA-256 before and after:
  `f9388c05e54bcf1603b7f901eefe0dedcaf7c636a384dcd039d4fe433e03dd1c`.
- Source: 18,274 vertices, 14,352 triangles, one UV layer, one fused PBR
  material.
- Indexed topology contains 2,529 islands. A `1e-6` position weld used only
  for classification yields 206 connected components.
- Every component and source face has exactly one recorded destination in
  `build/report.json`.
- The combined world-position/UV triangle multiset after grouping has the
  same SHA-256 as the provider source:
  `aaed67bc22e6c1ea5e559043b7c6d129bc9c69539ea617fee959f05c5b07140d`.
- Five resulting source-derived groups conserve all 14,352 triangles:

  | Group | Components | Triangles |
  | --- | ---: | ---: |
  | Hub | 83 | 5,696 |
  | Arm NE | 33 | 2,217 |
  | Arm NW | 30 | 2,154 |
  | Arm SW | 30 | 2,139 |
  | Arm SE | 30 | 2,146 |

- Arm balance is `0.964817321` from smallest to largest.
- Thirty-four components (`9.2461%` of source triangles) lie near the
  hub/arm angular boundary and remain explicitly listed. None crosses or
  conflicts with another arm sector; the blocking cross-arm ambiguity is
  `0.0%`.
- The false-color top render shows four visually complete diagonal arm masses,
  the shorter cardinal pods in the hub, and no stranded large arm panel.

This partition is geometric, not semantic AI inference. The remaining
hub/arm seam classification still needs human scrutiny during motion because a
geometrically consistent partition cannot prove an intended mechanical cut.

## PBR and semantic overlays

The source-derived groups retain the imported UVs and the shared provider
material:

- base color: 4096×4096;
- normal: 4096×4096;
- metallic/roughness: 2048×2048;
- emissive: 2048×2048.

The provider atlas is not recolored. The animated candidate adds five
independent, removable meshes:

- four small arm-top strips share `NB_TEAM_ACCENT`, whose glTF material extras
  contain `nilbotsRole = "team-accent"`;
- one centre ring uses `NB_CLASS_ENERGY`, whose extras contain
  `nilbotsRole = "class-energy"`.

The neutral strips are intentionally plain in the DCC review. The renderer must
still prove cyan/red tint and glow without altering the provider hull material.

## Rigid animation

The animated GLB contains one five-joint skin:

```text
Hub
├── Arm_NE
├── Arm_NW
├── Arm_SW
└── Arm_SE
```

Every source-derived vertex has exactly one weight, so the clips move rigid
parts without bending armor or degrading the T2 surface.

- `Turret_Deploy`: 1.0 second, 4 channels, 12 keyframes. All arms make a
  small symmetric inward/high start, pass a restrained outward settle, and
  return to the authored rest pose.
- `Turret_Recoil`: 0.5 seconds, 4 channels, 16 keyframes. All arms make a
  small symmetric lock compression and rebound.

There is no scan clip, world rotation, directional aim, or authoritative-facing
change. These clips are presentation candidates for recorded form/firing
events only.

## GLB structure and validation

| Output | Bytes | Triangles | Meshes | Materials | Skin | Clips |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| grouped base | 28,122,648 | 14,352 | 5 | 1 | 0 | 0 |
| animated + overlays | 28,613,440 | 15,552 | 10 | 3 | 1 | 2 |

The animated bounds in glTF coordinates are
`[-0.5, -0.10693, -0.5]` to `[0.5, 0.115, 0.5]`.

Both GLBs were checked offline with glTF Transform/Khronos validator `4.2.1`.
Both returned:

- zero errors;
- zero warnings;
- zero infos;
- zero hints.

Tangents are exported on every source-derived normal-mapped primitive.
Overlay meshes have no unused UV/tangent attributes.

## Independent rerun

Two fresh Blender 5.2 background processes built `build-m` and `build-n` from
the same read-only source and script.

The meaningful authored outputs were byte-identical:

- grouped GLB:
  `f85f8dc6b922dabeb04b7f37f24f52bc187c14c791fb7e584a11670cff86bd9f`;
- animated GLB:
  `5fe60176625b562b0c8a34d7aa12962289ec209911df84f2b4c37d2c23786224`;
- complete JSON report:
  `8ddc5a0c5e3fc23931406584b3743f907b31c466c6e09d265787a2e0050c680e`.

Blender's editable `.blend` container is not byte-stable and is not the
canonical build source. The checked-in Python recipe is.

Eevee produced tiny process-local raster differences despite fixed one-sample
settings and PNG normalization: across 17 review renders, 4 were byte-identical
and each other image changed at most 5 of 589,824 pixels, with a maximum channel
delta of 16/255. This is visually immaterial but is recorded rather than called
byte-deterministic. GLB and report determinism pass.

## Remaining gates

1. Owner review of the base/overlay angles, grouping map, and deploy/recoil
   start/mid/end frames.
2. Inspect hub/arm gaps and overlap through the complete clips in the actual
   Three.js renderer.
3. Mutate `team-accent` to both team colors and apply renderer-owned glow;
   verify the fixed class-energy ring does not change team.
4. Bind deploy only to the recorded form transition and recoil only to a
   recorded shot. The turret remains stationary and non-rotating.
5. Review at the 58-degree gameplay camera in a native replay, under fog,
   hit flash, multiple instances, and every shot heading.
6. Produce and compare an on-demand lean texture tier. The current 28.6 MB
   review master is not an approved runtime budget.
