# Woodland Grove

Woodland Grove is the Arc Relay deep-warren presentation for
`arc-relay-ambush-warren-06`. It does not change the map contract or any ASCII
tile. Walkable cells use one continuous quiet moss/grass field; blocked cells
retain separate ancient-perimeter and young-cover families.

## 3D mechanism

The optional `geometry3d.treeCover` family block turns `WallLayout` blockers
into deterministic instanced trunks, root flares, branches, uneven crown
lobes, and low understory. Stable family/tile/topology hashes vary height,
width, crown count, colour, rotation, and root position. Every cluster remains
inside the reviewed live-body clearance envelope.

Living cover deliberately skips the ordinary extruded wall and topology cap.
Instead, one flush, continuous, rounded foliage field is traced per connected
family beneath the clusters. That preserves immediate blocker legibility
without exposing the map as raised brown planting beds or stamped cubes.
Perimeter growth is darker, taller, and broader; interior growth is lighter,
lower, and airier. A theme-owned 14° camera yaw adds a restrained three-quarter
view while compensated framing keeps the arena in bounds. Canvas2D remains a
functional fallback, but this revision's visual review target is WebGL.

## Source and provider route

The three revision-2 raster sources were edited with Codex's built-in image
tool. Exact prompts are in `SOURCE-PROMPTS.md`; hashes are in
`PROVENANCE.json`. Meshy was not called (0 credits): repeated tree cover is
stronger and cheaper as instanced procedural geometry subordinate to the tile
contract. `art/themes/provider-runs/.gitignore` keeps any future raw provider
masters and texture dumps out of Git.

## Rebuild

```sh
sandbox/theme-art-venv/bin/python scripts/build-theme-art.py \
  art/themes/woodland-grove/art.json
node scripts/build-theme-3d-materials.mjs \
  art/themes/woodland-grove/art.json
```

## Budget and gameplay review

The deterministic bake reports `2,077,574 / 5,000,000` bytes for the
canonical runtime theme: 41.55% used, with 2,922,426 bytes of headroom. The
four lazy WebGL PBR maps add 490,788 bytes; even counting them with the
canonical files gives 2,568,362 bytes, 2,431,638 below the unchanged cap.

Six real `arc-relay-ambush-warren-06` matches use the standard exhibition
cells (`w-9001`, `w-9002`, `e-9005`, `e-9009`, `w-9012`, `e-9004`). Every
replay header records `themeId: woodland-grove`. The current desktop frame
uses sample 01 during live play at gameplay zoom:

- `review/ambush-warren-gameplay-webgl.webp` — revision-2 WebGL with the 14°
  yaw, flush foliage fields, and varied procedural tree clusters.

The prior Canvas2D frame is retained only as fallback history; this revision
was judged in 3D. Bodies, team accents, labels, projectiles, wells, reactors,
and combat overlays remain readable over the subdued floor.
