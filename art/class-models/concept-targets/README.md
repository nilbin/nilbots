# Class-model concept targets

These images bridge the approved 2D class art and authored 3D geometry. They
are modeling references, not runtime assets and not independent redesigns.

Each model must satisfy both sources:

- the canonical SVG controls the exact top-view silhouette, negative spaces,
  rule-bearing hardware, and semantic team-accent regions;
- the oblique target controls side/underside volume, material character,
  recess depth, and the intended gameplay-camera read.

When the two disagree, the canonical SVG wins. A target may explain depth
outside the visible top plane, but it cannot change the bot's footprint or
promise mechanics the class does not have.

## Striker target v1

- Canonical input:
  `web/src/assets/class-looks/trident-wasp/sprite.svg`
- Raster reference:
  `references/trident-wasp-2d.png`
- Oblique modeling target:
  `striker-oblique-target-v1.png`
- Four-view approval sheet:
  `striker-model-sheet-v1.png`
- Approval-sheet SHA-256:
  `7209a6afc3e58bcc37b2b5c710da167c8ee563656251e6de8fbe74c20305f683`
- Status: approved as the Striker depth/material direction on 2026-07-29.
- Generated with the built-in image-generation workflow as a
  `sketch-to-render` reference.

The generated top view is illustrative and is not a geometry source. Where it
differs, the canonical SVG and measured overlay win. Once a sheet is approved,
the human-corrected editable model source owns every later mesh/material
decision; further image-generation inference is outside the production path.
No procedural Blender generator is approved for this look.

Prompt:

> Translate the canonical top-view design into a genuinely
> three-dimensional low-hover combat skimmer. Preserve its dart/wasp
> planform, central hexagonal core, three forward channels, clipped aft
> shoulders, material-region boundaries, and three semantic cyan team
> panels. Use shaped continuous hull volumes, tapered sidewalls, armor
> thickness, recessed channels, an inset core well, underbody field emitters,
> vents, and a shallow visible air gap. Materials are restrained stylized
> PBR: graphite metal, warm titanium/brass, amber energy, and clean
> repaintable team panels. Avoid tracks, wheels, landing gear, rotors,
> cockpit, conventional tank cues, stacked flat cutouts, marble/crystal
> texture, grid noise, toy plastic, and extra weapons.

## 2026-07-30 class-family approval set

The directories added in this set preserve every generated variant. They were
made with Codex's built-in `imagegen` workflow; they are not Meshy results, no
provider task was submitted for them, and no provider credits were spent.
`PROMPTS.md` records the exact final prompt and checked-in inputs for every
image.

The owner approved all visible bot concepts and the three projectile concepts
as visual directions. That approval is deliberately not a provider-upload
waiver: the canonical East-facing 2D asset still wins for top silhouette,
negative space, rule-bearing hardware, and semantic team-accent placement.
Every candidate must pass a close same-scale 2D overlay before a provider call.

| Checked-in concept | Built-in call | SHA-256 | Status and next gate |
| --- | --- | --- | --- |
| `aegis-tortoise-3d-concept-v1/approval-sheet.png` | `call_tjsn1gBXIPdkxDhiHSFFwE1c` | `9661600ab01d3cdf0617fc757ac40be5a43affc2054c83339082045513767b91` | Initial mobile inference, retained. Its short articulated ground legs established the family locomotion direction. Use V2 for the current material/team-panel target. |
| `aegis-tortoise-3d-concept-v2/approval-sheet.png` | `call_MkoZZSMwntjmwz5hZtx1E2JH` | `d1acf3d6af2ed71c64146a843f7564c99ede0b9384b26c28515218f4f57c3be7` | Owner-approved mobile visual. V1 geometry/presentation was the edit authority; V2 is the current color/material target. Before Meshy, correct the top planform to the runtime SVG, prove the overlay, and extract clean consistent views with four deterministic team inlays. |
| `lattice-loom-3d-concept-v1/approval-sheet.png` | `call_oRN8ZSii8otSZHpMsCrlV7wM` | `5acf3673643e9caddf5ffb66f23f1406eb29b321dc654a06eb9479009ab58d4d` | Initial grounded-foot inference, retained as an approved explored variant. It is not the selected default locomotion. |
| `lattice-loom-3d-concept-v2/approval-sheet.png` | `call_q0WLLqRHUvl9zH1xMQY8MABV` | `c208e69e8099e3b88f521a3af02471fada686161300215553d210244c25d9036` | Owner-approved low-levitation geometry and selected default locomotion direction. It replaces feet with four integrated field nodes while retaining one separate-instance Fabricator chassis. |
| `lattice-loom-3d-concept-v3/approval-sheet.png` | `call_0WOW5tzgO0KrMUBdQFr0OT7y` | `29d32230975d728a0ae72cc29540362c3aa7ddbdf8760f9964371c271bdc3db2` | Owner-approved visible semantic-accent pass, but not deterministic enough for Meshy. Correct the four inlays against the runtime SVG, keep the aperture/field lights authored amber, prove the overlay, then extract clean views. |
| `aegis-tortoise-shell-3d-concept-v1/approval-sheet.png` | `call_UieJYBsjAPMFLZRKva6q5Bzm` | `2d77b5ae7e6a96714164fc4f417d4c0cc0b4a23f13851ce4dad34208fe19f867` | Owner-approved Shell visual. It remains a distinct model and animation target. Before Meshy, prove the exact East-facing 90-degree guard, open other three quadrants, no muzzle, and exact semantic inlays against the Shell SVG. |
| `aegis-tortoise-turret-3d-concept-v1/approval-sheet.png` | `call_d4nyZtiTKjtGDqyMEvVhZobZ` | `b3df8d605c734c7a078d63dd6d7b98848337c67755d4205b427ecd6c27cf4b0f` | Owner-approved whole-Turret visual. Before Meshy, prove fourfold symmetry, one central hub, four equal arms/inlays, and the exact SVG overlay. Runtime also needs the whole-Turret loader path described below. |
| `projectiles/trident-spark-3d-concept-v1/approval-sheet.png` | `call_kXIa4ouJSwfAKeevcN5TQXhw` | `8281af50b1cce1df2e9edba688d9b8ff982048f69f9fdc54f14149721eb11dc5` | Owner-approved review concept. Make clean separated views, pass the exact projectile SVG overlay, then submit this head alone as one future Meshy task. |
| `projectiles/rebound-diamond-3d-concept-v1/approval-sheet.png` | `call_r9Use93FxTXViSZV8DsgVgM2` | `164413d8b0177c7a1e7a67779f3dba20055344c62903f72c4f9fd0781423ad83` | Owner-approved review concept. Preserve the completely open elongated-hex tunnel. Clean views and overlay precede one independent future task. |
| `projectiles/lattice-rivet-3d-concept-v1/approval-sheet.png` | `call_K07ByXIyrBQbuv51APhCxFtx` | `fa40e7be54bf60bed707a18d017d182d0e363f244f3997558d683ff9d3a459fe` | Owner-approved review concept. Preserve the open diamond aperture and joined split rear rails. Clean views and overlay precede one independent future task. |

Approval chronology matters here. Aegis V1 was the initial mobile inference,
then V2 supplied the approved team-panel pass. Lattice V1 recorded the
grounded-foot inference, V2 changed only the selected locomotion to low hover,
and V3 was visible when the owner approved everything shown so far. Trident
Spark, Rebound Diamond, and Lattice Rivet were also visible under that approval.
Shell and whole Turret were generated afterward, so they were not silently
included in the earlier statement; the owner subsequently approved both
explicitly. The latest direction confirms all visible bot concepts subject to
close canonical-2D alignment. No approval in this chronology makes a contact
sheet provider-ready.

### Authority order

For Aegis Tortoise mobile, the production top-view geometry and semantic
authority is
`web/src/assets/class-looks/aegis-tortoise/sprite.svg`. The high-resolution
`art/class-look-concepts/bulwark/aegis-tortoise/concept.png` supplied the
generation identity and colors. V1 supplies the accepted inferred depth and
family leg architecture; V2 supplies the current material and team-panel
direction. Neither generated top view may silently replace the SVG.

For Lattice Loom, the production top-view geometry and semantic authority is
`web/src/assets/class-looks/lattice-loom/sprite.svg`. The high-resolution
`art/class-look-concepts/fabricator/lattice-loom/concept.png` supplied the
generation identity and colors. V2 is the selected levitating geometry target.
V3 is only a color/material proposal: its panel locations must be corrected
deterministically from the SVG before provider input is built.

The Shell and Turret geometry authorities are respectively
`web/src/assets/class-looks/aegis-tortoise-shell/sprite.svg` and
`web/src/assets/class-looks/aegis-tortoise-turret/sprite.svg`. Their checked-in
`vector-reference.png` files are raster review inputs derived from those SVGs;
the SVG always wins.

Each projectile's runtime SVG under
`web/src/assets/class-projectile-looks/<id>/sprite.svg` is the exact top
silhouette and negative-space authority. Its `vector-reference.png` is a
rasterized generation input, not a replacement source. The chassis sheet used
as Image 2 controls only sibling material language.

### Provider-input gate

The four-view approval sheets are review concepts, not direct multiview
provider inputs. For each object:

1. Rebuild the strict top view from the canonical SVG and record a same-scale
   alpha overlay. Correct any footprint, center-of-mass, negative-space,
   hardware, or team-panel drift before doing anything else.
2. Separate top, side, front three-quarter, and rear three-quarter into clean
   single-object images with identical scale, geometry, materials, lighting,
   and neutral background. A contact sheet must never be uploaded as one
   provider view.
3. Keep one object per future provider task: mobile Aegis, Shell, whole
   Turret, Lattice, Trident Spark, Rebound Diamond, and Lattice Rivet are seven
   independent tasks. Do not include a bot and its projectile in one upload.
4. Preserve all variants and prompt records even after one target advances.
   Do not submit any provider task from this concept-only branch.

### Animation and state plan

Animation must remain a presentation of replay truth, not an animation clip
that invents state.

- **Aegis mobile:** use a heavy short four-leg gait. Each articulated
  anchor-foot takes a deliberate compact step while the carapace stays level;
  the feet plant and the suspension compress before an authoritative Anchor
  transition. Mobile firing uses only the embedded East-facing gun.
- **Shell:** use a separate Shell body. During the recorded transition the
  mobile legs plant, the chassis lowers, and the 90-degree physical guard
  locks into place. Reverse the same motion for Mobilize. Shell remains flat,
  keeps its facing, never spins, and has no muzzle.
- **Turret:** use one whole radial body with a singular hub. The legs plant,
  the chassis settles, and four equal arms/locks unfold only during the
  recorded transition; Mobilize reverses it. Once deployed it has no
  privileged facing. A shot may recoil or flash only the heading-true arm
  selected from the authoritative absolute shot heading.
- **Lattice mobile:** use the same presentation-only `low-hover` style as the
  Striker: a shallow gap, restrained field pools, tight shadow, and gentle
  bob, with no altitude or collision change. Fabrication creates a separate
  identical actor from authoritative lifecycle state; nothing grows onto the
  existing chassis.
- **Projectiles:** the GLB contains only the head. The renderer owns team
  tint, emission, trail/tracer, halo, floor wash, recorded travel and bends,
  optional travel spin/roll, hit and miss presentation, ricochet/deflection
  cue, and hitbox. A future per-look presentation opt-in may request spin in
  the same spirit as `low-hover`; it must be deterministic, cosmetic, and
  absent from model geometry. Hits key from authoritative damage/deflection
  events. A traversal that ends without one gets a distinct restrained
  dissipate/miss effect rather than a fabricated impact.

### Whole-Turret renderer caveat

The current GLB manifest accepts `part: "whole"` and `part: "turret-arm"`,
but the current arena actor always constructs an omnidirectional turret by
cloning its resolved emplaced model four times. It treats only
`part: "turret-arm"` as pre-oriented; a whole radial Turret would therefore be
incorrectly repeated four times.

Do not mount the approved whole-Turret concept through that path. Before it
can ship, extend the manifest/actor contract so one whole radial model is
mounted once at the center, retains its unique hub, exposes the intended
deployment pieces/pivots, receives actor-local semantic tint, and falls back
to the existing procedural/SVG turret when unavailable. That loader work is a
runtime gate, not permission to remodel this approved concept as one repeated
arm.
