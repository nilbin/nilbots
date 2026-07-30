# Meshy input preparation

This directory contains deterministic provider-input derivatives and their
raw crop provenance. No Meshy task was submitted and no provider credit was
spent while preparing them.

The checked-in set covers two selected mobile class bodies and the three
approved default projectile heads:

| Package | Review state | Provider state |
| --- | --- | --- |
| `meshy/aegis-tortoise-mobile` | Clean V1 passed rich-2D overlay and input integrity | **Ready** |
| `meshy/lattice-loom` | Clean V1 passed rich-2D outer-planform and input integrity | **Ready** |
| `meshy/trident-spark` | Clean V1 passed exact SVG top-planform and input integrity | **Ready** |
| `meshy/rebound-diamond` | Clean V1 passed exact SVG top-planform and input integrity | **Ready** |
| `meshy/lattice-rivet` | Clean V1 passed exact SVG top-planform and input integrity | **Ready** |

Shell and whole Turret remain independent future packages.
`generate-crops.zsh` records their reviewed source-panel crop coordinates,
but this set deliberately does not check in those follow-up crops or pretend
they are upload-ready. Each projectile manifest is dry-run ready; no provider
task was submitted while producing or validating these inputs.

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

The approved approval sheets and native crops remain immutable provenance.
Each package's `clean-v1` directory contains the four submission inputs:
1254×1254, consistently scaled, no warp, exact flat `#b8b8b8` external
background, and no cast/contact shadow. The clean Aegis top achieved 99.16%
rigid silhouette IoU against its rich 2D authority. The clean Lattice top
achieved 90.96% full and 96.36% outer silhouette IoU; its remaining fork-tip
and internal truss cleanup is an explicit downstream Blender refinement, not
a provider-input blocker. The projectile tops achieved 99.81% for Trident
Spark, 99.77% for Rebound Diamond, and 99.64% for Lattice Rivet against their
exact runtime SVG authorities.

## Reproduction

Run `art/class-models/provider-inputs/generate-crops.zsh` from any directory in
this worktree. It uses the same macOS `sips` operation already recorded for
the accepted Striker crop pack. Re-running produces byte-identical PNGs on the
same toolchain.

Run `node art/class-models/provider-inputs/prepare-projectile-inputs.mjs` to
rebuild the clean projectile views and
`node art/class-models/provider-inputs/validate-projectile-inputs.mjs` to
repeat their offline background, scale, spill, and silhouette gates.

`art/class-models/concept-targets/ALIGNMENT-REPORT.md` remains the geometry
gate. Aegis mobile and Lattice use the rich high-resolution 2D art as visual
and strict top-planform authority. Their runtime SVGs remain the semantic
team-region and Canvas/site fallback authorities. Each projectile uses its
runtime SVG as the exact top-planform authority and its approved 3D concept as
the material and depth authority.
