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
selection is a stable hash of family, coordinates, topology mask, and detail
side. It may not use map ID, frame time, replay seed, or unpinned randomness.

## Footprint and connector contract

Local coordinates use one gameplay tile:

- tile centre: `(0, 0, 0)`;
- floor: `Y = 0`;
- cell boundaries: `X/Z = ±0.5`;
- open-facing final silhouette and bevel: no farther than `±0.35538`. That is
  the `0.14462` rotation-safe live-Striker relief from the cell boundary;
- the current outward `0.055` runtime bevel therefore starts from a source
  outline no farther than `±0.30038` (`0.14462 + 0.055` inset);
- same-family connector: the source outline and planar top may meet exactly at
  `±0.5` only on the connected edge;
- different-family connectors also retain the exact source/top grid boundary;
  the current outward bevel may visually overlap that plane by `0.055`;
- no geometry, shadow-casting appendage, or decal with apparent height may
  extend into an open neighbour.

The promoted topology cap is still a rectangular per-tile plane over rounded
family corners. Its straight open edges end on the supported planar top and
preserve the atlas rim, but a small diagonal corner overhang remains. Treat a
shaped or nine-slice cap as a follow-up rather than claiming that corner case
is solved.

Promoted gameplay-scale envelope:

| Family | Total height | Upper profile | Upper inset / chamfer | Edge radius |
| --- | ---: | ---: | ---: | ---: |
| Perimeter | 0.72 tile | 0.19 tile | 0.025 / 0.035 tile | 0.23 tile |
| Cover | 0.46 tile | 0.14 tile | 0.040 / 0.030 tile | 0.31 tile |

The renderer uses a hybrid: a continuous outline-derived substrate for
seam-free collision truth, a narrower rounded upper extrusion, topology caps,
and deterministic instanced/decal-like clamps, vents, and service panels.
Details remain inside the narrowest profile and do not purchase clearance by
changing gameplay. The substrate is rebuilt from map data at load time; it is
not a stored whole-map mesh.

## Continuous floor

The floor is not a Meshy object and is never subdivided into gameplay-cell
meshes. The existing Ember Forge material is mapped once across the arena.
The promoted WebGL treatment is deliberately dry and shallow: roughness
`0.95`, metalness `0.16`, and albedo-as-bump `0.045`. A stronger bump turns
baked rust, wear, and light into false physical relief. Future true
normal/roughness sources and presentation-only flat overlays may add:

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
7. Derive clearance from transformed model vertices and the live renderer
   scale. Do not use sprite width or model width divided by two when diagonal
   headings can expose a farther off-axis vertex.
