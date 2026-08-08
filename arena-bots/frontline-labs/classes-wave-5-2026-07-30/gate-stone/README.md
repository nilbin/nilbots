# GateStone

A Frontline Labs **bulwark** that keeps one ledger and settles every decision in
it. Wave-5 entrant, revising the wave-4 lineage of the same name; the doctrine,
the measured records and the frictions it surfaced are in [`DX.md`](DX.md).

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
  tick the point starts paying again. Free placement means the cheapest tile to
  fortify on is often the objective tile itself — a turret there scores nothing,
  but it is one tick from scoring again and it denies the tile to a body that
  would.
- **Cycle at full health or commit.** Health maps proportionally with a floor in
  both directions, so a full body round-trips losslessly and every partial value
  pays the floor once per leg. Below full, the gate reverts to the terms that
  justified a permanent anchor.
- **The body curve sets the gate's price.** How much relief the gate demands is
  read from the slots each side declares: outnumbered, one spare body is all the
  relief that will ever exist; level or ahead, presence is the cheaper lever.
- **The shield is a parry, and mostly declined.** A guarding form declares no
  attack profile, so the cooldown does not advance while it is up — a shielded
  tick banks nothing and costs a full tick of fire. The arc rises only against a
  bolt already in the air and already inside it, with nothing leaking round the
  side, and comes down the moment there is nothing left to turn.

Nothing is arm-specific. The guard route, the fortify route, its reversibility,
the health-transfer policy, the aim and bend envelope, the capture policy, the
lifecycle clocks and the movement coupling are all read from
`StartLife.Contract`, so one artifact plays strict ground, free ground, open
ground, kit-off, kit-on, bend-off, bend-on and the classless qualification
profile.

## Files

| file | what it holds |
| --- | --- |
| `GateStone.cs` | the `IGenericActorBot`: per-form decision ladders and the capture-arithmetic gate |
| `StoneContract.cs` | the one-time contract read (routes, reversibility, health policy, lifecycle clocks, body curve, envelopes) |
| `StoneGround.cs` | push pricing, the weight-per-tick unit, station choice, facing-aware routing |
| `StoneAim.cs` | fire control: aim offsets and bend programs, arrival ticks, guard-arc awareness |
| `StoneMemory.cs` | the little a life may remember, all of it re-derivable — including its own dwell in the current form |
| `ArenaBasics.cs` | the unmodified scaffold helpers |

## Reproducing

```bash
nilbots build . --no-cache

nilbots experiment frontline-labs qualify \
  --bot out/bot.wasm --suite frontline-qualification-5 --out evidence/t4

# the crew game — `deck` beside a fabricator, `sail-open` where none is in the cell
nilbots experiment frontline-labs \
  --bot out/bot.wasm --opponent <other>/out/bot.wasm \
  --classes bulwark-vs-fabricator --movement facing-locked \
  --pendulum keel --skills kit --bend universal --aim offset \
  --stance-ground open --five-slots wane \
  --seed 104729 --runtime wasm
```

Drop `--five-slots wane` on any pair without a fabricator: it is rejected rather
than inert-omitted. Evidence replays are stored gzipped; `gunzip -k` before
`nilbots verify`.
