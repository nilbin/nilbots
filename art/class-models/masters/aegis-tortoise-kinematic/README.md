# Aegis Tortoise — deterministic kinematic family master

This directory is the durable editable authority for the deterministic
Bulwark Mobile, Shell, and Tower family. It contains the accepted
`v13-strong-localized-recoil` build. The existing
`masters/aegis-tortoise-mobile/` art master remains unchanged and is not
replaced by this archive.

The model has 31 meshes, 22,320 triangles, one authored non-overlapping UV
atlas, and `0.41786406` UV coverage. It has no armature, skin, shape keys,
cameras, or baked textures.

## Authority files

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `aegis-tortoise-kinematic-master.blend` | 416,396 | `fbe50d1f6e2d7056eacee02139b3e4ce38dd7a348806f044c4114c3a2d0dad37` |
| `aegis-tortoise-neutral-animated.glb` | 1,551,096 | `f5dc6b74a59946d3b4c88abb802168a766a1d65898f73c651ee42f61cdf4a0b6` |
| `aegis-tortoise-texture-upload.glb` | 1,466,812 | `f96a4cb61312e10f7f86ac73e092a3e0f341972c1d1bb8d97de0e2d31903b175` |
| `validation/validation-report.json` | 1,053,324 | `cb834fd7822b1f06ca2e3842e781f65783b7ac9aa6b750d096030a39f6eb956d` |

The animated and texture-upload GLBs have the same exact surface and UV
digest:
`4465811c2e4eb8704ed4000320176267c5f5a4939f745db93ab3c374b77f3c97`.

Blender records its save path in `.blend` files, so a regenerated editable
file is not expected to be byte-identical. The validation gates and exported
surface/UV digest are the reproduction authorities.

## Semantic clips

The neutral animated GLB exports exactly 14 rigid-object clips at 30 fps:

- `PodPlant_HoverSettle`
- `Shell_Deploy`
- `Tower_Deploy`
- `Mobile_Recoil`
- `Shell_Recoil`
- `Tower_Recoil`
- `Tower_Recoil_01` through `Tower_Recoil_08`

Every deploy endpoint is exactly reversible and every recoil returns to its
starting pose exactly. At the native 41px gameplay footprint, the accepted
recoil proofs change 509 Mobile pixels, 91 Shell pixels, and 148 Tower pixels,
up from the superseded v11 proof's 199, 36, and 5. All three settle with zero
changed pixels.

The collision ledger samples both deploy clips, the three renderer recoil
slots, and all eight heading-specific Tower recoil clips. Foreign moving-part
intersections are forbidden. A Tower barrel may telescope only inside its own
authored cradle, with a hard allowance of 56 additional BVH triangle pairs;
the accepted build peaks at 53.

## Texture and team-accent contract

`aegis-tortoise-texture-upload.glb` is the exact static provider input. It
contains the full surface and original UVs, one neutral material, and no
animations. It is byte-identical to the input of Meshy task
`019fb4bc-03d1-7142-9a37-7c3a8e2ffc34`.

`aegis-tortoise-neutral-animated.glb` is a pre-texture animation and semantic
contract proof, not the final runtime asset. A production derivative must
transplant the accepted provider textures onto the texture-eligible surfaces
while preserving the authored animation hierarchy.

Team colour remains renderer-owned. Four `TeamAccent.Shell.*.Emitter` meshes
use the texture-free `NB_TeamAccent_Semantic` material, cover `1.834944%` of
the fixed top projection, and remain excluded from provider material
replacement. The neutral, blue, and orange files under `proofs/` demonstrate
the same semantic surfaces; blue and orange are review renders only.

## Proofs and reproduction

`proofs/` contains the key gameplay stills, deploy and recoil contact sheets,
native 41px sheets, short GIF loops, UV layout, and team-accent proofs. The
complete machine-readable evidence is in
`validation/validation-report.json`.

Rebuild into a fresh repository-relative directory with the shared pinned
Blender 5.2 toolchain:

```sh
python3 scripts/class-models/build_bulwark_kinematic_master.py \
  --output-dir sandbox/bulwark-kinematic-master-<new>
```

The builder makes no provider call and refuses to overwrite an existing output
directory.
