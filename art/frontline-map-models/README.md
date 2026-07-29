# Frontline modular 3D kit pilot

This directory is a presentation-only experiment for the Ember Forge package
used by `maps/experimental/frontline-01.json`.

The map JSON remains authoritative for tiles, collision, spawns, objectives,
and presentation family tags. No image or model here is a map definition, and
no generated whole-map mesh is acceptable. A future renderer may only resolve
reusable floor, perimeter, and cover presentation from that existing data.

## Owner-approval concepts

| File | Direction | Status |
| --- | --- | --- |
| `concepts/frontline-ember-forge-modular-kit-v1.png` | Conservative clipped/chamfered hard-surface kit | Awaiting owner review |
| `concepts/frontline-ember-forge-cast-bastion-v2.png` | Stronger cast-foundry profile with radiused perimeter and cover ends | Awaiting owner review |
| `concepts/frontline-ember-forge-living-bastion-v3.png` | Curved, tapered, asymmetrical living-bastion kit with localized floor wear | Geometry direction provisionally approved; too glossy |
| `concepts/frontline-ember-forge-matte-living-bastion-v4.png` | Controlled matte-material correction of V3 | Selected for the provider/procedural pilot |

Both concepts are edits of
`review/current/frontline-current-arena.png`, captured from the real
Frontline replay viewer at Fable's current gameplay camera. The checked-in
Ember Forge floor, perimeter, and cover sources were separate visual
references. The generated images describe a reusable kit language only; small
layout drift inside a concept image is not implementation authority.

V3 responds to the owner's first review: the material direction was liked,
but V2 still read as too square. It keeps the same family split while pushing
curved/tapered profiles, asymmetric panel rhythms, service channels, and flat
localized floor wear further. The generated layout is still only a mood frame;
the actual implementation must derive exact footprints from map JSON.

The owner provisionally approved V3's geometry but rejected its glossy
response. V4 is a controlled material-only correction: dry matte charcoal and
blackened iron, worn exposed edges, restrained satin copper, dusty heat wear,
and emission confined to recessed vents and seams.

The current recommended direction is V4:

- one continuous blackened forged-steel floor with shallow PBR relief;
- a taller, heavier perimeter family with radiused shoulders and restrained
  copper clamps;
- a lower cover family with clipped/radiused ends, sloped shoulders, vents,
  cooling channels, and sparse worn ochre service panels.

## Hard acceptance gates

1. `frontline-01.json` and future map JSON remain the only layout authority.
2. Floor art is a continuous material field. Walkable floor receives no
   obstacle-looking displacement or props.
3. Perimeter and cover remain separate semantic families.
4. Runtime geometry is a reusable, topology-driven modular kit. Straight,
   end, corner, and junction selection is deterministic from neighbouring wall
   tiles and family tags. Map tuning requires no remodel or hand placement.
5. No whole-map Meshy task and no frozen whole-map GLB.
6. Every model stays within its authoritative wall-cell footprint even when
   its visible cap or bevel is rounded.
7. Provider and procedural candidates are reviewed at the real 58-degree
   gameplay camera before runtime promotion.
8. Unreviewed provider masters stay under `provider-runs/` and never enter a
   web build.
9. Spawn pads and Frontline capture fields derive their footprints and state
   from the normalized replay map/presentation. The environment asset is
   neutral; team tint is renderer-owned.

## Current verdict

V4 is the selected visual direction for the pilot, not approval to ship a
generated arena wholesale.

This branch now contains four independent proofs/audits:

- a topology-driven procedural wall/floor proof that changes no runtime
  renderer;
- a presentation-only renderer prototype for authored spawn pads and
  stateful Frontline capture fields, reviewed in Fable's unchanged 58-degree
  camera.
- an exhaustive 1.12-span Striker clearance audit and renderer-only
  open-edge-setback proof;
- a read-only camera-scale audit that keeps normal action follow distinct from
  explicit whole-arena Fit.

The map, collision, replay schema, and camera remain unchanged. Wall models
have not been promoted into the runtime.

The isolated perimeter-straight Meshy task succeeded for 30 credits. It
demonstrated genuine depth, coherent PBR maps, and repeatable ends, but drifted
from the approved broad plate/vent hierarchy into repetitive square-cell
detail and is far too dense to ship. It is retained as a donor/benchmark; the
pilot stopped at **1 call / 30 credits**. The exact request, hashes, metrics,
review board, verdict, and remaining budget are recorded under
`provider-runs/` and in `MESHY-PLAN.md`.

The only runtime code in this pilot is the flat, state-derived spawn/capture
overlay prototype. `CLEARANCE-AUDIT.md` specifies a future renderer-only wall
relief without changing gameplay authority. `CAMERA-SCALE-AUDIT.md` recommends
an 18-tile maximum normal-follow span once a semantic action anchor exists,
while preserving Fable's explicit full-arena Fit.
