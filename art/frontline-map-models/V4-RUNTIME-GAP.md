# Ember Forge V4 runtime transfer audit

## Verdict

`concepts/frontline-ember-forge-matte-living-bastion-v4.png` is approved
direction, not a map, texture source, or claim of pixel equality. The current
runtime is a deterministic V4-inspired interpretation over the authoritative
23×15 Frontline contract. It materially closes the legacy blue/cold,
flat-profile, glossy-material gap while preserving collision, topology,
overlays, camera, and Canvas fallback.

## Property transfer

| Approved V4 property | Runtime treatment | Status |
| --- | --- | --- |
| Dark graphite / blackened-steel floor | Existing canonical Ember floor, mapped once over the arena | Retained; no concept crop |
| Dry shallow floor response | WebGL `roughness 0.95`, `metalness 0.16`, albedo-as-bump `0.045` | Implemented |
| Taller layered perimeter | Continuous traced body plus `0.19`-tile inset/chamfered upper profile | Implemented |
| Lower tapered cover | Continuous traced body plus `0.14`-tile inset/chamfered upper profile | Implemented |
| Matte, warmer environment | Theme-owned key/ambient/fill colors and intensities | Implemented |
| Surface depth rather than baked-only light | Existing authored wall normal and roughness sources baked to four lazy WebP maps | Implemented |
| Recessed vents, clamps, sparse panels | Stable family/X/Y/mask/side placement inside the narrowest upper profile | Implemented at gameplay scale |
| Exact map tuning and cover truth | Existing `WallLayout`, tile occupancy, family tags, and `0.14462` open-edge setback | Preserved |
| Spawn and capture readability | Existing replay-derived, renderer-owned overlays | Preserved in WebGL and Canvas |
| Native theme/class handoff | Separate replay-only descriptor writes theme, wall families, chassis, and projectile IDs | Implemented without gameplay fingerprints |

The four renderer-only wall maps total **303,654 bytes**. They live under the
lazy WebGL import tree. They are not a new floor, are not a whole-map texture,
and are not copied into the single-file Canvas CLI viewer. A same-tip scoped
CLI build measured the following single-file deltas:

| Viewer | `c111f88` baseline | V4 candidate | Delta |
| --- | ---: | ---: | ---: |
| `control-room` | 5,001,376 B | 5,003,292 B | +1,916 B |
| `ember-forge` | 3,688,195 B | 3,690,921 B | +2,726 B |
| `frost-relay` | 4,153,473 B | 4,155,389 B | +1,916 B |
| `overgrown-lab` | 6,701,613 B | 6,703,529 B | +1,916 B |

The shared delta is schema/normalization code; Ember's additional 810 bytes
are its declarative 3D recipe. None of the 303,654 bytes of PBR source maps is
present in these Canvas-only files.

## Honest remaining gaps

- Topology caps are still rectangular atlas planes over rounded profiles. Open
  edges are supported and UV-correct, but a shaped or nine-slice cap would
  close the residual rounded-corner mismatch.
- The runtime does not reproduce every concept module's bespoke asymmetrical
  silhouette. It deliberately favors reusable continuous geometry so later
  map tuning remains data-only.
- The floor has no authored true normal/roughness pair. Its shallow
  albedo-as-bump treatment is intentionally conservative; a future floor PBR
  source should be reviewed as a separate material upgrade.
- Sparse procedural service detail transfers the V4 rhythm, not the exact
  placement in the concept image.
- Provider geometry remains an art-side donor/benchmark. No paid or
  whole-arena provider output entered runtime.

These gaps are visible follow-ups, not permission to use the concept crop,
change the map, or claim an exact concept match.

## Native replay proof

The final boards use durable source identity
`frontline-v4-native-cli-fabricator-vs-fabricator-seed-104729`:

- replay v3; map `frontline-labs-01-classes`, 23×15, 500 ticks;
- canonical replay hash
  `8d2153a991bae77bf6f7d56242c5e20eb217ab02d71acb44c6c51ee6cb5291cf`;
- source JSON SHA-256
  `534ffe61961c86d6d1e07c02b56b3f721ecb392f13fcec0e360e4a3bfe88da68`;
- native header presentation: `ember-forge`, `perimeter`, `cover`,
  `lattice-loom`, and `lattice-rivet`;
- `partial=false`; complete result present; canonical hash verified by the
  CLI;
- review tick 6 contains two active lives and two projectiles.

`review/runtime/frontline-runtime-v4-concept-autofit-v2.png` pins the concept,
legacy runtime, and new runtime at the same native replay, tick, viewport,
auto-fit, and 58-degree camera. The concept panel is explicitly reference-only.

`review/runtime/frontline-runtime-v4-gameplay-views-v2.png` proves the whole
arena, six spawn anchors, five capture positions, the supported eight-tile
team-follow frame, close wall profiles, and forced Canvas fallback.

Machine-readable provenance and capture metrics are in
`review/runtime/frontline-runtime-v4-review-v2.json`. The generated replay and
review builds remain ignored scratch data; the report records only
repository-relative paths, content hashes, and a sanitized command.
