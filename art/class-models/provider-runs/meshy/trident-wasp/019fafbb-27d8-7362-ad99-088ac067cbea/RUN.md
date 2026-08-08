# Meshy Striker base-mesh proof

- Provider: Meshy
- Task: `019fafbb-27d8-7362-ad99-088ac067cbea`
- Status: succeeded
- Credits: 30 (`1100` before, `1070` after)
- Submitted: 2026-07-29
- Purpose: unapproved Striker multiview base-mesh proof

The four separate inputs and exact request settings are recorded in
`request-record.json`. The credential came from macOS Keychain service
`nilbots.meshy.api`; no credential value, Authorization header, input data URI,
or signed download query is stored here.

## Raw output

The downloaded master is retained locally at `raw-model.glb` with SHA-256
`c7f16f1f0fa1bd2207906b7d028297ed677e102ea5f48dcdf05b018b58f64436`.
It is 43,310,652 bytes and embeds its textures. The separately downloaded 4K
base-color, metallic, roughness, normal, and emission maps are also retained
locally with hashes in `request-record.json`.

Those 107 MB of provider masters are intentionally ignored by ordinary Git.
The committed evidence contains the provider preview, deterministic neutral
renders, task/request metadata, geometry measurements, and master hashes. Move
the ignored masters into durable artifact storage before deleting this
worktree; do not substitute the expiring provider URLs.

## Measured geometry

- 1 mesh and 1 primitive
- 245,924 vertices
- 468,626 triangles
- 1 fused, double-sided material
- 4 embedded textures: base color, combined metallic/roughness, normal, and
  emission
- Local source spans approximately `1.899 × 1.058 × 0.733`
- Source is effectively Z-up and needs conversion to the Nilbots `+Y`-up
  contract

## Review verdict

This result validates Meshy as a potentially useful **base-mesh** route, not as
a one-click runtime pipeline.

Passes:

- top-view class identity is substantially closer to the approved concept than
  the rejected procedural proof;
- graphite/bronze grouping, cyan inlays, amber core, panel texture, PBR response,
  and emission survived into the actual GLB;
- the model has genuine three-dimensional recesses and underside volume.

Fails:

- the top silhouette still drifts from the canonical SVG;
- side and underside inference is too bulbous and layered compared with the
  approved thin skimmer profile;
- 468k triangles and 43.3 MB are far above runtime budgets;
- one fused material bakes team cyan into the hull and cannot yet accept
  renderer-owned team color;
- axes, scale, pivot, hover clearance, topology, UV/material segmentation, and
  gameplay-camera presentation are not production-ready.

## Required handoff

A hard-surface modeler or technical artist should use the raw model and maps as
reference/source material, then:

1. correct the top silhouette against the canonical SVG;
2. rebuild the side profile into one thin continuous skimmer hull;
3. retopologize to measured gameplay tiers;
4. preserve or rebake the approved PBR detail;
5. split cyan surfaces into the semantic team-accent material while retaining
   amber engine/core emission;
6. convert to `+X` facing, `+Y` up, Nilbots scale, centered pivot, and a shallow
   hover gap;
7. validate both team colors and the SVG fallback in the real Frontline camera.

Do not generate the projectile from this task. Its head is a separate object
and requires a separate proof only after the Striker body route is accepted.
