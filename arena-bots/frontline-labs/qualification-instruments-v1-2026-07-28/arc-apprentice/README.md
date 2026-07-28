# ArcApprentice

Status: T3-qualified under `frontline-duel-depth-union-t3-v1`; T4 boundary
pending; not balance-verdict eligible.

ArcApprentice derives from the retained T2 HouseApprentice and adds one narrow
tactical layer:

- it enumerates only contract-legal shot programs;
- it previews each program against the declared map with strict diagonal
  corners;
- it fires a curve only when that preview intersects a currently visible
  enemy;
- when no curved intercept exists, objective tempo precedes a routine straight
  exchange.

That is enough to distinguish a valid off-axis bend from a wall-terminated
one, preserve range-3/range-4 cadence parity, use a missed-shot cooldown
window, and decline a locally dominated objective-weight-zero transform. It
still has no opponent model, curve mixture, forced-shot construction, body
roles, coordinated focus fire, transform doctrine, or multi-front planning.

The suite reruns the exact cumulative T2 profile before its T3 probes. Passing
therefore awards T3 rather than a disconnected component. It does not make
this an exact-boundary T3 instrument until the future cumulative T4 profile
also demonstrates a clean fail.

```bash
scripts/botarena experiment frontline-labs qualify \
  --bot arena-bots/frontline-labs/qualification-instruments-v1-2026-07-28/arc-apprentice/bot.wasm \
  --runtime wasm \
  --suite frontline-qualification-4 \
  --out /tmp/arc-apprentice-t3
```

