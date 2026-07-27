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

The engine copies the theme and presentation object to replay header `themeId`
and `presentation`. The viewer resolves those IDs against theme manifests; it
never switches on `mapId`. Legacy maps and replays without the optional fields
fall back to the Control Room defaults.

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

art/themes/<staged-theme-id>/runtime/
  theme.json
  floor-*.webp
  wall-*.webp

web/src/assets/bot-looks/<look-id>/
  look.json
  sprite.png | sprite.svg

web/src/assets/projectile-looks/<look-id>/
  look.json
  sprite.svg

web/src/render/
  arenaThemes.ts     data loader and legacy fallbacks
  drawArena.ts       layered replay-driven rendering and effects
  interpolate.ts     authoritative state interpolation
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
source prompt with the change/PR. If a future 2.5D renderer is adopted, feed
the checked-in albedo/normal/height/roughness/AO maps to the DCC; do not
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
twelve are genuine path-based SVGs. The earlier generated PNGs remain under
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
and Talon. All eleven are genuine SVG masks. Pulse Bolt, Ion Orb, Razor Shard,
Phase Needle, and Cinder Disc are starter-accessible; Arc Spark unlocks after
the account completes its first unranked challenge match on the official
service. Regent Lance unlocks with Aureate Warden after 100 completed ranked
matches. Helix Dart, Gravity Knot, and Prism Fan are independently
entitlement-locked for future achievement, challenge, and competition sources.
Talon unlocks with Mantis at 1300 rating; the two share an unlock source
without the chassis manifest recommending the projectile, which per
DECISIONS #106 stays an Aureate Warden exception.

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

## Animation contract

Animations describe recorded events; they do not create events.

| Presentation | Authoritative source | Current treatment |
| --- | --- | --- |
| Movement | Consecutive replay states | Eased tile-to-tile interpolation |
| Turning | Recorded facings | Shortest-angle rotation |
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
| Fog/vision | Recorded visible tiles/enemies | Existing truthful visibility masks |

Animation timing is expressed inside the current replay tick window. Do not
change tick duration, delay result disclosure, or extrapolate beyond received
live ticks to make an effect look better.

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
  companion: per DECISIONS #106 that recommendation stays unique to Aureate
  Warden.
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
5. Generate local review viewers first. Publish through the private
   replay-highlights workflow only when the visual iteration is approved.
