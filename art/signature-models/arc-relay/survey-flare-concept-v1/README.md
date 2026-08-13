# Survey Flare concept v1

Status: **owner-approved concept and unpromoted gameplay-review candidate** as
of 2026-08-13. Meshy generation, corrective matte retexture, normalization,
optimization, and desktop/phone WebGL lifecycle review are complete. It still
requires a separate promotion decision.

`survey-flare-six-view.png` proposes one rigid physical device for both public
phases: the renderer carries it horizontally during `in-flight`, then rotates
the same object upright when the flare becomes active. The object never opens,
deploys legs, or changes topology.

The model would replace only Survey Flare's procedural capsule, hub, bars, and
centre lens. The renderer continues to own the authoritative travel path,
radius-four scan footprint, horizon ring, calibration ticks, reveal truth,
suppression, timing, and expiry.

## Pinned concept contract

- Lantern-family heather/eggplant ceramic, aged ivory, dull brass, and charcoal.
- Broad circular optical iris at `+X`, readable at gameplay scale.
- Short weighted body with three flush stabilizer ribs.
- One connected watertight object; no rigging or model-owned animation.
- No teal/cyan or orange, which are team colours.
- No baked field, beam, trail, glow pool, shadow, or gameplay state.
- Must not read as a weapon, grenade, mine, Core, sentry, or objective beacon.

The normalized candidate is approximately `0.18` tile wide and `0.30` tile
long. The aspirational `2,000` triangle / `80 KB` target was not attainable
without losing the iris and rib silhouette: the gameplay-reviewed lean tier is
`361,444` bytes and `15,550` triangles with three embedded `256 px` WebP PBR
textures. It remains substantially smaller than a bot model.

## Provenance

- Generated with the built-in image-generation workflow on 2026-08-13.
- Exact prompt: `PROMPT.md`.
- Family reference: the approved Arc Lantern V3 top-down east render.
- Mechanics reference: Survey Flare's existing support-fields storyboard.
- PNG SHA-256: `cac7593d3d302bdea89578a192c4a4a376b314e14a239cceacc946c7dae050b6`.
- Meshy 7 corrective retexture task:
  `019ffbcd-1911-75e8-a822-90a439c88415` (10 credits, no rigging).
- Lean review artifact SHA-256:
  `0df64d292faff971e0b214883d68285112850eb71f43aa9f28c5b2d10f610ff7`.
- Gameplay evidence:
  `art/reviews/arc-relay-longshot-objectives-meshy7-v1/gameplay-review/`.
