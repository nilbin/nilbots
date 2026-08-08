# Meshy Striker image-enhancement A/B

- Provider: Meshy
- Task: `019fafe2-6a02-7048-b4f2-58be96b34423`
- Status: succeeded
- Credits: 30 (`1070` before, `1040` after)
- Submitted: 2026-07-29
- Purpose: same-input comparison with Meshy image enhancement enabled

This task reused the four `meshy-striker-v1` inputs from task
`019fafbb-27d8-7362-ad99-088ac067cbea`. The only source-processing change was
`image_enhancement: true`; the texture prompt was tightened to keep cyan inside
bounded inlays. Exact settings and input hashes are in `request-record.json`.

The credential came from macOS Keychain service `nilbots.meshy.api`. No
credential value, Authorization header, input data URI, or signed download
query is stored here.

## Raw output

The downloaded master is retained locally at `raw-model.glb` with SHA-256
`1ffa4868b74cff369b52ae2ad00c7ed4b7577092efa677c4b3d17858365819c9`.
It is 43,529,728 bytes and embeds its textures. The separately downloaded 4K
PBR maps are retained locally with hashes in `request-record.json`.

Provider masters are intentionally ignored by ordinary Git. Preserve them in
durable artifact storage before deleting this worktree.

## Measured geometry

- 1 mesh and 1 primitive
- 265,464 uploaded vertices
- 509,094 triangles
- 1 fused, double-sided material
- 4 embedded textures: base color, combined metallic/roughness, normal, and
  emission
- Local source spans approximately `1.899 × 1.058 × 0.724`
- Source is effectively Z-up, with the nose on `-X`

## Actual-game A/B verdict

The provider preview is slightly smoother and slimmer than the enhancement-off
result. In the real Frontline camera, however, the enhanced result is also
rounder and softer. It does not recover the missing thin side profile or
semantic team material, and it does not clearly improve class readability.

This A/B therefore does **not** justify another same-input provider call.
Meshy's current guidance still favors enhancement for AI-generated,
low-resolution, reflective, or shadowed inputs; the useful next experiment is
to improve the source views themselves and then measure one clean run.

The actual renderer proof also identified an unrelated bright front artifact:
the renderer's generic facing wedge was being drawn in front of an already
directional GLB. Authored GLBs should suppress that fallback-only cue.

## Runtime budget evidence

Deterministic normalization converts the raw provider coordinates to Nilbots
`+X` facing, `+Y` up, a one-tile maximum planform span, centered ground-plane
pivot, and floor at `Y=0`.

Measured derivatives:

| Source | GLB bytes | Triangles | Vertices | PBR maps |
| --- | ---: | ---: | ---: | ---: |
| Enhancement-off rich | 6,320,000 approx. | 149,953 | 85,218 | 2048² |
| Enhancement-off lean | 2,507,632 | 74,979 | 46,345 | 1024² |
| Enhancement-on lean | 2,471,936 | 76,364 | 46,241 | 1024² |

At the real 58-degree Frontline camera, the enhancement-off lean tier retained
nearly all visible detail from the rich tier and remained the sharper of the
two Meshy results. These are review derivatives, not approved runtime assets.

## Remaining production failures

- The source views were below Meshy's current recommended resolution and had
  inconsistent backgrounds, scale, shadows, and rear coverage.
- The top planform and side profile still require owner approval against the
  canonical concept.
- One fused material bakes cyan into the hull. Prompting can improve placement
  but cannot provide a semantic `team-accent` material.
- The optimized derivatives require a documented reproducible build recipe,
  semantic team mask/material, both-team review, and final owner approval
  before a runtime manifest may ship.
