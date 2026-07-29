# Codex handover: class-model vertical slice (2026-07-29)

Branch: `codex/class-models`

Implementation commit: `828d445` (`Prepare the class model vertical slice`)

This branch already contains merge commit `35d61fb`, which brought
`agent/frontline-duel-depth` tip `46c1ee4` into the worktree before the
renderer changes. It therefore includes the follow camera, arrival, stance,
and deflection work named in the original class-work handover.

## Land window

This is presentation-only and has no replay, Engine, SDK, API, or database
schema change. It can merge through the normal Fable merge-and-gauntlet flow.
No DECISIONS number or CLI version was minted; apply any required viewer
compatibility bump during integration as directed by the parent handover.

## What is ready

- The approved Striker oblique target and four-view sheet are preserved under
  `art/class-models/concept-targets/`, with the canonical SVG raster and pinned
  hashes.
- `meshy-striker-v1/` contains four separate, lossless upload views and the
  exact Meshy 6 first-proof settings. This is an art-side handoff pack, not a
  runtime package.
- No class bot or projectile GLB is approved. There are zero `model.glb` or
  `model3d.json` files in the class runtime packages, and both hosted and CLI
  builds emit no GLB.
- `lookModel.ts` adds renderer-only, manifest-driven, on-demand GLB discovery,
  actor-local materials, cached geometry, semantic team paint, and a safe
  fallback to the existing SVG-derived solid. With no manifests, every current
  look follows the fallback path.
- Striker and its volley stance declare `locomotionCue: "low-hover"` in their
  canonical `look.json`. The rendered body gets a shallow air gap and bob while
  its authoritative gameplay position and layer remain unchanged. This cue is
  independent of whether a GLB exists.
- The Timeline now gives `projectile-deflected` its own defender-lane diamond,
  completing the additional renderer item in the class-work handover.
- The visual skill and arena documentation define concept-fidelity gates,
  optional bot/projectile/environment companions, on-demand budgets, semantic
  team materials, and the Meshy-to-human route as an **unproven Striker-only
  vertical slice**.

## Intentionally absent

- The rejected procedural Blender GLBs, `.blend` files, textures, previews,
  reports, and generators are not retained. A compact measured rejection record
  remains in `art/class-models/README.md`.
- No generated Meshy output has been accepted, cleaned, or placed in runtime.
- No mobile renderer work is included. Canvas2D, mobile, site, loading, and CLI
  continue to use the canonical 2D assets.

## Next proof

Run one Meshy 6 Standard multi-view job for Striker with texture, 4K PBR, image
enhancement off, lighting removal on, and remesh/Smart Topology/Auto Split off.
Preserve the raw output, provider/model version, settings, credits, and license.

Treat the result only as a base mesh. A human modeler or technical artist must
then correct the exact top silhouette and proportions, retopologize, repair
UV/PBR maps, isolate the semantic team material, set Nilbots axes/scale/pivots
and hover clearance, and validate both team colors in the real gameplay camera.
Do not add a runtime manifest until that end-to-end Striker proof is approved.
Generate the projectile in a separate job; all multi-view images in one task
must depict the same object.

## Verification

- `npm test` — 281/281 passed.
- `npx tsc -b` — passed.
- `npm run build` — passed, including four theme-scoped CLI viewers and hosted
  production output.
- Hosted and CLI outputs contain zero `.glb` files.
- `quick_validate.py .claude/skills/nilbots-visual-assets` — passed.
- `git diff --check` — passed.
