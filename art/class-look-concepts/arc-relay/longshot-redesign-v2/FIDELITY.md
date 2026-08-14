# Railback fidelity brief

Status: owner-approved 2D identity; Meshy 7 multiview generation, corrective
retexture, and the owner-requested muted-blue palette retexture are complete
and ready for owner review. This is not approval to replace the live Longshot.

## Canonical identity

- Owner-selected direction: **Railback**, approved 2026-08-14.
- Primary identity reference: `railback-oblique.png`.
- Canonical East-facing planform: `railback-meshy7-v1/canonical-top-east.png`.
- Mechanical role: Longshot is a fragile, deliberate, fixed-heading corridor
  threat. Its normal attack visibly winds up and then emits a renderer-owned
  beam. Rail Line publicly charges for one tell tick and then pierces a fixed
  heading out to range 12 for 2 damage.
- Intended small-scale read: one broad, substantial high-energy accelerator
  with three pale transverse charge ribs and a dark full-width front shutter.
  It must read as wider and taller than the shipping narrow Longshot at normal
  phone zoom, without becoming an objective building.

## Required anchors

1. One connected, completely legless low-hover chassis; no detached pieces.
2. Broad compact hexagonal planform, strong front/rear asymmetry, and thick
   angled shoulder armor.
3. Exactly three oversized warm ceramic transverse capacitor ribs over one
   exposed central conductor bed. All three must survive strict top and the
   48-pixel gameplay proof.
4. One blunt full-width front emitter shutter, integrated into the front face.
   It is an aperture/seam, never a gun, barrel, cannon, muzzle, or separate
   weapon.
5. One squared split rear cooling block with a shallow center notch.
6. Meaningful vertical depth: raised ribs, recessed conductor machinery,
   layered shoulders, lower hull, and a visible hover gap from the gameplay
   camera.

## Materials and color regions

- Dominant hull: medium-light, desaturated dusty steel/denim blue, matte
  powder-coated metal.
- Capacitor ribs: warm light-gray matte ceramic.
- Conductors: dull brushed nickel.
- Recesses and lower mechanics: medium graphite, never near-black over large
  areas.
- Insulators: tiny muted burgundy details.
- Finish: mature, high-roughness industrial surface with restrained wear.
- Runtime team teal/cyan and orange remain renderer-owned. They are forbidden
  in the authored texture, along with baked team glow or beam light.

### Muted-blue review palette

The current review variant uses a new Meshy 7 text-prompt retexture rather
than the earlier renderer-side color lift. Its dominant hull is a
medium-light, dusty periwinkle steel blue: visibly blue against the graphite
arena, but deliberately desaturated, non-teal, and non-cyan. The exact
approved Railback geometry, normalized scale, three-rib silhouette, and
renderer-owned team treatment are unchanged. The optimized review asset has a
deterministic `0.72` roughness floor and remains review-only until the owner
separately approves runtime promotion.

## Geometry and gameplay-size gates

- Provider source views: near-orthographic, consistent scale and lighting,
  strict front + strict right side + strict rear + front-right three-quarter;
  first image is the primary front view as required by Meshy 7.
- Normalize the candidate to `+X` facing, `+Y` up, floor at `Y=0`, centered in
  planform, and `0.99` tile maximum planform span.
- Planform aspect ratio must be no more than `1.45:1` after normalization.
- The normalized transverse span must be at least `0.68` tile, with `0.72`
  tile the target. The shipping V3 Longshot's roughly `0.436`-tile transverse
  span is the explicit failure reference.
- The normalized visible body height must be at least `0.26` tile before the
  renderer's hover translation; target `0.30–0.36` tile.
- All transformed vertices at the runtime look scale (`1.10`) must be measured
  before clearance review. The authored `0.99`-tile maximum span becomes a
  `1.089`-tile live maximum span.
- Reject before texturing/retexture if the mesh misses width, height, facing,
  three-rib silhouette, full-width shutter, or rear-notch anchors.
- Final evidence must include strict provider views, a same-scale top
  silhouette overlay, a 48-pixel proof, and one real native replay at normal
  desktop and phone zoom with both team colors.

## Forbidden promises and cues

- No legs, feet, wheels, treads, rotors, wings, fins, cockpit, face, eyes,
  eye-like lights, humanoid anatomy, or player-controlled flight language.
- No gun, barrel, cannon, rifle, scope, ammunition, missile, separate weapon,
  white nose, universal charge orb, or baked projectile/beam/trail.
- No whole-hull teal/cyan or orange, baked team light, glossy plastic, chrome,
  wet varnish, candy paint, mirror reflections, text, labels, logos, or UI.
- Hover gap, tight shadow, small underbody wake, and gentle bob are
  presentation fantasy only; they do not promise altitude, banking, or
  vertical traversal.

## Approval boundary

The approved 2D Railback authorized provider experimentation. The generated
base mesh remains review-only. Retextures were used only after geometry passed
these independent gates. Runtime promotion requires a separate owner approval
after the muted-blue real-replay review.
