# Kestrel Meshy 7 multiview candidate v1

This directory records the approved inputs for the first Meshy 7 vertical
slice. One Kestrel task was submitted after the four-view check:
`019feeaa-c573-78ed-bfc8-a6630d8ae4f3`. It succeeded and consumed exactly 30
credits, reducing the balance from 220 to 190. No other class was submitted.

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

The next paid option is a separately capped 10-credit Meshy 7 multiview
retexture that reuses the generated UVs and the same four source images. It is
not yet authorized.

The live balance check on 2026-08-11 returned 220 credits. Meshy's live API
pricing listed a textured 2K Meshy 7 multi-image task at 30 credits. The full
sixteen-model fleet therefore requires 480 credits; the current account is 260
credits short before the pilot and still 260 credits short after it. Auto-rigging is
out of scope.
