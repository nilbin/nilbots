---
name: nilbots-visual-assets
description: Create, revise, bake, and validate nilbots arena themes, bot looks, projectile looks, and their 3D companions. Use when work involves visual concepts, concept-to-model fidelity, generated material sources, floor or wall textures, topology atlases, theme manifests, map presentation families, bot chassis or projectile PNG/SVG/GLB assets, PBR textures, visual bundle size, or gameplay-scale art review.
---

# Nilbots Visual Assets

Preserve ASCII gameplay semantics while improving presentation. Read `docs/ARENA-VISUALS.md`
completely before changing assets, then inspect the owning map JSON, theme manifest, or look manifest.

## Arena theme workflow

1. Generate distinct opaque material fields, never a whole map, isolated wall, or atlas. Keep the exact
   accepted prompt in `art/themes/SOURCE-PROMPTS.md`. Keep an accepted floor at
   `art/themes/<theme>/floor/source.png`; declare its optimized runtime filename, size, and WebP quality
   in `art.json`.
2. Put accepted wall sources at `art/themes/<theme>/walls/<family>/source.png`; keep perimeter and
   interior cover as separate semantic families.
3. Update `art/themes/<theme>/art.json` and `web/src/assets/themes/<theme>/theme.json`, with matching
   atlas dimensions. A staged theme instead sets `runtime.packagePath` to
   `art/themes/<theme>/runtime`; keep that complete package outside Vite discovery until a map ships it.
   Never raise `assetBudgetBytes` merely to pass a build.
4. Rebuild in the disposable environment:

   ```sh
   python3 -m venv sandbox/theme-art-venv
   sandbox/theme-art-venv/bin/pip install -r scripts/requirements-theme-art.txt
   sandbox/theme-art-venv/bin/python scripts/build-theme-art.py art/themes/<theme>/art.json
   ```

5. Assign shipping themes and wall families in map presentation data. Never infer a theme from map ID or
   add a viewer skin switch. Keep map packages within the engine's 32×32 envelope.
6. Review small and large maps at DPR 1 and 2: outer perimeter, isolated cover, corners, junctions, zone
   contrast, bots, health, projectiles, and fog.

The build owns floor resizing/encoding, seamless wall normalization, PBR helpers, 256 topology variants,
and the theme-wide runtime-size check. Never hand-edit its outputs.

## Concept-to-model fidelity gates

Treat 3D as a depth interpretation of an approved 2D identity, not another concept pass.

1. Before modeling, add an art-side fidelity brief naming the canonical 2D asset, intended gameplay
   read, silhouette/negative-space anchors, rule-bearing hardware, material/color and team-accent
   regions, and forbidden cues. Separate fantasy from mechanics: a low-hover ground skimmer may use a
   small air gap, underbody glow, tight shadow, and gentle bob, but not altitude, banking, rotors, or
   vertical traversal that promise player-controlled flight.
2. Approve the canonical East-facing 2D look first; derive top, side, front, and gameplay-oblique views.
   Require a same-scale top silhouette overlay. Side/underside inference may explain depth, never redesign
   the planform. For camera-consistent image-to-3D inputs, use separately approved inferred-3D top/rear
   renders rather than treating the flat sprite as a photo. Keep the sprite as identity/overlay authority;
   an unapproved inferred view is not a new concept.
3. Build an untextured blockout and stop. At renderer top/game cameras compare footprint, center of mass,
   proportions, negative spaces, and weapon/form hardware. Return any failed anchor to blockout; texture
   cannot repair silhouette drift.
4. Add armor thickness, recesses, joints, contact/hover hardware, vents, and undersides only after blockout
   passes. Never imply unavailable mobility/combat. If depth adds load-bearing feet, legs, or another
   locomotion cue, concept approval must pin its rig and movement animation; never slide a walking
   silhouette. Aegis Tortoise specifically uses heavy short four-leg steps, a level carapace, four
   planting/compressing anchors, and a body that lowers into Shell/Turret stance; do not generalize that gait.
5. Map approved 2D material regions before wear or micro-detail. Preserve dominant value grouping, authored
   accent placement, and class-readable color ratios. Keep semantic team material separate and texture-free
   unless its mask is explicitly authored.
6. Review one fixed sheet: canonical 2D, top overlay, blockout, textured gameplay camera, and a non-team-color
   turntable angle. Also use a real replay at normal zoom, both teams, and a tight ground shadow. Pin
   replay/time/viewport/camera; label comparisons approximate if any differ. Hard-gate forms/skins beside
   their parent mobile in the same 58° rig and one-tile frame: match value, PBR/material, detail, edge wear,
   and fixed emission, varying only the semantic team mask. Reject a good isolated render that reads as a
   different chassis.
7. Pass every anchor and forbidden cue independently. Record approved deviations and reasons in the brief;
   never average a missing identity anchor into a general score.

### Multiview-AI base-mesh vertical slice

Treat multiview generation as a measured base-mesh route, never one-click production. The Striker pilot
proved genuine depth can reach the renderer, while source quality, semantic materials, normalization,
game-camera review, and optimization remain separate stages.

1. Supply the approved canonical SVG, oblique target, and pinned owner-approved multiview sheet. Preserve
   exact inputs, prompts, service/model version, settings, outputs, and usage/license terms under `art/`.
   Camera-consistent generated views do not approve one another.
2. Label output a generated base mesh, never an approved look, specialist final, or runtime asset.
3. Measure and record the actual source-facing nose axis; identical tasks may reverse it. Derive a
   deterministic transform from that axis and measured bounds: `+X` facing, `+Y` up, centered planform,
   floor at `Y=0`, normally one-tile maximum planform before runtime look scale. Record deliberate larger
   spans. Multiply authored span by every runtime scale for clearance (`1.12 × 1.062 = 1.18944` tiles);
   test all legal/diagonal headings. Never infer axis from task order, confuse authored/live span, or use an
   undocumented DCC transform. Allow a named bounded local vertex correction only for an isolated audited
   defect, with pinned selection, maximum displacement, unchanged regions, and a rerun overlay; never hide
   whole-body drift with global nonuniform scale or undocumented sculpting.
4. Derive lean and rich tiers from the same normalized master; record bytes, triangles, vertices, materials,
   map sizes, warnings, and first-use transfer. Striker found ~2.5 MB/75k/1K comparable to
   ~6.3 MB/150k/2K at 58°; Fabricator found 6.60 MB/75k/1K indistinguishable from
   17.19 MB/150k/2K at normal follow and eight-tile minimum zoom. These are per-look evidence, not budgets;
   ship rich only for a visible gain in a fixed real-camera A/B.
5. Load an unapproved derivative only in a review worktree via the manifest lazy path. In a real replay test
   movement, every heading, hover/contact, shadow, damage, multiple instances, fog, and fallback. Directional
   GLBs suppress the generic fallback facing wedge; SVG models retain it. Run this end-to-end before buying
   resemblance-only iterations; choose regeneration versus deterministic/DCC correction from that evidence.
6. Before replay review, render deterministic strict top/side/front/rear, both three-quarters, and gameplay
   oblique beside approved inputs. Repeat the same-scale top gate and report coverage, centerline, and local
   width drift; reject a wrong side/depth body class even if top passes. Striker v2 exposed reversed facing
   and reached `95.67%` top IoU only after rigid correction.
7. Prompt-bounded team inlays are not semantic ownership. For a clean fused result use the deterministic
   offline mask/face-split procedure below, not a runtime hue key; require exactly one semantic accent
   primitive/material and both-team review while preserving hull PBR maps.
8. Hand measured failures to a modeler/technical artist for silhouette/proportion, retopology, functional
   parts, UVs, PBR, semantic-material isolation, artifact removal, or editable source. Do not prescribe
   human work where a deterministic reviewable transform passes.
9. Re-run top-overlay, forbidden-cue, gameplay-camera, team-color, animation/form, performance, and fallback
   gates. Multiview agreement alone proves none of them.
10. Complete one chassis vertical slice before another class/projectile; ledger calls/credits and compare
    quality, time, triangles, materials, textures, bytes, and deterministic/human rework with fallback.

#### Meshy 6 pilot recipe

The measured Meshy 6 Multi-Image-to-3D trial pins a reproducible experiment, not an endorsement or
approved production method.

1. Keep the API credential out of the repository, shell profiles, history, screenshots, logs, and
   provenance. On the Nilbots macOS workstation read Login Keychain service `nilbots.meshy.api` for the
   current account inside the process with `/usr/bin/security`; never print it. Elsewhere inject
   `MESHY_API_KEY` from a secret manager, never `.env`.
2. Before paid work, call `GET https://api.meshy.ai/openapi/v1/balance`, recording only numeric balance.
   Re-check official pricing/parameters at execution time because provider contracts change.
3. Upload one object per task as four separate owner-approved images—never a contact sheet or mixed
   bot/projectile/detached weapon/scenery. Prefer square lossless inputs, shortest side ≥1040 px, consistent
   scale/framing, neutral or transparent background, diffuse light, no cast shadow or clipping. Striker uses
   strict top/side and front/rear three-quarters; its approved-3D top must first overlay the canonical 2D.
4. Put mutually consistent geometry requirements in the views. `texture_prompt` controls appearance;
   “no landing gear” or “thin hull” there is not documented geometry control.
5. For the recoverable master use `ai_model:"meshy-6"`, `should_texture:true`, `enable_pbr:true`,
   `texture_resolution:"4k"`, `should_remesh:false`, `remove_lighting:true`, `pose_mode:""`, and
   `target_formats:["glb"]`. Disable Smart Topology, Auto Split, pose control, remesh, and 8K. First recover
   maximum form/material/emission evidence; optimize only after approval.
6. Use `image_enhancement:true` for noisy, low-resolution, photographed, shadowed, or unreconciled inputs;
   default false for clean approved exact-detail views. When unsure, record a same-input A/B without source
   changes. Striker v2 clean input measured ON at `62.38%` canonical IoU versus OFF at `95.67%`; do not
   generalize that result to noisy sources.
7. Keep the texture prompt descriptive/prohibitive, not redesigning: preserve graphite, weathered bronze,
   panel seams, vents, bounded cyan team inlays, and amber core/engine emission; forbid text, logos, labels,
   decals, and baked lighting. Validate geometry exclusions in the views.
8. Under `art/class-models/provider-runs/meshy/<look-id>/<task-id>/`, record task ID, input names/hashes,
   sanitized settings/task JSON, numeric balance before/after, `consumed_credits`, provider/model version,
   license, and downloads. Strip signed URL queries and input data URIs; never record Authorization.
9. Download all output immediately; non-Enterprise retention is short, not storage. Keep raw output art-side;
   only a corrected reviewed derivative may enter runtime.
10. Charge a default projectile as a separate task after its bot proof. Show only its head; Nilbots owns
    trail, glow, travel, impact, hitbox, and team tint.

After download, record counts for meshes/primitives/materials, triangles/vertices, axis/facing, bounds,
skins, animations, morphs, tangents, validation, and topology before part animation. Weld exact coincident
positions and recompute connectivity before calling indexed islands parts; UV/normal seams are not parts.
A welded manifold needs an authored cut/rebuild and named pivots, never arbitrary seam parenting. Normalize
every asset from measurement because even one batch may vary axes. Detect depth-stacked duplicate bodies by
strict side/gameplay views plus exact-position-welded component bounds, triangles, watertightness, and
planform coverage. Delete a layer only when independent components and a complete retained body are proved;
never average, flatten, bridge, or globally scale stacked copies. Re-run depth review and rebuild a deficient
retained underside.

Use Blender after useful identity/material evidence, not to excuse provider topology. Retopologize or rebuild
rule-bearing parts, isolate semantic materials, add named rigid parts or a skin, author only contract-owned
deterministic clips, rebake, add tangents, optimize, and rerun fixed-view/gameplay gates. Preserve the provider
master; never animate an inseparable high-poly manifold because its top view looks complete.

#### Static whole-form companions

Treat Shell and Turret as complete static same-family transformations of the approved mobile, not unrelated
designs or animation rigs. Reuse value hierarchy, PBR finish, wear, fixed emission, bounded team regions, and
recognizable parts; enforce the pinned family gate above.

- Export each as one fused `part:"whole"` GLB with no form clips. Shell declares top-level
  `formRole:"directional-stance"` plus `skillHardware:"aegis"`; Turret declares
  `formRole:"omnidirectional-emplacement"` and no directional hardware. Never reinterpret a mobile whole
  GLB across roles.
- Mount each whole form once. A whole Turret contains the complete radial form and keeps supports planted;
  only a declared `turret-arm` or SVG front-sector fallback repeats four times.
- Leave deploy/mobilize scale, fade, energy assembly, anchor dial, directional guard field, and scan to
  authoritative renderer state. Shell may replace the crude physical plate, never the exact floor guard
  extent; Turret geometry never authors the scan. Require missing/invalid/cross-role GLBs to fall back.
- Keep central and other fixed cyan/PBR surfaces authored and untagged. Only an intentionally bounded
  primitive/material carrying `extras.nilbotsRole="team-accent"` receives owner tint.

#### Fused team-accent extraction

Use this only for a reviewed fused material with clean bounded team paint; it is offline authoring, never a
runtime shader.

1. Preserve source model and lossless provider base-color/emission maps on one exact grid; never classify a
   resized JPEG/screenshot. Combine color and emission, require a stronger two-channel seed in every kept
   component, grow only a few deterministic pixels inside the loose base-color candidate, close one-pixel
   holes, and prune small unseeded components. Hue alone admits reflections/weathering.
2. Classify faces from several strictly interior UV samples, not vertices/edge midpoints. The validated
   Striker method uses seven Dunavant samples and a five-of-seven vote. Record thresholds, selected faces
   and area, boundary histogram, purity/coverage estimates, and exact mask/model hashes. Its cyan thresholds
   are Striker evidence, not a universal palette.
3. Split the original index stream into hull/accent primitives while sharing unchanged position, normal, UV,
   and authored tangent accessors. Preserve triangle multiset, scene graph, transforms, facing, and geometry
   buffer hashes exactly.
4. Remove classified color/emission pixels from lossless hull maps. Give accent neutral white base/emission
   factors, no fixed base/emission texture, original normal and metallic/roughness maps, and
   `extras.nilbotsRole="team-accent"` on primitive and material.
5. Require deterministic rerun hash, structural and Khronos validation, close top/gameplay renders in two
   teams, and real-replay proof. Renderer owns tint/emission/floor glow; bake neither bloom nor one team.
   For Blender zero tangents at collapsed UV derivatives or sub-thousandth export seam drift, repair only the
   exported tangent accessor with a documented stable normal-derived perpendicular and, if needed, measured
   direction grid plus renormalization. Prove geometry, UV, topology, textures, and transforms unchanged;
   compare independent GLB hashes and visuals. `.blend` is editable provenance, not byte-determinism target.

## Bot-look workflow

1. Start with genuine SVG. Allow PNG only when gameplay evidence shows intentional painterly, organic, or
   texture-heavy work is materially worse in vector; never auto-trace to claim vector.
2. Use transparent 512×512 canvas/viewBox facing East. Genuine SVG contains no `<image>` or `data:image`.
3. Keep high-resolution masters and exact prompts outside runtime; remove backgrounds before a 512×512 PNG.
   When replacing shipped raster with SVG, retain it at `art/bot-looks/<id>/raster-reference.png`, not runtime.
4. Add `web/src/assets/bot-looks/<id>/look.json` plus sprite; discovery is manifest-driven, never a TypeScript
   registry. `defaultProjectile` recommends an owned companion without preventing another selection.
5. Inspect gameplay size in four facings, movement, recoil, damage, destruction, fog, and panels.
6. Add WebGL depth only after 2D and the fidelity brief pass. Keep the sprite canonical for site, Canvas2D,
   mobile, CLI, loading, and fallback; 3D supplements identity. Model authored hull, armor, recesses, joints,
   vents, and weapon volumes—not shallow extrusion—and keep editable `.blend` plus deterministic recipe in
   `art/`.
7. Add runtime `model.glb`/`model3d.json` only after gameplay review: `+X` facing, `+Y` up, floor `Y=0`,
   tile-relative scale, no camera/light, and only renderer-contracted animation. Discover only inside
   `render3d`, never shared appearance/hand-written catalogs. Put `low-hover` and other presentation motion in
   `look.json` so sprite and GLB agree; follow the static whole-form contract above for forms.
8. Use compact PBR maps when flat materials lose panel/finish/wear: prefer one small per-look atlas or measured
   shared family atlas. Prove normal and metallic/roughness detail at arena lighting/camera. Keep team paint
   separate with `extras.nilbotsRole="team-accent"` so owner tint cannot wash hull PBR.
9. Compare shipping and richer on-demand tiers; record bytes, triangles, materials, texture dimensions,
   first-use transfer, and visible game-camera gain. On-demand loading does not justify invisible detail.
   Prove CLI remains model-free and missing/failed GLB uses the 2D fallback.

Bot appearance belongs to the bot and is snapshotted into matches/replays. Never add slot-owned selection
except the legacy fallback.

## Projectile-look workflow

1. Start with genuine East-facing SVG on transparent `viewBox="0 0 256 256"`. Sprites are white alpha
   masks; opacity may layer energy, but fixed color, raster, floor, shadow, trail, and glow do not belong.
2. Center one compact head with generous padding. Keep it distinct at 16–28 gameplay px, no wider than its
   authoritative tile, and never imply changed hitbox, path, speed, range, or damage.
3. Add manifest-driven `web/src/assets/projectile-looks/<id>/look.json` plus SVG, never a TS registry. Keep
   scale in tested 0.3–0.7 tile range. Put an approved optional `flightCue`—`stable`, `axial-spin`,
   `flat-spin`, or `tumble`—in `look.json`, never `model3d.json`.
4. Renderer owns accent tint, trail, glow, contrast adaptation, fog truth, terminals, and authoritative
   traversal interpolation. Flight cues transform only the head beneath its heading rig using `+X` forward,
   `+Y` up, shared by Canvas SVG, WebGL extrusion, and GLB. Never bake common effects into a look.
5. Test cardinal/diagonal headings, launch, speed-one/two traversal, bends, every flight cue, hit,
   miss/range expiry, deflection, light/dark themes, fog, DPR 1/2, and editor preview. Derive hit/miss from
   exact projectile-event causality: exact projectile events win; preserve generic replay-v1 finish and the
   generic finish when a modern death tick has damage/deflection with null projectile ID.
6. After 2D approval, a GLB companion follows bot coordinates/source rules. Model readable head volume and
   rule-honest direction only—no trail, floor glow, range, speed, or hitbox. Mark every tintable material
   `extras.nilbotsRole="projectile-mask"`; compact maps may retain grooves, but team tint/emissive response
   remains renderer-owned.
7. If a provider master has useful material/form but failed topology, rebuild from canonical SVG rather than
   repairing identity by eye. Construct exact one-tile outer planform/apertures, then contained depth/detail.
   Compare strict orthographic top alpha using only uniform longest-span normalization and translation—never
   mirror, rotate, or nonuniformly warp. Gate IoU, coverage, precision, and aperture count; separately inspect
   side/front/rear/gameplay because top cannot expose stacked bodies or wrong depth class.
8. Author PBR color/emission as neutral grayscale value masks because WebGL multiplies all head surfaces by
   team paint. Feed the same structure to base/emission so grooves survive tint. Mark all materials
   `projectile-mask`; include tangents, compact normal/metallic-roughness maps, and structural tests for
   dimensions, bounds, triangle/byte budgets, and export hashes. Check in deterministic recipe/editable
   `.blend`; remove `.blend1` backups.
9. Treat fixed-camera and 16–28 px DCC renders as local gates. Final approval needs a fresh hash-verified
   native replay pinned to replay/tick/viewport/camera/renderer with intended map presentation explicit in
   its header. The board must show genuine head plus renderer trail/glow, flight cue, both teams, and exact
   hit/miss/deflection. Beauty renders, overlays, fallback themes, partial boards, and toy layouts cannot approve.

Projectile appearance belongs to the bot/chassis/accent and is snapshotted into matches/replays. Missing or
legacy IDs use Pulse Bolt. Entitlements authorize equip only; they never affect rendering/gameplay.

For a shippable bot/projectile add the same stable ID/label to `cosmetics/catalog.json`; keep availability and
unlock policy there, never rendering manifests, and run alignment tests. Keep unshipped concepts under `art/`
without runtime manifest/catalog entry until availability and unlock are chosen.

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
