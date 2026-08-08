# Woodland Grove accepted source prompts

Generated with Codex's built-in image-generation tool on 2026-08-08. The
accepted lossless outputs are retained beside this file and are production
inputs to `scripts/build-theme-art.py`.

## Floor / grass and undergrowth

```text
Use case: stylized-concept
Asset type: seamless floor source material for the Nilbots top-down arena theme "Woodland Grove"
Primary request: create a premium woodland clearing floor material made from calm grass and low undergrowth that stays immediately readable beneath small combat robots
Scene/backdrop: one continuous natural ground material fills the square edge to edge
Style/medium: polished stylized game texture, high-end hand-painted PBR albedo source, readable at a 40–70 pixel gameplay cell
Composition/framing: strict orthographic top-down flat material swatch; broad interwoven mossy grass, muted clover-like ground cover, sparse soft leaf litter and small exposed loam patches; organic asymmetrical distribution; every feature continues naturally through the boundaries
Lighting/mood: neutral even upper-left illumination, no central spotlight, no cast shadows
Color palette: desaturated fern green, olive grass, deep moss, muted brown loam, restrained pale sage highlights
Materials/textures: short grass blades massed into broad shapes, velvety moss, low flat leaves, damp earth; medium-scale detail only
Constraints: opaque square seamless tileable texture; material only; no gameplay grid, paths, rivers, trenches, tall plants, tree trunks, rocks, flowers, standalone props, text, letters, numerals, logos, watermark, frame, horizon, perspective, or deep shadows
Avoid: repeated quadrants, checkerboard, identical tufts, high-frequency lawn noise, neon green, golf-course stripes, dense jungle clutter, obvious focal object
```

## Perimeter / ancient tree line

```text
Use case: stylized-concept
Asset type: seamless perimeter source material for the deterministic Nilbots top-down wall-baking pipeline, Woodland Grove theme
Primary request: create a fortified ancient woodland perimeter MATERIAL FIELD that reads as massive old trees and impenetrable roots, substantially heavier and darker than the grass floor
Scene/backdrop: one continuous organic material fills the square edge to edge
Style/medium: premium stylized game texture, high-end hand-painted PBR albedo source, readable at small gameplay scale
Composition/framing: strict orthographic top-down flat material swatch; broad dark old-oak bark plates, gnarled interlocking root ridges, deep crevices, restrained moss shelves and a few muted lichen patches; asymmetrical large forms continue naturally beyond every boundary
Lighting/mood: neutral flat albedo-like upper-left illumination; no cast shadows, no directional scene spotlight, no isolated tree silhouette
Color palette: charcoal umber bark, deep walnut, near-black crevices, forest moss, muted grey-green lichen
Materials/textures: coarse ancient bark, thick woody roots, damp moss, worn lichen; strong large-scale value separation
Constraints: opaque square seamless tileable continuous material; material only; no standalone tree, no canopy silhouette, no surrounding grass floor, no outer border, frame, perspective, empty background, flowers, fruit, text, logo, or watermark
Avoid: planks, lumber, cut logs, brick pattern, repeated quadrants, bilateral symmetry, grid, checkerboard, identical knots, dramatic black shadows, bright saturated foliage, dense tiny noise
```

## Interior cover / young trees and understory

```text
Use case: stylized-concept
Asset type: seamless interior-cover source material for the deterministic Nilbots top-down wall-baking pipeline, Woodland Grove theme
Primary request: create a lower woodland tree-cover MATERIAL FIELD that reads as younger trunks, roots, and understory, clearly lighter, lower, and more open than the ancient perimeter family
Scene/backdrop: one continuous organic material fills the square edge to edge
Style/medium: premium stylized game texture, high-end hand-painted PBR albedo source, readable at small gameplay scale
Composition/framing: strict orthographic top-down flat material swatch; broad warm-brown young bark forms, smaller interlaced roots, patches of soft moss, sparse flat fern fronds and muted leaf litter; asymmetrical distribution continuing naturally through boundaries
Lighting/mood: neutral flat albedo-like upper-left illumination; no cast shadows, no directional scene spotlight, no isolated tree silhouette
Color palette: warm chestnut bark, muted olive moss, sage fern, dark soil, restrained ochre leaf litter
Materials/textures: smoother young bark, shallow root ridges, velvety moss, low flat understory leaves; medium and large detail
Constraints: opaque square seamless tileable continuous material; material only; visually distinct from the darker fortified perimeter; no standalone tree, canopy silhouette, surrounding floor, outer frame, perspective, empty background, tall grass, flowers, text, logo, or watermark
Avoid: matching the perimeter, cut timber, lumber planks, brick pattern, repeated quadrants, grid, checkerboard, identical ferns, dramatic shadows, neon green, dense tiny foliage
```
