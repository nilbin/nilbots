# Built-in image-generation prompt record

All images in this record were produced on 2026-07-30 with Codex's built-in
`imagegen` workflow. They are concept/modeling references only. No Meshy task
was submitted. Paths are repository-relative unless noted otherwise.

The prompt text below is the exact final prompt for the checked-in byte. A
“delta” describes how that prompt changes the immediately preceding retained
variant; it is not an abbreviated replacement prompt.

## Aegis Tortoise mobile V1

- Output: `aegis-tortoise-3d-concept-v1/approval-sheet.png`
- Call: `call_tjsn1gBXIPdkxDhiHSFFwE1c`
- Image 1:
  `../../class-look-concepts/bulwark/aegis-tortoise/concept.png`
- Image 2:
  `meshy-striker-v2-candidate/03-front-three-quarter.png`
- Delta: initial 3D inference from the approved Aegis top identity, using the
  accepted Striker only for finish, depth, lighting, and presentation.

```text
Use case: stylized-concept
Asset type: Nilbots Bulwark default-class 3D concept approval sheet, later used to author consistent multiview image-to-3D inputs
Input images: Image 1 is the canonical approved exact top-down identity and silhouette for Aegis Tortoise; Image 2 is only the approved Nilbots 3D finish, depth, studio-lighting, and presentation reference. Do not copy Image 2's Striker silhouette or features.
Primary request: Infer a credible real three-dimensional Aegis Tortoise mobile ground bot from Image 1 without redesigning it. Preserve the broad near-square planform, exact East/right-facing horseshoe shield quadrant with its two 45-degree ends, exposed/open rear quarter, central octagonal energy core, four squat deployable anchor clamps, and short embedded right-facing gun. Give the shield and armor genuine layered depth, mechanically plausible seams, a low heavy underbody, visible suspension/anchor hardware, and a believable side/rear structure. It must read as a grounded fortifier that can lock down, not a hovercraft or spacecraft.
Style/medium: polished hard-surface 3D game model concept render, same grounded premium material language and realistic depth as Image 2; game-ready visual logic, restrained detail readable at the 58-degree gameplay camera.
Composition/framing: one clean four-view approval board on a uniform light-neutral studio background, no dividers or labels: strict orthographic top view largest; exact low side profile; front-right three-quarter hero view; rear-left three-quarter view. All four panels must depict the exact same chassis with camera-consistent geometry, proportions, colors, wear, and component placement. Generous padding, no crop.
Lighting/mood: neutral soft studio lighting with clear form shadows on the bot only; no dramatic environment.
Color palette: preserve Image 1's deep navy-black chassis and desaturated bronze shield panels. Use restrained neutral pale-cyan emissive strips and central core as clean bounded candidate team-accent surfaces; no cyan paint elsewhere.
Materials/textures: satin navy armor, brushed and lightly scuffed bronze shield plates, dark mechanical joints, sparse realistic edge wear, subtle normal-scale panel texture; less glossy than a showroom vehicle.
Constraints: Image 1 remains the identity authority; strict top silhouette and protected/front-vs-exposed/rear read must remain unmistakable. Exactly one design repeated across four views. No detached shield, no transparent force field, no oversized gun, no cockpit, no wings, no wheels, no tracks, no humanoid parts, no literal turtle, no extra bots, no projectile, no text, no logo, no watermark. Keep the chassis low but with real authored depth, not a simple 2D extrusion.
Avoid: spacecraft, flying saucer, tank turret, symmetric all-round armor, glossy toy plastic, excessive greebles, invented appendages, perspective drift between views.
```

## Aegis Tortoise mobile V2

- Output: `aegis-tortoise-3d-concept-v2/approval-sheet.png`
- Call: `call_MkoZZSMwntjmwz5hZtx1E2JH`
- Image 1: `aegis-tortoise-3d-concept-v1/approval-sheet.png`
- Image 2:
  `../../class-look-concepts/bulwark/aegis-tortoise/concept.png`
- Delta: material-only semantic pass intended to consolidate scattered cyan
  into four bounded team panels while preserving V1 geometry.

```text
Use case: precise-object-edit
Asset type: Nilbots Aegis Tortoise 3D concept approval sheet, semantic team-accent pass
Input images: Image 1 is the exact edit target and geometry/presentation authority; Image 2 is the canonical top identity/color reference.
Primary request: Change only the distribution and shape of the pale-cyan emissive team surfaces. Preserve the Aegis Tortoise geometry, anchor legs, top silhouette, four views, materials, navy armor, bronze shield, wear, lighting, framing, and neutral studio background exactly. Consolidate the scattered cyan into four clean, bounded, easily maskable inlay panels that remain visible at the 58-degree gameplay camera: one slim inset panel in each of the two large forward bronze shield lobes, and one compact rectangular inset on each of the two rear shoulder/anchor housings. The four panels should use the same neutral pale-cyan base and modest emissive glow, with crisp hard borders and no cyan texture. Keep the central octagonal reactor a neutral cool white-blue authored light, visibly distinct from the repaintable panels. Remove cyan from tiny seams, gun, feet, underside, and unrelated armor.
Constraints: color/material edit only; do not move, resize, add, remove, or redesign any component. Exactly the same single chassis repeated in all four views. Team panels must be semantically obvious, restrained, symmetric by chassis role, bounded within existing armor surfaces, and not dominate the bronze/navy identity. No orange team preview yet, no text, no logo, no watermark, no extra glow pools, no projectile.
Avoid: geometry drift, new armor plates, cyan paint over broad shield areas, scattered glowing pinstripes, overexposure, glossy plastic.
```

## Lattice Loom V1

- Output: `lattice-loom-3d-concept-v1/approval-sheet.png`
- Call: `call_oRN8ZSii8otSZHpMsCrlV7wM`
- Image 1:
  `../../class-look-concepts/fabricator/lattice-loom/concept.png`
- Image 2:
  `meshy-striker-v2-candidate/03-front-three-quarter.png`
- Image 3: `aegis-tortoise-3d-concept-v1/approval-sheet.png`
- Delta: initial grounded 3D inference. Images 2 and 3 controlled only shared
  presentation/detail quality.

```text
Use case: stylized-concept
Asset type: Nilbots Fabricator default-class 3D concept approval sheet, later used to author consistent multiview image-to-3D inputs
Input images: Image 1 is the canonical approved exact top-down identity and silhouette for Lattice Loom. Image 2 is the approved Nilbots Striker 3D finish/depth reference. Image 3 is the sibling Bulwark approval-board presentation and material-detail reference. Images 2 and 3 are style/presentation references only; do not copy either chassis silhouette, armor language, weapons, feet, or proportions.
Primary request: Infer a credible real three-dimensional Lattice Loom Fabricator ground bot from Image 1 without redesigning it. Preserve the lean diamond/hex planform, exact East/right-facing paired printing fork, central hexagonal honeycomb replication aperture, four short exposed triangular lattice rails, four copper material-feed coils, pale ceramic nested hex plates, small ordinary combat emitter between the fork arms, and compact node blocks at the four cardinal points. Give it genuine layered depth, open truss construction, visible feed mechanisms, a shallow technical underbody, and mechanically believable side/rear structure. It produces a separate identical instance beside itself; it never fabricates onto itself and must not carry, store, hatch, or visibly contain another bot.
Style/medium: polished hard-surface 3D game model concept render in the same grounded premium visual universe as Images 2 and 3; game-ready visual logic, restrained detail readable at the 58-degree gameplay camera.
Composition/framing: one clean four-view approval board on a uniform light-neutral studio background, no dividers or labels: strict orthographic top view largest; exact low side profile; front-right three-quarter hero view; rear-left three-quarter view. All four panels show the exact same single chassis with camera-consistent geometry, proportions, colors, wear, and component placement. Generous padding, no crop.
Lighting/mood: neutral soft studio lighting with clear form shadows on the bot only; technical and purposeful, not dramatic.
Color palette: preserve Image 1's charcoal open frame, pale off-white ceramic armor, dark lattice trusses, and restrained copper feed coils. Use the central aperture, tiny fork tips, and a few clean bounded seams as neutral warm-white/amber candidate team-accent surfaces; no amber paint elsewhere.
Materials/textures: matte ceramic plates, dark satin truss metal, brushed copper coils, precise seams, subtle micro-scratches and edge wear; less glossy than showroom plastic.
Constraints: Image 1 remains the identity authority; strict top silhouette, open truss negative spaces, and printing-fork facing must remain unmistakable. Exactly one design repeated across four views. No mini-bots, no child bay, no pod, no egg, no cargo compartment, no attached offspring, no self-upgrade growth, no factory building, no humanoid parts, no literal spider/insect, no cockpit, no wings, no oversized weapon, no projectile, no text, no logo, no watermark. Real authored depth, not a simple 2D extrusion. Keep it a grounded/light technical bot rather than a spacecraft.
Avoid: carrier or mothership, self-replicating body growth, extra detached parts, glossy toy plastic, bulky fortress armor, invented manipulators that change the top silhouette, excessive greebles, inconsistent geometry between views.
```

## Lattice Loom V2

- Output: `lattice-loom-3d-concept-v2/approval-sheet.png`
- Call: `call_q0WLLqRHUvl9zH1xMQY8MABV`
- Image 1: `lattice-loom-3d-concept-v1/approval-sheet.png`
- Image 2:
  `../../class-look-concepts/fabricator/lattice-loom/concept.png`
- Delta: locomotion-only edit replacing every ground contact with four
  integrated low-hover field nodes and a 6–10 cm gap.

```text
Use case: precise-object-edit
Asset type: revised Nilbots Fabricator Lattice Loom 3D concept approval sheet
Input images: Image 1 is the edit target and must remain the same four-view 3D model sheet; Image 2 is the canonical top-down identity authority.
Primary request: Change only Lattice Loom's locomotion/contact system so the chassis clearly levitates at a restrained low hover. Remove all little feet, legs, landing struts, walking pads, and physical ground-contact points visible beneath Image 1. Convert the four existing cardinal node blocks into compact integrated anti-gravity field nodes without changing their top-view size, position, or silhouette. Add a shallow dark technical underbody and a narrow 6–10 cm visual hover gap. In the side and both three-quarter views, show the whole chassis floating level with no component touching the ground; use four very restrained soft amber-white pools directly beneath the field nodes, consistent with the existing central aperture color. Keep the strict top view visually identical to Image 2 except for depth-consistent shading; no large glow outside the planform.
Constraints: change only the locomotion/contact system. Preserve the exact same single chassis across all four panels, exact planform, fork geometry, central honeycomb aperture, four lattice rails, copper coils, ceramic plates, proportions, surface detail, colors, camera views, neutral background, and studio lighting from Image 1. No redesign, no feet, no legs, no wheels, no tracks, no walking implication, no extra appendages, no child units, no fabrication onto itself, no projectile, no text, no logo, no watermark. Fabricator remains one instance and creates separate identical instances.
Avoid: aircraft wings, spacecraft cockpit, dramatic flight, high altitude, jet exhaust, large energy rings, excessive bloom, changing the printing fork, changing the top silhouette.
```

## Lattice Loom V3

- Output: `lattice-loom-3d-concept-v3/approval-sheet.png`
- Call: `call_0WOW5tzgO0KrMUBdQFr0OT7y`
- Image 1: `lattice-loom-3d-concept-v2/approval-sheet.png`
- Image 2:
  `../../class-look-concepts/fabricator/lattice-loom/concept.png`
- Delta: color/material-only semantic pass. The checked-in final call did not
  use the runtime SVG as an image input, so its approximate four-panel
  placement must not be treated as deterministic production placement.

```text
Use case: precise-object-edit
Asset type: Nilbots Lattice Loom levitating 3D concept approval sheet, semantic team-accent pass
Input images: Image 1 is the exact edit target and geometry/presentation authority. Image 2 is the high-resolution canonical top identity/color authority.
Primary request: Change only four small existing armor inlays into clean, bounded neutral pale-cyan renderer-owned team surfaces: two compact panels on the rear/left shoulder nodes immediately beside the upper-left and lower-left lattice rails, and two compact panels at the roots of the upper-right and lower-right East-facing printing forks. The four panels must repeat consistently in every view, be easily maskable, have crisp hard borders, a pale-cyan base, and modest emission. Keep the central honeycomb replication aperture, small fork-tip lights, copper feed coils, cardinal node lights, and all other existing energy details authored amber-gold. Remove pale cyan from everywhere else.
Constraints: preserve every part of Image 1 exactly—levitating locomotion, no feet or ground contact, shallow hover gap, four restrained amber-white field pools, exact top silhouette, fork geometry, lattice rails, coils, ceramic plates, proportions, side depth, all four camera views, materials, wear, lighting, framing, and neutral studio background. Color/material edit only; do not move, add, remove, resize, or redesign geometry. Exactly the same single chassis repeated in all views; it creates separate identical instances and never fabricates onto itself. No child units, no projectile, no text, no logo, no watermark.
Avoid: geometry drift, cyan central core, cyan hover pools, cyan fork-tip lights, broad cyan paint, scattered pinstripes, overexposure, walking feet, glossy plastic.
```

## Trident Spark V1

- Output: `projectiles/trident-spark-3d-concept-v1/approval-sheet.png`
- Call: `call_kXIa4ouJSwfAKeevcN5TQXhw`
- Image 1:
  `projectiles/trident-spark-3d-concept-v1/vector-reference.png`
- Image 1 source:
  `../../../web/src/assets/class-projectile-looks/trident-spark/sprite.svg`
- Image 2: `meshy-striker-v2-candidate/approval-sheet.png`
- Delta: initial projectile-head depth inference from the exact white-alpha
  mask; the parent sheet supplies material/finish only.

```text
Use case: stylized-concept
Asset type: Nilbots Striker default projectile 3D concept approval sheet; review-only, later one independent image-to-3D task
Input images: Image 1 is the canonical exact East/right-facing white-alpha top silhouette for Trident Spark and is the geometry authority. Image 2 is only the Trident Wasp parent chassis material, bevel, wear, depth, and studio-presentation reference; never include the bot or copy its whole chassis.
Primary request: Infer one compact real three-dimensional Trident Spark projectile head from Image 1 without redesigning it. Preserve the exact continuous fork-notched top silhouette: deep V-notched rear, long narrow central waist, two large swept outer conductor blades, one single longer East-pointing central spear, no holes, no detached prongs. Interpret it as genuine faceted volume with a raised central energy ridge, tapered outer blade prisms, shallow recessed channels, compact layered underside, and chamfered cutting edges—not a flat extrusion.
Style/medium: premium hard-surface 3D game projectile concept, compatible with Trident Wasp but clearly a separate small object.
Composition/framing: one clean 2×2 four-view approval board on uniform neutral gray, no labels/dividers: strict orthographic top view largest and facing East/right; strict side; front-right three-quarter; rear-left three-quarter. The exact same single projectile head in every view, camera-consistent geometry, colors, wear, and proportions, generous padding.
Lighting/mood: diffuse neutral studio light, crisp enough to read shallow depth.
Color/material logic: mostly neutral grayscale/value-driven PBR so the renderer can wholly recolor it by team: charcoal graphite structure, desaturated warm-metal bevels, neutral-white internal emission only, subtle scratches. No fixed cyan or orange team hue.
Constraints: projectile head only. Exactly one coherent object repeated across views. Runtime owns team tint, emission strength, trail, tracer, halo, floor wash, travel, roll, impact, and hitbox—bake none of those into the concept. Keep it compact and readable at roughly 0.42 tile live footprint.
Avoid: bot, mini-Striker, aircraft/cockpit, three separate darts, missing tail notch, broad blunt nose, extra muzzles, wings implying a vehicle, trail, halo, floor glow, motion blur, impact, text, logo, watermark, scenery.
```

## Rebound Diamond V1

- Output: `projectiles/rebound-diamond-3d-concept-v1/approval-sheet.png`
- Call: `call_r9Use93FxTXViSZV8DsgVgM2`
- Image 1:
  `projectiles/rebound-diamond-3d-concept-v1/vector-reference.png`
- Image 1 source:
  `../../../web/src/assets/class-projectile-looks/rebound-diamond/sprite.svg`
- Image 2: `aegis-tortoise-3d-concept-v2/approval-sheet.png`
- Delta: initial projectile-head depth inference from the exact white-alpha
  mask; the parent sheet supplies material/finish only.

```text
Use case: stylized-concept
Asset type: Nilbots Bulwark default projectile 3D concept approval sheet; review-only, later one independent image-to-3D task
Input images: Image 1 is the canonical exact East/right-facing white-alpha top silhouette for Rebound Diamond and is the geometry authority. Image 2 is only the Aegis Tortoise parent chassis material, heavy bevel, restrained wear, depth, and studio-presentation reference; never include the bot or copy its chassis.
Primary request: Infer one compact real three-dimensional Rebound Diamond projectile head from Image 1 without redesigning it. Preserve the exact asymmetric elongated diamond/hexagonal ring in strict top view: a real open central elongated-hex tunnel, broad top and bottom shoulders, shorter sharp West/rear point, longer sharp East/front point, plus a subtle raised rear chevron. The central aperture must remain completely open through the object. Give it genuine low armored depth with thick inner and outer bevels, a shallow crowned upper rim, segmented reflector faces, and a compact dark protected underside—not a flat extrusion.
Style/medium: premium hard-surface 3D game projectile concept, compatible with Aegis Tortoise but clearly a separate tiny projectile.
Composition/framing: one clean 2×2 four-view approval board on uniform neutral gray, no labels/dividers: strict orthographic top view largest and facing East/right; strict side; front-right three-quarter; rear-left three-quarter. The exact same single projectile head in every view, camera-consistent geometry, colors, wear, proportions, generous padding.
Lighting/mood: diffuse neutral studio light that reveals the open tunnel and bevels.
Color/material logic: nearly monochrome value-driven PBR suitable for wholesale renderer team tint: dark navy-graphite structure, desaturated bronze/steel reflector faces, neutral-white internal rim emission only, restrained scuffs. No fixed cyan or orange team hue.
Constraints: projectile head only. One coherent object repeated across views. Runtime owns team tint, emission strength, trail, halo, floor wash, travel, spin/roll, ricochet cue, impact, and hitbox—bake none into the concept. Keep compact and readable at roughly 0.40 tile live footprint.
Avoid: bot, filled aperture, shield bubble, disc, orb, boomerang, saw teeth, circular/radial ring, rotating mechanism, tank part, trail, spin depiction, halo, floor glow, impact, text, logo, watermark, scenery.
```

## Lattice Rivet V1

- Output: `projectiles/lattice-rivet-3d-concept-v1/approval-sheet.png`
- Call: `call_K07ByXIyrBQbuv51APhCxFtx`
- Image 1:
  `projectiles/lattice-rivet-3d-concept-v1/vector-reference.png`
- Image 1 source:
  `../../../web/src/assets/class-projectile-looks/lattice-rivet/sprite.svg`
- Image 2: `lattice-loom-3d-concept-v2/approval-sheet.png`
- Delta: initial projectile-head depth inference from the exact white-alpha
  mask; the parent sheet supplies material/finish only.

```text
Use case: stylized-concept
Asset type: Nilbots Fabricator default projectile 3D concept approval sheet; review-only, later one independent image-to-3D task
Input images: Image 1 is the canonical exact East/right-facing white-alpha top silhouette for Lattice Rivet and is the geometry authority. Image 2 is only the Lattice Loom parent chassis modular ceramic/truss material, depth, wear, and studio-presentation reference; never include the bot or copy its chassis.
Primary request: Infer one compact real three-dimensional Lattice Rivet projectile head from Image 1 without redesigning it. Preserve the exact outer planform in strict top view: concave V/forked West rear, long central body, stepped upper/lower mid and front shoulders, pointed East nose, a real open central diamond aperture, and the subtle raised rear diamond layer. Interpret it as a genuine compact modular fastener: faceted forward wedge, hollow lattice collar, split rear rails that remain joined to the same object, recessed truss detail, layered ceramic plates, and chamfered underside—not a flat extrusion or ordinary bullet.
Style/medium: premium hard-surface 3D game projectile concept, compatible with Lattice Loom but clearly a separate tiny projectile.
Composition/framing: one clean 2×2 four-view approval board on uniform neutral gray, no labels/dividers: strict orthographic top view largest and facing East/right; strict side; front-right three-quarter; rear-left three-quarter. The exact same single projectile head in every view, camera-consistent geometry, colors, wear, proportions, generous padding.
Lighting/mood: diffuse neutral studio light revealing the aperture, truss, and shallow depth.
Color/material logic: desaturated value-driven PBR suitable for wholesale renderer team tint: pale gray ceramic, graphite lattice structure, muted copper value accents, neutral-white internal energy only. No fixed cyan or orange team hue.
Constraints: projectile head only, one coherent object repeated across views. Runtime owns team tint, emission strength, trail, halo, floor wash, travel, roll, impact, and hitbox—bake none into the concept. Keep compact and readable at roughly 0.39 tile live footprint. The projectile never fabricates and contains no child.
Avoid: bot, mini-bot, fabrication scene, tool arms, cargo, self-assembly, cylindrical bullet, generic arrow, filled aperture, detached parts, excessive greebles, trail, halo, floor glow, impact, text, logo, watermark, scenery.
```

## Aegis Tortoise Shell V1

- Output: `aegis-tortoise-shell-3d-concept-v1/approval-sheet.png`
- Call: `call_UieJYBsjAPMFLZRKva6q5Bzm`
- Image 1: `aegis-tortoise-3d-concept-v2/approval-sheet.png`
- Image 2:
  `aegis-tortoise-shell-3d-concept-v1/vector-reference.png`
- Image 2 source:
  `../../../web/src/assets/class-looks/aegis-tortoise-shell/sprite.svg`
- Delta: first distinct deployed Shell model, inheriting material/depth from
  mobile Aegis while the Shell SVG controls the rule-bearing planform.

```text
Use case: stylized-concept
Asset type: Nilbots Bulwark Aegis Tortoise Shell 3D approval/modeling sheet
Input images: Image 1 is the approved Aegis Tortoise mobile 3D family sheet and controls the exact navy/bronze material language, mechanical depth, central core, short articulated ground-leg architecture, finish and detail density. Image 2 is the canonical East-facing Aegis Tortoise Shell sprite and controls the exact top-view silhouette, center of mass, negative spaces, protected quadrant, and team-accent placement. If they conflict, Image 2 wins for planform and rule-bearing hardware.
Primary request: Translate Image 2 into a genuinely three-dimensional deployed Shell form of the exact machine in Image 1. Produce one clean four-panel approval sheet; every panel must depict the same object with consistent geometry, not variants.
Scene/backdrop: neutral light-grey concept studio, soft contact shadow only, no environment.
Subject: a low, broad, physically planted armored machine in restrained worn stylized PBR. Preserve the concentric octagonal cool-white core; layered midnight/navy armor; warm aged bronze; exposed dark joints and braces. Its physical guard occupies only the East/right-facing 90-degree quadrant: two hard radial edges leave the core at exactly -45 and +45 degrees, the thick nested bronze/navy plate spans only between them, and the other three quarters remain visibly open and unguarded. Retain the two exposed rear braces and squat planted leg/brace pods inherited from the mobile family. The deployed Shell has no gun or muzzle. The two narrow light strips on the guard edges and the single rear light panel are clean bounded semantic team-accent inlays, shown pale cyan for neutral review and kept separate from hull paint.
Composition/framing: one four-view board with a large strict orthographic top view facing East/right and matching Image 2; 58-degree gameplay-camera oblique; strict side profile showing real plate thickness, hinges, low body and planted feet; rear three-quarter proving the other three quadrants are physically unguarded. Generous separation, no labels or text.
Materials/textures: preserve Image 1’s navy coated metal, worn warm bronze plates, dark gunmetal mechanics, fine restrained scratches and panel seams; readable at gameplay scale, matte-to-satin rather than glossy.
Constraints: exact same object and scale in all four views; real authored volume, not shallow extrusion; team strips isolated repaintable inlays; grounded, heavy and directional; protected arc stops sharply at both +/-45-degree edges. It is a distinct Shell model and animation target, not the mobile bot with a transparent overlay.
Avoid: force-field bubble, dome, 360-degree ring, shield extending behind hard quadrant edges, transparent glass shield, floating detached plate, radial symmetry, turret/scanner cues, barrel, cannon, missiles, extra weapons, hover/flight cues, tracks, wheels, cockpit, humanoid anatomy, toy plastic, excessive gloss, extra cyan wash, text, arrows, UI, watermark.
```

## Aegis Tortoise whole Turret V1

- Output: `aegis-tortoise-turret-3d-concept-v1/approval-sheet.png`
- Call: `call_d4nyZtiTKjtGDqyMEvVhZobZ`
- Image 1: `aegis-tortoise-3d-concept-v2/approval-sheet.png`
- Image 2:
  `aegis-tortoise-turret-3d-concept-v1/vector-reference.png`
- Image 2 source:
  `../../../web/src/assets/class-looks/aegis-tortoise-turret/sprite.svg`
- Delta: first whole radial Turret model, inheriting family material/depth
  from mobile Aegis while the Turret SVG controls its fourfold planform.

```text
Use case: stylized-concept
Asset type: Nilbots Bulwark Aegis Tortoise Turret 3D approval/modeling sheet
Input images: Image 1 is the approved Aegis Tortoise mobile 3D family sheet and controls navy/bronze material language, mechanical depth, central core, family construction, finish and detail density. Image 2 is the canonical Aegis Tortoise Turret sprite and controls the exact strict-top silhouette, fourfold radial symmetry, concentric hub, arm proportions, and semantic team-accent placement. If they conflict, Image 2 wins for planform and rule-bearing hardware.
Primary request: Translate Image 2 into a genuinely three-dimensional fully deployed omnidirectional Turret form of the exact machine in Image 1. Produce one clean four-panel approval sheet; every panel must depict the same whole turret object with consistent geometry, not variants and not one arm cloned in the image.
Scene/backdrop: neutral light-grey concept studio, soft contact shadow only, no environment.
Subject: a very low, broad, heavily grounded radial emplacement with one singular concentric octagonal cool-white core and exactly four equal cardinal armored arms. Preserve the canonical fourfold cross/flower planform: four equal navy outer housings, four equal warm-bronze inner weapon/brace rails, four equal clean pale-cyan semantic team-accent inlays, and the layered central octagonal hub. No arm is a nose, front, rear, or dominant cannon. The central hub and equal arms communicate that the form can address every direction without retaining a body facing. Use compact planted anchor locks and underbody mechanics inherited from the mobile family to explain stationary weight while staying inside the canonical top silhouette.
Composition/framing: one four-view board with a large strict orthographic top view aligned exactly to Image 2; 58-degree gameplay-camera oblique; strict low side profile revealing hub height, armor thickness, joints and ground locks; opposite three-quarter proving there is no privileged front or rear. Generous separation, no labels or text.
Materials/textures: preserve Image 1’s midnight/navy coated metal, worn warm bronze, dark gunmetal mechanics, restrained scratches, panel seams and satin finish; detail survives gameplay scale.
Constraints: exact same whole object and scale in all four views; strict fourfold radial symmetry; real authored depth, not shallow extrusion; four team strips are isolated repaintable inlays; grounded and unmistakably stationary; central core singular and centered. This concept is the whole radial turret, including unique hub, for a future whole-turret renderer mount path.
Avoid: tank chassis, one long cannon, one dominant arm, facing marker, asymmetry, eight giant barrels, directional nose, walker pose, Shell shield quadrant, force field, hover/flight cues, tracks, wheels, cockpit, humanoid form, toy plastic, excessive gloss, extra cyan wash, text, arrows, UI, watermark.
```
