# T2 turret semantic-grouping experiment

This is an art-side, non-shipping experiment on the owner-liked Meshy Smart
Topology T2 Aegis Tortoise turret result. It asks one narrow question:

> Can the untouched, high-fidelity T2 surface be partitioned into a stable
> central hub and four rigid radial arm groups by deterministic geometry rules,
> then animated without inferred cuts or texture loss?

The provider master remains outside this directory at:

```text
var/class-models/topology-experiments/meshy/
  019fb2f9-5ef2-7641-a4e6-0f59134f5c31/raw-model.glb
```

Its required SHA-256 is
`f9388c05e54bcf1603b7f901eefe0dedcaf7c636a384dcd039d4fe433e03dd1c`.
The script refuses any other input and verifies the hash again after every
build.

## Reproduce

Use the repository-pinned Blender 5.2 toolchain:

```sh
BLENDER=/Users/sebastian.lind/hobby-projects/nilbots/var/toolchains/Blender-5.2.0.app/Contents/MacOS/Blender
RAW=$PWD/var/class-models/topology-experiments/meshy/019fb2f9-5ef2-7641-a4e6-0f59134f5c31/raw-model.glb
OUT=$PWD/art/class-models/topology-experiments/t2-semantic-grouping/build-rerun

"$BLENDER" --background --factory-startup \
  --python art/class-models/topology-experiments/t2-semantic-grouping/build_t2_semantic_groups.py \
  -- --input "$RAW" --output-dir "$OUT"
```

The output directory must not already exist. This prevents a stale review
image or GLB from being mistaken for evidence from the current run.

## Geometric partition

The script does not ask an AI to identify parts and does not alter the provider
master.

1. Import the one provider mesh in Blender's DCC coordinates (`+Z` up).
2. Join indexed islands only for *classification* when their positions are
   coincident within `1e-6` world units. The source vertices are never welded.
3. Sort the resulting 206 components by face count, vertex count, and original
   root index.
4. Derive the planform centre from the source bounds.
5. Keep the central radius and the four cardinal corridors in `Hub`.
6. Assign an outer component to the diagonal sector containing its
   area-weighted centroid. Outer-vertex sector counts are retained as
   independent crossing evidence; cardinal-centred pods remain in `Hub`.
   Borderline and cross-sector components remain reported as ambiguous.
7. Copy the original triangles, UVs, material slots, and packed PBR images into
   five named mesh objects. No vertex, UV, atlas pixel, or source material is
   recolored.

The exact constants, component assignments, ambiguity reasons, and arm-root
pivots are written to `report.json`. A false-color top view makes the
partition inspectable rather than trusting its names.

## Reversible overlays and animation

The base grouped GLB contains only the five source-derived groups. The animated
candidate adds, as separate geometry:

- one narrow neutral strip on each arm, using a material whose glTF extras
  contain `nilbotsRole = "team-accent"`;
- one fixed cyan centre-energy ring using a separate
  `nilbotsRole = "class-energy"` material.

The provider atlas remains untouched. The strips can be removed without
changing any source-derived object.

When the grouping gate passes, a five-bone rigid armature gives every
source-derived vertex exactly one weight. `Turret_Deploy` is a small symmetric
in/out settle and `Turret_Recoil` is a short symmetric lock compression. The
clips do not rotate the whole form, scan, change authoritative facing, or
promise a new firing direction.

This result is not a runtime asset. It remains an alternate/store-skin
candidate until it passes renderer integration, team-color mutation, native
replay, gameplay-scale, transfer-budget, and form-event timing review.
