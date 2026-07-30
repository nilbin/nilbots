# Aegis Tortoise whole Turret clean multiview V1

Status: clean reconstruction-input derivative; offline provider gate passed.

No Meshy task was submitted and no provider credit was spent to create this
pack.

## Provenance

- Owner-approved inferred-3D source:
  `art/class-models/concept-targets/aegis-tortoise-turret-3d-concept-v1/approval-sheet.png`
- Approved source SHA-256:
  `b3df8d605c734c7a078d63dd6d7b98848337c67755d4205b427ecd6c27cf4b0f`
- Exact top/semantic authority:
  `web/src/assets/class-looks/aegis-tortoise-turret/sprite.svg`
- Authority SHA-256:
  `f2a471ce82285a8bba848e193e4d3a3e4d23567ddf6da2777f811c230a12dd04`
- Built-in identity-correction edit:
  `call_WHppoBUWfvOWEv8Yz4lKm4Vh`
- Retained identity-correction sheet: `identity-correction-source.png`
- Identity-correction SHA-256:
  `d0398fe70a42960ed0936b34ad94c4fb3f76bdeeac75c8c377f48c2876610a89`
- Built-in scene-cleanup edit:
  `call_KaWJBC21a1Fv1aEMd1nRDop2`
- Retained clean source sheet: `source-sheet.png`
- Clean source SHA-256:
  `40d060a84ec2d77d6f0f496526c4e08792a932408139d9a579b44acf494ac555`

The accepted top is still inferred 3D art. The vector was used as a correction
authority; it was not pasted in as a flat replacement. The same-scale rigid
audit improved full/outer silhouette IoU from 80.16%/80.57% to
85.85%/85.84%. The retained `top-overlay.png` uses pink for SVG-only area and
cyan for clean-top-only area.

`prepare-clean-inputs.mjs` then flood-replaced only the border-connected,
nearly achromatic shadowless backdrop, cropped without warping, scaled each
object width to 990 px, and centered it on a 1254×1254 exact `#b8b8b8` RGB
canvas.

| Provider view | SHA-256 |
| --- | --- |
| `01-top.png` | `c9ca639173a8744ff1c0475da451eca29ef1ea8828fd1399289fb0facd577ca2` |
| `02-side.png` | `87fe51f18efedb8f1b5160899715bdd6c4efb34d19b1a04738db6492a908adc2` |
| `03-front-three-quarter.png` | `404b12a82bf21a0b22f492bac2190ec4b5562cf1ae18a8baf23d4b204a029bb4` |
| `04-rear-three-quarter.png` | `c848dd98a7172ae28e718651b31fd4a46c1488ddc663d62f80809f157a5dfebd` |

`contact-sheet.png` and `top-overlay.png` are review evidence. Neither may be
submitted as a provider input.

## Exact identity-correction prompt

```text
Use case: precise-object-edit
Asset type: Nilbots provider-input multiview sheet for the whole Aegis Tortoise Turret
Input images: Image 1 is the owner-approved inferred-3D whole-Turret approval sheet and is the edit target. Image 2 is the authoritative exact strict-top planform and semantic-inlay reference; it is a guide, not an image to paste.
Primary request: Make one narrow identity-preserving production edit to Image 1. Keep the same four-view 2-by-2 board and the same genuinely three-dimensional very low, heavy radial Turret. Every panel must depict the same whole object, never one cloned arm. Change the strict top panel only as needed so its rigid silhouette and major feature placement match Image 2 materially more closely, while retaining Image 1's inferred 3D armor thickness, panel seams, wear, mechanics and physically modeled detail. Do not replace the top with flat vector art and do not nonuniformly warp it.
Exact top corrections: preserve strict fourfold symmetry, one singular centered concentric octagonal reactor, exactly four equal cardinal armored pods/arms, and exactly four equal bounded pale-cyan team inlays. Add the missing navy diagonal shoulder webs around the hub so the outer footprint follows Image 2's octagonal/star cross silhouette rather than a sparse plus. Slightly shorten/trim all four terminal pod tips equally to match the authority. Preserve the authoritative fourfold arm proportions and negative spaces. Keep the central reactor cool cyan as authored class energy, not team paint. No arm is a front, nose, rear, or dominant cannon.
Cross-view consistency: carry the same four diagonal shoulder webs and equal trimmed arms through both three-quarter views and the low side where visible, making only the smallest changes required for one coherent whole object. Preserve compact planted locks and mechanics inside the canonical footprint.
Scene/backdrop: perfectly flat uniform neutral #b8b8b8 RGB background in every panel, with no floor plane, cast shadow, contact shadow, reflection, gradient, texture, horizon, or backdrop lighting variation.
Materials/textures: preserve matte-to-satin graphite/navy coated armor, weathered warm bronze, dark gunmetal mechanics, restrained scratches and crisp panel seams. No glossy toy plastic.
Composition/framing: exactly four separated views with no overlap: strict orthographic top, strict low side, gameplay three-quarter, opposite three-quarter. Keep consistent object scale, generous padding and no clipping.
Constraints: change only the specified shoulder-web, terminal-length, background/shadow, and consistency details; preserve the approved object's identity, true depth, material hierarchy, layout, camera roles, and all unrelated geometry. Whole radial Turret including its unique hub, not a single arm and not four disconnected objects. No tank chassis, long cannon, asymmetry, directional marker, Shell shield, force field, hover cue, text, labels, arrows, UI, logo, or watermark.
```

## Exact scene-cleanup prompt

```text
Use case: precise-object-edit
Asset type: clean multiview 3D-reconstruction input sheet
Input image: Image 1 is the corrected whole Aegis Tortoise Turret four-view sheet and is the edit target.
Primary request: Preserve the exact same whole radial Turret object pixels, identity, corrected top silhouette, four diagonal shoulder webs, four equal trimmed cardinal arms, singular hub, strict fourfold symmetry, geometry, proportions, all four camera views, view ordering, framing, four semantic team inlays, central class-energy core, planted locks, armor, panel seams, colors, materials, surface wear, internal gaps, and edge detail. Do not redesign, add, remove, rotate, mirror, stretch, warp, repaint, relight, or reinterpret any part of the machine.
The only scene change: completely remove every cast shadow, contact shadow, floor reflection, halo, vignette, background glow and background gradient. Place each unchanged object view against one perfectly flat, perfectly uniform neutral light-gray #B8B8B8 RGB background with no floor plane and no lighting variation anywhere.
Critical preservation: preserve every neutral dark gunmetal support, underbody mechanism, ground lock, planted foot, antialiased edge pixel, recess and open negative space. Do not erase or recolor neutral hardware as if it were shadow. Object shading and material texture on the machine remain unchanged; only the external background and cast/contact shadows change.
Constraints: output one clean 3:2 four-view sheet matching the source layout, suitable for lossless cropping. The subject is one whole radial turret in every panel, not a cloned arm. No labels, borders, text, logos, scenery, new lights, glow spill, watermark, or extra objects.
```
