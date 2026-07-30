# Khronos glTF validation

- Tool: Khronos `gltf-validator` `2.0.0-dev.3.10`
- Input: `web/src/assets/class-looks/lattice-loom/model.glb`
- Input bytes: `6,604,048`
- Input SHA-256:
  `e5ada31e067aea1e44d62d561e0b33e1b892a1e883ac7aade7d88993a946bcb1`
- Errors: `0`
- Warnings: `0`
- Information messages: `0`
- Hints: `0`
- Draw calls: `2`
- Triangles: `75,000`
- Material count: `2`
- Animation count: `0`
- Skins/morph targets: none

Blender authored the tangent accessor. Five vertices on collapsed UV
derivatives originally received zero-length tangents; the deterministic build
recipe replaces those five with a stable normal-derived perpendicular. It
also canonicalizes every tangent through a two-decimal direction grid and
renormalizes it to eliminate sub-thousandth Blender seam-solver drift between
independent runs. The final two normal-mapped primitives share that accessor
and validate without a portability waiver.
