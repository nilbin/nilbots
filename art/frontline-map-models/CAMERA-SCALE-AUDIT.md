# Frontline camera-scale audit

## Verdict

Keep two visibly different camera promises:

- **Normal follow:** an action-follow camera with a maximum **18-tile
  world-space horizontal span**, once it has a semantic action anchor.
- **Fit:** the explicit full-arena frame, unchanged.

Do not express the limit as a maximum DOM viewport size. A smaller canvas
throws pixels away and changes nothing about how much world the camera tries
to show. A world-space span produces the same composition in both renderers
and lets CSS/device pixels improve naturally on larger displays.

This branch makes no camera code change. Fable's camera files remain the
integration authority.

## What exists now

Fable's shared `arenaCamera.ts` already has the correct architectural split:

- `focusFrame` computes a truthful adaptive box for the supplied lives;
- `ArenaCamera` adds a deadband and critically damped movement;
- a selection follows that unit's team rather than one isolated chassis;
- `showEverything` is the explicit whole-arena Fit state;
- manual zoom is bounded between 8 tiles and the full arena;
- both Canvas2D and Three.js consume the same world-space frame;
- the Three.js camera remains 42-degree FOV at the exact 58-degree pitch.

The limitation is semantic, not mathematical: with no selection,
`focusPointsAt` supplies every active actor. When opposing teams are far
apart, honest fitting necessarily approaches the whole-arena scale.

For Frontline at a 1.6 aspect ratio:

```text
whole Fit width = max(23 + 0.4, (15 + 0.4) × 1.6) = 24.64 tiles
opening spawn fit = 18 centre separation + 2 × 2.6 margin = 23.20 tiles
```

## Legibility comparison

Approximate on-screen width of a 1.12-tile Striker is:

```text
viewport width × 1.12 / camera world span
```

| Arena viewport width | Whole Fit 24.64 | Current opening fit 23.20 | Follow cap 18 | Follow cap 16 |
| ---: | ---: | ---: | ---: | ---: |
| 800 px | 36.4 px | 38.6 px | 49.8 px | 56.0 px |
| 927 px | 42.1 px | 44.8 px | 57.7 px | 64.9 px |
| 1200 px | 54.5 px | 57.9 px | 74.7 px | 84.0 px |

An 18-tile cap gives roughly 29 percent more model width than the current
opening fit and 37 percent more than whole Fit at the same viewport. Sixteen
is more dramatic, but crops too aggressively before the viewer has an
off-screen action cue.

## Required semantic rule

Do not blindly truncate an all-actor bounding box around its arithmetic
midpoint. Frontline's two opening spawn centres are exactly 18 tiles apart; a
blind 18-tile midpoint frame would put both chassis on the screen edges and
remove all context.

Normal follow needs an anchor priority such as:

1. explicitly selected team;
2. current combat/impact or contested-objective cluster;
3. current active Frontline position;
4. opening/redeployment fallback to the honest adaptive frame.

When a chosen cluster's honest frame exceeds 18 tiles, cap it around that
cluster and allow remote idle actors to leave the view. The explicit Fit
control remains the one-click tactical overview. An edge cue for off-screen
active actors is desirable before lowering the cap to 16.

This separation preserves Fable's smoothing, deadband, selection semantics,
manual gestures, and explicit Fit behavior. The proposed integration surface
is limited to shared camera arithmetic/tests and action-point selection; it
does not require renderer-specific scale logic.
