# Analysis specification

Create a JSON specification and run it from the repository root. Coordinates
are `[x, y]`, with `(0, 0)` at the upper-left. Terrain rows must contain only
terrain; declare gameplay objects as landmarks.

## Complete example shape

```json
{
  "title": "Tapline route candidate",
  "note": "West-team perspective; candidate geometry only.",
  "map": {
    "builder": "scripts/arc-relay-tapline-build.py",
    "function": "build",
    "addWalls": [[9, 9]],
    "removeWalls": []
  },
  "symmetry": "rotate-180",
  "stubAllowance": 2,
  "validateCutVertices": true,
  "atlasRequirements": {"distances": true, "combatRanges": true},
  "landmarkRequirements": {"reactor": 2, "well": 3, "healing": 2},
  "requiredLandmarkIds": [
    "own-reactor", "enemy-reactor", "own-well", "centre-well",
    "enemy-well", "north-healing", "south-healing"
  ],
  "requiredRouteIds": ["own-direct"],
  "combatReference": {
    "origin": "own-well",
    "facing": "east",
    "source": "Active rules base profiles; upgrades and signatures excluded.",
    "vision": {
      "label": "Base vision",
      "range": 5,
      "shape": "facing-quadrant",
      "lineOfSight": "corner-strict-supercover",
      "pointBlankRange": 1
    },
    "attacks": [
      {
        "id": "short-fast", "label": "Short-fast", "style": "short-fast",
        "range": 3, "classes": ["Kestrel", "Mason"]
      },
      {
        "id": "medium-steady", "label": "Medium-steady",
        "style": "medium-steady", "range": 4, "classes": ["Towline"]
      },
      {
        "id": "long-slow", "label": "Long-slow", "style": "long-slow",
        "range": 6, "classes": ["Longshot"]
      }
    ]
  },
  "landmarks": [
    {
      "id": "own-well",
      "kind": "well",
      "name": "Own Well (1)",
      "label": "1",
      "position": [11, 6]
    }
  ],
  "routes": [
    {
      "id": "own-direct",
      "label": "Shortest return",
      "origin": "Own Well",
      "group": "Own Well · direct",
      "style": "direct",
      "from": "own-well",
      "to": "own-reactor",
      "mode": "shortest",
      "expectedSteps": 12,
      "labelAt": [7, 5]
    }
  ]
}
```

The checked-in `tapline-equal-return-candidate.json` is a runnable full
example and regression fixture.

For an owner-facing atlas, set both `atlasRequirements` flags to true. This
makes an empty route inventory, a route omitted from `requiredRouteIds`, an
unpinned `expectedSteps`, a missing combat reference, or fewer than three gun
profiles fail validation. Small analyzer unit fixtures may omit the flags.

## Map sources

Define exactly one source under `map`:

- `rows`: rectangular strings embedded in the specification.
- `rowsFile`: path to a text file containing rectangular terrain rows.
- `builder`: path to a Python file. `function` defaults to `build`; the
  function must return rectangular strings.

`map.wall` defaults to `#`. Apply a candidate delta with `addWalls` and
`removeWalls`. Relative paths resolve from the working directory first, then
from the specification directory.

## Landmarks

Each landmark needs a unique `id`, a `position`, and normally a gameplay
`kind`. `label` is the compact glyph drawn in its tile; `name` is the explicit
human-readable atlas label. Landmarks are protected from walls unless
`protected` is false. Use `landmarkRequirements` for exact kind counts and
`requiredLandmarkIds` for named completeness.

For three-Well Arc Relay maps, declare the perspective explicitly in `note`
and include `own-well`, `centre-well`, and `enemy-well`. Include both reactors
and both healing tiles as well. Do not substitute an old scoring-shoulder
annotation for the actual reactor scoring tile.

With `symmetry: "rotate-180"`, terrain and landmarks of each kind must have
180-degree partners. Set `symmetryExempt` only for an intentionally asymmetric
landmark.

## Routes

Every route has `from`, `to`, and one of these modes:

- `shortest`: exact corner-strict breadth-first search.
- `via`: concatenate shortest segments through ordered `via` landmark IDs or
  coordinates. Use this for committed gates or named corridor families.
- `authored`: validate the exact tile sequence in `path`.

Use `avoid` to block route-specific tiles. Routes reject loops unless
`allowRepeats` is true. Pin accepted geometry with `expectedSteps`; a mismatch
is a validation failure. `labelAt` controls the map's distance badge. Supported
route styles are `north`, `south`, `direct`, and `alternate`. The reported
ratio compares each route with the unconstrained shortest path between that
route's own endpoints.

List the complete intended route inventory in `requiredRouteIds`. A declared
ID that is absent from `routes` is a validation failure, just as a missing
`requiredLandmarkIds` entry is. Build this inventory by origin and purposeful
family before measuring; the analyzer cannot infer which tactical families the
designer intended to include.

A cyclic map has no finite unconstrained "longest route." Name the commitment
that makes a long route finite, then express it with `via` or `authored`.

## Combat reference

`combatReference.origin` is a landmark ID or coordinate and must be open.
`facing` is cardinal. Vision currently supports the Arc Relay
`facing-quadrant` shape and `corner-strict-supercover` line of sight;
`pointBlankRange` expresses the effective omnidirectional adjacent ring. The
atlas outlines the full geometric cone and shades the actually visible tiles
after wall occlusion, making range and practical sight distinguishable.
The origin and facing are reproducible initial values. In the generated atlas,
the reviewer can drag the shared vision/range marker to any open tile, rotate
it through cardinal facings, and reset it; wall-occluded vision recomputes in
the browser. These interactions do not alter the JSON specification or route
analysis.

Declare exactly three ordinary gun profiles with unique IDs, positive ranges,
and their class names. Keep signature attacks out of this list. The atlas draws
their ranges as nested square Chebyshev distance envelopes—the mechanically
exact radius contours for this engine—so a designer can compare the map scale
at a glance. Do not replace them with Euclidean circles unless the distance
rule changes. An envelope does not imply a legal shot to every
tile inside it; gun facing, allowed headings, projectile paths, and blockers
remain runtime questions.

## Outputs and interpretation

`--json-out` writes measurements and validation details. `--html-out` writes a
self-contained interactive SVG atlas with every declared landmark and route
group. Use that generated file as the authoritative viewer rather than copying
route data into a separate hand-authored page. Append `?theme=light` or
`?theme=dark` while reviewing both palettes. A nonzero exit means the candidate
failed validation.

Steps are terrain distance, not elapsed time. They do not model movement
throttles, bodies, combat, deployments, or tactical risk, and therefore are
geometry evidence rather than balance evidence. In current Arc Relay, an
unloaded body can normally move each tick while a Core carrier is gated by the
Core relocation interval (currently two ticks); verify the active profile when
turning a distance into a timing estimate.
