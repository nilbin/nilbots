# Arena themes, bot looks, and presentation animation

The arena renderer is presentation-only. It consumes the authoritative replay,
interpolates between recorded states, and must never invent movement, hits,
visibility, projectiles, or outcomes.

## Product contract

- A map owns its theme. Match setup chooses a map; replay viewers never choose
  an alternate skin.
- A visual change ships as a new immutable map/theme package version so old
  replays retain their intended presentation.
- ASCII map tiles remain the gameplay contract: `#` is blocked and `.` is
  walkable. Textures, bevels, decals, props, lighting, and particles do not
  change collision or rules.
- Map packages are limited to 32×32 tiles. This bounds simulation/replay cost
  and prevents reusable floor materials from losing all per-tile visual
  density on unbounded arenas. It is not the wall-resolution control: larger
  maps make screen tiles smaller, while atlas scale and DPR review protect
  high-DPI wall sharpness.
- Props must tell the truth. Solid-looking machinery belongs on blocked tiles;
  walkable tiles may contain only flat details such as seams, stains, lights,
  cables, or grates.
- Map-scale features such as rivers, trenches, cable runs, or vegetation paths
  must be explicit map presentation data. They do not belong in a reusable
  base material where collision could arbitrarily cut them.

Each `maps/*.json` document names its standalone presentation package:

```json
{
  "id": "basic-01",
  "version": 5,
  "theme": "control-room",
  "presentation": {
    "boundaryWall": "perimeter",
    "interiorWall": "cover",
    "wallGroups": []
  }
}
```

The engine copies the theme and presentation object into replay presentation
metadata. Generation-3 generic-actor contracts deliberately keep
`ActorMapDefinition` gameplay-only, so their replay writers receive a separate
presentation descriptor containing theme, wall families, and per-form
chassis/projectile IDs. That descriptor changes the replay hash but never the
rules, map, or match fingerprints. Hosted execution and every Frontline
CLI/sandbox writer must supply it explicitly.

The viewer resolves those IDs against theme manifests; it never switches on
`mapId`. Legacy maps and replays without the optional fields fall back to the
Control Room defaults. That fallback is compatibility only: a native
Frontline review with null presentation has failed its handoff and cannot
approve a theme.

## File layout

```text
art/themes/<theme-id>/
  art.json
  floor/source.png              # generated floors
  walls/perimeter/
    source.png
    albedo.png
    normal.png
    height.png
    roughness.png
    ao.png

art/bot-looks/<look-id>/
  raster-reference.png

art/class-models/
  README.md                       # 3D trial status and budget record
  concept-targets/
    README.md
    striker-oblique-target-v1.png
    striker-model-sheet-v1.png

web/src/assets/themes/control-room/
  theme.json
  floor-metal.png
  wall-perimeter-albedo.webp
  wall-perimeter-edges.webp
  wall-perimeter-shadows.webp
  wall-cover-albedo.webp
  wall-cover-edges.webp
  wall-cover-shadows.webp

web/src/assets/themes/overgrown-lab/
  theme.json
  floor-ceramic-v2.png
  wall-*.webp

web/src/assets/themes/<theme-id>/
  theme.json
  floor-*.png | floor-*.webp
  wall-*.webp
  pbr/                              # optional lazy WebGL-only material maps
    wall-*-normal.webp
    wall-*-roughness.webp

art/themes/<staged-theme-id>/runtime/
  theme.json
  floor-*.webp
  wall-*.webp

web/src/assets/bot-looks/<look-id>/
  look.json
  sprite.png | sprite.svg
  model3d.json                     # optional WebGL companion
  model.glb

web/src/assets/projectile-looks/<look-id>/
  look.json
  sprite.svg
  model3d.json                     # optional WebGL companion
  model.glb

web/src/assets/class-looks/<form-look-id>/
  look.json
  sprite.svg
  model3d.json                     # optional WebGL companion
  model.glb

web/src/assets/class-projectile-looks/<form-projectile-id>/
  look.json
  sprite.svg
  model3d.json                     # optional WebGL companion
  model.glb

web/src/render/
  arenaThemes.ts     data loader and legacy fallbacks
  drawArena.ts       layered replay-driven rendering and effects
  interpolate.ts     authoritative state interpolation

web/src/render3d/
  lookModel.ts       renderer-only GLB discovery, loading, and fallback
  arenaScene.ts      topology-derived environment geometry/materials
  wallDetails.ts     deterministic profile-contained service detail
  themeMaterialAssets.ts  lazy environment-only PBR discovery
```

`art/themes` holds production sources and derived PBR maps; it is not bundled
into the viewer. `web/src/assets` holds optimized runtime output. Vite
discovers the runtime manifests with `import.meta.glob`. The hosted `dist/`
build emits referenced images as cacheable assets; theme-scoped CLI builds
inline only their selected theme into a self-contained viewer. Adding a valid
package requires no TypeScript registry edit.

## Theme asset contract

### Floor material

- Opaque square generated source, with a 1024×1024 runtime PNG or WebP.
- Exactly orthographic/top-down; no horizon or perspective.
- Even upper-left illumination without a central spotlight.
- Medium-scale readable material detail; avoid high-frequency noise that
  aliases at a 40–70 px gameplay cell.
- No baked gameplay grid, props, text, logos, deep shadows, or map-scale
  features such as rivers and long conduits.
- The renderer maps the image once across the whole arena below the wall mask.
  It does not slice, shuffle, outline, or decorate individual gameplay cells.
- New generated floors are retained at
  `art/themes/<theme>/floor/source.png`. Their `art.json` floor block declares
  runtime filename, dimensions, and WebP quality so
  `scripts/build-theme-art.py` reproduces the optimized viewer asset. Quality
  90 WebP is the current default for generated floors; it preserves
  gameplay-scale detail while avoiding multi-megabyte lossless PNGs in every
  self-contained viewer.

### Wall families and topology atlases

A theme is a kit, not one wall texture. At minimum it supplies a fortified
`perimeter` family and a lower `cover` family. More families such as
`damaged-cover`, `reactor`, or `ruin` can be added and assigned to specific wall
cells by the map.

Each family starts with an opaque square source material under
`art/themes/<theme>/walls/<family>/source.png`:

- Strict top-down material field; no surrounding floor, isolated wall object,
  outer frame, perspective, text, or map-scale feature.
- Medium and large material detail is welcome: panels, aggregate, cracks,
  hardware, moss, paint wear, and shallow seams.
- Generate each material separately. Do not ask an image model for an exact
  atlas: topology alignment is a deterministic build concern.

`scripts/build-theme-art.py` then produces:

- Edge-safe albedo plus height, tangent normal, roughness, and ambient-occlusion
  maps beside the source. These maps are DCC-ready even though the current
  viewer is 2D.
- An optimized world-space albedo for the viewer.
- A complete 256-entry, eight-neighbour edge atlas and matching shadow atlas.
  Every open side, convex corner, concave corner, junction, side face,
  fastener, and shadow is baked before runtime.

The renderer computes only the eight-neighbour mask and places the exact
pre-baked sprite. It does not draw borders, round rectangles, gradients,
bevels, or shadows. Materials are mapped continuously over each connected
family instead of restarting per ASCII cell.

The logical atlas contract is 16 columns, a 96 px gameplay-cell core, and a
16 px gutter on each side. Production currently bakes that geometry at 2×:
192 px cores, 32 px gutters, 256 px entries, and 4096×4096 atlases. Shadow and
edge sprites use the same mask index, so their registration cannot drift.
High-quality alpha WebP keeps those high-DPI atlases smaller than the former
1× lossless files. Every `art.json` also declares a hard runtime asset budget;
the build fails rather than silently making every replay download much larger.

`arenaThemes.ts` registers every active manifest and URL, but image decoding is
lazy: only the theme selected by the replay receives `Image` instances. Do not
move `loadImage` back into manifest registration. Four 4096×4096 atlases per
theme can occupy roughly 256 MiB after decoding regardless of their compact
WebP transfer size; eagerly decoding several themes can crash mobile browsers.

### Zone treatment

- A theme may use palette-only tint and perimeter treatment; Overgrown Lab
  deliberately does so to preserve contrast against its detailed pale floor.
- An optional opaque square PNG can supply a dedicated material when it remains
  legible, as in a darker or quieter arena.
- A visibly distinct floor treatment from the same architectural family, such
  as powered inlays or capture hardware—not merely a recolored base floor.
- Exact top-down view with continuous, mask-safe detail. Do not bake in an
  outer frame, central emblem, gameplay-cell borders, or an assumed zone size.
- The manifest declares the material's fixed world scale in gameplay tiles.
  The renderer anchors that repeating field to the map origin, clips it to the
  exact zone mask, and adds the theme-colored gameplay tint and exterior pulse.
  A zone changing size therefore reveals more material instead of stretching
  existing detail. Bots and projectiles must remain legible over it.

### Palette and registration

Create `web/src/assets/themes/<theme-id>/theme.json` with:

- Stable ID and player-facing label.
- Floor filename relative to the manifest, plus an optional dedicated
  zone-floor filename and its `scaleTiles` world scale. The renderer clips the
  fixed-scale material to the map's declared zone mask, so irregular zones
  remain continuous without per-cell borders or size-dependent distortion.
- `walls.defaults` for legacy maps, `walls.atlas` dimensions, and one
  `walls.families` entry per available family. Each family names its material,
  edge atlas, and shadow atlas.
- Canvas, floor, wall, frame, and objective-zone colors.

Then set `"theme": "<theme-id>"` and a `presentation` object in the owning map
JSON and bump that map's version. `boundaryWall` and `interiorWall` are
required when presentation exists. Optional `wallGroups` override exact wall
tiles:

```json
{
  "presentation": {
    "boundaryWall": "perimeter",
    "interiorWall": "cover",
    "wallGroups": [
      {
        "family": "damaged-cover",
        "tiles": [[3, 2], [4, 2]]
      }
    ]
  }
}
```

The engine rejects malformed family IDs, floor-cell assignments, and duplicate
overrides. The theme must contain every family named by the map. The loader
discovers packages automatically. Do not add a viewer preference switch.

### Rebuilding theme art

Use a disposable environment so the application itself gains no image-library
runtime dependency:

```sh
python3 -m venv sandbox/theme-art-venv
sandbox/theme-art-venv/bin/pip install -r scripts/requirements-theme-art.txt
sandbox/theme-art-venv/bin/python scripts/build-theme-art.py art/themes/control-room/art.json
sandbox/theme-art-venv/bin/python scripts/build-theme-art.py art/themes/overgrown-lab/art.json
```

`art.json` is the reproducible art-direction and geometry recipe. Its optional
`floor` block owns source, runtime filename, dimensions, and encoding quality;
its `runtime` block owns atlas scale, edge WebP quality, and the theme-wide
asset budget. `runtime.packagePath` may point a deliberately staged theme at
`art/themes/<theme>/runtime`; this retains a complete reviewable package
without making Vite eagerly embed it in every site and replay viewer. Remove
that override and move the package under `web/src/assets/themes` only when a
map intentionally ships the theme.
Do not raise that budget merely to make a build pass: inspect the output and
compare the relevant theme-scoped CLI viewer size first. Keep the generated
source prompt with the change/PR. A 3D environment companion feeds the
checked-in albedo/normal/height/roughness/AO maps to the DCC; it does not
regenerate the material merely to change camera or lighting.

Check the theme on the smallest and largest shipped maps, plus a synthetic
32×32 map when changing world-scale floor treatment. Dense wall maps must
still distinguish open floor, blocked cells, zone tiles, bots, and projectile
paths at a glance.

### Adaptive combat contrast

Health and ordnance retain their original accent-colored pips, glow, trails,
and projectile silhouettes. The renderer samples the already-painted pixels
beneath each indicator. If the authored bot accent falls below 3:1 graphical
contrast there, it makes the smallest one-percent blend toward black or white
that reaches the threshold. Otherwise it uses the authored color unchanged.

This adjustment is local presentation only: it does not mutate the bot-owned
accent or replay data, add a plaque/outline, or constrain theme materials.
Projectile glow remains additive, while its slim accent core uses normal
compositing so a locally darkened accent can actually read on a pale floor.
Canvas sampling failure also falls back to the authored accent rather than
breaking playback. Health pip count comes from the replay's rules-owned
`maxHealth` snapshot (with the historical three-health fallback), never a
presentation constant.

## Bot-look contract

- Genuine SVG with `viewBox="0 0 512 512"` is the recommended default. It keeps
  silhouettes, panel lines, and emissive shapes sharp through arena scaling,
  rotation, high-DPI playback, and telemetry thumbnails while usually reducing
  the self-contained viewer size.
- SVG must not embed a PNG/JPEG or a `data:image` payload; wrapping or
  automatically tracing a bitmap gives no meaningful scaling benefit.
- Transparent 512×512 PNG is the exception for a look whose painterly,
  organic, corroded, or texture-heavy art demonstrably becomes worse when
  authored as vector. Keep a higher-resolution master outside the runtime
  package and derive the runtime PNG from it.
- Exactly orthographic/top-down and facing East/right. The renderer rotates
  from that canonical orientation.
- One chassis only, centered with generous padding and a crisp silhouette.
- The visible chassis should occupy roughly 65–75% of the source canvas.
- No cast/contact shadow, floor, projectile, text, logo, or scenery. Shadows
  are rendered separately so they remain consistent between looks.
- Internal lighting may be baked into the chassis, but the silhouette and
  weapon direction must remain readable around 48 px.
- Record a stable ID, label, suggested accent, sprite filename, and render scale
  in the look's standalone `look.json`.
- A class-oriented look may declare presentation-only `classId` metadata.
  Current vocabulary includes Frontline's `striker`, `bulwark`, and
  `fabricator`, plus Arc Relay's sixteen launch IDs. It lets the frontend
  describe intent without parsing the look ID; it does not yet enforce account
  equip policy.
  Manifest discovery validates the value and exposes it as `BotLook.classId`
  (`null` for ordinary looks). Consumers must not infer a class by parsing a
  look ID.
- A genuine SVG may mark a restrained set of direct, filled shape elements
  `data-team-accent="true"`. Canvas2D and WebGL substitute the replay-resolved
  team accent on those paths only. Keep them around 5–10% of the visible
  chassis, retain a valid fallback fill/stroke, and never tag a parent group:
  authored armor, material, and class silhouette must survive the tint.
- A look may declare `defaultProjectile` as a recommended companion. The
  appearance UI selects that projectile with the chassis when both are owned,
  but projectile choice remains independently editable.
- Bot looks and projectile looks are independent catalogs, not one-to-one
  pairs. Do not add a default projectile merely because a chassis and
  projectile were authored in the same release.
- A look belongs to the bot. Player projects set it in `botarena.json`:

  ```json
  {
    "appearance": {
      "accent": "#22d3ee",
      "look": "needle",
      "projectile": "razor-shard"
    }
  }
  ```

  The CLI submits it as `lookId`; the server stores it on the bot and snapshots
  it as `lookIdSnapshot` when a match is created. The engine then copies it to
  replay participants. Historical playback therefore does not consult the
  bot's current account record.

Current looks are Vanguard, Bulwark, Needle, Orbiter, Lancer, Aureate Warden,
Rift Runner, Mossback, Helio Kite, Scrap Jackal, Glass Manta, and Mantis. All
are genuine path-based SVGs. Six locked Frontline class packs add Vector
Kestrel and Arc Viper for Striker, Gatehouse and Mirror Bastion for Bulwark,
and Copyforge and Rivet Mantis for Fabricator; each carries `classId`, semantic
team-accent surfaces, and a paired projectile in one purchase pack. The
earlier generated PNGs remain under
`art/bot-looks` as unbundled visual references; they are not disguised as
vector sources. Slot-based Vanguard / Bulwark selection exists only as a
compatibility fallback for old replays that predate `lookId`.
Vanguard, Bulwark, Needle, Orbiter, Rift Runner, and Mossback are
starter-accessible; Lancer is the first successful-build achievement unlock on
the official service. Aureate Warden and its recommended Regent Lance
projectile unlock together after an account completes 100 ranked matches. One
ranked match is the complete six-game mirrored set. Helio Kite, Scrap Jackal,
and Glass Manta are manifest-discovered but entitlement-locked for future
achievement, challenge, and competition sources respectively. Mantis unlocks
the first time any of the account's bots reaches 1300 rating on an official
ladder.

Frontline's selected base bodies are renderer-owned form presentation rather
than account cosmetics. Trident Wasp supplies Striker mobile and three-barrel
Volley bodies; Aegis Tortoise supplies Bulwark mobile, omnidirectional turret,
and exact-facing-quadrant Shell bodies; Lattice Loom is the one identical
mobile chassis used by every Fabricator life. They live under
`assets/class-looks`, never enter appearance options, and pair respectively
with internal Trident Spark, Rebound Diamond, and Lattice Rivet masks. An
authored replay form look/projectile still wins; this internal mapping exists
for Labs replays whose presentation metadata predates the art.

Arc Relay follows the same internal-default route with one genuine SVG package
for each launch class: Kestrel, Palisade, Towline, Patchbay, Lantern, Mortar,
Minesmith, Hush, Relay, Switchback, Longshot, Mason, Sunder, Repulsor, Veil,
and Nest. Their IDs are `arc-<class-id>` and their common basic projectile is
the renderer-tinted `arc-pulse` class-projectile mask. The canonical sources
are regenerated by `scripts/build-arc-relay-class-art.mjs`; each uses
restrained semantic team-accent surfaces and `low-hover`. These defaults never
enter account appearance options. Later alternate skins remain independent
entitlement/store packages. The SVG remains the site, Canvas2D, loading, and
failure representation. The hosted WebGL path additionally lazy-loads the
owner-approved sixteen-model Meshy T2 fleet pinned by
`art/class-models/provider-runs/meshy/arc-fleet-review/fleet-audit.json`.

The frontend keeps the two routes deliberately separate:

- `botLook()` and `botLookOptions()` resolve account cosmetics only. Alternate
  class skins remain normal entitlement-backed cosmetics and expose their
  intended family through `BotLook.classId`.
- `presentationBotLook()` can additionally resolve the internal
  `assets/class-looks` packages, but those packages never enter appearance
  options. `presentationProjectileLook()` does the same for
  `assets/class-projectile-looks`.
- `unitLook()` resolves authored per-form presentation, then the internal
  class-form compatibility mapping, then the participant's snapshotted
  cosmetic, then the legacy slot fallback. `unitProjectileLook()` uses the
  corresponding authored form, internal class pair, participant projectile,
  and Pulse Bolt order.
- Canvas2D and WebGL both consume these shared unit resolvers and the same
  replay-resolved team accent. `classId` describes a cosmetic; it does not
  override the replay's authoritative form or team.

To create another look:

1. Start with genuine SVG. Design the silhouette and major internal shapes for
   gameplay scale before adding detail.
2. Use a transparent 512 viewBox and inspect for embedded images, filter
   clipping, and hairline seams. Fall back to raster only after gameplay-scale
   comparison shows that intentional surface art is materially worse in
   vector; then preserve the master, remove its background, and derive a clean
   512×512 RGBA PNG.
3. Add its `look.json`; no TypeScript edit is required.
4. Test East, South, West, and North facings; movement, recoil, damage,
   destruction, fogging, and the telemetry thumbnail.
5. Verify the sprite never obscures neighbouring cells or appears smaller than
   health and projectile indicators. Inspect at actual gameplay size and at
   device pixel ratios 1 and 2; a large standalone preview is insufficient.

### WebGL model companion

The sprite is still the canonical look. It serves site cards, Canvas2D,
mobile, the self-contained CLI viewer, loading, and WebGL failure. A genuine
3D companion is an additional representation for the hosted WebGL renderer.
The approved Striker mobile look, Trident Wasp, and all sixteen Arc Relay class
looks now lazy-load genuine GLBs. Other looks and non-mobile forms use the
sprite-derived WebGL fallback:

- Author the SVG first and approve its class identity, silhouette, team-accent
  surfaces, and rule-bearing hardware. Then translate that design into actual
  hull, armor, recess, joint, vent, and weapon geometry. Extruding the SVG is
  the compatibility fallback, not the authored-model workflow.
- Check in an editable `.blend` source plus a deterministic generator/export
  recipe under `art/`. Runtime packages contain only `model3d.json` and a
  self-contained `model.glb` beside the look.
- Models face `+X`, use `+Y` as up, sit on `Y=0`, and are scaled relative to a
  gameplay tile. They contain no camera, light, floor, trail, or rules data.
  The manifest declares whether the asset is a whole bot/projectile or a
  renderer-owned part such as one turret arm.
- An approved provider model may be monolithic when real-replay evidence clears
  it. In that case the manifest keeps root-level motion tuning but omits invented
  node names; only per-node hardware/emissive animation is disabled. The Arc
  fleet's exact candidate geometry, orientations, hashes, and counts come from its
  `fleet-audit.json`. Shipping textures come from the separately audited
  `arc-relay-ktx2-selective-v1` tier and are promoted/checked by
  `scripts/class-models/promote-meshy-arc-fleet.mjs`.
- Arc Relay's selective mipmapped texture contract is 512 ETC1S base color,
  256 UASTC tangent normal, 256 UASTC metallic/roughness, and 128 ETC1S emissive.
  The tier builder must prove byte-semantic accessor equality with the approved
  candidate; texture optimization is never permission to alter geometry, facing,
  topology, or normalization.
- Measure three costs independently: network transfer, compressed-target GPU
  residency, and RGBA8 fallback residency on devices without a supported target.
  The fleet audit hard-limits these values and the fixed decoder payload. A small
  `.glb` is not evidence of a small decoded texture footprint.
- The Three KTX2 transcoder is initialized only inside the lazy WebGL renderer,
  before any actor can request a model. Canvas2D and self-contained CLI output must
  not include the decoder or GLB package.
- Presentation motion such as Striker's shallow `low-hover` belongs in the
  canonical `look.json`, not `model3d.json`. The SVG fallback and any future
  approved GLB must receive the same cue without duplicating metadata.
- The renderer discovers model manifests only from `render3d`. It caches the
  downloaded source by URL, shares immutable geometry, and clones nodes and
  materials per actor because team paint, fog, selection, and hits mutate
  presentation state. A missing, malformed, or failed model returns to the
  sprite-derived fallback.
- `NB_TEAM_ACCENT` (or any future equivalent) carries
  `extras.nilbotsRole = "team-accent"`. It remains a restrained separate
  material, so the replay-resolved color and glow can change without tinting
  authored hull maps. Projectile tint surfaces use
  `extras.nilbotsRole = "projectile-mask"`.
- The approved Arc Meshy fleet is an explicit exception to model-owned team
  paint: its textured monolithic meshes stay untouched. Do not infer semantic
  surfaces, hue-key, split, or repaint them merely to satisfy this optional
  material convention; team ownership stays in renderer-owned cues and effects.
- Broad flat colors are not a sufficient translation when the sprite depends
  on layered values, panel lines, material contrast, or surface wear. In that
  case use compact embedded base-color, normal, metallic/roughness, and
  emissive maps. Judge them under arena lighting at the moving gameplay
  camera; a close turntable cannot prove that texture or micro-geometry
  survives play.
- Model packages load on first use and emit as separate hosted assets, so the
  decision is per-look rather than one catalog-wide bundle. Compare a lean and
  a richer tier with exact bytes, triangles, material count, atlas dimensions,
  first-use transfer, and decoded residency. A larger tier earns its cost only
  through visible gameplay-scale improvement. Compare under the real director and
  player camera—browser zoom or an enlarged crop is not camera evidence. The
  theme-scoped CLI outputs must remain byte-identical because they cannot render
  GLBs.

#### Multiview-AI to human handoff pilot

The next Striker attempt may use a multiview generation service to produce a
base mesh from the canonical SVG and approved pinned model sheet. This is an
unproven vertical slice, not a validated replacement for authored modeling:

- Preserve input images, hashes, service/model version, settings, raw outputs,
  and usage/license terms under `art/`. A provider preview is review evidence,
  not runtime art.
- A human modeler or technical artist must correct silhouette and proportions,
  retopologize, separate functional pieces, repair geometry, build UVs/PBR
  materials, isolate semantic team paint, and set scale, axes, pivots, and
  floor/hover placement. The corrected editable source is the candidate.
- Re-run the same canonical overlay, gameplay-camera, team-color, rule-cue,
  performance, on-demand transfer, and fallback gates. Multiview consistency
  does not waive any gate.
- Finish and assess one Striker end to end before generalizing this route to
  Bulwark, Fabricator, projectiles, or maps. Until then, describe the route as
  a trial and the compact procedural-proof record as rejected evidence, not a
  proven production method. The rejected generated binaries and sources are
  intentionally not retained.

### Sustained mobile-watch budget

A replay is a minutes-long workload, so phone acceptance is based on sustained
renderer work rather than first-frame speed. Browser code cannot make a
portable temperature claim; it can own and bound the work most directly tied
to heat: presentation frequency, drawing-buffer pixels, live shadow-map pixels,
draw submissions, and Canvas2D operations.

- Select the mobile profile from input and viewport capabilities, never from a
  user-agent string. A coarse pointer or a touch phone-sized viewport receives
  the profile. `?render-profile=full` and `?render-profile=mobile` are evidence
  overrides for same-build A/B only, not a player-facing graphics menu.
- While a replay or live broadcast advances, mobile WebGL and Canvas2D present
  at 30 fps. Paused WebGL/Canvas2D presentation runs at 12 fps so selection,
  camera settling, emissive breathing, and other micro-life remain alive.
  Replay interpolation remains wall-clock-based; authoritative telegraphs and
  discrete facts do not move earlier or later.
- Mobile drawing buffers cap at DPR 1.5. WebGL retains antialiasing, all actors,
  effects, fog, and shadows, but uses a 1024² rather than 2048² shadow map and
  requests the browser's low-power GPU. The full profile remains the unthrottled
  DPR-2, 2048² visual reference.
- At the fixed 844×390 CSS / DPR-3 phone viewport, active WebGL must remain at
  or below 54 million weighted pixels per second, charging one full color pass
  plus one complete shadow-map pass per presented frame. The exact arithmetic is
  regression-tested. Canvas2D is charged by its actual backing-buffer area.
- Validate both the hosted WebGL path and the Canvas2D host/fallback. Use
  `scripts/profile-mobile-replay.mjs` on a real replay, confirm its renderer
  profile data, inspect a fixed-camera full/mobile image pair at actual game
  scale, smoke representative replays in WebKit and Chromium, and finish with
  a real phone watch. Software WebGL is useful as a stress path, not as a proxy
  for an iPhone GPU's achievable frame rate.

## Projectile-look contract

- Genuine SVG with `viewBox="0 0 256 256"`, transparent and facing East/right.
- The sprite is a white alpha mask for one compact projectile head. Opacity may
  create internal energy layers, but it must not contain fixed colors, embedded
  rasters, text, a floor, cast shadow, trail, glow, impact, or scenery.
- The silhouette must remain identifiable around 16–28 gameplay pixels and at
  cardinal and diagonal headings. It must not visually claim a wider hitbox,
  longer path, different speed, range, collision, or damage.
- Record a stable ID, label, sprite filename, and gameplay-tile scale in
  `web/src/assets/projectile-looks/<id>/look.json`. Scale stays within 0.3–0.7.
- The renderer tints the alpha mask with the bot's locally contrast-adjusted
  accent. It owns the shared trail, glow, muzzle treatment, fog visibility,
  impact effects, and interpolation over authoritative traversal paths.
- Projectile appearance belongs to the bot beside accent and chassis. The CLI
  reads `appearance.projectile`; the server stores it on the bot and snapshots
  it into match participants and replays. Historical playback never consults
  the current bot or account.
- Legacy, missing, or unknown IDs fall back to Pulse Bolt. Cosmetic
  entitlements are checked when equipping, never when rendering a replay.

Current projectile looks are Pulse Bolt, Ion Orb, Razor Shard, Arc Spark,
Regent Lance, Phase Needle, Cinder Disc, Helix Dart, Gravity Knot, Prism Fan,
and Talon, plus the six paired class-pack masks Vector Fork, Arc Cutter, Gate
Slug, Mirror Wedge, Copy Bit, and Rivet Punch. All are genuine SVG masks. Pulse
Bolt, Ion Orb, Razor Shard,
Phase Needle, and Cinder Disc are starter-accessible; Arc Spark unlocks after
the account completes its first unranked challenge match on the official
service. Regent Lance unlocks with Aureate Warden after 100 completed ranked
matches. Helix Dart, Gravity Knot, and Prism Fan are independently
entitlement-locked for future achievement, challenge, and competition sources.
Talon unlocks with Mantis at 1300 rating; the two share an unlock source
without the chassis manifest recommending the projectile. The six Frontline
store packs do declare their projectile companions: each pack grants one
complete visual pair, while ownership still leaves chassis and projectile
independently equipable.

Trident Spark, Rebound Diamond, and Lattice Rivet are separate internal
class-form projectile masks. They are resolved after an authored per-form
`projectileLookId` and before a participant cosmetic only for class forms, so
historical Duel playback keeps the snapshotted participant projectile.

To create another projectile look:

1. Design the gameplay-scale silhouette first on the canonical 256 viewBox.
2. Add opacity layers only where they survive tinting and downscaling. Do not
   auto-trace or embed a raster.
3. Add its manifest and sprite; no TypeScript registry edit is required.
4. Test launch, stationary and imminent states, speed-one and speed-two
   movement, bends, impact, light/dark themes, fog, DPR 1/2, and the bot
   appearance editor.
5. Compare the self-contained viewer size before and after. A growing catalog
   eventually requires per-replay asset packaging rather than bundling every
   cosmetic into every viewer.

A projectile can use the same optional WebGL companion contract after its SVG
mask is approved. Its model is one readable head volume, not a baked trail,
glow pool, speed, range, or hitbox. Renderer tint remains authoritative; PBR
detail may shape grooves and reflections but cannot replace the semantic
projectile-mask material.

## Animation contract

Animations describe recorded events; they do not create events.

| Presentation | Authoritative source | Current treatment |
| --- | --- | --- |
| Movement | Consecutive replay states plus a declared Arc carrier relocation cadence | Monotone, causal tile-to-tile glide with boundary-speed continuity; carriers cross the tile edge on the resolved move boundary and keep travelling through relocation-locked ticks |
| Turning | Recorded facings | Shortest-angle rotation with causal angular-velocity continuity |
| Idle | Active status | Subtle hover and separate soft shadow |
| Firing | `Shot` event | Brief chassis recoil and layered beam/muzzle glow |
| Projectile travel | Recorded traversal path | Substep interpolation, glow core, and trail |
| Damage | `Damage` event | Short brightness flash, ring, and radial sparks |
| Destruction | `Destroyed` event | Collapse/rotation plus expanding sparks |
| Zone activity | Recorded zone tiles/control state | Optional theme-owned floor material, continuous tint, and pulsing exterior perimeter |
| Frontline | Recorded active position/claim/redeploy state | Five-position route, signed progress, claiming-team color, and redeploy timing |
| Fabrication/lifecycle | Stable unit and explicit lifecycle events | Unit-qualified spawn/rebuild status without inventing an active body |
| Anchor | Recorded pending transition plus start/change/cancel events | Source-body windup ring/status, then body swap only on `FormChanged` |
| Turret | Recorded current form/capabilities and absolute shot heading | Stationary/360 cue and heading-true muzzle/projectile treatment |
| Fog/vision | Recorded visible tiles/enemies/projectiles from every active teammate | Selecting a bot chooses its team perspective: both renderers union that team's published observations for one truthful shared-vision mask |
| Arc Relay Core | Recorded spawn/flight/possession plus the carrier's interpolated pose | One luminous sphere: low over its well or dropped tile, arcing in flight, then fixed above the carrier with subtle hover and a faint possession tether |

Arc Relay's compact spectator transport carries that visible-tile union once per
team per tick. The renderer may reconstruct the corresponding visible actors
from that tick's authoritative public world, but it must never recompute vision
geometry or substitute the spectator's omniscient state. Archived compact
broadcasts that predate the column disable perspective fog instead of presenting
an empty black board as if it were real vision.

Animation timing is expressed inside the current replay tick window. Do not
change tick duration, delay result disclosure, or extrapolate beyond received
live ticks to make an effect look better.

Ordinary position curves land exactly on every recorded tick boundary, remain
inside the current axis-aligned movement segment, and use only current plus
previously revealed displacement. A revealed right-angle turn carries scalar
speed into the new segment while taking its direction only from that segment; a
true reversal brakes rather than overshooting the path. A hold therefore stays
still even when a later tick moves. When an Arc contract declares a multi-tick
carrier relocation cadence, the renderer may spread the already-resolved move
across that cadence: the body remains on the origin side until the authoritative
move boundary, crosses the tile edge on that boundary, then keeps the same
visual speed through relocation-locked ticks. It never reads the next action or
direction. Rotation follows the same causal rule for consecutive same-direction
turns. Renderer-only hull lag may settle inside the chassis root after a move,
but it cannot start a future action early. Movement wakes and thrust remain on
through an active segment rather than pulsing at integer tick boundaries. Tread
and wheel scroll accumulates distance along this rendered path, including a
carrier's relocation-locked glide, rather than the underlying one-tile action.
Arc Relay's automatic director has a shared fifteen-tile closest shot in
Canvas2D and WebGL. It clusters sustained combat, treats a carrier as a camera
subject only while threatened, and holds an ordinary shot for seven replay
ticks before another theater may win. The manual overview fits the published
Wells, retaining all three theaters while allowing deep home aprons to leave the
frame. A compact causal HUD call beside the standing score identifies Core
birth, pickup/steal, drop, bank, and Pulse events; it does not replace their
diegetic world effects or reveal facts after the playhead.

## Art-generation brief

The Control Room and Overgrown Lab sources were generated as separate
production candidates, then normalized locally:

- Dark graphite, gunmetal, midnight blue, restrained cyan.
- Matte hard-surface materials with upper-left illumination.
- Top-down orthographic camera.
- No text, branding, people, horizon, perspective, or scene-level spotlight.
- Bot sources used a flat magenta removal background, no shadows, and one
  centered East-facing chassis.
- The original five looks were authored directly as path-based SVG. Each
  checked-in runtime SVG is its editable source and contains no embedded
  raster. The earlier generated PNGs for Vanguard, Bulwark, Needle, and Orbiter
  are retained only as unbundled art-direction references; their exact
  generation prompts predate the reproducible prompt log.
- Mantis and Talon were authored directly as path-based SVG with no generated
  reference, so `art/bot-looks` holds no raster master for them. The chassis is
  deliberately near-square where the other looks are longer along their facing,
  and Talon repeats its recurved claw in the projectile mask. They share the
  1300-rating unlock, but the chassis manifest does not name Talon as a
  companion. The six Frontline class packs are the later, owner-approved
  exception: each intentionally recommends the projectile sold in the same
  pack, while equipped chassis and projectile choices remain independent.
- Aureate Warden and Regent Lance were authored as genuine SVG from separate
  generated concept references. Eclipse Bloom + Null Seed and Redshift Crucible
  + Crucible Splitter are retained under `art/` as reserved, unavailable
  concepts; they have no runtime manifest or catalog entry.
- Rift Runner, Mossback, Helio Kite, Scrap Jackal, Glass Manta, and the five
  new projectile masks were authored directly as genuine SVG. Their catalogs
  are independent; none of those chassis declares a default projectile.
- Overgrown Lab uses pale ceramic/composite slabs, restrained moss, and
  mask-safe reinforced lab plating. Water channels were removed from the base
  material: a future river belongs in explicit map presentation data.
- Ember Forge, Frost Relay, Drowned Vault, Desert Array, and Void Sanctum use
  fifteen separately generated material fields—one floor, one fortified
  perimeter, and one interior-cover source per theme. Their exact accepted
  prompts live in `art/themes/SOURCE-PROMPTS.md`; deterministic baking owns
  seamless normalization and topology. Ember Forge and Frost Relay ship on
  Arena and Gallery respectively. Drowned Vault, Desert Array, and Void
  Sanctum remain complete staged packages under `art/`; keeping them outside
  Vite avoids adding every large theme to every self-contained viewer before
  per-replay asset packaging exists.
- Wall sources are generated as opaque material fields, never whole slabs or
  maps. The deterministic build makes them edge-safe, derives PBR helper maps,
  and bakes all topology variants. Do not ask an image model for a 16-, 47-, or
  256-cell atlas: exact registration drifts.

Generate distinct assets separately rather than asking for one large atlas.
Large generated atlases tend to drift in perspective and create mismatched
edges. Normalize, validate, and pack assets only after each source passes at
gameplay scale.

## 3D arena environment companions

Environment geometry follows the same two-stage rule: approve the 2D
theme/map package first, then add an optional WebGL representation. Frontline
ships the first topology-derived procedural environment: continuous family
solids, inset upper profiles, profile-contained deterministic service detail,
and lazy wall normal/roughness maps. No authored modular environment GLB
package or whole-map mesh ships. It is the first pilot because its camera,
cover, objective strip, class silhouettes, and projectile grammar exercise
the contract before it is generalized to other arenas.

- Start from `WallLayout`: trace continuous family solids, then layer
  manifest-owned upper-profile and material settings. Place optional panels,
  vents, and clamps from a stable family/coordinate/mask/side hash and keep
  them inside the narrowest reviewed profile. A future authored module kit may
  replace individual layers only after it preserves the same data authority.
- Walls may gain chamfers, recesses, profiles, damage, and less rectangular
  silhouettes, but the occupied tile and cover edge must remain immediately
  legible. Decorative overhangs cannot imply an opening, changed collision,
  sight line, spawn pad, or traversal route.
- Resolve open-floor relief from the largest approved live model's transformed
  vertices, not its nominal width. Compose GLB node transforms and runtime look
  scale, take the maximum XZ radius for arbitrary yaw/diagonal facing, add the
  review safety margin, and subtract the half-tile centreline clearance. If an
  extrusion bevel expands outward, add that reach to the source-outline inset
  and assert the final generated-vertex bounds.
- Solid geometry belongs on blocked tiles. Walkable cells retain only
  rule-honest flat or clearly non-blocking detail. Objective treatment cannot
  obscure capture ownership or pressure.
- The whole-arena concept approves material hierarchy, profile language, and
  lighting direction only. It is never cropped into runtime, sampled as a
  floor, or treated as layout authority. Record transferred and still-missing
  properties instead of claiming an exact match.
- Keep environment-only PBR maps under the lazy WebGL import tree. Canvas2D,
  site, mobile, loading, and the single-file CLI continue to use the canonical
  theme assets. An albedo reused as bump requires a shallow reviewed scale so
  baked highlights and wear do not become false geometry.
- Measure the environment's first-load transfer and GPU cost independently
  from on-demand bot looks. Prefer instancing and a small shared atlas over
  unique wall meshes or textures per cell; add LOD only after the actual
  Frontline camera demonstrates a benefit.
- Review every wall topology with both teams, all three default class bodies,
  projectiles, fog, health, objective effects, camera motion, and the Canvas
  fallback. “Less square” succeeds only when it also preserves or improves
  gameplay reading.
- Final evidence uses one fresh hash-verified native replay for the
  same-frame legacy/new A/B. Pin tick, viewport, camera, auto-fit, supported
  minimum span, and renderer; include a whole-arena overlay check and forced
  Canvas fallback. A harness-injected theme or fallback-theme board is useful
  diagnosis, not approval.

## Release checklist

1. `npm run build` produces the hashed hosted `dist/` build and one
   self-contained `dist-cli/<theme>/index.html` viewer per active theme.
2. Real replays exercise movement, turns, every changed bot look, projectiles,
   hits, destruction, fog, and zone visuals. Internal Frontline changes also
   exercise fabrication, lifecycle absence, Anchor start/change/cancel,
   stationary turret state, and all absolute shot headings.
3. The viewer remains readable at desktop and phone widths.
4. Replay theme, bot-look, and projectile-look IDs round-trip and are included
   in replay hashes for new matches; map presentation round-trips; legacy null
   fields remain omitted.
5. Every GLB passes header, coordinate-bound, semantic-material, source-hash,
   and size-budget validation. Review it in a real replay at gameplay scale.
   Verify the hosted build emits models separately and compare every
   theme-scoped CLI viewer against the pre-change size.
6. Generate local review viewers first. Publish through the private
   replay-highlights workflow only when the visual iteration is approved.
