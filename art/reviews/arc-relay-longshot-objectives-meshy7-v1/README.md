# Longshot and objectives Meshy 7 review

Status: **owner review** as of 2026-08-13. Nothing in this batch has been
merged to `main`.

## Outcome

- Longshot: pass for owner gameplay review after midpoint color correction and
  deterministic matte-roughness treatment.
- Survey Flare: pass for owner gameplay review in both in-flight and active
  states. The model replaces only the physical device; renderer-owned scan
  state remains intact.
- Team Reactor: pass for owner gameplay review. The renderer continues to own
  team color, crest, charge counters, integrity counters, and state effects.
- Core Well: pass for owner gameplay review after the owner accepted the
  four-anchor silhouette as an intentional deviation. A deterministic 0.80
  vertical scale reduces the housing from about 0.237 to 0.190 tile high, and
  its metallic-roughness map is floored at 0.82 to remove the shiny finish.
  The renderer continues to own source identity, birth/rearm progress, the live
  Core, and all team/state color.

The corrective pass consumed the approved 80-credit cap and left a balance of
1,100 credits. No task used auto-rigging.

## Evidence

- `corrective-provider-review/`: strict six-view provider geometry and material
  sheets for all four generated outputs.
- `sandbox/longshot-objectives-meshy7-v1/game-review/interactive/`: interactive
  two-replay picker for playing, pausing, scrubbing, zooming, and checking both
  team orientations in the real WebGL game.
- `gameplay-review/desktop-gameplay-review.webp`: Longshot, Survey Flare, Team
  Reactor, and the flattened matte Core Well in the real desktop WebGL renderer.
- `gameplay-review/phone-gameplay-review.webp`: retained replay evidence at the
  mobile landscape viewport.
- `gameplay-review/evidence.json`: exact replay seed, proof time, renderer, asset
  load, image hash, and viewport provenance for every gameplay capture.
- `gameplay-review/build-record.json`: exact candidate bundle hashes and proof
  that all temporarily staged runtime assets were restored or removed.

The game-review bundle stages all four candidates only while building, then
restores the live Longshot byte-for-byte and removes the temporary Flare,
Reactor, and Well runtime directories. The review audit and build record make
those restoration checks explicit. None of these candidate assets is promoted
by this review branch.
