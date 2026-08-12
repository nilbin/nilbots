# Arc Relay matte signature concepts v1

Status: **owner review**. These images are concept inputs only. They do not
authorize a Meshy request, change a runtime asset, or alter game mechanics.

Open `index.html` for the complete visual review sheet.

## Recommendation

Run exactly two Meshy 7 multi-image-to-3D tasks after owner approval:

1. **Trip Node** — replace the current glossy generic mine with the
   Minesmith-family three-lobed forge clamp in `trip-node-turnaround.png`.
2. **Sentinel Seed** — replace the current beige tank-like turret with the
   Nest-family planted seed-pod sentry in `sentinel-seed-turnaround.png`.

No other launch signature needs a standalone GLB in this pass. Standard
attacks and the travelling parts of Survey Flare, Falling Star, Arc Toss, and
Smoke Canister remain 2D masks / renderer geometry. Hardlight Block remains a
renderer-authored construct because its rise, hull fractures, timer, and
translucency carry more rule information than a baked mesh would.

## Concept contract

### Trip Node

- Parent family: Minesmith.
- Palette: oxide terracotta, muted mustard / dull brass, dark graphite.
- Silhouette: low three-lobed forge clamp, at least 4:1 width-to-height, one
  subtle `+X` forward wedge.
- Sensor: flat recessed triangular aperture with one tiny neutral slit.
- Live target: about `0.44` tile wide and `0.12` tile high.
- Renderer owns reveal, team ring, damage, detonation, and replacement.

### Sentinel Seed

- Parent family: Nest.
- Palette: pine-sage, warm umber, muted aubergine, charcoal, dull brass.
- Silhouette: angular planted calyx, exposed yaw collar, compact wedge head,
  very short emitter.
- The upper assembly must have honest unobstructed 360-degree yaw clearance.
- Live target: about `0.62` tile wide and `0.55` tile high.
- Renderer owns team ring, health, targeting, shot, suppression, and expiry.

Both concepts use the approved Arc fleet's high-roughness matte / satin
industrial finish, broad gameplay-scale masses, restrained wear, and no baked
team colour or gameplay effect.

## Complete launch-signature asset sheet

| Class / signature | Visual asset decision |
| --- | --- |
| Kestrel / Vector Dash | Renderer-authored surge, route and stop flare; no model. |
| Palisade / Prism Wall | Renderer-authored translucent energy segments, contacts and cracks; no model. |
| Towline / Tractor Hook | Renderer-authored cable, latch and pull path; no model. |
| Patchbay / Repair Beam | Renderer-authored channel and body emitter; no model. |
| Lantern / Survey Flare | 2D travelling silhouette plus renderer scan field; no GLB for now. |
| Mortar / Falling Star | 2D shell silhouette plus renderer reticle, shadow and impact; no GLB for now. |
| Minesmith / Trip Node | **New Meshy 7 model candidate in this package.** |
| Hush / Null Field | Renderer-authored suppression volume; no model. |
| Relay / Arc Toss | Existing Core geometry plus renderer trajectory and landing; no duplicate model. |
| Switchback / Exchange | Renderer-authored link and body motion; no model. |
| Longshot / Rail Line | Renderer-authored charge line and shot; no model. |
| Mason / Hardlight Block | Renderer-authored construct with rise, health fractures and timer; no model. |
| Sunder / Target Paint | Renderer-authored brackets and remaining-hit state; no model. |
| Repulsor / Kinetic Burst | Renderer-authored displacement ring; no model. |
| Veil / Smoke Canister | 2D travelling silhouette plus renderer smoke volume; no GLB for now. |
| Nest / Sentinel Seed | **New Meshy 7 model candidate in this package.** |

## Handoff after approval

Do not upload either contact sheet directly. Split each approved sheet into
its four canonical views, inspect consistency, and submit one object per Meshy
task. Before spending credits, re-check the live official Meshy API model,
multi-image endpoint, pricing, balance, and credit cap. Preserve request,
response, task, timing, source hashes, raw outputs, and provider previews under
`art/class-models/provider-runs/meshy/`.

Any returned mesh is a base candidate. It must still pass normalization,
orientation, one-tile bounds, gameplay-scale, performance, WebGL replay, and
Canvas fallback review before runtime promotion.

## Provenance

- Generated: 2026-08-12 with the built-in image-generation workflow.
- Exact prompts and ordered source references: `PROMPTS.md`.
- Machine-readable hashes and dimensions: `manifest.json`.
- Image-generator source copies remain under the local Codex generated-image
  store; the tracked PNGs in this directory are the project-bound review
  artifacts.
