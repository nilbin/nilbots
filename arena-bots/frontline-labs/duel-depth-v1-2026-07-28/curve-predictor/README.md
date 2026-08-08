# CurvePredictor

CurvePredictor is a one-pass `IGenericActorBot` for the local Frontline Labs
duel-depth screen. Its doctrine is to select one visible mobile opponent,
predict that opponent's next cardinal step toward the active objective, and
commit a private projectile path through either the predicted tile or the
opponent's current objective tile.

Every mobile shot starts on the body's current facing. The bot considers only:

- the canonical straight program; or
- exactly one 45-degree bend left or right after 1–4 travelled tiles.

`InitialAimOffset` is always zero and `BendCount` is never greater than one.
Candidate paths are previewed with the public `ShotPaths.Preview` API against
the contract map. A bent path is preferred when it uniquely intercepts the
predicted move; straight fire remains the simpler choice for an opponent that
holds the objective or already lies on the current firing line.

The remaining priorities are deliberately small: evade an imminent projectile
without abandoning an objective tile when a safe on-objective move exists,
fabricate a Ready companion when legal, rotate toward a viable future
intercept, and otherwise pathfind to the active objective. All forms, actions,
objective regions, attack limits, walls, and legality values come from the
resolved match contract or current observation.

Diagnostic compilation and self-play:

```bash
dotnet build CurvePredictor.csproj --nologo

scripts/botarena experiment frontline-labs \
  --bot arena-bots/frontline-labs/duel-depth-v1-2026-07-28/curve-predictor \
  --opponent arena-bots/frontline-labs/duel-depth-v1-2026-07-28/curve-predictor \
  --runtime in-process \
  --one-bend-shots \
  --seed 104729
```

The cohort owner performs the single controlled WASM freeze; this authoring
pass intentionally does not invoke a WASM build.
