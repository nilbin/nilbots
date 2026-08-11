# Kestrel Meshy 7 multiview retexture assessment

Task `019fefdd-ac96-7dc8-a61e-f0760b4ae407` succeeded on 2026-08-11. It
consumed 10 credits, taking the account from 190 to 180. This was the only
paid task submitted after the original Kestrel pilot; no other class was run.

## Request

- Meshy 7 Retexture
- original Meshy task `019feeaa-c573-78ed-bfc8-a6630d8ae4f3` as the model source
- the same four ordered 1536×1024 input images as multiview style authority,
  with strict front first
- original UV option enabled
- 2K PBR textures
- GLB output only
- no rigging

`request-record.json` contains the exact source-model and input-image hashes
plus the sanitized request. `task.sanitized.json` removes input payloads and
signed URL query strings.

## Raw result

- GLB: 64,337,428 bytes; SHA-256
  `6daeca9df3f60fd9e7260b3fd7ec3dd5ecba15505a440da4c36700cc98ae31b3`
- 1 mesh, 1 primitive, 1 monolithic material
- 1,022,746 position vertices
- 1,976,674 triangles
- source bounds: `[-1, -0.162412, -0.607402]` to
  `[1, 0.162412, 0.607402]`
- source dimensions: `2 × 0.324825 × 1.214805`
- three embedded 2048×2048 JPEG images: base color, combined
  metallic/roughness, and normal
- separately returned 2K base color, metallic, roughness, and normal maps are
  retained locally beside the ignored raw master

glTF-Transform 4.4.2 validation found no errors. It repeats the source model's
generated-tangent-space portability warning because the normal-mapped
primitive has no authored tangents. It also reports the identity node matrix
as informational default-transform data.

The six-view review was rendered directly from the retextured GLB using Three
0.185.1, Playwright 1.62.0, neutral diffuse lighting, orthographic cameras,
and no geometry transformation beyond temporary centering for the review
camera. `provider-six-view-review.png` has SHA-256
`1df52491d642aab84a3b179505b65513369638b4e47cb87b6099bbcd54209977`.

## Material gate

Materials provisionally pass the approved identity:

- warm-bone technical ceramic is again the dominant readable hull material;
- the central spine is muted sand/burnt ochre rather than vivid team orange;
- graphite and desaturated dark metal remain subordinate structural accents;
- there is no fixed teal, vivid orange team paint, glow, eye motif, gun, text,
  logo, or decal;
- front, side, top, and oblique views preserve the same bounded material
  hierarchy as the four approved style images.

This still needs owner approval and a real gameplay-camera proof. A close
neutral turntable cannot establish readability in the dark arena by itself.

## Geometry audit

The retexture does **not** preserve the GLB's geometry byte-for-byte despite
the original-UV option:

- the geometry fingerprint changed from
  `63a4b1ca8fe5c995d6263f742984d05b5e34ce377d1ebc09697e134252be079b`
  to `ee87bbe4383057576e362fc7c7ee8df4594018d49be8c0cc60570a451d905637`;
- Meshy recentered the bounds and applied a uniform `1.051614263×` scale;
- position accessor count increased by 238;
- triangle count decreased by 64.

After undoing the uniform scale and centering, quantized unique-position set
overlap is 99.967% at `0.0001` source units and 99.998% at `0.001` source
units. The six-view comparison exposes no meaningful silhouette or hardware
change, but the exact geometry-preservation gate fails and the output must be
treated as a near-equivalent re-export, not as the original mesh with only new
textures.

The raw 64 MB / 1.98-million-triangle output also fails every runtime budget.
It remains recoverable source evidence for a corrected and retopologized human
handoff, not runtime art.

## Next gate

Stop paid work here. The owner should first approve or reject the retextured
material identity. Do not submit the remaining fifteen classes or promote this
raw model to runtime. If the visual direction is accepted, the next technical
decision is how to retain this look while producing corrected, efficient,
semantically controlled runtime geometry.
