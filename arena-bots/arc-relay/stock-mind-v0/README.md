# Arc Relay stock mind v0

This freeze separates the commander layer from the execution layer for the
Gate 3.2 loop audit.

> **Provisional evaluation format.** `arc-relay-evaluation-sheet-v0` exists for
> controlled coverage, hashing, and reproducibility. It is not the product or
> player-facing sheet schema. The human drawing/editing model and unlock-gated
> sheet parts require their own UX design pass after Gate 3.

- `ArcRelayStockMind.cs` is the fixed, map-reading stock algorithm. The Gate
  3.2 freeze adds rising-edge, cooldown, and role-scoped evaluation gambits,
  plus a same-Core prior-carrier guard against handoff ping-pong.
- `sheet.json` is the provisional evaluation commander sheet: eight-class composition under
  the two-copy cap, per-slot theater allocation, drawn outbound/return paths,
  named zones and rally lines, carrier/escort/interception policies, and an
  ordered gambit list.
- `StockSheet.cs` is the deterministic ARS1 evaluation-data decoder. Build the
  algorithm once; validate each provisional JSON sheet independently with:

```bash
python3 scripts/generate-arc-relay-sheet.py \
  arena-bots/arc-relay/stock-mind-v0/sheet.json \
  --validate-only
```

At match launch, the Arc Relay CLI deterministically encodes that JSON as ARS1
and passes the participant-local bytes as `MindStart.EvaluationData`. Every
match records the shared algorithm hash and each sheet hash separately; a new
evaluation variant no longer rebuilds the WASM. This is audit plumbing, not
the future player-facing sheet format.
