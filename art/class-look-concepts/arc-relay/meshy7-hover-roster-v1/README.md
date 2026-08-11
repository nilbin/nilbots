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

`concept-sheet-v6.png` is the canonical owner-review artifact for this gate. It
removes the rejected eye lights, gun-like muzzles, and universal white segmented
nose. The roster uses the pinned elevated 58-degree gameplay-oblique camera,
bold top silhouettes, fewer micro-details, larger signature apparatus,
class-owned prow geometry, and a flush leading-edge beam seam. Earlier sheets
remain superseded evidence.

V6 separates the three previously similar low bodies: Mortar is a faceted
asymmetric launch hex, Hush is a notched four-lobed absorber, and Nest is a very
wide open twin-rack carrier. Longshot is roughly forty percent thicker. A
roster-wide authored-material pass lifts dominant hull planes into medium/light
values for the dark arena, and Palisade uses a vivid bright royal cobalt.

The current sheet's shared 4x4 presentation intentionally makes family
consistency, palette separation, silhouette variety, forward beam seams,
and signature apparatus easy to compare at a glance. The four full-size files
currently in `drafts/` are generation references used to establish the sheet's
shared rendering language; they are not a partial approval requirement.

After owner approval, each approved sheet identity will be rebuilt as a clean,
isolated front-right three-quarter anchor, then receive separately generated and
approved matching views:

1. strict front — first/primary Meshy 7 input;
2. strict right side — depth, prow, and hardware profile;
3. strict back — unseen armor, hover engine, and mounted hardware;
4. approved front-right three-quarter identity anchor.

Meshy receives those four full-resolution files separately. The 4x4 concept
sheet and its reduced proof are review evidence only and must never be
submitted as input images.

`concept-sheet-v6-48px-proof.png` is a deterministic 256x256 reduction of the
canonical sheet with no repainting. Each visible bot is roughly 48–50 pixels
wide, matching the current runtime readability gate. At that size, class
judgment is based on outer silhouette, top value blocks, signature hardware,
and facing—not seams, labels, or decorative lights.

The v6 proof keeps all sixteen outer silhouettes and their dominant signature
anchors distinguishable. The idle emitter seam can disappear entirely at that
size; a temporary renderer-owned teal/orange sweep across each bot's existing
front edge owns the one-tick runtime wind-up read.

This proof does not replace the later pinned real-replay review with both team
colors, arena lighting, movement, fog, and overlapping actors.

## Shared gameplay truth

- Every chassis levitates only a few centimeters above the ground. Visible lift
  modules, a narrow dark air gap, and a gentle bob communicate hover without a
  baked underbody glow. There are no legs, feet, wheels, treads, skids, landing
  gear, wings, rotors, jets, altitude control, banking promise, or flight
  silhouette.
- Every class has a basic attack that winds up for one replay tick and then
  emits an instant straight energy beam. Each bot therefore has one thin dark
  emitter seam recessed flush into its own leading-edge armor. There is no
  shared visible nose or charge module; a semantic renderer mask supplies the
  temporary team-colored wind-up sweep at runtime.
- The basic beam hardware is visually separate from the class signature. The
  signature apparatus remains readable while the shared beam is idle.
- Teal and orange are the opposing runtime team colors. Neither is baked into
  fixed emission. Semantic team-accent surfaces remain neutral/unlit in the
  source concept so the renderer can own teal/orange tint and bloom.
- Eye motifs are prohibited. Facing comes from the outer silhouette, prow
  wedge, signature hardware, and emitter seam; necessary sensors remain asymmetric,
  recessed industrial optics rather than a face.
- Gun-like cues are prohibited. No standard or signature system uses a barrel,
  muzzle ring, bore, rifle profile, cannon tube, or open nozzle. Mortar uses a
  closed dorsal launch iris; Longshot uses a chisel rail; Patchbay uses a flat
  service projector; Veil uses sealed canisters.
- The renderer-owned wind-up sweep, flush forward beam seam, and larger
  class-signature apparatus remain independently authored systems.
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
| 6 | Mortar | faceted asymmetric launch hex | closed Falling Star iris and polygonal recoil collar | light moss olive, warm brass, charcoal |
| 7 | Minesmith | low triangular foundry pod | underside Trip Node carousel and one deployment door | terracotta, mustard, gunmetal |
| 8 | Hush | notched four-lobed absorber | four attached absorber lobes around a Null Field core | medium indigo-plum, lavender, silver |
| 9 | Relay | open U-shaped catcher | empty Core cradle and twin Arc Toss rails | crimson, brass, cream |
| 10 | Switchback | mirrored twin-lobe frame | paired Exchange link coils around a strict center seam | white, black, amber |
| 11 | Longshot | thickened long rail barge | body-length Rail Line spine and staged charge cells | bright glacier blue, navy, silver |
| 12 | Mason | compact blocky forge | empty block bay and forked Hardlight emitters | golden ochre, blue-gray, burnt orange |
| 13 | Sunder | faceted diamond/kite | designator optic and three-stage Target Paint brackets | magenta-plum, ivory, graphite |
| 14 | Repulsor | heavy circular field-disc | concentric Kinetic Burst hub and radial coils | burgundy, rose gold, basalt |
| 15 | Veil | shallow stealth crescent | paired sealed Smoke Canister pods | emerald, smoky teal, sand |
| 16 | Nest | very wide open twin-rack carrier | six Sentinel Seed cells and a central deployment lane | pine-sage, warm umber, aubergine |

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

`prompt-specs.json` and `CONCEPT-SHEET-PROMPT.md` preserve the v1 generation
prompts verbatim. `CONCEPT-SHEET-V2-PROMPTS.md` preserves the rejected eye pass,
and `CONCEPT-SHEET-V3-PROMPTS.md` preserves the two accepted gameplay-scale
edits that established the camera and form hierarchy.
`CONCEPT-SHEET-V4-PROMPT.md` preserves the accepted gunless correction.
`CONCEPT-SHEET-V5-PROMPTS.md` preserves the accepted shared-nose removal and
Palisade signature restoration.
`CONCEPT-SHEET-V6-PROMPTS.md` preserves the silhouette-separation,
dark-arena-material, and bright-Palisade edits.
`manifest.json` records the review order and file provenance.
`concept-sheet-v6.png` is the approval index and is not a Meshy input. Future
isolated/multiview prompts inherit the v6 no-eye, no-gun, no-shared-nose,
neutral-emission, simplified-form, dark-arena value hierarchy, separated class
silhouettes, and renderer-owned wind-up rules rather than superseded prompt
language.
