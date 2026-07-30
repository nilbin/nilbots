# Deterministic Blender export validation

- Blender: `5.2.0 LTS`
- Recipe: `scripts/class-models/build-lattice-loom.py`
- Source SHA-256:
  `0cd9084e14355323b46194f84e9a406cb07fa19f69320c62dce263223dbd0b6f`
- Runs: two independent factory-startup processes with separate output
  directories
- Lean GLBs: byte-identical
- Lean bytes: `4,978,440`
- Lean SHA-256:
  `60bbc96d292ee36408cadcda2786f87dbd48060ef3fed769a43986bc8916f2c6`
- Rich GLBs: byte-identical
- Rich bytes: `11,911,816`
- Rich SHA-256:
  `c0dff80ec24e76c108ee451f51050514938aadb4d26454f44448f4d44b0102b6`
- Final semantic GLB reruns: byte-identical
- Final semantic bytes: `6,604,048`
- Final semantic SHA-256:
  `e5ada31e067aea1e44d62d561e0b33e1b892a1e883ac7aade7d88993a946bcb1`

The lean export examines and canonicalizes all `68,743` tangent vertices,
repairing the five invalid zero-length values. The rich export examines and
canonicalizes all `110,284`, repairing six. Geometry, normals, UVs, topology,
materials, and embedded textures are not rewritten by that pass.

The `.blend` files are compressed editable provenance and are not expected to
be byte-identical because Blender records save metadata. Determinism is asserted
on the normalized GLBs and the final semantic derivative.
