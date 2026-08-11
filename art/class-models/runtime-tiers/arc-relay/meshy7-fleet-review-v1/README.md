# Meshy 7 Arc Relay fleet review tier

Status: **owner review only**. These files have not replaced the approved runtime fleet.

This tier applies the already reviewed Kestrel lean profile to all sixteen Meshy 7 concepts:

- upright `+Y`, grounded at `Y=0`, and normalized to a `0.99`-tile planform;
- source front inspected at `-X`, then corrected by 180 degrees to the Nilbots `+X` contract;
- quantized review geometry and embedded 512 px WebP maps;
- one fused authored material per bot, with renderer-owned team presentation left intact.

Fifteen models are 1.59–1.85 MB and approximately 77–80k triangles. Sunder is the review-only exception at 7.94 MB / 205k triangles: its disconnected layered shell reaches the simplifier's clean topology limit before the shared target. No second destructive optimization pass was applied because this gate is about approving the concepts in the game, not promoting shipping assets.

The complete review fleet is 33,309,644 transfer bytes and 118,506,828 bytes of estimated RGBA8-fallback GPU residency. Shipping still requires owner approval, selective KTX2 textures, device profiling, and a separate promotion decision.

Rebuild and verify:

```bash
node scripts/class-models/build-meshy7-fleet-review-tier.mjs
node scripts/class-models/build-meshy7-fleet-review-tier.mjs --check
```
