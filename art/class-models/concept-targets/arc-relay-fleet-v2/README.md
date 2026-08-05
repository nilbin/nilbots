# Arc Relay fleet conceptual 3D v2

- Status: concept review only. No Meshy task has been submitted.
- Purpose: brighter, simpler image-to-3D targets for a possible second Arc
  Relay fleet pass.
- Canonical gameplay identity remains the sixteen `arc-*` class looks. These
  concepts may clarify depth and simplify finish, but they may not change a
  class's rule-bearing hardware or promise a new capability.
- Model-owned team color is deliberately absent. Team ownership remains a
  renderer concern.

## Generated review artifact

- High-chroma experiment: `concept-sheet-clear-color-v3.png`
- High-chroma dimensions: 1254×1254 RGBA PNG
- High-chroma SHA-256:
  `d6e2e6723169d6195e400227554fba343ab98857475893415815f27697b549bf`
- High-chroma gameplay-scale proof:
  `gameplay-scale-clear-color-sheet.png`, a 256×256 deterministic downsample
  that presents each bot at approximately 63×63 pixels.
- Built-in image-generation high-chroma output:
  `019fc27e-33d3-7971-a896-3915521f3a0b/exec-df35cdd3-f964-497c-a9fc-3d21b4dd8388.png`
- Restrained color direction: `concept-sheet-class-color-v2.png`
- Restrained source dimensions: 1254×1254 RGBA PNG
- Restrained source SHA-256:
  `af63986804a0da856b43480a59e3d3770fd74a937049f2b95df4acb9bae1acc0`
- Restrained gameplay-scale proof: `gameplay-scale-color-sheet.png`, a 256×256
  deterministic downsample that presents each bot at approximately 63×63
  pixels.
- Built-in image-generation color-pass output:
  `019fc27e-33d3-7971-a896-3915521f3a0b/exec-2c17e09a-3129-4f21-a0d5-ce6b2cdb7860.png`
- Built-in image generation output:
  `019fc27e-33d3-7971-a896-3915521f3a0b/exec-2e8a5e45-e990-4f37-b6fd-e412ae96d863.png`
- Checked-in neutral baseline: `concept-sheet.png`
- Source dimensions: 1254×1254 RGBA PNG
- Source SHA-256:
  `d7d64c98b0a1b97a223b32d8aeac0f12bfdb6a3a5c72e3b4566d75ea39bc7ab1`
- Gameplay-scale proof: `gameplay-scale-sheet.png`, a 256×256 deterministic
  downsample that presents each bot at approximately 63×63 pixels.

The generated sheets keep the exact class count/order and clear the intended
payload distinction: Minesmith carries flat marked discs, while Nest carries
two barrelled turret pods. The selected pass restores class-specific fixed
paint without adding semantic team color: each chassis keeps the shared bright
ivory/metal family, while 25–40% of its large armor panels carry one muted
ochre, olive, orange, indigo, violet, bronze, or tan class color. Emissives stay
neutral amber. Its soft studio contact shadows are acceptable for concept
review but are not approved provider inputs. If the direction is approved,
isolation must remove the shadows and produce one clean square image of at
least 1040 pixels per bot before any Meshy request.

The high-chroma experiment increases the fixed paint coverage to roughly
45–60% and uses saturated yellow, green, orange, turquoise, violet, blue,
magenta, red, purple, and lime. It has a materially stronger 63-pixel read, but
some fixed hues necessarily overlap the team-color spectrum. Emissives remain
neutral; approval still requires a real-replay check that renderer-owned team
ownership remains faster to read than class paint.

## High-chroma refinement prompt

```text
Use case: precise-object-edit.
Asset type: conceptual 3D game-bot roster high-clarity color experiment.
Input image: Image 1 is the edit target.

Primary request: change ONLY the matte class paint into much clearer, bolder, more saturated colors. Preserve the exact sixteen machines, exact four-by-four order and positions, every silhouette, geometry, proportion, signature hardware, mine discs, folded turret pods, locomotion, East/right facing, camera, scale, padding, background, materials, lighting, and rendering style. Do not redesign or move anything.

Color treatment: make 45–60 percent of each machine's large external armor panels a single strong, high-chroma class color, with the remainder bright ivory, pale titanium, and brushed metal. Use clean contiguous color blocks that remain obvious at 50–70 pixels; no tiny stripes as the main color read. Saturated premium industrial paint, not pastel, beige, muddy, faded, military-drab, or mostly white. Keep dark graphite only in mechanical recesses and underbodies. Retain realistic matte paint and metal response; do not make the bots toy plastic.

Use these unmistakable colors by fixed grid position:
row 1 col 1 Kestrel: vivid golden yellow;
row 1 col 2 Palisade: saturated emerald green;
row 1 col 3 Towline: bright safety orange;
row 1 col 4 Patchbay: vivid leaf green with crisp white medical armor;
row 2 col 1 Lantern: bright turquoise;
row 2 col 2 Mortar: strong deep olive green;
row 2 col 3 Minesmith: vivid hazard orange, with flat mine discs staying dark slate metal;
row 2 col 4 Hush: saturated violet;
row 3 col 1 Relay: vivid cobalt blue;
row 3 col 2 Switchback: strong magenta;
row 3 col 3 Longshot: saturated deep blue with a bright yellow rail stripe;
row 3 col 4 Mason: canary yellow with charcoal mechanics;
row 4 col 1 Sunder: vivid crimson-red;
row 4 col 2 Repulsor: saturated turquoise-green;
row 4 col 3 Veil: rich royal purple;
row 4 col 4 Nest: bright khaki-lime armor with dark green turret-pod housings.

Team distinction: all emissive lights remain tiny neutral warm-white or amber. No cyan/red/team-colored glow, no luminous colored paint, no team inlays. These bold colors are fixed matte class paint only; team ownership remains renderer-owned.

Constraints: exactly sixteen machines, no additions or omissions, no text, labels, borders, logos, watermark, scenery, effects, weapon fire, black ink outlines, or comic strokes. Change only color blocking and saturation; keep everything else unchanged.
```

## Class-color refinement prompt

```text
Use case: precise-object-edit.
Asset type: conceptual 3D game-bot roster color pass.
Input image: Image 1 is the edit target.

Primary request: change ONLY the fixed non-team paint colors of the sixteen machines so class color helps recognition. Preserve the exact sixteen designs, exact four-by-four positions and order, geometry, silhouettes, proportions, signature hardware, locomotion, East/right facing, camera angle, object scale, padding, neutral background, materials, lighting, and rendering style. Do not redesign, replace, add, remove, rotate, or move any machine or hardware. Minesmith must still carry flat mine discs; Nest must still carry two folded barrelled turret pods.

Keep the shared bright fleet identity: every chassis retains substantial ivory, light warm-gray, pale titanium, and brushed aluminum. Add one clearly visible restrained matte class paint color to approximately 25–40 percent of each bot's large armor panels, enough to read at 50–70 pixels. Dark graphite remains only in recesses and underbodies. All emissive lights remain tiny neutral warm-white or amber. Paint is matte material color, never glow.

Apply these distinct palettes by fixed grid position:
row 1 col 1 Kestrel: saffron-gold wing panels;
row 1 col 2 Palisade: moss-olive shield and tread armor;
row 1 col 3 Towline: burnt-orange utility panels;
row 1 col 4 Patchbay: muted sage-green medical panels;
row 2 col 1 Lantern: warm brass and pale gold;
row 2 col 2 Mortar: military olive and sand;
row 2 col 3 Minesmith: safety orange mine-rack armor, with mine discs staying slate metal;
row 2 col 4 Hush: rich muted violet dampener band;
row 3 col 1 Relay: aged copper-bronze fork panels;
row 3 col 2 Switchback: deep matte indigo twin-frame panels;
row 3 col 3 Longshot: dark blue-gray rail housing with a narrow ochre rail stripe;
row 3 col 4 Mason: construction-yellow tool-arm armor;
row 4 col 1 Sunder: royal purple optic-body panels;
row 4 col 2 Repulsor: copper-orange radial emitter vanes;
row 4 col 3 Veil: smoky lavender canister banks;
row 4 col 4 Nest: desert-tan carrier armor with dark olive turret-pod housings.

Team-color constraint: no cyan, bright blue, scarlet, or red paint; no cyan or red emission; no team-color glow or inlays. Keep adjacent bots visibly different. Preserve physical bevel/value separation with no black ink outlines or comic strokes.

Constraints: exactly sixteen machines, same order and framing, no text, labels, borders, logos, watermark, scenery, effects, weapon fire, or extra objects. Change only the class paint color blocking; keep everything else unchanged.
```

## Fixed order and silhouette contract

The review sheet uses four columns by four rows in this exact order:

1. Kestrel — narrow arrowhead hovercraft with two swept dart fins.
2. Palisade — broad, blunt treaded body dominated by one shield-projector face.
3. Towline — wheeled tug with one large open winch drum and a visible hook boom.
4. Patchbay — compact skid ambulance with two unmistakable repair arms.
5. Lantern — light hover scout with one tall circular sensor dish.
6. Mortar — squat treaded artillery body with one elevated forward-pointing tube.
7. Minesmith — wheeled carrier with exposed stacks of flat mine discs; no balls.
8. Hush — low oval hover body with one wide dampener band.
9. Relay — open forked skid body carrying one unobscured central Core cradle.
10. Switchback — mirrored twin-prong hover frame with a clear central gap.
11. Longshot — long low skid body built around one dominant rail.
12. Mason — treaded utility rig with a circular fabrication hub and two tool arms.
13. Sunder — sharp hover designator with one elevated optic mast.
14. Repulsor — round treaded body with a radial ring of large emitter vanes.
15. Veil — fast hover body with two large smoke-canister banks.
16. Nest — deliberate wheeled carrier with two folded angular micro-turret pods,
    each showing a short barrel and yaw collar; no balls and no mine discs.

## Generation brief

```text
Use case: stylized-concept
Asset type: conceptual 3D image-to-model direction sheet for sixteen distinct bots in a top-down arena game.

Reference: preserve the referenced Arc Relay roster's exact class order, East/right facing, locomotion family, broad planform, and signature hardware. Redesign the surface treatment and clarify the hardware; do not copy the dense dark finish.

Output one exact four-column by four-row review sheet in this fixed order: row 1 Kestrel, Palisade, Towline, Patchbay; row 2 Lantern, Mortar, Minesmith, Hush; row 3 Relay, Switchback, Longshot, Mason; row 4 Sunder, Repulsor, Veil, Nest. Exactly sixteen machines, one per equal cell, no omissions, duplicates, labels, text, borders, or extra objects.

Shared 3D direction: stylized hard-surface game miniatures with plausible solid depth, clean beveled major volumes, restrained panel breaks, and only a few large mechanical details. Make them visibly brighter and simpler than the reference: light warm-gray, ivory, pale titanium, and brushed aluminum armor; dark graphite only in narrow recesses and underbodies; one restrained muted class-color material where useful; tiny neutral warm-white or amber emissives. No cyan, red, or other baked team-color glow. No black ink outline, comic stroke, soot rim, or dark contour band around the silhouettes. Use physical bevel/value separation instead of drawn outlines. Avoid dense greebles, noisy seams, scratches, weathering, tiny bolts, and repeated armor scales.

Gameplay readability: each bot is recognized first by one large signature-hardware shape and second by its locomotion silhouette. Large forms must survive at 50-70 pixels. Keep generous negative spaces around hooks, forks, arms, rails, dishes, tubes, and pods. Keep every bot centered at consistent visual scale and fully inside its cell.

Class hardware: Kestrel narrow arrowhead and swept dart fins; Palisade one broad shield-projector face and treads; Towline one large open winch drum plus hook boom and wheels; Patchbay two clear repair arms and skids; Lantern one tall circular sensor dish and hover hull; Mortar one elevated forward-pointing artillery tube and treads; Minesmith obvious exposed stacks of flat low-profile mine discs in a rack, with holes or triangular sensor marks, never spherical payloads; Hush one broad dampener band on a low oval hover body; Relay one open forked cradle around an unobscured central Core socket and skids; Switchback a mirrored twin-prong hover frame with a large central gap; Longshot one long dominant rail and skids; Mason one circular fabrication hub with two chunky tool arms and treads; Sunder one elevated optic mast on a sharp hover body; Repulsor a radial ring of large emitter vanes and treads; Veil two unmistakable cylindrical smoke-canister banks on a fast hover body; Nest two folded angular micro-turret pods in cradles, each with a visible short barrel and circular yaw collar, never spherical payloads and never mine discs.

Camera and composition: every machine faces East/right in the same mild 25-degree top-front three-quarter view, showing the top plus a narrow side and underbody. Not straight top-down, not side-on, not isometric. Orthographic-like long-lens presentation with no perspective exaggeration. Soft diffuse studio lighting, perfectly flat neutral light-gray cell backgrounds, no floor plane, horizon, cast shadow, scenery, effects, weapon fire, bloom, fog, labels, logos, or watermark.

Avoid: dark muddy robots, thick black outlines, identical repeated hulls, spherical payload racks on Minesmith or Nest, tiny unreadable hardware, toy-plastic finish, walking legs, humanoids, baked team colors, atmospheric backgrounds, dramatic spotlighting, and excessive microdetail.
```

## Next gate

Owner approval of the roster sheet comes before isolation or any paid provider
request. After approval, each bot is separated into its own square lossless
input and receives the normal silhouette, camera, facing, and Meshy provenance
checks. A contact sheet is never uploaded as one Meshy task.
