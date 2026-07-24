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

Each `maps/*.json` document names its standalone presentation package:

```json
{
  "id": "basic-01",
  "version": 4,
  "theme": "control-room"
}
```

The engine copies that value to replay header `themeId`. The viewer resolves
that ID against theme manifests; it never switches on `mapId`. Legacy maps and
replays without the optional field fall back to `control-room`.

## File layout

```text
web/src/assets/themes/control-room/
  theme.json
  floor-metal.png
  wall-bulkhead.png

web/src/assets/themes/overgrown-lab/
  theme.json
  floor-ceramic.png
  wall-overgrown.png

web/src/assets/bot-looks/<look-id>/
  look.json
  sprite.png

web/src/render/
  arenaThemes.ts     data loader and legacy fallbacks
  drawArena.ts       layered replay-driven rendering and effects
  interpolate.ts     authoritative state interpolation
```

Vite discovers both manifest families with `import.meta.glob` and inlines their
assets into the self-contained replay viewer. Adding a valid package requires
no TypeScript registry edit.

## Theme asset contract

### Floor material

- Opaque square PNG, currently 1024×1024.
- Exactly orthographic/top-down; no horizon or perspective.
- Even upper-left illumination without a central spotlight.
- Large readable material features; avoid high-frequency noise that aliases at
  a 40–70 px tile.
- No baked gameplay grid, props, text, logos, or deep shadows.
- The renderer maps the full image continuously across the arena. Adjacent
  gameplay cells sample adjacent UV rectangles, so seams and channels cannot
  become a shuffled collage.
- Grid lines and sparse coordinate-stable service details are layered on top.

### Wall material

- Opaque square PNG, currently 1024×1024.
- Top-down armored material, visually heavier than the floor.
- No surrounding floor or outer ornamental frame.
- Must read coherently as a continuous material field. Only regions selected by
  `#` cells are visible, but neighboring wall cells share adjacent UVs.
- The renderer computes open edges from neighbouring `#` cells and adds the
  directional bevel, shadow, outline, and occasional service light. The source
  image does not encode collision shape.

### Palette and registration

Create `web/src/assets/themes/<theme-id>/theme.json` with:

- Stable ID and player-facing label.
- Floor and wall filenames relative to the manifest.
- Canvas, floor, wall, grid, frame, and objective-zone colors.
- A service-light color.

Then set `"theme": "<theme-id>"` in the owning map JSON and bump that map's
version. The loader discovers the package automatically. Do not add a viewer
preference switch.

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
- Record a stable ID, label, suggested accent, sprite filename, and render scale
  in the look's standalone `look.json`.
- A look belongs to the bot. Player projects set it in `botarena.json`:

  ```json
  {
    "appearance": {
      "accent": "#22d3ee",
      "look": "needle"
    }
  }
  ```

  The CLI submits it as `lookId`; the server stores it on the bot and snapshots
  it as `lookIdSnapshot` when a match is created. The engine then copies it to
  replay participants. Historical playback therefore does not consult the
  bot's current account record.

Current looks are Vanguard, Bulwark, Needle, and Orbiter. Slot-based Vanguard /
Bulwark selection exists only as a compatibility fallback for old replays that
predate `lookId`.

To create another look:

1. Generate or draw the East-facing source against a removable flat background.
2. Remove the background, downscale to 512×512 RGBA, and inspect for colored
   fringes and transparent corners.
3. Add its `look.json`; no TypeScript edit is required.
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

The Control Room and Overgrown Lab sources were generated as separate
production candidates, then normalized locally:

- Dark graphite, gunmetal, midnight blue, restrained cyan.
- Matte hard-surface materials with upper-left illumination.
- Top-down orthographic camera.
- No text, branding, people, horizon, perspective, or scene-level spotlight.
- Bot sources used a flat magenta removal background, no shadows, and one
  centered East-facing chassis.
- Overgrown Lab uses pale ceramic/composite slabs, restrained moss and water
  channels, and root-wrapped lab bulkheads.

Generate distinct assets separately rather than asking for one large atlas.
Large generated atlases tend to drift in perspective and create mismatched
edges. Normalize, validate, and pack assets only after each source passes at
gameplay scale.

## Release checklist

1. `npm run build` produces one self-contained viewer.
2. A real replay exercises movement, turns, both bot looks, projectiles, hits,
   destruction, fog, and zone visuals.
3. The viewer remains readable at desktop and phone widths.
4. Replay theme/look IDs round-trip and are included in replay hashes for new
   matches; legacy null fields remain omitted.
5. Generate local review viewers first. Publish through the private
   replay-highlights workflow only when the visual iteration is approved.
