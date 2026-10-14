# Meshy game-model pipeline

Use this reference for paid Meshy work on Nilbots bots, rigid form kits,
projectiles, or skins. Re-check the linked official documentation immediately
before a request because provider surfaces, prices, and model names change
outside the repository.

The facts below were verified on 2026-07-31.

## Choose the art authority

For a new Nilbots family that is intended to have real 3D depth, make the
accepted editable 3D scene the visual authority. Concept art and generated
views are inputs to the brief and approval process; they stop being separate
runtime identities once the model is accepted. Render the East-facing 2D
avatar, store/site imagery, thumbnails, and a lossless semantic team-mask pass
from pinned Blender cameras in that same scene. This keeps the WebGL model and
2D surfaces visually identical and makes later skins material/kit variants of
one family master.

Existing 2D-first class art remains a migration constraint until its first 3D
master passes silhouette, rule-hardware, material, gameplay-camera, and owner
approval gates. Do not silently replace a shipped 2D identity with a generated
model. Keep the old sprite as the fallback authority until the derived raster
plus semantic-mask runtime contract is implemented and reviewed.

### Freeze the family master and fork skins

After owner approval, preserve the concept provenance and promote the exact
accepted neutral scene when it passes the structural and runtime gates. Use
stable art-side identities:

- `familyId`: the persistent chassis family, for example `aegis-tortoise`;
- `conceptId`: the immutable approved concept provenance;
- `geometryAuthorityId`: one immutable geometry/rig/UV/camera revision;
- `skinId`: the stable catalog and entitlement identity;
- `skinRevision`: an art/runtime revision that does not change entitlement
  identity.

The neutral authority owns geometry, UVs, hierarchy, pivots, clips, form
choreography, semantic emitter surfaces, pinned cameras, and presentation-only
locomotion such as Bulwark's low hover. Record `blendSha256`,
`neutralGlbSha256`, `surfaceUvDigest`, `sceneRigDigest`, `clipDigest`,
`teamAccentSurfaceDigest`, and `cameraRigDigest`. Never modify that authority
to make a texture donor fit. A mechanical or silhouette change creates a new
`geometryAuthorityId`; retain the prior concept and authority as historical
evidence.

All Nilbots bot families levitate unless the owner explicitly changes that
game-wide premise. Encode the family's hover height, shadow/underglow,
bob/tilt response, and any stance-lock behavior in its authority and runtime
presentation profile. Generated geometry or textures must not invent feet,
walking legs, wheels, tracks, or landing gear; apparent support modules are
hover/lock hardware unless the approved mechanics say otherwise.

Each skin record copies the family and authority IDs/digests and adds its own
stable `skinId`, revision, sanitized provider task/request hashes, donor and
map hashes, transplant report hash, runtime material factors/GLB hash, and
derived avatar/mask hashes. Generate each skin from the same hash-pinned
static upload and transplant each donor independently onto a clean copy of the
neutral authority. Never branch skin B from textured skin A.

Keep radically different texture identities broad enough to survive the
roughly 41-pixel gameplay footprint: change large material regions, value
hierarchy, finish, and fixed non-team accents rather than relying on
microdetail. Preserve the family's hover identity in every skin; texture must
not invent feet, legs, tracks, wheels, landing gear, or another locomotion
promise.

## Choose the route before the provider

When any required motion depends on authored seams, hidden faces, stowage,
clearance, pivots, or ordered deployment, skip provider geometry and use the
deterministic multipart route:

1. Infer the complete concept from the rule-and-identity brief, including
   stowed parts and ordered choreography.
2. Build the untextured multipart Blender authority with names, hierarchy,
   pivots, hidden surfaces, UVs, and semantic clips.
3. Render the strict untextured multiview and clearance-critical intermediate
   stages from that scene; stop for explicit owner approval and freeze its
   hashes.
4. Export a static body-surface upload with the same body surface/UV digest,
   one provider-eligible material, and no animation. Exclude renderer-owned
   semantic emitter geometry from the provider upload and pin its separate
   authority digest so it remains untouched during transplant. If a deployed
   coverage pose is needed, bake every evaluated child world transform into a
   temporary identity-root mesh copy. Never export articulated children in
   local space after omitting their non-renderable parents.
5. Run Meshy 6 Retexture with original UV, PBR, 4K base/normal source maps,
   lighting removal, and a recorded standalone-PNG payload.
6. Quarantine the returned GLB, prove oriented-UV correspondence and global
   similarity, discard its geometry, and transplant only its proven PBR
   payload onto the untouched authority.
7. Preserve the donor-exact source and derive renderer-specific material
   tuning explicitly.
8. Preserve texture-free semantic team emitters; let the renderer own tint,
   emission strength, bloom, and floor glow, then approve through a
   hash-verified native replay.

Use Smart geometry only when the accepted mobile body may remain generated
and no required motion crosses an inferred or fused seam.

### Gate nested rigid mechanisms before material work

Treat every cover, well, magazine, socket, and hidden stage as production
geometry. A rendered endpoint can conceal an impossible transition. For a
nested deploy, author and prove this dependency order explicitly:

1. Unlock and move the real cover completely clear of both the swept volume
   and neighboring stance parts.
2. Raise the outer stage only after the cover reaches its clearance endpoint.
3. Raise the inner stage while retaining a measured telescopic engagement.
4. Move a stowed weapon carrier through its authored magazine while it remains
   in the compact orientation.
5. Fold the carrier into its deployed orientation only after it clears the
   keep and deck.
6. Extend or recoil the barrel only after the carrier is nearly aligned.

Prove the reverse order with the same clip sampled backward or with
transform-identical endpoints. At the rest frame, measure the complete kit
below the cover, above the well floor, and inside the narrowest radial opening;
include bevels, muzzles, emitters, and carrier shoulders rather than nominal
pivot radii. Sample every exported frame for forbidden intersections using the
static hull, cover leaves, both stage meshes, neighboring form kits, semantic
emitters, and all moving meshes. Do not call a collision ledger conclusive when
it excludes the surface that creates the clearance.

Render a phase sheet that names the dependency stages, for example `HATCH`,
`LOWER`, `UPPER`, `SLIDE`, `FOLD`, `BARREL`, and `HOLD`, plus strict side and
top views. Encode the review animation with a maintained library or a standard
container. Decode frame zero independently and compare it with the source PNG;
custom GIF headers, page counts, and timing metadata can all pass while the
pixel stream is corrupt.

## Choose the provider surface for generated geometry

Prefer Smart Topology T2 for a new real-time asset:

- `model_type: "smart-topology"`
- `ai_model: "meshy-t2"`
- native triangle output
- native part segmentation
- `target_polycount` from 100 through 15,000

Do not use deprecated `model_type: "lowpoly"`. Do not assume native
segmentation means useful names, mechanical meaning, hierarchy, pivots,
closed hidden faces, or animation-ready seams.

Keep web-app and API capabilities distinct:

- The documented Image-to-3D API accepts one image and supports Smart T2.
- The documented Multi-Image API accepts one through four images but currently
  exposes only Meshy 5, Meshy 6, or `latest`; it has no `model_type` or T2.
- The web app presents Smart Topology and Multi-view in the same workflow.
  When that UI accepts four views, use four there. Never invent an
  undocumented Smart multi-image REST payload or assume it ran T2.

For an automated API proof, use one approved elevated front three-quarter
image that exposes planform and side depth. For a web-app Smart multi-view
proof, use four separate approved images in this order:

1. elevated front three-quarter;
2. strict inferred-3D top;
3. strict side;
4. rear or rear three-quarter.

Use one object per image and one object per task. Do not upload a contact
sheet, map, detached projectile, alternate form, or reference annotations.
Keep all views at the same proportions, pose, framing, scale, geometry,
materials, diffuse lighting, and neutral background. Use lossless square
inputs at least 1040 pixels on the shortest side. Remove cast shadows, floor,
hover bloom, text, and team-colored light. Require owner approval of every
inferred view before upload.

## Gate geometry before texture

Start with an untextured Smart task. As of the verification date this costs
5 credits; adding texture immediately costs 15 total, while a later 2K/4K
Retexture costs 10. The staged path costs the same for a success and only
5 credits for rejected geometry.

For an API proof use:

```json
{
  "image_url": "<approved-elevated-three-quarter-data-uri-or-url>",
  "model_type": "smart-topology",
  "ai_model": "meshy-t2",
  "target_polycount": 4000,
  "should_texture": false,
  "target_formats": ["glb"],
  "alpha_thumbnail": true
}
```

Omit `topology`, `should_remesh`, `save_pre_remeshed_model`,
`image_enhancement`, PBR, and texture fields. Smart ignores the first three;
image enhancement is documented for Meshy 6/`latest`, not T2.

Use 4,000 triangles as a recoverable bot geometry master, not an automatic
runtime entitlement. It is T2's current default and leaves enough geometry to
judge segmented hard-surface parts. Meshy's current general mobile-hero
guideline is 1,000–3,000 triangles. After the master passes, compare
deterministic derivatives around 2,500, 1,200, and 500 triangles at the store
preview, 140-pixel review size, and roughly 41-pixel gameplay size. Keep a
richer on-demand tier only when it visibly wins. For a compact projectile
head, begin around 800–1,500 triangles and reduce after a real renderer test.

Do not spend the texture stage until all applicable geometry gates pass:

- Normalize and measure the actual provider-facing axis, then compare the
  corrected `+X`-facing result.
- Require at least 90% same-scale top-silhouette IoU against the approved
  canonical planform.
- Require the side and rear body class, thickness, negative spaces, and weapon
  placement to match their approved views.
- Reject invented feet, landing gear, mobility cues, weapons, or detached
  fragments.
- Reject melted seams, holes, inverted normals, incoherent undersides, and
  fused joints that a required motion must cross.
- Inventory triangles, vertices, meshes, nodes, primitives, materials, UVs,
  bounds, origin, and validation warnings.
- Verify that every required moving plate, barrel, pod, crown, or vane is a
  usable segment or clean loose island. Part names and object origins do not
  count as correct until Blender proves them.
- Run a Blender pivot proof for at least one representative required motion
  before accepting the geometry.

If Smart fails, classify the failure. Regenerate only when another input or
T2 target can plausibly repair inference. Use Blender for deterministic
orientation, scale, pivots, hidden faces, seams, cleanup, and retopology. Buy a
Standard result only after a measured Smart failure shows a need for a
high-detail form or texture donor. Do not buy Standard automatically.

Triangle topology is acceptable for rigid machinery. Use rigid object
transforms or one hundred percent rigid bone weights; deformation edge loops
are unnecessary when vertices never bend. Manually retopologize only parts
that truly deform.

## Texture only accepted geometry

For generated geometry, retexture the accepted T2 task before introducing a
multipart rig. For a deterministic animated chassis, export a separate static
full-surface upload from the exact authority: one texture-eligible material,
one authored non-overlapping UV atlas, no animation, and semantic team-accent
surfaces excluded from provider replacement. Use `input_task_id` for the first
route and `model_url` for the second. Never upload the only copy of the
animation authority.

Meshy may fuse meshes, weld duplicate vertices, and apply a global similarity
transform even when `enable_original_uv` is true. “Original UV” preserves the
layout; it is not a promise that the returned GLB keeps object hierarchy,
pivots, vertex indexing, bounds, or animation.

Use a Meshy 6 Retexture request shaped like:

```json
{
  "model_url": "<accepted-static-upload-data-uri-or-url>",
  "image_style_url": "<approved-neutral-material-reference>",
  "ai_model": "meshy-6",
  "enable_original_uv": true,
  "enable_pbr": true,
  "texture_resolution": "4k",
  "remove_lighting": true,
  "target_formats": ["glb"]
}
```

As verified on 2026-07-31, Retexture costs 10 credits at either 2K or 4K;
8K costs 15 and does not return an emission map. Prefer 4K for a lazy-loaded
hero bot skin once the UV atlas passes preflight. Meshy defines this setting as
the base-color resolution: a 4K task may still return some auxiliary maps at
2K, so record the dimensions of every downloaded map rather than assuming the
whole PBR set is 4K. Resolution is not a substitute for usable UV allocation.

Before any paid texture task, generate an atlas report from the exact static
upload:

- exclude semantic `team-accent`/`projectile-mask` surfaces from the
  texture-eligible provider upload;
- measure body UV occupancy and reject obviously wasted atlases; target at
  least 70% unless padding or a recorded platform constraint justifies less;
- record per-mesh 3D surface-area share, UV-area share, and texels per world
  unit, plus projected pixels per world unit at gameplay and avatar/store
  cameras;
- flag major visible meshes whose UV share is radically smaller than their
  surface/projected share, even when global occupancy passes;
- use coverage poses or a static coverage scene that exposes hidden surfaces
  needed by Mobile, Shell, Tower, recoil, and reverse; and
- reimport the exact provider upload and compare its complete named object
  set, per-object triangle counts, and world-space bounds to the evaluated
  coverage scene. Explicitly prove repeated pods, plates, vanes, barrels, or
  other instances have distinct centers; a plausible preview does not catch
  multiple local-space copies stacked on one transform;
- preserve exact geometry, names, partitions, transforms, pivots, and clips
  when making a UV-only authority revision. A changed UV digest requires a new
  reviewed authority revision, not an undocumented overwrite.

The first `aegis-tortoise-ga-002` texture trial is the failure example: 38
meshes received equal `0.12 × 0.12` cells, body occupancy was only `27.804%`,
and `Mobile.Hull.Static` held `62.65%` of body surface area but only `0.93%` of
used UV area. Its 2K hull therefore had roughly `104 × 104` effective source
pixels and was magnified about `5.7×` in the 512 proof. A 4K rerun alone would
only double that inadequate allocation.

Choose exactly one of `image_style_url` or `text_style_prompt`. Prefer the
approved neutral material concept when its geometry and regions match the
accepted mesh. Use a text prompt when the task is primarily to suppress
photorealism or correct finish. Keep the prompt descriptive rather than
redesigning:

- stylized matte game asset;
- broad readable material regions;
- controlled roughness and restrained edge wear;
- no micro-scratches, dirt noise, glossy clearcoat, text, logos, decals, or
  baked lighting;
- no blue or red team color. When semantic emitter meshes are excluded from
  the upload, do not ask Meshy to invent team lights on the body; restore the
  untouched texture-free emitters from the authority after transplant.

Keep the 2K PBR output as an art source. Test 1K and 512 runtime atlases. Retain
normal or metallic/roughness maps only when their contribution survives the
actual arena lighting and camera. Nilbots wants shaped, stylized materials,
not realism for its own sake.

### Prove and transplant a texture donor

Do not ship an uploaded Retexture result directly for an animated authority.
Treat its geometry as untrusted and transplant only the PBR material payload
after all of these gates pass:

1. Pin hashes for the animated authority, static upload, raw provider GLB, and
   every texture. Reject external buffer/image URIs, traversal, unexpected
   extensions, and unsupported samplers before Blender opens the donor.
   A world-baked deployed coverage upload also requires a pinned PASS report,
   the exact active semantic clips, and its evaluation frame. Reconstruct the
   body-only identity-root coverage GLB from the untouched authority and
   require exact mesh accessors, nodes, and raw surface digest before accepting
   the upload. `nilbots-texture-donor-transplant-4` is the Aegis Tortoise
   implementation: temporary coverage geometry must be gone before material
   transplant and provider geometry must never enter the output.
2. Pair every retained oriented provider triangle with exactly one upload
   triangle by its three UV corners. The donor may not introduce or remap a
   triangle, contain an ambiguous/degenerate UV triangle, or change UV/3D
   winding. A provider may drop source-only export slivers only under fixed
   count, UV-area, normalized 3D-area, and source-degenerate bounds recorded
   by the report; never weaken the authority or copy donor geometry to make a
   result pass.
3. Solve one orientation-preserving global similarity from the paired
   positions and normals. Reject reflection, non-uniform deformation, or
   residual drift. For Aegis Tortoise `ga-002`, schema
   `nilbots-texture-donor-transplant-2` uses maximum/RMS/p99 normalized
   position gates of `1e-4`/`5e-6`/`2e-6`; all four initial donors achieved
   `7.53e-5`/`4.05e-6`/`1.70e-7`.
4. Account explicitly for provider welding and bounded removal. The `ga-002`
   proof pairs all `30,570` retained oriented triangles and `91,710` corners,
   rejects every donor-only triangle, and explains exactly `288` source-only
   slivers: `3.91e-6` of source UV area and `1.043e-4` of source-span-squared
   spatial area, including `96` zero-area UV triangles. Matching raw vertex or
   triangle counts is not required; complete accounting is.
5. Prefer the Retexture task's standalone lossless `base_color`, `metallic`,
   `roughness`, `normal`, and `emission` PNG downloads. Pin their hashes and
   dimensions, verify they belong to the same archived task and match the
   donor's UV/material roles, and compose glTF metallic/roughness losslessly
   (`G = roughness`, `B = metallic`) without JPEG conversion. Keep base color
   and emission in sRGB and data maps in Non-Color. The provider GLB remains
   geometry/UV, sampler, and material-factor evidence, but its embedded JPEGs
   are not preferred texture bytes: the initial Foundry donor measured only
   about `37 dB` PSNR against the downloaded PNG base/normal maps and showed
   large edge errors. Assign the resulting exact PBR maps/factors to the
   untouched authority materials. Preserve this donor-exact art source and
   keep the semantic team material texture-free; renderer-specific factor
   tuning belongs to a separately recorded runtime derivative, not inside the
   transplant proof.
6. Export from the authority scene, then compare baseline and final scene
   roots, hierarchy, transforms, pivots, skins, geometry/UV/material bindings,
   semantic material metadata, and every animation channel, target, time, and
   value exactly. Deriving MikkTSpace tangents at runtime is acceptable only
   when the target renderer is tested and the unchanged
   `POSITION`/`NORMAL`/`TEXCOORD_0` streams are pinned.

Fail closed. A visually attractive donor with an unprovable UV/geometry
correspondence is reference art, not a transplant source.

Author a genuinely non-overlapping body atlas for new masters. When a frozen
legacy/approved authority contains numerical subpixel atlas exceptions, the
validator may ignore them only under fixed aggregate UV-area, pair-count,
per-triangle UV-area, and unique participating 3D-area bounds. The `ga-002`
ledger records `672` strict micro-overlap pairs covering `4.96e-5` of atlas
area and `5.54e-4` of model-span-squared unique 3D area; a regression fixture
with large 3D panels compressed into tiny overlapping UV triangles must fail.
Call this a bounded micro-exception in reports—never “non-overlapping.”

### Calibrate the runtime material derivative

Provider previews and Blender worlds are not runtime authority. Nilbots'
production arena currently uses directional key/fill plus ambient light and
no environment map or IBL. A fully metallic donor can therefore collapse into
dark specular-only surfaces even when its maps are valid.

After the donor-exact transplant passes, begin by A/B testing body material
factors in the production renderer using the same hash-verified replay,
tick/fraction, viewport, DPR, camera, and theme. Keep geometry, UVs, hierarchy,
clips, and the semantic material unchanged. If a valid donor still bakes
near-black armor, pure-white body islands, excessive metallic response, or
non-team emission into its maps, preserve that donor-exact source and create a
separate deterministic runtime texture derivative. Pin the source and output
hashes and record every transfer curve, hue region, roughness floor, metallic
cap, emission scale, and normal scale. Never silently repaint the donor proof.
Choose the smallest adjustment that restores gameplay-size value and material
separation without flattening the finish; non-team emission must remain below
the renderer-owned team emitters.

At a reviewed bot footprint of roughly 39--46 pixels, 4K base color and normal
already provide substantial source-art headroom. Do not buy an 8K retry merely
to cure a soft, bland, or noisy runtime read: high-frequency detail will not
survive that projection, and provider 8K support may not raise every PBR map.
A justified paid retry changes the broad value/material hierarchy or fixed
accent placement. Preserve a materially different but rejected result as a
hash-pinned future skin source instead of iterating it into the default.

Do not use a custom Blender sheet as the final lighting gate. It may diagnose
UV mapping and PBR response, but acceptance requires the production WebGL
color management, camera, theme key/fill/ambient lights, bloom, floor glow,
and normal zoom. Use a higher-sample DCC render for art inspection and retain a
deterministic low-sample render only as structural QA; never mistake jagged
single-sample output for source texture resolution. Record the exact renderer
configuration instead of claiming a DCC calibration matches runtime.

For automated browser evidence, hash the source model and the bundled lazy
asset before navigation, then require the production viewer to fetch that
exact bundled path and byte length. Do not depend on reading a large GLB body
from Chromium's inspector cache. Explicitly dismiss the ready play overlay and
assert it is detached before every screenshot; a technically complete capture
through the dimmed `Watch the match` scrim is invalid evidence.

Record donor and runtime factors, renderer/light configuration, source/final
GLB hashes, and the reason in material extras and the capture report. The
Bulwark candidate's `metallicFactor: 0.45` from a donor default of `1.0` is
look-specific evidence, not a global default.

Do not ask Meshy to own the semantic team material. In Blender, isolate clean
bounded emitter faces into one texture-free neutral material with
`extras.nilbotsRole = "team-accent"` for bots or
`extras.nilbotsRole = "projectile-mask"` for projectiles. The renderer owns
team tint, emission strength, bloom, floor glow, and local contrast. Keep at
least 90% of a bot's visible body class-authored and neutral with respect to
team. Every skin must preserve the exact `teamAccentSurfaceDigest`, material
role, emitter transforms, and texture-free base/emissive slots; never assign
provider maps to those surfaces. Fixed skin color belongs to the hull material,
not the semantic accent. Review both team colors at roughly 41 pixels and
normal zoom, and require an explicit fixed-color collision check when a skin
uses orange, red, cyan, or blue.

Emission strength is not projected surface area. If a semantic strip remains
about one native pixel wide, first enlarge only that emitter's authored minor
axis, with a pinned geometry transform and new semantic-surface digest, until
it forms a restrained two-to-three-pixel cluster. Re-run all pose, occlusion,
clip, bounds, both-team, and no-broad-paint gates. Do not compensate by tinting
the hull or indefinitely raising emission; tone mapping tends to drive the same
tiny samples toward white while barely changing their apparent size.

## Build the runtime asset in Blender

Treat provider output as source art, never as a shippable result.

1. Add closed hidden faces, hinge barrels, sockets, rails, and cavities.
2. Rename parts, place exact pivots, and establish parent hierarchy.
3. Merge static segments by module/material. Keep only independently moving
   parts separate. Target roughly 8–12 functional draw structures for a
   Bulwark-class bot; native segmentation may otherwise create many draws.
4. Prefer one shared PBR atlas plus the texture-free semantic team material.
5. Author rigid `deploy`, `hold`, `retract`, and localized `recoil` actions.
   Keep forward and reverse endpoints transform-identical and inspect actual
   keyframes. At the pinned gameplay camera, require multiple readable deploy
   stages and enough recoil travel to show a kick and recovery; the accepted
   roughly 41-pixel Bulwark proof measured about 3–5 pixels of principal-part
   travel and exact zero-difference settle.
6. Keep hover/bank/settle as a renderer-owned locomotion profile. Do not bake a
   free-running hover loop that advances independently of replay time.
7. Export `+X` facing, `+Y` up, floor at `Y=0`, tile-relative scale, no camera,
   no light, and only named semantic clips the renderer explicitly samples.
8. Bind each runtime clone to its own mixer, but sample clips from absolute
   replay time. Drive deploy/retract from authoritative form progress, firing
   recoil from attack events, and defensive Shell recoil from authoritative
   deflections. Deduplicate same-tick volleys and interrupt recovery on a
   conflicting form transition.
9. Validate the GLB, hashes, clips, bounds, draw count, maps, both team colors,
   all legal headings, wall clearance, fog, damage, fallback, multiple
   instances, store preview, and a fresh hash-verified native replay. Record
   replay-file SHA-256, canonical replay hash, tick/fraction, viewport, DPR,
   camera, theme, renderer/build revision, and model hash. Baseline and
   candidate must use the same replay state. Inspect Mobile, ordered deploy
   stages, forward/reverse, every recoil, both accents on the same neutral
   hull, semantic emitters, and renderer-owned floor glow at normal zoom; clip
   presence alone is not animation approval.

Meshy's programmatic Rigging API is for standard textured humanoid bipeds.
Its Animation API applies preset action IDs to those rigs. The web app also
offers humanoid/quadruped presets, but neither surface authors custom
Mobile/Shell/Tower hard-surface choreography. Author that work in Blender.

## Choose the hybrid route

Pick the route before buying geometry. The deciding question is who must own
the silhouette and moving seams:

1. **Generated mobile body, deterministic semantic rig** — use Smart for a
   visually distinctive rigid mobile whose required motion can be expressed by
   rotating or translating clean connected components. Normalize, partition,
   name, pivot, and validate those components in Blender. This is the earlier
   accepted Bulwark mobile-only route.
2. **Deterministic chassis and form kits, generated texture donor** — use
   Blender for the complete mobile/Shell/Tower geometry when a transition must
   be mechanically traceable. Lock the kinematic blockout first, unwrap one
   non-overlapping atlas, then use Retexture as a material donor and transplant
   only its proven PBR payload. This is the accepted full Bulwark-family route
   and the preferred route for future skills and stances because provider
   inference cannot invent trustworthy hidden faces, stowage, sockets, or
   pivots.
3. **Generated donor plus DCC rebuild** — use a generated result only as a
   proportion, surface, or texture donor when it has the right visual language
   but the wrong topology. Rebuild the production mesh around the approved
   silhouette rather than accumulating brittle cuts in a fused surface.
4. **2.5D fallback** — retain the canonical SVG-derived renderer form when
   genuine depth does not survive gameplay size or when a 3D route cannot meet
   the rule/fallback/budget gates. It is a valid shipping fallback, not a
   failed source model.

Do not ask one provider task to solve mobile body, planted form, detached
projectile, and texture together. They have different silhouettes, pivots,
runtime lifecycles, and acceptance gates. A planted form may reuse the mobile
core and material atlas, but its add-on kit must remain separately selectable
and independently grounded.

### Historical proven Bulwark-family recipe

The accepted 2026-07-30 Aegis Tortoise Mobile/Shell/Tower authority demonstrates
the second route from start to finish. Its hashes and measurements remain
historical evidence if a later folded-gun authority supersedes it; never
rewrite them in place:

1. Blender owns all `31` rigid meshes, hidden faces, pivots, sockets, and
   mechanically traceable endpoints. The authority has `22,320` triangles and
   one non-overlapping atlas.
2. The neutral master exports exactly `14` named semantic clips:
   `PodPlant_HoverSettle`, two deploy clips, three renderer recoil clips, and
   eight independently addressable Tower-vane recoil clips. Deploy endpoints
   reverse exactly; every recoil settles exactly.
3. Four small texture-free `team-accent` emitters occupy `1.834944%` of the
   fixed top projection. Team blue/orange, bloom, and floor glow remain
   renderer-owned.
4. A static full-surface upload with the same surface/UV digest went to Meshy 6
   Retexture with original UV, 2K PBR, lighting removal, and a stylized matte
   graphite/gunmetal prompt. The provider charged `10` credits.
5. Meshy returned a fused, welded, uniformly normalized GLB. The oriented-UV
   similarity proof passed, so only its PBR payload was transplanted onto the
   untouched animated authority; provider geometry was discarded.
6. The final export preserves all `14` clips and semantic accents exactly.
   Runtime manifests name the two deploy and three form-specific recoil clips;
   per-clone mixers sample them from replay truth and fail closed to the
   existing fallback when binding is invalid.
7. Native roughly 41-pixel proofs quantify deploy stages and stronger localized
   recoil. Mobile, Shell, and Tower settle to the exact rest frame; sampled BVH
   gates reject foreign moving-part collisions while allowing only each Tower
   barrel's bounded travel inside its own cradle.
8. Keep the transparent East-facing avatar and semantic mask derivation as the
   final stage. Do not replace an existing 2D fallback until those outputs and
   their consumers are explicitly reviewed.

The reusable lesson is that generated finish and mechanically valid motion
have different authorities. Build and prove the machine first, buy surface
language second, transplant only what can be verified, and approve motion in
the real renderer rather than in a provider preview.

## Projectiles and skins

Generate a default projectile separately after its bot geometry path passes.
Show only the projectile head. Model a readable volume and directional
silhouette; do not model trail, range, hitbox, floor glow, or impact field.
The renderer owns spin, travel, trail, team emission, hit/miss effects, and
replay timing. A look may choose an outcome-agnostic terminal signature, but
the renderer must classify authoritative damage, destruction, guard,
deflection, harmless expiry, and purge before sampling it; never infer the
outcome from disappearance. Compare a mechanical head and a generic neutral
energy head in the real renderer before choosing the default.

For skins that preserve the approved silhouette and moving hardware, reuse one
neutral family authority and author a distinct material/texture set for each.
Submit one Retexture task per skin from the same static UV-identical upload and
settings, then transplant each donor independently onto a clean authority
copy. Never use a textured skin as the input to another skin. A skin identity
must survive roughly 41 pixels through broad regions, value grouping, and
material finish rather than palette-only microdetail. Use a new Smart task only
for a deliberate silhouette or hardware change. Keep store-bundle availability
separate from rendering manifests and gameplay.

For Aegis Tortoise's first Foundry candidate, keep the hull mid-value and
readable: warm ash-gray forged iron, refractory slate, and broad fixed
lava-orange heat-ceramic/enamel regions. The lava orange is class-authored hull
color, not team emission; keep it non-emissive or deliberately weaker than the
semantic lights. Reject a near-black soot blanket or specular-only metal and
require an orange-team collision review at roughly 41 pixels.

## Credentials and provenance

Keep the API credential out of the repository, shell profiles, command
history, screenshots, logs, and generated provenance. On the Nilbots macOS
workstation it lives in Login Keychain under service `nilbots.meshy.api` and
the current login account. Read it inside the calling process with
`/usr/bin/security`; never print the value. Other environments inject a secret
at process scope and never commit `.env` files.

Before a paid request, check the numeric balance and re-check the exact price.
Record the approved image filenames and hashes, sanitized request, task ID,
provider surface, model version, numeric balance before/after,
`consumed_credits`, sanitized task JSON, license terms, downloaded outputs,
validation reports, and output hashes under
`art/class-models/provider-runs/meshy/<look-id>/<task-id>/`. Strip signed URL
queries, input data URIs, and authorization data.

Download all outputs immediately. Non-Enterprise API assets are retained for
at most three days, and API tasks do not serve as repository storage.

## Official sources

- <https://docs.meshy.ai/en/api/image-to-3d>
- <https://docs.meshy.ai/en/api/multi-image-to-3d>
- <https://docs.meshy.ai/en/api/retexture>
- <https://docs.meshy.ai/en/api/rigging>
- <https://docs.meshy.ai/en/api/animation>
- <https://docs.meshy.ai/en/api/pricing>
- <https://docs.meshy.ai/en/api/asset-retention>
- <https://docs.meshy.ai/en/api/changelog>
- <https://docs.meshy.ai/en/webapp/image-to-3d>
- <https://help.meshy.ai/en/articles/12634481-how-to-use-multi-view>
- <https://help.meshy.ai/en/articles/15723950-how-to-make-meshy-models-game-ready>
- <https://help.meshy.ai/en/articles/9991884-what-3d-file-formats-does-meshy-support-full-export-list>
