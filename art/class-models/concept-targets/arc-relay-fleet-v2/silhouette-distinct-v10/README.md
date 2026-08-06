# Silhouette-distinct v10 provenance

- Status: concept review only; no Meshy calls and no runtime changes.
- Accepted built-in image-generation source:
  `019fc27e-33d3-7971-a896-3915521f3a0b/exec-0e1f8066-b657-415b-9944-7cc55d19e4ed.png`
- Output: `../concept-sheet-silhouette-distinct-v10.png`
- Output SHA-256:
  `0d2ee8c349b1851aab2b8cb42830899f11d6863da3cda78a1a3527e875362714`
- Gameplay-scale proof: `../gameplay-scale-silhouette-distinct-v10.png`
- Gameplay-scale SHA-256:
  `9323be26b9c12e0443c029f5e187d19fcf4203b14d94d5180032e25f84f1beb1`
- First-pass built-in source:
  `019fc27e-33d3-7971-a896-3915521f3a0b/exec-00c7e61c-472b-4586-aae6-afb7659bd6b2.png`
- Preserved first-pass input: `input-after-identity-edit.png`
- Preserved first-pass SHA-256:
  `033d76c7173c04a941489deed2307e55eb58771543f0f9b69f867a975c28cb6f`
- Exact v9 edit target: `../concept-sheet-hover-explicit-v9.png`
- V9 SHA-256:
  `d3a02a1a0b1f3b0b69f957a0d85683572e1a0f1fc8809182837ea955c6f4bf62`
- Canonical 2D identity reference:
  `../../../../class-look-concepts/arc-relay/premium-roster-v1/concept-sheet.png`
- Canonical reference SHA-256:
  `462af9a32953758100ade6dc8e12455b2484baacdc0410afc4ca35f8c3dc89b1`
- Generation route: two built-in `image_gen` precise-object edits, with all
  inputs visible in the conversation context.

## Exact first-pass prompt

```text
Use case: precise-object-edit.
Asset type: class-identity and silhouette correction for the exact 4x4 Nilbots Arc Relay conceptual 3D roster.
Input images: Image 1 is the canonical older Arc Relay 2D roster and is reference authority only for class identity and rule-bearing hardware. Image 2 is the exact hover-explicit v9 edit target and controls composition, geometry quality, clean materials, clear class colors, hover treatment, lighting, background, camera, scale, and all unchanged cells.

Primary request: fix two specific failures in Image 2:
1. Restore Patchbay's unmistakable field-medic identity with a large white medical cross that survives at 63 pixels.
2. Eliminate the repeated twin-tip / twin-prong nose language across the roster. Relay alone keeps a deliberate open two-prong front because its Core cradle requires it.

Preserve the exact 4x4 order:
row 1 Kestrel, Palisade, Towline, Patchbay;
row 2 Lantern, Mortar, Minesmith, Hush;
row 3 Relay, Switchback, Longshot, Mason;
row 4 Sunder, Repulsor, Veil, Nest.

Hard edit scope:
- Row 1 col 1 Kestrel: replace the split twin-tip nose and widely separated forward darts with ONE continuous extremely narrow single-point delta/lance nose on a fast swept hover planform. It may retain two rear swept fins, but the East/right front terminates in exactly one dominant central point. No open fork and no paired forward tips.
- Row 1 col 4 Patchbay: replace the open two-arm/twin-tip front silhouette with a broad compact cruciform or rounded-plus hover platform. Put ONE large, clean, unmistakable matte white medical cross on the top central green medical-equipment bay, covering enough surface to read clearly at 63 pixels. The cross is a painted/inlaid non-emissive medical identifier, not text and not team color. Keep one articulated repair manipulator folded above or beside the body as secondary hardware, but no paired forward arms and no forked nose.
- Row 2 col 1 Lantern: replace the two-pontoon/twin-tip body with ONE asymmetric crescent-teardrop scout hull ending in one compact rounded East/right nose. Preserve the one tall circular sensor dish and narrow mast as the dominant identity. No open front channel, no paired forward tips.
- Row 3 col 1 Relay: KEEP its unmistakable blunt open U-shaped two-prong chassis and unobscured central Core cradle. This must be the only open twin-tip/tuning-fork front in the entire roster.
- Row 3 col 2 Switchback: close the current twin-prong/boomerang frame into one continuous asymmetric zigzag or closed diamond-loop hover planform with a single offset East/right nose. Preserve the mirrored reversal/counter-steer mechanical idea through opposing swept side masses and a central negative-space aperture, but the forward outline must not end in two tips and must not resemble Relay.
- Row 4 col 3 Veil: replace the twin-pontoon/catamaran nose with ONE broad blunt wedge/lozenge smoke-layer hull, clearly recalling Image 1's compact single-body silhouette. Preserve two large visible banks of cylindrical smoke canisters mounted on top or shoulders. The hull has one continuous East/right front edge or single central nose, with no central front cleft and no paired forward tips.

Counting gate: exactly ONE visibly open twin-prong/twin-tip front in the entire 4x4 roster—Relay at row 3 col 1. Kestrel, Patchbay, Lantern, Switchback, and Veil must each have a plainly different single-body outline, and no other cell may acquire a forked or paired nose.

Invariants: preserve every other bot and cell exactly from Image 2, including Palisade, Towline, Mortar, Minesmith, Hush, Longshot, Mason, Sunder, Repulsor, Nest; preserve all signature hardware, exact tracked-vs-hover distribution, visible low-hover air gaps, recessed underside field hardware, detached shadows for the twelve hover bodies, planted contact shadows for the four tracked bodies, East/right facing, broad mid-value armor, sparse seams, realistic metal/enamel roughness, restrained amber underside light, gray cards, gutters, padding, scale, camera, and lighting.

No black outline network, dense plate mosaic, toy simplification, candy gloss, rounded children's-toy proportions, team-colored cyan/red glow, broad bloom, decals other than Patchbay's required white medical cross, labels, words, numbers, logos, watermark, scenery, weapon fire, fog, or extra objects. Exactly sixteen machines. At 50–70 pixels, Patchbay's cross and all six corrected silhouette categories must remain obvious.
```

## Exact accepted correction prompt

```text
Use case: precise-object-edit.
Asset type: one-cell material correction on the exact Arc Relay 4x4 conceptual 3D roster.
Input images: Image 1 is the hover-explicit v9 palette/material reference. Image 2 is the exact current edit target.

Primary request: change ONLY row 3 column 1, Relay, in Image 2. Repaint Relay's broad brown armor to the clear cobalt-blue enamel and pale titanium inner-rail palette shown for Relay at row 3 column 1 in Image 1. Replace the large bright cyan/blue glowing Core with a non-team central cradle: layered pale titanium and medium gunmetal mechanical surfaces around one TINY neutral warm-white or amber indicator. The central cradle remains visible and mechanically important, but must not read as a blue energy orb, team-colored light, or broad emission.

Preserve Relay's exact current open blunt U-shaped two-prong geometry, proportions, negative space, low-hover air gap, underside hardware, detached shadow, East/right facing, camera, scale, and position from Image 2. Relay must remain the only open twin-tip chassis.

Hard invariant: every pixel-level design decision outside row 3 column 1 remains unchanged from Image 2. Preserve Patchbay's large white medical cross and compact cruciform platform; preserve the newly single-nose Kestrel, Lantern, Switchback, and Veil silhouettes; preserve all sixteen bots, all hardware, tracked-vs-hover distribution, paint colors in other cells, clean broad surface treatment, gray cards, gutters, lighting, and shadows.

No geometry redesign anywhere. No cyan or blue glow on Relay, no team color, no broad bloom, no black outline network, no text, labels, logos, watermark, effects, scenery, weapon fire, fog, additions, omissions, or extra objects. Exactly sixteen machines. Change only Relay's paint/material color and Core emission treatment.
```
