# Meshy 7 Kestrel review tiers

These are reversible review derivatives of the approved Meshy 7 Kestrel retexture. They do not replace the shipping `arc-kestrel` look.

## Source

- provider task: `019fefdd-ac96-7dc8-a61e-f0760b4ae407`
- raw GLB SHA-256: `6daeca9df3f60fd9e7260b3fd7ec3dd5ecba15505a440da4c36700cc98ae31b3`
- raw transfer size: 64,337,428 bytes
- raw geometry: 1,976,674 triangles
- raw textures: three embedded 2K maps
- normalization: 0.99 tile planform span, +Y up, yaw 180° so the nose faces +X

## Candidates

| Tier | GLB bytes | Triangles | Maps | Review GPU residency* |
| --- | ---: | ---: | ---: | ---: |
| lean | 1,674,640 | 79,064 | 3 × 512 WebP | 5,756,364 bytes |
| rich | 4,240,344 | 158,130 | 3 × 1024 WebP | 20,667,672 bytes |

\* Geometry buffers plus decoded RGBA8 texture mip chains. WebP is a review transfer format, not GPU-compressed residency.

The lean candidate removes 97.4% of the provider GLB bytes and 96.0% of its triangles. It is the selected interactive-review tier because the rich tier shows no useful improvement at the Arc Relay overview or phone camera.

Both GLBs pass `gltf-transform validate` with no errors. Each retains the known missing-authored-tangent warning for its normal map. KTX2 texture compression and a tangent decision remain mandatory before any shipping promotion.

## Reproduce

```sh
node scripts/class-models/build-meshy7-kestrel-review-tiers.mjs
node scripts/class-models/build-meshy7-kestrel-review-tiers.mjs --check
```

Exact inputs, tool versions, transforms, geometry counts, memory estimates, and output hashes are recorded in [audit.json](audit.json).
