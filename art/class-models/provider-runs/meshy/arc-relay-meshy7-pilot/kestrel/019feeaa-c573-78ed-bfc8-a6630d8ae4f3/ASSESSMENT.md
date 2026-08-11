# Kestrel Meshy 7 pilot assessment

Task `019feeaa-c573-78ed-bfc8-a6630d8ae4f3` succeeded on 2026-08-11. It
consumed 30 credits, taking the account from 220 to 190. This was the only
paid class task submitted.

## Request

- Meshy 7 Multi-Image to 3D
- four separate 1536×1024 PNGs; strict front first
- 2K PBR textures
- remesh disabled
- image enhancement disabled
- lighting removal requested
- no pose mode or rigging
- GLB output only

`request-record.json` contains the exact input hashes and sanitized request.
`task.sanitized.json` removes input payloads and signed URL query strings.

## Raw result

- GLB: 63,573,812 bytes; SHA-256
  `04b4d86e85ee7cb172db4dfa7934e2d28fab045460a8de06b643c65d8076517b`
- 1 mesh, 1 primitive, 1 monolithic material
- 1,022,508 position vertices
- 1,976,738 triangles
- source bounds: `[-0.951313, -0.153732, -0.578075]` to
  `[0.950525, 0.155150, 0.577106]`
- source dimensions: `1.901838 × 0.308882 × 1.155181`
- longest axis: X; up: Y; inspected nose direction: -X
- four embedded JPEG texture images in the GLB; separately returned 2K base
  color, metallic, roughness, normal, and emission files are retained locally
  beside the ignored raw master

glTF-Transform 4.4.2 validation found no errors. It reported one portability
warning: the normal-mapped primitive has no authored tangents and therefore
relies on runtime-generated tangent space. It also reported the provider's
identity node matrix as informational default-transform data. Any later
candidate must generate tangents or explicitly prove the target renderer's
derivative tangent-space behavior.

The six-view review was rendered directly from the raw GLB using Three 0.185.1,
Playwright 1.62.0, neutral diffuse lighting, orthographic cameras, and no
geometry transformation. `provider-six-view-review.png` has SHA-256
`d3823a6f6fecf7fbf0397d74b75b37c6aac875e19a697d32e0b0dfd958d20ef4`.

## Gate result

Geometry is a provisional pass for a generated base mesh:

- split-delta top silhouette and low wedge profile survived;
- front/rear span and paired louvered heat exchangers remain coherent;
- the model has genuine shallow depth and rear/underside structure;
- there are no legs, wheels, eyes, humanoid cues, gun barrel, or protruding
  shared nose module.

Materials fail the approved identity:

- warm-bone ceramic became a dominant near-black/navy hull;
- muted burnt ochre became vivid saturated orange;
- the fixed orange area would collide with one runtime team color;
- the result is substantially darker and more toy-racer-like than the v7
  industrial concept.

The raw topology and 61 MB transfer also fail every runtime budget. That is
expected with remesh disabled: this file is recoverable source evidence only,
not an optimization candidate yet.

## Next gate

Do not submit another class. The narrow next experiment, if separately
authorized, is one 10-credit Meshy 7 Retexture task using this Meshy-generated
model's original UV layout and the same four multiview source images as the
style authority. It should request 2K PBR and GLB only. Pass only if the
warm-bone/ochre/navy hierarchy returns without fixed teal/orange team color,
glow, eyes, or decals. Geometry must remain byte-semantically equivalent apart
from texture/UV payload changes.
