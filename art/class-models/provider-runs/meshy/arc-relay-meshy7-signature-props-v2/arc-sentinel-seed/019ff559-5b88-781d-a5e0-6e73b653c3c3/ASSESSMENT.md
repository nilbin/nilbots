# Sentinel Seed Meshy 7 multiview assessment

Task `019ff559-5b88-781d-a5e0-6e73b653c3c3` succeeded on 2026-08-12. It
consumed 30 credits, taking the account from 1400 to 1370. This raw result is a
generated base mesh and is not approved for runtime promotion.

## Preserved request

- Meshy 7 Multi-Image to 3D
- four separate 1536×1024 PNGs from the approved matte-v2 sheet; strict front
  first
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

- GLB: 63,566,880 bytes; SHA-256
  `05a752e236739004d36afb36248ac4f02bb5bcbd575b62d34cc4f411588367bb`
- one monolithic mesh, primitive, and material
- 1,011,617 uploaded vertices; 1,950,794 triangles
- four embedded 2048² JPEG maps: base color, metallic/roughness, normal, and
  emission
- source bounds: `[-0.946620, -0.646659, -0.950722]` to
  `[0.950393, 0.646383, 0.949964]`
- normalized six-view review: `provider-six-view-review.png`, 1,457,578 bytes;
  SHA-256 `69648461a87a13132b0ece4230ade5f83336e96f944ae1a6637d7457da47c99e`

Khronos glTF Validator reports no errors, one generated-tangent-space
portability warning, and the provider's default node matrix as informational
data. Tangents are not authored even though the material uses a normal map.

`topology-audit.json` records 508 connected index sets. A second audit at a
deterministic `1e-5` positional tolerance collapses them to one continuous
closed surface with no boundary edges, zero-area triangles, or non-manifold
spatial edges. The original count is therefore seam duplication, not floating
debris; intentional UV and hard-normal seams must remain controlled in the
optimized derivative.

## Visual gate

Provisional geometry pass:

- the planted calyx, compact wedge head, short rectangular emitter, exposed yaw
  collar, and clear sweep space survived all six diagnostic views;
- the result does not gain wheels, tracks, legs, a cockpit, or a long cannon;
- the upper body has enough visible clearance for renderer-owned authoritative
  yaw, but overlay and gameplay-scale review must still prove proportions.
- after normalizing the planform span to 0.99, the raw height is about 0.674;
  the current 0.95 renderer multiplier would make this candidate about 0.94
  tile wide, so its review must begin near 0.63 to hold the approved 0.62-tile
  footprint and then assess vertical proportions separately.

Material failure:

- Meshy replaced the approved pine-sage / warm-umber / muted-aubergine hierarchy
  with a dominant glossy orange-gold shell;
- that fixed orange collides directly with one semantic team color and no longer
  belongs to Nest;
- bright white highlight patches and the current roughness response violate the
  approved dry matte finish.

## Proposed improvement gate

Do not promote or reroll this geometry. If the owner approves the improvement
plan, use one bounded 10-credit Meshy 7 multiview retexture calibration with the
same approved four views as style authority and original UVs enabled. Accept it
only if the sage/umber/aubergine hierarchy and high-roughness finish return. Then
transplant the accepted maps onto repaired, optimized geometry so provider
re-export drift cannot become runtime authority. If that single calibration
fails, stop paid work and correct the textures locally or hand them to a
technical artist.
