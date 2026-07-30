# Class projectile 3D fidelity brief

Status: authored production candidates; fixed-view and structural gates are
owned here, while final gameplay approval waits for the integration branch's
fresh, hash-verified native Frontline replay with its own V4 presentation
header.

These models interpret the already approved East-facing projectile masks in
depth. They are not a new projectile concept and do not change rules. The
runtime SVG remains canonical for Canvas2D, mobile, site surfaces, loading,
CLI playback, and WebGL fallback.

## Shared contract

- Runtime axes are `+X` forward and `+Y` up. Each authored planform is centred,
  rests at `Y=0`, and has a one-tile maximum source span before the existing
  look scale and renderer projectile factor.
- Every visible runtime material is team-neutral and carries
  `extras.nilbotsRole = "projectile-mask"`. Compact grayscale albedo, tangent
  normal, and metallic/roughness maps may preserve panel and wear breakup, but
  no fixed cyan or red survives into the asset.
- The GLB contains one directional projectile head only. It contains no trail,
  bloom, floor wash, impact, miss, deflection, hitbox, range, speed, camera,
  light, animation, or rules metadata.
- `look.json` owns axial spin, flat spin, or tumble so the authored GLB and SVG
  fallback share one deterministic motion language. Pulse Bolt remains the
  legacy/default fallback and is not replaced by this package.
- The accepted geometry tier must stay readable at the normal 16–28 px head
  size. Broad silhouette, open negative space, two-to-three value groups, and
  a restrained raised core take priority over invisible micro-detail.

## Trident Spark

- Canonical identity:
  `web/src/assets/class-projectile-looks/trident-spark/sprite.svg`
  (`b44365017f788d9ba8ff207f04976ee244f488493699d9214d2fd8a767b25f59`).
- Approved depth/material direction:
  `art/class-models/concept-targets/projectiles/trident-spark-3d-concept-v1/approval-sheet.png`.
- Provider reference only: Meshy task
  `019fb203-d779-779d-8374-59deec6606d3`, raw SHA-256
  `b48adbf2931a101663918e207af12794f5ee3cc3a480bf35e98ede979be0736d`.
- Required read: deep forked west/rear, long narrow waist, paired swept
  conductor fins, one longer east/front spear, and a raised central energy
  ridge.
- Correction from the audit: rebuild the full exact SVG planform and a
  0.165-tile authored side depth. The raw master's pinched underside and
  33.7% depth deficit are not retained.
- Forbidden cues: wings that imply steerable flight, detached blades, a
  second projectile, or a modelled trail.

## Rebound Diamond

- Canonical identity:
  `web/src/assets/class-projectile-looks/rebound-diamond/sprite.svg`
  (`7aa2a8621dbd75fa135135a2573b35aebcc140d46afc35b8cf4ae16f35d34dd7`).
- Approved depth/material direction:
  `art/class-models/concept-targets/projectiles/rebound-diamond-3d-concept-v1/approval-sheet.png`.
- Provider reference only: Meshy task
  `019fb206-978f-7ff8-9f18-e2b725c02584`, raw SHA-256
  `fad945e58c6010f72205787c1570c895395f4846a05e4984e232c06858f1964d`.
- Required read: short sharp west/rear, long east/front point, broad shoulders,
  a completely open elongated-hex tunnel, thick inner rim, and a low armoured
  profile.
- Correction from the audit: the aperture uses the exact SVG elongated hex.
  The raw rounded hole, which retained only 14.76% open area versus the
  authority's 27.59%, is not retained.
- Forbidden cues: a filled aperture, circular/orb silhouette, ricochet path,
  shield radius, or modelled impact.

## Lattice Rivet

- Canonical identity:
  `web/src/assets/class-projectile-looks/lattice-rivet/sprite.svg`
  (`e5f86e1d2c33816ab807132ced9274871854b2881ee2d47978d8ce980248a3ab`).
- Approved depth/material direction:
  `art/class-models/concept-targets/projectiles/lattice-rivet-3d-concept-v1/approval-sheet.png`.
- Provider reference only: Meshy task
  `019fb209-ea51-76ae-94ed-c6b3c92275b3`, raw SHA-256
  `f62409fd1d8bf3cafaaa92a29cd8e3eb9cfe072d51368c7ce1a63c253f030774`.
- Required read: joined split west/rear rails, stepped shoulders, a pointed
  east/front wedge, an open central diamond, pale-over-dark lattice value
  hierarchy, and compact visible truss/coil structure.
- Correction from the audit: no provider topology ships. The complete
  positive-depth top identity is retained as visual reference, the complete
  negative-depth duplicate is discarded, and one new underside is authored to
  the approved 0.160-tile side depth.
- Forbidden cues: two stacked bodies, detached rails, a fabricated child,
  tool arm, locomotion hardware, or modelled tumble path.

## Approval gates

Each candidate must independently pass:

1. Exact authority hash and one-tile, centred planform construction.
2. Strict top, side, front, rear, three-quarter, and pinned 58° gameplay
   renders from the generated GLB/source, with cyan and red tint proof.
3. Open-aperture and silhouette anchors at the 16–28 px gameplay size.
4. GLB 2.0 header, `+X`/`+Y` bounds, floor placement, tangent-bearing
   primitives, semantic materials, compact transfer budget, and deterministic
   rebuild hash.
5. Lazy WebGL loading with genuine-model/fallback behaviour, all headings,
   authored flight motion, fog, hit, miss, and deflection.
6. Final same-frame evidence only on a fresh native Frontline replay whose
   hash verifies and whose header explicitly carries the intended V4 map
   presentation. Overlay, fallback-theme, partial, or toy-layout boards cannot
   approve these models.
