# ArcApprentice

Status: exact T3 boundary measured under
`frontline-duel-depth-union-t3-v1`/`frontline-duel-depth-union-t4-v1`; not
balance-verdict eligible.

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
therefore awards T3 rather than a disconnected component. Suite 5 now
measures its upper edge: it passes suppression, prediction-chamber,
front-rotation, and thin-front holdout components, but fails current-map entry
from one mirrored assignment by repeatedly conceding the choke.

```bash
scripts/botarena experiment frontline-labs qualify \
  --bot arena-bots/frontline-labs/qualification-instruments-v1-2026-07-28/arc-apprentice/bot.wasm \
  --runtime wasm \
  --suite frontline-qualification-4 \
  --out /tmp/arc-apprentice-t3
```

The clean upper-boundary report is
`qualification-frontline-5-boundary.json`; its replay-byte manifest is
`../arc-t4-boundary-evidence-manifest.json`.
