# BOTNAME

A Bot Arena bot. Edit `BOTNAME.cs`, then:

```bash
botarena build .            # compile to WASM (cached; only rebuilds on changes)
botarena play --bot . --opponent hunter --seed 42
botarena watch . --opponent hunter --seed 42   # rebuild + replay on every save
```

`botarena play` prints where it wrote `replay.json` and `viewer.html`
(default: `out/<bot>-vs-<opponent>-<map>-s<seed>/`, so parallel runs never
overwrite each other; `--out <dir>` pins a directory). Open the viewer in a
browser, click your bot, and inspect what it saw and why it acted.

## Rules that decide matches (v0.1)

- 5 actions (Wait / MoveForward / TurnLeft / TurnRight / Shoot), one per tick,
  both bots decide simultaneously from the pre-tick state.
- 3 HP. Shooting is an instant ray in your facing with **unlimited range** —
  the first wall or bot stops it. 2-tick cooldown (a shot every 3rd tick).
- Vision is range 6 and **corner-strict**: if the sight line clips a wall
  corner, you can't see past it. Shots outrange sight — you can be hit by (and
  hit) a bot you cannot see, if it's straight ahead down a clear line.
- Resolution order each tick: turn → move → shoot (from post-move positions) →
  damage (simultaneous). Both bots destroyed on the same tick = **draw**.
- If nobody wins by tick 500, the bot with more health wins; equal health is a
  draw. Passivity is a losing strategy against anyone who lands one hit.
- A bot that faults 3 times (exception, infinite loop, out-of-memory) is
  disqualified.

Randomness must come from `context.Random` — wall clocks and `System.Random`
are neutralized in the sandbox. `context.Random` is seeded from the match seed
and your slot, so the same seed always replays the same match (that's how you
debug losses).
