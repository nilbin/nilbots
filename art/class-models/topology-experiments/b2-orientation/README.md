# B2 deterministic orientation correction

Meshy T2 returned the approved B2 Turret cross on the X/Z plane instead of the
Nilbots +Z-up ground plane. This directory records the review-only correction.
It is an axis fix, not a concept or geometry edit.

`normalize_b2_orientation.py`:

- accepts only the frozen B2 raw GLB with SHA-256
  `6fd79e66b0211241c2ef1e1f6b048634d39074f81cd1e4b4a535e97629b7e05f`;
- applies the inspected right-handed `-90` degree rotation around +X;
- centers the X/Y planform and floors the minimum Z without changing scale;
- writes the transform as the sole glTF mesh-node matrix;
- proves that the binary chunk, local geometry, UVs, materials, and PBR
  textures remain unchanged;
- refuses to overwrite an existing output directory.

The corrected GLB SHA-256 is
`8d410d9d335091e3d304dd3cfd4554fd46f3f1e0112f402b2918be081cc0497e`.
Two independent runs produced byte-identical GLBs and reports. `report.json`
contains the source/corrected bounds, fingerprints, document counts, topology
metrics, and fixed-view hashes.

The five files in `review/` were rendered by the unchanged frozen topology
audit after correction. The top view now contains the approved cross and the
side view contains its edge profile. This remains a non-shipping
default-fidelity candidate until semantic grouping, team material, native
runtime, clearance, and budget gates pass.

Run with the repository-pinned Blender 5.2 executable and a new ignored output
directory:

```sh
BLENDER=/Users/sebastian.lind/hobby-projects/nilbots/var/toolchains/Blender-5.2.0.app/Contents/MacOS/Blender
RAW=$PWD/var/class-models/topology-experiments/meshy/019fb30c-b8e4-7da7-9103-d1f49af0d585/raw-model.glb
OUT=$PWD/var/class-models/topology-experiments/b2-orientation/<new-run>

"$BLENDER" --background --factory-startup \
  --python art/class-models/topology-experiments/b2-orientation/normalize_b2_orientation.py \
  -- --input "$RAW" --output-dir "$OUT"
```
