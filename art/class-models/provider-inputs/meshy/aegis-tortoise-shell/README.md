# Aegis Tortoise Shell — Meshy input package

Status: **clean V1 provider input ready**. No provider task was submitted and
no provider credit was spent while preparing this package.

This package contains exactly one object: the distinct deployed directional
Shell body. It does not contain the mobile Aegis, whole Turret, projectile,
effect, floor, or scenery.

The owner-approved inferred-3D sheet remains immutable under
`concept-targets/`. `clean-v1` preserves that real depth and material language
while making a targeted top-planform correction toward the authoritative Shell
SVG. The rigid same-scale top IoU improved from 82.37% to 88.57% without
rotation, reflection, deformation, or nonuniform warp.

Semantic review passed:

- the physical guard occupies only the East/right 90-degree quadrant;
- the other three quadrants remain visibly open;
- the Shell has no gun or muzzle;
- the two guard-edge bars and west-centre panel are the three team inlays;
- the central reactor remains authored class energy; and
- rear corner-pod bars are not team-owned.

The generated result will still be a static base mesh. A downstream
Blender/technical-art pass owns exact final silhouette correction,
segmentation, retopology, separate semantic team material, pivots, and the
deterministic deploy/retract animation.

See `clean-v1/README.md` for exact prompts, hashes, and crop provenance, and
`../../BULWARK-FORM-AUDIT.md` for the retained rigid overlay audit.
