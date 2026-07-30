# Aegis Tortoise mobile clean multiview V1

Status: clean reconstruction-input derivative; provider gate passed.

No Meshy task was submitted and no provider credit was spent to create this
pack.

## Provenance

- Approved concept source:
  `../../../../concept-targets/aegis-tortoise-3d-concept-v2/approval-sheet.png`
- Approved concept SHA-256:
  `d1acf3d6af2ed71c64146a843f7564c99ede0b9384b26c28515218f4f57c3be7`
- Built-in image-generation edit:
  `call_0zpX3CxfIXJZpWOB3AGuasbU`
- Uncropped edited sheet: `source-sheet.png`
- Edited-sheet SHA-256:
  `ca6c5a24147175e4b3283b3cb23d9ab266ea0bbcc2d5eb9b5e44311a556d3a29`

The edit removed only the studio/floor presentation and retained the approved
four-view robot. `prepare-clean-inputs.mjs` then replaced the remaining
border-connected studio background with exact `#b8b8b8`, cropped without
warping, scaled every object's width to the same 990 px, and centered each on
a 1254×1254 RGB PNG.

| Provider view | SHA-256 |
| --- | --- |
| `01-top.png` | `d806e4ba3517fb8e09ed02ca41636dbb04d31e92b92a5d792b648574768aec18` |
| `02-side.png` | `5b74bdd48413aedf63a6f7a37e3d33f8f29355c8985a18c23f437ccaa0ef008f` |
| `03-front-three-quarter.png` | `279ab86b4b370706628db23edde62b959b28e85a8a372dd5046307e6efb3687e` |
| `04-rear-three-quarter.png` | `8e2067220e89b98e526c0cc3ca3c129bcbdb6becb571ca22c20db121067aa54f` |

`contact-sheet.png` is review-only and must never be submitted as one provider
image.

## Exact cleanup prompt

```text
Edit the supplied four-view approval sheet of the Aegis Tortoise mobile robot for use as multi-view 3D reconstruction input. Preserve the exact same robot identity, four camera views, geometry, top silhouette, proportions, four short articulated walking supports, embedded east/right-facing gun, reactor, panel seams, graphite-navy armor, weathered bronze plates, cyan light locations, surface wear, framing, and view ordering. Do not redesign, add, remove, rotate, mirror, stretch, or warp any robot part. The only scene change: completely remove every cast shadow, contact shadow, floor reflection, halo, vignette, and background gradient, then place each unchanged robot view against a perfectly flat uniform neutral light-gray #B8B8B8 background with no floor plane. Preserve neutral dark gun/leg hardware and all edge pixels; do not mistake them for shadow. Keep internal gaps and negative spaces open. Keep the cyan reactor and the two long forward shield slits as authored class-energy lights rather than changing their shape. No labels, borders, text, logos, scenery, glow spill, or new lights. Output one clean 3:2 four-view sheet matching the original layout, suitable for lossless cropping into separate views.
```

The generated edit is not allowed to overrule the rich 2D top authority.
The retained rigid overlay against
`art/class-look-concepts/bulwark/aegis-tortoise/concept.png` passed at 99.16%
silhouette IoU and 99.16% outer IoU, with no rotation, reflection, or warp.
