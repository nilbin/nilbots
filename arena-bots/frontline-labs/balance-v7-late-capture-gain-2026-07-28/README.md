# Frontline Labs balance v7: late capture gain

This is a pre-registered, schedule-shaped pacing arm.

- Capture threshold remains `15`.
- Sole-control gain is `1` through tick `299` and `2` from tick `300`.
- Decay remains `1` every `2` ticks, redeploy pause remains `5`, MaxTicks
  remains `500`, and pushes-to-breach remains `3`.
- All four baseline-v2 strategy sources are byte-identical.
- The artifacts are mechanically rebuilt against SDK/Guest `0.10.3` so they
  can parse the optional canonical `gainSchedule`; there is no policy pass.
- Map, format, lifecycle, combat, forms, actions, assignments, and seed remain
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

This arm is intended to alter only matches that enter a repetitive late tail.
If its improvements are merely timeout relabeling, or if early breaches change
despite identical pre-300 trajectories, reject the implementation or the
hypothesis.

## Result

All 12 WASM matches verified with zero faults or disqualifications. After
normalizing only contract/artifact identity and the ruleset-derived per-life
seed, every ordered pairing had an identical spectator-visible trajectory
through tick 299 (or its earlier terminal tick).

The arm moved breaches from `7` to `8`, MaxTicks from `5` to `4`, draws from
`2` to `1`, median duration from `430.5` to `416.5`, and median action-family
entropy from `0.565` to `0.589`. It did not change stalled games (`6`), looped
games (`6`), or games with a no-interaction run of at least 75 ticks (`4`).

Verdict: the generic schedule architecture is accepted, but `300:2` is
rejected as the watchability fix and is not promoted into hosted v1.
