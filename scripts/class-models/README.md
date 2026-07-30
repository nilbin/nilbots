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
