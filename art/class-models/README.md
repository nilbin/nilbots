# Frontline 3D class-model vertical slice

The three primary class bodies and their default projectiles now have
manifest-driven, on-demand WebGL companions. Striker established the
generated-geometry route, Fabricator uses the same static companion contract,
and Bulwark establishes the deterministic animated-authority plus generated
texture-donor route. Bulwark Mobile, Shell, and Tower are one mechanically
continuous model rather than unrelated form swaps.

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

Striker's shallow `low-hover` is canonical presentation metadata in
`look.json`. Fabricator's levitation and Bulwark's heavy low hover are likewise
presentation metadata shared by the model and SVG fallback.

The three default projectile heads remain separate objects with their own
manifests and GLBs. Their spin, travel, trail, floor glow, team tint, and
terminal effects remain renderer-owned. Their look manifests select only the
outcome-agnostic signature: Striker fork burst, Bulwark facet bloom, and
Fabricator lattice unweave.

## Accepted animated Bulwark family

The durable authority is under `masters/aegis-tortoise-kinematic/`. Blender
owns the 22,320-triangle Mobile/Shell/Tower machine, all rigid pivots, hidden
faces, exact endpoints, one UV atlas, four texture-free team emitters, and 14
semantic clips.

Meshy task `019fb4bc-03d1-7142-9a37-7c3a8e2ffc34` supplied only the stylized
2K graphite/gunmetal PBR payload. Its returned GLB fused, welded, and normalized
the upload, so a deterministic oriented-UV similarity proof discarded that
geometry and transplanted only the verified maps onto the untouched authority.
The final 8,024,032-byte GLB has SHA-256
`a169822ef3c3b98ab854257e3c637e00f50900e53cfb3f51dfd8910d7ec4df8b`.

The WebGL renderer samples Shell/Tower deploy and reverse, form-specific
recoil, and settle from replay truth through one mixer per clone. It falls back
to the canonical SVG representation if the manifest or clip bindings fail.
The earlier `masters/aegis-tortoise-mobile/` archive remains as historical
mobile-only evidence; it is not the full-family runtime authority.
