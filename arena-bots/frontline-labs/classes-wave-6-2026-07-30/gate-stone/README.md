# GateStone

A Frontline Labs **bulwark** that keeps one ledger and settles every decision in
it. Wave-6 entrant, revising the wave-5 lineage of the same name; the doctrine
delta, the per-rule measured attributions and the frictions it surfaced are in
[`DX.md`](DX.md).

The ledger's unit is **one tick of objective weight**. Under this arm's
net-scaling control policy the signed capture gain is linear in net weight, so
the marginal worth of a body standing on the active objective is exactly its own
weight per tick — and exactly zero while the redeploy pause runs or while a
completion would be spent inside an enemy hold. Everything else is priced
against that.

- **A kill is worth an absence.** A destroyed enemy body is its objective weight
  times the delay its own lifecycle profile declares — eighteen to thirty ticks
  of progress its owner never collects, doubled when nothing brings that slot
  back without an explicit action. That is the only currency an
  objective-weight-zero turret earns in.
- **Fortifying is a lease, not a sale.** `anchor` ⇄ `mobilize` are reversible for
  the whole life, so the turret's faster, longer, eight-headed gun is rented by
  the tick: taken in windows where weight is worth nothing, and given back the
  tick the point starts paying again.
- **Cycle at full health or commit.** Health maps proportionally with a floor in
  both directions, so a full body round-trips losslessly and every partial value
  pays the floor once per leg.
- **The body curve sets the gate's price.** How much relief the gate demands is
  read from the slots each side declares.
- **The shield is a parry, and mostly declined.** A guarding form declares no
  attack profile, so the cooldown does not advance while it is up.

**New in wave 6 — the crew layer prices our OWN bodies' transit.** The wave-5
ledger priced enemy bodies exactly and own bodies not at all, so a relief body
that could not reach the point because the gate body stood in the doorway was
arithmetic the ledger could not see. What ships is one mechanism with three
clauses, each measured with and without on the same twelve games:

- **The transit ledger.** Every body derives its siblings' routes with the same
  planner over the same shared observation — one artifact controls every life and
  team perception publishes each sibling's position, facing and form, so a
  sibling's plan is derivable without a message, a shared memory or a leader. The
  tiles a higher-precedence sibling needs this tick or next are reserved out of
  this body's graph, and a body standing on one steps off. Precedence is the
  **muster order**: a body that can capture outranks one that cannot, then the
  body nearer the active objective, then ascending actor identity — computed from
  shared state alone, so every life derives the same order.
- **The choke precedence rule.** A sibling entering a one-tile corridor is treated
  as committed to the whole corridor, because a corridor is the one place a meeting
  cannot be resolved by stepping aside. Exactly neutral on this map (no corridor
  here is longer than the two-tile reservation) and load-bearing on one whose
  corridors are longer, which suite 5's holdout runs.
- **The rally lane.** The tile our next automatic arrival will take is derived from
  the declared `automaticReturnPlacement` policy, the objective chain and our own
  advance delta — never from the team ID — and kept clear while an arrival is due.

Measured against the rebuilt wave-5 predecessor over the same twelve games:
territorial progress +113 → **+214**, record 8-4-0 → **10-2-0**, refused steps into
a sibling 59 → **11**, ticks wasted queued behind a sibling in a corridor 18 →
**1**, and the sibling detour this bot charges its own team 180 ticks → **61**.

**Four rules that look obviously right lost their own measurement and are not in
the artifact** — including the doorway yield, the single most intuitive fix for the
behaviour that opened the wave. Each is argued out at its call site in the source,
with numbers, so the next wave does not rebuild it; `DX.md` has the tables.

Nothing is arm-specific. The guard route, the fortify route, its reversibility,
the health-transfer policy, the aim and bend envelope, the capture policy, the
placement policy, the lifecycle clocks and the movement coupling are all read
from `StartLife.Contract`, so one artifact plays strict ground, free ground, open
ground, kit-off, kit-on, bend-off, bend-on and the classless qualification
profile.

## Files

| file | what it holds |
| --- | --- |
| `GateStone.cs` | the `IGenericActorBot`: per-form decision ladders and the capture-arithmetic gate |
| `StoneContract.cs` | the one-time contract read (routes, reversibility, health policy, lifecycle clocks, body curve, envelopes, the map's one-tile corridors, the advance direction) |
| `StoneCrew.cs` | the crew layer: muster order, the transit ledger, the choke precedence rule, the rally lane |
| `StoneGround.cs` | push pricing per body, station choice, facing-aware routing with reservations |
| `StoneAim.cs` | fire control: aim offsets and bend programs, arrival ticks, guard-arc awareness |
| `StoneMemory.cs` | the little a life may remember, all of it re-derivable |
| `ArenaBasics.cs` | the unmodified scaffold helpers |

`StoneCrew` carries three `static readonly bool` rule switches. All three are
`true` in the frozen artifact; they exist so the per-rule ablation table in
`DX.md` can be reproduced by flipping one and rebuilding **in a copy outside this
directory** — `nilbots build` globs every `.cs` under the project, so a variant
source left in here breaks the freeze with duplicate-member errors.

## Reproducing

```bash
nilbots build . --no-cache      # -> 06b4ae21ae0393c220cd675933bfe5e2ff6efdeb37f45e7bba701178872a7d93

nilbots experiment frontline-labs qualify \
  --bot out/bot.wasm --suite frontline-qualification-5 --out evidence/t4

# the deck game — `deck` beside a fabricator, `sail-open` where none is in the cell
nilbots experiment frontline-labs \
  --bot out/bot.wasm --opponent <other>/out/bot.wasm \
  --classes bulwark-vs-fabricator --movement facing-locked \
  --pendulum keel --skills kit --bend universal --aim offset \
  --stance-ground open --five-slots wane \
  --seed 104729 --runtime wasm
```

Drop `--five-slots wane` on any pair without a fabricator: it is rejected rather
than inert-omitted. Evidence replays are stored gzipped; `gunzip -k` before
`nilbots verify`. Matches no longer write a `viewer.html` unless you pass
`--viewer` or `--open`; `qualify` still writes one per probe, so expect 213 MB
from the suite before you compress it.
