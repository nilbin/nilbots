# Frontline spawn and capture overlay prototype

The runtime proof lives in `web/src/render3d/arenaOverlays.ts`. It is
presentation-only:

- protected spawn-pad footprints come from
  `replay.map.frontline.teamHomes[].protectedSpawnPad`;
- pad team identity comes from the tick presentation's team accent;
- every Frontline objective footprint comes from
  `replay.map.frontline.positions[]`;
- the active position, claimant, progress, and redeployment state come from
  `TickPresentation.objective`;
- exact ratchet ownership and clock state come from
  `holdOwnerTeamId` and `holdEndsAtTick`; the shared presenter derives only
  the remaining-tick display and the contract-declared 40-tick denominator;
- active capture pressure is resolved from the authored objective footprint,
  each live form's objective weight, and the replay's declared binary or
  net-control policy. Equal/non-positive pressure is contested; a 2:1
  net-control advantage keeps applying capture instead of being mislabelled
  as frozen.

No ownership, occupancy, collision, spawn reservation, or objective state is
written back. All geometry is flat below the fog plane, does not cast shadows,
and cannot imply a new obstacle.

## Visual language

Spawn pads use a dark inset service bed, an exposed-edge seal, and small hatch
marks. The underlying asset language is neutral bronze/graphite; the seal and
hatches receive renderer-owned team tint.

Capture fields use a dark recessed bed, one boundary around the authored
multi-tile footprint, and one small hexagonal signal per tile:

- inactive positions are faint neutral bronze landmarks;
- the active neutral position is stronger bronze;
- the authoritative claimant owns the inner progress arc and translucent
  footprint wash;
- a rules-resolved pressure team matching the claimant builds that arc;
- a different rules-resolved pressure team gets a short counter-rotating
  outer arc and the exterior boundary while the incumbent keeps its
  stored-progress arc. The challenger receives no filled credit until the
  authoritative claimant changes;
- policy-resolved contested pressure becomes a pulsing warm neutral signal
  rather than pretending either team owns it;
- a live ratchet uses the exact hold owner's runtime accent for the exterior
  boundary and whole-footprint pulse. A separate outer arc counts down from
  `ratchetHoldTicks` to `holdEndsAtTick`, so an early and late hold are
  materially different;
- Canvas and WebGL consume the same renderer-neutral
  `frontlineCaptureVisual` reading. Presentation accents are already
  contrast-corrected; Canvas deliberately does not correct them a second time.

The signals remain legible under bots and projectiles without reading as
raised collision. Progress is encoded by arc length, never opacity alone.

## Evidence

- `review/capture-visibility/frontline-capture-visibility-webgl-v1.png` —
  production WebGL at Fable's unchanged 58-degree camera.
- `review/capture-visibility/frontline-capture-visibility-canvas-v1.png` —
  forced production Canvas fallback from the same replay and exact ticks.
- `review/capture-visibility/frontline-capture-visibility-review-v1.json` —
  replay/viewer hashes, exact source states, reproduction command, capture
  hashes, and the approved-V4 lock proof.

Both boards use a fresh, complete, hash-verified native replay-v3 generated
from the frozen wave-5 `ledger-fly` and `spark-line` projects. The command
passed `--viewer` explicitly and produced the self-contained viewer recorded
in the ledger. The replay is not mutated for review. Its selected ticks cover:

| Reading | Native tick | Exact state |
| --- | ---: | --- |
| Neutral | 0 | no claimant, no hold, no objective occupant |
| Build | 15 | team 0, 5/15, sole team 0 |
| Contested | 8 | stored team 1 claim 1/15, both teams present |
| Weighted net control | 138 | team 0 weight 2:1 over team 1, both present, progresses 7→8 |
| Erosion | 110 | team 0 owns 4/15, sole team 1 erodes |
| Post-advance / early hold | 25 | position 3, team 0 hold, 40/40 left |
| Late hold + challenge | 64 | team 0 hold, 1/40 left, team 1 at 12/15 |

The earlier V4 overlay references remain useful as the pre-pass baseline:

- `review/runtime/frontline-spawn-capture-overlays-v1.png`;
- `review/runtime/frontline-spawn-capture-overlays-full-arena-v1.png`.

Focused tests in `web/tests/arenaActors3d.test.ts` verify:

- all authored positions and both authored home pads are materialized;
- pad tint is supplied by presentation rather than a baked map asset;
- neutral, build, incumbent erosion, two-team contest, post-advance, and live
  hold state are represented without changing inactive positions;
- exact hold owner/end fields reach the WebGL overlay;
- zero-tick prefixes do not invent an active objective.

`web/tests/frontlineCaptureVisual.test.ts` separately fixes the shared
renderer semantics, including early-versus-late hold fractions.
`web/tests/frontlineViewer.test.ts` fixes exact replay-v3 hold presentation and
proves weighted net-control presentation plus Canvas
neutral/build/erosion/contest/early-hold/late-hold frames remain distinct on
one fixed objective footprint.

## Approved V4 lock

This pass does not edit the map JSON, Ember theme/PBR package, wall topology,
wall details, scene construction, camera, collision, proportions, or spacing.
The evidence ledger compares those files to approved V4 merge
`572095091fc36e87385368a98aadbfdd28069838` and records every one unchanged.
The capture treatment is an additive flat renderer overlay only.
