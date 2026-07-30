# Frontline 3D class-model vertical slice

The Striker mobile body is the first accepted generated-geometry vertical
slice. Its reviewed runtime derivative is staged beside
`web/src/assets/class-looks/trident-wasp/sprite.svg`; every other class body,
stance, emplacement, and class projectile still uses the canonical SVG
extrusion fallback.

Aegis Tortoise mobile and Lattice Loom now also have separately charged,
audited high-detail provider masters. Neither raw master is a runtime asset:

- Aegis passes the rich top at 97.06% IoU, but its side is approximately 41%
  too thick and its four walking supports collapsed into the lower hull. Keep
  its armor/top as reference and reconstruct/rig the lower chassis in Blender.
- Lattice passes at 93.62% outer IoU to its clean top and 90.67% to the rich
  2D authority. Keep the low-hover/no-feet/open-truss body, then perform
  targeted fork/truss correction, semantic material isolation, and runtime
  optimization.

Bulwark's distinct Shell and whole-Turret forms now have separately charged
raw masters and a fixed Blender 5.2 audit as well. Both reconstruct their
corrected concept inputs strongly, but exact welding proves each is one
watertight manifold rather than usable rigid parts:

- Shell reaches 90.56% top IoU to the corrected concept input, 84.81% to the
  exact form SVG, and is 17.1% too thin in side profile. Preserve its visual
  evidence while rebuilding named guard and support hierarchies.
- Whole Turret reaches 95.62% top IoU to the corrected concept input, 83.98%
  to the exact form SVG, and 99.84% fourfold rotational IoU. Rebuild one
  canonical arm/support hierarchy at exact 90-degree increments around one
  singular hub; the completed whole object is mounted once.

The tasks, exact inputs, hashes, PBR maps, audit overlays, and raw verdicts are
recorded under `provider-inputs/meshy/` and `provider-runs/meshy/`. The raw
GLBs remain ignored recoverable masters; only reviewed derivatives may enter
`web/src/assets/class-looks/`.

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

## Remaining landing work

Before merging, retain the deterministic team-accent script/config/report
outside ignored scratch space, record the tangent-space decision, rebase onto
Fable's current integration tip, and rerun the full web and production/CLI
builds. The Frontline map branch owns the corrected live-span wall-clearance
proof and modular wall implementation; it does not change gameplay collision.

Striker's shallow `low-hover` is canonical presentation metadata in
`look.json`, independent of whether an approved GLB ever exists.

The default `trident-spark` projectile has not been submitted to Meshy. It is a
separate object, input review, task, and credit charge after this chassis
landing; its trail, floor glow, travel, impact, hitbox, and team tint remain
renderer-owned.
