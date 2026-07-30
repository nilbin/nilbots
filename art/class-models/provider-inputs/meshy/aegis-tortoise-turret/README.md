# Aegis Tortoise whole Turret — Meshy input package

Status: **clean V1 provider input ready**. No provider task was submitted and
no provider credit was spent while preparing this package.

This package contains exactly one object: the complete radial Turret,
including its singular hub and four equal cardinal arms. It is not one turret
arm, a set of four disconnected objects, the Shell, mobile Aegis, projectile,
effect, floor, or scenery.

The owner-approved inferred-3D sheet remains immutable under
`concept-targets/`. `clean-v1` preserves its depth and material language while
adding the missing diagonal shoulder webs and trimming all terminal pods
equally toward the authoritative Turret SVG. The rigid same-scale top IoU
improved from 80.16% to 85.85% without rotation, reflection, deformation, or
nonuniform warp.

Semantic review passed: one centered class-energy reactor, strict fourfold
symmetry, four equal cardinal arms, and exactly four equal team inlays.

The generated result will still be a static base mesh. A downstream
Blender/technical-art pass owns exact final silhouette correction,
segmentation, retopology, semantic team material, pivots, and deterministic
anchor/fire animation. Runtime must mount this whole object once; it must not
send the model through the existing path that clones one turret arm four times.

See `clean-v1/README.md` for exact prompts, hashes, and crop provenance, and
`../../BULWARK-FORM-AUDIT.md` for the retained rigid overlay audit.
