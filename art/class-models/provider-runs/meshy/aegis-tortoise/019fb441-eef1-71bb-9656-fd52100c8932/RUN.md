# Meshy Bulwark image-led retexture gate

- Provider: Meshy
- Task: `019fb441-eef1-71bb-9656-fd52100c8932`
- Input geometry: `019fb42a-e3ed-7f18-a54e-795edf685815`
- Status: succeeded
- Credits: 10 (`685` before, `675` after)
- Style authority:
  `art/class-models/concept-targets/meshy-aegis-smart-v1/03-front-three-quarter.png`
- Settings: Meshy 6, original UV, PBR, 2K, lighting removal, GLB

## Verdict

Retain as useful source art and a possible polished skin donor. Do not ship the
provider GLB unchanged.

This pass follows the approved concept's dark/light zoning better than the
first text-led comparison. The shield, gun, reactor, and rear pod structure
remain readable at the 140-pixel review size. Its raw finish is too
chrome/pearl and noisy at the roughly 41-pixel gameplay size, but that is a
recoverable material defect: the UVs and PBR source maps can be preserved
while deterministic Blender treatment raises roughness, limits metalness, and
consolidates the broad cold fortress values.

Retexture preserved the accepted planform and 122 welded connected components.
Blender imports 12,979 nondegenerate triangles, and the normalized top
silhouette remains `91.65%` IoU against the canonical concept. The provider
output contains one fused material and no semantic team role, so renderer-owned
team glow must be authored after regrouping.

The ignored raw GLB is 9,376,064 bytes with SHA-256
`1b10881f6cbcae17781fa4f8bc40a240dcf15e6900a9990283e6e11b63aaaea3`.
Its expiring provider master and lossless maps are backed up in the local
artifact store. The committed `audit/` directory contains the corrected fixed
views, gameplay view, geometry report, and unchanged silhouette overlay.
