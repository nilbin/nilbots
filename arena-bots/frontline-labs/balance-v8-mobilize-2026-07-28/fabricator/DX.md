# Fabricator authoring DX

## Timing and scope

- One strategy-authoring pass was used.
- Time from starting the source pass to the first valid local build was about
  9 minutes 39 seconds. That includes the first compiler-diagnostic cycle and
  a controlled-build attempt that spent five minutes in toolchain startup
  before timing out with an empty log.
- The post-repair local build completed in 1.36 seconds with zero warnings and
  zero errors.
- A retry of the controlled WASM build completed successfully in 22.51
  seconds.
- No match, experiment, replay, tournament output, opponent source, built-in
  policy, or private runtime/Engine implementation was run or inspected.

## Documentation and terminology

The original common packet linked the hosted Labs product boundary but did
not provide a complete standalone Labs-v1 mechanics card. Authorship paused
before scaffold inspection or source editing while the shared packet was
corrected. The frozen packet then linked `FRONTLINE-LABS-RULES.md`, whose
objective, lifecycle, form, transition, projectile, map, and memory sections
were sufficient to author from public inputs. This was a shared documentation
correction and is not counted as a Fabricator repair.

The most important terminology distinction was between three kinds of change:
Fabricate creates a child in another stable Ready slot, Split retires a source
and creates fresh replicated lives, and Anchor is a same-life form transition.
The rule card made the different memory and identity consequences clear.
“Ready” is a slot lifecycle state, not an already active child, which is easy
to misread before seeing the typed observation union.

Mapping an actor back to its own home pad required more API assembly than the
rule card suggests. The bot derives a home spawn from its lifecycle assignment
or initial deployment, then selects the connected `SpawnProtected` component
containing that spawn. A direct public helper for “this participant’s region
for role” or “this life’s protected home tiles” would reduce uncertainty.

## API and scaffold friction

The scaffold was valuable for the core contract-first pattern: store
`StartLife.Contract`, resolve the active objective through the mode binding,
look up each action legality by stable ID, and take the numeric code from that
tick. Its breadth-first objective navigator was also a useful safe base.

The starter `Choose` helper checks action availability but accepts arbitrary
arguments without checking the nested typed constraint union. A small SDK or
scaffold decision builder for direction, unit target, form target, and
projectile heading would remove repetitive casts and make the safest path the
shortest path.

Other helpers that would have reduced ordinary bot code:

- contract catalog lookup by stable ID for forms, attacks, transitions, and
  objective regions;
- a path-distance helper over the static map;
- helpers for objective weight, own home assignment, and connected tile-tag
  regions;
- a legality method such as `TryDirection(actionId, preferredDirection)`;
- an exact cardinal/eight-way alignment helper for projectile fire.

The controlled compiler’s file/line diagnostics were concise and directly
actionable. The five-minute controlled-build timeout was less useful because
the advertised build log stayed empty, making queue delay, Docker startup, and
restore latency indistinguishable. The immediate retry succeeding from the
same source suggests toolchain/cache startup rather than a source fault.

## Hardcoding temptations

Labs-v1 makes it tempting to copy the two-team shape, three unit slots, unlock
ticks 120 and 260, the 23-by-15 map, objective coordinates, the
`prime-mobile`/`child-mobile`/`turret` form names, and current action codes.
Fabricator instead reads topology, lifecycle assignments, forms, transitions,
limits, capture arithmetic, regions, tile tags, attack ranges, and numeric
codes from the contract. It recognizes only doctrine-relevant semantic action
IDs (`fabricate` and fallback `split`) plus ordinary movement/combat actions,
and verifies current typed constraints before supplying arguments.

The authored map format’s `#` wall symbol remains visible in pathfinding, as
demonstrated by the generated scaffold and declared by the player rule card;
dimensions and coordinates are not copied.

## Repairs

Mechanical repair count: **1 pass**.

The first controlled compile reported eight source diagnostics:

- six collection expressions targeted `IReadOnlySet<Position>`, which is not a
  constructible collection-expression target;
- one LINQ tuple projection dropped the intended `Heading` element name;
- one lookout helper retained an unfinished team-ID placeholder.

The single mechanical repair pass introduced a concrete empty-position set,
used concrete singleton sets, preserved the tuple element name, and threaded
the observing team ID into the already-authored objective-approach lookup.
No priorities, thresholds, doctrine choices, or strategy behavior were
revised from match evidence. There were no runtime or contract repairs.

The first controlled-build timeout was retried unchanged and is recorded as
toolchain friction, not a mechanical bot repair.
