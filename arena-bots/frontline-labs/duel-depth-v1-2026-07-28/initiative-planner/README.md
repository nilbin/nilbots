# InitiativePlanner

Retained positional reference for the Frontline duel-depth study.

Its narrow claim is explicit: when the active objective is at most two moves
away, spend movement before a visible straight projectile reaches its final
public state. Once established, prefer immediate safe evasion, deterministic
private straight/left/right fire, visible-enemy facing, and contract-driven
objective pathing. It also activates available companions in complete matches.

The bot reads rules, action kinds, legalities, objective binding, map walls,
projectile cadence, and topology from the generic actor contract. Current map
coordinates are not embedded in its policy.

Run the implemented component:

```bash
nilbots experiment frontline-labs qualify \
  --bot . \
  --runtime in-process \
  --out evidence/qualification
```

Passing that command proves only the mirrored `entry-initiative` component.
It does not award T4 because the cumulative T1–T3 and remaining T4 probes are
not implemented yet.
