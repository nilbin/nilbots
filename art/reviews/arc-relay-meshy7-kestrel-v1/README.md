# Meshy 7 Kestrel in-game review

## Decision

Use the lean candidate for interactive review. Do not promote it to the shipping look yet.

At the real Arc Relay desktop and phone overview cameras, the 79,064-triangle / 512-map lean derivative preserves the same pale angular silhouette and readable nose as the 158,130-triangle / 1024-map rich derivative. The richer asset costs 2.53× more transfer bytes and about 3.6× more decoded model GPU residency without a meaningful zoomed-out benefit.

The current shipping Kestrel remains untouched as the fallback and comparison baseline.

## Evidence contract

- canonical Arc Relay preflight broadcast, replay hash `79577c3d46c3bc88d5ad9be29a6e709cf1ead134ec26f7243600ac004633325a`
- authoritative tick 000 for every candidate
- Chromium WebGL2 at browser scale 1
- 1440 × 900 desktop and 844 × 390 phone-landscape arenas
- all eight active fleet GLBs loaded successfully for every capture
- no page, console, or request errors
- an additional 5× in-engine camera inspection; no browser or image enlargement

The strongest overview is [desktop-inspection-game-camera-comparison.png](desktop-inspection-game-camera-comparison.png). The tighter desktop and phone crops are [desktop-comparison.png](desktop-comparison.png) and [phone-comparison.png](phone-comparison.png). Machine-readable capture details and asset hashes are in [evidence.json](evidence.json).

## Remaining shipping work

- transcode the chosen maps to the selective KTX2 runtime tier
- explicitly resolve or accept the missing-authored-tangent warning
- re-run runtime memory, fallback, and CLI-model-free validation after promotion
