---
name: nilbots-visual-assets
description: Create, revise, bake, and validate nilbots arena themes, bot looks, projectile looks, and their 3D companions. Use when work involves visual concepts, concept-to-model fidelity, generated material sources, floor or wall textures, topology atlases, theme manifests, map presentation families, bot chassis or projectile PNG/SVG/GLB assets, PBR textures, visual bundle size, or gameplay-scale art review.
---

# Nilbots Visual Assets

Preserve ASCII gameplay semantics while improving presentation. Read
`docs/ARENA-VISUALS.md` completely before changing assets, then inspect the owning map JSON, theme manifest, or bot-look manifest.

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

Choose the visual authority before concept work:

- **3D-first is the default for a new modeled bot, form family, skin family,
  or projectile.** Develop and approve the editable 3D model from a
  rule-and-identity brief. Once accepted, that model is the canonical visual
  source. Derive the East-facing 2D gameplay avatar, site/store renders,
  thumbnails, and semantic team mask from the same pinned scene and camera;
  do not ask an image model to redraw them.
- **2D-first remains valid for legacy identities, map/theme work, and looks
  that intentionally have no model.** When upgrading one of those looks, the
  approved 2D asset constrains the migration until a 3D candidate passes and
  the owner explicitly promotes the editable model to visual authority.

The runtime still retains a 2D representation for Canvas, mobile, CLI,
loading, store/site surfaces, and WebGL failure. “3D-first” changes the
authoring source, not that fallback contract. A texture-bearing model normally
derives a transparent raster avatar plus a separate lossless semantic
team-mask pass; keep the previous SVG authoritative until the consuming
runtime supports that mask pair.

1. Before modeling, add an art-side fidelity brief naming the current
   authority (3D-first brief or legacy 2D asset), intended gameplay read,
   required silhouette and negative-space anchors, rule-bearing hardware,
   material/color regions, team-accent regions, and cues the look must not
   promise. Distinguish fantasy from mechanics explicitly: for example, a
   low-hover ground skimmer may visibly levitate through a small air gap,
   underbody glow, tight shadow, and gentle bob without using the altitude,
   banking, rotors, or vertical traversal that promise player-controlled
   flight.
2. For a 3D-first family, approve the untextured editable model from strict
   top, side, front, rear, and gameplay-oblique views before material polish.
   For a 2D-first migration, derive those same camera-consistent views from
   the approved East-facing look; the top view must include a same-scale
   silhouette overlay. Invented side and underside structure may explain depth
   but must not redesign the constrained planform. Never pretend a flat sprite
   is a camera photograph or admit an unapproved inferred view as authority.
3. Build an untextured blockout and stop. Compare it with the 2D silhouette at
   the renderer's top/game camera, including footprint, center of mass, major
   proportions, negative spaces, and weapon/form hardware. Return a failed
   anchor to blockout; textures cannot repair silhouette drift.
4. Add real depth only after the blockout passes. Model armor thickness,
   recesses, joints, contact or hover hardware, vents, and undersides in ways
   consistent with the brief. Do not add mobility or combat affordances that
   the rules do not provide. If inferred depth introduces load-bearing feet,
   legs, or another locomotion cue, concept approval must also pin its rig and
   movement animation; never ship a walking silhouette that merely slides.
   The initial three class families use presentation-only hover rather than
   walking rigs: Striker skims higher and responds quickly, Fabricator
   levitates calmly and level, and Bulwark stays in a very low, heavy hover
   with a tight shadow and slow settle. Bulwark's corner modules are
   hover/lock pods, not feet; they telescope into planted anchors only during
   the authoritative Shell or Tower transition. Those planted forms do not
   bob or bank.
5. Map the approved 2D material regions onto the model before adding wear or
   micro-detail. Preserve the dominant light/dark/value grouping, authored
   accent placement, and class-readable color ratios. A sibling class's
   material language is not a shared default: preserve Striker's approved warm
   graphite/bronze and Fabricator's approved pale ceramic/copper, then require
   an independently approved fortress-like language for Bulwark rather than
   borrowing either family. Keep the semantic team material separate and
   texture-free unless its mask is explicitly authored. Team identity is
   restrained emitted light, not broad armor paint: neutral/class-authored
   materials should remain at least 90% of the visible body, while a few
   chunky team emitters and the renderer-owned floor glow must still survive
   the smallest reviewed gameplay frame. Hairline inlays that project below
   one pixel do not pass.
6. Review a fixed contact sheet containing the canonical 2D image, top-view
   overlay, untextured blockout, textured gameplay camera, and at least one
   non-team-color turntable angle. Also review the model in a real replay at
   normal zoom, with both team colors and a tight ground shadow. Use one pinned
   replay/time/viewport/camera for every before/after; label a comparison
   approximate when any of those differ.
   Alternate forms/skins have a hard family gate: the same pinned 58° rig and
   one-tile frame beside the parent mobile; matching value, material/PBR,
   detail, edge wear, and fixed emission; only semantic team-mask differences.
   Reject an attractive isolated render that reads as another chassis.
7. Require every named anchor and forbidden cue to pass independently. Record
   deliberate deviations in the fidelity brief with the approval reason; do
   not average a missing identity anchor into a general quality score.

### Rigid form and stance animation

Generated surface quality and mechanically valid motion are separate stages.
For a bot that changes stance or form, prefer one approved mobile base plus
small rigid form kits over unrelated whole-model replacements.

1. Design the complete family before generating a production mesh. The concept
   must show where every moving plate, pod, barrel, mast, vane, rail, hinge,
   socket, and stowed kit part exists in the mobile form. A final frame may not
   invent geometry that no preceding frame contains.
2. Build an untextured Blender kinematic blockout before texture cleanup.
   Name every rigid object and record its parent, pivot, permitted axis/range,
   stowed transform, and deployed transform. Add the hidden back faces, hinge
   barrels, rail cavities, and sockets that an image-to-3D service cannot infer
   from exterior views.
3. Use rigid object or bone transforms for hard-surface machinery. Do not use
   skin deformation, shape keys, crossfades, or vertex-by-vertex morphs to turn
   one fused shell into another object. A reverse transition either samples the
   same clip backward or has endpoints proven transform-identical to the
   forward clip.
4. For Bulwark, keep the moving set deliberately small: four hover/lock pods
   telescope into anchors; the mobile muzzle retracts; a compact set of
   traceable physical plates forms the East-facing `-45°..+45°` Shell guard;
   and a separately stowed central crown may rise and unfold exactly eight
   equal radial weapon vanes for the non-facing Tower. The Shell leaves the
   other three quadrants visibly open. The Tower has no privileged nose and
   each vane may recoil independently.
5. Choose geometry ownership from the required motion. For a mobile body with
   no authored part motion, start with Meshy Smart Topology T2 or a later
   equivalent that offers explicit face-count control and native parts. For a
   stance/form family whose plates must trace through a mechanical transition,
   make the Blender kinematic blockout the geometry authority and use
   generation primarily as a material/texture donor after the pivots, hidden
   faces, sockets, and endpoints pass. Treat every generated segment only as a
   candidate mechanical mesh. Accept it when its boundary follows the approved
   seam and it has enough closed geometry to rotate without exposing a hole.
   Run a high-detail Standard result only after a measured Smart failure that
   could benefit from a visual or texture donor; do not pay for both by
   default.
6. Meshy's programmatic Rigging and Animation APIs are not the custom
   hard-surface animator. They currently rig standard textured humanoid bipeds
   and apply a preset action-library ID to that rig; do not submit a Nilbots
   chassis expecting a custom Mobile/Shell/Tower clip. Author those clips in
   Blender.
7. Export only semantic clips whose timing can be sampled from replay truth:
   hover idle/move/settle, deploy/hold/retract for each form, and localized
   recoil where applicable. Record action names, frame ranges, endpoint
   transforms, and export hashes. No clip may free-run as gameplay state or
   finish a form change before the authoritative replay event.
8. Review the actual keyframes, not only a rendered video. Validate named
   objects, pivots, clip presence/duration, forward/reverse endpoints, no
   interpenetration at sampled frames, retained UV/material assignment, and
   the 58-degree gameplay read before adding polish.

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
   When rigid animation or part editing is required, start with one controlled
   same-input Smart Topology T2 candidate at an explicit face target. T2
   deliberately emits triangles and ignores remesh/topology flags; do not ask
   it for quads. Add DCC retopology or a separate measured fallback only if the
   approved motion actually needs it. Cleaner topology or native segmentation
   does not overrule silhouette, depth, material, UV, or seam-boundary
   failures.
5. Put an unapproved derivative through the manifest-driven lazy loader only
   in a review worktree. Exercise movement, every heading, hover/contact,
   shadow, damage, multiple instances, fog, and fallback in a real replay.
   Authored directional GLBs suppress the generic fallback-only facing wedge;
   SVG-derived models keep it. Do this end-to-end renderer spike before paying
   for resemblance-only provider iterations; use the evidence to decide
   whether the next work belongs in another generation or deterministic/DCC
   correction.
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

#### Meshy game-model provider recipe

Before any paid Meshy task or any production use of a Meshy result, read
[`references/meshy-game-model-pipeline.md`](references/meshy-game-model-pipeline.md)
completely. It pins the Smart-first geometry gate, web-app/API multi-view
difference, credit boundary, texture stage, runtime optimization, animation
ownership, provenance, credential handling, and projectile variant. The short
rule is: approve clean views, buy geometry before texture, reject silhouette or
part-boundary failures while cheap, prove required rigid pivots in Blender,
texture only an accepted mesh, and optimize the corrected result for gameplay
rather than shipping provider output directly.

#### Fused team-accent extraction

When a reviewed generated mesh has one fused material but clean bounded
team-color paint, read
[`references/team-accent-extraction.md`](references/team-accent-extraction.md)
completely and use its deterministic offline partition procedure. Never solve
this with a runtime hue-key shader.

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
6. A legacy 2D-first look may add a WebGL companion after its 2D look and
   fidelity brief are approved. A new 3D-first look instead derives its
   `sprite.png`, store/site renders, and semantic mask from the accepted
   editable model and pinned cameras. In both cases keep a 2D runtime asset for
   site, Canvas2D, mobile, CLI, loading, and fallback.
7. Build the companion as authored geometry, not a shallow extrusion of the
   sprite. Preserve the 2D silhouette and rule-bearing hardware, then model the
   hull, armor layers, recesses, joints, vents, and weapon volumes that explain
   it from the gameplay camera. Check in an editable `.blend` source and a
   deterministic generator or export recipe under `art/`.
8. Put `model.glb` and `model3d.json` beside the runtime look only after the
   model passes the gameplay review. The GLB contract is `+X` facing, `+Y` up,
   floor at `Y=0`, tile-relative scale, no camera, no light, and no autonomous
   animation. An animated companion may contain only named semantic clips
   supported by the renderer contract; the renderer samples them from replay
   time and explicit events rather than letting the asset advance gameplay
   state. Discovery stays manifest-driven inside `render3d`; never add the
   model to the shared appearance registry or a hand-written TypeScript
   catalog.
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
5. Keep a shared world language with the approved class concepts: blackened
   structural metal, restrained warm machinery, layered armor, recessed vents,
   and swept/chamfered profiles. Do not copy any one class's hero palette into
   the whole environment—warm graphite/bronze belongs to Striker, pale
   ceramic/copper to Fabricator, and Bulwark receives its own separately
   approved fortress identity.
   Preserve visual hierarchy rather than matching finish one-for-one.
   Environments are rougher, more matte, coarser-detail, lower-contrast, and
   less saturated; bots and projectiles are cleaner, sharper, finer-detail
   hero assets with stronger controlled emission. Team colors belong to
   renderer-owned actor/zone light, not neutral architecture.
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
