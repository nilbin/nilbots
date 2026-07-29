# Frontline 3D class-model vertical slice

No class or class-projectile 3D model is approved. Runtime look packages contain
no `model.glb` or `model3d.json`; WebGL intentionally uses the canonical SVG
extrusion fallback.

## Approved Striker inputs

The only approved 3D-direction material retained from the first pass is under
`concept-targets/`:

- `striker-oblique-target-v1.png`;
- `striker-model-sheet-v1.png`;
- pinned model-sheet SHA-256
  `7209a6afc3e58bcc37b2b5c710da167c8ee563656251e6de8fbe74c20305f683`;
- the canonical `trident-wasp/sprite.svg`, which remains authoritative where
  the generated reference differs.

## Rejected procedural evidence

The procedural Blender proof was rejected because its three-dimensional form
and visual quality did not meet the approved target. Increasing texture size
did not address that modeling gap.

| Measured proof | GLB bytes | Texture-source bytes |
| --- | ---: | ---: |
| Striker geometry only | 325,080 | 0 |
| Striker 256px PBR | 663,784 | 242,626 |
| Striker 512px PBR | 1,320,624 | 899,437 |

The proof used 5,940 triangles and six materials. A wider nine-look experiment
measured 396,388 bytes for the old lean baseline, 873,956 bytes for geometry
only, 1,468,708 bytes for the mid tier, and 3,255,816 bytes for the rich tier.
These numbers document the on-demand cost decision only; they are not visual
approval.

All generated GLBs, Blender files, textures, previews, build reports, and
procedural model generators from those failed experiments were removed so the
approach cannot be mistaken for a production pipeline.

## Next route

The next Striker attempt is an unproven multiview-AI-to-human vertical slice.
A service output is only a base mesh. A human modeler or technical artist must
correct form and silhouette, retopologize, author UV/PBR materials, isolate the
team-color material, set axes/pivots/scale, and pass the normal gameplay-camera
and fallback gates before any runtime companion is considered.

Striker's shallow `low-hover` is canonical presentation metadata in
`look.json`, independent of whether an approved GLB ever exists.
