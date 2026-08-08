# GateStone

A Frontline Labs **bulwark** built around the aegis shell rather than fitted
with one. Wave-4 entrant, fresh lineage; the doctrine, the measured per-arm
records and the frictions it surfaced are in [`DX.md`](DX.md).

Its whole policy comes from three contract reads and one piece of arithmetic:

- **Weight decides where a body stands.** While net objective weight is
  positive the claim already builds, so the marginal body takes a *shoulder*
  tile — one the contract permits a stance on — with a firing lane onto the same
  ground. At net zero or below, every point of weight beats any amount of fire,
  and that body walks back onto the objective.
- **The shield is a stance, not a parry.** A windup-1 entry completes after
  combat and a bulwark sees four tiles, so it can never meet a bolt already in
  the air. GateStone raises the arc pre-emptively, on the two ticks in three
  that a cooldown-3 gun is idle, and lowers it the tick the gun comes back.
- **The hold clock is published, so it is priced.** Inside an enemy hold a
  completed capture is spent; with a claim one tick from the threshold and
  nobody contesting, GateStone steps off its own objective (this decay clock
  preserves a claim on empty ground) and returns when the hold lapses.

Nothing is arm-specific. The guard route, the fortify route, the bend envelope,
the capture policy and the movement coupling are all read from
`StartLife.Contract`, so one artifact plays kit-off/kit-on × bend-off/bend-on and
the classless qualification profile.

## Files

| file | what it holds |
| --- | --- |
| `GateStone.cs` | the `IGenericActorBot`: per-form decision ladders |
| `StoneContract.cs` | the one-time contract read (routes, tags, capture policy, envelopes) |
| `StoneGround.cs` | push pricing, station choice, facing-aware routing |
| `StoneAim.cs` | fire control: programs, arrival ticks, guard-arc awareness |
| `StoneMemory.cs` | the little a life may remember, all of it re-derivable |
| `ArenaBasics.cs` | the unmodified scaffold helpers |

## Reproducing

```bash
nilbots build . --no-cache          # sha256 b0d74dafaf6aff9c8dc01876447c913513937e3c3b125e2675d147a1bc09bb8b

nilbots experiment frontline-labs qualify \
  --bot out/bot.wasm --suite frontline-qualification-5 --out evidence/t4

# the primary doctrine cell (registered token `rig`)
nilbots experiment frontline-labs \
  --bot out/bot.wasm --opponent <baseline>/out/bot.wasm \
  --classes bulwark-vs-bulwark \
  --pendulum keel --skills kit --bend universal --movement facing-locked \
  --seed 104729 --runtime wasm
```

Evidence replays are stored gzipped; `gunzip -k evidence/**/replay.json.gz`
before `nilbots verify`.
