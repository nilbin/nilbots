# Projectile provider-input prompt record

All edits below were made on 2026-07-30 with Codex's built-in `imagegen`
workflow. They prepared offline reconstruction inputs only. No Meshy task or
other paid provider task was submitted.

The original owner-approved concept prompts remain verbatim in
`../concept-targets/PROMPTS.md`. This file records the later cleanup,
background-extraction, and exact-top material passes.

| Asset/pass | Retained output | SHA-256 |
| --- | --- | --- |
| Trident reconstruction board | `meshy/trident-spark/clean-v1/reconstruction-sheet.png` | `fed1d7b4c29d7b3753003a1a0b63f6caa09a7a24482cc08e455f8627446f6bf5` |
| Trident chroma board | `meshy/trident-spark/clean-v1/chroma-source-sheet.png` | `7234437b8f018cdbeeb72c5f9b31aef646de6a9180c96ed1050e4da76e5c29d7` |
| Trident exact-top texture | `meshy/trident-spark/clean-v1/top-texture-source.png` | `81f5eab9cae1497184a7b83d2584b6a232bdf87e42fd6ec357baed5f34c015e9` |
| Rebound reconstruction board | `meshy/rebound-diamond/clean-v1/reconstruction-sheet.png` | `25ef988f2e24be7cfc4803547f23a301c4a2493e9d21f0d8983c0c2e2a66dbfc` |
| Rebound chroma board | `meshy/rebound-diamond/clean-v1/chroma-source-sheet.png` | `ecc3af23b8e6b0ba6d303e7c99761fbf6391a1a127723c7e78b4764d027ff518` |
| Lattice reconstruction board | `meshy/lattice-rivet/clean-v1/reconstruction-sheet.png` | `fa48190c864f242f3489673e21d7f3d1ead6b33c385b069222e007c88f8c26d9` |
| Lattice chroma board | `meshy/lattice-rivet/clean-v1/chroma-source-sheet.png` | `95f7fa1eb5b9ef4337f84c4f7b4547e56375cdee3ce57c39b27af8814736575c` |
| Lattice exact-top texture | `meshy/lattice-rivet/clean-v1/top-texture-source.png` | `5dbf0c4a71cd76c043df02023ce885f402e42606cfc8951b5fc69067c59a3b30` |

## Trident Spark reconstruction board

```text
Use case: precise-object-edit
Asset type: provider-input cleanup board for the approved Nilbots Striker default projectile head, Trident Spark
Input images: Image 1 is the exact approved 3D projectile model, material, depth, wear, and component identity to preserve. Image 2 is the canonical East/right-facing SVG-derived alpha mask and is the absolute strict-top outer-silhouette authority; trace its deep V-notched West/rear, long central waist, swept outer blades, and single East/front spear exactly.
Primary request: Re-render the same one Trident Spark head from Image 1 as four clean, mutually consistent views. Do not redesign it. Correct only the strict top planform to match Image 2 exactly while preserving Image 1's real faceted volume, raised central energy ridge, layered graphite structure, desaturated warm-metal blade faces, bevels, seams, restrained scratches, and shallow underside.
Composition/framing: one clean 2x2 board, no labels or dividers: upper-left strict orthographic top facing East/right and largest; lower-left strict longitudinal side profile with East/right nose still pointing right; upper-right front-right three-quarter; lower-right rear-left three-quarter. The exact same single object, same scale, geometry, materials, proportions, and surface markings in all four panels. Generous separation and no crop.
Scene/backdrop: perfectly flat uniform #b8b8b8 neutral background with no floor plane.
Lighting: diffuse neutral object lighting only, readable form without environmental light variation.
Team/material logic: neutral value-driven PBR suitable for one downstream renderer-owned projectile-mask material. Keep charcoal graphite, desaturated warm metal, and neutral-white uncolored energy. No fixed cyan, red, orange, or other team hue.
Constraints: projectile head only; one connected object; no trail, tracer, halo, bloom, floor wash, motion blur, spin depiction, impact, scenery, bot, text, logo, watermark. Absolutely no cast shadow, contact shadow, reflection, floor gradient, vignette, background texture, or background lighting. Runtime owns tint, emission strength, trail, glow, spin, travel, hit/miss/deflection effects, and hitbox.
```

## Trident Spark chroma cleanup

```text
Use case: background-extraction
Asset type: chroma-key cleanup source for the already approved Trident Spark four-view projectile board
Input image: Image 1 is the exact edit target and complete visual authority.
Primary request: Change only the background. Preserve every projectile pixel, all four camera views, layout, geometry, top silhouette, proportions, materials, scratches, bevels, seams, neutral-white energy ridge, orientation, scale, and framing exactly. Replace the entire background—including every gap around the object—with one perfectly flat solid pure #00FF00 chroma-key color.
Constraints: the exact same four Trident Spark views remain unchanged. No new geometry, no removed geometry, no shifted parts, no recolor, no surface cleanup, no crop, no resize, no labels, no dividers. The projectile contains no #00FF00. No cast shadow, contact shadow, reflection, glow spill, floor plane, gradient, vignette, texture, or lighting variation in the background. Do not place green inside or over the projectile. Output the same board arrangement as Image 1.
```

## Trident Spark exact-top texture

```text
Use case: sketch-to-render
Asset type: exact strict-top material reconstruction input for the Nilbots Trident Spark projectile head
Input images: Image 1 is the absolute East/right-facing outer silhouette authority and must be traced exactly at the same proportions: deep V-notched West/rear, long narrow waist, two large swept outer conductor blades, and one single longer East/front spear, with no holes or detached pieces. Image 2 supplies only the approved graphite/warm-metal PBR materials, central neutral-white ridge, bevel language, panel seams, restrained wear, and real shallow depth. Do not copy Image 2's alternate top proportions when they differ from Image 1.
Primary request: Render one single Trident Spark head in strict orthographic top view, filling Image 1's exact silhouette with coherent real hard-surface material detail. Preserve a connected compact head: charcoal graphite structure, desaturated warm-metal blade faces, crisp inset panel seams, chamfered edges, a narrow raised neutral-white energy ridge, subtle scratches, and restrained shallow depth. The material layout must cover the full exact silhouette with no shadow-like gap-fill wedges.
Composition/framing: one centered object only, East/front pointing right, object width about 75% of a square canvas, generous padding, no crop.
Scene/backdrop: perfectly flat solid pure #00FF00 chroma key everywhere outside the object; no floor plane.
Lighting: diffuse neutral object lighting only, enough to reveal bevels without changing the background.
Team/material logic: neutral value-driven surface suitable for downstream whole-object projectile-mask tint. No cyan, red, orange, or fixed team color.
Constraints: exact Image 1 planform; no cast/contact shadow, reflection, halo, bloom, trail, motion blur, impact, bot, scenery, text, logo, watermark, floor gradient, background variation, extra fins, vehicle/cockpit cues, or detached prongs.
```

## Rebound Diamond reconstruction board

```text
Use case: precise-object-edit
Asset type: provider-input cleanup board for the approved Nilbots Bulwark default projectile head, Rebound Diamond
Input images: Image 1 is the exact approved 3D projectile model, material, depth, wear, open tunnel, and component identity to preserve. Image 2 is the canonical East/right-facing SVG-derived alpha mask and is the absolute strict-top outer silhouette and open elongated-hex aperture authority.
Primary request: Re-render the same one Rebound Diamond head from Image 1 as four clean, mutually consistent views. Do not redesign it. Correct only the strict top planform to trace Image 2 exactly: short sharp West/rear point, longer East/front point, broad upper/lower shoulders, and completely open elongated-hex tunnel. Preserve Image 1's real low armored depth, thick inner and outer bevels, shallow crowned rim, segmented dark graphite and desaturated bronze/steel reflector faces, restrained wear, raised rear chevron, and protected underside.
Critical side-view correction: Image 1's upper-right panel is an end-on/nose-on view and must NOT be reused as the side. Create a true strict longitudinal side profile seen from the object's South side: the full West-to-East length is visible horizontally, West/rear at left and East/front at right, showing the low rim thickness and the open tunnel through the body. It must be unmistakably a side profile, not an end view.
Composition/framing: one clean 2x2 board, no labels or dividers: upper-left strict orthographic top facing East/right and largest; lower-left the required true strict longitudinal side profile; upper-right front-right three-quarter; lower-right rear-left three-quarter. The exact same single object, same scale, geometry, materials, proportions, aperture, and surface markings in all four panels. Generous separation and no crop.
Scene/backdrop: perfectly flat uniform #b8b8b8 neutral background with no floor plane.
Lighting: diffuse neutral object lighting only, readable form and open aperture without environmental variation.
Team/material logic: neutral value-driven PBR suitable for one downstream renderer-owned projectile-mask material. Dark navy-graphite, desaturated bronze/steel, and neutral-white uncolored energy only. No fixed cyan, red, orange, or other team hue.
Constraints: projectile head only; one connected open-ring object; aperture remains empty and transparent in every applicable view; no trail, halo, bloom, floor wash, motion blur, spin depiction, ricochet depiction, impact, scenery, bot, text, logo, watermark. Absolutely no cast shadow, contact shadow, reflection, floor gradient, vignette, background texture, or background lighting. Runtime owns tint, emission strength, trail, glow, flat-spin, travel, hit/miss/deflection effects, and hitbox.
```

## Rebound Diamond chroma cleanup

```text
Use case: background-extraction
Asset type: chroma-key cleanup source for the already approved Rebound Diamond four-view projectile board
Input image: Image 1 is the exact edit target and complete visual authority.
Primary request: Change only the background. Preserve every projectile pixel, all four camera views, layout, geometry, exact open elongated-hex tunnel, true longitudinal side profile, top silhouette, proportions, materials, scratches, bevels, seams, neutral-white inlays, orientation, scale, and framing exactly. Replace the entire background, including the visible background through every open aperture, with one perfectly flat solid pure #00FF00 chroma-key color.
Constraints: the exact same four Rebound Diamond views remain unchanged. The lower-left view must remain the full West-to-East true side, not an end view. No new geometry, no removed geometry, no shifted parts, no recolor, no filled aperture, no surface cleanup, no crop, no resize, no labels, no dividers. The projectile contains no #00FF00. No cast shadow, contact shadow, reflection, glow spill, floor plane, gradient, vignette, texture, or lighting variation in the background. Do not place green inside or over the projectile. Output the same board arrangement as Image 1.
```

## Lattice Rivet reconstruction board

```text
Use case: precise-object-edit
Asset type: provider-input cleanup board for the approved Nilbots Fabricator default projectile head, Lattice Rivet
Input images: Image 1 is the exact approved 3D projectile model, material, depth, wear, open lattice construction, and component identity to preserve. Image 2 is the canonical East/right-facing SVG-derived alpha mask and is the absolute strict-top outer silhouette and central diamond-aperture authority.
Primary request: Re-render the same one Lattice Rivet head from Image 1 as four clean, mutually consistent views. Do not redesign it. Correct only the strict top planform to trace Image 2 exactly: concave split/V-notched West rear with both rear rails joined to the same object, long central body, stepped upper/lower shoulders, pointed East nose, and completely open diamond aperture. Preserve Image 1's genuine compact depth, pale ceramic layered plates, graphite lattice/truss structure, muted copper-value coils, faceted forward wedge, hollow collar, recessed rails, underside chamfers, seams, and restrained wear.
Composition/framing: one clean 2x2 board, no labels or dividers: upper-left strict orthographic top facing East/right and largest; lower-left strict longitudinal side profile with West/rear left and East/front right; upper-right front-right three-quarter; lower-right rear-left three-quarter. The exact same single connected object, same scale, geometry, materials, proportions, aperture, and surface markings in all four panels. Generous separation and no crop.
Scene/backdrop: perfectly flat uniform #b8b8b8 neutral background with no floor plane.
Lighting: diffuse neutral object lighting only, readable truss and aperture without environmental variation.
Team/material logic: neutral value-driven PBR suitable for one downstream renderer-owned projectile-mask material. Pale neutral ceramic, graphite structure, desaturated muted copper-value details, and neutral-white uncolored energy only. No fixed cyan, red, orange, amber glow, or other team hue.
Constraints: projectile head only; one connected object; open diamond aperture; joined split rear rails; no fabricated child, mini-bot, tool arm, trail, halo, bloom, floor wash, motion blur, tumble depiction, impact, scenery, bot, text, logo, watermark. Absolutely no cast shadow, contact shadow, reflection, floor gradient, vignette, background texture, or background lighting. Runtime owns tint, emission strength, trail, glow, tumble, travel, hit/miss effects, and hitbox.
```

## Lattice Rivet chroma cleanup

```text
Use case: background-extraction
Asset type: chroma-key cleanup source for the already approved Lattice Rivet four-view projectile board
Input image: Image 1 is the exact edit target and complete visual authority.
Primary request: Change only the background. Preserve every projectile pixel, all four camera views, layout, geometry, pale ceramic plates, dark open trusses, copper details, joined split rear rails, exact open diamond aperture, top silhouette, proportions, materials, scratches, bevels, seams, orientation, scale, and framing exactly. Replace the entire background, including the visible background through the diamond aperture and every truss opening, with one perfectly flat solid pure #00FF00 chroma-key color.
Constraints: the exact same four Lattice Rivet views remain unchanged. No new geometry, no removed ceramic, no erased dark truss, no shifted parts, no recolor, no filled aperture, no surface cleanup, no crop, no resize, no labels, no dividers. The projectile contains no #00FF00. No cast shadow, contact shadow, reflection, glow spill, floor plane, gradient, vignette, texture, or lighting variation in the background. Do not place green inside or over any pale neutral hardware. Output the same board arrangement as Image 1.
```

## Lattice Rivet exact-top texture

```text
Use case: sketch-to-render
Asset type: exact strict-top material reconstruction input for the Nilbots Lattice Rivet projectile head
Input images: Image 1 is the absolute East/right-facing outer silhouette and negative-space authority and must be traced exactly at the same proportions: concave joined split/V-notched West rear, long stepped body, pointed East nose, and one completely open central diamond aperture. Image 2 supplies only the approved pale ceramic, graphite truss, muted copper-value details, panel seams, wear, and real shallow depth. Do not copy Image 2's alternate top proportions when they differ from Image 1.
Primary request: Render one single connected Lattice Rivet head in strict orthographic top view, filling Image 1's exact silhouette with coherent real hard-surface material detail. Preserve pale neutral ceramic plates over a dark graphite lattice collar, joined split rear rails, visible restrained truss channels, crisp panel seams, compact muted copper hardware, chamfered edges, subtle scratches, and a clean completely empty diamond aperture. The material layout must cover the full exact silhouette without gray shadow patches or detached parts.
Composition/framing: one centered object only, East/front pointing right, object width about 75% of a square canvas, generous padding, no crop.
Scene/backdrop: perfectly flat solid pure #00FF00 chroma key everywhere outside the object and through the exact diamond aperture and any real truss holes; no floor plane.
Lighting: diffuse neutral object lighting only, enough to reveal bevels without changing the background.
Team/material logic: neutral value-driven surface suitable for downstream whole-object projectile-mask tint. Pale neutral ceramic, graphite structure, desaturated muted copper-value details, neutral-white uncolored energy only. No cyan, red, orange glow, or fixed team color.
Constraints: exact Image 1 planform and aperture; projectile head only; one connected object; no cast/contact shadow, reflection, halo, bloom, trail, motion blur, tumble depiction, impact, bot, child, scenery, text, logo, watermark, floor gradient, background variation, detached rail, filled hole, or tool arm.
```
