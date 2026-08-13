# Team Reactor recess concept v1

Status: **owner approved and promoted**. The owner-approved concept produced a
successful low open-ring geometry pass and one bounded corrective retexture. It
replaces the rejected raised U-station direction with a very flat floor socket
whose physical housing sits on the arena surface as a shallow recess.

## Contract

- Physical asset: one neutral, rigid, matte octagonal socket with a broad pale rim,
  dark central aperture, three empty top-facing state bays, and three muted clamps.
- Renderer: team color and crest, exact charge and integrity state, ownership ring,
  banking pulse, damage, and destruction.
- Runtime placement: normalize to roughly `1.4–1.5` tiles across, then sink the
  insert until only its thin rim and central aperture sit above the floor.
- The floor itself is never part of the GLB. Collision and rules remain unchanged.

The six-view sheet is concept evidence. Four deterministic separate square Meshy
inputs—strict front, strict right, strict top, and front-right three-quarter—live
under `meshy7-inputs-v1/`. The contact sheet itself is never submitted.

## Meshy result

- Geometry task: `019ffcc4-f21b-79f7-b6f4-2ecb9eed441a` (`30` credits).
- Corrective multiview retexture: `019ffccf-7594-7de3-a30f-603697cf63a2`
  (`10` credits).
- Exact first-pass geometry/UVs are retained. Only the corrected neutral PBR maps
  are transplanted, because Meshy's retexture output performed a small uniform
  provider rewrite.
- The review candidate is normalized to `1.5` tiles across and `0.17` tile high,
  with a deterministic `0.82` roughness floor.
- Lean review tier: `1,635,852` bytes, `79,318` triangles, three `512`-pixel WebP
  maps. Rich review tier: `4,148,132` bytes, `158,644` triangles, three
  `1024`-pixel WebP maps. Fixed-camera evidence shows no meaningful gameplay-scale
  benefit from the rich tier, so lean is the recommended in-game trial candidate.
- Team color, charge/integrity state, crest, pulses, damage, and destruction remain
  renderer-owned and are not baked into the GLB.
- Runtime promotion is deterministic through
  `scripts/class-models/promote-reactor-recess-v1.mjs`; the emitted asset is
  `web/src/assets/objective-models/arc-team-reactor/model.glb`.

Review evidence:

- `art/reviews/arc-relay-reactor-recess-meshy7-v1/retexture-review/`
- `art/reviews/arc-relay-reactor-recess-meshy7-v1/lean-review/`
- `art/reviews/arc-relay-reactor-recess-meshy7-v1/rich-review/`
- `art/class-models/runtime-tiers/arc-relay/reactor-recess-review-v1/audit.json`

## Provenance

- Generated with the built-in image-generation workflow on 2026-08-13.
- Source: `/Users/sebastian.lind/.codex/generated_images/019fee2d-edd3-7723-8309-6a84e5627578/exec-089a7750-6251-4711-b5f4-06e2306ac124.png`
- Project SHA-256: `5e869249f8a18c7322f6d852160c3040803c7679d02336fba8e531d56fc72844`
- Exact prompt: `PROMPT.md`
