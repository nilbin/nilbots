# BOTNAME — a mind

You have written **one program that drives your whole army**. Not one program
per body: one, for the match.

```bash
nilbots experiment frontline-labs --profile mind \
  --bot . --opponent <other-bot> --seed 42 --viewer
```

`--profile mind` selects the participant-scoped contract
(`generic-mind-match-1`). Without it the match runs the per-life profile, where
your `IGenericMindBot` has nothing to do.

## The five rules that are actually different

1. **One instance, whole match.** Nilbots constructs your class once, before
   tick 0, and disposes it after the terminal tick. **Your fields are your
   memory.** Nothing is cleared when a body dies. There is no memory API.
2. **`Think` runs every tick, unconditionally** — from tick 0 to the last tick,
   whether you own nine bodies, one, or none. "Am I alive?" stopped being a
   control-flow question and became `mind.Bodies.Length`.
3. **Commands are written, not returned.** `body.Command(...)`, `body.Hold(...)`.
   Every live body you do not write to **waits**. Forgetting one costs that body
   a tick, visibly, in the replay — not the match. Writing to the same body
   twice throws immediately, because that means you decided twice and did not
   notice.
4. **You can only command what you own.** `mind.Bodies` is yours. `mind.Allies`
   is allied *minds'* bodies in a team format — visible, never commandable, and
   empty in head-to-head.
5. **A mind that traps forgets the match.** There is no snapshot: a runtime
   fault discards the whole instance and its memory, and under the shipped Labs
   contract the first fault also disqualifies you. Robustness is part of your
   doctrine now.

## What is in the box

| File | What it is |
| --- | --- |
| `BOTNAME.cs` | The mind: `StartMatch` / `Think` / `EndMatch`, one small method per role, and a build order that outlives the bodies executing it. |
| `Roles.cs` | **Edit this first.** The assignment function — who channels, who screens, who fetches. |
| `Recall.cs` | Persistent memory, shipped working: enemy last-seen with staleness, the pile ledger. |
| `ArenaBasics.cs` | Contract-driven helpers, mind-shaped. Movement helpers reserve their destination tile so your own bodies never collide. |
| `botarena.json` | Name, entry type, SDK version, appearance. |

## Facts you get for free that a per-life bot did not

- `body.MovedLastTick` and `body.PreviousPosition` — the most-requested platform
  fact of the last cohort, and load-bearing under the capture channel, where
  only bodies that held their tile build a claim.
- `body.LifeStartedTick`, `body.Origin` — per body, on the tick it appears.
- `mind.Slots` — your **complete** slot table, every tick, live or not: pending
  returns with their due ticks, fabrication and replication in progress,
  permanently dormant slots.
- `body.SetRole("channeler")` — a free-vocabulary public label. It shows under
  the body in the viewer and in the bot panel, it is **also published to the
  opponent on any body they can see**, and the engine never reads it. Which
  makes a deliberately wrong label a real move.
- `mind.Debug.Write(...)` — one diagnostic line per **tick**, recorded on the
  mind's replay turn rather than on a body, because a mind reasons once per tick
  for everybody.

## Budgets

- **Fuel:** `250M + 200M × live own bodies` per tick. The per-body term is
  exactly the per-life budget, so per-body compute is unchanged; the base term
  funds the once-per-tick shared thinking and is there even at zero bodies.
- **Memory:** 128 MiB linear memory for the one instance (a per-life bot got
  64 MiB per body).
- **Startup:** 5 billion fuel, paid once per match instead of once per life.

## Where to look next

- `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md` — the arms and what they change.
- `docs/FRONTLINE-LABS-RULES.md` — the contract and the memory model.
- `docs/RUNTIME-PROTOCOL.md` — the mind profile and runtime configuration 2.0.
- Public types and XML comments in `BotArena.Sdk` — `MindContext`, `MindBody`,
  `MindSlot`, `MindStart` all carry their own documentation.

Then qualify it:

```bash
nilbots experiment frontline-labs qualify --profile mind \
  --bot . --suite frontline-mind-qualification-3
```

Out of the box this scaffold is awarded **T1** and passes six of the seven T2
probes — including `body-handoff`, the mind-native one, where the body holding
the objective is destroyed and another of yours takes the point. That is exact
parity with the shipped per-life scaffold, which fails the same single probe:
`straight-evade`, where a bolt comes down an open corridor and the only escape
is off the row. Clearing it is your first real exercise, and it is deliberately
left for you.
