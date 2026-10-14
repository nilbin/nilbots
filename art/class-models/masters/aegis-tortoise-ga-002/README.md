# Aegis Tortoise `ga-002` — folded-tower family authority

This directory freezes the owner-approved elevated/folded-tower Bulwark scene
formerly reviewed as `bulwark-elevated-tower-concept-v22`. The promoted scene
is both the retained conceptual 3D source and the candidate production
geometry/animation authority. Meshy may supply texture donors, but it never
replaces this geometry, hierarchy, UV layout, pivots, clips, cameras, or
semantic team-light surfaces.

## Stable identities

- `familyId`: `aegis-tortoise`
- `conceptId`: `aegis-tortoise-elevated-tower-concept-v22`
- `geometryAuthorityId`: `aegis-tortoise-ga-002`
- historical predecessor: `aegis-tortoise-ga-001`, archived at
  `art/class-models/masters/aegis-tortoise-kinematic/`

Texture skins use their own stable `skinId` and revision while pointing to
`aegis-tortoise-ga-002`. A new palette or material language does not create a
new model. Any silhouette, hardware, UV, pivot, hierarchy, clip, camera, or
team-emitter change requires a new geometry-authority revision.

## Authority contract

The neutral model has 38 rigid meshes, 30,858 triangles, 14 semantic clips,
one shared provider atlas, no armature, no skin, and no shape keys. The frozen
atlas has only the bounded numerical micro-exceptions recorded by transplant
schema v2; new authorities should still target a genuinely non-overlapping
atlas.
The texture upload is static, material-neutral, UV-identical, and contains no
animation. Every skin must start independently from that same upload; never
retexture one skin to create another.

Bulwark remains a very low levitating bot. The four corner modules are
hover/lock stabilizers, not feet or walking limbs. Skin materials must not
introduce wheels, tracks, landing gear, feet, or other locomotion cues.

Eight `TeamAccent.*` meshes remain on the exact same texture-free
`NB_TeamAccent_Semantic` material for every skin. Meshy sees those surfaces
only in the neutral static upload; its treatment is discarded during donor
transplant. The renderer owns blue/orange team tint, emission strength, bloom,
floor glow, and hover underglow.

See `authority.json` for pinned hashes and `skin-candidates.json` for the first
four deliberately different material directions. `validation/` and `proofs/`
preserve the accepted structural and visual evidence.
