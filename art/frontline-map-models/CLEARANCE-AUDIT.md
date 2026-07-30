# Frontline live-size visual-clearance audit

## Verdict

The approved Striker GLB has a 1.12-tile source planform, but the live actor
does not render it at source scale. `arenaActors.ts` computes
`max(0.82, look.scale × 0.9)`; Trident Wasp's `look.scale = 1.18` therefore
produces a `1.062` model scale and an effective long-axis span of
`1.12 × 1.062 = 1.18944` tiles. Width divided by two is not a safe radius:
the farthest actual planform vertex is `0.5881543316` tile from the asset
origin, or `0.6246199002` live. That rotation-invariant radius covers
Frontline's diagonal headings and presentation yaw. It is larger than the
authoritative one-tile corridor envelope before wall art is considered. The
current 3D wall bevel and cap make the visible overlap materially worse.

The least invasive universal relief is a renderer-only, topology-aware
setback of every wall face that borders open floor:

- inset the widest body face by `0.14462` tile from the wall-cell boundary;
- keep the cap on the planar top source outline, `0.19962` tile from an open
  boundary before the outward `0.055` bevel;
- retain the current extent at edges connected to another wall, including
  connections between perimeter and cover families.

That produces a `0.64462`-tile centreline clearance and a `+0.020` static
visual margin for the measured rotation-safe chassis radius. Map JSON, tile
centres, collision, pathfinding, movement interpolation, spawns, pads,
objectives, and replay state stay unchanged.

Review image:
`review/clearance/frontline-striker-live-clearance-v2.png`.

The exact hosted-renderer before/after is the full map-pilot comparison
`review/runtime/frontline-runtime-walls-before-after-v1.png`: same Frontline
replay, Ember presentation, tick, viewport, whole-arena frame, and 58-degree
camera on both sides. It also includes the pilot's state-derived spawn/capture
overlays; use the separate clearance image above for the isolated wall setback
proof.

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
| Approved GLB source span | 1.120 |
| Approved GLB maximum source planform radius | 0.5881543316 |
| Live actor scale | 1.062 |
| Effective long-axis span / rotation-safe radius | 1.18944 / 0.62462 |
| Baseline signed visual margin | -0.12462 |

Authored spawn and objective tiles reach the same `0.500` minimum. A
spawn-only or objective-only exception would therefore leave identical
clipping elsewhere.

## Baseline renderer contribution

The pre-relief body used a `0.055` outward bevel. A geometry-bounds probe of
those real `ExtrudeGeometry` parameters measured a nominal `[1, 2]` wall cell at
`[0.945, 2.055]`; the open-floor clearance is therefore `0.445`, for a
`-0.17962` chassis margin.

The pre-relief topology cap span was:

```text
1 + 2 × (32 gutter px / 192 content px) - 2 × 0.055 = 1.223333
```

Its exposed outset is `0.111667`, leaving `0.388333` centreline clearance and
a `-0.236287` projected margin. The 58-degree perspective can make that upper
cap overlap appear more severe than the floor-plane measurement.

## Renderer implementation contract

For a visual radius `r` and desired safety `s`, the open-edge inset is:

```text
max(0, maxVertexPlanformRadius × liveScale + s - 0.5)
```

For Striker, `0.5881543316 × 1.062 + 0.02 - 0.5 = 0.1446199002`,
rounded to `0.14462`. Never substitute half the model's width: an off-axis
vertex can be farther from the pivot and Frontline supports diagonal facing.

The production builder should consume the existing all-wall neighbour mask:

1. a face next to open floor is emitted at `cell boundary + 0.14462`;
2. a source outline/top plane next to any wall family stays on the shared grid
   boundary; the widest outward bevel may overlap it by `0.055`;
3. the top cap ends on that supported planar outline at open edges and maps the
   atlas content-edge UV there, while same-family joins retain gutter overlap;
4. family materials remain separate, but geometry continuity is resolved
   before family styling;
5. selection, fog, projectiles, and actor transforms continue to use the
   unchanged world grid.

A global negative bevel offset is not sufficient. It can inset joins between
two different wall families and create false seams on future maps. Frontline
currently happens to have no cardinal perimeter-to-cover joins, but the
renderer contract must not depend on that map accident.

The current cap remains a rectangular per-tile plane over a rounded family
outline, so a small diagonal corner overhang remains possible even though the
straight-edge lip is gone. A shaped or nine-slice cap can remove that residual
later without changing the approved clearance contract.

Scaling the world grid by 12 percent is also the wrong layer: it would require
coordinated changes to every actor, projectile, floor, wall, overlay, effect,
hit test, and camera mapping. Shrinking the approved bot would simply discard
the requested visual size.

The review proof uses the approved 1.12-source-span Striker GLB read-only,
applies the real `1.062` actor scale, and does not copy or promote it. It
renders the real map and Ember Forge materials at the exact 58-degree camera.
No Meshy call was made for clearance.

## Promoted V4 profile

The runtime now applies this contract to both the continuous lower body and
the manifest-owned upper profile. Perimeter and cover upper extrusions add
their own inset and chamfer on open sides; topology caps use the same upper
profile extents and content-edge UV mapping. Deterministic service panels,
vents, and clamps stay coplanar with side faces or inside the narrowest upper
profile, so they do not consume the proved `0.020` live-Striker safety margin.

`review/runtime/frontline-runtime-v4-gameplay-views-v2.png` includes the
supported eight-tile, 58-degree close view used to inspect those shoulders and
open-edge gaps. The whole-arena and Canvas panels use the same native replay
tick and keep all gameplay authority unchanged.

## Cosmetic-motion boundary

The `0.14462` setback is deliberately a yaw-rotation-safe static model
contract. Actor-owned translation and out-of-plane rotation can exceed it:
idle lateral sway reaches roughly `0.0775` tile and firing recoil reaches
`0.14` tile before roll projection. Inflating every wall to contain every
animation extreme would require roughly a quarter-tile setback and would
visibly gut cover.

`arenaActors.ts` already cancels drift when any neighbouring tile is solid,
but idle sway and recoil are not currently wall-aware. The follow-up belongs
to actor presentation after the class-model branch lands: suppress or clamp
only the component that moves into an adjacent wall while keeping the
authoritative actor position unchanged. This wall slice does not edit that
overlapping file and does not silently shrink the approved Striker.
