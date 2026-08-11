# Meshy 7 brighter ordered-multiview fleet retexture V3

Status: owner approved and promoted to the web runtime.

Kestrel and the approved Palisade calibration are retained. The remaining
fourteen classes were retextured from four ordered V7 views each: strict front
first, followed by strict right, strict back, and front-right three-quarter.
Those source views carry the matte/satin powder-coated material direction and
received one deterministic RGB value lift; no text-style prompt competed with
the images.

All fourteen tasks succeeded with Meshy 7, original UVs, 2K PBR maps, and no
rigging. The batch consumed exactly 140 credits and moved the balance from
1,570 to 1,430.

## Review normalization

Raw provider artifacts and maps are retained locally with hashes in the run
records. The review tier transplants the maps onto the pinned original-UV
geometry, normalizes facing and scale, and compresses each bot to three 512 px
WebP PBR textures. The fourteen new bots receive a deterministic 0.65
roughness floor because Lantern's provider map undercut the matte source
direction. This changes only the metallic-roughness texture's green channel;
base color, metallic, normals, geometry, Kestrel, and Palisade remain
unchanged.

All sixteen review geometry fingerprints are identical to the original V1
review tier.

## Initial visual read

This pass is materially closer to the approved 2D identities than the rejected
text-prompt V2 run. The dominant surfaces are brighter than Palisade, the
palette remains restrained rather than toy-bright, and the roughness gate
removes the remaining plastic response. Several classes intentionally occupy
neighboring industrial color families. The owner approved this fixed-camera
fleet and asked for the matching shared web icons to be replaced as well.

The exact approved WebP review artifacts are now the runtime models; no further
optimization pass was applied. Promotion provenance and per-class budgets are
pinned in `art/class-models/arc-relay/ledger.json`. Canvas/UI presentation uses
transparent 192 px, strict top-down renders with +X pointing screen-right/east;
team teal/orange remains renderer-owned through narrow leading-edge masks.

Review sheet:
`art/reviews/arc-relay-meshy7-fleet-retexture-v3/fixed-orientation-sheet.png`

Runtime icon sheet:
`art/reviews/arc-relay-meshy7-fleet-retexture-v3/top-down-east-icons-sheet.png`
