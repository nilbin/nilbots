# Arc Relay premium roster concept v1

This concept sheet is an art-direction source, not a runtime asset. The final
class looks use the visual-assets brief's documented raster exception: compact
PNG bodies and semantic team-light masks, plus named-group SVG source templates
for later rigging. Effects and the shared projectile remain genuine SVG.

## Fixed class order

1. Kestrel
2. Palisade
3. Towline
4. Patchbay
5. Lantern
6. Mortar
7. Minesmith
8. Hush
9. Relay
10. Switchback
11. Longshot
12. Mason
13. Sunder
14. Repulsor
15. Veil
16. Nest

The contact sheet is four columns by four rows in that order.

## Generation prompt

```text
Use case: stylized-concept
Asset type: premium 2D game-art direction sheet for a top-down arena game's sixteen launch-class bot sprites.

Input images: Image 1 is the prior Nilbots premium class-look contact sheet and the primary finish-quality reference; Image 2 is the current sixteen-class silhouette/signature-hardware map and fixes the class order and core identities; Images 3 and 4 are close premium material/detail references (Trident Wasp and Lattice Loom). Do not copy their exact chassis.

Primary request: infer a brand-new coherent set of sixteen distinct Arc Relay bot looks, arranged in an exact 4-by-4 grid in this fixed order: row 1 Kestrel, Palisade, Towline, Patchbay; row 2 Lantern, Mortar, Minesmith, Hush; row 3 Relay, Switchback, Longshot, Mason; row 4 Sunder, Repulsor, Veil, Nest.

Class identity: each body must visibly telegraph its signature before firing. Kestrel is a fast dart; Palisade has a broad projector face; Towline has a winch and hook; Patchbay is a field medic; Lantern has a sensor mast/disc; Mortar is lobbing artillery; Minesmith carries mines; Hush is a dampener array; Relay is a thrower with a Core cradle; Switchback is a mirrored paired frame; Longshot is built around a rail; Mason is a builder rig; Sunder has designator optics; Repulsor has radial emitters; Veil wears smoke launchers; Nest carries deployable pods.

Projection: every machine faces East in a genuine mild oblique / shallow three-quarter top view around 20 degrees. Show the top plane plus a narrow, clearly drawn chassis side face and underbody depth. Not straight top-down, not isometric, not side view. The same single sprite must plausibly rotate to all eight compass headings.

Finish: match the older premium Nilbots references in craftsmanship: layered armor plates, inset machinery, material separation, panel seams, restrained edge wear, directional highlights, dark recesses, fine but controlled surface texture, emissive cores, polished game concept art. Stylized high-end 2D game sprite rendering, not photorealistic and not a flat vector icon. Preserve strong silhouettes and gameplay-scale readability; use large meaningful forms before micro-detail.

Locomotion: varied hover, wheels, treads, and skids; no articulated walking legs and no gait promise.

Team language: use cyan only on a few large, semantic team-light strips/surfaces per bot; team color must read clearly but must not recolor the whole chassis. Other palettes and material characters may vary boldly by class.

Composition: one isolated bot per equal dark-neutral cell, consistent scale and lighting, generous padding, no overlap. No labels, no text, no logos, no watermark, no environment, no weapons fire, no effects. Exactly sixteen bots, exactly four rows and four columns.

Avoid: toy-like flat iconography, mobile-game clip art, overly black silhouettes, dominant cyan paint, generic repeated hulls, walking legs, straight top-down projection, isometric projection, blurry microdetail.
```

## Production interpretation notes

- Preserve the concept's large silhouette and signature-hardware reads in the
  compact keyed derivative; do not redraw them into flatter icon geometry.
- Sunder's red concept chassis is not literal runtime color: the production palette must remain neutral enough for semantic cyan/red team accents to read without ambiguity.
- The projection is structural. Every runtime source needs a visible lower hull/side face and elevated hardware; a vertical scale transform alone does not count as tilt.
- Fine texture is subordinate to gameplay-scale panel breaks, material separation, and emissive hierarchy.

## Production isolation prompts

The approved concept was first isolated as a roster atlas with this edit prompt:

```text
Edit the referenced Arc Relay 4-by-4 roster concept into a clean production sprite atlas while preserving all sixteen bot designs, class identities, materials, silhouettes, and exact fixed order. Output exactly sixteen isolated East-facing bots in an exact four-column by four-row grid: Kestrel, Palisade, Towline, Patchbay; Lantern, Mortar, Minesmith, Hush; Relay, Switchback, Longshot, Mason; Sunder, Repulsor, Veil, Nest. Remove every card, cell background, floor, cast shadow, label, text, divider, border, logo, and environment. Use a fully transparent alpha background with generous transparent gutters between bots. Keep the genuine mild 20-degree shallow-oblique view, visible narrow chassis side faces and underbody depth. Use consistent scale and directional lighting. Preserve crisp premium material detail and hard silhouette edges; no haze, no glow spilling beyond the bot, no weapon fire, no effects. Retain only small cyan semantic light surfaces on the bots; do not recolor the chassis cyan. This is a sprite atlas, not a presentation sheet. Exactly 16 bots, no additions or omissions.
```

The provider returned a baked checkerboard rather than an alpha channel. A second edit replaced only that background with a deterministic key color:

```text
Production cleanup edit. Preserve every bot exactly as shown: same sixteen designs, exact 4-by-4 positions and order, silhouettes, scale, details, colors, cyan light surfaces, East-facing 20-degree shallow-oblique view, and crisp hard edges. Change ONLY the background. Replace the entire baked gray-and-white checkerboard everywhere, including all holes and gaps around hooks, arms, rails, pods, and antennae, with one perfectly uniform flat chroma-key green color #00FF00. No gradient, texture, grid, shadow, reflection, floor, labels, borders, glow spill, or environmental light on the background. Do not add green anywhere on the bots. Keep generous gutters. Exactly sixteen bots and nothing else.
```

`scripts/build-arc-relay-class-art.mjs` performs the repeatable production pass: cell extraction, chroma matting and spill removal, scale normalization, semantic cyan-light separation, Sunder palette deconfliction, PNG optimization, and layered runtime-SVG wrapping. The team-light mask remains renderer-owned and is recolored cyan/red at runtime.

This is the art brief's raster exception. The attempted genuine-vector output is archived under `vector-fallback/`; it is smaller and fully articulated by named vector groups, but its flat icon finish did not clear the gameplay art bar. The compact keyed derivatives live under `raster-masters/`; runtime packages embed optimized derivatives, never the original generation sheets.
