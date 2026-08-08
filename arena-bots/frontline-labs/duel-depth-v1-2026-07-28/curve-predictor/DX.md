# CurvePredictor authoring DX

Date: 2026-07-28

## Scope and source freeze

The project was scaffolded with:

```bash
scripts/botarena new CurvePredictor --profile generic-actor
```

Generation occurred in a temporary directory before the scaffold was placed
under the retained cohort path. During this authoring pass, additional reading
was limited to the player-facing Frontline rules and duel-depth brief, the
generic-actor template, and the public `BotArena.Sdk` API/source documentation.
No other entrant source or prior replay/result material was opened.

The single strategy pass was frozen before self-play. No strategy, priority,
target-selection, movement, or shot-program changes were made from mechanical
match output.

Frozen pre-documentation file hashes:

```text
fd809c6a2a9e9215f32c8003a34991b26eaa5d595908ae8fb9af3ede43d52087  CurvePredictor.cs
8e57ff596f1f8eacad7ca5c88e5d1b0056cf8e470587bf55bc91353f0e7d6584  ArenaBasics.cs
da707aa194a21d00de7504e62ea04dd2546bb6d6dc26c2805d3ca8daa740b231  CurvePredictor.csproj
4ba1934cb57db410ae648eec35ba9a6c1653b054aede7dcb330b6c9ab57d1310  botarena.json
```

## Mechanical checks

`dotnet build CurvePredictor.csproj --nologo` succeeded with zero warnings and
zero errors.

Two seed-104729 self-play checks ran in-process under
`frontline-labs-1-experiment-one-bend-shots`, once normally and once with
`--swap`. Both completed without runner failure and produced the same
deterministic replay hash:

```text
bac3d302615c7d72d068786079eaab379ae02e5cb31882c78433ecb721210445
```

`scripts/botarena verify` accepted both outputs as canonical replay v3 with
valid contract content and matching stored hashes.

These checks establish compilation, generic-actor loading, decision legality,
full in-process completion, deterministic mirrored self-play, and replay
integrity. They are mechanical checks only and were not used for strategy
iteration. No WASM build was performed.
