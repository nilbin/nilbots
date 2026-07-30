# Class-model topology experiments

This directory guards a small, evidence-driven comparison of production paths
for the Bulwark Turret. Nothing here is an approved runtime asset. Provider
output remains a base-mesh experiment until it passes Blender normalization,
runtime capture, visual review, technical validation, and an explicit decision
gate.

## Current result

Candidate A is the existing Standard Multi-Image Turret result. Candidate B is
a controlled T2 input-view experiment:

- **B1 oblique** used the owner-approved inferred-3D front-three-quarter view.
  Task `019fb2f9-5ef2-7641-a4e6-0f59134f5c31` succeeded for exactly 15
  credits. The owner likes the aesthetic result, so it is retained as a viable
  alternate or future store Turret skin candidate. Its raw provider document
  had one unnamed mesh and 206 welded connected components, so it was not
  animation-ready on arrival. The deterministic semantic-grouping experiment
  subsequently conserved every textured source triangle while partitioning
  the surface into one hub and four balanced rigid arms. Its animated GLB has
  a five-joint rigid skin plus embedded `Turret_Deploy` and `Turret_Recoil`
  clips, and passes Khronos validation with zero findings. See
  `t2-semantic-grouping/`.
- **B2 top** used the owner-preferred approved inferred-3D top view. Task
  `019fb30c-b8e4-7da7-9103-d1f49af0d585` succeeded for exactly 15 credits.
  It preserves the fourfold dorsal identity more closely than B1 and remains a
  strong default-fidelity candidate. Meshy returned it on the wrong up axis;
  `b2-orientation/` records the deterministic `-90` degree X-axis correction,
  proves that only the mesh-node matrix changed, and reproduces a correct
  gameplay view without changing geometry, UVs, materials, or PBR textures.

The experiment has used two calls and 30 credits. The third call remains
unspent and must test a separately named, approved question rather than
duplicating B1 or B2. Neither T2 result is wired into the store or runtime by
this branch. B1 proves that this type of loose provider surface can sometimes
be made rigid-animation-ready deterministically; it does not prove that every
future T2 result will have separable geometry.

The two pinned inputs are:

```text
B1 oblique
art/class-models/provider-inputs/meshy/aegis-tortoise-turret/clean-v1/03-front-three-quarter.png
SHA-256 404b12a82bf21a0b22f492bac2190ec4b5562cf1ae18a8baf23d4b204a029bb4
PNG 1254 x 1254 RGBA

B2 top
art/class-models/provider-inputs/meshy/aegis-tortoise-turret/clean-v1/01-top.png
SHA-256 c9ca639173a8744ff1c0475da451eca29ef1ea8828fd1399289fb0facd577ca2
PNG 1254 x 1254 RGBA
```

No alternate crop, concept sheet, unapproved generated angle, Shell, mobile
Bulwark, projectile, or multi-image package is accepted by this runner.

## Offline preflight

The default command validates the exact path, PNG header/dimensions, SHA-256,
fixed request, numeric budget ledger, and first-call gate. It does not read the
credential store, build a data URI, or contact Meshy:

```sh
python3 scripts/class-models/meshy_smart_topology.py --trial oblique
python3 scripts/class-models/meshy_smart_topology.py --trial top
```

`--trial` is mandatory for every new dry run or submission. It is omitted only
when resuming an existing recorded task; the runner infers old B1 records from
their immutable input path and hash.

The fixed T2 request is:

```json
{
  "model_type": "smart-topology",
  "ai_model": "meshy-t2",
  "target_polycount": 15000,
  "should_texture": true,
  "enable_pbr": true,
  "texture_resolution": "4k",
  "texture_prompt": "<exact existing Turret manifest prompt>",
  "target_formats": ["glb"]
}
```

The texture prompt is copied byte-for-byte from the existing Standard
Multi-Image Turret manifest so the first comparison changes model mode and
input count without also changing texture direction. `image_enhancement` and
`remove_lighting` are intentionally omitted because they are unsupported for
T2. They must not be added with false values.

## Paid and resumable paths

The two named trials have already succeeded. Do not resubmit either command;
the runner refuses duplicate B1 and B2 tasks even while experiment budget
remains. The original explicit B2 submission shape was:

```sh
python3 scripts/class-models/meshy_smart_topology.py \
  --trial top --submit --max-credits 15
```

The runner checks the numeric balance, creates at most one task, immediately
records its task ID and numeric credit ledger, polls strict known statuses, and
preserves the returned GLB, previews, and separately exposed PBR maps. A
recorded task can be resumed without another POST:

```sh
python3 scripts/class-models/meshy_smart_topology.py \
  --resume-run \
  art/class-models/topology-experiments/runs/meshy/<task-id>
```

Sanitized provenance and every returned provider preview are committed below
`runs/meshy/<task-id>/`. The raw GLB and every separately exposed PBR map are
written only to the globally ignored path
`var/class-models/topology-experiments/meshy/<task-id>/`; each
repository-relative location, byte count, and SHA-256 is committed. The
credential, Authorization header, input data URI, raw provider response, and
signed URL queries are never recorded.

Before deleting a worktree, preserve the ignored GLB and PBR maps in the
durable artifact store. Their presence in `var/` is not durable storage.

## Evaluation

Use identical top, side, oblique, and gameplay-camera captures for the control,
A, B1, and B2. Review neutral, red-team, and blue-team material states. The
next decision must consider:

- concept silhouette and actual side-profile depth;
- fourfold Turret identity, hub, cardinal arms, locks, and negative space;
- matte graphite/navy, bronze, gunmetal, wear, normals, roughness, and UVs;
- exactly four semantically isolated team inlays plus a separate class-energy
  reactor;
- topology/editability, pivots, fire/recoil anchors, deployment and cover
  clearance;
- GLB conformance, file/texture bytes, load-on-demand behavior, and frame cost.

See `PIPELINES.md` for the control and candidate A-E comparison. The ledger is
the machine-readable source of budget, call, stage, and gate state.

## Measured verdict

The promising production route is now concrete:

1. approve and hash the canonical 2D identity plus inferred-3D views;
2. use one named, guarded T2 trial as shape and PBR evidence;
3. measure axes and welded connectivity before assuming parts;
4. deterministically group only geometrically trustworthy components;
5. preserve the provider atlas and add removable semantic team/class overlays;
6. add named rigid pivots, a rigid skin, and restrained rule-honest clips;
7. require byte-stable GLBs/reports, Khronos validation, fixed views, and then
   the real renderer/team-color/native-replay/budget gates.

B1 passes through step 6 and the offline portion of step 7. B2 passes the
provider, axis-correction, and fixed-view gates but has not yet received the
same semantic grouping or animation treatment. Neither is a production asset
until the remaining native runtime gates pass.
