# Frontline Labs balance v6: capture threshold 12

This is a local-only, single-variable numeric causal arm.

- Capture threshold changes from `15` to `12`.
- Gain remains `1`, decay remains `1` every `2` ticks, redeploy pause remains
  `5`, MaxTicks remains `500`, and pushes-to-breach remains `3`.
- All four baseline-v2 source trees and WASM artifacts are byte-identical.
- Map, format, lifecycle, combat, forms, actions, assignments, and seeds remain
  frozen.
- The candidate has its own content-descriptive ruleset ID and fingerprints;
  it does not reinterpret immutable hosted `frontline-labs-1`.
- The sprint uses seed `104729` with mirrored assignments: 12 matches. The v2
  seed-104729 slice is the paired control because v2's second seed repeated
  every ordered trajectory.

Acceptance criteria, scaled to the non-duplicated 12-game matrix:

- all 12 matches verify with zero faults;
- MaxTicks fall from `5` to at most `3`;
- draws fall from `2` to at most `1`;
- breaches rise from `7` to at least `9`;
- stalled and looped games each fall from `6` to at most `3`;
- games with a no-interaction run of at least 75 ticks fall from `4` to at
  most `2`;
- active share remains at least 75%;
- median normalized action-family entropy reaches at least 0.60;
- decisive participant-assignment share stays at or below 65%, and no entrant
  exceeds 50% of match points.

If breaches rise while stalled and looped games remain above three, the lower
threshold only relabels repetitive trajectories and is rejected as a
watchability fix.
