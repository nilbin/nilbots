# BOTNAME

A Bot Arena bot. Edit `BOTNAME.cs`, then:

```bash
botarena build .            # compile to WASM (cached; only rebuilds on changes)
botarena play --bot . --opponent hunter --seed 42
botarena watch . --opponent hunter --seed 42   # rebuild + replay on every save
```

`botarena play` writes `out/replay.json` and `out/viewer.html` — open the
viewer in a browser, click your bot, and inspect what it saw and why it acted.

Rules of the arena: 5 actions (Wait / MoveForward / TurnLeft / TurnRight /
Shoot), 3 HP, vision range 6 blocked by walls, shooting is an instant ray with
a 2-tick cooldown. Randomness must come from `context.Random` — wall clocks and
`System.Random` are neutralized in the sandbox and will get your submission
rejected later.
