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
  "landmarkRequirements": {"reactor": 2, "well": 3, "healing": 2},
  "requiredLandmarkIds": [
    "own-reactor", "enemy-reactor", "own-well", "centre-well",
    "enemy-well", "north-healing", "south-healing"
  ],
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

A cyclic map has no finite unconstrained "longest route." Name the commitment
that makes a long route finite, then express it with `via` or `authored`.

## Outputs and interpretation

`--json-out` writes measurements and validation details. `--html-out` writes a
self-contained interactive SVG atlas with every declared landmark and route
group. Append `?theme=light` or `?theme=dark` while reviewing both palettes. A
nonzero exit means the candidate failed validation.

Steps are terrain-only lower bounds: one open tile per tick under corner-strict
movement. They do not model bodies, combat, sight, deployments, or tactical
risk, and therefore are geometry evidence rather than balance evidence.
