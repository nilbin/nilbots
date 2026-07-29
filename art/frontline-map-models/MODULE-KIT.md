# Topology-preserving module breakdown

## Authority boundary

`maps/experimental/frontline-01.json` remains the only source of truth for
tile occupancy, collision, spawns, objective regions, and Frontline topology.
The renderer's existing `WallLayout` remains the shared presentation resolver:
it assigns a family and an eight-neighbour mask to each `#` tile.

The 3D kit consumes that result. It never stores a second layout and never
selects geometry by map ID.

## Canonical module classes

Cardinal neighbours determine physical silhouette. The diagonal bits in the
existing eight-neighbour mask choose cap/edge continuity and a deterministic
surface variant; they never change the occupied footprint.

| Cardinal neighbours | Canonical class | Required variants |
| --- | --- | --- |
| 0 | `obstacle` | isolated cover pod; optional perimeter pier only when authored as that family |
| 1 | `end` | one canonical mesh, four rotations |
| 2 opposite | `straight` | one canonical mesh, horizontal/vertical rotations |
| 2 adjacent | `corner` | one canonical mesh, four rotations |
| 3 | `junction-t` | one canonical mesh, four rotations |
| 4 | `junction-cross` | one canonical mesh |

The six classes are required separately for `perimeter` and `cover` because
family is semantic, not a material swap:

- **Perimeter** is taller, broader, and heavier: matte blackened iron, broad
  layered armor, radiused shoulders, sparse copper clamps, deep recessed
  vents, and low amber machinery.
- **Cover** is lower and visually lighter: tapered/waisted housings,
  bowed/faceted ends, sloped caps, cooling channels, and sparse dry ochre
  service ceramics.

One or two art variants per canonical class may break repetition. Variant
selection is a stable hash of the snapshotted map presentation, family,
coordinates, and mask. It may not use frame time, replay seed, or unpinned
randomness.

## Footprint and connector contract

Local coordinates use one gameplay tile:

- tile centre: `(0, 0, 0)`;
- floor: `Y = 0`;
- cell boundaries: `X/Z = ±0.5`;
- open-facing silhouette and bevel: no farther than `±0.47`, retaining a
  visible safety margin to the collision boundary;
- same-family connector: may meet exactly at `±0.5` only on the connected
  edge;
- no geometry, shadow-casting appendage, or decal with apparent height may
  extend into an open neighbour.

Initial gameplay-scale envelope:

| Family | Body height | Cap/profile variation | Edge radius |
| --- | ---: | ---: | ---: |
| Perimeter | 0.68–0.74 tile | at most 0.06 tile | 0.18–0.24 tile |
| Cover | 0.40–0.48 tile | at most 0.05 tile | 0.22–0.31 tile |

The renderer may use a hybrid: a continuous outline-derived substrate for
seam-free collision truth plus instanced caps, ribs, clamps, vents, and
service panels. That substrate is rebuilt from map data at load time; it is
not a stored whole-map mesh.

## Continuous floor

The floor is not a Meshy object and is never subdivided into gameplay-cell
meshes. The existing Ember Forge material is mapped once across the arena.
Future depth comes from its PBR normal/height/roughness treatment and
presentation-only flat overlays:

- dusty traffic polish;
- quenched blue/brown heat bloom;
- soot gradients near architecture;
- copper repair seams;
- shallow inset drains or grates.

Those overlays stay visibly flat and world-anchored. They do not trace the
gameplay grid or imply collision. Team cyan and red never appear in
environment materials.

## Deterministic resolution

```text
map JSON / replay map snapshot
  -> WallLayout.familyAt + WallLayout.maskAt
  -> canonicalize cardinal mask under rotation
  -> choose family + canonical module class
  -> choose stable art variant from family/x/y/mask
  -> append transform to that module's InstancedMesh
  -> render over one continuous floor material
```

Future map tuning changes only map JSON and its immutable version. The next
load resolves a new arrangement from the same kit; no DCC scene is opened and
no map-specific model is regenerated.

## Runtime review gate

1. Overlay the authoritative wall-cell mask during development and verify
   every module stays within it.
2. Compare open corridors and wall gaps against the current renderer at the
   same frame and 58-degree camera.
3. Exercise all six canonical classes even when the current Frontline map does
   not contain every class/family combination.
4. Review repeated straight runs for seams and obvious copy-stamping.
5. Verify perimeter and cover remain immediately distinguishable at gameplay
   scale.
6. Verify bots win silhouette, color, contrast, and emission against the map.
