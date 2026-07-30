# Lattice Loom runtime derivative

This directory pins the deterministic Blender normalization and semantic
team-accent split that produce the Fabricator's reviewed WebGL model. The
provider master and lossless maps remain in the project artifact store rather
than ordinary Git blobs. Restore them at the paths named by the build and
accent recipes before rebuilding.

The Blender stage is:

```sh
/path/to/blender --background --factory-startup \
  --python scripts/class-models/build-lattice-loom.py -- \
  --source /path/to/raw-model.glb \
  --output-dir sandbox/class-model-builds/lattice-loom
```

It applies the audited fixed `180°` yaw, a uniform scale and floor/centre
translation, and one bounded fork-local correction. It does not nonuniformly
warp the fused body. The accepted lean tier keeps 75,000 triangles, 1K PBR
maps and authored tangents. `lattice-loom-lean.blend` is the editable output
of that stage.

The team split is:

```sh
node scripts/class-models/extract-team-accent.mjs \
  art/class-models/provider-runs/meshy/lattice-loom/019fb1da-1e28-7e26-8237-8913171b482a/team-accent/recipe.json \
  sandbox/class-model-builds/lattice-loom/team-accent

node scripts/class-models/validate-team-accent.mjs \
  art/class-models/provider-runs/meshy/lattice-loom/019fb1da-1e28-7e26-8237-8913171b482a/team-accent/recipe.json \
  sandbox/class-model-builds/lattice-loom/team-accent
```

The accepted output is `lattice-loom-team-accent.glb`, 6,604,048 bytes,
SHA-256
`e5ada31e067aea1e44d62d561e0b33e1b892a1e883ac7aade7d88993a946bcb1`.
It losslessly partitions all 75,000 source triangles into 74,869 hull and 131
team-accent triangles. Those 131 faces occupy exactly the four authored panel
quadrants: port/starboard shoulders and port/starboard fork roots. The
honeycomb core, coils, forks and hover energy remain amber.

The richer comparison was 150,000 triangles with 2K maps and a 17,194,988
byte final GLB. It was visually indistinguishable at the real `58°` Fable
camera, including the supported eight-tile minimum zoom, so it is retained as
measurement evidence rather than shipped transfer.

The top silhouette measures `81.322%` full IoU and `88.501%` outer-envelope
IoU against the approved rich 2D authority under rigid centre/uniform-width
alignment. No rotation, reflection, anisotropic scale or warp is used in that
comparison.

The GLB faces `+X`, uses `+Y` as up, sits on `Y=0`, contains no cameras,
lights, skin, morphs or animation, and has tangent accessors on both
normal-mapped primitives. Blender's five zero tangents at collapsed UV
derivatives are replaced by deterministic normal-derived perpendiculars. All
tangent directions are then canonicalized through the documented two-decimal
direction grid and renormalized, eliminating Blender seam-solver byte drift
without changing geometry, UVs, topology or textures. Two independent builds
produce byte-identical rich and lean GLBs, and the final artifact has zero
Khronos errors or warnings. The low hover is canonical `look.json`
presentation metadata; it does not change the Fabricator's ground
movement/collision layer, and fabrication remains a separate identical
instance.
