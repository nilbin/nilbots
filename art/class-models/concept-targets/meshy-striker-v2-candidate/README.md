# Striker clean multiview v2

Owner-approved on 2026-07-30 as the input set for one Striker body-only Meshy
proof. The default projectile is intentionally absent and requires a separate
task after the chassis route passes.

All four files are 1254×1254 RGB PNGs with a flat neutral background, diffuse
lighting, no cast shadow, and consistent full-object framing:

| View | SHA-256 |
| --- | --- |
| `01-top.png` | `39c65768e05880d46f430c62542ac7aced9b4b721ee942b327d17d2c10ca681e` |
| `02-side.png` | `e91b7fdeeb591c3400101758fb415258eb7b5d75ff30b23c8758520658b79149` |
| `03-front-three-quarter.png` | `69ddad1a5043975e6b2d6aadfdb29ada599f41d6ba3befd5391593c5cc9faf60` |
| `04-rear-three-quarter.png` | `6aaa912e9c428c68d47eb92ab9bfd01f51b9a85bdc5f430f3639ea311606fc78` |

`approval-sheet.png` is review-only and must never be sent to a multi-image
task as one source. `SILHOUETTE-REPORT.md` and
`01-top-silhouette-overlay.png` record the deterministic top-view gate against
the canonical SVG.

## Output decision

On 2026-07-30 the owner accepted the Meshy enhancement-OFF geometry from task
`019fb00c-9e8d-723b-9485-49706e307cb9` as the Striker runtime base. The
deterministic semantic team-accent split and actual blue/orange replay gates
subsequently passed. Landing remains gated on preserving the reproducible
authoring record and resolving the measured effective live-span clearance,
not on another provider generation.

The rigidly corrected lean derivative reaches `95.673%` top-planform IoU
against the canonical SVG and preserves `99.952%` of the raw provider
silhouette. Its strict side is deliberately accepted as a deviation: the
generated hull is taller, more rectilinear, and less continuously tapered than
this approved side concept. Do not spend another provider call attempting to
repair that profile. Preserve this geometry through the team-material pass;
future side cleanup, if ever justified, belongs to authored modeling and
rebaking rather than another inferred reconstruction.

## Accepted generation prompts

The images were generated sequentially so each accepted view constrained the
next. Paths below are repository-relative even where the original tool call
used their absolute equivalents.

### 01 — top

References:

- `../meshy-striker-v1/01-top.png`
- `../meshy-striker-v1/02-side.png`
- `../meshy-striker-v1/03-front.png`
- `../meshy-striker-v1/04-three-quarter.png`

```text
Use case: stylized-concept
Asset type: high-resolution single-view input for multi-view 3D reconstruction
Input images: Images 1–4 are the same approved Striker low-hover skimmer from top, side, front, and front three-quarter views; preserve one exact object identity across them.
Primary request: Render the approved Striker as one clean, strict orthographic top-down 3D view. This is a reconstruction reference, not a redesign.
Subject: Preserve the dart/wasp planform, narrow pronged nose pointing right, clipped broad rear shoulders, central hexagonal amber core, three long forward channels, graphite-black hull, weathered bronze armor panels, exactly three sharply bounded cyan team-accent inlay regions, and amber engine/core emission.
Composition/framing: square canvas; vehicle centered; full extents visible; consistent scale; object occupies about 78% of the canvas; camera exactly perpendicular to the top plane; no perspective or oblique tilt.
Scene/backdrop: perfectly flat uniform neutral light-gray background, no floor plane.
Lighting/mood: diffuse neutral studio illumination solely to reveal material; no dramatic light, no color spill, no cast shadow, no contact shadow, no reflection on the background.
Materials/textures: restrained stylized PBR graphite and weathered bronze; crisp panel seams and recessed vents; pure cyan only inside the three team inlays and nowhere else; warm amber only for core/engine energy.
Constraints: match the approved top silhouette and material-region boundaries; preserve all negative spaces and proportions; one vehicle only; no landing gear, wheels, tracks, rotors, cockpit, text, logo, support, projectile, scenery, extra weapon, or new ornament.
Avoid: fisheye, perspective, three-quarter camera, background gradient, vignette, bloom spill, cast shadow, cropped edges, extra cyan reflections, redesign, asymmetry, stacked flat cutouts, watermark.
```

### 02 — strict side

References:

- `01-top.png`
- `../meshy-striker-v1/01-top.png`
- `../meshy-striker-v1/02-side.png`
- `../meshy-striker-v1/03-front.png`
- `../meshy-striker-v1/04-three-quarter.png`

```text
Use case: stylized-concept
Asset type: high-resolution single-view input for multi-view 3D reconstruction
Input images: Images 1–5 depict the same approved Striker low-hover skimmer; Image 1 is the proposed clean top identity, the others constrain approved proportions and depth.
Primary request: Render that exact same Striker as one clean, strict orthographic side-profile 3D view, with the narrow pronged nose pointing right. Reconstruction reference only, no redesign.
Subject: Preserve one thin continuous low-hover skimmer hull, central raised hexagonal amber core, tapered dart nose, clipped rear shoulders, graphite-black hull, weathered bronze armor plates, exactly three cyan team-inlay regions positioned consistently with the top view, and compact amber underbody field emitters.
Composition/framing: square canvas; full vehicle centered and completely visible; consistent scale with the top reference; strict side elevation with no yaw and no top-down perspective; vehicle occupies about 78% of canvas width.
Scene/backdrop: perfectly flat uniform neutral light-gray background, no floor plane.
Lighting/mood: diffuse neutral studio illumination; no dramatic light, no color spill, no cast shadow, no contact shadow, no reflection on background.
Materials/textures: restrained stylized PBR graphite and weathered bronze; crisp seams, vents, armor thickness; pure cyan confined to discrete team inlays; warm amber confined to core/engine emitters.
Constraints: thin, continuous skimmer profile; shallow visible air gap may be implied by underbody hardware but no ground/shadow; one vehicle only; preserve proportions and material-region continuity.
Avoid: landing gear, feet, wheels, tracks, rotors, cockpit, tank cues, tall fuselage, bulbous belly, layered floating slabs, extra weapon, projectile, text, logo, support, scenery, gradient, vignette, cast shadow, crop, watermark, redesign.
```

### 03 — front three-quarter

References:

- `01-top.png`
- `02-side.png`
- `../meshy-striker-v1/03-front.png`
- `../meshy-striker-v1/04-three-quarter.png`

```text
Use case: stylized-concept
Asset type: high-resolution single-view input for multi-view 3D reconstruction
Input images: Images 1–4 depict the same approved Striker low-hover skimmer; Images 1 and 2 are the proposed clean top and strict side identities.
Primary request: Render that exact same Striker as one clean front three-quarter 3D reconstruction view, looking slightly down at the narrow pronged nose and right-front quarter. No redesign.
Subject: Preserve the dart/wasp planform, thin continuous skimmer hull, narrow three-channel pronged nose, central raised hexagonal amber core, clipped broad rear shoulders, graphite-black hull, weathered bronze armor, exactly three sharply bounded cyan team inlays in the same top-view positions, and compact amber underbody field emitters.
Composition/framing: square canvas; full vehicle centered and completely visible; consistent object scale; front three-quarter camera about 25 degrees above the vehicle, enough to show top and side depth without exaggeration; nose remains clearly directional and unclipped; vehicle occupies about 76% of canvas.
Scene/backdrop: perfectly flat uniform neutral light-gray background, no floor plane.
Lighting/mood: diffuse neutral studio illumination; no dramatic key light, no color spill, no cast shadow, no contact shadow, no reflection on background.
Materials/textures: restrained stylized PBR graphite and weathered bronze; crisp panel seams, recessed channels, vents, armor thickness; pure cyan only in the three team panels; warm amber only in core/engine emitters.
Constraints: same proportions, negative spaces, material boundaries, thin side profile, and geometry as the clean top/side references; one vehicle only.
Avoid: landing gear, feet, wheels, tracks, rotors, cockpit, tank cues, bulbous belly, stacked slabs, extra weapons, projectile, text, logo, support, scenery, gradient, vignette, bloom spill, cast shadow, crop, watermark, asymmetry, redesign.
```

### 04 — rear three-quarter

References:

- `01-top.png`
- `02-side.png`
- `03-front-three-quarter.png`
- `../meshy-striker-v1/03-front.png`

```text
Use case: stylized-concept
Asset type: high-resolution single-view input for multi-view 3D reconstruction
Input images: Images 1–4 depict the same approved Striker low-hover skimmer; Images 1–3 are the proposed clean top, side, and front-three-quarter identities.
Primary request: Render that exact same Striker as one clean rear three-quarter 3D reconstruction view. Camera is above and behind the broad clipped rear shoulders on the left-rear side; the narrow pronged nose recedes toward the right. This view must reveal the previously unseen rear/underside structure without redesigning the object.
Subject: Preserve the dart/wasp planform, thin continuous skimmer hull, broad clipped rear shoulders, narrow three-channel pronged nose, central raised hexagonal amber core, graphite-black hull, weathered bronze armor, exactly three sharply bounded cyan team-inlay panels in the same top-view positions, and compact amber underbody field emitters. Rear structure should be a continuous armored skimmer tail with restrained vents and field-emitter hardware, not a new propulsion concept.
Composition/framing: square canvas; full vehicle centered and completely visible; consistent object scale; rear three-quarter camera about 25 degrees above; rear shoulders closest to camera at left, nose farther away at right; vehicle occupies about 76% of canvas.
Scene/backdrop: perfectly flat uniform neutral light-gray background, no floor plane.
Lighting/mood: diffuse neutral studio illumination; no dramatic light, no color spill, no cast shadow, no contact shadow, no reflection on background.
Materials/textures: restrained stylized PBR graphite and weathered bronze; crisp seams, vents, armor thickness; pure cyan only in the three team inlays; warm amber only in core/underbody emitters.
Constraints: same top silhouette, proportions, negative spaces, material boundaries, and thin side profile as Images 1–3; one vehicle only; infer only the rear surfaces necessary to make those views physically consistent.
Avoid: rear jet nozzle cluster, wings, tail fins, landing gear, feet, wheels, tracks, rotors, cockpit, tank cues, bulbous belly, layered floating slabs, extra weapons, projectile, text, logo, support, scenery, gradient, vignette, bloom spill, cast shadow, crop, watermark, asymmetry, redesign.
```
