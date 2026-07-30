# Aegis Tortoise mobile chassis correction

Status: **strong art-side candidate; intentionally not a runtime asset**.

This pass preserves the accepted Meshy upper carapace, removes the fused lower
slab, and reconstructs a separate lower chassis, embedded cannon, and four
short articulated legs. It is the mobile Bulwark body only. The deployed
turret and shell remain separate models.

The tracked lean GLB is a handoff candidate, not something the web loader
discovers. Nothing from this directory is copied into `web/src/assets`.

## Review result

The fixed East-facing comparisons use rigid same-scale mask normalization:
uniform scale and translation only, with no rotation, reflection, or
non-uniform warp.

| Gate | Result | Verdict |
| --- | ---: | --- |
| Rich 2D top silhouette IoU | `95.687%` | pass |
| Rich 2D top outer IoU | `95.687%` | pass |
| Approved inferred side IoU | `78.124%` | pass |
| Approved inferred side outer IoU | `79.840%` | pass |
| Side aspect drift from `3.345` target | `0.178%` | pass |

The top/gameplay identity is strong. The reconstructed side fixes the
provider's overly thick body and collapsed pseudo-feet, but the foot pads and
joints remain simpler and visually thinner than the approved inferred side.
That limitation is visible in the review board and is not averaged away by
the passing top score.

The three bounded semantic team panels switch between blue and red in the
review. The long forward slits and core remain fixed cyan class energy. The
panels are deliberately small enough not to redesign the approved carapace,
so live team readability also depends on the existing renderer-owned floor
glow.

## Authored construction

- Meshy task: `019fb1d5-0c4f-7716-8b86-048ad5d8b066`.
- Pinned raw GLB SHA-256:
  `dcb22bd3975bb767c5206a92918a59d6ef92cb338e7bad8463c2040edf274534`.
- Pinned rich 2D authority SHA-256:
  `22e41a0925f1b88497ff55265bebb249b21da004c9a2559a5f1d0f70db95fbd5`.
- Provider conversion: mesh-space `X +90°`, then mesh-space `Z +180°`;
  uniform planform scale to `1.12` tiles; no global vertical or non-uniform
  warp.
- Provider lower cut plane: Blender `Z=0.118`; rebuilt ring, belly, side
  skirt, cannon, hip guards, joints, feet, and toe armor are named separate
  rigid parts.
- Rig: `Aegis.Tortoise.Rig`, 15 named bones with `root`, `body`, `cannon`,
  and hip/knee/ankle pivots for all four corners.
- Actions: `Aegis_Idle` frames `1–49` and `Aegis_Walk` frames `1–25`.
  The walk is an in-place heavy diagonal gait: FL+RR oppose FR+RL while the
  carapace stays nearly level.
- Team material: exactly one `NB_TEAM_ACCENT` with
  `extras.nilbotsRole = "team-accent"`, used by exactly three semantic inlay
  nodes.

The recipe strips unused UV attributes from the flat-color procedural parts
and canonicalizes retained `TEXCOORD_0` values to six decimal places. This
removes Blender's one-ULP export noise while moving a 2K texture sample by no
more than `0.002` pixels.

## Candidate budgets

| Tier | Bytes | Triangles | Blender vertices | Provider maps |
| --- | ---: | ---: | ---: | --- |
| Lean handoff candidate | `6,346,356` | `99,343` | `57,714` | base/normal 1K; MR/emission 512 |
| Rich source/review tier | `22,217,724` | `228,266` | `122,798` | base/normal 2K; MR/emission 1K |

Only the lean GLB is retained. The editable Blend file contains the rich
source state, packed textures, both actions, and named pivots. The rich GLB is
reproducible from that recipe but is not stored as another binary.

## Runtime landing gates

The current replay renderer loads a GLTF scene as a static object. It does not
select `Aegis_Idle`/`Aegis_Walk` or seek a clip from normalized replay time.
Landing this walking chassis before that support exists would make the
articulated silhouette slide across the arena. The next renderer pass must:

1. select the locomotion state deterministically;
2. seek animation time from replay state rather than wall-clock time;
3. preserve pause, scrub, replay determinism, and multiple-instance behavior;
4. rerun both-team, every-heading, wall-clearance, fallback, and normal-zoom
   review.

The candidate also has 49 meshes/primitives, which is too fragmented to call
runtime-optimized without a measured draw-call pass. Its one provider
normal-mapped primitive has no authored tangent attribute. Three.js can
generate the target basis, but broader renderer portability requires authored
tangents or an explicit acceptance of that warning. Khronos glTF Validator
reports zero errors, one generated-tangent warning, and one informational
unused-skin notice; the model uses rigid node parenting rather than
vertex-weighted skinning.

## Reproduce and validate

Use Blender `5.2.0 LTS` (`fbe6228777e7`) and a directory containing the
pinned Meshy task files named in `build_aegis_tortoise.py`.

```sh
BLENDER=/Users/sebastian.lind/hobby-projects/nilbots/var/toolchains/Blender-5.2.0.app/Contents/MacOS/Blender
PROVIDER=/Users/sebastian.lind/hobby-projects/nilbots/var/model-artifacts/meshy/aegis-tortoise-mobile/019fb1d5-0c4f-7716-8b86-048ad5d8b066

"$BLENDER" --background --factory-startup \
  --python art/class-models/aegis-tortoise-mobile/build_aegis_tortoise.py -- \
  --provider-dir "$PROVIDER" \
  --output-dir sandbox/aegis-tortoise-build

npm ci --prefix web
node art/class-models/aegis-tortoise-mobile/audit_aegis_tortoise.mjs \
  --build-dir sandbox/aegis-tortoise-build \
  --output-dir sandbox/aegis-tortoise-audit
node art/class-models/aegis-tortoise-mobile/validate_aegis_glb.mjs \
  --glb sandbox/aegis-tortoise-build/aegis-tortoise-lean.glb \
  --output sandbox/aegis-tortoise-audit/validation-report.json
```

`build-report.json` records exact source hashes, geometry, texture tiers,
actions, and artifact hashes. `determinism-report.json` records exact matching
rich and lean hashes from two independent Blender invocations.
`audit-report.json` records the fidelity gates. `validation-report.json`
records the semantic/rig structure and tangent warning.
`khronos-validation-report.json` is the independent Khronos glTF Validator
result. The fixed authority, team/tier, and walk-cycle boards under `review/`
are the retained visual evidence.
