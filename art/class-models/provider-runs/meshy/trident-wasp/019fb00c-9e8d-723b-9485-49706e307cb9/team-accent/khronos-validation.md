# Khronos glTF validation

- Tool: `gltf-transform 4.2.1 validate` (official glTF Validator)
- Input: `striker-team-accent-review.glb`
- Input SHA-256:
  `17c1998906729ebe70d8653c8b18b588ac3acfbeb521698737a266279147c07e`
- Errors: 0
- Warnings: 2
- Information messages: 0
- Hints: 0

Both warnings are
`MESH_PRIMITIVE_GENERATED_TANGENT_SPACE`, at
`/meshes/0/primitives/0/material` and
`/meshes/0/primitives/1/material`. The materials use a normal map while the
provider geometry has no authored tangent accessor. This matches
`recipe.json` and `accepted-validation.json`.
