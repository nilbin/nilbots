# BOTNAME

A Bot Arena bot. Edit `BOTNAME.cs`, then:

If `botarena` is not installed globally, expose the checkout's fast wrapper
once per shell from the repository root:

```bash
export PATH="$PWD/scripts:$PATH"
```

```bash
botarena play --runtime in-process --bot . --opponent hunter --seed 42   # fastest loop
botarena play --bot . --opponent hunter --seeds 7,42,1337   # batch a seed matrix
botarena set --bot . --opponent hunter                      # the ranked 6-game format
botarena replay out/<match>/replay.json --summary           # compact loss forensics
#   quiet stretches are sampled every ~25 ticks — add --full when exact
#   tick-by-tick movement matters (short loops can look like teleports)
botarena build .            # compile to WASM (cached; artifact also at out/bot.wasm)
botarena play --bot . --opponent hunter --seed 42   # official WASM sandbox (exact)
botarena watch . --opponent hunter --seed 42 --runtime in-process   # replay on every save
```

**Iterate in-process, verify in WASM.** `--runtime in-process` builds your bot
as a plain .NET assembly in seconds and runs the exact same engine and
deterministic per-bot randomness — perfect for the edit→watch→improve loop.
It does NOT enforce the fuel/memory limits or the sandbox clock/entropy
neutralization, so before you submit, run once in the default WASM mode (the
same sandbox the server uses) to confirm nothing changes. Compare *results
and summaries* across runtimes, not replay hashes — the runtime kind is part
of a replay's identity, so in-process and WASM hashes differ by design.

**Coordinates:** (0,0) is the top-left tile; x grows east, **y grows south**
— so North is (0,−1) and South is (0,+1).

Practicing for a rules experiment? Add `"rules": "<name>"` to `botarena.json`
and every `play`/`set` defaults to that ruleset (an explicit `--rules` flag
still wins) — no more losing a practice session to one dropped flag.
Experimental observation collections such as `VisibleProjectiles` and
`HeardSounds` are nullable because official rulesets may not enable them;
check for null (or use `?? []`) before iterating.

Programmed-shot experiments are capability-gated the same way. When
`context.ShotPrograms` is non-null, it gives the legal numeric envelope and
you may return `Actions.Shoot(new ShotProgram(...))`; otherwise use ordinary
`Actions.Shoot()`. The committed future path is private. Visible bolts expose
only their exact current eight-way `Heading`, while `ShotPaths.Preview(...)`
lets you enumerate your own candidate path against a wall predicate:

```csharp
if (context.CanShoot && context.ShotPrograms is { } limits)
{
    var arc = new ShotProgram(
        InitialAimOffset: -1, BendDirection: 1,
        BendAfterTiles: 2, BendEveryTiles: 1, BendCount: 2);
    if (limits.IsValid(arc))
    {
        var path = ShotPaths.Preview(
            context.Position, context.Facing, arc, limits.MaxPathTiles,
            context.IsWall);
        return Actions.Shoot(arc);
    }
}
```

`context.IsWall` covers current vision only; pass your own remembered-wall
predicate once you maintain terrain memory. `InitialAimOffset` and
`BendDirection` use 45-degree octants (`-1` left, `+1` right). Diagonal paths
obey strict corners.

`botarena play` prints where it wrote `replay.json` and `viewer.html`
(default: `out/<bot>-vs-<opponent>-<map>-s<seed>/`, so parallel runs never
overwrite each other; `--out <dir>` pins a directory). Open the viewer in a
browser, click your bot, and inspect what it saw and why it acted.

## Rules that decide matches (v0.4)

- **The zone is the objective.** Every map declares zone tiles
  (`context.ZoneTiles` — the full list from tick 0, not gated by vision; some
  maps split the zone into disconnected pads). At the end of every tick you
  are alive and the **sole** bot standing on zone tiles you gain 1 zone-tick;
  a **contested** zone (both bots on it) pays nobody — evict, don't share.
  **150 zone-ticks wins immediately** (Domination). Scores are public:
  `context.MyZoneTicks` / `context.EnemyZoneTicks` — a frozen counter while
  you stand on the zone proves the enemy is on it too, even unseen.
- Spawn positions/facings vary by match seed, never share a clear firing
  lane, and are zone-distance-fair (within 2 walking steps of each other to
  the zone) — don't hardcode an opening. Ranked sets play both spawns of the
  same seed, so starts are fair.
- 5 actions (Wait / MoveForward / TurnLeft / TurnRight / Shoot), one per tick,
  both bots decide simultaneously from the pre-tick state.
- 3 HP. Shooting is an instant ray in your facing with **range 8**: the first
  wall or bot within range stops it. 2-tick cooldown (a shot every 3rd tick).
- Vision is omnidirectional (facing doesn't matter), Chebyshev range 6, and
  **corner-strict**: if the sight line touches a wall — corners included — the
  tile is hidden. Even a *diagonally adjacent* wall can be invisible, and
  `IsWall()` returns false for unseen tiles — remember the map yourself.
  Shots still outrange sight (8 > 6) — you can be hit by (and hit) a bot you
  cannot see, if it's straight ahead down a clear line within range.
- `context.Slot` is your slot; a `VisibleEvent.Slot` is the *acting* bot (for
  Damage: the dealer). Events describe last tick and are delivered when part
  of them is inside your current vision — distant muzzle flashes included.
- Resolution order each tick: turn → move → shoot → damage (simultaneous);
  shots resolve on the post-move board, and a shooter never moves that tick.
  Both bots destroyed on the same tick = **draw**.
- If nobody wins earlier, tick 500 breaks the tie by **zone-ticks**, then
  health, then damage dealt — a health lead *without* the zone loses.
  Camping off the hill is a losing strategy.
- A bot that faults 3 times (exception, infinite loop, out-of-memory) is
  disqualified.

Randomness must come from `context.Random` — wall clocks and `System.Random`
are neutralized in the sandbox. `context.Random` is seeded from the match seed
and your slot, so the same seed always replays the same match (that's how you
debug losses).
