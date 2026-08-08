# Woodland Grove

Woodland Grove is the Arc Relay deep-warren presentation for
`arc-relay-ambush-warren-06`. It does not change the map contract or any ASCII
tile. Walkable cells use one continuous grass/undergrowth field; blocked cells
retain separate ancient-perimeter and young-cover topology families.

## 3D mechanism

The existing renderer supported continuous wall solids, profiles, sparse
service detail, and lazy material maps, but no theme-owned scenery geometry.
Giving those solids bark textures would have made walls pretend to be trees.
The minimal extension is the optional `geometry3d.treeCover` family block.
It produces deterministic instanced trunks, root flares, branches, and small
broken-up crown lobes from `WallLayout`. The atlas-backed solid remains only as
a low rounded root bed that makes every blocked footprint explicit.

Perimeter trees are darker, taller, wider, and rooted on a 0.30-tile bed.
Interior cover uses warmer young bark, smaller crowns, and a 0.25-tile bed.
All crown geometry remains inside `0.5 - WALL_OPEN_EDGE_INSET` of its tile
centre. Total declared height is at most 1.05 tiles; this theme uses 1.00 for
perimeter and 0.68 for cover. The compact crowns were chosen over broad forest
canopies because bodies and projectile paths must win every visibility trade.

## Source and provider route

The three canonical raster sources were generated with Codex's built-in image
tool. Exact prompts are in `SOURCE-PROMPTS.md`; hashes are in
`PROVENANCE.json`. Meshy was not called (0 credits): repeated tree cover is
stronger and cheaper as instanced procedural geometry whose placement remains
exactly subordinate to the tile contract. `art/themes/provider-runs/.gitignore`
keeps any future raw provider masters and texture dumps out of Git.

## Rebuild

```sh
sandbox/theme-art-venv/bin/python scripts/build-theme-art.py \
  art/themes/woodland-grove/art.json
node scripts/build-theme-3d-materials.mjs \
  art/themes/woodland-grove/art.json
```

## Budget and gameplay review

The deterministic bake reports `2,996,534 / 5,000,000` bytes for the
canonical runtime theme: 59.93% used, with 2,003,466 bytes of headroom. The
four lazy WebGL PBR maps add 458,546 bytes; even counting them with the
canonical files gives 3,455,080 bytes, 1,544,920 below the unchanged cap.

Six real `arc-relay-ambush-warren-06` matches were generated with the standard
exhibition cells (`w-9001`, `w-9002`, `e-9005`, `e-9009`, `w-9012`, `e-9004`).
Every replay header records `themeId: woodland-grove`. The reviewed desktop
frames below use sample 01 during live play at gameplay zoom:

- `review/ambush-warren-gameplay-webgl.webp` — WebGL, including the procedural
  trunk/root/branch/crown instances.
- `review/ambush-warren-gameplay-canvas2d.webp` — forced Canvas2D fallback,
  using the same family-specific floor/root atlas.

Both views keep bodies, team accents, labels, projectiles, wells, reactors,
and combat overlays readable. Perimeter trees remain the clearly taller,
darker family; the warmer interior trees stay compact enough that open lanes
and live-body space are never covered.
