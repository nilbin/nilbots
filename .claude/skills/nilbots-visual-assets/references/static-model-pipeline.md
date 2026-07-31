# Static model pipeline

Read this file completely before creating or reducing a bot/projectile GLB. It is the
reconstructable source-to-runtime route for static low-hover class models and their later
skins. Animation experiments are not part of this route.

## Authority chain

Keep these layers distinct and never overwrite an earlier one:

1. Owner-approved 3D concept plus strict multiview inputs.
2. Raw Meshy provider output and sanitized provider record.
3. Deterministically normalized master.
4. Optional semantic-material derivative.
5. Measured runtime tier candidates.
6. Accepted runtime GLB and pinned 2D derivative renders.

The accepted 3D master owns the family silhouette after approval. The target mobile game
renders that 3D asset directly. Site, store, Canvas2D, loading, CLI and mobile-fallback
images derive from pinned renders of it. Later skins reuse the accepted geometry; do not
generate a new chassis and call it a texture variant.

## 1. Approve inputs

1. Write the fidelity brief: identity references, top silhouette anchors, side/depth
   anchors, negative spaces, rule-bearing hardware, fixed material regions, bounded team
   accent candidates, forbidden cues and intended gameplay read.
2. Approve one coherent concept plus strict top, strict side, front three-quarter and rear
   three-quarter images. Keep object scale/framing consistent on square lossless canvases
   at least 1040 pixels on the shortest side. Use neutral/transparent backgrounds, diffuse
   lighting, no floor, cast shadow, text, projectile or scenery.
3. Pass the same-scale top overlay before upload. Do not use an unapproved inferred view or
   a contact sheet as one provider image.

## 2. Generate the recoverable master

Use one Meshy 6 Standard Multi-Image task per object with four separate approved images:

- `ai_model: "meshy-6"`
- `should_texture: true`
- `enable_pbr: true`
- `texture_resolution: "4k"`
- `should_remesh: false`
- `image_enhancement: false` for clean reconciled inputs
- `remove_lighting: true`
- `pose_mode: ""`
- `target_formats: ["glb"]`

Do not enable Smart Topology, Auto Split, pose control, remesh or 8K on the recoverable
master path. Describe surface appearance in `texture_prompt`; keep geometry requirements
visible in the inputs. Prompt bounded neutral/team inlays, but do not mistake a prompt for
semantic runtime ownership.

Before submission, read the credential from the workstation Keychain/secret manager,
check the numeric balance and re-check Meshy's live API contract and price. Never emit the
credential. Immediately retain inputs, hashes, sanitized settings/response, task ID,
provider/model version, credits, license terms and every downloaded output under
`art/class-models/provider-runs/meshy/<look-id>/<task-id>/` plus durable artifact storage.

## 3. Audit and normalize

1. Reject wrong silhouette/body class before correction. Deterministic post-processing is
   not permission to redesign a failed provider result.
2. Inspect the actual nose axis. Provider results may reverse facing. Convert to `+X`
   facing and `+Y` up, center XZ, put the floor at `Y=0`, apply one recorded uniform scale
   to the approved planform span and remove cameras/lights/animations/skins.
3. Preserve geometry, UVs, normals, material graph and texture payloads when a JSON-only
   root transform suffices. Otherwise pin the Blender/equivalent version and exact script.
4. Record source/output SHA-256, bounds, planform span, facing decision and every deliberate
   local correction. Re-run strict views, top overlay and side/depth gates on the normalized
   master.

The normalized full-resolution master remains art-side. It is review authority and the
input to every runtime tier and later Retexture skin.

## 4. Establish team ownership

Prefer an authored separate material carrying `extras.nilbotsRole = "team-accent"`. For a
clean fused provider result, either:

- run the documented deterministic offline texture mask/face split while retaining hull
  PBR maps and exact geometry; or
- use an explicitly approved renderer-owned restrained chassis tint/rim treatment that
  preserves the provider maps and composes with hit, selection and fog effects.

Do not ship fixed provider cyan/orange as proof of team ownership. Validate both teams.
Projectiles use `extras.nilbotsRole = "projectile-mask"`.

## 5. Build runtime tiers

Use the native `gltfpack` 1.2 release, not its npm/WASM build: the native build is required
for WebP texture encoding. Pass its path explicitly and let the report record its version
and executable hash.

```sh
node scripts/class-models/optimize-static-model.mjs \
  --source <normalized-or-semantic-master.glb> \
  --look-id <look-id> \
  --output-dir sandbox/static-model-tiers/<look-id> \
  --gltfpack <native-gltfpack-1.2>
```

The default comparison ladder is deliberately below the first Striker/Fabricator trials:

| Tier | Triangles | PBR map limit | Purpose |
| --- | ---: | ---: | --- |
| `quality-reference` | 120k | 2048 | on-demand visual reference, not a mobile default |
| `mobile` | 60k | 1024 | conservative mobile candidate |
| `mobile-lean` | 30k | 1024 | geometry-loss isolation |
| `mobile-lite` | 15k | 512 | aggressive lower bound |

Add uncoupled candidates with repeated
`--tier <id>:<triangles>:<texture-limit>:<simplify-error>` when geometry and texture loss
must be separated; for example `30k/512` after the default ladder. Treat all numbers as
review hypotheses, not universal budgets.

The optimizer performs attribute-aware permissive simplification, cache/fetch optimization,
quantization, tangent generation, deterministic single-threaded embedded WebP encoding,
texture limiting and semantic-extra preservation. It deliberately does not emit
`EXT_meshopt_compression` until `lookModel.ts` owns and tests a Meshopt decoder. It runs each
tier twice and rejects hash drift, animation/skin/camera/light introduction, material/role
drift, excessive bound movement, over-budget triangles/maps and non-WebP runtime images.

Record for every candidate:

- GLB/texture transfer bytes and SHA-256;
- triangles, vertices, draw/material count and extensions;
- texture dimensions and compressed bytes;
- decoded RGBA bytes plus the mip-chain estimate;
- normalized world-bound drift and deterministic rerun result;
- validator output and first-use runtime evidence.

On-demand loading limits initial transfer; it does not reduce decode time or GPU memory once
three class families appear together. WebP normally becomes RGBA on the GPU, so compare
decoded texture+mipmap memory, not only the compact GLB. The target mobile product renders
3D, so these are shipping budgets now even while the current WebView remains Canvas2D during
the transition. Profile actual supported iOS and Android hardware, including sustained
thermal behavior and the maximum legal visible actor count. Keep the derived sprite as the
site, CLI, loading and render-failure fallback regardless.

## 6. Review tiers in the real renderer

Use one hash-pinned native replay, tick, actor, production camera, lighting and map theme for
the entire A/B. `capture-static-model-tiers.mjs` can substitute each candidate at an existing
production build's hashed GLB URL without editing renderer or replay bytes:

```sh
node scripts/class-models/capture-static-model-tiers.mjs \
  --spec sandbox/static-model-tiers/review-spec.json
```

Capture at desktop DPR 1 and phone-landscape DPR 2. Exercise multiple instances, every legal
heading, movement, low hover, shadow, firing/recoil, damage, destruction, fog, load failure
and 2D fallback. Confirm the exact candidate response and reject blank/unstable captures.
Select the smallest tier whose silhouette, broad material regions and readable surface
breakup survive the normal gameplay camera. Close turntables cannot justify a larger tier.

## 7. Install and derive

1. Copy only the accepted candidate to the manifest-discovered runtime package as
   `model.glb`; add/update `model3d.json`. Keep `+X` facing, `+Y` up, floor `Y=0`, no camera,
   light, floor, animation or rules data.
2. Run GLB contract/semantic tests, official Khronos validation, web tests/build, hosted
   asset emission check, mobile 3D device profiling and CLI size comparison. The model must
   remain absent from the self-contained Canvas2D CLI output.
3. Render the approved fixed-camera 2D derivatives from the accepted 3D master. Keep the
   current sprite until the owner approves those derivatives.
4. Retain alternate approved textures/skins as store bundles with stable IDs and entitlement
   catalog entries. Use Meshy Retexture or deterministic material replacement on the same
   master; repeat semantic, tier, both-team and runtime gates.

Default projectiles are separate objects/provider tasks, paired with the default class skins
in presentation but independently equipable. Apply the same authority, normalization,
semantic, optimization and runtime gates with projectile-scale triangle/texture candidates.
The renderer continues to own travel, spin, trail, glow and impact/miss effects.
