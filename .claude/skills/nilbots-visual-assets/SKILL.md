---
name: nilbots-visual-assets
description: Create, revise, bake, and validate nilbots arena themes, bot looks, projectile looks, and their 3D companions. Use when work involves visual concepts, concept-to-model fidelity, generated material sources, floor or wall textures, topology atlases, theme manifests, map presentation families, bot chassis or projectile PNG/SVG/GLB assets, PBR textures, visual bundle size, or gameplay-scale art review.
---

# Nilbots Visual Assets

Preserve ASCII gameplay semantics while improving presentation. Read
`docs/ARENA-VISUALS.md` completely before changing assets, then inspect the
owning map JSON, theme manifest, or bot-look manifest.

## Arena theme workflow

1. Generate distinct opaque material fields, never a whole map, isolated wall,
   or atlas. Keep the exact accepted prompt in `art/themes/SOURCE-PROMPTS.md`.
   When the floor is generated, keep its accepted source at
   `art/themes/<theme>/floor/source.png` and declare the optimized runtime
   filename, size, and WebP quality in `art.json`.
2. Put accepted wall sources under
   `art/themes/<theme>/walls/<family>/source.png`. Keep perimeter and interior
   cover as separate semantic families.
3. Update `art/themes/<theme>/art.json` and the runtime
   `web/src/assets/themes/<theme>/theme.json`. Keep their atlas dimensions in
   agreement. A deliberately staged theme instead sets
   `runtime.packagePath` to `art/themes/<theme>/runtime`; its complete package
   stays outside Vite discovery until a map intentionally ships it. Do not
   raise `assetBudgetBytes` just to make a build pass.
4. Rebuild in the disposable environment:

   ```sh
   python3 -m venv sandbox/theme-art-venv
   sandbox/theme-art-venv/bin/pip install -r scripts/requirements-theme-art.txt
   sandbox/theme-art-venv/bin/python scripts/build-theme-art.py art/themes/<theme>/art.json
   ```

5. For a shipping theme, assign the theme and wall families in map
   presentation data. Never infer a theme from map ID or add a viewer skin
   switch. Keep map packages within the engine's 32×32 envelope.
6. Review small and large maps at device pixel ratios 1 and 2. Inspect outer
   perimeter, isolated cover, corners, junctions, zone contrast, bots, health,
   projectiles, and fog.

The build owns floor resizing/encoding, seamless wall normalization, PBR
helpers, 256 topology variants, and the theme-wide runtime size check. Do not
hand-edit its outputs.

## Concept-to-model fidelity gates

For a modeled bot family, treat the approved 3D master as the presentation
authority. Existing 2D art remains an identity and silhouette reference while
the model is being approved; after approval, derive the site, store, mobile,
Canvas2D, loading, and CLI images from pinned renders of that master. Do not
let those derivatives drift into independent concepts.

1. Before modeling, add an art-side fidelity brief naming every approved
   concept/reference, intended gameplay read, required silhouette and
   negative-space anchors, rule-bearing hardware, material/color regions,
   team-accent regions, and cues the look must not promise. Nilbots bots use a
   low-hover ground language: a small air gap, underbody/team glow, tight
   shadow, and gentle renderer-owned bob without altitude, aircraft banking,
   rotors, walking limbs, or vertical traversal that promises player-controlled
   flight.
2. Approve one coherent 3D concept and its East-facing top, side, front
   three-quarter, and rear three-quarter views before a provider call. The top
   view must include a same-scale silhouette overlay against the prior 2D look
   when one exists. Invented side and underside structure may explain depth but
   must not redesign the approved planform. Never admit an unapproved inferred
   view as a new concept merely because it looks plausible.
3. Gate footprint, center of mass, major proportions, negative spaces, side
   profile, and weapon/form hardware in the approved multiview inputs. Repeat
   those gates on the textured provider master before accepting it. Return a
   failed anchor to the concept/provider round; textures cannot repair
   silhouette drift and deterministic normalization must not become redesign.
4. Require real armor thickness, recesses, joints, hover hardware, vents, and
   undersides consistent with the brief. Do not add mobility or combat
   affordances the rules do not provide. Reject inferred load-bearing feet,
   walking legs, or another locomotion cue; the static chassis must read
   correctly while the renderer supplies low hover, settle, recoil, and other
   rule-timed motion.
5. Preserve the approved material regions before adding wear or micro-detail:
   dominant light/dark/value grouping, authored accent placement, and
   class-readable color ratios. Keep semantic team material separate and
   texture-free unless its mask is explicitly authored.
6. Review a fixed contact sheet containing the concept reference, top-view
   overlay, textured gameplay camera, and at least one non-team-color
   turntable angle. Also review the model in a real replay at normal zoom, with
   both team colors and a tight ground shadow. Use one pinned
   replay/time/viewport/camera for every before/after; label a comparison
   approximate when any of those differ.
   Alternate forms/skins have a hard family gate: the same pinned 58° camera
   and one-tile frame beside the parent; matching value, material/PBR, detail,
   edge wear, and fixed emission; only semantic team-mask differences. Reject
   an attractive isolated render that reads as another chassis.
7. Require every named anchor and forbidden cue to pass independently. Record
   deliberate deviations in the fidelity brief with the approval reason; do
   not average a missing identity anchor into a general quality score.

### Meshy Standard Multi-Image production path

Use Meshy 6 Standard Multi-Image with its full textured/PBR output as the
primary bot-model route. This is the richer project workflow commonly called
the 30-credit path. It produced the original textured Striker, Bulwark, and
Fabricator masters. Source quality, semantic team materials, deterministic
normalization, gameplay-camera review, and runtime optimization remain
separate required stages; a provider preview alone does not approve a look.

1. Give the service four pinned owner-approved views of the same coherent 3D
   concept. Preserve the exact inputs, generation prompt, service/model
   version, settings, output files, and usage/license terms under `art/`.
2. Call the result a recoverable textured provider master and preserve it as
   source art. It becomes the family authority only after the fidelity and
   real-game gates pass. Never copy it directly into runtime merely because
   its provider preview is attractive.
3. Inspect and record which source axis the generated nose actually follows;
   provider tasks may reverse facing even when inputs and settings match.
   Normalize provider output deterministically before review: derive the
   transform from measured bounds and inspected source-facing axis, convert to
   `+X` facing and `+Y` up, center the planform, put the floor at `Y=0`, and
   normally scale the maximum planform span to one gameplay tile before the
   look's runtime scale. Record a deliberately larger span. Multiply authored
   span by every renderer/look scale before clearance review; do not infer
   facing from task order, test only one legal heading, or hide a repair in an
   undocumented DCC transform.
4. Keep the full normalized master for art review, then produce measured
   runtime derivatives from that same master. Record bytes, triangles,
   vertices, material count, map dimensions, validation warnings, and
   first-use transfer. Compare a lean shipping tier with a richer on-demand
   tier at the 58-degree gameplay camera; choose the richer tier only when its
   visible gain survives gameplay scale.
5. Put a candidate derivative through the manifest-driven lazy loader only in
   a review worktree. Exercise movement, every heading, hover/contact, shadow,
   damage, multiple instances, fog, and fallback in a real replay. Authored
   directional GLBs suppress the generic fallback-only facing wedge;
   SVG-derived models keep it. Use the evidence to decide whether the next
   work belongs in another provider round or deterministic correction.
6. Before real-replay review, render deterministic strict top, strict side,
   strict front, strict rear, both three-quarter views, and the gameplay
   oblique. Compare them beside the approved inputs. Repeat the same-scale
   top-silhouette gate and report coverage, centerline, and local width drift;
   reject a side/depth profile that changes the approved body class even when
   the top passes.
7. Prompting may confine team color to clean bounded inlays, but it does not
   create semantic runtime ownership. For a clean fused result, prefer a
   deterministic offline texture mask and face split over a runtime hue-key
   shader. Mark the resulting material
   `extras.nilbotsRole = "team-accent"`, neutralize provider team color in the
   hull maps, preserve the hull PBR maps, add a structural GLB test, and pass
   both-team review. The renderer owns tint, emission, bloom, and floor glow.
8. Use Blender or equivalent deterministic tooling only for measured
   post-processing: axes, scale, origin, cleanup, UV/PBR repair, semantic
   team-material isolation, tangents, decimation, and export. Do not use an
   inferred Blender rebuild as an unreviewed replacement for the approved
   Meshy form. If silhouette or body class still fails, return to the concept
   or provider round.
9. Run the normal top-overlay, forbidden-cue, gameplay-camera, team-color,
   static-form, performance, and fallback gates on the corrected result.
   Generated multiview agreement is not evidence those gates pass.
10. Record provider calls and credits, where deterministic correction was
    required, and compare quality, time, triangles, materials, textures, and
    first-use bytes with the fallback. Pin the accepted raw provider master,
    normalized/editable master, runtime GLB, geometry/UV/camera measurements,
    semantic team-emitter recipe, and hashes as the family reconstruction
    authority.

#### Meshy 6 Standard Multi-Image recipe

This is the primary provider recipe for a new Nilbots bot family. The settings
below reproduce the richer textured route; re-check Meshy's live API contract
and price before every paid submission.

1. Keep the API credential out of the repository, shell profiles, command
   history, screenshots, logs, and generated provenance. On the Nilbots macOS
   workstation it lives in Login Keychain under service
   `nilbots.meshy.api` and the current login account. Read it inside the
   calling process with `/usr/bin/security`; never print the returned value.
   Other environments inject `MESHY_API_KEY` from their secret manager rather
   than adding an `.env` file.
2. Check `GET https://api.meshy.ai/openapi/v1/balance` before a paid request
   and record only the numeric balance. Re-check Meshy's official pricing and
   parameter documentation; the accepted class masters consumed 30 credits
   each, but provider contracts can change independently of this repository.
3. Upload one object per task as four separate owner-approved images. Never
   upload a contact sheet as one image or mix a bot, projectile, detached
   weapon, or scenery in the same task. Prefer square lossless inputs at least
   1040 pixels on the shortest side, with consistent framing and object scale,
   a flat neutral or transparent background, diffuse lighting, no cast shadow,
   and no clipped geometry. Use strict top, strict side, front three-quarter,
   and rear three-quarter views. The top view derives from the approved 3D
   concept and, for an existing family, passes a same-scale overlay against its
   prior 2D planform before upload.
4. Keep geometry requirements visible and mutually consistent in the source
   images. Meshy's `texture_prompt` describes surface appearance;
   prohibitions such as “no landing gear” or “thin hull” there are not a
   documented geometry control.
5. Use `ai_model: "meshy-6"`, `should_texture: true`, `enable_pbr: true`,
   `texture_resolution: "4k"`, `should_remesh: false`,
   `image_enhancement: false`, `remove_lighting: true`, `pose_mode: ""`, and
   `target_formats: ["glb"]` for clean approved inputs. Do not enable Smart
   Topology, Auto Split, pose control, remesh, or 8K on this path. The goal is
   maximum recoverable form/material evidence, including emission;
   optimization happens after approval.
6. Set `image_enhancement: true` only for noisy, low-resolution, photographed,
   shadowed, or otherwise unreconciled inputs, matching Meshy's general
   guidance. For clean exact-detail inputs, keep it `false`; use a recorded
   same-input A/B when uncertain and do not mix source changes into the A/B.
7. Keep the texture prompt descriptive and prohibitive, not redesigning:
   preserve the approved broad material regions, panel seams, vents, bounded
   neutral/team-inlay candidates, and class-authored fixed emission. Add no
   text, logos, labels, decals, or baked lighting. Validate geometry
   exclusions from the views rather than relying on this prompt.
8. Record task ID, input filenames and SHA-256 hashes, sanitized settings,
   numeric balance before/after, `consumed_credits`, sanitized task JSON,
   provider/model version, license terms, and downloaded artifacts under
   `art/class-models/provider-runs/meshy/<look-id>/<task-id>/`. Strip signed
   URL queries and input data URIs. Never record authorization data.
9. Download every output immediately. Meshy's non-Enterprise API retention is
   short and must not be treated as storage. Keep raw provider output art-side
   and in durable project artifact storage; only a normalized, corrected, and
   reviewed derivative may enter a runtime look.
10. A default projectile is a separate provider task after its bot path passes.
    Its inputs show only the projectile head; Nilbots owns spin, trail, glow,
    travel, impact, hitbox, replay timing, and team tint.

Smart Topology or an untextured geometry-first trial is optional diagnostic
work after a measured Standard failure, not the default production route.
Retexture is the preferred paid path for later skins that preserve an accepted
family mesh and silhouette; it is not the primary way to construct the base
model. Keep those variants in store bundles until availability is chosen.

The accepted bot GLB is static: no skeleton, autonomous loop, authored walk,
deploy, stance, transform, or return clip is required. The renderer owns low
hover, settle, recoil, and rule-timed cosmetic response. If Shell or Tower
needs distinct hardware later, add a separately approved static companion or
attachment with explicit renderer state; do not block the base family on a
multipart animation experiment.

#### Fused team-accent extraction

Use this only when a reviewed generated mesh has one fused material but clean,
bounded team-color paint. It is an offline authoring step, not a runtime
shader.

1. Preserve the source model and lossless provider base-color/emission maps.
   Align the maps on an exact common grid; do not classify a resized JPEG or a
   screenshot. Build a candidate from both color and emission, require a
   stronger two-channel seed in every kept component, grow only a few
   deterministic pixels inside the loose base-color candidate, close
   one-pixel holes, and prune small unseeded components. Hue alone admits
   reflections and weathering.
2. Classify faces from several strictly interior UV samples, never only
   vertices or edge midpoints. The validated Striker recipe uses seven
   Dunavant interior samples and a conservative five-of-seven vote. Record the
   threshold configuration, selected faces, selected surface area, boundary
   histogram, estimated purity/coverage, and exact mask/model hashes. Treat its
   numeric cyan thresholds as Striker evidence, not a universal palette rule.
3. Partition the original index stream into hull and accent primitives while
   sharing untouched position, normal, and UV accessors. The triangle multiset,
   scene graph, transforms, facing, and geometry-buffer hashes must remain
   exact.
4. Remove classified color/emission pixels from lossless hull maps. Give the
   accent material neutral white base/emission factors, no fixed base or
   emission texture, the original normal and metallic/roughness textures, and
   `extras.nilbotsRole = "team-accent"` on both primitive and material.
5. Require a deterministic rerun hash, structural validation, close top and
   gameplay-oblique renders in at least two team colors, and real-replay proof.
   The renderer supplies tint, emissive response, and the floor glow; do not
   bake bloom or one team's color into the asset.

## Bot-look workflow

1. For a 2D-only look, start with genuine SVG. For a modeled family, first
   approve the 3D master, then derive a pinned East-facing transparent image
   from its fixed camera and lighting. Use PNG when it materially preserves
   the model's authored PBR/material identity; never auto-trace a render merely
   to claim vector.
2. Use the canonical transparent 512×512 canvas or SVG viewBox, facing East.
   A genuine SVG must not contain `<image>` or `data:image` raster payloads.
3. Keep high-resolution derived renders, source masters, camera/lighting
   recipe, and exact generation prompts outside the runtime package. Remove
   backgrounds before deriving the 512×512 PNG. When replacing a shipped
   raster, retain it under `art/bot-looks/<id>/raster-reference.png`, not beside
   runtime assets.
4. Add `web/src/assets/bot-looks/<id>/look.json` plus `sprite.png` or
   `sprite.svg`. Discovery is manifest-driven; do not add a TypeScript registry.
   A look may name an owned `defaultProjectile` companion; selecting that
   chassis recommends the projectile but never prevents the owner from
   choosing another.
5. Inspect the look at gameplay size facing all four directions and during
   movement, recoil, damage, destruction, fogging, and panel rendering.
6. For a modeled family, the approved 3D master owns presentation identity;
   keep `sprite.svg` or `sprite.png` as its required derived site, Canvas2D,
   mobile, CLI, loading, and failure fallback. For a legacy 2D-first family,
   keep the prior sprite as a fidelity reference until the owner approves the
   master and its newly derived fallback.
7. Build genuine depth through the Meshy Standard Multi-Image path, not a
   shallow sprite extrusion. Preserve rule-bearing hardware and the approved
   concept silhouette while giving the hull, armor layers, recesses, joints,
   vents, and weapon volumes coherent depth. Preserve the raw provider master
   plus the deterministic correction/export recipe under `art/`; an editable
   `.blend` is useful when that recipe uses Blender but is not permission to
   reconstruct the identity by inference.
8. Put `model.glb` and `model3d.json` beside the runtime look only after the
   model passes the gameplay review. The GLB contract is `+X` facing, `+Y` up,
   floor at `Y=0`, tile-relative scale, no camera, no light, and no animation.
   Discovery stays
   manifest-driven inside `render3d`; never add the model to the shared
   appearance registry or a hand-written TypeScript catalog.
   Presentation motion cues such as `low-hover` belong in the canonical
   `look.json`, not the model companion, so SVG fallback and an approved GLB
   behave identically.
9. Use compact PBR material maps when broad flat materials discard authored
   panel, finish, or wear information. Prefer one small per-look atlas (or a
   measured shared family atlas) over many tiny images. Normal and
   metallic/roughness detail must survive the actual arena lighting and camera,
   not merely a turntable close-up. Keep team paint on a separate material with
   `extras.nilbotsRole = "team-accent"` so the renderer can tint it without
   washing out the hull maps.
10. Compare at least the current shipping tier and one richer on-demand tier.
    Record GLB bytes, triangle count, material count, texture dimensions, total
    first-use transfer, and what is still visible at the gameplay camera. An
    increased per-look budget needs visible evidence; on-demand loading is not
    permission to ship invisible micro-detail. Verify that the CLI build remains
    model-free and that a missing or failed GLB returns to the 2D-derived
    fallback.

Bot appearance belongs to the bot and is snapshotted into matches and replays.
Do not introduce slot-owned selection except the existing legacy fallback.

## Projectile-look workflow

1. Start with genuine SVG on a transparent `viewBox="0 0 256 256"`, authored
   facing East. Current projectile sprites are white alpha masks: opacity may
   shape internal energy layers, but fixed colors, embedded rasters, floors,
   shadows, trails, and glows do not belong in the sprite.
2. Keep one compact projectile head centered with generous transparent
   padding. It must remain distinct around 16–28 gameplay pixels without
   appearing wider than its authoritative tile or implying a different
   hitbox, path, speed, range, or damage.
3. Add `web/src/assets/projectile-looks/<id>/look.json` plus `sprite.svg`.
   Discovery is manifest-driven; do not add a TypeScript registry. Keep scale
   within the tested 0.3–0.7 gameplay-tile range.
4. The renderer tints the mask with the bot accent and owns the common trail,
   glow, local contrast adaptation, fog truthfulness, and interpolation across
   authoritative replay traversals. Do not bake those effects into one look.
5. Test all cardinal and diagonal headings, launch, speed-one and speed-two
   traversal, bends, impact, light and dark themes, fog, DPR 1/2, and the
   appearance-editor preview.
6. After the 2D projectile is approved, it may receive a genuine
   `model.glb`/`model3d.json` WebGL companion under the same coordinate and
   source rules as a bot. Model a readable head volume and rule-honest
   directional silhouette; do not model a trail, floor glow, range, speed, or
   hitbox. Every tintable projectile material uses
   `extras.nilbotsRole = "projectile-mask"`. Compact texture maps may retain
   grooves and surface breakup, but renderer-owned team tint and emissive
   response must remain intact.

Projectile appearance belongs to the bot beside its chassis and accent. The
bot record snapshots it into each match and replay. Legacy or missing IDs use
the default Pulse Bolt. Entitlements authorize equipping a look; they never
change replay rendering or gameplay.

When adding a shippable bot or projectile look, also add the same stable ID and
label to `cosmetics/catalog.json`. Keep availability and unlock policy there,
never in the rendering manifest. Run the catalog/manifest alignment tests.
Unshipped concepts belong only under `art/`; do not add a runtime manifest or
catalog entry until their availability and unlock are intentionally chosen.

## 3D arena-companion workflow

The 2D arena-theme workflow remains first and canonical. A map may receive a
WebGL environment companion only after its floor, wall families, topology
atlas, collision map, and presentation truth are already approved. Frontline
is the first environment pilot; do not generalize the runtime contract from an
unreviewed second map.

1. Approve a whole-arena concept at the real gameplay camera before modeling
   modules. Use it to approve material hierarchy and shape language, not map
   geometry: the map JSON remains the only layout and collision authority.
2. Derive modular floor, perimeter, interior-cover, corner, junction, and prop
   pieces from the approved theme materials. Give walls real profiles,
   chamfers, recesses, and restrained non-square silhouettes while keeping the
   authoritative occupied tiles unmistakable.
3. Geometry must never imply a changed collision, opening, sight line, zone,
   spawn pad, or traversal route. Decorative overhangs stay inside a tile's
   readable footprint; solid-looking machinery remains on blocked tiles.
4. Keep gameplay topology in the map contract and presentation in the
   companion package. The renderer resolves pieces from explicit wall
   families/tags; it must not infer rules from mesh names or pixels.
5. Keep a shared world language with the approved class concepts: graphite and
   bronze construction, restrained amber machinery, layered armor, recessed
   vents, and swept/chamfered profiles. Preserve visual hierarchy rather than
   matching finish one-for-one. Environments are rougher, more matte,
   coarser-detail, lower-contrast, and less saturated; bots and projectiles are
   cleaner, sharper, finer-detail hero assets with stronger controlled
   emission. Team cyan/red belongs to actors, not neutral architecture.
6. Keep the floor a continuous theme material whose seams, wear, channels, and
   localized transitions never imply collision. Do not turn each walkable tile
   into a separate raised mesh or bake the current layout into one floor model.
7. Render spawn protection and objective/capture fields as low-relief,
   data-authoritative overlays or decals, not baked map art. Keep their base
   assets neutral and apply team ownership at runtime; cover neutral, claiming,
   contested, controlled, and inactive states. They must remain legible under
   bots, projectiles, and fog without resembling solid cover or changing
   collision.
8. When a larger reviewed bot visually intersects cover, measure the narrowest
   authoritative route and its complete runtime transform before changing
   scale. Prefer a consistent presentation-only wall setback, rounded lower
   plinth, or inward bevel that remains inside each blocked footprint. Size
   that static setback from the effective live mesh envelope over every legal
   heading, not the GLB's unscaled long axis alone.
   Separately measure recoil, idle sway, banking, and rotation: suppress or
   clamp actor-owned cosmetic motion beside walls instead of shrinking wall
   art enough to absorb the entire animation envelope. Do not widen the map,
   move tile centers, or rescale zones, effects, and camera merely to hide a
   visual overlap.
9. Use instanced modular meshes, shared atlases, and level-of-detail only where
   the gameplay camera demonstrates a win. Record first-load and steady-state
   GPU/transfer budgets separately from bot looks, which load independently.
10. Compare generated modules with a deterministic Blender/procedural kit before
   choosing a route. Never submit a whole map to a multiview service; one task
   depicts one reusable module, and paid calls wait for explicit owner approval
   of their concept inputs.
11. Review Frontline with both class teams, projectiles, fog, health, objective
   pressure, camera motion, every wall topology, and the Canvas fallback. A
   more organic wall outline is successful only if grid occupancy and cover
   remain faster to read, not merely less square.
12. Start the runtime foundation from `WallLayout`, not a provider mesh: trace a
   continuous solid per wall family, let the theme manifest own reviewed
   height/radius profiles, and crop topology caps only on edges facing open
   floor. Keep connector source outlines and planar tops at the authoritative
   grid boundary, including between different families; an intentionally
   outward bevel may still overlap that plane by its measured reach. Add
   instanced ribs/panels only after this substrate passes.
13. Derive open-floor relief from the largest live model's actual transformed
   GLB vertices. Compose the node hierarchy and runtime look scale, measure the
   maximum XZ radius for arbitrary yaw/diagonal facing, then use
   `max(0, radius + safety - 0.5)`. If an extrusion bevel grows outward, add
   its reach to the source-outline inset and assert the final generated bounds.
   Keep recoil, sway, and other translations actor-owned; do not hollow every
   wall to absorb animation extremes.
14. Keep topology caps on the extrusion's supported planar top. On an open
   side their half-extent is `0.5 - visualInset - outwardBevel`, while a
   same-family side may retain the atlas gutter overlap. Preserve the authored
   rim by mapping open and different-family edges to the atlas cell's content
   edge `gutter / (content + 2*gutter)`, not by proportionally cropping farther
   into the cell; same-family joins retain UV 0/1. Assert built cap bounds
   against top-face bounds and assert the UV endpoints. A square cap corner
   over a rounded outline remains a later shaped/nine-slice-cap problem, not
   permission to leave an unsupported square lip.
15. Set a provider call/credit cap and ledger before experiments. Stop paid
   generation when the deterministic topology proof is the stronger route,
   retain the provider result as an art-side donor/benchmark, and never spend
   the remainder merely because it was budgeted.
16. Treat the approved whole-arena concept as direction, never as a runtime
   crop, UV source, or tile-layout authority. Audit its transferable properties
   explicitly—value hierarchy, material finish, wall profile, recesses,
   service detail, lighting temperature—and record which remain absent from
   the deterministic runtime. Do not call a concept match exact while source
   materials or profiles are still missing.
17. Express reusable 3D environment treatment as declarative theme data:
   profile height/inset/chamfer, deterministic sparse detail cadence, material
   maps and scalars, and lighting/floor response. Geometry placement may hash
   only stable wall-family/tile/topology inputs; no random source, replay ID,
   or current map layout may become art authority. Keep detail inside the
   narrowest reviewed profile and collision-clearance envelope.
18. Generation-3 replays require explicit presentation metadata for every map
   theme/wall family and authored form look/projectile look. Route that
   descriptor separately from `ActorMapDefinition` and the gameplay contract:
   presentation changes the canonical replay hash, never rules, map, or match
   fingerprints. Hosted execution and every Frontline CLI/sandbox writer must
   supply the same descriptor; reject a native Frontline proof whose header
   presentation is null or silently falls back to Control Room.
19. Keep true normal/roughness and other environment-only material maps inside
   the lazy WebGL renderer tree. The canonical floor/theme sources remain the
   Canvas2D, site, mobile, loading, and CLI fallback; verify the map bytes do
   not enter single-file CLI artifacts. When an albedo is reused as bump,
   review a deliberately shallow scale—baked lighting, rust, and wear are not
   physical height.
20. Approve with a fresh hash-verified native replay whose own header carries
   presentation. Pin replay, tick, viewport, camera, and renderer for A/B;
   include bots/projectiles, whole arena, minimum span, and Canvas fallback.
   Injected, hash-invalid, toy-layout, or fallback-theme boards cannot approve.

## Validation

Run:

```sh
(cd web && npm test && npm run build)
sandbox/theme-art-venv/bin/python -m py_compile scripts/build-theme-art.py
sandbox/theme-art-venv/bin/python scripts/build-theme-art.py \
  art/themes/<theme>/art.json --check
git diff --check
```

Compare the final `web/dist/index.html` size with the pre-change baseline.
For 3D companions, also validate every GLB header, coordinate bounds, semantic
material role, build-report hash, and budget; run the real replay viewer at its
gameplay camera rather than accepting only DCC renders. Compare every
`dist-cli/<theme>/index.html` size with the pre-change baseline to prove the
renderer-only assets stayed out. Treat a normal-mapped primitive without
tangents as a portability decision: generate tangents for portable assets or
record and test the target renderer's derivative tangent-space support rather
than ignoring the validator warning.
When contracts change, run `bash scripts/test.sh`; record choices in the
owning doc, never a numbered DECISIONS entry on a branch.
