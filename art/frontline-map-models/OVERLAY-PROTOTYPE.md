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
- contested presentation is derived from active units whose normalized
  presentation says `holdingObjective`, grouped by team.

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
- a sole present/claiming team applies its runtime accent;
- contested presence becomes a pulsing warm neutral signal rather than
  pretending either team owns it.

The hex signals remain legible under bots and projectiles without reading as
raised collision.

## Evidence

- `review/runtime/frontline-spawn-capture-overlays-v1.png` — actual replay,
  active team claim, focused Fable camera.
- `review/runtime/frontline-spawn-capture-overlays-full-arena-v1.png` — actual
  replay, full-arena Fable camera, both authored protected pads and the
  Frontline lane visible.

Focused tests in `web/tests/arenaActors3d.test.ts` verify:

- all authored positions and both authored home pads are materialized;
- pad tint is supplied by presentation rather than a baked map asset;
- two-team objective occupancy resolves to `contested`;
- zero-tick prefixes do not invent an active objective.
