# Frontline 1.12-span visual-clearance audit

## Verdict

The approved Striker's 1.12-tile visual span is larger than the authoritative
one-tile corridor envelope before wall art is considered. The current 3D wall
bevel and cap make the visible overlap materially worse.

The least invasive universal relief is a renderer-only, topology-aware
setback of every wall face that borders open floor:

- inset the widest body face by `0.080` tile from the wall-cell boundary;
- crop the cap to the same open-edge inset;
- retain the current extent at edges connected to another wall, including
  connections between perimeter and cover families.

That produces a `0.580`-tile centreline clearance and a `+0.020` visual
margin for a radius-`0.560` chassis. Map JSON, tile centres, collision,
pathfinding, movement interpolation, spawns, pads, objectives, and replay
state stay unchanged.

Review image:
`review/clearance/frontline-striker-112-clearance-v1.png`.

## Exhaustive map audit

`scripts/audit-frontline-visual-clearance.mjs` evaluates the authoritative
`maps/experimental/frontline-01.json`, not a reconstructed image:

| Measurement | Result |
| --- | ---: |
| Map dimensions | 23 × 15 |
| Open / wall tiles | 233 / 112 |
| Legal cardinal centreline segments | 372 |
| Conservative no-corner-cut diagonal segments | 252 |
| One-tile corridor centres | 24 |
| Minimum centre or movement clearance to wall boundary | 0.500 |
| Approved Striker radius | 0.560 |
| Baseline signed visual margin | -0.060 |

Authored spawn and objective tiles reach the same `0.500` minimum. A
spawn-only or objective-only exception would therefore leave identical
clipping elsewhere.

## Current renderer contribution

The current body uses a `0.055` outward bevel. A geometry-bounds probe of the
real `ExtrudeGeometry` parameters measured a nominal `[1, 2]` wall cell at
`[0.945, 2.055]`; the open-floor clearance is therefore `0.445`, for a
`-0.115` chassis margin.

The topology cap's current span is:

```text
1 + 2 × (32 gutter px / 192 content px) - 2 × 0.055 = 1.223333
```

Its exposed outset is `0.111667`, leaving `0.388333` centreline clearance and
a `-0.171667` projected margin. The 58-degree perspective can make that upper
cap overlap appear more severe than the floor-plane measurement.

## Renderer implementation contract

For a visual radius `r` and desired safety `s`, the open-edge inset is:

```text
max(0, r + s - 0.5)
```

At `r = 0.56` and `s = 0.02`, the answer is `0.08`.

The production builder should consume the existing all-wall neighbour mask:

1. a face next to open floor is emitted at `cell boundary + 0.08`;
2. a face next to any wall family stays on the shared grid boundary;
3. the top cap uses the same rule per edge;
4. family materials remain separate, but geometry continuity is resolved
   before family styling;
5. selection, fog, projectiles, and actor transforms continue to use the
   unchanged world grid.

A global negative bevel offset is not sufficient. It can inset joins between
two different wall families and create false seams on future maps. Frontline
currently happens to have no cardinal perimeter-to-cover joins, but the
renderer contract must not depend on that map accident.

Scaling the world grid by 12 percent is also the wrong layer: it would require
coordinated changes to every actor, projectile, floor, wall, overlay, effect,
hit test, and camera mapping. Shrinking the approved bot would simply discard
the requested visual size.

The review proof uses the approved 1.12 Striker GLB read-only and does not copy
or promote it. It renders the real map and Ember Forge materials at the exact
58-degree camera. No Meshy call was made for clearance.
