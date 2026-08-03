# Arc Mason Smart Topology T2 result

This is the one-call Arc Relay production-path timing requested on 2026-08-02.
The provider candidate remains art-side and does not replace the reviewed runtime
fleet asset.

## Call

- task: `019fc335-af21-735b-ac08-6215f3031b69`;
- source: `web/src/assets/class-looks/arc-mason/sprite.png`, 192x192,
  SHA-256 `8adbc038d3aa203074b5105cea0e878722707deaac555b94a80f31b17d77c3df`;
- request: Image to 3D, `model_type: smart-topology`, `ai_model: meshy-t2`,
  15,000 target faces, textured 4K PBR, GLB only;
- result: success on the first call, no reroll;
- credits: 15; balance 535 before and 520 after.

## Measured timing

| Stage | Seconds |
| --- | ---: |
| Submit request | 0.64 |
| Provider generation | 220.08 |
| Download all 12 returned artifacts | 12.86 |
| Provider stage total | **234.30** |
| Deterministic lay-flat/+X/floor/tile normalization | 0.04 |
| 1024px WebP PBR optimization | 0.74 |
| Fixed 58-degree comparison load and capture | 1.14 |
| Transparent 192px sprite render | 0.77 |
| Review production build | 3.51 |
| Full primary/swapped 2D/3D replay smoke | 56.43 |

With the tooling now present, submission through a normalized GLB and derived
2D sprite is approximately **3 minutes 57 seconds**. Including a fresh review
production build and the exhaustive four-capture replay smoke is approximately
**4 minutes 57 seconds**.

The initial first-run wall clock was **14 minutes 43.9 seconds** from submission
to the first saved runtime-smoke evidence. Review then exposed an orientation
error: Meshy had interpreted the top-down reference as a side elevation. The
candidate was deterministically rolled -90 degrees around X and recaptured; this
used no additional provider call or credits. The remaining difference is one-time
authoring and diagnosis: the secure runner, provider normalization, comparison
page, sprite renderer, and repair of the review install step were built during
this run.

## Output

- raw provider GLB: 29,362,584 bytes;
- generated mesh: 14,278 triangles, 10,045 vertices;
- provider scene: one node, one mesh, one primitive, one fused material;
- corrected bounds: 0.900 wide x 0.287 high x 0.638 deep;
- normalized review GLB: 678,556 bytes with 1024px WebP PBR maps;
- derived transparent 2D sprite: 36,856 bytes at 192x192;
- glTF validation: zero errors, one generated-tangent-space portability warning;
- production replay smoke: four captures, zero page errors, console errors, or
  failed requests; primary 3D first-frame readiness was 2,901 ms.

The model intentionally has no semantic team-tint material. Its small lights and
yellow armor are fixed class hardware. Team ownership remains in renderer-owned
floor light and existing arena cues.

## Honest result

The corrected fixed-camera comparison is materially richer: the T2 candidate has
true undercuts, coherent construction arms, better PBR, and no heavy graphic
silhouette outline. The provider output did not initially retain the intended
world orientation; the pinned review caught the side-elevation interpretation.
The deterministic lay-flat transform restores the intended +X-facing planform
without a reroll.

At real arena scale the candidate remains legible, but much of its fine texture
compresses into noise and it reads more like a mechanical crab than the current
treaded Mason. It is thinner and less planted, and treads are not clearly
represented. Meshy's documented separated-part benefit did not arrive as named
scene parts in this output: the result is one fused mesh, so the renderer cannot
apply independent hardware lag without a later connected-component/mesh split.

The original per-class arena evidence also enlarged a 104x104 source crop to
220x220. The corrected `mason-arena-3d.png` uses a native 220x220 arena window
with no resize, so bot scale and surrounding tile context are honest.

## Tactical scale check

A deterministic +10% variant increases the normalized planform span from 0.90
to 0.99 tile. After Mason's existing renderer scale, its maximum width is about
0.97 tile, retaining a small occupancy margin. It required no provider call.
`mason-tactical-scale-comparison.png` compares both variants at the identical
20-tile tactical camera with native arena pixels and no crop enlargement. The
larger variant is saved as `mason-normalized-large-review.glb`; it remains
art-side and does not replace the reviewed runtime fleet asset.

The route is therefore proven as a fast visual-quality upgrade and as a source
for deterministic 2D rendering. It is not yet a zero-cleanup replacement for
class-specific locomotion or articulated-node contracts.

Evidence:

- `review.png` — current relief beside T2 at the pinned camera;
- `mason-arena-3d.png` — both team assignments at real arena scale;
- `arena-primary-3d.png` — full production replay frame;
- `mason-sprite-from-t2.png` — transparent 2D derivative;
- `mason-tactical-scale-comparison.png` — 0.90 versus 0.99 tile at the
  identical tactical camera;
- `runtime-smoke.json` — browser timing and zero-error record.
