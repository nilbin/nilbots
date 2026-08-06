# Sentry-color v14 provenance

- Status: concept review only; no Meshy calls and no runtime changes.
- Accepted built-in image-generation source:
  `019fc27e-33d3-7971-a896-3915521f3a0b/exec-0a44956a-3051-4608-9873-ff0be5debdc5.png`
- Output: `../concept-sheet-sentry-color-v14.png`
- Output SHA-256:
  `e18983afa59487a6cdd19434734ea8aaa63cdebe290a5f55f677677198b60e92`
- Gameplay-scale proof: `../gameplay-scale-sentry-color-v14.png`
- Gameplay-scale SHA-256:
  `e79ef743fc85fbd3404d76dfaef6a254240815ec890ebafb119689f5223da962`
- First-pass built-in source:
  `019fc27e-33d3-7971-a896-3915521f3a0b/exec-5bbdea03-3ba4-411b-81b4-d2f64fd805ec.png`
- Preserved first-pass input: `input-sentry-color-edit.png`
- Preserved first-pass SHA-256:
  `85b3aa708b92cb3d4e95d4ca75cd87b2b876f27047744b587837a6e8f300cd44`
- Exact v13 edit target: `../concept-sheet-payload-fidelity-v13.png`
- V13 SHA-256:
  `735dda9431e999bc79bb9020933cf050716d11c1786b30e430f10074bbbc787f`
- Approved deployed Sentinel Seed color/material reference:
  `../../../../signature-models/arc-relay/sentinel-seed/concept-source-v2.png`
- Sentinel Seed reference SHA-256:
  `22a8fec31336dde344424e3c7f7b0d9cf0bc7a7ea26477e3f39af8998bc9c728`
- Generation route: two built-in `image_gen` precise-object edits, with all
  inputs visible in the conversation context.

## Exact sentry-color prompt

```text
Use case: precise-object-edit.
Asset type: one-cell payload material correction on the exact Arc Relay 4x4 conceptual 3D roster.
Input images: Image 1 is the approved deployed Sentinel Seed color/material authority. Image 2 is the exact payload-fidelity v13 edit target.

Primary request: change ONLY the material colors of the two folded Sentinel Seed payloads carried by Nest at row 4 column 4 in Image 2. The payload geometry is already correct and must not change.

Match each carried sentry unmistakably to Image 1:
- large gun-head armor and angular shoulder/base armor use noticeably LIGHTER warm sandstone-taupe ceramic, clearly separated in value and hue from Nest's darker desaturated olive/khaki carrier;
- the exposed circular yaw bearing, mechanical neck, barrel, recesses, and joints use dark charcoal and medium gunmetal;
- small fasteners and bounded hardware accents use restrained weathered bronze;
- each narrow sensor slit is tiny neutral white, with no bloom;
- the short square barrel remains dark gunmetal.

Hard separation gate: the two complete sentry payloads must visibly read as two light-taupe-and-charcoal deployed Sentinel Seeds resting on an olive/khaki Nest carrier. Do not tint the sentries olive, green, military drab, or the carrier's body color. Do not repaint Nest's carrier platform, deck, cradles, shadow, or any other bot.

Hard invariants: preserve every pixel-level geometry decision, exact sixteen count, exact 4x4 order, silhouettes, payload positions, no-underbody-glow treatment, visible air gaps, detached neutral shadows, camera, scale, lighting, gray cards, all colors outside the two sentries, Minesmith's Trip Node payloads, Patchbay cross, and every named class feature. No geometry changes, no team cyan/red, no broad emission, no glow beneath bodies, no decals, labels, words, logos, watermark, effects, scenery, or extra objects. Change only the material palette of Nest's two carried sentries.
```

## Exact accepted regression-correction prompt

```text
Use case: precise-object-edit.
Asset type: one-cell palette regression correction on the exact Arc Relay 4x4 conceptual 3D roster.
Input images: Image 1 is the payload-fidelity v13 color reference. Image 2 is the exact current edit target with the corrected light-taupe Sentinel Seed payloads.

Primary request: change ONLY Mason at row 3 column 4 in Image 2. Restore Mason's circular upper construction/work ring and its two compact radial tool housings to the strong construction-yellow satin enamel shown at row 3 column 4 in Image 1. Preserve Mason's dark charcoal hover base, gunmetal center mechanism, small neutral indicator, exact geometry, position, air gap, and shadow from Image 2. Do not repaint Mason cream, beige, taupe, white, or olive.

Hard invariant: preserve the newly corrected two light warm sandstone-taupe Sentinel Seed payloads on Nest at row 4 column 4 exactly from Image 2, including their dark gunmetal yaw bearings and barrels, bronze details, and visual separation from the darker olive/khaki carrier. Preserve every other bot and pixel-level design decision from Image 2, including the complete no-underbody-glow treatment, Minesmith's three Trip Nodes, Patchbay cross, all silhouettes, camera, lighting, cards, and shadows.

No geometry change, no payload change, no glow beneath bodies, no team color, no new objects, text, labels, logos, watermark, effects, or scenery. Exactly sixteen machines. Change only Mason's upper ring/tool enamel back to construction yellow.
```
