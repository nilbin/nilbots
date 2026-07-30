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

## Meshy T2 Smart Topology experiment

`meshy_smart_topology.py` is a separate, narrower Image-to-3D experiment
runner. It has two named owner-approved Aegis Tortoise Turret inputs:
`oblique` (B1) and `top` (B2). It validates the selected input's exact
repository path, 1254×1254 PNG header, and SHA-256. A new invocation requires
an explicit trial, is offline without `--submit`, and does not read Keychain,
build a data URI, or make a network request:

```sh
python3 scripts/class-models/meshy_smart_topology.py --trial oblique
python3 scripts/class-models/meshy_smart_topology.py --trial top
```

The fixed request pins Meshy T2 Smart Topology, 15,000 target polygons, PBR 4K
texturing, the existing Turret texture prompt byte-for-byte, and GLB output.
The T2-unsupported `image_enhancement` and `remove_lighting` fields are omitted.
The succeeded B1 oblique and B2 top records remain resumable without another
POST. Their original submission shape required the named trial and both
explicit paid flags:

```sh
python3 scripts/class-models/meshy_smart_topology.py \
  --trial top --submit --max-credits 15
```

The committed ledger caps the experiment at three calls/45 credits and rejects
a duplicate named trial. B1 and B2 consumed two calls/30 credits; the third
call remains unspent. B1 is retained as a visually liked alternate/future
store-skin candidate, but no store wiring occurs here. Its raw provider model's
206 unnamed welded components were not assumed to be parts: the separate
`t2-semantic-grouping` deterministic Blender experiment measures and assigns
them once to a hub plus four rigid arms, preserves every textured source
triangle, and exports real `Turret_Deploy`/`Turret_Recoil` clips. B2 required a
recorded deterministic axis correction before fixed-view review. Sanitized run
records and provider previews go under
`art/class-models/topology-experiments/runs/meshy/<task-id>/`; the raw GLB and
separately returned PBR maps go only under ignored
`var/class-models/topology-experiments/meshy/<task-id>/`, with paths, sizes, and
hashes retained in provenance. A recorded task is resumable with
`--resume-run`.

Run its offline guardrail suite with:

```sh
python3 scripts/class-models/test_meshy_smart_topology.py
```
