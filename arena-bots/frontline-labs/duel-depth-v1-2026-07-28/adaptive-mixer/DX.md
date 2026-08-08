# AdaptiveMixer authoring notes

Source freeze precedes tournament disclosure. This bot was authored from the
generic-actor template, player rules, experiment brief, and public SDK only.

## Useful surfaces

- The delivered `ShotProgramDefinition` makes the candidate envelope
  discoverable without a ruleset-name branch.
- `ShotPaths.Preview` and deterministic `context.Random` make committed
  private choices straightforward to author and reproduce.
- The generated `ArenaBasics.cs` keeps routine fabrication, direct fire,
  evasion, and pathfinding out of the strategy file.

## Friction

- `ShotPaths.Preview` accepts the legacy-shaped `Direction` plus numeric
  program, while legality is exposed through the generic action constraint.
  The connection is sound but requires reading both public types.
- The public program envelope validates numeric fields but does not provide a
  generic convenience constructor for the current minimum legal bend.

No opponent source, replay, standings, or tournament outcome informed this
revision. Mechanical compile/self-play findings may repair faults but may not
change doctrine.

## Freeze

- `AdaptiveMixer.cs` SHA-256:
  `b045619ad7b0b8d381a98a9ac4f94a418c5c4a4bd332760ea043156adb581c38`
- `ArenaBasics.cs` SHA-256:
  `c4e95503ad44b9c56cd97275eddfbccc1d92145e7bebd96d292c6967669e575c`
- .NET build: zero warnings and errors.
- candidate self-play seed `104729`: complete replay v3, hash verified;
  44 curved program submissions demonstrated the declared mechanic.
