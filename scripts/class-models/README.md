# Class-model authoring scripts

`extract-team-accent.mjs` converts a reviewed fused-material GLB into separate
hull and semantic team-accent primitives. It uses a lossless base-color plus
emission classifier, seeded connected-component filtering, two pixels of
guided growth, a one-pixel close, and a five-of-seven Dunavant interior UV
vote. It is an offline authoring tool, never a runtime hue-key shader.

Every run requires a tracked recipe and a disposable output directory:

```sh
node scripts/class-models/extract-team-accent.mjs <recipe.json> <output-directory>
node scripts/class-models/validate-team-accent.mjs <recipe.json> <output-directory>
```

The recipe pins source/output byte sizes and SHA-256 hashes. The extractor
fails on input or generated-output drift. The validator independently checks
the exact triangle partition, shared geometry, scene transforms, coordinate
bounds, material semantics, retained PBR payloads, generated artifacts, and
known tangent-space warnings.

The scripts use the `@napi-rs/canvas` version pinned by `web/package-lock.json`.
Restore provider masters and lossless maps from the project artifact store;
do not place credentials, signed URLs, or raw provider blobs in Git.

`run-meshy-smart-geometry.mjs` creates a paid, untextured Smart Topology T2
Image-to-3D task only when passed `--execute`. It accepts a real PNG/JPEG under
`art/class-models/concept-targets/`, requires at least 1040 pixels on both
axes, reads the API credential from macOS Login Keychain, and pins the current
five-credit geometry request.

The runner writes an atomic pre-submit intent before contacting Meshy and
serializes paid calls with
`art/class-models/provider-runs/meshy/.meshy-paid-call.lock`. After Meshy
returns a task ID, it persists that ID immediately, polls only the five
documented statuses, and archives the GLB and previews through HTTPS
host-checking, bounded streaming, signature/GLB-length validation, SHA-256,
and an atomic final rename. The committed records contain no credential,
input data URI, signed query, or provider account path.

```sh
node scripts/class-models/run-meshy-smart-geometry.mjs \
  --input art/class-models/concept-targets/<candidate>/03-front-three-quarter.png \
  --look-id <look-id> \
  --purpose "<controlled experiment>" \
  --target-polycount 4000 \
  --execute
```

Run it without `--execute` first to review the exact sanitized request. The
documented public API accepts one image for Smart T2; do not pass a contact
sheet or invent a multi-image Smart payload.

If polling, balance recording, or artifact download is interrupted after a
task ID exists, resume that task instead of creating another paid task:

```sh
node scripts/class-models/run-meshy-smart-geometry.mjs \
  --resume <task-id> \
  --look-id <look-id> \
  --execute
```

The runner never retries a create POST. A transport failure or 5xx during
submission leaves the intent as `SUBMISSION_UNKNOWN` and deliberately retains
the paid-call lock. Inspect the intent, Meshy's recent tasks, and the account
balance before manually clearing that exact lock directory. A `FAILED` task
is recorded with Meshy's documented refund; `CANCELED` refund behavior is
recorded as unknown. Neither status is presented as resumable.

Run the provider-runner tests offline:

```sh
node --test scripts/class-models/run-meshy-smart-geometry.test.mjs
```

`run-meshy-retexture.mjs` spends the separate ten-credit texture gate on
either an accepted provider task or an approved, deterministic Blender GLB.
It pins Meshy 6, original authored UVs, PBR, 2K source textures, lighting
removal, and GLB output. Choose exactly one model source and exactly one
repository-local style PNG or text prompt:

```sh
node scripts/class-models/run-meshy-retexture.mjs \
  --input-model art/class-models/<approved-authored-model>.glb \
  --look-id <look-id> \
  --purpose "<controlled texture gate>" \
  --image-style-url art/class-models/concept-targets/<candidate>/03-front-three-quarter.png \
  --execute
```

The retexture runner uses the same durable intent, lock, resume, sanitization,
download validation, and credit-ledger rules as the geometry runner. Uploaded
models must be repository-relative GLB v2 files no larger than 32 MiB. The
runner records their path, size, and hash, but creates the base64 request value
only for the POST and never writes it to an intent, record, or log. Run its
offline tests with:

```sh
node --test scripts/class-models/run-meshy-retexture.test.mjs
```

## Bulwark semantic team-accent postprocess

`add_bulwark_team_accents.py` is the offline Blender stage after the reviewed
retextured semantic-rig build. It does not call Meshy or any other provider.
The public CLI accepts repository-relative paths only, requires the exact
source `.blend` SHA-256, resolves the shared pinned Blender 5.2 toolchain, and
refuses to overwrite an output directory:

```sh
python3 scripts/class-models/add_bulwark_team_accents.py \
  --input-blend sandbox/<semantic-rig>/aegis-retextured-semantic-rig.blend \
  --expected-source-sha256 <exact-lowercase-sha256> \
  --output-dir sandbox/<new-team-accent-proof>
```

The stage adds four small, explicit chamfered emitter bars: two slim forward
shield strips and two compact rear/outer pod strips. Each bar uses one
center-ray surface fit and a clean tangent-plane solid; no independently
projected patch vertices or inferred broad paint are created. The objects are
direct children of `ShieldTrim_North_Pivot`, `ShieldLobe_South_Pivot`,
`Pod_NorthOuter_Pivot`, and `Pod_SouthOuter_Pivot`, so the existing
`PodPlant_HoverSettle` clip remains coherent without adding keys.

All four meshes share exactly one neutral, texture-free Principled material.
The material, each mesh, and each exported GLB primitive carry
`nilbotsRole=team-accent`. Blender 5.2 does not emit primitive extras, so the
stage deterministically patches only the GLB JSON chunk, preserves its BIN
payload, and then validates chunk bounds, references, role counts, neutral
factors, texture payload hashes, body triangle count, parent nodes, and both
animation channel sets.

Every successful run writes:

- an editable neutral `.blend` and runtime-neutral `.glb`;
- neutral, blue, and orange fixed top/gameplay proofs;
- separate body/accent top masks and a numeric projected-area gate;
- a 512×512 transparent, +X/East-facing top/avatar proof;
- a separate lossless RGBA `team-accent` semantic mask;
- a sorted, repository-relative `report.json` with exact artifact hashes.

The blue/orange colours exist only in proof renders. The GLB retains equal
neutral base/emissive factors for runtime recolouring. PBR top/avatar proofs
use a recorded 0.034-degree camera bias to resolve exact source depth ties
without a perceptible presentation-angle change; exact +Z masks remain
unbiased. The editable `.blend` records its save location, so compare the GLB,
masks, and proof payloads—not `.blend` bytes—across different output
directories. Run the wrapper tests without Blender or network access:

```sh
python3 -m unittest scripts/class-models/test_add_bulwark_team_accents.py
```

## Bulwark runtime tier packager

`package_bulwark_runtime.py` is the offline stage after an accented Mobile
`.blend` is approved. It accepts only repository-relative paths, verifies the
exact source SHA-256 before Blender starts, resolves pinned Blender 5.2, and
refuses to overwrite an output directory:

```sh
python3 scripts/class-models/package_bulwark_runtime.py \
  --input-blend sandbox/<approved-accent>/bulwark-mobile-team-accent.blend \
  --expected-source-sha256 <exact-lowercase-sha256> \
  --output-dir sandbox/<new-runtime-tier-proof>
```

One run exports 2K source, 1K runtime, and 512 comparison GLBs from the same
frame-1 bind pose. Every derivative is `+X` facing, `+Y` up, floor-aligned at
`Y=0`, and contains no camera, light, or animation. The gate preserves all
nine semantic body groups, all body triangles and PBR roles, the four rigid
emitter bars, and exactly one texture-free
`extras.nilbotsRole=team-accent` material. Mesh and primitive role extras are
restored deterministically after Blender export.

The output includes neutral/blue/orange fixed top and 58-degree gameplay
renders at 512, 140, and 41 pixels, cross-tier contact sheets, and a
hash-pinned `report.json`. Choose the smallest tier whose PBR hierarchy and
team bars survive those fixed gameplay views; retain 2K as source art. Run
the wrapper boundary tests without Blender or network access:

```sh
python3 -m unittest scripts/class-models/test_package_bulwark_runtime.py
```
