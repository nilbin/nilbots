# Rebound Diamond provider-input package

Status: **provider-ready offline; never submitted**.

This package contains one Bulwark default projectile head and no bot, trail,
glow, spin, ricochet, hit, miss, deflection, floor wash, or hitbox
presentation. `clean-v1` provides strict top, a newly reconstructed true
West-to-East longitudinal side (not the concept's end-on panel), front
three-quarter, and rear three-quarter 1254×1254 RGB PNGs at one 990 px
object-width scale.

The strict top is clipped by
`web/src/assets/class-projectile-looks/rebound-diamond/sprite.svg`; its
retained raster gate is 99.77% IoU. The elongated-hex tunnel is exact
`#b8b8b8` beyond the two-pixel antialias transition and remains visibly open
in both obliques. The owner-approved concept remains at
`art/class-models/concept-targets/projectiles/rebound-diamond-3d-concept-v1`.

- Manual original-resolution review: all four views passed; no filled
  aperture, clipped/erased hardware, shadow, green spill, floor, or foreign
  object.
- Contact sheet: `clean-v1/contact-sheet.png` (review only, never upload).
- Overlay: `clean-v1/top-overlay.png` (review only).
- Exact new prompts and source hashes:
  `../../../PROJECTILE-PROMPTS.md`.
- Reproduction: `../../../prepare-projectile-inputs.mjs`.
- Integrity gate: `../../../validate-projectile-inputs.mjs`.

Any reconstructed GLB remains a static base mesh. Every visible tintable head
primitive must use a material with
`extras.nilbotsRole = "projectile-mask"` while retaining useful neutral PBR
detail. Runtime owns accent tint, emission response, trail, flat spin, travel,
ricochet/deflection, terminal effects, and hitbox.
