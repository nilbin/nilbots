# Frontline duel-depth experiment

Status: completed as a local-only one-pass usability/DX screen. See
[`EXPERIMENTAL-FRONTLINE-DUEL-DEPTH-RESULTS.md`](EXPERIMENTAL-FRONTLINE-DUEL-DEPTH-RESULTS.md)
for the rejected arm-policy evidence and
[`FRONTLINE-DUEL-THEORY.md`](FRONTLINE-DUEL-THEORY.md) for the later
engine-path analysis. The one-bend mechanic remains a reference hypothesis;
this document does not change hosted `frontline-labs-1` or declare 1v1
balanced.

## Question

Can the real Frontline opening produce a non-trivial duel before companion
bodies add coordination, or does one simple geometric dodge policy preserve
both health and territory against every attack?

The experiment retains the hosted map, topology, deployment, forms, health,
vision, projectile damage/range/cadence, objective rules, lifecycle, and
companion unlocks. The opening through tick 119 is therefore the native
Prime-versus-Prime isolation window. Unit 1 still unlocks at tick 120 and unit
2 at tick 260, letting a successful opening snowball into the ordinary
early/mid/late structure.

## Candidate contract

Run with:

```bash
nilbots experiment frontline-labs \
  --bot <generic-spec> \
  --opponent <generic-spec> \
  --one-bend-shots
```

The resolved ruleset is
`frontline-labs-1-experiment-one-bend-shots`. Mobile shots have only these
choices:

- omit the payload and fire straight along current facing; or
- commit one private 45-degree left or right bend after 1–4 travelled tiles.

Initial aim offset is exactly zero. Repeated bends are unavailable. Turret
fire remains straight and omnidirectional.

Movement still resolves before an existing projectile advances. Opponents see
the projectile's current heading, position, remaining distance, and exact
advance timing, but never its committed future bend. Physics and replay remain
deterministic; strategic uncertainty comes from the unknown opponent policy
and private committed choice, not random engine outcomes.

## Entry qualification component

Run one frozen artifact through the mirrored opening-entry probe with:

```bash
nilbots experiment frontline-labs qualify \
  --bot <generic-project-or-wasm> \
  --suite frontline-qualification-1 \
  --out <evidence-dir>
```

The distinct qualification contract starts the two Primes at `(8,7)` and
`(14,7)`, removes companion slots/actions, ends after tick 119, and uses the
same one-bend combat physics. A deterministic public-SDK sentinel applies
straight pressure. Passing requires life 0 to reach the central objective
from both team assignments. Exit `3` is a clean probe failure; exit `2` is
runtime/contract invalidity.

This is one T4 positional component. `qualification.json` deliberately awards
no cumulative tier until T1–T3 prerequisite probes exist.

## One-pass micro-cohort

This is the frozen historical T1/T2/DX screen. Higher balance claims now use
the tiers and probes in
[`BOT-QUALIFICATION-SUITE.md`](BOT-QUALIFICATION-SUITE.md).

Retain four source-complete policies:

1. `GeometricDodger` — strongest simple public-heading evasion baseline; no
   curve or opponent model.
2. `TerritoryHolder` — values control and firing tempo over reflexive evasion.
3. `CurvePredictor` — uses one private bend to predict or force movement.
4. `AdaptiveMixer` — varies straight/left/right commitments from observed
   responses and its deterministic private random stream.

Each receives one authoring pass plus compile or runtime-fault repair only.
No entrant sees another source, replay, or result before artifact freeze.
Every source revision and final WASM is retained.

Use seed `104729` for every mirrored unordered pairing. Because
`AdaptiveMixer` consumes private randomness, add seeds `130363` and `155921`
only to its three pairings. This produces 24 matches rather than a repeated
full 36-match tournament.

## Opening evidence and gates

Analyze ticks 0–119 separately from the complete match. Record per entrant:

- straight and curved attacks launched;
- damage dealt and lives destroyed;
- imminent threats while holding the active objective;
- movement chosen when an obvious attack was also available;
- objective-holding moves, objective-leaving responses, pushes, and signed
  territorial score at the isolation boundary;
- complete-match result and whether the opening advantage survives companion
  unlocks.

Safety requires complete replay v3, exact contract/hash verification, and zero
faults or disqualifications in every match.

The 1v1 foundation fails if `GeometricDodger` takes no opening damage, concedes
no objective pressure, and is not territorially disadvantaged against either
curve-capable policy from either assignment. It also fails if curved policies
cannot demonstrate their declared action or if their only advantage appears
after companions unlock.

It passes provisionally when private trajectory commitment creates opening
damage or a measurable territorial/firing-tempo concession against the
geometric baseline from both assignments, no one simple response dominates
the micro-cohort, and an outcome-blind reviewer can identify the commitment,
defensive choice, and consequence at normal replay speed.

Passing does not prove the duel is permanently unsolvable. It establishes
that the smallest current action set supports adversarial prediction rather
than routine projectile arithmetic, which is enough to justify layering
companions, transforms, and coordination on top.
