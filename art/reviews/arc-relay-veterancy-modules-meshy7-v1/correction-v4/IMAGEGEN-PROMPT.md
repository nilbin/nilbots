# Carried-Core clearance generation record

Tool: built-in image generation

Corrected review output: `core-carry-clearance-concept-v2.png`

SHA-256: `29b660cf276a6ebb8ff4bba7f3770f37b697014308996e057d1c92a2ac2bbbd4`

References:

1. `../correction-v3/floating-diadem-rank-1-5-concept-v2.png`
2. `../../arc-relay-play-awareness/core-pickup-after-awareness-same-replay-webgl.png`

## Exact generation prompt

```text
Use case: stylized-concept
Asset type: Nilbots gameplay-readability concept sheet for a universal level-5 veterancy diadem while carrying an Arc Core

Input images:
- Image 1 is the authoritative visual reference for the approved light-ivory levitating bot, matte segmented brass open-U diadem, and its five-prong feathered crown silhouette. Preserve that bot and crown language closely.
- Image 2 is supporting reference for the dark industrial arena, elevated spherical carried Core, team-colored possession glow, tether, and realistic zoom context. Do not copy its older bot designs or UI clutter.

Primary request: Create one polished landscape concept sheet that proves the level-5 crown can coexist with a carried Core without geometry or screen-space clipping. Use three clean panels with the exact same light-ivory legless hovering bot and same level-5 diadem.

Panel 1: close three-quarter gameplay view, level-5 bot with no Core, clearly showing the complete open-U matte brass crown behind the chassis.
Panel 2: same view and scale, carrying one detached glowing Arc Core directly above the chassis center. The crown is a rear-mounted open U behind the Core anchor. Its prongs begin outside a permanently reserved circular Core aperture and fan around the sphere. The glowing Core sits visually in front of the crown, with a clear air gap on every side and a faint narrow tether down to the bot. The center prong remains behind the Core rather than intersecting it, so the Core reads like a framed objective, not a crown jewel. Include a short row of small health pips positioned so they remain readable and do not overlap either Core or crown.
Panel 3: wider, more top-down actual match zoom of that same Core-carrying bot on the dark arena floor, demonstrating that the bright spherical Core remains the first read, the five-prong crown remains the second read, and the pale chassis remains distinct from the floor. Include a few unobtrusive nearby bots for scale only.

Scene/backdrop: dark gunmetal industrial arena floor with subtle panel seams, no scenery obscuring the subject.
Style/medium: polished high-end 3D game concept render, readable mobile-game silhouette, consistent with Image 1.
Composition/framing: horizontal three-panel sheet with thin dark dividers; no captions or text. Keep every subject fully inside its panel.
Lighting/mood: restrained warm studio/game lighting; enough fill light to keep the ivory bot readable against the dark floor.
Color palette: light warm ivory bot; aged matte brass crown; carried Core with luminous pale white center and restrained cyan-teal possession rim/glow; crown never adopts team color.
Materials/textures: crown and chassis matte, worn industrial ceramic/metal, never glossy plastic.

Hard geometry rule: Reserve a spherical no-geometry volume around the carried Core at all veterancy levels. No crown segment, feather, prong, health pip, chassis part, or tether may cross the Core silhouette. The Core must remain detached and clearly removable.
Constraints: levitating bot only; no humanoid body; no legs; no eyes; no guns; no white nose; no toy-like saturation; no overly dark bot; no full halo ring; no floor marker; no extra objective or duplicate Core; no labels, text, logo, or watermark.
Avoid: crown clipping through Core, Core hidden behind crown, crown reshaping or moving when Core is carried, shiny plastic, teal chassis, orange chassis, oversized crown, tiny unreadable Core.
```

## Exact correction prompt

```text
Use case: precise-object-edit
Asset type: Nilbots veterancy/Core compatibility concept sheet

Edit target: the three-panel concept sheet just generated.
Primary request: In panels 2 and 3 only, remove the entire thick circular mechanical ring, dark collar, segmented housing, and any teal metal structure surrounding the carried Core. Replace it with one simple detached luminous Arc Core sphere: a clean pale white/lilac energy orb with a restrained thin cyan-teal ownership glow, approximately 45% of the current ring assembly's outer diameter. It must float above the bot on the same faint narrow tether.

The Arc Core is only a sphere plus soft glow—no casing, no donut, no halo, no gyroscope, no mechanical holder, no extra rings. Preserve a conspicuous air gap between the sphere and every brass crown prong. The rear-mounted brass open-U crown stays behind the sphere; its five feather-like prongs fan around the reserved empty pocket without touching it. The center prong is visible behind and above the sphere but never crosses the sphere silhouette. Keep the crown aged matte brass; nearby teal light may illuminate it subtly but must not recolor its material.

Invariants: Keep panel 1 exactly unchanged. Preserve the same three-panel layout, same bot design, pose, scale, arena, lighting, dividers, health pips, crown geometry, wide-match-zoom composition, and nearby scale bots. Do not alter the ivory chassis. Do not add text, labels, logos, UI, weapons, legs, eyes, or any extra Core. Matte industrial materials, no glossy plastic.

Success criterion: Panel 2 reads first as one removable glowing ball carried in the open center of a crown; panel 3 still reads as Core first and veterancy crown second at gameplay zoom.
```
