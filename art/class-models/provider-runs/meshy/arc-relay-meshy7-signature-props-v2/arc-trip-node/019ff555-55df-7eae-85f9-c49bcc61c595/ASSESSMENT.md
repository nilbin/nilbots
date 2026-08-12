# Trip Node Meshy 7 multiview assessment

Task `019ff555-55df-7eae-85f9-c49bcc61c595` succeeded on 2026-08-12. It
consumed 30 credits, taking the account from 1430 to 1400. This raw result is a
generated base mesh and is not approved for runtime promotion.

## Preserved request

- Meshy 7 Multi-Image to 3D
- four separate 1536×1024 PNGs; strict front first
- 2K PBR textures
- remesh and image enhancement disabled
- lighting removal requested
- no pose mode, rigging, or extra output formats

`request-record.json` pins the ordered input hashes and sanitized request.
`task.sanitized.json`, `artifacts.json`, and `timing.json` record the provider
result, artifact hashes, timing, and credit ledger without credentials or input
data URIs. Raw GLB and lossless returned maps remain in the local provider
artifact store and are ignored as ordinary Git blobs.

## Raw result

- GLB: 74,653,544 bytes; SHA-256
  `56e227a630f61f6659bf3373b85e642f6d0e07c2f526e342260094b88e939868`
- one monolithic mesh, primitive, and material
- 1,308,891 uploaded vertices; 1,978,078 triangles
- four embedded 2048² JPEG maps: base color, metallic/roughness, normal, and
  emission
- source bounds: `[-0.876496, -0.340204, -0.953659]` to
  `[0.877240, 0.332136, 0.950357]`
- normalized six-view review: `provider-six-view-review.png`, 1,172,293 bytes;
  SHA-256 `03ff9f71e650db62a92c7fb35116148514ef710f42b5c79c7b29774499ae820e`

Khronos glTF Validator reports six `ACCESSOR_VECTOR3_NON_UNIT` errors caused by
zero-length normal entries, one generated-tangent-space portability warning,
and the provider's default node matrix as informational data. Tangents are not
authored even though the material uses a normal map.

`topology-audit.json` records 52,464 connected index sets, including 34,769
single-triangle sets. A second audit at a deterministic `1e-5` positional
tolerance collapses them to one continuous closed surface, proving the count is
seam duplication rather than floating debris. It finds two zero-area triangles
and two non-manifold spatial edges with maximum multiplicity eight. Repair those
locations without blindly welding intentional UV or hard-normal seams.

## Visual gate

Provisional geometry pass:

- the three-lobed forge-clamp planform, low planted body, central triangular
  aperture, and one forward wedge survived;
- no wheels, legs, turret, barrel, loose Core, or movement affordance appeared;
- the output is notably thicker, rounder, and more mechanically busy than the
  approved sheet, so silhouette overlay and gameplay-scale review remain
  required.
- after normalizing the planform span to 0.99, the raw height is about 0.350;
  a 0.44 live scale therefore reads about 0.155 tile high, still above the
  roughly 0.12-tile concept target and requiring local profile cleanup.

Material pass with correction required:

- oxide-terracotta remains dominant and graphite/brass structures are readable;
- the small saturated teal triangular lens is not authorized team paint and
  should become a restrained neutral sensor surface;
- bright panel highlights and the current roughness variation need a stricter
  matte floor under the arena lights.

## Proposed improvement gate

Do not spend more provider credits yet. If the owner approves the improvement
plan, retain this geometry as the source candidate and perform deterministic
normal repair, orientation/scale normalization, lean/rich topology tiers,
texture correction/compression, and fixed-camera plus real-replay review. Any
remaining shape drift after the top/side overlay goes to local geometry cleanup,
not an unbounded Meshy reroll.
