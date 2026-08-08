# Meshy Striker clean-v2 enhancement-OFF result

- Provider: Meshy
- Task: `019fb00c-9e8d-723b-9485-49706e307cb9`
- Status: succeeded
- Credits: 30 (`1010` before, `980` after)
- Submitted: 2026-07-30 local date
- Purpose: controlled same-input Striker v2 A/B with image enhancement
  disabled

This task reused byte-identical copies of the four owner-approved clean
1254×1254 inputs from task `019fb001-0806-763e-8d12-60f58ac6ab9e`. The only
request change was `image_enhancement: false`. Exact settings and hashes are in
`request-record.json`.

The credential came from macOS Keychain service `nilbots.meshy.api`. No
credential value, Authorization header, input data URI, or signed download
query is stored here.

## Owner verdict

The geometry was accepted on 2026-07-30 as the Striker runtime base. The
deterministic semantic team-accent split and actual blue/orange replay gates
subsequently passed.

The owner explicitly accepts one deviation: the strict side is taller, more
rectilinear, and less continuously tapered than the approved thin-skimmer
concept. Do not spend another provider call trying to repair it. Any later
profile cleanup belongs to authored modeling and rebaking.

## Measured fidelity

The provider result arrived Z-up with its nose on `-X`. A documented rigid
normalization rotates it to Nilbots `+X` facing, converts it to `+Y` up,
centers the planform, puts its floor at `Y=0`, and scales its maximum planform
span to a recorded target. Fidelity measurements use the one-tile derivative;
the accepted larger runtime derivative has an authored `1.12`-tile span.

- raw canonical top IoU after facing correction: `95.669%`
- lean canonical top IoU: `95.673%`
- rich canonical top IoU: `95.666%`
- raw-to-lean silhouette IoU: `99.952%`
- raw-to-rich silhouette IoU: `99.986%`
- lean area difference from canonical: `-0.207%`

The output is closer to the canonical top planform than the approved input's
`94.848%` IoU. Residual width drift is localized and does not justify a
nonlinear warp.

## Raw output

The downloaded master is retained locally at `raw-model.glb` with SHA-256
`7e7d71bad742551731673f167ad34c44a89f775eb6a29e16772eb54b4ad6db66`.
It is 45,492,852 bytes and embeds four PBR textures. Separately downloaded maps
and their hashes are listed in `request-record.json`.

Provider masters are intentionally ignored by ordinary Git. Preserve them in
durable artifact storage before deleting this worktree.

## Runtime-tier evidence

| Tier | GLB bytes | Triangles | Vertices | Maps | Top IoU |
| --- | ---: | ---: | ---: | ---: | ---: |
| Lean | 2,813,112 | 88,989 | 54,400 | 1024² | 95.673% |
| Rich | 7,072,728 | 177,976 | 100,427 | 2048² | 95.666% |

At the real 58-degree game camera the rich tier does not provide a meaningful
planform or class-read improvement over lean. The lean geometry is the base
candidate. These source tiers retain one fused material; only the reviewed
semantic derivative below is eligible for runtime use.

## Accepted semantic runtime derivative

The tracked recipe under `team-accent/` deterministically separates the fused
cyan paint into one renderer-owned material while preserving geometry, UVs,
scene transforms, facing, and the exact triangle multiset. A fresh bake is
byte-identical to the staged runtime asset:

- GLB bytes: `4,588,820`;
- triangles: `88,989` (`83,825` hull + `5,164` team accent);
- SHA-256:
  `17c1998906729ebe70d8653c8b18b588ac3acfbeb521698737a266279147c07e`;
- structural validation: `18/18`;
- Khronos validation: zero errors and two explicitly recorded
  `MESH_PRIMITIVE_GENERATED_TANGENT_SPACE` warnings.

Three.js generated-tangent rendering passed close and real-replay blue/orange
review. The warning is accepted only for the current target renderer; a future
renderer must generate or receive tangents rather than treating the asset as
universally portable.

The authored `1.12` span is subsequently multiplied by the current Striker
look's `1.062` actor size, producing a `1.18944`-tile live span. Frontline wall
clearance is audited against the live, rotation-safe mesh envelope. Cosmetic
idle/recoil translation beside walls is a separate actor-owned follow-up.
