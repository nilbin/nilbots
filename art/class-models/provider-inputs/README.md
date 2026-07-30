# Meshy input preparation

This directory contains deterministic, review-only isolation crops. No Meshy
task was submitted and no provider credit was spent while preparing them.

The checked-in partial covers the two selected mobile class bodies:

| Package | Review state | Provider state |
| --- | --- | --- |
| `meshy/aegis-tortoise-mobile` | Complete raw crop/provenance review | **Blocked** |
| `meshy/lattice-loom` | Complete raw crop/provenance review | **Blocked** |

Shell, whole Turret, and all three projectile heads remain independent future
packages. `generate-crops.zsh` records their reviewed source-panel crop
coordinates, but this partial deliberately does not check in those follow-up
crops or pretend they are upload-ready.

## Coordinate and orientation contract

- Crop sizes are recorded as height × width and offsets as `(y, x)`, matching
  `sips --cropToHeightWidth ... --cropOffset ...`.
- Cropping is lossless and never stretches, rotates, reflects, or warps a
  source.
- Model-local `+X` is East/forward/right for directional bodies.
- Model-local `+Y` is physical up. In a strict top image, `+Y` points out of
  the image plane; it does not mean the image's upper edge.
- Each package contains exactly one modeled object. Variants under one package
  are retained references for that same object, not extra task inputs.

The approved approval sheet remains the contact sheet for each package. The
individual crops are intentionally kept at native resolution as provenance.
They do not satisfy the Nilbots provider-input requirement for 1254×1254 flat
neutral images with at least 1040 px source detail and consistent object
scale. Studio cast/contact shadows also remain. These facts are explicit
blockers in each manifest, so an offline runner must refuse submission.

## Reproduction

Run `art/class-models/provider-inputs/generate-crops.zsh` from any directory in
this worktree. It uses the same macOS `sips` operation already recorded for
the accepted Striker crop pack. Re-running produces byte-identical PNGs on the
same toolchain.

`art/class-models/concept-targets/ALIGNMENT-REPORT.md` remains the geometry
gate. Aegis mobile and Lattice use the rich high-resolution 2D art as visual
and strict top-planform authority. Their runtime SVGs remain the semantic
team-region and Canvas/site fallback authorities.
