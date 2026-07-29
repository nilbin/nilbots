# Meshy perimeter-straight run 019fb015

## Outcome

The task succeeded in 4 minutes 7 seconds and consumed 30 credits. It proves
that Meshy can provide genuine depth and usable PBR surface detail for an
isolated map module. It does not pass the runtime-promotion gate.

The result:

- repeats without an obvious geometric end seam;
- reads as a deep cast-metal object rather than a flat extrusion;
- includes a coherent base-color, metallic, roughness, normal, and emission
  set;
- loses the approved broad bronze wear plate and front vent hierarchy;
- replaces those landmarks with small, evenly repeated square cells;
- is substantially warmer and more emissive than the matte V4 environment
  hierarchy;
- is far too dense for a repeated runtime wall without retopology.

Verdict: retain it as a shape/surface donor and provider benchmark. Do not
promote the raw mesh, do not make the cover call yet, and do not spend another
credit until a deterministic module has been compared at runtime scale.

Review board:
`../../../../review/provider/perimeter-straight-meshy-v1-board.png`.

## Technical inventory

| Property | Value |
| --- | --- |
| Provider model axis | +Y up |
| Source bounds | min `[-0.950828, -0.442195, -0.513467]`, max `[0.948851, 0.440088, 0.511702]` |
| Source dimensions | `1.899679 × 0.882283 × 1.025169` |
| Meshes / primitives / materials | `1 / 1 / 1` |
| Vertices / triangles | `132,011 / 250,436` |
| Texture set | base color, metallic, roughness, normal, emission |
| Raw GLB | 39,588,056 bytes |
| Raw GLB SHA-256 | `45aefa699efb9bf124018cadf91877de2df20fa83967e1f5d1ae936ab98ceca1` |
| Provider preview | 186,086 bytes |
| Preview SHA-256 | `2ba0b9a761ee244d4080bc0e2bd636a8acaa179f85e73ed98375aeee3451f323` |

Raw provider files and 4K maps are retained locally and ignored by Git. The
compact preview, immutable input views, hashes, request record, and sanitized
response are the committed provenance.

## Texture hashes

| Map | SHA-256 |
| --- | --- |
| Base color | `def199d7a45711bf7e8d671d177e33a428d625c65362337313f65a560bf67f4c` |
| Metallic | `d042c3ab7b2d70ed6533fc0058a53fadcc546608e36d61279f73181344f106c1` |
| Roughness | `fd228e67524a25969af7e323cd48669c655dc2ba01d6c0097d397fef13e695d0` |
| Normal | `6e271f77146bffbffc6488b315c5431f5eb169d4b8ab3cc581571ccf58c1a970` |
| Emission | `e7cf6277534e28ad45ba50ee20455f94594cb59d78e6ecd76266e915684a964e` |

No API key, signed URL, or expiring provider URL is committed.
