# Kestrel Meshy 7 multiview candidate v1

This directory records the approved inputs for the first Meshy 7 vertical
slice. One Kestrel generation task was submitted after the four-view check:
`019feeaa-c573-78ed-bfc8-a6630d8ae4f3`. It succeeded and consumed exactly 30
credits, reducing the balance from 220 to 190. The owner then approved one
separately capped retexture task, `019fefdd-ac96-7dc8-a61e-f0760b4ae407`.
It consumed exactly 10 credits and reduced the balance to 180. No other class
was submitted.

The four proposed API inputs, in submission order, are:

1. `01-front.png` — required primary/front image;
2. `02-right.png` — strict right profile, prow pointing image-left;
3. `03-back.png` — strict rear elevation;
4. `04-front-right-three-quarter.png` — identity-reconciling oblique view.

`construction-top.png` is inference evidence only. It helped keep the
split-delta planform consistent, but it is deliberately excluded from the
four-file Meshy request. `rejected/rear-narrow.png` records the failed first
rear inference that compressed Kestrel into a narrow boxy body.

## Review gate

Approve the four input files together. They must read as one physical object
with the same broad front/rear span, shallow wedge depth, central ochre spine,
bone side armor, paired rear louvered heat exchangers, and restrained material
wear. Reject the set if the rear louvers read as glowing thrusters, if any view
suggests legs or flight controls, or if the prow reads as a gun.

The pilot used Meshy 7 Multi-Image to 3D with 2K PBR textures, no remesh, no
image enhancement, no pose mode, and GLB output only. The result remains a
generated base mesh and is not a runtime candidate.

Geometry passed the first visual gate: it retained the low split-delta wedge,
paired rear louvers, shallow profile, and gunless/legless identity. Authored
materials failed: Meshy replaced the warm-bone/ochre hierarchy with a dominant
near-black/navy hull and vivid orange regions, including a fixed color that
would collide with the orange runtime team. See the provider run's
`ASSESSMENT.md` and `provider-six-view-review.png`.

The Meshy 7 multiview retexture reused the generated model, requested original
UVs, and used the same four source images as style authority. The corrected
materials provisionally pass: warm bone dominates again, the center spine is
muted ochre rather than vivid team orange, graphite is subordinate, and there
is no teal, eye glow, or gun treatment. See its provider `ASSESSMENT.md` and
six-view review.

The retexture did not preserve geometry byte-for-byte. Meshy uniformly scaled
and recentered the source, added 238 position vertices, and removed 64 of
1,976,738 triangles. Quantized surface-position overlap remains above 99.99%
at 0.001 source units and there is no visible silhouette change, but it is a
near-equivalent re-export rather than a texture-only binary change.

The live balance check on 2026-08-11 returned 220 credits. Meshy's live API
pricing listed a textured 2K Meshy 7 multi-image task at 30 credits. The
remaining fifteen generation tasks require 450 credits; after the retexture,
the 180-credit balance is 270 credits short. Auto-rigging is out of scope.
