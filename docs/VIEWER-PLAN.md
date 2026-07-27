# Viewer plan

Ordered work for the replay viewer: payload, correctness, then depth. Written after an
audit of `web/src/render/` and the review build, and after measuring rather than guessing.

Status: payload splitting, adaptive atlases, asset-readiness gating, composited fog, and
the planned depth/audio foundations are implemented. The meaningful remaining items are
the deferred renderer-maintainability extraction and soundtrack-dependent mixing work.

The renderer is shared by four consumers — the site, the CLI's single-file artifact, the
hosted review build, and the mobile app's WebView — so every item here lands in all four
unless noted. Each also touches the replay viewer, which is a CLI compatibility surface:
a `CliVersion` bump and `publish-cli` are required before deploy. **Batch these rather
than shipping one at a time.**

## What is already right

Worth stating, because it decides against a rewrite. Themes, bot chassis and projectile
looks are manifest-driven through `import.meta.glob`, and `drawArena` contains **zero**
references to any theme or look id. Adding content is a folder with a `manifest.json`,
not a code change — which is how there are 4 themes, 12 chassis, 11 projectile looks and
4 audio packs without the renderer knowing about any of them. A v2 would put that at risk
to fix problems that are local to one function and one build config.

## 1. Payload and atlas resolution — DONE

**The scaling cliff was bundling, not code.** The original build inlined every eager
theme, chassis, and projectile look into a 15.2 MB single-file artifact while a replay
used one theme, two chassis, and two projectile looks. That would have grown linearly
with content; theme-scoped CLI builds and the hashed hosted build remove that shape.

**Atlas resolution should follow the device.** Measured, for a 24×18 map with 16-column
atlases of 192-content + 2×32-gutter cells:

| atlas | content px per tile | decoded RAM per atlas |
|---|---|---|
| 1024 | 48 | 4 MB |
| 2048 | 96 | 16 MB |
| 4096 | 192 | 64 MB |

Against demand: a phone needs 31–62 device px per tile, a laptop at 2× needs 84, at 3×
needs 126, a large desktop at 2× needs 116.

So **4096 is 1.5–2× oversampled even on a large desktop** and costs 64 MB decoded — the
memory cliff that made mobile tabs die, and the reason the review build downscales at all.
But **1024 is undersampled on any desktop** (48 px against 84–126), which reads as soft.
Neither single number is right; the variant should be chosen at load from
`devicePixelRatio` and viewport.

Implemented:

- build-time 1024/2048 variants retain 4096 as the source master;
- `preferredAtlasWidth` selects the variant from viewport and device pixel ratio;
- the site and mobile `?standalone` viewer use the ordinary hashed `dist/` build, so image
  URLs load and cache independently rather than arriving in one inline document;
- the CLI alone uses theme-scoped `dist-cli/<theme>/index.html` self-contained artifacts,
  with a build-time guard that fails if the scoping transform no longer matches.

Placement already survives rebaking: `wallAtlasDestination` derives destination from the
manifest's core:gutter ratio rather than the baked pixel size. That fix is a prerequisite
for variants, not an optional tidy-up.

## 2. Asset readiness and a loader — DONE

Originally `loadImage` fired `new Image()` and returned it. **Nothing awaited it and
nothing gated playback**, so a replay started at tick 0 while atlases were still decoding
and the arena popped in mid-match. That was a correctness bug, not polish.

`assetReadiness.ts` tracks outstanding decodes, `usePlayback` holds tick 0 until they
settle, and the viewer shows progress. Hosted bridge state carries `loading` and
`pendingAssets`, so the native mobile chrome receives the same readiness truth even though
the WebView renders only the canvas.

## 3. Split `drawArena` into passes — DEFERRED, and why

**Golden-frame tests are done** (`tests/goldenFrames.test.ts`), which was the precondition.
The extraction itself is deferred on inspection: the call sequence in `drawArena` is
*already* an explicit ordered pipeline — floor, zone, cones, walls, spill, fog,
projectiles, sounds, bots, shots, impacts — and inserting a pass is adding a function to
that list, which is exactly how the light pass was added.

So extraction buys **maintainability, not capability**. The inner functions close over
some twenty shared values (`ctx`, `tile`, `px`, `py`, `theme`, `currentTick`, `poses`…),
so hoisting them means threading a context object through 835 lines — a large diff whose
only guard covers geometry rather than textures, since atlases do not load under Node.
Worth doing when the file next needs real work, not as an end in itself.

### Original rationale

It is **one ~835-line function**; the file has four top-level functions. That is the
maintainability problem, and it is exactly where every item below lands.

Passes: floor → walls → zone → shadows → entities → light → overlay. A pass pipeline is
what depth needs anyway, because draw order *is* depth order, and it is what makes fog
compositing and lighting insertable at all. The refactor is the enabling step, not
overhead.

**Guard it with golden-frame tests.** Replays are deterministic, so render a fixed replay
at fixed ticks and hash the canvas. Write those against current output *before*
refactoring, so any unintended pixel change is caught immediately. This is strictly better
than keeping a parallel v1 around: no fork, no double maintenance.

## 4. Fog as a composited mask — DONE

The original two symptoms had one cause. `drawFog` painted flat `tile × tile` rects after
`drawWalls`, while cover and perimeter sprites **overhang their logical tile**. The fog
grid therefore sliced across wall art: a visible wall reaching a fogged neighbour was
half-darkened, while a fogged wall reaching a visible neighbour kept a bright sliver.

`fogMask.ts` now builds, blurs, and composites an offscreen mask over rendered pixels,
with a deterministic rectangle fallback where the platform cannot provide the required
surface/filter support. Fog therefore no longer disagrees with wall overhang geometry.

**Not in scope here:** the *shape* of wall occlusion. That a tile diagonally behind a wall
is visible is the engine's corner-strict supercover rule (`Visibility.HasLineOfSight`,
DECISIONS #11), deliberately chosen and pinned by tests. Widening it changes what bots
perceive: a `GameRules` bump, a new ladder, invalidated replay hashes and a balance
evaluation. It is a rules project and must not ride along with viewer work.

## 5. 2.5D depth cues — DONE, and narrower than written

The renderer already had more depth than this plan assumed: additive `lighter`
compositing on projectiles and impacts, accent bloom on bots via `shadowBlur`, and wall
relief and shadow baked into the atlases. **Light spill is done** — flashes now cast onto
the arena rather than only glowing on themselves.

**All of it is now done.** Contact shadows: bots already had one, projectiles did not,
which is what made bolts read as decals painted on the floor rather than something
travelling over it.

Wall height: the *top* of a wall is displaced, its cast shadow stays on the floor, and the
sliver between them is filled near-black as the exposed face. That gap is the whole effect
— moving the shadow with the wall would just slide the tile sideways and read as a
misalignment. The displacement is **outward** from the arena centre, not toward it as this
plan originally said: a camera above the middle sees a wall's top as nearer than its base,
so it projects further from the centre of frame. It is deliberately tiny (0.012 tile per
tile of distance, about a sixth of a tile in the far corner of a 24x18 map) because players
reason about cover in tile coordinates and the grid has to stay unambiguous.

Camera shake is derived from the tick, never accumulated between frames — this renderer is
called fresh with a time, so state would make the same moment render differently depending
on whether it was reached by playing or by scrubbing backwards. Only damage and destruction
shake; a camera that moves on every shot stops meaning anything.

Canvas2D throughout for *this* renderer — no WebGL here. The scene is a bounded set of bots
on a tile grid; rasterisation is not the bottleneck, payload is, and normal maps would
double atlas weight to fix a problem that does not exist.

**A second, WebGL renderer now exists beside it** (DECISIONS #126) — opt-in, lazily loaded,
and stubbed out of the CLI artifact. That is not a reversal of the line above so much as an
admission of its scope: it was an argument about faking depth, and it says nothing about
whether real depth is worth having. This renderer stays the default and stays flat.

The WebGL renderer now consumes only the version-neutral `ReplayModel` and shared
presentation/interpolation derivations (DECISIONS #128). It handles variable stable-unit
collections, exact actor lives, absent fabrication/rebuild states, pending and completed
forms, stationary 360-degree turrets, eight-way absolute projectile headings, and variable
Frontline position counts without reading either replay wire format itself. Replay-v1 still
normalizes through its historical one-unit teams and therefore keeps the same behavior.

In rough order of feel per unit of effort:

1. additive light from muzzle flashes, projectiles and explosions (`'lighter'` compositing);
2. contact shadows under bots and projectiles;
3. wall height — offset cover tops outward from the arena centre and darken their faces
   (the `wall-cover-edges` / `wall-perimeter-edges` atlases already exist for this);
4. camera easing and a small shake on impact.

## 6. Audio depth

- ~~pan cues by tile x (`StereoPannerNode`)~~ **DONE**;
- ~~one `ConvolverNode` with a short impulse response to give the arena a room~~ **DONE.**
  The response is *synthesized*, not shipped — a recorded IR is another asset in a payload
  this project has spent real effort shrinking, and a plausible metal room is noise and an
  envelope. Deterministic noise, so two people reviewing the same fight hear the same tail.
  Cues send post-pan, so reflections inherit the cue's position instead of collapsing to
  the centre.

**Blocked on assets that do not exist:**

- duck music under impacts;
- adaptive music layered on health, control pressure and overtime.

There is no music in the repository. Audio candidates carry four cues each — `projectile`,
`impact`, `destroyed`, `unlock` — and nothing else, so both of these describe mixing a
signal that has never been authored. They are not code-blocked; they are waiting on a
soundtrack. Building the ducking bus before there is anything to duck would be scaffolding
for a decision nobody has made yet.

**Keep music out of the CLI artifact** when it does arrive. Cues are efficient (584 KB for
16), but loops are where audio payload explodes, and every `nilbots play` would carry a
soundtrack.

## Verifying

`npm run review` serves the review build to a real device — LAN, or `-- --tunnel` for a
public HTTPS URL. Judgements about how anything *looks or sounds* have to be made by a
human on a real screen and real headphones; the numbers above bound the argument but do
not settle it.
