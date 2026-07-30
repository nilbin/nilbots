# Meshy Striker clean-v2 enhancement-ON result

- Provider: Meshy
- Task: `019fb001-0806-763e-8d12-60f58ac6ab9e`
- Status: succeeded
- Credits: 30 (`1040` before, `1010` after)
- Submitted: 2026-07-30 local date
- Purpose: owner-approved clean-input Striker v2 body proof with image
  enhancement enabled

This task used the four clean 1254×1254 inputs and exact hashes recorded in
`request-record.json`. The credential came from macOS Keychain service
`nilbots.meshy.api`. No credential value, Authorization header, input data URI,
or signed download query is stored here.

## Verdict

Reject the geometry. Keep it only as surface/detail evidence.

Despite using the approved clean multiview set, image enhancement collapsed the
planform width to approximately `0.715` over a `1.899` length. After correcting
its rigid facing, the top silhouette reaches only `62.381%` IoU against the
canonical SVG. It reads as a narrow torpedo in the actual 58-degree game camera
and cannot be repaired by runtime scaling, texture work, or a width warp
without replacing the modeled form.

The same inputs with image enhancement disabled produced `95.669%` raw
planform IoU in task `019fb00c-9e8d-723b-9485-49706e307cb9`. That controlled
A/B pins enhancement OFF for this exact clean source set; it does not override
the provider's general enhancement guidance for noisy or photographic inputs.

## Raw output

The downloaded master is retained locally at `raw-model.glb` with SHA-256
`1ed650a12ae891722130dfa525867d07f160c297841872b57544dd29ec5cae85`.
It is 47,634,564 bytes and embeds four PBR textures. Separately downloaded maps
and their hashes are listed in `request-record.json`.

Provider masters are intentionally ignored by ordinary Git. Preserve them in
durable artifact storage before deleting this worktree.

## Measured geometry

- 1 mesh, 1 primitive, and 1 fused double-sided material
- 321,514 uploaded vertices
- 613,170 triangles
- source bounds approximately `1.899 × 0.715 × 0.693`
- source is Z-up with the nose on `-X`
- no semantic `team-accent` material

The lean and rich review derivatives are measured in
`geometry-report.json`. Neither is a runtime candidate.
