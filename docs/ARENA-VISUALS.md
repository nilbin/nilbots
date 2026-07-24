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
- Map-scale features such as rivers, trenches, cable runs, or vegetation paths
  must be explicit map presentation data. They do not belong in a reusable
  base material where collision could arbitrarily cut them.

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
  wall-bulkhead-v2.png

web/src/assets/themes/overgrown-lab/
  theme.json
  floor-ceramic-v2.png
  wall-overgrown-v2.png

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
- Medium-scale readable material detail; avoid high-frequency noise that
  aliases at a 40–70 px gameplay cell.
- No baked gameplay grid, props, text, logos, deep shadows, or map-scale
  features such as rivers and long conduits.
- The renderer maps the image once across the whole arena below the wall mask.
  It does not slice, shuffle, outline, or decorate individual gameplay cells.

### Wall material

- Opaque square PNG, currently 1024×1024.
- Top-down armored material, visually heavier than the floor.
- No surrounding floor or outer ornamental frame.
- Must be homogeneous and mask-safe: no large frames, rails, conduits, or
  motifs that look broken when the wall silhouette clips them.
- Each theme also provides a transparent 512×512 wall-trim donor and baked
  shadow donor. Generate the trim as one isolated square slab with a broad
  center, four detailed edges, four corners, and uniform transparent padding.
- The renderer constructs one connected shape from all `#` cells and clips the
  base material through it. It then uses ASCII adjacency only to crop and place
  the trim donor's north/east/south/west strips and convex corners. All bevels,
  lips, side faces, fasteners, grime, ambient occlusion, and cast-shadow pixels
  come from the theme assets; canvas code does not synthesize them.
- `sourceInner` and `sourceCorner` locate reusable regions in the donor.
  `inset` and `outset` control their world-space placement without changing the
  artwork. No trim is drawn between adjacent wall cells.
- Bake the shadow donor from the extracted trim with
  `scripts/build-wall-shadow.py`. It is composited before the connected wall
  top, while the trim donor is composited afterward.

### Zone material

- Optional opaque square PNG; the Overgrown Lab source is 512×512.
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
- Floor and wall filenames relative to the manifest, plus an optional dedicated
  zone-floor filename and its `scaleTiles` world scale. The renderer clips the
  fixed-scale material to the map's declared zone mask, so irregular zones
  remain continuous without per-cell borders or size-dependent distortion.
- Wall-trim and baked-shadow filenames, plus the donor's normalized
  `sourceInner`, `sourceCorner`, `inset`, and `outset` measurements.
- Canvas, floor, wall, frame, and objective-zone colors.

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
| Zone activity | Recorded zone tiles/control state | Optional theme-owned floor material, continuous tint, and pulsing exterior perimeter |
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
- Overgrown Lab uses pale ceramic/composite slabs, restrained moss, and
  mask-safe reinforced lab plating. Water channels were removed from the base
  material: a future river belongs in explicit map presentation data.
- Wall trims are generated as isolated square donor slabs on removable
  magenta, extracted to alpha, and reduced to 512×512. Do not ask an image
  model for a 16- or 47-cell atlas: exact atlas alignment drifts. One coherent
  donor gives the renderer repeatable edge and corner crops, while the baked
  shadow retains consistent softness.

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
