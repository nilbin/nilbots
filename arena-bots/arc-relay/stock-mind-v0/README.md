# Arc Relay stock mind v0

This freeze separates the commander layer from the execution layer for the
Gate 3 depth audit.

> **Provisional evaluation format.** `arc-relay-evaluation-sheet-v0` exists for
> controlled coverage, hashing, and reproducibility. It is not the product or
> player-facing sheet schema. The human drawing/editing model and unlock-gated
> sheet parts require their own UX design pass after Gate 3.

- `ArcRelayStockMind.cs` is the fixed, map-reading stock mind. Hold its source
  hash constant throughout the Phase D depth audit.
- `sheet.json` is the provisional evaluation commander sheet: eight-class composition under
  the two-copy cap, per-slot theater allocation, drawn outbound/return paths,
  named zones and rally lines, carrier/escort/interception policies, and an
  ordered gambit list.
- `StockSheet.g.cs` is deterministic generated input. Regenerate it after a
  sheet edit with:

```bash
python3 scripts/generate-arc-relay-sheet.py \
  arena-bots/arc-relay/stock-mind-v0/sheet.json \
  --output arena-bots/arc-relay/stock-mind-v0/StockSheet.g.cs
```

The generated file records the exact sheet SHA-256. A sheet-space entrant is a
fresh WASM build of the unchanged mind source plus one generated sheet. The
sheet JSON and WASM hashes are both recorded for every match.
