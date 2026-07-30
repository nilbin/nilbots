# Trident Wasp team-accent derivative

This directory pins the deterministic authoring step that produced the
reviewed Striker GLB. The provider master, lossless provider maps, and
normalized lean source are deliberately not ordinary Git blobs. They must be
restored at the paths in `recipe.json`; the scripts reject any byte or hash
drift before processing.

From the repository root, after installing the web dependencies:

```sh
node scripts/class-models/extract-team-accent.mjs \
  art/class-models/provider-runs/meshy/trident-wasp/019fb00c-9e8d-723b-9485-49706e307cb9/team-accent/recipe.json \
  sandbox/class-model-builds/trident-wasp-team-accent

node scripts/class-models/validate-team-accent.mjs \
  art/class-models/provider-runs/meshy/trident-wasp/019fb00c-9e8d-723b-9485-49706e307cb9/team-accent/recipe.json \
  sandbox/class-model-builds/trident-wasp-team-accent
```

The accepted output is
`striker-team-accent-review.glb`, 4,588,820 bytes, SHA-256
`17c1998906729ebe70d8653c8b18b588ac3acfbeb521698737a266279147c07e`.
It partitions the untouched 88,989 source triangles into 83,825 hull and
5,164 semantic team-accent triangles. Both primitives share the original
position, normal, and UV accessors; the team primitive retains normal and
metallic/roughness maps but has no fixed base-color or emission map.

`accepted-build-report.json` and `accepted-validation.json` are the outputs of
the commands above. The validator pins all source/output hashes, the lossless
mask dimensions, the triangle multiset, scene graph, normalized bounds,
semantic material roles, retained PBR payloads, and the absence of cameras,
lights, and animation.

The two recorded `MESH_PRIMITIVE_GENERATED_TANGENT_SPACE` warnings are known:
both materials use the retained normal map while the provider geometry has no
authored tangent accessor. The current Three.js renderer generates a tangent
basis and passed real-replay review, but the warning remains an explicit
portability waiver rather than being hidden.

The baked model spans 1.12 tiles. With the current look/runtime multiplier of
1.062, its live presentation span is 1.18944 tiles. This record reproduces the
reviewed artifact exactly; it does not waive the separate map-clearance/scale
blocker.

No API credential, Authorization header, input data URI, provider download
URL, or signed query is consumed or recorded by this offline step.
