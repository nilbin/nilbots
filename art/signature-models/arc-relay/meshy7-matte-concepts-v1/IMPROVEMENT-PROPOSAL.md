# Meshy 7 signature-prop improvement proposal

Status: **owner approval required**. The authorized two-task generation batch
is complete. This proposal authorizes no additional provider spend, runtime
promotion, or game-mechanics change by itself.

## First-pass evidence

| Asset | Raw result | Visual verdict |
| --- | --- | --- |
| Trip Node | 74,653,544 bytes; 1,978,078 triangles; 1,308,891 vertices | Geometry provisional pass; terracotta identity survived; too thick/busy, teal lens is unauthorized, and raw normals fail validation. |
| Sentinel Seed | 63,566,880 bytes; 1,950,794 triangles; 1,011,617 vertices | Geometry provisional pass with usable yaw clearance; material failure because sage/aubergine became glossy orange-gold. |

Both models are monolithic one-material PBR assets with four embedded 2K maps.
Neither is a runtime candidate. The current shipping signature props are only
about 0.51–0.59 MB and 13.6–15.7k triangles each, so the new raw masters need a
measured derivative route before any A/B in the game.

The initial indexed topology audit reports 52,464 connected index sets for Trip
Node and 508 for Sentinel Seed, but a deterministic `1e-5` positional audit
collapses each asset to one continuous closed surface. The apparent
fragmentation is therefore seam duplication, not floating debris. Sentinel has
no boundary, degenerate, or non-manifold spatial edges in that audit. Trip has
two zero-area triangles and two non-manifold spatial edges, in addition to its
six invalid normal entries. Cleanup can be surgical rather than a destructive
fragment-removal pass.

## Proposed work after approval

1. **Lock source authority and repair structure locally.** Preserve both raw
   masters and hashes. Normalize deterministically to `+X` facing, `+Y` up,
   centered planform, floor at `Y=0`, and a 0.99 authored planform span. Pin the
   actual generated facing before applying any yaw correction. Repair Trip
   Node's six zero-length normal entries, two zero-area triangles, and two
   non-manifold edge locations; retain Sentinel's clean closed topology. Generate
   portable tangents for both assets. Preserve intentional UV and hard-normal
   seams through retopology rather than blindly welding by position. Do not
   invent rig nodes or change mechanics.

2. **Run exact silhouette and proportion gates.** Render strict top, side,
   front, rear, both three-quarter views, and the 58° gameplay camera. Compare
   against the approved four-view sheets at the same scale. Preserve Trip
   Node's three lobes, low floor contact, triangular aperture, and forward wedge;
   preserve Sentinel's planted calyx, short emitter, compact head, and 360° yaw
   clearance. Any material issue cannot excuse a silhouette failure.

3. **Retune only the new props' presentation scale.** The normalized raw Trip
   is about 0.350 high at a 0.99 planform span; Sentinel is about 0.674 high.
   Reusing the current renderer multipliers (`0.60` / `0.95`) would make the new
   Sentinel about 0.94 tile wide. Start the new review at approximately `0.44`
   for Trip and `0.63` for Sentinel, which holds their approved live widths near
   0.44 and 0.62 tile. Trip must lose roughly 20–25% of its excess vertical bulk
   toward the approved 0.12-tile low profile. If Sentinel then reads too short
   against its roughly 0.55-tile height target, correct the base/head vertical
   proportions in the derivative master rather than enlarging its footprint.
   These are renderer/art transforms only; hitboxes and mechanics stay untouched.

4. **Correct Trip Node without another provider call.** Keep oxide terracotta,
   graphite, and dull brass, but neutralize the saturated teal triangle, enforce
   a dry roughness floor, remove specular hot spots, and suppress micro-detail
   that aliases at gameplay scale. Simplify the excess perimeter stacks and
   incidental hardware only where the overlays prove they thicken or muddy the
   approved three-lobed read.

5. **One bounded Sentinel retexture calibration.** Make exactly one Meshy 7
   multiview retexture call using this generated Sentinel as model source and
   the same approved matte-v2 four views as style authority. Use original UVs,
   2K PBR, GLB only, and a hard 10-credit cap. Accept only a pine-sage dominant
   hull with warm-umber rails, muted-aubergine cap, charcoal collar, dull brass,
   and high roughness—no fixed teal or vivid orange team color. If it fails,
   stop paid work and correct locally; no reroll chain.

6. **Preserve geometry authority across retexture.** Meshy retexture can
   re-export slightly changed geometry. Use its accepted maps only as a texture
   donor, transplanting them onto the corrected source geometry/UV contract.
   Record geometry fingerprints before and after.

7. **Build two optimized tiers from each corrected master.** Target a lean tier
   around 50–75k triangles with 512/256/256/128 KTX2 maps and a richer review
   tier around 100–150k triangles with at most 1K/512/512/128 maps. Each runtime
   GLB must remain below 1 MiB if gameplay evidence does not justify a budget
   exception; combined compressed-model GPU residency must remain below 2 MiB
   and RGBA8 fallback below 6 MiB. Keep one monolithic material and no baked
   team color.

8. **Review in the actual Tapline 3D renderer.** Install only reversible review
   assets. Compare current vs new props at identical replay, tick, viewport,
   camera, and renderer. Exercise both teams, multiple simultaneous mines and
   sentries, fog, suppression, damage, destruction, capacity pips, load failure,
   and Canvas fallback. Sentinel yaw may change only after an observed
   authoritative shot; Trip Node remains floor-locked.

9. **Promote only after a second owner review.** Deliver a gallery containing
   canonical concepts, current runtime props, normalized raw six-view sheets,
   lean/rich fixed-camera renders, topology/transfer ledgers, and pinned
   Tapline A/B captures. No runtime replacement occurs until that gallery is
   explicitly approved.

## Proposed additional spend

- Sentinel multiview retexture calibration: **10 credits maximum**.
- Trip Node: **0 additional credits**.
- Rerolls or other provider work: **not authorized**.

The account balance after the completed generation batch is 1370 credits.
