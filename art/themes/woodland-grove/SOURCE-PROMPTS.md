# Woodland Grove accepted source prompts — revision 2

Generated with Codex's built-in image-generation tool on 2026-08-08. These
lossless outputs replace the first-pass sources and are production inputs to
`scripts/build-theme-art.py`. The first-pass prompts remain in Git history.

## Floor / quiet continuous undergrowth

```text
Use case: stylized-concept
Asset type: revised seamless floor source material for the Nilbots top-down arena theme Woodland Grove
Input images: Image 1 is the edit target and establishes the woodland palette and material identity
Primary request: calm the existing ground down so it supports tiny combat robots instead of competing with them; replace the busy high-frequency leaf and grass pattern with broad, softly blended areas of short moss, muted grass, and very sparse low groundcover
Scene/backdrop: one continuous natural ground material filling the square edge to edge
Style/medium: premium stylized game texture, hand-painted PBR albedo source, intentionally quiet at a 40–70 pixel gameplay cell
Composition/framing: strict orthographic top-down flat material swatch; broad irregular patches much larger than a gameplay tile; no directional bands or repeating clusters; only occasional soft leaf shapes
Lighting/mood: neutral even upper-left illumination, shallow value range, no cast shadows
Color palette: desaturated deep moss, subdued olive, fern grey-green, tiny amounts of dark loam; reduce saturation and local contrast substantially from Image 1
Materials/textures: velvety moss and short grass dominate; low soft detail, no crisp individual blades across most of the field
Constraints: change only texture frequency, contrast, and groundcover density; preserve opaque square woodland ground identity; visually continuous and seamless; no gameplay grid, tile outlines, paths, rivers, trenches, tall plants, tree trunks, rocks, flowers, props, text, logos, watermark, frame, horizon, perspective, or deep shadows
Avoid: checkerboard, quilting, square patches, repeated quadrants, diagonal seams, obvious focal areas, bright lime green, crunchy micro-detail, high-contrast bare-dirt channels
```

## Perimeter / foliage-led ancient tree line

```text
Use case: stylized-concept
Asset type: revised seamless ancient-tree perimeter material for the Nilbots Woodland Grove 3D arena
Input images: Image 1 is the edit target and establishes the ancient woodland family
Primary request: replace the bark-dominated brown field with a dark organic forest-boundary material led by overlapping evergreen and broadleaf crown fragments, moss cushions, ivy, and only occasional glimpses of charcoal old trunk; it must read as dense living tree cover rather than dirt or a brown wall
Scene/backdrop: one continuous impenetrable woodland material filling the square edge to edge
Style/medium: premium stylized game material, hand-painted PBR albedo source, readable at a 40–70 pixel gameplay cell
Composition/framing: strict orthographic top-down flat material swatch; broad irregular foliage islands interlock across the entire field; varied leaf mass sizes and asymmetric gaps; no isolated tree silhouette and no outer boundary
Lighting/mood: neutral even upper-left illumination, restrained contrast, no cast shadows
Color palette: deep pine, blue-green hemlock, charcoal moss, muted sage lichen, very small amounts of near-black trunk; green dominates strongly
Materials/textures: layered foliage and moss dominate; old wood is subordinate and dark, never a continuous brown bed
Constraints: preserve opaque square ancient-perimeter identity; visually continuous and seamless; material field only; no surrounding floor, exposed soil, dirt border, brown frame, cut logs, lumber, rocks, flowers, gameplay grid, tile outline, text, logos, watermark, perspective, or deep shadows
Avoid: checkerboard, square clumps, rows of identical trees, repeated quadrants, bare root carpet, continuous bark slabs, orange brown, obvious focal tree, crisp cellular tiling
```

## Interior cover / living understory

```text
Use case: stylized-concept
Asset type: revised seamless young-tree cover material for the Nilbots Woodland Grove 3D arena
Input images: Image 1 is the edit target and establishes the younger woodland family
Primary request: remove the exposed brown soil-and-root carpet and replace it with lower, airier living understory: overlapping fern fans, soft sapling leaf clusters, ivy, moss, and sparse slim young stems; it must read as organic green cover rather than dirt outlined into wall tiles
Scene/backdrop: one continuous younger woodland material filling the square edge to edge
Style/medium: premium stylized game material, hand-painted PBR albedo source, readable at a 40–70 pixel gameplay cell
Composition/framing: strict orthographic top-down flat material swatch; broad asymmetric understory masses with varied sizes and irregular negative pockets; loose alternation between fern, sapling leaf, and moss shapes; no isolated tree object and no boundary frame
Lighting/mood: neutral even upper-left illumination, softer and lighter than the ancient perimeter, no cast shadows
Color palette: muted fern, sage, olive moss, cool young-leaf green; tiny restrained dark stems only; green dominates strongly
Materials/textures: foliage and moss dominate; young wood is subordinate; no exposed earth bed
Constraints: preserve opaque square young-cover identity and clear distinction from the darker ancient perimeter; visually continuous and seamless; material field only; no surrounding floor, exposed soil, dirt border, brown frame, cut logs, lumber, rocks, flowers, gameplay grid, tile outline, text, logos, watermark, perspective, or deep shadows
Avoid: checkerboard, square clumps, rows, repeated quadrants, root carpet, orange or chestnut field, isolated identical ferns, bright lime, crisp cellular tiling
```
