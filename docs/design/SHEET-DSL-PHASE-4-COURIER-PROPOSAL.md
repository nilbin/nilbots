# Sheet DSL Phase 4 — courier policy proposal

Status: **proposal only; deliberately unimplemented** (2026-08-09).

## The remaining opacity

`custodyPolicies` is correctly a shared protocol: reservations, handoff windows,
drop recovery, delivery routes, and timeout behavior coordinate several bodies.
But the stock mind still chooses which visible Core a volunteer pursues, when a
carrier gives up, and who should receive a handoff. A player cannot predict or
tune those choices from the sheet.

The smallest addition is a body-owned `courier` policy beside `fight`. It is a
preference offered to the shared custody coordinator, not a second custody
protocol:

```json
"courier": {
  "prefer": ["highest-value", "nearest", "deepest"],
  "reach": 20,
  "abandon": { "threats": 2, "health": 2 },
  "handoff": "homeward-nearest"
}
```

- `prefer` is an ordered list, like `fight.targets.prefer`. `highest-value`
  names the Core's actual charge value; it replaces the vague draft word
  `richest`. `nearest` is path distance. `deepest` is team-relative progress
  into enemy territory, not a map coordinate.
- `reach` is the maximum path distance at which this body volunteers. It is the
  courier analogue of a fight chase leash.
- `abandon` gives the two current safety thresholds names. Omitting either
  threshold means that threshold does not make this body abandon the run.
- `handoff` selects a receiver policy. `homeward-nearest` means the legal
  receiver that advances the Core toward the owner's Reactor with the least
  path cost. The draft word `pass` is rejected: Arc Relay already distinguishes
  a same-position handoff from a ranged forward pass/arc toss.

The compiler would allow `courier` on a role doctrine and a partial override on
an individual mode, exactly as it does for `fight`. A `fetch` rung still answers
*when this role volunteers*. `courier` answers *how this body chooses and
executes the run*. `custody` still answers *which shared protocol arbitrates the
team*. Those are three different questions and retain three different names.

## Determinism and arbitration

Every preference list needs an engine-owned final tie-break (`core id`, then
actor id). Conflicting body preferences are bids to the existing deterministic
custody reservation pass; they do not permit two bodies to independently claim
one Core. The runtime must attach a non-empty reason to selection, abandonment,
and handoff so the replay can explain which authored rung won.

Before implementation, a focused design pass still must answer:

1. whether `threats` counts visible enemies within a fixed game-rule radius or
   needs a named condition fact;
2. whether `deepest` is strategically useful enough to ship or should be
   omitted until a real sheet requires it;
3. whether every current `forwardPass`, bait-drop, and accidental-pickup case
   can consume the same receiver preference without changing frozen behavior;
4. how a mode override merges ordered `prefer` lists (replace is recommended;
   list concatenation would be surprising).

No compiler, mind, engine, or source-schema behavior in Phase 3 implements this
proposal.
