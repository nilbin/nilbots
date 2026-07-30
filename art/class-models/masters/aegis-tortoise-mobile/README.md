# Aegis Tortoise — accepted Bulwark mobile master

This directory is the accepted editable three-dimensional authority for the
default Bulwark mobile form. New Bulwark mobile avatars and site art should be
derived from this master rather than used as inputs to rebuild it.

The model is the accepted Meshy Smart geometry from task
`019fb42a-e3ed-7f18-a54e-795edf685815`, retextured by task
`019fb455-88da-751a-8bb4-f4f40301c8d2`, then deterministically split into nine
semantic rigid groups in Blender. Four restrained emitter bars were added as
separate `team-accent` surfaces:

- two forward shield strips;
- two outer pod strips.

The accent surfaces cover `2.117523%` of the body in the fixed top projection.
They stay neutral in the GLB and are recoloured by the renderer; the blue and
orange files under `proofs/` are review renders only.

## Source authority

- `aegis-tortoise-mobile.blend` is the editable 2K PBR art master.
- The master contains the `PodPlant_HoverSettle` and `GunRecoil` clips.
- `art-top-avatar.png` is the transparent, East-facing 2D derivative.
- `art-top-team-mask.png` is the separate semantic team-colour mask.
- `validation/semantic-rig-report.json` proves exact triangle, UV, PBR, pivot,
  and clip preservation from the accepted provider texture.
- `validation/team-accent-report.json` proves the neutral accent material,
  parent pivots, projected-area gate, and exported GLB structure.

The tracked Blender file has SHA-256
`be2ccebf9fc2fc5165e3a6297b5668eb2c1ee78b713e926e765ca9df9bc68027`.
Blender records the save path inside `.blend` files, so a regenerated copy is
not expected to be byte-identical. The exported accented source GLB is
byte-identical across independent runs:
`e2ef237f026a4198f8c91e490b2dcd0a1d3ae1f59cad43dac85cf7de46fe503c`.

## Runtime decision

`validation/runtime-tiers-report.json` compares the same geometry and material
contract with 2K, 1K, and 512 PBR maps. The 512 tier is the runtime choice:

- `13,091` triangles, including `112` accent triangles;
- `685,744` bytes;
- SHA-256
  `0d083a34c6d5a947882273dce0b7d18d10364bdf9e36f437c0c94e7f2b4d5c8c`;
- all four PBR maps and all nine semantic body groups retained;
- byte-identical GLB across independent packaging runs;
- visually equivalent to the 2K source at the measured 41px and 140px
  gameplay sizes.

Runtime derivatives intentionally omit Blender animation clips. Nilbots
currently applies replay-time hover and stance motion in the renderer, while
the editable clips remain available for a future authored-animation runtime.

## Scope boundary

This master is the Bulwark **mobile** body. Aegis Shell and Tower remain
separate planted forms and are not inferred transformations of this mesh.
Their current renderer fallbacks stay authoritative until separate add-on kits
pass the same concept, gameplay-scale, team-accent, and deterministic-export
gates.
