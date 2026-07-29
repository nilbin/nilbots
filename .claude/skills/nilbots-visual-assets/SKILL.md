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

Treat multiview generation as an unproven handoff route, not as a production
modeling method. Pilot it on Striker only before changing the wider workflow.

1. Give the service the approved canonical SVG, oblique target, and pinned
   multiview sheet. Preserve the exact inputs, service/model version, settings,
   output files, and usage/license terms under `art/`.
2. Call the result a generated base mesh. It is not an approved look, a
   specialist-authored final, or a runtime asset, even when its preview is
   attractive.
3. Hand the base mesh to a human modeler/technical artist for silhouette and
   proportion correction, topology/retopology, separate functional parts,
   clean UVs, PBR authoring, semantic team-material isolation, scale,
   orientation, pivots, floor/hover placement, and artifact removal. The human
   handoff must remain editable.
4. Run the normal top-overlay, forbidden-cue, gameplay-camera, team-color,
   animation/form, performance, and fallback gates on the corrected result.
   Generated multiview agreement is not evidence that those gates pass.
5. Complete one Striker vertical slice end to end before using the route for
   another class or projectile. Record where human rework was required and
   compare its quality, time, triangles, materials, textures, and first-use
   bytes with the current fallback. Only then decide whether to pin the route
   as a repeatable skill.

#### Meshy 6 pilot recipe

The first provider trial is Meshy 6 Multi-Image to 3D. This pins a reproducible
experiment, not a provider endorsement or an approved production method.

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
3. Upload one object per task as separate images. Do not upload a contact sheet
   as one image, and never mix a bot, projectile, detached weapon, or scenery
   in the same multi-image task. For Striker use the four lossless images under
   `art/class-models/concept-targets/meshy-striker-v1/`.
4. The first proof uses `ai_model: "meshy-6"`,
   `should_texture: true`, `enable_pbr: true`,
   `texture_resolution: "4k"`, `should_remesh: false`,
   `image_enhancement: false`, `remove_lighting: true`,
   `pose_mode: ""`, and `target_formats: ["glb"]`. Do not enable Smart
   Topology, Auto Split, pose control, remesh, or 8K for this proof. The first
   goal is maximum recoverable form/material evidence, including emission;
   optimization happens after approval.
5. Keep the texture prompt descriptive and prohibitive, not redesigning:
   preserve the approved graphite, weathered bronze, panel seams, vents,
   cyan team inlays, and amber core/engine emission; add no weapons, landing
   gear, text, logos, supports, scenery, or ornament.
6. Record the task ID, image filenames and SHA-256 hashes, sanitized request
   settings, numeric balance before/after, `consumed_credits`, full task JSON,
   provider/model version, license terms, and downloaded artifacts under
   `art/class-models/provider-runs/meshy/<look-id>/<task-id>/`. Never record
   the Authorization header or a data-URI copy of the input images.
7. Download every output immediately. Meshy's non-Enterprise API retention is
   short and must not be treated as storage. Keep raw provider output
   art-side; only a corrected, reviewed derivative may enter a runtime look.
8. A default projectile is a second, separately charged task after its bot
   proof passes. Its input shows only the projectile head; Nilbots continues
   to own trail, glow pool, travel, impact, hitbox, and team tint.

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

1. Derive modular floor, perimeter, interior-cover, corner, junction, and prop
   pieces from the approved theme materials. Give walls real profiles,
   chamfers, recesses, and restrained non-square silhouettes while keeping the
   authoritative occupied tiles unmistakable.
2. Geometry must never imply a changed collision, opening, sight line, zone,
   spawn pad, or traversal route. Decorative overhangs stay inside a tile's
   readable footprint; solid-looking machinery remains on blocked tiles.
3. Keep gameplay topology in the map contract and presentation in the
   companion package. The renderer resolves pieces from explicit wall
   families/tags; it must not infer rules from mesh names or pixels.
4. Use instanced modular meshes, shared atlases, and level-of-detail only where
   the gameplay camera demonstrates a win. Record first-load and steady-state
   GPU/transfer budgets separately from bot looks, which load independently.
5. Review Frontline with both class teams, projectiles, fog, health, objective
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
renderer-only assets stayed out.
When engine, replay, map, or submission contracts changed, also run
`bash scripts/test.sh`. Record new durable presentation choices in
`docs/DECISIONS.md`.
