# Meshy 7 Arc Relay fleet — in-game owner review

This is a reversible review of all sixteen unapproved Meshy 7 concepts in the real Arc Relay WebGL renderer.

- The canonical 600-tick replay fields eight distinct blue classes and eight distinct orange classes, so all sixteen models are active at tick 000.
- Desktop and phone evidence use the same authoritative opening tick, strategic overview camera, browser scale 1, and WebGL renderer.
- Every emitted review-build GLB is hash-matched to its candidate tier artifact.
- The fixed-orientation sheet replaces Meshy's provider-camera contact sheet for comparison. Every cell uses the same front-right three-quarter camera after the source `-X` front is corrected to Nilbots `+X`.
- Approved runtime models were restored byte-for-byte after the review build. Nothing here promotes or ships the candidates.

The review bundle is intentionally a basic lean pass. Its sixteen models total 33.3 MB; fifteen sit near 1.6–1.85 MB, while Sunder remains a 7.94 MB topology outlier. Further geometry and KTX2 work is deferred until the concepts are approved.

Reproduce the review:

```bash
node scripts/class-models/build-meshy7-fleet-review-tier.mjs --check
node scripts/class-models/generate-meshy7-fleet-review-replay.mjs
node scripts/class-models/build-meshy7-fleet-game-review.mjs
node scripts/class-models/install-meshy7-fleet-game-review.mjs
node scripts/class-models/capture-meshy7-fleet-game-review.mjs --url 'http://127.0.0.1:9073/?standalone&audio=off'
node scripts/class-models/capture-meshy7-fleet-fixed-orientation.mjs http://127.0.0.1:9074
```
