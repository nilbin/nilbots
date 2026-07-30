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

`run-meshy-retexture.mjs` spends the separate ten-credit texture gate only
after a Smart geometry task has passed local silhouette and mechanical review.
It pins Meshy 6, original UVs, PBR, 2K source textures, lighting removal, and
GLB output. Choose exactly one repository-local style PNG or one text prompt:

```sh
node scripts/class-models/run-meshy-retexture.mjs \
  --input-task-id <accepted-smart-task-id> \
  --look-id <look-id> \
  --purpose "<controlled texture gate>" \
  --image-style-url art/class-models/concept-targets/<candidate>/03-front-three-quarter.png \
  --execute
```

The retexture runner uses the same durable intent, lock, resume, sanitization,
download validation, and credit-ledger rules as the geometry runner. Run its
offline tests with:

```sh
node --test scripts/class-models/run-meshy-retexture.test.mjs
```
