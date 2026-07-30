# Lattice Loom clean multiview V1

Status: clean reconstruction-input derivative; provider gate passed.

No Meshy task was submitted and no provider credit was spent to create this
pack.

## Provenance

- Approved geometry source:
  `../../../../concept-targets/lattice-loom-3d-concept-v2/approval-sheet.png`
- Approved semantic-color source:
  `../../../../concept-targets/lattice-loom-3d-concept-v3/approval-sheet.png`
- V2/V3 SHA-256:
  `c208e69e8099e3b88f521a3af02471fada686161300215553d210244c25d9036` /
  `29d32230975d728a0ae72cc29540362c3aa7ddbdf8760f9964371c271bdc3db2`
- Built-in image-generation edit:
  `call_o4ZB0IitqeHKYczPx8THXS0i`
- Uncropped edited sheet: `source-sheet.png`
- Edited-sheet SHA-256:
  `4bdbdec59afa76d316d088269b050d3676c2b6cf557aeb26bd2c90f9ffcab680`

The edit removed the unsupported north/south cyan strips, retained exactly
four shoulder/fork-root team inlays, and removed the studio/floor presentation
without changing the selected low-hover chassis. `prepare-clean-inputs.mjs`
then replaced the remaining border-connected studio background with exact
`#b8b8b8`, cropped without warping, scaled every object's width to the same
990 px, and centered each on a 1254×1254 RGB PNG.

| Provider view | SHA-256 |
| --- | --- |
| `01-top.png` | `85d433e6f962ce95e8f49250d2e2b912f9ee85bfe9ed4b14c3071f466f0d6819` |
| `02-side.png` | `db1eb1c090687bb58048506b926b274a1d068c4779864aff54fe3e17fcd72da5` |
| `03-front-three-quarter.png` | `cc2d8f7c7551ffcabfcc6bb5326a078d7f717a41869737d96877b1855368090c` |
| `04-rear-three-quarter.png` | `a0c3361db16082796f234dcb4c8e951d4e2ef9a7425a0654eedf10433877e3cf` |

`contact-sheet.png` is review-only and must never be submitted as one provider
image.

## Exact cleanup and semantic-correction prompt

```text
Edit Image 1, using Image 2 only as the exact approved low-hover geometry reference, to make a clean four-view multi-image 3D reconstruction sheet for the Lattice Loom Fabricator robot. Preserve the exact same single robot identity, four camera views, diamond/open-truss planform, proportions, central hexagonal honeycomb aperture, two east/right-facing open fork arms, four integrated low-hover field nodes, all ceramic-white/graphite/copper materials, panel seams, negative spaces, surface wear, framing, and view ordering. Do not add feet, landing gear, a fabricated child, weapons, or any new geometry; this Fabricator creates separate identical instances and fabricates nothing onto itself. Semantic accent correction only: remove the unsupported cyan strips from the north/top and south/bottom cardinal tip modules; retain exactly four cyan team-owned inlays in the northwest, southwest, northeast/fork-root, and southeast/fork-root shoulder quadrants, slightly enlarging them into clean bounded shoulder/fork-root panels consistently across every view. Keep the central honeycomb core, copper coils, fork-tip lights, and underside hover emitters warm amber/orange authored class energy, never cyan. Scene cleanup only: completely remove all cast shadows, contact shadows, floor reflections, halos, vignette, and background gradients; place every unchanged view on a perfectly flat uniform neutral light-gray #B8B8B8 background with no floor plane. Preserve neutral ceramic and dark truss hardware; do not erase it as shadow. Keep every internal truss opening fully open. No labels, borders, text, logos, scenery, bloom spill, or new lights. Output one clean 3:2 four-view sheet matching Image 1's layout, suitable for lossless cropping into separate views.
```

The generated edit is not allowed to overrule the rich 2D top authority.
The retained rigid overlay against
`art/class-look-concepts/fabricator/lattice-loom/concept.png` passed at 90.96%
full silhouette IoU and 96.36% outer IoU, with no rotation, reflection, or
warp. A roughly 3% fork-tip/truss cleanup remains a Blender refinement.
