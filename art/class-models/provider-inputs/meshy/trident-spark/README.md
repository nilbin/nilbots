# Trident Spark provider-input package

Status: **provider-ready offline; never submitted**.

This package contains one Striker default projectile head and no bot, trail,
glow, spin, hit, miss, deflection, floor wash, or hitbox presentation.
`clean-v1` provides strict top, longitudinal side, front three-quarter, and
rear three-quarter 1254×1254 RGB PNGs at one 990 px object-width scale.

The strict top is clipped by
`web/src/assets/class-projectile-looks/trident-spark/sprite.svg`; its retained
raster gate is 99.81% IoU, with every pixel beyond the two-pixel antialias
transition at exact `#b8b8b8`. The owner-approved concept remains at
`art/class-models/concept-targets/projectiles/trident-spark-3d-concept-v1`.
The focused top texture changes material evidence only; it cannot overrule the
SVG silhouette.

- Manual original-resolution review: all four views passed; no clipped or
  erased hardware, shadow wedge, green spill, floor, or foreign object.
- Contact sheet: `clean-v1/contact-sheet.png` (review only, never upload).
- Overlay: `clean-v1/top-overlay.png` (review only).
- Exact new prompts and source hashes:
  `../../../PROJECTILE-PROMPTS.md`.
- Reproduction: `../../../prepare-projectile-inputs.mjs`.
- Integrity gate: `../../../validate-projectile-inputs.mjs`.

Any reconstructed GLB remains a static base mesh. Every visible tintable head
primitive must use a material with
`extras.nilbotsRole = "projectile-mask"` while retaining useful neutral PBR
detail. Runtime owns accent tint, emission response, trail, axial spin, travel,
hit/miss/deflection effects, and hitbox.
