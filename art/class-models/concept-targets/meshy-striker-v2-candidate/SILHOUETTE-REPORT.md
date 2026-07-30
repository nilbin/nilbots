# Striker v2 candidate top-silhouette report

## Verdict

`01-top.png` preserves the canonical Trident Wasp top identity strongly enough
to remain a useful multiview-input candidate. It is not an exact geometry
source: the canonical SVG still wins wherever the generated image differs.

- Intersection over union: **94.848%**
- Canonical silhouette covered by the candidate: **98.408%**
- Candidate silhouette inside the canonical silhouette: **96.326%**
- Candidate-only area: **18,240 px** after alignment
- Canonical-only area: **7,735 px** after alignment
- Candidate area at equal width: **2.162% larger**
- Candidate height at equal width: **4.533% shorter**

The high overlap says "the same Striker," while the equal-width height
difference records a real, localized proportion drift rather than hiding it
with a non-uniform fit.

## Method

The deterministic script `measure-top-silhouette.mjs` compares:

- canonical authority:
  `../../../../web/src/assets/class-looks/trident-wasp/sprite.svg`;
- candidate: `01-top.png`.

It rasterizes the canonical SVG directly onto the candidate's native
1254×1254 canvas and thresholds alpha at 128. The candidate background is
segmented from the 16 px border using its median RGB (`123, 122, 123`) and a
fixed per-channel tolerance of 12. A four-connected flood identifies exterior
background; the largest eight-connected remainder is the candidate
silhouette.

Both sources already face East/right. The candidate is scaled uniformly by
`1.014760148` so its outer bounding-box width matches the canonical width, then
the two bounding-box centers are aligned. There is no rotation, anisotropic
fit, centroid optimization, or IoU-maximizing translation.

Run from the repository root:

```sh
node art/class-models/concept-targets/meshy-striker-v2-candidate/measure-top-silhouette.mjs
```

Exact inputs, hashes, thresholds, alignment values, bounding boxes, and raw
pixel counts are recorded in `silhouette-metrics.json`.

## Overlay and material drift

`01-top-silhouette-overlay.png` uses:

- light gray: overlap;
- magenta: canonical-only silhouette;
- cyan: candidate-only silhouette.

The material drift is concentrated in three places:

1. The candidate is slightly narrower across the upper and lower aft shoulders,
   accounting for most of the magenta fringe.
2. Its central aft tab/notch projects farther outward than the canonical
   waist, visible as cyan at the left-center edge.
3. The transitions from the core into the forward shoulders are slightly
   fuller, with small cyan shelves before the long nose taper. The extreme nose
   remains aligned and East-facing, with only small corner differences.

This measurement covers the **outer top silhouette only**. It does not approve
internal panel boundaries, the three forward channels, cyan team-accent
regions, materials, lighting, side depth, or rear/underside inference. Those
remain independent fidelity gates.
