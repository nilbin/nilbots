# Aegis Tortoise Shell clean multiview V1

Status: clean reconstruction-input derivative; offline provider gate passed.

No Meshy task was submitted and no provider credit was spent to create this
pack.

## Provenance

- Owner-approved inferred-3D source:
  `art/class-models/concept-targets/aegis-tortoise-shell-3d-concept-v1/approval-sheet.png`
- Approved source SHA-256:
  `2d77b5ae7e6a96714164fc4f417d4c0cc0b4a23f13851ce4dad34208fe19f867`
- Exact top/semantic authority:
  `web/src/assets/class-looks/aegis-tortoise-shell/sprite.svg`
- Authority SHA-256:
  `935cd10f6ecbe6b006e14011218d0619160f89b78e6b20c6b6a0d7469dc03d81`
- Built-in identity-correction edit:
  `call_NB4gO1nQP2JVa0R74dfHIQ4j`
- Retained identity-correction sheet: `identity-correction-source.png`
- Identity-correction SHA-256:
  `e099012c964ae3be92b79e5284ff22ec7b993eefdca2b2f8bf85625620ae7b05`
- Built-in scene-cleanup edit:
  `call_pJcJVuqK281jv2QIRQnQe6Zo`
- Retained clean source sheet: `source-sheet.png`
- Clean source SHA-256:
  `147694c15072a7006e61bbebf1d75585addd5801a612c48d1a70fde3dd488655`

The accepted top is still inferred 3D art. The vector was used as a correction
authority; it was not pasted in as a flat replacement. The same-scale rigid
audit improved full/outer silhouette IoU from 82.37%/83.21% to
88.57%/88.58%. The retained `top-overlay.png` uses pink for SVG-only area and
cyan for clean-top-only area.

`prepare-clean-inputs.mjs` then flood-replaced only the border-connected,
nearly achromatic shadowless backdrop, cropped without warping, scaled each
object width to 990 px, and centered it on a 1254×1254 exact `#b8b8b8` RGB
canvas. The rear three-quarter retained one non-authoritative cyan corner-pod
light after scene cleanup, so the deterministic preparation step darkens only
433 color-qualified pixels inside a pinned 34×53 region. It does not alter
geometry, the three team inlays, or the class-energy core.

| Provider view | SHA-256 |
| --- | --- |
| `01-top.png` | `d39111631efa577b5efdfa16811da6a03d4a9aa8714def29ee455cf7681e26e4` |
| `02-side.png` | `98e0fed13fe0edbf613150763df7d1d53700769148a44ab54112d8e5f72c9e3f` |
| `03-front-three-quarter.png` | `eb7ccfa0e03afca30847c193a63426c19316e63ed7aa45c35cc853586fedbd91` |
| `04-rear-three-quarter.png` | `4ca9878351ed4ffb6f70ec716b9f56189fbc05cdf8fd0686dc6abea7c8573bfb` |

`contact-sheet.png` and `top-overlay.png` are review evidence. Neither may be
submitted as a provider input.

## Exact identity-correction prompt

```text
Use case: precise-object-edit
Asset type: Nilbots provider-input multiview sheet for the Aegis Tortoise Shell
Input images: Image 1 is the owner-approved inferred-3D Shell approval sheet and is the edit target. Image 2 is the authoritative exact East-facing top-planform and semantic-inlay reference; it is a guide, not an image to paste.
Primary request: Make one narrow identity-preserving production edit to Image 1. Keep the same four-view 2-by-2 board and the same genuinely three-dimensional low, heavy Shell machine. Render all four panels as the same coherent object. Change the strict top panel only as needed so its rigid silhouette and major feature placement match Image 2 materially more closely, while retaining Image 1's inferred 3D armor thickness, panel seams, weathering and physically modeled detail. Do not replace the top with flat vector art and do not nonuniformly warp it.
Exact top corrections: preserve the East/right 90-degree physical shield and hard +/-45-degree radial edges, circular/octagonal reactor, open other three quadrants, and exposed rear-support identity. Remove the extra north/south outer protrusions, restore the slimmer west/rear lobes and authoritative outer footprint, and keep no gun or muzzle. Preserve only three bounded pale-cyan semantic team inlays: the two narrow shield-edge bars and the single west-center rear panel. Darken/remove cyan from the two rear corner-pod bars. Keep the central reactor cool cyan as authored class energy, not team paint.
Cross-view consistency: keep all panels consistent with those inlay semantics and the corrected form; make the smallest corresponding geometry corrections in the oblique/side views only where required for one coherent object. Preserve the approved short planted supports, low plate thickness, materials, camera views, proportions, and all unrelated detail.
Scene/backdrop: perfectly flat uniform neutral #b8b8b8 RGB background in every panel, with no floor plane, cast shadow, contact shadow, reflection, gradient, texture, horizon, or backdrop lighting variation.
Materials/textures: preserve matte-to-satin graphite/navy coated armor, weathered warm bronze, dark gunmetal mechanics, restrained scratches and crisp panel seams. No glossy toy plastic.
Composition/framing: exactly four separated views with no overlap: strict orthographic top facing East/right, strict low side, front/gameplay three-quarter, rear three-quarter. Keep generous padding and no clipping.
Constraints: change only the specified silhouette, semantic-inlay, and background/shadow details; preserve the approved object's identity, true depth, material hierarchy, layout, camera roles, and all other geometry. No added objects. No force field, dome, ring, weapon, hover cue, text, labels, arrows, UI, logo, or watermark.
```

## Exact scene-cleanup prompt

```text
Use case: precise-object-edit
Asset type: clean multiview 3D-reconstruction input sheet
Input image: Image 1 is the corrected Aegis Tortoise Shell four-view sheet and is the edit target.
Primary request: Preserve the exact same Shell object pixels, identity, corrected top silhouette, geometry, proportions, all four camera views, view ordering, framing, three semantic team inlays, central class-energy core, supports, armor, panel seams, colors, materials, surface wear, internal gaps, and edge detail. Do not redesign, add, remove, rotate, mirror, stretch, warp, repaint, relight, or reinterpret any part of the machine.
The only scene change: completely remove every cast shadow, contact shadow, floor reflection, halo, vignette, background glow and background gradient. Place each unchanged object view against one perfectly flat, perfectly uniform neutral light-gray #B8B8B8 RGB background with no floor plane and no lighting variation anywhere.
Critical preservation: preserve every neutral dark gunmetal support, rear mechanism, planted foot, underside component, antialiased edge pixel, recess and open negative space. Do not erase or recolor neutral hardware as if it were shadow. Object shading and material texture on the machine remain unchanged; only the external background and cast/contact shadows change.
Constraints: output one clean 3:2 four-view sheet matching the source layout, suitable for lossless cropping. No labels, borders, text, logos, scenery, new lights, glow spill, watermark, or extra objects.
```

A later built-in edit (`call_QZzg0iD0WQ7lzwLVzB8mzrfa`) tried to remove the
remaining corner-pod light, but it retained a cyan remnant and reintroduced a
background gradient. Its output is rejected and not part of this package.
