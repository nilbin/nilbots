# Pressure authoring DX

## Timing and outcome

- Scaffold created: `2026-07-28T08:10:45Z`.
- Source frozen before the build started at `2026-07-28T08:19:26Z`.
- First controlled WASM build completed successfully by
  `2026-07-28T08:19:52Z`.
- Wall-clock time from scaffold creation to first valid build: 9 minutes
  7 seconds. This includes the mandatory shared-documentation pause of about
  3 minutes; active authoring and build time was therefore about 6 minutes.
- Authoring passes: 1.
- Mechanical repairs: 0.
- Matches, replays, opponents, tournament outputs, and runtime results
  inspected: none.

## Documentation and terminology

The initial common packet pointed to a hosted Labs product-boundary section
but did not provide a complete standalone Labs-v1 mechanics card. Authorship
paused before the generated starter was changed. The common packet was then
corrected for every author to add `FRONTLINE-LABS-RULES.md`, and the source was
written only after reading that frozen correction. This was a shared
documentation correction, not a mechanical bot repair.

The new rule card cleanly separates `frontline-labs-1` and
`IGenericActorBot` from the older Frontline alpha and `IActorBot`. Before that
correction, "Frontline" referred to two generations in nearby documentation,
which made it too easy to import alpha mechanics accidentally.

The contract-versus-current-values warning was useful and repeated at the
right points. In particular, it prevented current team count, unit count,
unlock ticks, form names, objective coordinates, and map dimensions from
becoming structural assumptions.

## API and scaffold friction

The generated starter provided valuable examples for resolving action codes,
joining the mode binding to objective regions, computing a first BFS step, and
constructing direction and projectile-heading arguments.

The largest gap was typed legality ergonomics. `Choose` demonstrates
availability and code lookup, but a production bot still has to inspect the
nested constraint union before supplying a direction, target, form, heading,
or shot program. Small SDK helpers such as `TryLegalDirection`,
`TryLegalHeading`, and a validated parameterless-decision factory would reduce
repetitive code and invalid-action risk.

The other awkward join is `self.FormId -> Rules.Forms -> AttackProfileId ->
Rules.AttackProfiles`. It is correct and explicit, but common tasks such as
reading objective weight, range, or shot-program limits require repeated
ordinal catalog lookups. Typed contract lookup helpers would make strategy
code shorter without hiding the contract.

There is no contract helper for shortest-path distance or for resolving the
active Frontline region. The scaffold's BFS was enough to extend safely, but
each author is likely to recreate objective binding, region lookup, occupancy,
and first-step filtering.

## Diagnostics

The controlled build diagnostic was concise and useful. It identified the
entry type, selected WASM runtime capability, SDK version, compiler, cache
status, artifact path, and artifact hash. The first build succeeded with no
compiler errors, so no diagnostic-driven source change was made.

## Hardcoding temptations

The rule card necessarily publishes memorable values: two teams, three unit
slots, five objectives, a 23-by-15 map, specific unlock ticks, and familiar
form IDs. The implementation instead reads topology observations, objective
regions, advance delta, map rows and bounds, form objective weights, attack
profiles, and typed legality values.

Numeric action codes were never copied. Stable optional IDs `split` and
`shoot-direction` are recognized only for doctrine-relevant capabilities and
fall back safely when absent. Fundamental movement, rotation, shooting, and
waiting are also selected through their current legality entries. The bot
does not name current forms or slots.

## Repairs

There were no post-freeze source edits and no mechanical repairs. The shared
rule-card correction occurred before source authorship and is recorded above
without incrementing the repair count.
