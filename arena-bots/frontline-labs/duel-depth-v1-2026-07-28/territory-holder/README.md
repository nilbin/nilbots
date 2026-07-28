# TerritoryHolder

`TerritoryHolder` is a retained `IGenericActorBot` candidate for the
Frontline Labs duel-depth micro-screen. Its doctrine is deliberately legible:
take the active objective, keep useful firing tempo, and leave control only
when the incoming danger justifies the territorial cost.

## Doctrine

Each independently executing body uses this priority order:

1. Fabricate the first Ready allied child when the contract makes that action
   available.
2. Respond to a projectile that will reach this body on its next advance.
3. Fire a clear direct shot.
4. Pathfind to the active objective.
5. Hold position.

The projectile response contains the defining risk decision. A sole allied
mobile on an active, unpaused objective holds through exactly one imminent
projectile when its current health exceeds the worst single-hit damage
declared by the contract. It fires if a direct shot is available; otherwise
it waits and accepts the hit. Multiple imminent projectiles, a potentially
lethal hit, an objective pause, or another allied mobile already providing
control sends the body through the normal dodge path instead.

Mobile fire is intentionally straightforward: the target must lie on the
body's current facing ray, the ray must be clear under the contract's corner
rule, and the bot fires the canonical straight program. Turrets may use their
contract-declared absolute direct heading. This candidate does not bend
shots, rotate solely to acquire a target, Split, Anchor, model opponents, or
adapt from match outcomes.

The implementation reads action IDs/codes, form weights, projectile damage,
objective regions, topology, and per-tick legalities from the resolved
contract rather than duplicating the current playlist constants.

## Mechanical authoring checks

The retained source was checked with:

```bash
dotnet build TerritoryHolder.csproj

../../../../scripts/botarena experiment frontline-labs \
  --bot . \
  --opponent . \
  --runtime in-process \
  --seeds 104729,130363 \
  --out <temporary-directory>
```

The build completed with zero warnings and zero errors. The two-seed
in-process self-play command exited successfully. Its outcomes and replay
documents were not inspected, and the strategy was not revised from results.

No WASM artifact is built here. The cohort owner performs the single
controlled freeze/build after retaining the candidate.
