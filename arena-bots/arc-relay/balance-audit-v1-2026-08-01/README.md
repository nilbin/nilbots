# Arc Relay balance audit mind v1

This is an evaluation-only, build-once mind used by the 2026-08-01 balance
pass. It runs several deterministic strategies from the provisional `ARS1`
sheet envelope so composition and doctrine can change without changing the
algorithm artifact. It is not a player-facing sheet design or a stock-bot
candidate.

The archived sheets reproduce the strategies used by the balance screens:

- `balanced.json`: three-theater baseline;
- `convoy.json`: concentrated escort play with outer-Core fallback;
- `control-grid.json`: complete roster/mechanics coverage, including live Veil
  smoke;
- `interception.json`: loose-Core and enemy-carrier pressure; and
- `split.json`: distributed theater control.

The execution artifact used for the report had SHA-256
`acd71497b5beeece8752c7dc0800c775b2ec715dd6e83311c51782596c9d26b8`.
The final exact-verified broadcasts, records, and scorecards remain generated
evidence under `sandbox/`; this directory freezes the authored inputs needed to
rebuild or extend the study.

Build through the same controlled generic-mind pipeline used by
`scripts/arc-relay-sweep.py`; do not infer a product schema from `AuditSheet`.
