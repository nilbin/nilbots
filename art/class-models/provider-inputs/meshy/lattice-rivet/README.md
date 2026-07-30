# Lattice Rivet provider-input package

Status: **provider-ready offline; never submitted**.

This package contains one Fabricator default projectile head and no bot,
fabricated child, trail, glow, tumble, hit, miss, floor wash, or hitbox
presentation. `clean-v1` provides strict top, longitudinal side, front
three-quarter, and rear three-quarter 1254×1254 RGB PNGs at one 990 px
object-width scale.

The strict top is clipped by
`web/src/assets/class-projectile-looks/lattice-rivet/sprite.svg`; its retained
raster gate is 99.64% IoU. The central diamond aperture and the SVG-authored
rear notch remain exact background beyond the two-pixel antialias transition;
the small raised rear diamond is also SVG-authored, not a shadow remnant. The
owner-approved concept remains at
`art/class-models/concept-targets/projectiles/lattice-rivet-3d-concept-v1`.

- Manual original-resolution review: all four views passed; pale ceramic,
  dark trusses, joined rear rails, copper hardware, and all open cells remain
  intact with no gray flood damage, green spill, floor, or foreign object.
- Contact sheet: `clean-v1/contact-sheet.png` (review only, never upload).
- Overlay: `clean-v1/top-overlay.png` (review only).
- Exact new prompts and source hashes:
  `../../../PROJECTILE-PROMPTS.md`.
- Reproduction: `../../../prepare-projectile-inputs.mjs`.
- Integrity gate: `../../../validate-projectile-inputs.mjs`.

Any reconstructed GLB remains a static base mesh. Every visible tintable head
primitive must use a material with
`extras.nilbotsRole = "projectile-mask"` while retaining useful neutral PBR
detail. Runtime owns accent tint, emission response, trail, slow tumble,
travel, terminal effects, and hitbox.
