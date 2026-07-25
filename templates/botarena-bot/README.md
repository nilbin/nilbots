# BOTNAME

A nilbots bot. Edit `BOTNAME.cs`, then use the
[Nilbots global tool](https://www.nuget.org/packages/Nilbots):

```bash
nilbots play --runtime in-process --bot . --opponent hunter --seed 42   # fastest loop
nilbots play --bot . --opponent hunter --seeds 7,42,1337   # batch a seed matrix
nilbots set --bot . --opponent hunter                      # the ranked 6-game format
nilbots replay out/<match>/replay.json --summary           # compact loss forensics
#   quiet stretches are sampled every ~25 ticks — add --full when exact
#   tick-by-tick movement matters (short loops can look like teleports)
nilbots build .            # compile to WASM (cached; artifact also at out/bot.wasm)
nilbots play --bot . --opponent hunter --seed 42   # official WASM sandbox (exact)
nilbots watch . --opponent hunter --seed 42 --runtime in-process   # replay on every save
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

Choose the bot's replay chassis with `appearance.look` in `botarena.json`
(`vanguard`, `bulwark`, `needle`, `orbiter`, or `lancer`) and its projectile
with `appearance.projectile` (`pulse-bolt`, `ion-orb`, `razor-shard`, or
`arc-spark`). Appearance belongs to the bot and is snapshotted into each match,
so historical replays do not change later. You can also change accent, chassis,
and projectile from the bot page without submitting a new code version.

Practicing for a rules experiment? Add `"rules": "<name>"` to `botarena.json`
and every `play`/`set` defaults to that ruleset (an explicit `--rules` flag
still wins) — no more losing a practice session to one dropped flag.
Observation capabilities such as `VisibleProjectiles`, `HeardSounds`, and
`ShotPrograms` are nullable because historical rulesets may not enable them;
check for null (or use `?? []`) before iterating. They are present in current
rules 0.5.

Programmed shots are capability-gated the same way. When
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

`nilbots play` prints where it wrote `replay.json` and `viewer.html`
(default: `out/<bot>-vs-<opponent>-<map>-s<seed>/`, so parallel runs never
overwrite each other; `--out <dir>` pins a directory). Open the viewer in a
browser, click your bot, and inspect what it saw and why it acted.

<!--BOTARENA_RULES-->
