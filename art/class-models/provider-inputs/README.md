# Meshy input preparation

This directory contains deterministic provider-input derivatives and their
raw crop provenance. No Meshy task was submitted and no provider credit was
spent while preparing them.

The checked-in input set covers the two selected mobile class bodies plus the
two distinct approved Bulwark deployed forms:

| Package | Review state | Provider state |
| --- | --- | --- |
| `meshy/aegis-tortoise-mobile` | Clean V1 passed rich-2D overlay and input integrity | **Ready** |
| `meshy/lattice-loom` | Clean V1 passed rich-2D outer-planform and input integrity | **Ready** |
| `meshy/aegis-tortoise-shell` | Clean V1 preserves inferred 3D depth and improves rigid SVG IoU from 82.37% to 88.57% | **Ready** |
| `meshy/aegis-tortoise-turret` | Clean V1 is one whole radial object and improves rigid SVG IoU from 80.16% to 85.85% | **Ready** |

All three projectile heads remain independent future packages.
`generate-crops.zsh` records reviewed source-panel crop coordinates, but the
projectiles are not included or treated as upload-ready here.

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
a provider-input blocker.

## Reproduction

Run `art/class-models/provider-inputs/generate-crops.zsh` from any directory in
this worktree. It uses the same macOS `sips` operation already recorded for
the accepted Striker crop pack. Re-running produces byte-identical PNGs on the
same toolchain.

`art/class-models/concept-targets/ALIGNMENT-REPORT.md` remains the geometry
gate. Aegis mobile and Lattice use the rich high-resolution 2D art as visual
and strict top-planform authority. Their runtime SVGs remain the semantic
team-region and Canvas/site fallback authorities.

Shell and whole Turret instead use their runtime SVGs as strict top and
semantic authorities. Their accepted inputs preserve the approved inferred-3D
top rather than substituting a flat SVG, and pass the explicit
material-improvement gate retained in `BULWARK-FORM-AUDIT.md`. Exact final
contours, semantic material separation, topology, pivots, and animation remain
downstream technical-art gates.
