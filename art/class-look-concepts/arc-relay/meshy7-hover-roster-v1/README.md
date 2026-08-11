# Arc Relay Meshy 7 hover roster — concept gate

This directory contains owner-review concepts only. It does not change the
canonical Arc Relay sprites, the approved Meshy T2 fleet, runtime manifests,
or replay presentation. No Meshy credits are authorized or spent at this gate.

The pass creates a fresh visual identity for all sixteen launch classes as
legless, low-hover machines. Previous Arc Relay concept art is used only as a
mechanics inventory: these concepts do not inherit its chassis shapes,
locomotion, palette assignments, or surface design.

## Meshy 7 input findings

Research was checked against Meshy's live official documentation on
2026-08-11:

- Meshy 7 is available to Image to 3D and Multi-Image to 3D as
  `ai_model: "meshy-7"`; `latest` currently resolves to Meshy 7.
- Meshy 7 Multi-Image accepts one to four separate JPG/PNG images of the same
  object. The first image is the primary/front view.
- Meshy's recommended four-view set is **front + side + back + front
  three-quarter**. Front/back/left/right is the classic cardinal alternative;
  top is optional when top geometry is uniquely important.
- Every view must show the exact same object at matching scale and camera
  distance, with consistent pose, proportions, lighting, background, colors,
  hardware, and surface details. One clean view is better than conflicting
  extras.
- Inputs should contain one clearly defined object, a plain/simple background,
  high resolution (ideally at least 1040x1040), sharp focus, even diffuse
  lighting, full uncropped silhouette, strong background separation, and no
  text or obscuring effects.
- Meshy 7 can emit PBR metallic/roughness/normal maps but currently does not
  emit an emission map. Nilbots will retain charge, beam, team tint, and hover
  emission as renderer-owned or deterministic authoring layers.

Official references:

- <https://docs.meshy.ai/en/api/changelog>
- <https://docs.meshy.ai/en/api/image-to-3d>
- <https://docs.meshy.ai/en/api/multi-image-to-3d>
- <https://help.meshy.ai/en/articles/9996860-how-to-use-meshy-image-to-3d>
- <https://help.meshy.ai/en/articles/16102789-meshy-multi-view-best-practices-angles-and-images>
- <https://docs.meshy.ai/en/webapp/image-to-3d>

## Approval and view plan

`concept-sheet.png` is the canonical owner-review artifact for this gate. Its
shared 4x4 presentation intentionally makes family consistency, palette
separation, silhouette variety, forward beam apertures, and signature apparatus
easy to compare at a glance. The four full-size files currently in `drafts/`
are generation references used to establish the sheet's shared rendering
language; they are not a partial approval requirement.

After owner approval, each approved sheet identity will be rebuilt as a clean,
isolated front-right three-quarter anchor, then receive separately generated and
approved matching views:

1. strict front — first/primary Meshy 7 input;
2. strict right side — depth, prow, and hardware profile;
3. strict back — unseen armor, hover engine, and mounted hardware;
4. approved front-right three-quarter identity anchor.

Meshy receives those four full-resolution files separately. The 4x4 concept
sheet is review evidence only and must never be submitted as one input image.

## Shared gameplay truth

- Every chassis levitates only a few centimeters above the ground. A tight
  underbody glow and visible lift modules may support a gentle bob, but there
  are no legs, feet, wheels, treads, skids, landing gear, wings, rotors, jets,
  altitude control, banking promise, or flight silhouette.
- Every class has a basic attack that winds up for one replay tick and then
  emits an instant straight energy beam. Each bot therefore has one small,
  obvious forward beam aperture with a focusing lens and two or three charge
  capacitors that can light sequentially.
- The basic beam hardware is visually separate from the class signature. The
  signature apparatus remains readable while the shared beam is idle.
- The fixed class palette dominates. Small, sharply bounded cyan team-light
  surfaces occupy about five percent of the chassis and can later become
  renderer-owned team tint.
- Concepts show one connected bot only. No Core, mine, sentry, projectile,
  wall, flare, smoke, beam, or other spawned effect appears beside it.

## Class slate

| # | Class | Fresh silhouette | Signature-support hardware | Fixed palette |
| ---: | --- | --- | --- | --- |
| 1 | Kestrel | narrow split-delta dart | compression vanes and vector heat sinks for Vector Dash | porcelain, tangerine, midnight navy |
| 2 | Palisade | broad vertical shield-slab | three-rib edge projector for Prism Wall | royal cobalt, antique gold, pale quartz |
| 3 | Towline | squat horseshoe tug | captive winch drum and closed grapple-bolt throat | cadmium yellow, brushed steel, black |
| 4 | Patchbay | rounded service capsule | dedicated articulated Repair Beam lens | seafoam, coral, pearl |
| 5 | Lantern | gyroscopic survey eye | protected flare cell, halo scanner, calibration mast | ultraviolet, champagne gold, white |
| 6 | Mortar | planted armored hex-puck | one elevated Falling Star launch tube and recoil collar | forest olive, copper, soot black |
| 7 | Minesmith | low triangular foundry pod | underside Trip Node carousel and one deployment door | terracotta, mustard, gunmetal |
| 8 | Hush | flattened obsidian ovoid | broken-ring absorber fins around a Null Field core | indigo-black, lavender, silver |
| 9 | Relay | open U-shaped catcher | empty Core cradle and twin Arc Toss rails | crimson, brass, cream |
| 10 | Switchback | mirrored twin-lobe frame | paired Exchange link coils around a strict center seam | white, black, amber |
| 11 | Longshot | long needle-lance | body-length Rail Line spine and staged charge cells | glacier blue, dark navy, silver |
| 12 | Mason | compact blocky forge | empty block bay and forked Hardlight emitters | golden ochre, blue-gray, burnt orange |
| 13 | Sunder | faceted diamond/kite | designator optic and three-stage Target Paint brackets | magenta-plum, ivory, graphite |
| 14 | Repulsor | heavy circular field-disc | concentric Kinetic Burst hub and radial coils | burgundy, rose gold, basalt |
| 15 | Veil | shallow stealth crescent | paired sealed Smoke Canister pods | emerald, smoky teal, sand |
| 16 | Nest | wide hexagonal carrier | six Sentinel Seed cells and one deployment cradle | pine green, earthen umber, aubergine |

## Signature truth checklist

| Class | Current signature fact represented by the concept |
| --- | --- |
| Kestrel | Vector Dash: one-tick tell, straight surge up to four; carried Core drops |
| Palisade | Prism Wall: three projectile-blocking, body-passable segments |
| Towline | Tractor Hook: wound-up grapple bolt, range six, pulls up to three |
| Patchbay | Repair Beam: interruptible ally channel, range four, up to two hull |
| Lantern | Survey Flare: travelling flare reveals radius four through smoke |
| Mortar | Falling Star: two-tick reticle, delayed compact area hit |
| Minesmith | Trip Node: one adjacent hull-one, two-damage mine |
| Hush | Null Field: one visible charge tick, radius-three suppression |
| Relay | Arc Toss: carrier throws the Core to a straight landing within five |
| Switchback | Exchange: one-tick link tell, swaps with one visible ally |
| Longshot | Rail Line: fixed heading, two-tick charge, piercing range-twelve beam |
| Mason | Hardlight Block: one adjacent temporary hull-three block |
| Sunder | Target Paint: marks one visible enemy for three empowered allied hits |
| Repulsor | Kinetic Burst: one-tick tell, pushes every adjacent body |
| Veil | Smoke Canister: range-six deployment, exact radius-two sight blocker |
| Nest | Sentinel Seed: adjacent hull-two sentry with deterministic beam bolts |

`prompt-specs.json` records the full per-class prompt assembly. The accepted
shared-sheet generation prompt is preserved verbatim in
`CONCEPT-SHEET-PROMPT.md`, and `manifest.json` records the review order and file
provenance. `concept-sheet.png` is the approval index and is not a Meshy input.
