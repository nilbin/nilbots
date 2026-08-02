# Intelligent gambit vertical slice

This directory is evaluation evidence for the owner-approved semantics in
`docs/ARC-RELAY-INTELLIGENT-GAMBIT-FRAMEWORK.md`. It is not the player-facing
sheet format.

The two comparison sheets have the same eight classes and the same complete
baseline. `baseline-only.json` contains no operation cards.
`rear-hook-lantern-sweep.json` adds two bounded team operations:

- Rear Hook claims two named Towlines for concealment, a carrier strike, and
  physical extraction. Its sample replay shows costly preparation and honest
  abort/deadline recovery when the trap is exposed or never becomes feasible.
- Lantern Sweep claims the current carrier, a Lantern, and one replaceable
  screen during preparation. Its two ordered commit branches are fixed once
  chosen; the sample selects `alternate-return`, then aborts and recovers when
  the carrier loses the Core.

Both durable evidence cells use seed `86080201`, the WASM runtime, and the
`depth-counterflow` loop profile. Full canonical replays were regenerated
twice and verified byte-identically, then pruned under the normal Arc Relay
evidence policy. Each retained match record pins its canonical hash; the
adjacent gzip broadcast is the compact watchable slice.

Build the one algorithm artifact from `stock-mind-v3/` with:

```sh
dotnet ../../../src/BotArena.Cli/bin/Debug/net10.0/botarena.dll build .
```

The expected artifact SHA-256 is
`50a032e96efc6502a4f2fb662eb095b37561e420d4644aa87e81977922dfc12b`.
