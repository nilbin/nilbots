# Exact built-in image-generation prompts

Generated on 2026-08-12 with OpenAI's built-in image-generation tool. The
generated model identifier is not exposed by the tool. The reference roles and
ordered local paths below are part of each prompt contract.

## Trip Node four-view concept

References:

1. `art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet-retexture-v3-multiview/minesmith/019ff0f5-8ee4-79fa-94d3-6a6cffbaa95f/provider-preview.png`
2. `art/reviews/arc-relay-meshy7-fleet-retexture-v3/fixed-orientation-sheet.png`

```text
Use case: stylized-concept
Asset type: high-resolution 2x2 orthographic game-prop concept sheet for a new Nilbots Arc Relay TRIP NODE, later usable as separate Meshy multi-image-to-3D references after owner approval.
Input images: Image 1 is a visual reference only for the current Minesmith parent’s mature matte hard-surface material language and restrained dusty brick-rose / muted brass / graphite family; do not copy the chassis. Image 2 is a visual reference only for the approved Arc Relay fleet’s broad readable shapes, worn matte finish, and restrained saturation; do not copy any existing bot or signature prop.
Primary request: invent one completely new, compact proximity trap whose silhouette reads instantly at 48 pixels and cannot be mistaken for a bot, turret, radial disc, or circular puck.
Subject/design: one extremely low broken-triangle floor trap made of three broad blunt trapezoidal clamp jaws arranged around a large clean triangular negative-space center. The three jaws define a strong open triangular outer contour rather than a circle. One jaw subtly points toward +X/front; it is a broad directional bevel, not a spike. Short exposed graphite flexure bridges connect the jaws near the corners. In the center recess, place one small flat mechanical pressure plate with no light and no raised lens. Use only very large masses and at most one broad seam per jaw. Width-to-height ratio at least 6:1. The object lies directly on the ground and stays compact within one tile.
Mechanic/fantasy: a patient route-tax trap placed on one predicted tile, hidden until near, destructible with hull one, rising-spike tell handled by the renderer. The resting model itself must not show detonation, spikes, ownership, range, or reveal state.
Style/medium: polished stylized 3D game-asset concept render, mildly chunky industrial sci-fi, readable hard-surface depth, not a toy.
Materials/textures: very high-roughness powder-coated metal and technical ceramic; dry dusty abrasion; broad diffuse highlights; restrained edge wear. No clearcoat, lacquer, polished plastic, wet metal, mirror reflections, tiny greebles, etched scratch clutter, or razor-thin panels.
Color palette: dominant light desaturated dusty brick-rose / pale clay armor, secondary muted old-brass hinge blocks, charcoal graphite mechanisms. Keep the overall object medium-light so it reads in a dark arena. No teal, cyan, turquoise, bright orange, saturated red, blue team paint, pure white nose, or glowing eye/light.
Panel layout: identical object, identical proportions and materials in all panels. TOP LEFT strict orthographic front view looking toward the broad +X jaw. TOP RIGHT strict orthographic right-side profile with +X/front toward image-left. BOTTOM LEFT strict orthographic rear view. BOTTOM RIGHT mildly elevated orthographic front-right three-quarter view at about 35 degrees yaw. These are the official provider-style Front + Side + Back + 3/4 view set. Make every view camera-consistent, same scale, centered, generous padding, no cropping, no perspective exaggeration.
Scene/backdrop: perfectly uniform light warm-gray background in every panel, with no floor plane, horizon, panel border, divider, shadow, or reflection.
Constraints: output only the four-view sheet; exactly one trap repeated in four views; no alternate design, detached parts, text, labels, numbers, arrows, logo, watermark, cast shadow, contact shadow, glow, bloom, range ring, explosion, smoke, particles, scenery.
Avoid: circular mine or puck, round radial silhouette, dome, sphere, orb, Core-like part, glowing eye, white nose, gun, barrel, turret, antenna, tall spike, wheel, track, leg, hover jet, vehicle body, toy-like saturation, dark-on-dark body.
```

## Trip Node strict top source

Reference: `trip-node-provider-views.png`, as the sole geometry/material
authority.

```text
Use case: precise-object-edit
Asset type: canonical strict top-down East-facing gameplay concept render derived from the approved four-view Trip Node draft.
Input image: Image 1 is the sole geometry, proportions, material, color, wear, and identity authority.
Primary request: render exactly the same Trip Node from a strict orthographic camera directly above it. Preserve the broken-triangle footprint, the three broad dusty brick-rose jaws, their graphite flexure bridges, muted old-brass end hardware, the central flat graphite pressure plate, and the extremely low profile. Do not redesign, add, remove, open, close, or move any part. Choose one jaw as canonical +X and point that jaw toward image-right/east. Center the complete object with generous equal padding; visible chassis occupies about 68 percent of the square.
Scene/backdrop: perfectly flat uniform solid #00ff00 chroma-key background for later background removal. Background must have no gradient, texture, floor plane, horizon, shadow, reflection, lighting variation, or color spill.
Lighting/material: same dry high-roughness matte finish and restrained color palette as Image 1, with even diffuse illumination that preserves broad value blocks. No tight highlights, bloom, or glow.
Constraints: one object only; strict top view; no perspective or oblique view; no cast/contact shadow; no floor; no text, label, arrow, border, logo, watermark, range ring, detonation, smoke, particles, team color, or scenery. Do not use #00ff00 anywhere on the object. Preserve the exact simple silhouette and large negative center. No eye, light, dome, sphere, spike, gun, wheel, leg, or additional detail.
```

The built-in result's selected jaw pointed North; the deterministic review
builder rotates the canonical transparent derivative 90° clockwise so +X
points East.

## Sentinel Seed initial four-view concept

References:

1. `art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet-retexture-v3-multiview/nest/019ff0f6-1446-7a06-b591-652543441f63/provider-preview.png`
2. `art/reviews/arc-relay-meshy7-fleet-retexture-v3/fixed-orientation-sheet.png`

```text
Use case: stylized-concept
Asset type: high-resolution 2x2 orthographic game-prop concept sheet for a new Nilbots Arc Relay SENTINEL SEED, later usable as separate Meshy multi-image-to-3D references after owner approval.
Input images: Image 1 is a visual reference only for the current Nest parent’s twin parallel rail/pod-rack family language, dry worn matte construction, light weathered sage-taupe armor, warm umber rails, muted aubergine seed-cell caps, charcoal mechanisms, and dull brass hardware. Do not copy the large carrier chassis. Image 2 is a visual reference only for the approved Arc Relay fleet’s broad readable shapes, restrained saturation, and mature non-toy finish. Do not copy an existing bot or signature prop.
Primary request: invent one completely new stationary automated guard that looks unmistakably like a single Nest seed pod planted and unfolded, not like a tank turret, gun emplacement, vehicle, mine, or toy.
Subject/design: fixed planted base plus visually separate rotating head. FIXED BASE: two short, broad, parallel sage-taupe anchor rails with clipped ends, running front-to-back as a compact H-shaped cradle; connect them by a low warm-umber cross saddle. They are heavy planted skids, with no wheels, tracks, feet, or hover gap. Center a clearly exposed charcoal circular yaw bearing above the saddle. ROTATING HEAD: one compact flattened seed-capsule mounted on the bearing, formed from two small sage shell cheeks around a muted aubergine upper core. Its +X/front face is almost flush and ends in one broad recessed dark beam-emitter grille; no protruding barrel, cannon, muzzle tube, eye, lens, or nose. Add a narrow horizontal neutral off-white status notch beneath the grille only if needed, never eye-like and with no bloom. The head has unobstructed 360-degree horizontal sweep and an obvious clean seam/pivot separating it from the fixed base. Large simple masses, very few seams. The whole prop is medium-low and compact inside one tile.
Mechanic/fantasy: one stationary hull-two, short-range guard deployed by Nest. It chooses nearest visible enemies, visibly winds up, then emits a beam. Range ring, target line, windup, beam, health, ownership, and expiration remain renderer-authored and must not be baked into the object.
Style/medium: polished stylized 3D game-asset concept render, mildly chunky industrial sci-fi, readable hard-surface depth, not a toy.
Materials/textures: very high-roughness powder-coated metal and technical ceramic; dry battlefield wear; broad soft diffuse highlights; restrained edge abrasion. No clearcoat, lacquer, glossy plastic, wet metal, mirror reflections, tight white highlights, tiny greebles, or etched scratch clutter.
Color palette: dominant medium-light weathered sage-taupe armor, secondary warm muted umber structural rails, one small muted aubergine seed-cap area, charcoal yaw mechanism, tiny dull-brass hardware. Keep the overall object light enough to read in a dark arena. No teal, cyan, turquoise, bright orange, saturated red, strong blue, team paint, pure white nose, or glowing eyes.
Panel layout: identical object, identical proportions and materials in all panels. TOP LEFT strict orthographic front view looking directly at the flush emitter face. TOP RIGHT strict orthographic right-side profile with +X/front toward image-left. BOTTOM LEFT strict orthographic rear view showing the back of the seed capsule and unobstructed yaw clearance. BOTTOM RIGHT mildly elevated orthographic front-right three-quarter view at about 35 degrees yaw. These are the official provider-style Front + Side + Back + 3/4 view set. Make every view camera-consistent, same scale, centered, generous padding, no cropping, no perspective exaggeration.
Scene/backdrop: perfectly uniform light warm-gray background in every panel, with no floor plane, horizon, panel border, divider, shadow, or reflection.
Constraints: output only the four-view sheet; exactly one sentinel repeated in four views; no alternate design, detached parts, text, labels, numbers, arrows, logo, watermark, cast shadow, contact shadow, glow, bloom, range ring, target line, beam, projectile, muzzle flash, smoke, particles, scenery.
Avoid: tank, generic turret, pillbox, pedestal cannon, gun barrel, protruding snout, artillery, missile launcher, eye or face, circular mine/puck, radial disc, sphere, orb, clustered round pods, mobile chassis, cockpit, wheels, tracks, legs, feet, hover jets, tall antenna, toy-like saturation, dark-on-dark body.
```

This first result was rejected before handoff because its two long base rails
read like tracks and its head dominated the top silhouette.

## Sentinel Seed accepted silhouette correction

References:

1. rejected initial four-view result (edit target; retained under the built-in
   generator path recorded in `manifest.json`)
2. current Nest provider preview (family reference only)

```text
Use case: precise-object-edit
Asset type: silhouette correction of the Nilbots Arc Relay Sentinel Seed four-view concept sheet.
Input images: Image 1 is the edit target and authority for the exact 2x2 panel layout, four camera angles, object scale, dry matte material finish, restrained sage-taupe / warm umber / muted aubergine / charcoal palette, lighting, background, and general visual family. Image 2 is a Nest family reference only; retain the relationship through its pale worn armor, warm rails, muted aubergine seed-cell material, and mature finish, not through vehicle geometry.
Primary correction: eliminate the mobile tank/track read and make the stationary planted seed silhouette unmistakable at 48 pixels.
Change the fixed base only as follows: replace the two long parallel rail-like skids with four short broad blunt planted armor lobes arranged as a compact diagonal X/calyx around the center bearing. Each lobe is a squat clipped wedge rooted to one central low warm-umber cross-frame, with open negative-space notches between lobes. The lobes must look like folded seed petals or ground anchors, not feet, legs, tracks, wheels, runners, or a vehicle chassis. Keep the base much wider than the head and very low.
Change the rotating head only as follows: shrink the entire rotating seed-capsule head to approximately 55 percent of its current width and 60 percent of its current height. Make it a compact flattened lozenge/seed with two sage shell cheeks and one small muted-aubergine top inset. Keep the +X/front face nearly flush with one broad recessed dark beam-emitter grille; no protruding barrel or snout. Remove the circular bullseye/ring from the aubergine top. The head must sit on the exposed charcoal circular yaw bearing with a clear air/seam break around it and unobstructed 360-degree sweep.
Identity/mechanics: stationary hull-two automated guard deployed by Nest; fixed planted base, separate yawing head, visible windup then beam handled later by the renderer. It is not a mobile body and not a conventional gun turret.
Hard invariants: preserve Image 1’s exact four panel positions and view directions: top-left strict front, top-right strict right-side, bottom-left strict rear, bottom-right front-right 3/4. Preserve consistent proportions across every panel, same centered framing, same light warm-gray background, dry high-roughness matte response, restrained wear, palette family, and no shadows. Use the identical revised object in all four views.
Constraints: output only the revised four-view sheet; no text, labels, borders, arrows, alternate designs, detached parts, shadow, floor, horizon, reflection, glow, range ring, target line, beam, projectile, smoke, logo, watermark, team teal/cyan/orange, saturated colors, or dark-on-dark body.
Avoid: tank, tracks, parallel vehicle rails, pillbox, generic gun turret, pedestal cannon, barrel, protruding snout, eye, face, bullseye, circular mine/puck, robot body, cockpit, wheels, legs, feet, hover jets, antenna, toy-like gloss or saturation.
```

## Sentinel Seed strict top source

Reference: `sentinel-seed-provider-views-v2.png`, as the sole
geometry/material authority.

```text
Use case: precise-object-edit
Asset type: canonical strict top-down East-facing gameplay concept render derived from the revised Sentinel Seed four-view sheet.
Input image: Image 1 is the sole geometry, proportions, material, color, wear, and identity authority.
Primary request: render exactly the same revised Sentinel Seed from a strict orthographic camera directly above it. Preserve the fixed four short broad clipped armor lobes arranged as a diagonal X/calyx around the center, their open negative-space notches, the low warm-umber central cross-frame, the exposed charcoal circular yaw bearing, and the visibly separate compact flattened seed-capsule rotating head. Preserve its two sage shell cheeks, small muted-aubergine top inset with no circular bullseye, and nearly flush broad dark beam-emitter grille. The emitter face and canonical +X/front point exactly image-right/east. Make the fixed four-lobe base dominate the top silhouette and keep the rotating head clearly smaller, centered, and separate. Do not redesign, add, remove, enlarge, or move any part. Center the object with generous equal padding; visible prop occupies about 68 percent of the square.
Scene/backdrop: perfectly flat uniform solid #ff00ff chroma-key background for later background removal. No gradient, texture, floor plane, horizon, shadow, reflection, lighting variation, or color spill.
Lighting/material: same dry high-roughness matte finish and restrained sage-taupe / umber / aubergine palette as Image 1, with even diffuse illumination and broad value blocks. No tight highlights, bloom, or glow.
Constraints: one object only; strict top view; no perspective or oblique view; no cast/contact shadow; no floor; no text, label, arrow, border, logo, watermark, range ring, target line, beam, projectile, smoke, particles, team color, or scenery. Do not use #ff00ff on the object.
Avoid: tank, generic gun turret, barrel, protruding cannon, face, eyes, bullseye, circular mine/puck, vehicle, tracks, wheels, legs, feet, hover gap, antenna, extra pod, additional detail, toy-like saturation, dark-on-dark body.
```

The first top result placed the head North/South. A second edit rotated only
the head 90° clockwise; that exact prompt is retained below.

```text
Use case: precise-object-edit
Asset type: orientation-only correction of the strict top-down Sentinel Seed concept render.
Input image: Image 1 is the edit target and absolute authority for the fixed base, geometry, materials, palette, wear, lighting, strict top camera, scale, framing, and flat magenta chroma background.
Primary request: rotate only the compact central rotating seed-capsule head exactly 90 degrees clockwise around its existing central yaw bearing. Leave the four-lobe planted base, warm-umber cross-frame, bearing, object center, scale, and every other pixel-level design feature unchanged. After rotation, the capsule’s long axis runs horizontally and its canonical +X/front points exactly image-right/east. Make the front/right short edge carry the same flush dark recessed beam-emitter grille seen in the source four-view sheet, visible only as a narrow dark edge from this strict top view. Preserve the small plain aubergine top inset with no bullseye.
Hard invariants: change only the yaw orientation of the central head. Keep the strict orthographic top-down camera, fixed base orientation, all proportions, four planted lobes, negative-space notches, central cross-frame, matte sage-taupe / umber / aubergine / charcoal palette, dry wear, diffuse lighting, object size, centering, padding, and perfectly uniform solid #ff00ff background unchanged.
Constraints: one object; no shadow, floor, text, labels, arrow, ring, glow, team color, range, beam, projectile, scenery, logo, watermark, added parts, removed parts, or redesign. Do not use #ff00ff on the object.
Avoid: rotating the fixed base, changing camera angle, mobile chassis, tank, barrel, protruding gun, eye, face, bullseye, circular mine, tracks, wheels, legs, toy gloss.
```
