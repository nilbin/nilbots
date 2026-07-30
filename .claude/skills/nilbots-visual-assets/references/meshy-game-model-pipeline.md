# Meshy game-model pipeline

Use this reference for paid Meshy work on Nilbots bots, rigid form kits,
projectiles, or skins. Re-check the linked official documentation immediately
before a request because provider surfaces, prices, and model names change
outside the repository.

The facts below were verified on 2026-07-30.

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

## Choose the provider surface

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
  "texture_resolution": "2k",
  "remove_lighting": true,
  "target_formats": ["glb"]
}
```

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
- neutral-white candidate emitter regions, never baked blue or red team color.

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
2. Pair every oriented provider triangle with exactly one upload triangle by
   its three UV corners. Reject ambiguous or degenerate UV triangles and any
   UV/geometry winding mismatch.
3. Solve one orientation-preserving global similarity from the paired
   positions and normals. Reject reflection, non-uniform deformation, or
   residual drift. The accepted Bulwark donor used maximum/RMS position gates
   of `2e-5`/`2e-6`, achieved `1.28e-7`/`3.23e-8`, and had minimum normal dot
   above `0.999999996`.
4. Account explicitly for provider welding. The accepted proof paired all
   `22,320` oriented triangles and `66,960` corners while explaining `2,212`
   welded duplicate export vertices; matching vertex counts was not required.
5. Assign the donor's exact base-color, metallic/roughness, normal, and
   emission maps/factors to the untouched authority materials. Preserve the
   semantic team material texture-free.
6. Export from the authority scene, then compare baseline and final scene
   roots, hierarchy, transforms, pivots, skins, geometry/UV/material bindings,
   semantic material metadata, and every animation channel, target, time, and
   value exactly. Deriving MikkTSpace tangents at runtime is acceptable only
   when the target renderer is tested and the unchanged
   `POSITION`/`NORMAL`/`TEXCOORD_0` streams are pinned.

Fail closed. A visually attractive donor with an unprovable UV/geometry
correspondence is reference art, not a transplant source.

Do not ask Meshy to own the semantic team material. In Blender, isolate clean
bounded emitter faces into one texture-free neutral material with
`extras.nilbotsRole = "team-accent"` for bots or
`extras.nilbotsRole = "projectile-mask"` for projectiles. The renderer owns
team tint, emission strength, bloom, floor glow, and local contrast. Keep at
least 90% of a bot's visible body class-authored and neutral with respect to
team.

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
   instances, store preview, and a real native replay. Inspect forward and
   reverse deploy plus every recoil at normal zoom; clip presence alone is not
   animation approval.

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

### Proven full Bulwark-family recipe

The accepted Aegis Tortoise Mobile/Shell/Tower family demonstrates the second
route from start to finish:

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

For a skin that preserves the approved silhouette and moving hardware, reuse
the corrected base mesh and author a new material/texture set. Use a new Smart
task only for a deliberate silhouette or hardware change. Keep store-bundle
availability separate from rendering manifests and gameplay.

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
