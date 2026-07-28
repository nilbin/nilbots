# Frontline Labs balance v8: Mobilize

This is the pre-registered action-contract arm in
`../BALANCE-ITERATION-2026-07-28.md`.

- Ruleset: `frontline-labs-1-experiment-mobilize`.
- Turrets gain the declared no-argument `mobilize` action.
- `mobilize-child` preserves the same life, runtime memory, position, facing,
  cooldown, and energy while returning to `child-mobile`.
- Health is preserved and capped from turret maximum `5` to mobile maximum
  `3`.
- Mobilize is irreversible back toward turret for that life, so cycling cannot
  create an Anchor healing loop.
- Map, static capture tuning, lifecycle, fabrication, Split, combat, seed, and
  the three non-Bastion policies remain fixed.
- Adapter, Fabricator, and Pressure source files are byte-identical to
  baseline v2 and their SDK 0.10.3 WASM artifacts are reused byte-for-byte from
  v7.
- Bastion receives one policy pass: its designated turret mobilizes once the
  active objective differs from the position it fortified.

The final evidence is one seed (`104729`), every unordered pair, and both
participant assignments: 12 verified WASM matches.

## Result

The mechanism worked exactly as declared: 15 Anchors led to 15 Mobilizes,
actor identity was preserved, mobile health never exceeded `3`, and no
Mobilized life could Anchor again. All six non-Bastion trajectories matched
the baseline-v2 control, and each Bastion trajectory first diverged on its
first Mobilize tick.

The pacing hypothesis failed. Breaches fell from `7` to `6`, MaxTicks rose
from `5` to `6`, stalled and looped games stayed at `6`, and median duration
rose from `430.5` to `474` ticks. Mobilize is retained as an isolated
architecture/extensibility experiment, but neither it nor this Bastion policy
is promoted into hosted `frontline-labs-1`.
