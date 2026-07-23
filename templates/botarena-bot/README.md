# BOTNAME

A Bot Arena bot. Edit `BOTNAME.cs`, then:

```bash
botarena play --runtime in-process --bot . --opponent hunter --seed 42   # fastest loop
botarena play --bot . --opponent hunter --seeds 7,42,1337   # batch a seed matrix
botarena set --bot . --opponent hunter                      # the ranked 6-game format
botarena replay out/<match>/replay.json --summary           # compact loss forensics
botarena build .            # compile to WASM (cached; artifact also at out/bot.wasm)
botarena play --bot . --opponent hunter --seed 42   # official WASM sandbox (exact)
botarena watch . --opponent hunter --seed 42 --runtime in-process   # replay on every save
```

**Iterate in-process, verify in WASM.** `--runtime in-process` builds your bot
as a plain .NET assembly in seconds and runs the exact same engine and
deterministic per-bot randomness — perfect for the edit→watch→improve loop.
It does NOT enforce the fuel/memory limits or the sandbox clock/entropy
neutralization, so before you submit, run once in the default WASM mode (the
same sandbox the server uses) to confirm nothing changes.

Practicing for a rules experiment? Add `"rules": "<name>"` to `botarena.json`
and every `play`/`set` defaults to that ruleset (an explicit `--rules` flag
still wins) — no more losing a practice session to one dropped flag.

`botarena play` prints where it wrote `replay.json` and `viewer.html`
(default: `out/<bot>-vs-<opponent>-<map>-s<seed>/`, so parallel runs never
overwrite each other; `--out <dir>` pins a directory). Open the viewer in a
browser, click your bot, and inspect what it saw and why it acted.

## Rules that decide matches (v0.3)

- Spawn positions/facings vary by match seed and never share a clear firing
  lane — don't hardcode an opening. Ranked sets play both spawns of the same
  seed, so starts are fair.
- 5 actions (Wait / MoveForward / TurnLeft / TurnRight / Shoot), one per tick,
  both bots decide simultaneously from the pre-tick state.
- 3 HP. Shooting is an instant ray in your facing with **range 8** (v0.3 —
  cross-map lane camping is dead): the first wall or bot within range stops
  it. 2-tick cooldown (a shot every 3rd tick).
- Vision is omnidirectional (facing doesn't matter), Chebyshev range 6, and
  **corner-strict**: if the sight line touches a wall — corners included — the
  tile is hidden. Even a *diagonally adjacent* wall can be invisible, and
  `IsWall()` returns false for unseen tiles — remember the map yourself.
  Shots still outrange sight (8 > 6) — you can be hit by (and hit) a bot you
  cannot see, if it's straight ahead down a clear line within range.
- `context.Slot` is your slot; a `VisibleEvent.Slot` is the *acting* bot (for
  Damage: the dealer). Events describe last tick and are delivered when part
  of them is inside your current vision — distant muzzle flashes included.
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
