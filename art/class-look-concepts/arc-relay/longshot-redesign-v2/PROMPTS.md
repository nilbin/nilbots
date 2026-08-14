# Longshot redesign v2 prompts

The three oblique candidates used the exact common prompt below plus the named
candidate addendum. Each was generated in a separate built-in `imagegen` call.

## Common oblique prompt

```text
Use case: stylized-concept
Asset type: Nilbots Arc Relay LONGSHOT bot redesign candidate for owner silhouette approval and later Meshy 7 multiview generation
Primary request: create one premium stylized 3D hard-surface concept render of a completely legless levitating Longshot machine. Longshot is a fragile fixed-heading corridor threat whose normal attack winds up then produces a beam, and whose Rail Line signature publicly charges a piercing line. Communicate this through a broad integrated front beam shutter and exposed high-energy capacitor hardware, never through a gun.
Scene/backdrop: perfectly uniform warm light-gray studio background, no floor grid, no scenery, no cast shadow, no hover glow
Style/medium: mature industrial game-asset concept render, stylized realism, chunky readable proportions, modest bevels, broad surfaces, restrained wear, low micro-detail, designed to remain distinctive at 18–48 gameplay pixels
Composition/framing: exactly one isolated rigid robot, centered with generous padding, orthographic pinned gameplay-oblique view from above; front faces screen-right/East; show top, front, and one side clearly; entire chassis visible
Lighting/mood: soft diffuse neutral studio lighting, no dramatic highlights, bloom, baked glow or reflections
Color palette: medium-light dusty steel/denim-blue armor, warm light-gray ceramic capacitor housings, dull nickel conductors, medium graphite mechanisms, tiny muted burgundy insulator details. No teal/cyan and no orange because those are runtime team colors. Nothing very dark, bright, saturated, or toy-like.
Materials/textures: high-roughness matte powder-coated metal, matte ceramic, dull brushed nickel; no glossy plastic, chrome, wet varnish, candy paint, or mirror reflections
Shared constraints: silhouette must be broad, compact, substantial, and meaningfully tall; maximum 1.45 times as long as wide; no thin slab, needle, rail-barge, pill, capsule, coffin, ship, submarine, tank, vehicle cab, cockpit, face, eyes, eye-like lights, humanoid anatomy, legs, feet, wheels, treads, wings, fins, antenna, floating ring, separate weapon, gun, barrel, cannon, muzzle, rifle, scope, ammunition, missile, white nose, universal charge orb, baked beam, trail, particles, text, label, logo, watermark, team-colored authored light, extra objects, or separate floating pieces. One connected watertight-looking object. Use a wide flush recessed emitter shutter integrated across the front face. Strong front/rear asymmetry. Large masses and only a few large seams.
```

## Candidate 1 — Railback addendum

```text
Candidate direction: RAILBACK.
Build a broad low hexagonal hover chassis about 1.40:1 length-to-width and roughly one-third as tall as it is wide. Use thick angled shoulder armor around a clearly exposed raised central conductor backbone. Put exactly THREE oversized transverse ceramic capacitor ribs along that backbone, spaced generously and large enough to remain visible in strict top view. Give the front a blunt full-width chisel face containing one dark recessed horizontal beam shutter. Give the rear one clearly different squared split cooling block with a shallow center notch. The large dorsal ribs and wide planted shoulders are the identity. Fragility comes from the exposed conductor gaps and insulators, not from a skinny outer silhouette.
```

## Candidate 2 — Hammerhead addendum

```text
Candidate direction: HAMMERHEAD.
Build a compact broad wedge chassis about 1.25:1 length-to-width and noticeably tall. The forward third is a wide hammerhead-like armored brow with two thick faceted shoulders joined into one continuous chassis; between them, but not in a deep U-shape, place one broad flush recessed beam shutter spanning most of the straight front face. Behind the brow, raise a short central charging crown carrying exactly THREE large stepped capacitor plates that climb gently rear-to-front. Narrow the rear slightly into one thick vented block so heading is unmistakable. Keep the outline chunky and near-square, not a ship, tank, gun, or Palisade-like upright wall.
```

## Candidate 3 — Rail Citadel addendum

```text
Candidate direction: RAIL CITADEL.
Build a squat broad octagonal hover chassis about 1.20:1 length-to-width with the greatest vertical mass of the three directions. The lower body is one heavy chamfered platform with broad side armor cut-ins, not a round disc. Above it rises one compact rectangular accelerator bridge with exactly THREE thick transverse conductor arches/ribs, visibly exposing medium graphite machinery between them. The full straight front edge contains a wide flush recessed beam shutter; the rear carries two low joined cooling buttresses, not separate pods. It should read as an unusual high-energy machine or compact mobile citadel rather than a vehicle, turret, dome, gun, or diamond/pyramid.
```

## Inferred strict-top edit template

Each candidate's oblique image was supplied as Image 1. The identity-anchor
sentence was specialized to that candidate; the rest of the prompt was exact.

```text
Use case: precise-object-edit
Asset type: strict top-view identity proof for Nilbots Longshot concept <candidate>
Input image: Image 1 is the exact robot identity and material reference
Primary request: infer and render the exact same rigid robot from Image 1 from a strict orthographic top-down camera. Change only the camera viewpoint and object rotation needed to place the front toward screen-right/East. Preserve the exact same proportions, silhouette, geometry, part count, hardware placement, wear level, palette, and matte materials. Identity anchors: <candidate-specific anchors>.
Composition/framing: exactly one isolated robot, centered, front facing precisely screen-right, entire chassis visible with generous equal padding, no perspective, no tilt, no oblique angle
Scene/backdrop: uniform warm light-gray background with no floor plane, no grid, no cast shadow, no contact shadow, no hover glow
Lighting/mood: soft diffuse neutral light, even across the object
Constraints: no redesign, no geometry drift, no extra or missing capacitor stage, no separate pieces, no beam, no team teal/cyan, no team orange, no text, label, logo, UI, watermark, scenery, gun, barrel, cockpit, eyes, legs, wheels, treads, wings, or new details. Keep the chassis broad and chunky, not stretched thin.
```

Candidate-specific identity anchors:

- Railback: `exactly three large warm ceramic transverse arches over the exposed central conductor spine, broad angled blue shoulder armor, blunt full-width front beam shutter, squared split cooling rear`
- Hammerhead: `exactly three large stepped warm ceramic rectangular capacitor plates on the raised center, very broad blue hammerhead front brow with integrated full-width beam shutter, slightly narrowed thick vented rear`
- Rail Citadel: `exactly three thick warm ceramic transverse arches over exposed machinery, broad octagonal blue lower chassis, straight full-width front beam shutter, two low joined rear cooling buttresses`

