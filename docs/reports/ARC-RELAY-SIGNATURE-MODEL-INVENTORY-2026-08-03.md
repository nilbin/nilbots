# Arc Relay signature-model inventory — 2026-08-03

## Decision

The sixteen launch bodies already have approved GLB companions. No separate
signature-object GLBs ship today: WebGL builds every signature from a shared
pool of discs, rings, boxes, spheres, tetrahedra, and line segments in
`web/src/render3d/arcRelayEffects.ts`; Canvas2D draws the corresponding forms.

Four signatures justify a new physical prop model:

1. **Trip Node** — the persistent, destructible mine.
2. **Sentinel Seed** — the persistent, destructible sentry.
3. **Survey Flare** — a small projectile/beacon anchoring the high-frequency
   reveal field.
4. **Smoke Canister** — a small projectile/canister anchoring the
   high-frequency smoke field.

That is the pragmatic asset batch. The remaining signatures are beams, fields,
telegraphs, displacement, or hardlight geometry whose truth is clearer and
cheaper when renderer-owned. Falling Star gets one gameplay-scale procedural
shell trial before it earns a fifth GLB.

## Evidence and priority

The 12-match first-play audit counted 325 Survey Flare attempts, 128 Smoke
Canisters, 116 Repair Beams, 106 Null Fields, 101 Tractor Hooks, 98 Hardlight
Blocks, 84 Arc Tosses, 45 Prism Walls, 31 Target Paints, 7 Kinetic Bursts, and 6
Vector Dashes. The audit's table did not count the other five signatures, while
the exhaustive 600-tick presentation replay proves all sixteen can appear. Use
the counts as a polish priority, not as permission to omit a persistent
mechanical object.

This produces two complementary priorities:

- **Physical-read priority:** Trip Node and Sentinel Seed. Their current shared
  tetrahedron marker does not communicate mine versus armed gun, hull, or
  persistence.
- **Frequency priority:** Survey Flare and Smoke Canister. Their field effects
  already carry the rule, but a centre prop makes launch, travel, landing, and
  expiry one continuous object story. Repair Beam remains an effect-and-body
  polish task because there is no deployed object to model.

## Complete launch-signature inventory

| Class / signature | Authoritative read | Current WebGL read | Asset decision |
| --- | --- | --- | --- |
| Kestrel / Vector Dash | Public straight surge | Line and route markers | **No model.** Keep the rare signature simple; trail and stop flare are renderer motion. |
| Palisade / Prism Wall | Three projectile-blocking, body-passable segments | Generic translucent panels | **No GLB.** Replace the plain boxes with renderer-authored beveled/ribbed energy panels if needed; placement, contacts, cracks, and expiry remain state-driven. |
| Towline / Tractor Hook | Cable to the first pulled body | Line, target ring, generic latch marker | **No model.** The Towline body already owns the winch; cable, latch, and pull path are transient effects. |
| Patchbay / Repair Beam | Interrupted healing channel | Double beam and travelling particles | **No model.** Polish the priority beam and body emitter; no independent world object exists. |
| Lantern / Survey Flare | Travelling flare, then radius-four reveal | Area disc/ring, vertical beam, orbiting particles | **New model P1.** One flare dart that rotates into a compact landed beacon. Scan volume, revealed outlines, and exact expiry stay renderer-owned. |
| Mortar / Falling Star | Two-tick reticle, overhead shell, compact impact | Reticle cross/ring and vertical beam | **Procedural trial.** Add a tiny renderer shell plus shadow first. Only author a GLB if that silhouette survives real phone and desktop gameplay scale. |
| Minesmith / Trip Node | One hidden-until-near, hull-one proximity mine | Generic tetrahedron marker | **New model P0.** A low mine puck with a readable central sensor and three-prong/spike silhouette. Reveal, ownership, damage, replacement, and detonation stay renderer-owned. |
| Hush / Null Field | Radius-three suppression field | Dark disc/ring and orbiting particles | **No model.** It is a volume emitted by the existing chassis. |
| Relay / Arc Toss | The actual Core leaves the carrier and flies | Arc particles, landing ring, separately rendered Core | **No model.** The Core is already the physical luminous sphere; do not create a duplicate projectile. |
| Switchback / Exchange | Two bodies swap after a public link tell | Link lines and endpoint rings | **No model.** This is a relationship and motion event, not an object. |
| Longshot / Rail Line | Fixed-heading two-tick charge and shot | Connected line segments | **No model.** Telegraph and shot must remain heading- and timing-true. |
| Mason / Hardlight Block | One temporary hull-three tile obstacle | Generic translucent box panel | **No GLB.** Give the procedural block a wireframe-rise, solid core, health fractures, and timer rim; those states matter more than a baked mesh. |
| Sunder / Target Paint | Bounded marked target and remaining empowered hits | Target brackets | **No model.** The target body and breakable brackets are the whole read. |
| Repulsor / Kinetic Burst | One-tick radial displacement | Expanding ring | **No model.** Keep the rare signature simple. |
| Veil / Smoke Canister | Travelling canister, then exact radius-two vision blocker | Ring plus procedural smoke spheres | **New model P1.** One compact canister visible in flight and at the cloud centre. Smoke volume and sight boundary stay renderer-owned. |
| Nest / Sentinel Seed | One stationary hull-two sentry, range four, fires every three ticks | Stretched tetrahedron and ring | **New model P0.** A planted seed-pod sentry with unmistakable base, sensor, and short gun. Range, target line, muzzle event, hull, suppression, replacement, and expiry remain renderer-owned. |

## Asset contracts for the four-model batch

All four use the fleet's orientation contract: **+X is forward, +Y is up, the
floor contact is Y=0**, and the gallery must show the forward direction. Models
contain no baked team colour, glow, telegraph, smoke, range circle, or damage
state. Renderer materials may accent a named semantic light surface.

| Object | Gameplay footprint target | Sensible upper bound | Required silhouette |
| --- | --- | ---: | --- |
| Trip Node | about 0.44 tile wide and 0.12 tile high | 120 KB / 3k triangles | Low puck, three-prong or spike rhythm, central proximity eye; cannot resemble a loose Core. |
| Sentinel Seed | about 0.62 tile wide and 0.55 tile high | 250 KB / 6k triangles | Stable planted base, sensor head, short directional barrel; cannot resemble a full mobile bot. |
| Survey Flare | about 0.18 tile wide and 0.30 tile long | 80 KB / 2k triangles | Bright-ended dart that still reads when stood upright as a beacon. |
| Smoke Canister | about 0.15 tile wide and 0.26 tile long | 80 KB / 2k triangles | Blunt capsule with a distinct band/nozzle, readable in flight and lying on the floor. |

These are ceilings, not quality targets. Small props should spend geometry on
the outer silhouette, not hidden undercuts or texture detail. Reuse one loaded
geometry/material set per prop type; active instances must not duplicate texture
memory. Even at all four ceilings, the combined proposed batch is only about one
current bot model.

## Renderer and truth gates

- A model appears only when the signature is present in the published visible
  state. An unrevealed hostile Trip Node must never leak through asset loading,
  shadow, light, or selection.
- Anticipation starts and ends exactly on the authoritative signature phase.
  Model arrival, unfolding, firing, or disappearance cannot pre-roll a tick.
- Models stay inside their occupied tile and never hide its centre or adjacent
  bodies. No model-driven screen shake is added.
- Team colour is a renderer-owned ownership/readability layer. It is not baked
  into any prop and is not reused by the loose or in-flight Core.
- Each candidate is judged in a real replay at the fixed desktop overview and
  phone viewport, in WebGL and Canvas fallback. The model is rejected if it only
  reads in a showroom close-up.

## Arena objects outside this batch

Wells and reactors are already distinct procedural world objects with truthful
charge/integrity state. They could receive authored housings later, but no
mechanical read is currently missing. The Core should remain renderer geometry:
it is a luminous sphere whose loose and in-flight white/lilac palette is neutral,
while possession alone applies the carrier team's colour.
