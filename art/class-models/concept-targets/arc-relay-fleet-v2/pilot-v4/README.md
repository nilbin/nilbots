# Arc Relay v4 two-model pilot inputs

These are the isolated image-to-3D inputs used for the Minesmith/Nest pilot.
They were made with the built-in `imagegen` tool from
`concept-sheet-industrial-color-v4.png`, then converted from chroma-keyed RGB
to soft-matted RGBA with the imagegen skill's `remove_chroma_key.py` helper.
No renderer-owned team color is present in either source.

## Minesmith

- Built-in image-generation output:
  `019fc27e-33d3-7971-a896-3915521f3a0b/exec-c5643394-a021-4156-a51f-5b258acb211c.png`
- Checked-in chroma source: `arc-minesmith/concept-chroma.png`
- Chroma source SHA-256:
  `361d8f1d63c6c8ad571190ac5f3c3ccd0e361abb10df2fdc2bf2cf7b68f72437`
- Meshy RGBA input: `arc-minesmith/meshy-input.png`
- Meshy input SHA-256:
  `927c522ccb671e72c598f548545d8d2545e25d133457a52eab0a5ce50f717f62`

```text
Use case: precise-object-edit.
Asset type: isolated single-object image-to-3D provider input for the Nilbots Arc Relay Minesmith bot.
Input image: use the provided industrial-color 4x4 roster sheet as the exact visual reference. The target is ONLY the Minesmith in row 2, column 3: the burnt-orange and gunmetal compact wheeled industrial carrier with exactly three flat circular mine discs visible on its upper rack.
Primary request: produce one isolated, centered, complete render of that exact Minesmith design. Preserve its silhouette, proportions, low heavy chassis, wheel arrangement, right/East-facing orientation, three distinct flat mine discs, front and rear armor, burnt-orange industrial enamel coverage, exposed graphite/gunmetal mechanisms, warm light-gray metal, restrained edge wear, and realistic satin PBR material hierarchy. Do not redesign or add anything. Keep the same mild 58-degree gameplay-oblique camera, with the nose pointing right.
Composition: one object only, fully visible, centered, large but with generous equal padding, square image. No crop, no floor, no cast/contact shadow, no reflection.
Background: perfectly flat solid #00ff00 chroma-key background for deterministic removal. The background must be one uniform color with no gradients, texture, lighting variation, horizon, or floor plane. Do not use #00ff00 anywhere in the bot.
Lighting: neutral diffuse product lighting on the bot only; preserve material separation without dramatic highlights.
Constraints: exact single Minesmith; exactly three flat mine discs; East/right facing; no detached mine; no spherical cargo; no team paint; no colored emissive; tiny neutral amber lights only; no text, labels, decals, logos, border, watermark, scenery, weapon effects, particles, smoke, or extra objects.
Avoid: toy-like plastic, candy gloss, primary-color shell coverage, rounded molded highlights, black outlines, cel shading, extra greebles, exaggerated chunky seams, additional wheels, turrets, guns, barrels, domes, spheres, legs, tracks, or floating parts.
```

## Nest

- Built-in image-generation output:
  `019fc27e-33d3-7971-a896-3915521f3a0b/exec-845160cd-d297-486e-a8fd-fcf1ebc642a7.png`
- Checked-in chroma source: `arc-nest/concept-chroma.png`
- Chroma source SHA-256:
  `f0671fa5daa3c4af528ec8cb7bc975b1734ac18c30666f99c81a770a2a667c3a`
- Meshy RGBA input: `arc-nest/meshy-input.png`
- Meshy input SHA-256:
  `ebfdd6b56f091f9e08cb4d35be13c73938e7a04fa9c8e9250aacbb0de6ac1dc2`

```text
Use case: precise-object-edit.
Asset type: isolated single-object image-to-3D provider input for the Nilbots Arc Relay Nest bot.
Input image: use the provided industrial-color 4x4 roster sheet as the exact visual reference. The target is ONLY the Nest in row 4, column 4: the olive/khaki and gunmetal compact four-wheel industrial carrier with exactly two folded compact sentry pods on top, each pod having one short dark gun barrel pointing right/East.
Primary request: produce one isolated, centered, complete render of that exact Nest design. Preserve its silhouette, proportions, low compact chassis, four-wheel arrangement, open rectangular pod cradles, two clearly separate folded sentry pods, exactly two short barrels, right/East-facing orientation, military olive/khaki industrial enamel coverage, exposed graphite/gunmetal mechanisms, restrained edge wear, and realistic satin PBR material hierarchy. Do not redesign or add anything. Keep the same mild 58-degree gameplay-oblique camera, with both barrels and the chassis nose pointing right.
Composition: one object only, fully visible, centered, large but with generous equal padding, square image. No crop, no floor, no cast/contact shadow, no reflection.
Background: perfectly flat solid #ff00ff chroma-key background for deterministic removal. The background must be one uniform color with no gradients, texture, lighting variation, horizon, or floor plane. Do not use #ff00ff anywhere in the bot.
Lighting: neutral diffuse product lighting on the bot only; preserve material separation without dramatic highlights.
Constraints: exact single Nest; exactly two folded sentry pods; exactly two short barrels; East/right facing; no deployed turret; no spherical cargo; no ball-shaped pods; no team paint; no colored emissive; tiny neutral amber lights only; no text, labels, decals, logos, border, watermark, scenery, weapon effects, particles, smoke, or extra objects.
Avoid: toy-like plastic, candy gloss, lime toy-truck color, rounded molded highlights, black outlines, cel shading, extra greebles, exaggerated chunky seams, additional wheels, mines, discs, long artillery guns, domes, spheres, legs, tracks, or floating parts.
```
