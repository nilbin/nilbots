# Deterministic Blender and provider prototype plan

Blender does not infer the map. It owns reproducible module construction,
cleanup, UVs, material segmentation, LODs, and export after an art direction
has passed concept review.

Blender was not installed in this environment, so the executable deterministic
proof is currently `procedural/proof.html`; it reads the real Frontline map,
builds continuous family substrates, instances presentation details, uses the
checked-in Ember Forge material helpers, and renders at the 58-degree gameplay
camera. It demonstrates the authority boundary without modifying the runtime
renderer.

## Blender source layout

```text
frontline-ember-forge-kit.blend
  KIT
    perimeter
      obstacle
      end
      straight
      corner
      junction-t
      junction-cross
    cover
      obstacle
      end
      straight
      corner
      junction-t
      junction-cross
  MATERIALS
    floor
    perimeter
    cover
    aged-copper
    dry-ochre-ceramic
    recessed-amber
  REVIEW
    module-lineup
    repeat-stress
    gameplay-camera
```

Every module origin is its tile centre at floor height, +Y is up, and its
canonical forward is +X. Connector planes are exact at `X/Z = ±0.5`.

## Parameterized construction

A Blender Python entry point or Geometry Nodes group owns these named
parameters:

```text
tile_size = 1.0
open_edge_visual_inset = 0.14462
open_edge_source_inset_for_outward_0_055_bevel = 0.19962
perimeter_height = 0.72
cover_height = 0.46
perimeter_radius = 0.23
cover_radius = 0.31
bevel_width = 0.055
cap_height_max = 0.06
variant_seed = 1
```

Construction sequence:

1. Start from exact canonical 2D footprint curves for obstacle, end, straight,
   corner, T, and cross.
2. Extrude the collision-faithful substrate.
3. Add swept/tapered shoulder profiles and bevels without crossing the final
   `0.14462` open-edge relief. If the bevel grows outward by `0.055`, build the
   source outline at the `0.19962` inset; validate final vertices, not the
   nominal curve.
4. Boolean only broad recessed vents and service channels; hairline detail
   belongs in normal/roughness maps.
5. Place clamps, ribs, caps, and ceramic panels from socket empties named by
   neighbour direction.
6. Select one of a small number of variants using an explicit integer seed.
7. Apply transforms, validate bounds/connectors, UV unwrap, and bake PBR maps.
8. Export a kit GLB with stable node/material names, then derive measured LOD
   tiers. Do not export a map scene.

The script must render a fixed module lineup and the actual Frontline topology
after every change. A change to parameters is reviewable as source diff plus
deterministic images.

## Provider comparison

Meshy receives isolated approved modules only, never the arena:

- one perimeter straight;
- one cover straight;
- a corner/end only after the straight results pass.

Four consistent views pin front/gameplay-oblique, side, rear, and top. The top
view pins the cell footprint and the rear prevents an inferred unusable back.
Geometry requirements must be visible in the images because Meshy's text
prompt guides texture, not shape.

Provider output enters Blender as a surface/shape candidate:

1. retain raw GLB and provider provenance outside runtime;
2. compare against the deterministic module at neutral and gameplay cameras;
3. reject if it violates footprint, connectors, family distinction, or repeat
   continuity;
4. if it wins visually, retopologize and rebuild exact connector/bounds
   deterministically in Blender;
5. segment matte graphite, bronze/copper, ceramic, and recessed amber into
   stable materials;
6. bake maps and export the same kit contract as the procedural route.

The Meshy mesh is never allowed to define collision or topology.

The live-bot clearance input is also measured, not inferred from a bounding
box width: transform every GLB vertex through its node hierarchy and runtime
look scale, then take the maximum XZ radius. Striker's current source radius is
`0.5881543316`; at live scale `1.062` it becomes `0.6246199002`, so a `0.02`
margin in a one-tile corridor requires the `0.14462` final relief above.

## Art hierarchy

Frontline and Striker share graphite, aged bronze, amber machinery, layered
armor, recessed vents, and swept/chamfered construction. The environment stays
rougher, more matte, coarser, lower-contrast, and less saturated. Bots retain
cleaner/finer panels, sharper silhouettes, stronger bounded emission, and all
team cyan/red. The same-world fit must not make the map a second hero asset.
