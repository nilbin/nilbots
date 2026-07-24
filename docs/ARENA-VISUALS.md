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
- Props must tell the truth. Solid-looking machinery belongs on blocked tiles;
  walkable tiles may contain only flat details such as seams, stains, lights,
  cables, or grates.
- Presentation randomness is derived from stable map ID and cell coordinates.
  It must not flicker between frames or depend on `Math.random()`.

The current replay header carries `mapId` but no presentation manifest, so
`web/src/render/arenaThemes.ts` is the map-to-theme registry. All shipped maps
currently bind to `control-room`. A future publishable-map package should carry
an immutable theme manifest and asset hash; the renderer-facing contract should
remain the same.

## File layout

```text
web/src/assets/themes/control-room/
  floor-metal.png
  wall-bulkhead.png
  bot-vanguard.png
  bot-bulwark.png

web/src/render/
  arenaThemes.ts     theme registry, palette, assets, and bot-look metadata
  drawArena.ts       layered replay-driven rendering and effects
  interpolate.ts     authoritative state interpolation
```

The generated image sources are project assets, not runtime dependencies. Vite
inlines them into the self-contained replay viewer.

## Theme asset contract

### Floor material

- Opaque square PNG, currently 1024×1024.
- Exactly orthographic/top-down; no horizon or perspective.
- Even upper-left illumination without a central spotlight.
- Large readable material features; avoid high-frequency noise that aliases at
  a 40–70 px tile.
- No baked gameplay grid, props, text, logos, or deep shadows.
- The renderer deterministically samples material patches, adds grid lines,
  and may add flat service lights or vents.

### Wall material

- Opaque square PNG, currently 1024×1024.
- Top-down armored material, visually heavier than the floor.
- No surrounding floor or outer ornamental frame.
- Must tolerate being cropped into many square wall cells.
- The renderer computes open edges from neighbouring `#` cells and adds the
  directional bevel, shadow, outline, and occasional service light. The source
  image does not encode collision shape.

### Palette and registration

Create an `ArenaTheme` entry in `arenaThemes.ts` with:

- Stable ID and player-facing label.
- Floor and wall asset URLs.
- Canvas, floor, wall, grid, frame, and objective-zone colors.
- An ordered set of compatible bot looks.
- An explicit map binding. Do not add a viewer preference switch.

Check the theme on the smallest and largest shipped maps. Dense wall maps must
still distinguish open floor, blocked cells, zone tiles, bots, and projectile
paths at a glance.

## Bot-look contract

- Transparent 512×512 PNG.
- Exactly orthographic/top-down and facing East/right. The renderer rotates
  from that canonical orientation.
- One chassis only, centered with generous padding and a crisp silhouette.
- The visible chassis should occupy roughly 65–75% of the source canvas.
- No cast/contact shadow, floor, projectile, text, logo, or scenery. Shadows
  are rendered separately so they remain consistent between looks.
- Internal lighting may be baked into the chassis, but the silhouette and
  weapon direction must remain readable around 48 px.
- Record a stable ID, label, presentation accent, and render scale in
  `arenaThemes.ts`.

The starter vertical slice assigns the ordered looks by participant slot:
Vanguard to slot 0 and Bulwark to slot 1. That guarantees both silhouettes are
visible in every two-bot match. Player-selectable cosmetics should later add an
immutable `lookId` to replay participant presentation metadata; do not infer a
mutable account preference while replaying history.

To create another look:

1. Generate or draw the East-facing source against a removable flat background.
2. Remove the background, downscale to 512×512 RGBA, and inspect for colored
   fringes and transparent corners.
3. Register the look with a stable ID, accent, and scale.
4. Test East, South, West, and North facings; movement, recoil, damage,
   destruction, fogging, and the telemetry thumbnail.
5. Verify the sprite never obscures neighbouring cells or appears smaller than
   health and projectile indicators.

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
| Zone activity | Recorded zone tiles/control state | Low-amplitude holographic pulse |
| Fog/vision | Recorded visible tiles/enemies | Existing truthful visibility masks |

Animation timing is expressed inside the current replay tick window. Do not
change tick duration, delay result disclosure, or extrapolate beyond received
live ticks to make an effect look better.

## Art-generation brief

The Control Room sources were generated as separate production candidates,
then normalized locally:

- Dark graphite, gunmetal, midnight blue, restrained cyan.
- Matte hard-surface materials with upper-left illumination.
- Top-down orthographic camera.
- No text, branding, people, horizon, perspective, or scene-level spotlight.
- Bot sources used a flat magenta removal background, no shadows, and one
  centered East-facing chassis.

Generate distinct assets separately rather than asking for one large atlas.
Large generated atlases tend to drift in perspective and create mismatched
edges. Normalize, validate, and pack assets only after each source passes at
gameplay scale.

## Release checklist

1. `npm run build` produces one self-contained viewer.
2. A real replay exercises movement, turns, both bot looks, projectiles, hits,
   destruction, fog, and zone visuals.
3. The viewer remains readable at desktop and phone widths.
4. The replay hash and engine test results are unchanged by renderer-only work.
5. The generated viewer is published through the private replay-highlights
   workflow for product review.
