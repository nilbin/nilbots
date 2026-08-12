# Arc Relay signature props — gameplay-first concepts V2

Status: **Meshy 7 batch complete; generated base meshes awaiting owner review**.

This package replaces the rejected salvage plan for the earlier Trip Node and
Sentinel Seed results. The restarted 2D concepts were approved, and two
authorized Meshy 7 multi-image tasks succeeded on 2026-08-12 for 60 credits
total. Balance after the batch is 1310. The currently shipped signature models
remain unchanged.

## Review first

- `gameplay-scale-proof.png` compares the strict top silhouettes at 48 px and
  at the intended live footprint on a dark 104 px tile mockup. It is an early
  readability gate, not a real-replay approval.
- `trip-node-provider-views.png` and
  `sentinel-seed-provider-views-v2.png` are the owner-facing four-view sheets.
- `trip-node-top-east.png` and `sentinel-seed-top-east.png` are canonical
  transparent top-down, East-facing identity images.
- `trip-node-meshy7-six-view.png` and
  `sentinel-seed-meshy7-six-view.png` show normalized WebGL renders of the
  generated base meshes from six fixed cameras.
- `trip-node-top-silhouette-overlay.png` and
  `sentinel-seed-top-silhouette-overlay.png` compare the canonical and
  generated planforms at the same normalized span. Light gray overlaps;
  magenta is canonical-only; cyan is provider-only.

## Provider result gate

Both generated forms are worth retaining as base meshes, but neither is ready
for runtime promotion.

- Trip Node preserves the broken triangle, three jaws, open centre, and low
  profile. Its top-silhouette IoU is `0.87784`. The source front followed `+Z`;
  the review derivative uses a recorded `-90°` yaw to meet the `+X` contract.
  Its texture drifted from dusty brick-rose toward fixed orange, so material
  approval is withheld because orange is a team color.
- Sentinel Seed preserves the four planted lobes, compact head, flush grille,
  and visible yaw collar. Its top-silhouette IoU is `0.88783`, and its source
  front already follows `+X`. The pale sage/taupe material is a substantial
  improvement over the rejected pass. However, Meshy returned one fused node,
  mesh, primitive, material, and spatially continuous surface. Production must
  still split the stationary base from the yawing head.

Both raw GLBs validate with zero errors. Each has the expected warning that a
normal-mapped primitive has no authored tangents. Raw provider masters and 2K
maps remain ignored art-side files; the 1K WebP normalized GLBs are review
derivatives only, not mobile shipping tiers.

Exact task receipts, downloaded-artifact hashes, credit ledgers, structural
audits, and individual assessments live under
`art/class-models/provider-runs/meshy/arc-relay-meshy7-signature-props-v3/`.

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

## Completed Meshy input plan

The official Meshy documentation currently accepts one to four images of the
same object and recommends **Front + Side + Back + 3/4**. Those four views are
prepared for Sentinel under `provider-inputs/sentinel-seed/`.

Trip Node substitutes strict Top for Back because its rule-bearing identity is
a floor planform and its rear differs only by rotational registration. Its
ordered set is Front, Right, Top, Front-right 3/4 under
`provider-inputs/trip-node/`.

Every provider input was a separate 1040×1040 PNG; neither contact sheet was
uploaded as one image. The completed tasks are
`019ff5c4-af6d-7bee-bfac-591b78b34c52` (Trip Node) and
`019ff5c8-f17f-7083-a432-71f16c9f1c05` (Sentinel Seed). Both paid
authorizations are consumed. The public API receipts expose no retry allowance;
any free retry shown in Meshy's gallery remains a separate owner gate.

## Reproduction

The source-generation prompts are retained verbatim in `PROMPTS.md`. The
chroma sources are preserved for deterministic background removal. With web
dependencies installed, rebuild the derived canonical top views, split
provider inputs, and scale proof with:

```sh
node scripts/class-models/build-arc-signature-concept-review.mjs
```

The generator never calls Meshy or changes runtime assets.
