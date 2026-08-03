# Arc Relay deterministic 3D fleet source

`fleet.json` is the deterministic fleet recipe for the sixteen launch-class
companions. Each entry binds an orthographic vector with five actual named part
groups: `underbody-locomotion`, `chassis`, `weapon-hardware`, `team-accents`,
and `emissives`. The vectors live under the premium roster's `vector-fallback/`
directory; the more detailed 20-degree raster masters and semantic masks remain
archived beside them as the Canvas2D/taste reference.

Run from the repository root:

```sh
node scripts/build-arc-relay-models.mjs
node scripts/build-arc-relay-models.mjs --check
```

The provider-free build rasterizes those vector groups at 256×256, samples them
onto a 48-cell planform, and gives each group its own beveled extrusion. Chassis
and signature hardware use separate distance-transform domes and separate
renderer pivots. The source drawing becomes the albedo, its physical relief
becomes the normal map, and the authored light group becomes the emissive map.
Semantic team paint is geometry/material-isolated and never baked to either
team colour.

The orthographic source choice is intentional. An earlier pass mapped the
premium 20-degree raster onto a horizontal 3D lid, then viewed it through the
58-degree gameplay camera. That projected and shaded an already projected image
a second time; increasing texture or mesh resolution could not correct the
result. The archived raster remains untouched and authoritative for Canvas2D,
while the GLB uses the coherent planform that the older renderer extrusion used
successfully.

The 3D bake also omits the vectors' `chassis-depth` and `hardware-depth` paint:
the meshes already supply those side faces. Wide silhouette strokes are reduced
more aggressively than fine panel seams, and sidewall UVs sample their actual
planform boundary instead of wrapping the full albedo around every edge. These
steps prevent baked ink, real extrusion, and contact shadow from becoming a
triple black outline.

Runtime output is `model.glb` and `model3d.json` beside each `arc-*` look. The
fleet ledger is `ledger.json` here. Do not hand-edit generated GLBs, manifests,
albedo/normal/emissive maps, or the ledger.
