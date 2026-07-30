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

Retexture the accepted T2 task before introducing a multipart rig. Meshy may
merge uploaded multi-mesh files, and its keep-original-UV path expects one
material and one non-overlapping UV set after merge.

Use a Meshy 6 Retexture request shaped like:

```json
{
  "input_task_id": "<accepted-smart-task-id>",
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
   keyframes.
6. Keep hover/bank/settle as a renderer-owned locomotion profile. Do not bake a
   free-running hover loop that advances independently of replay time.
7. Export `+X` facing, `+Y` up, floor at `Y=0`, tile-relative scale, no camera,
   no light, and only named semantic clips the renderer explicitly samples.
8. Validate the GLB, hashes, clips, bounds, draw count, maps, both team colors,
   all legal headings, wall clearance, fog, damage, fallback, multiple
   instances, store preview, and real replay.

Meshy's programmatic Rigging API is for standard textured humanoid bipeds.
Its Animation API applies preset action IDs to those rigs. The web app also
offers humanoid/quadruped presets, but neither surface authors custom
Mobile/Shell/Tower hard-surface choreography. Author that work in Blender.

## Projectiles and skins

Generate a default projectile separately after its bot geometry path passes.
Show only the projectile head. Model a readable volume and directional
silhouette; do not model trail, range, hitbox, floor glow, or impact field.
The renderer owns spin, travel, trail, team emission, hit/miss effects, and
replay timing. Compare a mechanical head and a generic neutral energy head in
the real renderer before choosing the default.

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
