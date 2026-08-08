# Frontline class-look source prompts

All nine sources were generated separately with the built-in image-generation
tool. No reference image was supplied. The accepted generated file is retained
as `<class>/<concept>/source.png`.

Transparent `concept.png` files were derived with:

```sh
sandbox/concept-art-venv/bin/python \
  "${CODEX_HOME:-$HOME/.codex}/skills/.system/imagegen/scripts/remove_chroma_key.py" \
  --input <class>/<concept>/source.png \
  --out <class>/<concept>/concept.png \
  --auto-key border \
  --soft-matte \
  --transparent-threshold 12 \
  --opaque-threshold 220 \
  --despill
```

## Striker / Vector Kestrel

```text
Use case: stylized-concept
Asset type: Nilbots class chassis concept reference, later to be redrawn as a genuine SVG game sprite
Primary request: Design “Vector Kestrel”, a Striker-class combat bot. Its silhouette must communicate speed, long-range precision, trajectory prediction, and a one-shot three-lane volley cast. Build a narrow arrowhead/delta chassis with a long forward spine, two swept stabilizer blades, and three clearly separated but compact muzzle channels aimed East/right. Include small paired trajectory-calculation vanes and layered matte armor; keep the body mechanically plausible and distinct from a spacecraft.
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local background removal
Style/medium: polished hard-surface sci-fi game asset concept render; crisp, graphic forms suitable for faithful manual SVG interpretation; restrained surface detail that survives at 48 px
Composition/framing: exact orthographic top-down view, canonical facing East/right, one chassis only, centered with generous transparent-removal padding, chassis occupies about 70% of a square canvas
Lighting/mood: even soft upper-left studio illumination on the chassis only, high silhouette clarity
Color palette: matte graphite and cool titanium body panels, restrained pale-cyan emissive seams; do not use magenta or pink anywhere on the chassis
Materials/textures: matte anodized metal, brushed titanium edges, fine panel seams, no grime-heavy noise
Constraints: background must be one perfectly uniform #ff00ff color with no shadow, gradient, texture, reflection, floor plane, or lighting variation; one coherent chassis only; no cast or contact shadow; no projectile; no trail; no scenery; no text; no symbols; no logos; no watermark; no perspective; no isometric angle; no extra detached parts
Avoid: humanoid robot, aircraft cockpit, wings that imply flight, oversized guns, photoreal scene, excessive greebles, tiny antennae, radial symmetry
```

## Striker / Arc Viper

```text
Use case: stylized-concept
Asset type: Nilbots class chassis concept reference, later to be redrawn as a genuine SVG game sprite
Primary request: Design “Arc Viper”, a Striker-class combat bot. Its silhouette must communicate an agile ground skirmisher, long-range prediction, and projectiles that can make a single hidden 45-degree bend. Create a compact narrow wedge chassis with a deep pointed nose facing East/right, a segmented central calculation spine, and two short opposing S-curved deflection vanes hugging the body. Give it one crisp forward muzzle and three subtle parallel charge rails inside the nose to foreshadow the one-shot three-lane volley without turning it into artillery.
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local background removal
Style/medium: polished hard-surface sci-fi game asset concept render; crisp graphic forms suitable for faithful manual SVG interpretation; strong silhouette and restrained detail that survives at 48 px
Composition/framing: exact orthographic top-down view, canonical facing East/right, one compact ground-machine chassis only, centered with generous padding, chassis occupies 68–72% of a square canvas
Lighting/mood: even soft upper-left studio illumination on the chassis only
Color palette: matte black ceramic, smoke-gray armor, restrained acid-yellow emissive seams; no magenta or pink in the chassis
Materials/textures: satin ceramic plates, brushed dark metal joints, precise panel seams
Constraints: perfectly uniform #ff00ff background with no shadows, gradient, texture, reflection, floor or lighting variation; one coherent chassis only; no detached parts; no cast/contact shadow; no projectile; no trail; no scenery; no text; no symbols; no logos; no watermark; no perspective; no isometric angle
Avoid: spacecraft, aircraft wings, cockpit, humanoid robot, radial symmetry, giant tail fins, oversized gun, organic snake body, excessive greebles
```

## Striker / Trident Wasp

```text
Use case: stylized-concept
Asset type: Nilbots class chassis concept reference, later to be redrawn as a genuine SVG game sprite
Primary request: Design “Trident Wasp”, a Striker-class combat bot. Make a short, predatory, unmistakably East/right-facing ground drone whose one-shot volley is readable in silhouette: three slim forward prongs form a compact trident nose, fed by one bright central reactor; two swept shoulder blades sit close to the body and a notched rear stabilizer makes the chassis look quick rather than armored. The three prongs must be part of one chassis, not detached weapons.
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local background removal
Style/medium: premium hard-surface sci-fi game sprite concept; graphic, slightly insectile mechanical design; shapes simple enough for later genuine SVG authoring; readable at 48 px
Composition/framing: exact orthographic top-down view, canonical facing East/right, one centered chassis, generous padding, body occupies about 70% of a square canvas
Lighting/mood: even soft upper-left illumination, crisp edge definition
Color palette: dark graphite frame, warm titanium armor, restrained amber emissive channels; never use magenta or pink on the chassis
Materials/textures: matte metal, brushed edge plates, subtle heat-darkened muzzle tips, low-noise paneling
Constraints: background is perfectly uniform #ff00ff with no shadow, gradient, texture, reflection, floor plane or lighting variation; one chassis only; no loose parts; no projectile; no cast/contact shadow; no scenery; no text; no insignia; no logos; no watermark; no perspective; no isometric view
Avoid: literal insect, spacecraft cockpit, broad aircraft wings, radial symmetry, bulky tank, three separate bots, oversized cannon, excessive surface noise
```

## Bulwark / Gatehouse

```text
Use case: stylized-concept
Asset type: Nilbots class chassis concept reference, later to be redrawn as a genuine SVG game sprite
Primary request: Design “Gatehouse”, a Bulwark-class ground combat bot. The silhouette must communicate durability, deliberate anchoring, and frontal protection against incoming bolts. Create a broad low hexagonal chassis facing East/right, with a thick crenellated front gate plate, one short integrated muzzle, four heavy corner anchor shoes, and a compact central reactor. The forward 42% must be a strong self-contained shape that could be repeated around a center to form an omnidirectional turret. Keep the rear visibly less protected so facing remains obvious.
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local background removal
Style/medium: polished hard-surface sci-fi game asset concept; bold filled armor layers and crisp silhouette suitable for later genuine SVG authoring; restrained details readable at 48 px
Composition/framing: exact orthographic top-down view, canonical facing East/right, one centered ground chassis, generous padding, occupies about 70% of square canvas
Lighting/mood: even soft upper-left illumination on chassis only
Color palette: charcoal and dark gunmetal, muted oxide-red armor panels, restrained warm-white emissive core; no magenta or pink
Materials/textures: thick matte armor, worn brushed edges, inset mechanical joints, broad low-noise panels
Constraints: perfectly uniform #ff00ff background without shadow, gradient, texture, reflection, floor or lighting variation; one coherent chassis; no detached parts; no cast/contact shadow; no projectile; no energy shield drawn outside the body; no scenery; no text; no logos; no watermark; no perspective or isometric angle
Avoid: radial symmetry, circular flying saucer, spacecraft cockpit, humanoid tank, giant cannon, delicate fins, excessive greebles
```

## Bulwark / Aegis Tortoise

```text
Use case: stylized-concept
Asset type: Nilbots class chassis concept reference, later to be redrawn as a genuine SVG game sprite
Primary request: Design “Aegis Tortoise”, a Bulwark-class ground bot. Build a compact near-square plated carapace with a hard-edged horseshoe shield occupying the East/right-facing quadrant, ending visibly at two 45-degree corners. Behind it, show layered shell plates, a central energy core, four squat deployable anchor clamps, and a short embedded gun. The open rear quarter and shield boundaries must make its facing and flanking weakness immediately readable; the chassis should suggest it can lock down into a shell and later mobilize.
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local background removal
Style/medium: premium hard-surface sci-fi game sprite concept; heavy graphic armor volumes, vector-friendly geometry, readable at 48 px
Composition/framing: exact orthographic top-down, canonical facing East/right, one chassis only, centered with generous padding, about 68–72% canvas occupancy
Lighting/mood: even soft upper-left studio light on the subject only
Color palette: deep navy-black chassis, desaturated bronze shield plates, restrained ice-blue emissive seams; never magenta or pink
Materials/textures: satin armor, brushed bronze edges, sparse impact scuffs, thick broad panels
Constraints: background one perfectly uniform #ff00ff field, no shadow, gradient, floor, reflection, texture or lighting change; no detached shield; no cast/contact shadow; no projectile; no trail; no scenery; no text; no insignia; no logos; no watermark; no perspective
Avoid: literal animal, full radial symmetry, spaceship, aircraft, transparent force-field bubble, oversized gun, thin spidery legs, fine noisy detail
```

## Bulwark / Mirror Bastion

```text
Use case: stylized-concept
Asset type: Nilbots class chassis concept reference, later to be redrawn as a genuine SVG game sprite
Primary request: Design “Mirror Bastion”, a Bulwark-class combat bot focused on deflection. Make a wide diamond-shaped mobile fortress facing East/right, with two thick angled reflector cheeks meeting around a compact central muzzle, massive shoulder blocks, a bright circular power core, and four folded ground braces. The reflector cheeks should form a clear forward 90-degree wedge but remain physical armor, not a floating energy shield. The rear is blunt and mechanically exposed enough to show the protected direction.
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local background removal
Style/medium: polished sci-fi hard-surface game asset concept; clean high-end mechanical rendering, broad filled layers suitable for genuine SVG translation, strong at 48 px
Composition/framing: exact orthographic top-down view, canonical facing East/right, one centered chassis, generous padding, roughly 70% square-canvas occupancy
Lighting/mood: even upper-left studio illumination, crisp silhouette
Color palette: graphite structural frame, pale brushed steel reflector plates, restrained cobalt emissive core and seams; no magenta or pink
Materials/textures: matte graphite, brushed steel with controlled highlights, thick beveled armor
Constraints: perfectly uniform #ff00ff background, no shadow, gradient, texture, reflection, floor or backdrop lighting; one chassis only; no detached parts; no cast/contact shadow; no projectile; no glow outside silhouette; no scenery; no text; no symbols; no logos; no watermark; no perspective
Avoid: radial symmetry, spacecraft cockpit, aircraft wings, humanoid form, glass shield, giant turret barrel, overly ornate fantasy armor
```

## Fabricator / Copyforge

```text
Use case: stylized-concept
Asset type: Nilbots class chassis concept reference, later to be redrawn as one genuine SVG sprite shared by every Fabricator instance
Primary request: Design “Copyforge”, a Fabricator-class ground combat bot. It creates a separate new instance of the same chassis beside itself; it does not attach upgrades or child parts onto its own body. Give the chassis a light modular hexagonal frame, one central forge/replication core, and two compact East/right-facing assembly manipulators that clearly work outward beyond the chassis perimeter. Use repeating panel geometry and exposed structural rails to suggest a reproducible machine. The bot is fragile and technical rather than armored, yet still carries one ordinary compact combat muzzle.
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local background removal
Style/medium: polished hard-surface sci-fi game asset concept; crisp vector-friendly mechanical forms, strong at 48 px, one design used identically by separate instances
Composition/framing: exact orthographic top-down view, canonical facing East/right, one centered chassis only, generous padding, roughly 68–72% square-canvas occupancy
Lighting/mood: even soft upper-left illumination on subject only
Color palette: dark graphite frame, warm copper mechanisms, restrained mint-green emissive core and seams; no magenta or pink
Materials/textures: open dark metal frame, brushed copper tool surfaces, ceramic insulation, broad low-noise panels
Constraints: one perfectly uniform #ff00ff background with no shadow, gradient, texture, reflection, floor or lighting variation; show exactly one bot; no stored mini-bots, no onboard drones, no cargo bays, no sockets containing children, no body growing onto itself; manipulators are attached to the same coherent chassis and point outward; no cast/contact shadow; no projectile; no scenery; no text; no logos; no watermark; no perspective or isometric view
Avoid: carrier ship, mother ship, nesting doll, giant factory building, humanoid, literal crab, overly delicate antennae, heavy tank armor
```

## Fabricator / Lattice Loom

```text
Use case: stylized-concept
Asset type: Nilbots class chassis concept reference, later to be redrawn as one genuine SVG sprite shared by every Fabricator instance
Primary request: Design “Lattice Loom”, a Fabricator-class bot that assembles a separate identical instance on an adjacent tile. Create a lean diamond/hex chassis with a luminous central replication aperture, four short lattice rails, and a paired forward printing fork facing East/right. Its surface language should be modular and repeatable: nested hex plates, measured seams, small material-feed coils. Keep every mechanism part of this one chassis, aimed outward; imply copying through geometry, not through visible babies, pods, or carried drones. Include one small ordinary combat emitter.
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local background removal
Style/medium: premium sci-fi hard-surface game sprite concept; clean graphic design with broad filled layers suitable for manual SVG translation; legible at 48 px
Composition/framing: exact orthographic top-down, East/right-facing, exactly one centered chassis, generous padding, body about 70% of square canvas
Lighting/mood: even upper-left studio light on chassis only
Color palette: charcoal structure, pale ceramic armor panels, restrained amber-gold emissive aperture; never magenta or pink
Materials/textures: matte ceramic plates, dark lattice trusses, subtle copper feed coils, sparse precise panel detail
Constraints: uniform flat #ff00ff background only; no shadow, gradient, floor, reflection, texture or lighting change; exactly one bot and no separate small units; no self-upgrading visual, cargo compartments, child bays, attached offspring, projectile, trail, scenery, text, symbols, logos, watermark, perspective or isometric view
Avoid: spaceship cockpit, giant wings, carrier silhouette, literal spider, factory room, bulky fortress, excessive greebling
```

## Fabricator / Rivet Mantis

```text
Use case: stylized-concept
Asset type: Nilbots class chassis concept reference, later to be redrawn as one genuine SVG sprite shared by every Fabricator instance
Primary request: Design “Rivet Mantis”, a Fabricator-class mobile machine that spends an action to produce a separate identical machine beside itself. Make an unmistakably East/right-facing low chassis with two sturdy forward tool arms surrounding a compact rivet/welder head, a visible circular assembly core, and a narrow lightly armored rear frame. Use bilateral tool geometry and modular replaceable plates, but do not depict stored children or pieces being added to this chassis. All Fabricator units will use this same silhouette.
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local background removal
Style/medium: polished hard-surface sci-fi game asset concept, slightly industrial, crisp vector-friendly volumes, gameplay-readable at 48 px
Composition/framing: exact orthographic top-down view, canonical facing East/right, exactly one coherent chassis, centered with generous padding, 68–72% of square canvas
Lighting/mood: even soft upper-left illumination, clear silhouette
Color palette: gunmetal structure, desaturated construction-yellow armor, restrained teal emissive core; no magenta or pink on chassis
Materials/textures: matte industrial metal, replaceable enamel panels, brushed tool steel, restrained wear
Constraints: one uniform #ff00ff background with no shadow, gradient, floor, reflection, texture or lighting variation; one bot only; tool arms remain attached; no mini-bots, drones, eggs, pods, cargo bays, self-upgrades, growth, cast/contact shadow, projectile, scenery, text, hazard lettering, logos, watermark, perspective or isometric angle
Avoid: literal insect, mother ship, tank, humanoid construction robot, giant pincers, fragile antennae, excessive tiny machinery
```
