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

## Meshy multiview runner

`meshy_multi_image.py` consumes one explicit package manifest under
`art/class-models/provider-inputs/meshy/<asset-id>/manifest.json`. Its default
mode is an offline validation: it does not read Keychain or contact Meshy.

```sh
python3 scripts/class-models/meshy_multi_image.py \
  --package art/class-models/provider-inputs/meshy/<asset-id>/manifest.json
```

The paid path requires all package blockers to be cleared,
`providerSubmission.allowed: true`, a purpose, a texture prompt, and
`expectedCredits: 30`. It pins Meshy 6, clean-input enhancement off, PBR 4K,
lighting removal, no remesh, and GLB-only output. Both the paid flag and the
exact current ceiling are mandatory:

```sh
python3 scripts/class-models/meshy_multi_image.py \
  --package art/class-models/provider-inputs/meshy/<asset-id>/manifest.json \
  --submit --max-credits 30
```

The runner checks balance before creation, records the task immediately,
polls to a terminal status, downloads every expiring GLB/preview/PBR output,
and writes sanitized provenance beneath
`art/class-models/provider-runs/meshy/<asset-id>/<task-id>/`. A timed-out or
interrupted recorded task can be resumed without another paid request:

```sh
python3 scripts/class-models/meshy_multi_image.py \
  --resume-run art/class-models/provider-runs/meshy/<asset-id>/<task-id>
```

The credential is read only inside submit/resume execution from Login
Keychain service `nilbots.meshy.api`. The script never prints or records the
credential, Authorization header, request data URIs, or signed URL queries.
Meshy currently documents a textured Meshy-6 multiview task as 30 credits but
does not expose a pre-submit quote endpoint. The runner therefore refuses any
other configured ceiling and post-checks `consumed_credits`; re-check the
official pricing page before approving a package.

Run the focused offline checks with:

```sh
python3 scripts/class-models/test_meshy_multi_image.py
```
