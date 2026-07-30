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

Treat 3D as a depth interpretation of an approved 2D identity, not another
concept pass.

1. Before modeling, add an art-side fidelity brief naming the canonical 2D
   asset, intended gameplay read, required silhouette and negative-space
   anchors, rule-bearing hardware, material/color regions, team-accent
   regions, and cues the look must not promise. Distinguish fantasy from
   mechanics explicitly: for example, a low-hover ground skimmer may visibly
   levitate through a small air gap, underbody glow, tight shadow, and gentle
   bob without using the altitude, banking, rotors, or vertical traversal that
   promise player-controlled flight.
2. Approve the canonical East-facing 2D look first. Derive a top, side, front,
   and gameplay-oblique model sheet from it. The top view must include a
   same-scale silhouette overlay; invented side and underside structure may
   explain depth but must not redesign the approved planform.
3. Build an untextured blockout and stop. Compare it with the 2D silhouette at
   the renderer's top/game camera, including footprint, center of mass, major
   proportions, negative spaces, and weapon/form hardware. Return a failed
   anchor to blockout; textures cannot repair silhouette drift.
4. Add real depth only after the blockout passes. Model armor thickness,
   recesses, joints, contact or hover hardware, vents, and undersides in ways
   consistent with the brief. Do not add mobility or combat affordances that
   the rules do not provide.
5. Map the approved 2D material regions onto the model before adding wear or
   micro-detail. Preserve the dominant light/dark/value grouping, authored
   accent placement, and class-readable color ratios. Keep the semantic team
   material separate and texture-free unless its mask is explicitly authored.
6. Review a fixed contact sheet containing the canonical 2D image, top-view
   overlay, untextured blockout, textured gameplay camera, and at least one
   non-team-color turntable angle. Also review the model in a real replay at
   normal zoom, with both team colors and a tight ground shadow.
7. Require every named anchor and forbidden cue to pass independently. Record
   deliberate deviations in the fidelity brief with the approval reason; do
   not average a missing identity anchor into a general quality score.

### Multiview-AI base-mesh vertical slice

Treat multiview generation as a measured base-mesh route, not as a one-click
production modeling method. The Striker pilot proved that a textured generated
mesh can reach the real renderer with genuine depth; it also proved that source
quality, semantic materials, deterministic normalization, gameplay-camera
review, and optimization remain separate required stages.

1. Give the service the approved canonical SVG, oblique target, and pinned
   owner-approved multiview sheet. Preserve the exact inputs, generation
   prompts, service/model version, settings, output files, and usage/license
   terms under `art/`. Generated views are not approved merely because they
   agree with one another.
2. Call the result a generated base mesh. It is not an approved look, a
   specialist-authored final, or a runtime asset, even when its preview is
   attractive.
3. Inspect and record which source axis the generated nose actually follows;
   provider tasks may reverse facing even when their inputs and settings are
   identical. Normalize provider output deterministically before review:
   derive the transform from measured bounds and the inspected source-facing
   axis, convert to `+X` facing and `+Y` up, center the planform, put the floor
   at `Y=0`, and normally scale the maximum planform span to one gameplay tile
   before the look's runtime scale. Record any deliberately larger authored
   span explicitly. Always multiply the authored span by every renderer/look
   scale before clearance review; a `1.12`-tile GLB followed by a `1.062`
   renderer scale occupies `1.18944` tiles live. Do not infer facing from task
   order, confuse authored and live span, test only one heading when diagonal
   facing is legal, or repair one result with an undocumented DCC transform.
4. Produce at least a lean and richer derivative from the same normalized
   master. Record bytes, triangles, vertices, material count, map dimensions,
   validation warnings, and first-use transfer. The Striker proof found a
   roughly 2.5 MB / 75k-triangle / 1K-map lean tier visually comparable to a
   roughly 6.3 MB / 150k-triangle / 2K-map rich tier at the 58-degree gameplay
   camera; treat those as evidence from one look, not universal budgets.
5. Put an unapproved derivative through the manifest-driven lazy loader only
   in a review worktree. Exercise movement, every heading, hover/contact,
   shadow, damage, multiple instances, fog, and fallback in a real replay.
   Authored directional GLBs suppress the generic fallback-only facing wedge;
   SVG-derived models keep it.
6. Before real-replay review, render a deterministic strict top, strict side,
   strict front, strict rear, both three-quarter views, and the gameplay
   oblique. Compare them beside the approved inputs. Repeat the canonical
   same-scale top-silhouette gate on the generated mesh and report coverage,
   centerline, and local width drift; also reject a side/depth profile that
   changes the approved body class even when the top passes. The clean Striker
   v2 trial caught a reversed result only through the facing check and found
   `95.67%` top IoU after the rigid correction.
7. Prompting may confine a team color to clean, bounded inlays, but it does not
   create semantic runtime ownership. For a clean fused result, prefer a
   deterministic offline texture mask and face split over a runtime hue-key
   shader: derive the mask from the authored base/emissive cyan, prune isolated
   noise, assign majority-covered faces to a second primitive, keep its normal
   and metallic/roughness maps, remove its base/emissive textures, use neutral
   white factors, and mark its material
   `extras.nilbotsRole = "team-accent"`. Neutralize the source cyan in the hull
   maps, retain the hull PBR maps, add a structural GLB test for exactly one
   semantic accent material/primitive, and pass both-team review.
8. Hand the base mesh to a modeler/technical artist wherever the measured gates
   still fail: silhouette and proportion correction, topology/retopology,
   separate functional parts, clean UVs, PBR repair, semantic team-material
   isolation, artifact removal, or an editable production source. Do not
   prescribe human rework where a deterministic, reviewable transform already
   passes.
9. Run the normal top-overlay, forbidden-cue, gameplay-camera, team-color,
   animation/form, performance, and fallback gates on the corrected result.
   Generated multiview agreement is not evidence that those gates pass.
10. Complete one Striker vertical slice end to end before using the route for
   another class or projectile. Record provider calls and credits, where
   deterministic or human rework was required, and compare quality, time,
   triangles, materials, textures, and first-use bytes with the fallback.

#### Meshy 6 pilot recipe

The measured provider trial uses Meshy 6 Multi-Image to 3D. This pins a
reproducible experiment, not a provider endorsement or an approved production
method.

1. Keep the API credential out of the repository, shell profiles, command
   history, screenshots, logs, and generated provenance. On the Nilbots macOS
   workstation it lives in Login Keychain under service
   `nilbots.meshy.api` and the current login account. Read it inside the
   calling process with `/usr/bin/security`; never print the returned value.
   Other environments inject `MESHY_API_KEY` from their secret manager rather
   than adding an `.env` file.
2. Check `GET https://api.meshy.ai/openapi/v1/balance` before a paid request
   and record only the numeric balance. Re-check Meshy's official pricing and
   parameter documentation when the experiment is run; provider contracts can
   change independently of this repository.
3. Upload one object per task as four separate, owner-approved images. Do not
   upload a contact sheet as one image, and never mix a bot, projectile,
   detached weapon, or scenery in the same multi-image task. Prefer square
   lossless inputs at least 1040 pixels on the shortest side, with consistent
   framing and object scale, a flat neutral or transparent background, diffuse
   lighting, no cast shadow, and no clipped geometry. For Striker use a strict
   top, strict side, front three-quarter, and rear three-quarter view. The top
   view is derived from the approved 3D concept and must pass a same-scale
   overlay against the canonical 2D planform before upload.
4. Keep geometry requirements visible and mutually consistent in the source
   images. Meshy's `texture_prompt` describes surface appearance; prohibitions
   such as “no landing gear” or “thin hull” in that field are not a documented
   geometry control.
5. The recoverable-master proof uses `ai_model: "meshy-6"`,
   `should_texture: true`, `enable_pbr: true`,
   `texture_resolution: "4k"`, `should_remesh: false`,
   `remove_lighting: true`,
   `pose_mode: ""`, and `target_formats: ["glb"]`. Do not enable Smart
   Topology, Auto Split, pose control, remesh, or 8K for this proof. The first
   goal is maximum recoverable form/material evidence, including emission;
   optimization happens after approval.
6. Set `image_enhancement: true` for noisy, low-resolution, photographed,
   shadowed, or otherwise unreconciled inputs, matching Meshy's general
   guidance. Default it to `false` for already-clean, approved, exact-detail
   multiview inputs; use a recorded same-input A/B when uncertain. The clean
   Striker v2 A/B was decisive: enhancement ON collapsed the planform to
   `62.38%` canonical IoU while OFF preserved it at `95.67%`. Do not transfer
   that result to noisy sources, and do not mix source changes into the A/B.
7. Keep the texture prompt descriptive and prohibitive, not redesigning:
   preserve the approved graphite, weathered bronze, panel seams, vents,
   sharply bounded cyan team inlays, and amber core/engine emission; add no
   text, logos, labels, decals, or baked lighting. Validate geometry exclusions
   from the views rather than relying on this prompt.
8. Record the task ID, image filenames and SHA-256 hashes, sanitized request
   settings, numeric balance before/after, `consumed_credits`, sanitized task
   JSON, provider/model version, license terms, and downloaded artifacts under
   `art/class-models/provider-runs/meshy/<look-id>/<task-id>/`. Strip signed
   URL queries and input data URIs from the recorded response. Never record the
   Authorization header.
9. Download every output immediately. Meshy's non-Enterprise API retention is
   short and must not be treated as storage. Keep raw provider output
   art-side; only a corrected, reviewed derivative may enter a runtime look.
10. A default projectile is a second, separately charged task after its bot
   proof passes. Its input shows only the projectile head; Nilbots continues
   to own trail, glow pool, travel, impact, hitbox, and team tint.

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

1. Start with genuine SVG. Treat PNG as an exception that requires
   gameplay-scale evidence that intentional painterly, organic, or
   texture-heavy art is materially worse in vector. Never auto-trace a raster
   merely to claim vector.
2. Use the canonical transparent 512×512 canvas or SVG viewBox, facing East.
   A genuine SVG must not contain `<image>` or `data:image` raster payloads.
3. Keep high-resolution raster masters and exact generation prompts outside
   the runtime package. Remove backgrounds before deriving the 512×512 PNG.
   When replacing a shipped raster with SVG, retain the old image under
   `art/bot-looks/<id>/raster-reference.png`, not beside runtime assets.
4. Add `web/src/assets/bot-looks/<id>/look.json` plus `sprite.png` or
   `sprite.svg`. Discovery is manifest-driven; do not add a TypeScript registry.
   A look may name an owned `defaultProjectile` companion; selecting that
   chassis recommends the projectile but never prevents the owner from
   choosing another.
5. Inspect the look at gameplay size facing all four directions and during
   movement, recoil, damage, destruction, fogging, and panel rendering.
6. After the 2D look and fidelity brief are approved, a WebGL companion may add
   genuine modeled depth. Keep `sprite.svg` or `sprite.png` canonical for the
   site, Canvas2D, mobile, CLI, loading, and fallback; the model supplements
   that identity and never replaces it.
7. Build the companion as authored geometry, not a shallow extrusion of the
   sprite. Preserve the 2D silhouette and rule-bearing hardware, then model the
   hull, armor layers, recesses, joints, vents, and weapon volumes that explain
   it from the gameplay camera. Check in an editable `.blend` source and a
   deterministic generator or export recipe under `art/`.
8. Put `model.glb` and `model3d.json` beside the runtime look only after the
   model passes the gameplay review. The GLB contract is `+X` facing, `+Y` up,
   floor at `Y=0`, tile-relative scale, no camera, no light, and no animation
   unless a later renderer contract explicitly owns it. Discovery stays
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
When engine, replay, map, or submission contracts changed, also run
`bash scripts/test.sh`. Record new durable presentation choices in
`docs/DECISIONS.md`.
