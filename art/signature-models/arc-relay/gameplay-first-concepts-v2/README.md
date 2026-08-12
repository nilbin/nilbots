# Arc Relay signature props — gameplay-first concepts V2

Status: **owner review; no provider call and no runtime promotion**.

This package replaces the rejected salvage plan for the raw Meshy 7 Trip Node
and Sentinel Seed results. It restarts both props at the two-dimensional
silhouette gate and keeps the currently shipped signature models unchanged.

## Review first

- `gameplay-scale-proof.png` compares the strict top silhouettes at 48 px and
  at the intended live footprint on a dark 104 px tile mockup. It is an early
  readability gate, not a real-replay approval.
- `trip-node-provider-views.png` and
  `sentinel-seed-provider-views-v2.png` are the owner-facing four-view sheets.
- `trip-node-top-east.png` and `sentinel-seed-top-east.png` are canonical
  transparent top-down, East-facing identity images.

## Trip Node

The new identity is an extremely low broken triangle: three broad clamp jaws,
large open centre, flat pressure plate, and no circular puck, dome, eye, spike,
or gun. Dusty light brick-rose armor carries Minesmith's family color without
using orange team paint. The outer silhouette and negative space survive the
48 px proof; surface seams are subordinate.

Target live envelope remains approximately `0.44` tile wide. The model itself
does not depict its renderer-owned rising-spike tell, reveal state, ownership,
range, or detonation.

## Sentinel Seed

The new identity uses a fixed four-lobe planted calyx surrounding an exposed
yaw bearing. A much smaller flattened seed-capsule head sits above it. Its
nearly flush recessed front grille emits the signature beam after the
renderer-owned windup; there is no gun barrel. Pale sage-taupe armor, warm
umber structure, and one muted aubergine seed inset connect it to Nest without
copying the carrier body.

The future production asset must expose two explicit parts:

1. a stationary base rooted at the prop origin;
2. a separately yawing head centered on the bearing.

Do not accept a monolithic model whose entire planted base rotates. The exact
node naming contract must be agreed before provider or runtime promotion.

Target live envelope remains approximately `0.62` tile wide and `0.55` tile
high. Range, target line, windup, beam, hull, ownership, and expiry stay
renderer-owned.

## Meshy input plan

The official Meshy documentation currently accepts one to four images of the
same object and recommends **Front + Side + Back + 3/4**. Those four views are
prepared for Sentinel under `provider-inputs/sentinel-seed/`.

Trip Node substitutes strict Top for Back because its rule-bearing identity is
a floor planform and its rear differs only by rotational registration. Its
ordered set is Front, Right, Top, Front-right 3/4 under
`provider-inputs/trip-node/`.

Every provider input is a separate 1040×1040 PNG. Do not upload either contact
sheet as one image. Inputs remain inactive until the owner approves the
concepts and explicitly authorizes provider use.

## Reproduction

The source-generation prompts are retained verbatim in `PROMPTS.md`. The
chroma sources are preserved for deterministic background removal. With web
dependencies installed, rebuild the derived canonical top views, split
provider inputs, and scale proof with:

```sh
node scripts/class-models/build-arc-signature-concept-review.mjs
```

The generator never calls Meshy or changes runtime assets.
