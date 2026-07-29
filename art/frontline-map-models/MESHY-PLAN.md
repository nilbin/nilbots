# Meshy map-kit experiment plan

## Budget ledger

- Hard ceiling: 10 paid Meshy calls and 300 credits.
- Calls used: **1 / 10**
- Credits used: **30 / 300**
- Current task: perimeter straight
  `019fb015-f431-7462-9ed2-36ba06a0e2c2` (`SUCCEEDED`, 30 credits).

The pilot is intentionally stopped at one call. The result proved useful
depth and PBR generation, but failed the approved landmark/detail hierarchy
and runtime-density gates. The remaining **9 calls / 270 credits are reserved
for bot/projectile work and are no longer authorized for this map pilot**.
There will be no cover call, A/B, or other map-provider request. See the run's
`RUN.md` and `review/provider/perimeter-straight-meshy-v1-board.png`.

The ceiling is enforced for this map pilot independently of any bot-model
work. A failed provider task still counts against the call ceiling and any
credits reported by the task.

## Why the map is not one task

Meshy receives one isolated object per task. A whole arena would freeze one
layout, spread detail across unrelated pieces, and make future map tuning
require a remodel. It would also duplicate a topology contract already owned
by map JSON.

The first approved experiment therefore models only:

1. one one-tile-wide perimeter straight module;
2. one one-tile-wide interior-cover straight module.

A corner/end/junction is added only if the two straight modules pass the
gameplay-camera gate and procedural derivation is visibly insufficient.
The floor stays the existing continuous Ember Forge material/PBR family; it
does not need an object-generation task.

## Input gate per object

Each paid task requires four separately approved images of the same isolated
module:

- front or gameplay-oblique;
- strict side;
- rear;
- top.

Every view is at least 1040x1040, uses the same scale, proportions, material
placement, neutral lighting, and plain/transparent background, and contains
one object with no floor, cast shadow, connector prop, or scene. A full concept
sheet is not passed as one image.

The top view pins the footprint; the side view pins wall height and shoulder
profile; the rear view prevents Meshy from inventing an unusable back face.

## First-call parameters

```json
{
  "ai_model": "meshy-6",
  "should_texture": true,
  "enable_pbr": true,
  "texture_resolution": "4k",
  "should_remesh": false,
  "pose_mode": "",
  "image_enhancement": false,
  "remove_lighting": true,
  "target_formats": ["glb"]
}
```

`symmetry_mode` is intentionally omitted because the current API marks it
deprecated. `image_enhancement` starts off for the new 1536-square, controlled
turnaround inputs: the adjacent Striker A/B found that enhancement softened
hard-surface forms without improving fidelity. An on A/B is justified only by
a concrete first-result defect. The text prompt guides texture, not geometry.
Geometry constraints must be visible and mutually consistent in all four
inputs.

Perimeter texture prompt:

```text
Blackened forged iron and graphite armor, broad weathered plates, restrained
aged-copper clamps and seam bands, sparse ember-orange heat wear only in
recesses. Preserve the exact material placement in the reference views.
Physically based metal/roughness/normal detail. No text, logo, floor, shadow,
glow, lava, flame, added prop, or new ornament.
```

Cover texture prompt:

```text
Matte graphite refractory-composite armor with restrained brushed-copper
cooling channels, sparse worn ochre ceramic service panels, shallow scuffs and
recessed vents. Preserve the exact material placement in the reference views.
Physically based metal/roughness/normal detail. No text, logo, floor, shadow,
glow, lava, flame, added prop, or new ornament.
```

## Call schedule

| Gate | Maximum additional calls | Cumulative credits |
| --- | ---: | ---: |
| Perimeter straight, first textured result | 1 | 30 |
| Cover straight, first textured result | 1 | 60 |
| One targeted A/B per failed straight | 2 | 120 |
| Corner or end only if straight modules pass | 2 | 180 |
| Retexture/remesh experiments only after geometry passes | 4 | 300 |

Stop early when the provider route is clearly better or clearly worse than a
deterministic procedural module. Unused calls are not a target.

The first result triggered that stop: it is a useful donor/benchmark, not a
runtime wall. Map-provider spend is closed at **1 call / 30 credits**.

## Review gate

For every result:

1. save the raw provider master and sanitized task response outside runtime;
2. report bounds, material/texture count, vertices, triangles, axes, and file
   size;
3. render neutral front/side/rear/top views;
4. render at the exact 58-degree gameplay camera with Ember Forge lighting;
5. place repeated instances in straight, end, and corner arrangements derived
   from the real Frontline topology;
6. reject any module that alters the apparent blocked footprint, creates
   visible seams when repeated, or makes cover/perimeter ambiguous;
7. compare against a parameterized procedural candidate before promotion.

Official Meshy guidance used for this plan:

- <https://help.meshy.ai/en/articles/16102789-meshy-multi-view-best-practices-angles-and-images>
- <https://help.meshy.ai/en/articles/12634481-how-to-use-multi-view>
- <https://docs.meshy.ai/en/api/multi-image-to-3d>
- <https://docs.meshy.ai/en/api/pricing>
