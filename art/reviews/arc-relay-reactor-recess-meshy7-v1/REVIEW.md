# Team Reactor recess Meshy 7 review

Status: **owner approved; promoted as the runtime Team Reactor**.

## Verdict

The first Meshy 7 task passed the geometry gate: one broad octagonal ring, one
open central aperture, three low clamps, no tower, and a normalized envelope of
`1.5` tiles across by about `0.17` tile high. Its material failed because amber
edge paint resembled the orange team and the authored roughness was too low.

One owner-authorized 10-credit multiview retexture removed the amber/orange and
produced the approved neutral aged-ivory, graphite, and oxblood palette without
emission. Meshy rewrote ten triangles and uniformly rescaled that returned GLB,
so the provider retexture mesh is not the geometry source. The corrected PBR maps
are transplanted onto the exact first-pass geometry and original UVs before
normalization, facing correction, matte enforcement, and review optimization.

## Review tiers

| Tier | Transfer | Triangles | Texture maps | Decision |
| --- | ---: | ---: | ---: | --- |
| Lean | 1,635,852 bytes | 79,318 | 3 × 512 WebP | Recommended in-game trial |
| Rich | 4,148,132 bytes | 158,644 | 3 × 1024 WebP | Retain only as comparison |

The six canonical renders are visually indistinguishable at gameplay-review
scale. Across identical 480-pixel cameras, mean absolute RGB difference is under
`0.47/255` for all oblique/cardinal views and `1.22/255` for strict top. The rich
tier therefore adds about 2.51 MB transfer and 79,326 triangles without a useful
gameplay read.

## Evidence

- `provider-geometry-review/`: first-pass geometry with deterministic matte gate.
- `retexture-review/`: raw corrective retexture normalized and matte-gated.
- `lean-review/`: recommended mobile-sized candidate.
- `rich-review/`: higher-cost comparison candidate.
- `art/class-models/runtime-tiers/arc-relay/reactor-recess-review-v1/audit.json`:
  hashes, provenance, provider rewrite audit, memory estimates, and budgets.

Team ownership, charge, integrity, crest, banking pulse, damage, and destruction
remain renderer-owned. The promoted housing keeps the exact lean review bytes;
the WebGL renderer adds a low team-coloured additive pool and restrained point
light through the aperture, and lowers the existing state readouts to the rim.

## Runtime gameplay gate

A fresh Tapline-3 match (seed `9001`) ran the stock Hunter v2 and Wellwright v2
sheets for 438 ticks and ended by Reactor destruction. It contains 16 banks and
five pulses, including both team colours at both home ends. The compact broadcast
is 158,333 gzip bytes; the review installer recompresses its served sibling to
158,457 bytes. Phone-landscape validation used the exact public gallery URL and
confirmed the promoted GLB hash, WebGL canvas, gzip transport, playable overlay,
timeline seeks, both team treatments, and an empty browser error log.
