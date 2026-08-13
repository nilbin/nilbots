# Longshot and objectives Meshy 7 review

Status: **combined owner review** as of 2026-08-14. Nothing in this batch has
been merged to `main`.

## Outcome

- Longshot: pass for owner gameplay review after midpoint color correction and
  deterministic matte-roughness treatment.
- Survey Flare: pass for owner gameplay review in both in-flight and active
  states. The model replaces only the physical device; renderer-owned scan
  state remains intact.
- Team Reactor: its approved low Meshy recess is the feature branch runtime
  model. The renderer now sinks most of the housing below the floor, masks its
  open aperture against bot glow pools, keeps transient Core lights off the
  neutral metal, and lets hovering actors win walkable overlap. Team color,
  crest, charge counters, integrity counters, and state effects remain
  renderer-owned.
- Core Well: pass for owner gameplay review after the owner accepted the
  four-anchor silhouette as an intentional deviation. A deterministic 0.50
  vertical scale reduces the housing from about 0.237 to 0.107 tile high, and
  its metallic-roughness map is floored at 0.82 to remove the shiny finish.
  The renderer recesses it another 0.045 tile and applies actor-first overlap.
  The renderer continues to own source identity, birth/rearm progress, the live
  Core, and all team/state color.

The corrective pass consumed the approved 80-credit cap and left a balance of
1,100 credits. No task used auto-rigging.

## Evidence

- `corrective-provider-review/`: strict six-view provider geometry and material
  sheets for all four generated outputs.
- `sandbox/longshot-objectives-meshy7-v1/combined-review/site/`: one interactive
  two-replay picker containing all four subjects together on current Tapline-3,
  with Longshot and Lantern swapped across the teal/orange sides.
- `combined-gameplay-review/`: exact build and browser evidence for the current
  integrated review, including emitted model hashes and phone validation.

The combined game-review bundle uses the promoted low Reactor already present
on this feature branch. It stages the corrected Longshot, Survey Flare, and
flattened Well only while building, then restores the live Longshot manifest
and GLB byte-for-byte and removes the temporary Flare and Well runtime
directories. Their permanent default promotion remains gated on this combined
owner review.
