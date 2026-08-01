# Arc Relay retained artifacts

Arc Relay Gate 3 freezes live here. Directories are append-only after their
manifest hashes are opened to evaluation.

- `stock-mind-v0/` — fixed stock execution engine plus one provisional
  evaluation sheet.
- `native-cohort-v1-2026-08-01/` — four independently authored native minds,
  their source/WASM revisions, sheets, DX notes, manifest, and durable match
  records/broadcast slices.

Canonical replays are gzip evaluation scratch only. They are verified and
deleted after scorecard extraction. A durable game contains only a match
record (at most 4 KiB) and a broadcast slice (at most 300 KiB gzip).
