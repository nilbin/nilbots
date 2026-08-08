# Woodland Grove

Woodland Grove is the Arc Relay deep-warren presentation for
`arc-relay-ambush-warren-06`. It does not change the map contract or any ASCII
tile. Walkable cells use one continuous quiet moss/grass field; blocked cells
retain separate ancient-perimeter and young-cover families.

## 3D mechanism

The optional `geometry3d.treeCover` family block turns `WallLayout` blockers
into deterministic instanced trunks, root flares, branches, uneven crown
lobes, conifer tiers, and low understory. Stable family/tile/topology hashes
vary height, width, lean, crown count, colour, rotation, and root position.
Blocked cells carry zero to two roots instead of one centred tree per tile, so
gaps and overlaps break the grid without changing collision. Roots stay in
authoritative `#` cells; decorative foliage may overhang their cells.

Living cover deliberately skips the ordinary extruded wall and topology cap.
Instead, one flush, continuous, rounded foliage field is traced per connected
family beneath the clusters. That preserves immediate blocker legibility
without exposing the map as raised brown planting beds or stamped cubes.
Flat, irregular moss and understory tongues cross onto the floor side of the
perimeter seam, hiding its straight polygon edge without changing collision.
Perimeter growth is darker, taller, and broader, with a scattered 1.45-tile
forest continuing outward beyond the map. Its locally varying reach, feathered
outer density, and rounded corners prevent the boundary from resolving into a
sharp rectangle. Interior growth is lighter and airier; its much broader
height and width ranges mix low scrub with occasional tall, wide inner trees
instead of producing a uniform hedge. A theme-owned 22° yaw, 52° pitch, and
26° field of view provide the low-distortion three-quarter framing common to
MOBA play. Camera-space fitting keeps the requested action frame visible, and
fog distance scales with the lens. Canvas2D remains a functional fallback,
but this revision's visual review target is WebGL.

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

The deterministic bake reports `2,078,100 / 5,000,000` bytes for the
canonical runtime theme: 41.56% used, with 2,921,900 bytes of headroom. The
four lazy WebGL PBR maps add 490,788 bytes; even counting them with the
canonical files gives 2,568,888 bytes, 2,431,112 below the unchanged cap.

Six real `arc-relay-ambush-warren-06` matches use the standard exhibition
cells (`w-9001`, `w-9002`, `e-9005`, `e-9009`, `w-9012`, `e-9004`). Every
replay header records `themeId: woodland-grove`. The current desktop frame
uses sample 01 during live play in the strategic overview:

- `review/ambush-warren-gameplay-webgl.webp` — revision-3 WebGL with the MOBA
  camera, flush foliage fields, irregular rooted clusters, mixed tree forms,
  and the wider, irregular outward perimeter forest.

The prior Canvas2D frame is retained only as fallback history; this revision
was judged in 3D. Bodies, team accents, labels, projectiles, wells, reactors,
and combat overlays remain readable over the subdued floor.
