# Bastion authoring DX

## Timing and scope

- Scaffold creation to first valid controlled WASM build: approximately
  7 minutes 2 seconds.
- Authored strategy passes: 1.
- Controlled build attempts: 2.
- Mechanical compiler repairs: 2.
- Matches, replays, standings, opponent sources, and tournament outputs
  inspected: 0.

The strategy was frozen before the first build. The only later source changes
were the two compiler-directed type repairs listed below. This report was
written after the repaired source built successfully.

## Documentation and terminology

The standalone Labs-v1 rule card was sufficient to reason about the objective,
Fabricate, Anchor, lifecycle isolation, shared allied observation, and turret
weight. Its repeated warning that current values are not array-shape
guarantees was useful, as was the explicit separation between Labs v1 and the
older Frontline-alpha contract.

The most important terminology distinction was between a body life, a stable
unit slot, and a per-life bot instance. It directly shaped the implementation:
the designated Anchor role is derived from the stable fabricated slot because
private fields cannot coordinate roles across separately created lives.

The authoring packet says to use map regions and tags, but discovering a home
pad still requires a multi-step join:

1. `fabricate` action to fabrication transition;
2. transition source-region role to participant-region assignment;
3. assigned region ID to map region tiles.

Anchor placement similarly requires joining the `transform` form transition's
placement tags to the map's tile-tag overlays. These joins are expressive, but
a small public helper for role-bound regions and transition-valid tiles would
make contract-driven authorship less error-prone.

## API and scaffold friction

The generated scaffold was valuable for the legality lookup, typed arguments,
objective binding, and deterministic BFS pattern. Its starter assumes the mode
binding and observation are Frontline and throws otherwise. Bastion replaced
that assumption with safe action fallbacks because the cohort requirements
emphasize bounded, non-faulting behavior when optional capabilities are absent.

The typed legality union makes illegal argument values avoidable, although the
repeated `OfType<...>().SingleOrDefault()` pattern is verbose for direction,
unit, form, and heading constraints. Convenience accessors such as
`AllowedDirections("move")` and `AllowedFormTargets("transform")` would reduce
boilerplate without hiding the contract.

`VisibleProjectiles` is a nullable `ImmutableArray<T>`. That accurately
distinguishes unsupported sensing from an empty observation, but its nullable
value-type shape was easy to unwrap incorrectly at first.

No missing capability prevented the doctrine. The public contract exposed
enough data to choose one fabricated slot for Anchor duty while preserving all
other bodies for mobile capture pressure.

## Diagnostics

The controlled builder reported source file, line, compiler code, and concise
type errors. It also made the cold-cache environment, selected runtime,
compiler, SDK, cache status, artifact hash, and output path visible. Those
diagnostics were enough to repair compilation without running a match.

## Hardcoding temptations avoided

The player-facing rule card makes the current map, team count, unit slots,
forms, coordinates, action codes, and objective count easy to copy. Bastion
does not copy them. In particular, it does not assume:

- two teams or three units;
- a particular map size or objective coordinate;
- numeric action codes;
- a fixed active-objective index range;
- `prime-mobile`, `child-mobile`, or `turret` form IDs;
- a particular home pad or spawn coordinate;
- that Fabricate, Transform, or turret heading fire exists.

The doctrine intentionally recognizes the optional stable action semantics
`fabricate`, `transform`, and `shoot-direction`. Numeric codes and typed target
values always come from the current legality entry.

## Mechanical repairs

1. Replaced a nullable-style check on `Dictionary<string, Position>` lookup
   results with `TryGetValue`, because `Position` is a non-nullable value type.
2. Replaced an invalid `.Value` access on the nullable
   `ImmutableArray<ObservedProjectile>` observation with pattern matching that
   cleanly distinguishes supported projectile sensing from `null`.

Neither repair changed role assignment, priorities, navigation, targeting, or
the Anchor doctrine.
