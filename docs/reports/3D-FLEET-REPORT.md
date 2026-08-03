# Arc Relay authored 3D fleet report

## Production promotion correction (2026-08-03)

The owner-approved Meshy T2 fleet is now the permanent runtime fleet. Commit
`1cdce66a` correctly preserved the provider runs and built review evidence, but
its review builder staged the Meshy candidates only temporarily and restored the
older vector/procedural GLBs afterward. A later normal review build therefore
served those older files. This follow-up promotes the approved candidate bytes
without regenerating, modifying, splitting, or repainting them.

The source of truth is
`art/class-models/provider-runs/meshy/arc-fleet-review/fleet-audit.json`: Mason
comes from its audited pilot, Lantern and Mortar use the audited identity
normalization, and the other thirteen use the audited lay-flat-X normalization.
`scripts/class-models/promote-meshy-arc-fleet.mjs --check` proves each runtime
GLB equals its approved candidate byte-for-byte and that every manifest and the
fleet ledger match. The production fleet totals 10,638,692 bytes, 215,130
triangles, 16 materials, and 64 embedded textures; the largest look is Veil at
814,688 bytes, within the explicit 1 MiB on-demand per-look budget.

The provider meshes are intentionally monolithic and unsplit: one material and
four WebP textures per class, no named mounted nodes, and no model-owned team
glow. Root-level renderer motion remains live—lean/bank/pitch, hull
follow-through, wake/exhaust, cooldown venting, camera/position interpolation,
and idle restraint. Only unavailable per-node hardware, wheel, idle-part, and
model-emissive animation is skipped. Team identity remains renderer-owned; no
texture or topology was altered to manufacture an accent material.

The provider-free vector fleet, recipe, generated sources, and the remainder of
this original report are retained below as reproducible fallback and historical
evidence. Their runtime, named-group, semantic-paint, 512 KiB, totals, and
`build-arc-relay-models.mjs --check` claims are superseded by this production
promotion; they no longer describe the shipping Arc GLBs.

Follow-up validation: web tests pass 381/381; the production and review builds
pass; both emitted asset directories contain all sixteen approved candidate
hashes with none missing; the existing three-replay gallery was reinstalled;
and real playback passed desktop WebGL, 844×390 phone-sized WebGL, and forced
Canvas2D fallback with advancing ticks and no page or console errors.

## Result

Arc Relay's sixteen launch classes now resolve to deterministic GLB companions in
the optional WebGL renderer. Every model has a real distance-domed hull,
undercarriage, separately raised class hardware, semantic team-light surfaces,
emissive surfaces, and stable named nodes for renderer-owned motion. Canvas2D
remains canonical and unchanged; missing or invalid GLBs still retain the existing
safe renderer placeholder/fallback path.

This is presentation-only work. It changes no rule, map tile, collision, vision,
action, telegraph duration, replay contract, canonical replay, result, score, bot
sheet, or hosted product surface. Models and their renderer code are discovered
only by the lazy 3D path.

Two GLB passes failed the owner's taste gate. The first treated the approved 20°
baked-oblique raster as a new heightfield, then added generic hardware and another
set of physical lights. At the fixed 58° camera that double-projected and
double-shaded the drawing. The second removed the false dome but remained a coarse
192px embellished billboard; it was clearer, not good.

The current improvement route returns to the old renderer extrusion's coherent
orthographic basis. Pipeline v3 builds from the roster's archived named-group
vectors: the real `chassis` and `weapon-hardware` groups receive separate beveled
distance-transform domes and pivots. The premium baked-oblique masters remain
untouched as Canvas2D/taste references, not as a texture projected through a second
camera. A later taste pass removed the vectors' baked black depth silhouettes,
reduced wide outer ink much more than fine panel seams, corrected sidewall UVs that
had stretched the whole albedo around every edge, and normalized useful silhouettes
to at most 0.9 tile. Real geometry, bevel light, and contact shadow now define the
contour instead of three stacked black outlines.

This is materially better and game-readable, but it is still stylized layered
extrusion rather than bespoke DCC modeling. Its finish is crisper and more coherent
than the failed raster relief; it does not reproduce all of the premium raster
masters' small machinery or painterly material richness. That remaining ceiling is
recorded here rather than hidden behind a passing asset contract.

## Review evidence

The standalone, outcome-blind review is
[`art/reviews/arc-relay-3d/index.html`](../../art/reviews/arc-relay-3d/index.html).
It leads with a tick-000 Canvas2D/WebGL pair and indexes every class beside its 2D
counterpart. Each class image contains both amber and cyan assignments taken from
real arena frames, not isolated showroom renders.

Capture invariants:

- viewport: 1440×900;
- WebGL camera: fixed 58° whole-board overview;
- replay version: canonical replay-v3 identity carried by a deterministic
  Arc Relay broadcast-v1 browser transport;
- primary seed: 42;
- no result or winner appears in the review index;
- both renderers were played from tick 000, observed advancing to tick 002,
  paused, restarted, and captured at tick 000;
- four browser captures reported zero page errors, console errors, and failed
  requests.

The primary canonical replay is 253 ticks and ends naturally through the real
engine. Its SHA-256 is
`507ed83e13f4ec9a99271a532167471e542eaa11b8507afef219a2291b45fb1c`.
The 66,855-byte review transport preserves that identity and has SHA-256
`79defbd28e5d73c7f5a3bda3011bd468920a7c429e6c9350778d68252ff4d1a9`.
The pinned evidence includes Core birth at tick 25, first bank at tick 60, and
first Pulse at tick 101.

A second 284-tick replay swaps all class assignments between teams so each model
is inspected under both team colours. Its canonical SHA-256 is
`a2b1e24964a879f482aff92152e273ff2a848edc40ea88570f8176dfb517f4f9`;
its 82,942-byte transport SHA-256 is
`85f8f85e99a87c8157fe4c682f13d1fc2800b2ea95a32940bb8da6c204048e93`.

An additional exhaustive 600-tick canonical replay exercises every one of the
sixteen signatures, six steals, fifteen handoffs, five banks, and a Pulse. Its
SHA-256 is
`b76b692622298cb2d7ab8a65a37950bb39650657987cdd96f976e7655474f2b9`.
It is retained as CLI/audit evidence rather than used as the interactive browser
payload because its canonical document is roughly 120 MB uncompressed.

## Deterministic asset pipeline

The editable fleet recipe is
[`art/class-models/arc-relay/fleet.json`](../../art/class-models/arc-relay/fleet.json).
`scripts/build-arc-relay-models.mjs` is provider-free and byte-deterministic. It
reads each orthographic named-group SVG, rasterizes the identity drawing without
baked depth or baked team colour, generates group masks and physical normal and
emissive maps, then writes `model.glb` and `model3d.json` beside each `arc-*`
runtime look. The original vector and both archived premium raster layers
participate in the provenance hash; the generator's own SHA-256 does too.

The full fleet totals 5,464,772 bytes, 90,266 triangles, 80 materials, and 48
embedded textures. Every model is below the 512 KiB per-look budget. Every model:

- faces +X, uses +Y up, and sits at floor Y=0;
- contains no camera, light, animation, or skeleton;
- exposes `underbody-locomotion`, `chassis`, `weapon-hardware`, `team-accents`,
  and `emissives` groups;
- isolates team paint with `extras.nilbotsRole="team-accent"` and bakes neither
  team's colour;
- embeds 256×256 albedo, tangent normal, and emissive textures;
- records exact bounds, triangle/material counts, bytes, and SHA-256 in the
  generated fleet ledger and runtime manifest.

Reproduction commands:

```sh
node scripts/build-arc-relay-models.mjs
node scripts/build-arc-relay-models.mjs --check
```

The `--check` path rebuilds everything in memory and requires every tracked output
byte to match.

## Per-class fleet ledger and visual audit

The visual notes are deliberately judged at the fixed gameplay camera. “Strong”
means the class survives that scale; it does not claim close-up DCC quality.

| Class | Locomotion / handling | Authored hardware | Signature treatment | GLB bytes / tris | Fixed-camera audit |
| --- | --- | --- | --- | ---: | --- |
| Kestrel | low hover / swift | dart fins | Vector Dash, simple | 292,612 / 4,404 | Strong. The red dart planform and forked nose survive the overview; rare Dash stays restrained. |
| Palisade | treads / deliberate | projector plate | Prism Wall | 408,908 / 7,324 | Good. Broad armored mass and pale projector face remain distinct, though the vector finish is plainer than its premium raster. |
| Towline | wheels / standard | winch boom | Tractor Hook | 332,480 / 5,560 | Good. Wheel stance and circular winch read; the hook itself remains a live-effect read rather than a strong idle detail. |
| Patchbay | skids / standard | repair arms | Repair Beam, priority | 285,228 / 4,544 | Acceptable. The compact service silhouette is clean but not unmistakable without its priority beam. |
| Lantern | low hover / swift | sensor dish | Survey Flare, priority | 342,664 / 5,428 | Strong. The star planform and concentric dish are the clearest revised silhouette; slow idle rotation remains non-fatiguing. |
| Mortar | treads / deliberate | mortar tube | Falling Star | 363,936 / 6,304 | Good. The planted rectangular body and circular tube survive the overview without an oversized anticipation prop. |
| Minesmith | wheels / standard | mine rack | Trip Node | 333,300 / 5,496 | Acceptable. Utility wheels and rear rack separate it in motion, but the compact round body is not a premium standalone read. |
| Hush | low hover / standard | dampener array | Null Field | 339,688 / 5,486 | Good. The purple diamond/dampener planform now separates cleanly from Veil before the null-field form appears. |
| Relay | skids / swift | Core cradle | Arc Toss | 312,652 / 4,744 | Acceptable. Cradle and compact thrower read at normal play scale; maximum overview still needs the Core/effect context. |
| Switchback | low hover / standard | twin frame | Exchange | 328,664 / 5,394 | Good. The mirrored paired frame is recognizable before the exchange line appears. |
| Longshot | skids / deliberate | long rail | Rail Line | 252,672 / 3,790 | Strong. The bright body-length rail remains the roster's clearest directional hardware. |
| Mason | treads / deliberate | builder rig | Hardlight Block | 418,924 / 7,754 | Acceptable. Its heavy construction mass is honest but still approaches Palisade at maximum overview; deployed blocks disambiguate it. |
| Sunder | low hover / standard | optic mast | Target Paint | 338,184 / 5,190 | Good. Star points and optic core distinguish it; the target form remains tile-centred. |
| Repulsor | treads / standard | radial emitters | Kinetic Burst, simple | 447,804 / 8,022 | Good. The circular emitter stance reads and remains under budget; rare Burst stays a short radial accent. |
| Veil | low hover / swift | smoke tubes | Smoke Canister, priority | 313,492 / 4,972 | Acceptable. The revised outline is clean, but tubes remain subtle; the volumetric smoke cluster supplies the high-frequency role read. |
| Nest | wheels / deliberate | pod rack | Sentinel Seed | 353,564 / 5,854 | Good. Wheel carrier and paired pods are clear; opposing pod idle shifts remain tiny. |

Patchbay, Minesmith, Relay, Mason, and Veil remain below a bespoke-model quality
bar at maximum overview. They are class-distinct in motion and signature context,
but their orthographic vector relief is more graphic than materially rich. That is
a visual-quality disclosure, not a request to enlarge effects, thicken outlines,
or compromise tile occupancy.

## Renderer-owned motion and state

All new animation is a pure function of replay time, authoritative state, and
manifest tuning. It keeps no previous-frame simulation state, so playing into a
moment and seeking directly to it produce the same pose.

- Actual tick displacement, not facing, drives lean. Hover bodies bank and pitch;
  treads/wheels dip on starts and stops; skids counter-steer during an authoritative
  turn. Facing remains the chassis/nose truth.
- Hover wake, dust, and skid cues point opposite displacement. Wheel/tread rotation
  uses signed cumulative real travel rather than the facing vector.
- The hull follows authoritative facing first. The named hardware node follows with
  a manifest delay: swift bodies catch with slight overshoot, standard bodies settle,
  and deliberate bodies grind into alignment.
- Hover bob, 3.5% emissive breathing, Lantern dish rotation, and opposing Nest pod
  shifts are deliberately small. A handoff receiver or channelled hold reduces idle
  gain to 18%, reading as braced rather than frozen.
- Signature cooldown is diegetic body state: signature emissives fall to 24% and
  three restrained vent motes rise from the chassis; ready restores authored light.

The maximum bank is 0.13 radians, maximum pitch 0.09 radians, and effects never
translate a body outside its authoritative tile.

## Signature and story effects

Every signature has a distinct procedural 3D form. Survey Flare, Smoke Canister,
and Repair Beam receive the priority treatment because they are high-frequency
reads. Vector Dash and Kinetic Burst remain simple. Other forms use standard
polish: prism panels, hook/repair/exchange links, Falling Star crosshair and beam,
Trip Node tetrahedra, Null Field disc/orbit, Arc Toss parabola, Rail Line,
Hardlight blocks, Target Paint, and Sentinel Seed markers.

Core birth/pickup, bank, and Pulse add deterministic world-space rings or sweeps.
The Threefold Wells now carry triangle, circle, and diamond source glyphs, and the
Reactors have an unmistakable pylon/cage silhouette. These are renderer geometry;
they do not alter map occupancy.

`arcSignatureVisualPhase` explicitly returns hidden before `startedTick` and at
or after the authoritative completion/end boundary. No anticipation begins early
or lasts a cosmetic extra frame. Impact shake was reduced from 0.14 to 0.045 tile:
a whisper on the limited existing impact channel, never signature-wide shake.

## Size and validation

The baseline was measured in this isolated worktree before implementation. The
production build proves that neither the hosted entry point nor any self-contained
Canvas2D CLI viewer absorbs the new model fleet. Only the already-lazy 3D chunk
grows; the GLBs remain separately cached assets fetched only by that path.

| Production artifact | Before | After | Delta |
| --- | ---: | ---: | ---: |
| Hosted main JS | 1,279,390 B | 1,279,390 B | 0 |
| Lazy 3D JS chunk | 749,679 B | 784,486 B | +34,807 B (+4.64%) |
| Sixteen lazy Arc GLBs | 0 | 5,464,772 B | +5,464,772 B |
| CLI control-room | 6,213,442 B | 6,213,442 B | 0 |
| CLI ember-forge | 4,901,071 B | 4,901,071 B | 0 |
| CLI frost-relay | 5,365,539 B | 5,365,539 B | 0 |
| CLI overgrown-lab | 7,913,679 B | 7,913,679 B | 0 |
| Four CLI viewers, sum | 24,393,731 B | 24,393,731 B | 0 |

The 1,312,224-byte layered source directory and 14,113,615-byte review package
are repository evidence, not runtime payload. The review package is intentionally
large because it retains six full 1440×900 arena frames and 32 two-colour crops.

| Check | Result |
| --- | --- |
| Web tests | 363/363 pass |
| Focused 3D tests | 12/12 pass: models, manifests, semantic paint, motion, and exact telegraphs |
| Full production build | pass: TypeScript, hosted Vite, atlas variants, and four CLI viewers |
| Repository `scripts/test.sh` | pass: 1,861 .NET passes, 78 pre-existing skips, 86 Python passes, WASM/release/deployment/database smoke checks |
| Deterministic model rebuild | pass: all 16 generated GLBs/manifests/source maps/ledger byte-identical |
| Browser capture smoke | pass: 4/4 frames, WebGL and forced Canvas2D fallback, tick 000→002, zero errors/failures |
| Static review audit | pass: 34/34 referenced images decoded, arena pair present, interactive link valid, no result text |
| Production size boundary | pass: main and all CLI viewers unchanged; only lazy 3D code/assets grow |
| Diff hygiene | `git diff --check` pass; no engine, rules, replay, App backend, hosted route, ladder, nav, or decision-log edit |

The final cold capture timings were 1,017 ms for primary Canvas2D, 3,199 ms for
primary WebGL, 1,019 ms for swapped Canvas2D, and 3,177 ms for swapped WebGL on the
development machine. They are smoke measurements rather than product latency
budgets; readiness in every case was gated until the first real frame and all
required assets existed.
