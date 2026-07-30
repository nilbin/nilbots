# Meshy Bulwark Smart T2 4k geometry gate

- Provider: Meshy
- Task: `019fb418-41b0-7a1b-a888-1e2f4756c64b`
- Status: succeeded
- Credits: 5 (`710` before, `705` after)
- Submitted: 2026-07-30
- Purpose: untextured, single-image Smart Topology T2 geometry gate for
  Bulwark Mobile

This task used the approved elevated front-three-quarter concept as the only
provider input. The request deliberately stopped at geometry so a failed mesh
would cost five credits rather than fifteen. Exact request settings, input
hash, task metadata, and downloaded artifact hashes are recorded beside this
file. The API credential, Authorization header, input data URI, and signed
download queries are not stored.

## Verdict

Rejected for production and texturing.

The provider preview makes the overall fortress silhouette appear plausible,
but deterministic Blender 5.2 fixed-view inspection exposes malformed floating
rear pieces, an incoherent underside, fused functional boundaries, and weak
agreement between top, side, and rear views. It is not a viable base for the
four hover/lock pods or reversible Mobile-to-Shell/Tower mechanics.

The raw result contains 3,959 triangles in one mesh, but that mesh breaks into
105 indexed connected components and 95 components after a one-microunit weld.
That is provider fragmentation, not useful mechanical part segmentation. The
height-to-planform ratio is `0.245605469`. Its best unselected quarter-turn
top silhouette reaches only `81.71%` IoU against the canonical concept.

Do not retexture, rig, optimize, or ship this task. Retain it as negative
evidence for the controlled 4k-versus-15k comparison.

## Review evidence

The `audit/` directory contains pinned-camera top, side, front-three-quarter,
and rear-three-quarter renders. The compact structural measurements are in
`geometry-report.json`.

The next controlled comparison keeps the input and all settings fixed while
raising `target_polycount` to 15,000. A separate strict-top 15,000-triangle
probe tests whether planform authority is better preserved when depth is left
to Blender rather than inferred from the oblique image. Neither candidate
earns texture credits unless it preserves the four pods, weapon notch,
fortress planform, coherent side profile, and usable rigid islands.

## Raw output

The ignored provider master is retained locally as `raw-model.glb`:

- bytes: `75,800`
- SHA-256:
  `123bce9d515e162c9d1cd96da52d58346fa9ddf93e9dc891a3d280011ffe0a36`

Meshy API assets expire; the local master and compact provenance are the
durable record.
