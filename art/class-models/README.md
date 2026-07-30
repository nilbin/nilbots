# Frontline 3D class-model vertical slice

The Striker and Fabricator mobile bodies are accepted generated-geometry
vertical slices. Their reviewed runtime derivatives are staged beside the
canonical SVGs for `trident-wasp` and `lattice-loom`; every other class body,
stance, emplacement, and class projectile still uses the canonical SVG
extrusion fallback.

The companion supplements rather than replaces the SVG. WebGL discovers and
downloads the separate GLB only when the 3D renderer resolves that look. The
site, Canvas2D, mobile, CLI, loading state, and failed-model path remain on the
canonical 2D asset.

## Accepted Striker result

The clean multiview v2 inputs and fidelity evidence are under
`concept-targets/meshy-striker-v2-candidate/`. The accepted geometry came from
Meshy task `019fb00c-9e8d-723b-9485-49706e307cb9` with image enhancement
disabled:

- rigidly normalized top-planform IoU: `95.673%`;
- accepted deliberate deviation: a taller, more rectilinear side profile;
- runtime GLB: `4,588,820` bytes, `88,989` triangles, two primitives;
- runtime SHA-256:
  `17c1998906729ebe70d8653c8b18b588ac3acfbeb521698737a266279147c07e`;
- one renderer-owned `team-accent` material with shared normal and
  metallic/roughness detail;
- blue/orange replay review, semantic structural checks, WebGL review build,
  and the web test suite pass.

The team split is a deterministic offline derivative of the accepted fused
material. It preserves geometry, UVs, transforms, facing, and the complete
triangle multiset while neutralizing cyan in the hull maps. The renderer owns
team tint, bounded emissive response, and the floor glow.

The GLB has no validation errors and two generated-tangent-space portability
warnings, one per normal-mapped primitive. Three.js renders the derivative
correctly; the landing record must explicitly accept that target-renderer
basis or add authored tangents before claiming broader renderer portability.

The GLB's authored span is `1.12` tiles. The current Striker look then applies
`1.18 × 0.9 = 1.062`, so its effective live span is `1.18944` tiles. Frontline
clearance must use that live value rather than the GLB span. The approved
direction is to preserve the larger look and give open wall edges a
presentation-only setback; actor-owned idle/recoil motion remains a separate
wall-aware clamp problem.

## Accepted Fabricator result

The accepted Lattice Loom geometry came from Meshy task
`019fb1da-1e28-7e26-8237-8913171b482a` with image enhancement disabled. The
immutable provider master is retained in the project artifact store; the
runtime derivative and reproducible evidence are under
`provider-runs/meshy/lattice-loom/<task-id>/team-accent/`.

- rigid top-planform full IoU: `81.322%`;
- rigid top-planform outer-envelope IoU: `88.501%`;
- runtime GLB: `6,604,048` bytes, `75,000` triangles, two primitives;
- runtime SHA-256:
  `e5ada31e067aea1e44d62d561e0b33e1b892a1e883ac7aade7d88993a946bcb1`;
- exactly 131 team-owned faces across four bounded shoulder/fork-root panel
  quadrants; the amber core, coils, forks, and hover energy stay authored;
- authored tangents, zero Khronos errors or warnings, and byte-identical lean
  and rich GLBs from two independent deterministic Blender runs;
- blue/orange real-replay review at normal follow and the supported eight-tile
  minimum zoom, plus an explicit failed-GLB fallback capture.

The deterministic correction is a fixed `180°` yaw, one uniform
centre/floor/size normalization, and a bounded extension of only the two short
outer fork prongs. There is no global anisotropic warp. The accepted 75k/1K
tier was visually indistinguishable at the real camera from the retained
150k/2K comparison, whose final semantic GLB measured `17,194,988` bytes.

The GLB's authored span is `1.08` tiles. The Fabricator look applies
`1.14 × 0.9 = 1.026`, so its effective live span is `1.10808` tiles. Its
`low-hover` cue is presentation-only: rules, movement, and collision remain on
the ground layer. Fabrication continues to create separate identical
instances; neither the model nor renderer implies self-fabrication.

## Rejected procedural evidence

The procedural Blender proof was rejected because its three-dimensional form
and visual quality did not meet the approved target. Increasing texture size
did not address that modeling gap.

| Measured proof | GLB bytes | Texture-source bytes |
| --- | ---: | ---: |
| Striker geometry only | 325,080 | 0 |
| Striker 256px PBR | 663,784 | 242,626 |
| Striker 512px PBR | 1,320,624 | 899,437 |

The proof used 5,940 triangles and six materials. A wider nine-look experiment
measured 396,388 bytes for the old lean baseline, 873,956 bytes for geometry
only, 1,468,708 bytes for the mid tier, and 3,255,816 bytes for the rich tier.
These numbers document the on-demand cost decision only; they are not visual
approval.

All generated GLBs, Blender files, textures, previews, build reports, and
procedural model generators from those failed experiments were removed so the
approach cannot be mistaken for a production pipeline.

## Landing boundaries

The Frontline map branch owns the corrected live-span wall-clearance proof and
modular wall implementation; it does not change gameplay collision. Runtime
models remain WebGL-only, manifest-driven, and lazy-loaded. The canonical
2D assets continue to serve the site, Canvas2D, mobile, CLI, loading, and
failed-model paths.

Striker and Fabricator shallow `low-hover` cues are canonical presentation
metadata in `look.json`, independent of whether an approved GLB exists.

The default `trident-spark` projectile has not been submitted to Meshy. It is a
separate object, input review, task, and credit charge after this chassis
landing; its trail, floor glow, travel, impact, hitbox, and team tint remain
renderer-owned.
