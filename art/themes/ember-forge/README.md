# Ember Forge

Ember Forge is the active Arc Relay presentation, including the 31×27
`arc-relay-ambush-warren-06` arena. Presentation changes replay identity but do
not alter the rules fingerprint, map fingerprint, ASCII tiles, collision,
vision, wells, reactors, or lanes. Older Woodland Grove replays remain valid
and retain the theme ID written into their own headers.

## 3D living-bastion mechanism

The WebGL scene still traces the exact `WallLayout` into one continuous solid
per family. Ember's optional `geometry3d.bastionCover` layer adds sparse raised
shoulders, rectangular and round towers, paired unequal-height towers, sloped
face ribs, heat seams, ringed crucible crowns, and outside-map foundry supports
from stable family/coordinate/topology hashes. This gives a wall run multiple
widths and heights without replacing it with a frozen arena mesh or moving its
authoritative open edge.

In-map modules remain inside blocked footprints and the existing live-chassis
clearance. The perimeter alone receives a 1.8-tile outside-map foundation:
visible low matte apron bands, a raised outer rail, rounded corner plates,
irregular pylons, banded chimney stacks, recessed furnace mouths, and links make
the border read as a broad working foundry shell rather than an isolated row of
wall tiles. None of that surface is walkable or used by gameplay.

The base profiles are now 0.84 tiles for perimeter and 0.58 for cover, with
deterministic rises above them. Matte graphite modules and cooler neutral light
replace the prior brown/glossy response. Deep burgundy trim and a brighter but
sparse crimson tier now identify structure; orange-red emission is reserved for
functional vents, occasional live crucibles, and furnace cores. Emissive faces
are physically separated from their recesses so mobile depth precision cannot
make them flicker. The topology cap is darkened and partially blended so it
contributes panel rhythm without flattening every raised profile into a tan
rectangle. The floor remains one arena-wide material field, with quieter 0.028
bump and 0.98 roughness rather than per-tile repetition.

Ember Forge owns the same restrained MOBA camera family proven on Woodland:
12° yaw and 52° pitch, with a slightly wider 28° lens. Follow allows up to 9.5
tiles on the tight axis at a 60-CSS-pixel density; its complete selected render
still clears 63×63 CSS pixels. Director opens and idles on one local theater
rather than the whole board, while overview retains the approved 24-tile
three-Well strategic frame. The all-unit terrain fog applies to every base
wall, raised module, apron, and floor; selecting a bot narrows that mask to its
team. Ember owns a lighter 20% unseen-terrain darkness (the renderer default is
58%), preserving the foundry silhouette without changing which tiles are seen.

## Source and review

The existing canonical Ember material sources and deterministic bake remain in
`art.json` and `walls/`. This pass adds no generated whole-map image, provider
mesh, or new bitmap source. Crucibles, furnace mouths, and chimneys are
procedural instanced geometry; the four lazy wall normal/roughness maps remain
the only WebGL-only raster additions.

The current review images come from a fresh native Hunter-versus-Wellwright
Arc Relay exhibition. Sample 01 is seed 9001 on Ambush Warren, ends at tick 348,
and carries native `themeId: ember-forge`; its broadcast records canonical
replay hash
`9a40d1de8ee085ce41cb7ba14bd593d268841481dfb178b4f3ec5bc94de47938`.

- `review/ambush-warren-overview-webgl.webp` — the 24-tile three-Well overview
  under the unselected all-unit terrain union.
- `review/ambush-warren-director-webgl.webp` — unattended local-theater
  director framing with the raised cover hierarchy visible around the play.
- `review/ambush-warren-follow-webgl.webp` — selected Kestrel follow view with
  wider tactical context, stronger team pool, team fog, and the 63×63
  interaction-size contract intact.
- `review/ambush-warren-whole-arena-webgl.webp` — manual maximum zoom-out used
  only to audit the complete perimeter and outside-map foundation.

All four frames use the production hosted WebGL bundle at 1440×900 CSS pixels.
SwiftShader reported an active context, WebGL error 0, and no console or page
errors. Canvas2D remains a functional fallback but is not the visual target.
