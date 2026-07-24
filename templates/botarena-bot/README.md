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
(`vanguard`, `bulwark`, `needle`, or `orbiter`). The look belongs to the bot
and is snapshotted into each match, so historical replays do not change later.

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

## Rules that decide matches (v0.5)

- **The zone is territory.** `context.ZoneTiles` is the full public zone from
  tick 0. After movement, projectiles, and damage, the sole active zone
  occupant gains 1 signed `context.ControlPressure` with **any** action. If
  both bots occupy the zone—or neither does—existing pressure decays 1 toward
  zero every 2 ticks. Positive pressure favors slot 0; negative favors slot 1.
  Reaching ±100 wins by Domination. From tick 200, the limit becomes ±10 and a
  sole occupant gains 2 while decay remains. `MyZoneTicks` / `EnemyZoneTicks`
  are null under this shared meter. Evict, don't merely arrive.
- Spawn positions/facings vary by match seed, never share a clear firing
  lane, and are zone-distance-fair (within 2 walking steps of each other to
  the zone) — don't hardcode an opening. Ranked sets play both spawns of the
  same seed, so starts are fair.
- 5 actions (Wait / MoveForward / TurnLeft / TurnRight / Shoot), one per tick,
  both bots decide simultaneously from the pre-tick state.
- 3 HP. Shooting launches a range-8 projectile onto the first adjacent path
  tile that tick; on each later tick it traverses **two ordered tiles** after
  bot movement. Every intermediate tile checks walls, strict diagonal corners,
  range, and the first bot encountered—speed two never tunnels. A projectile
  tile is lethal to non-owners; hits deal 1 damage; cooldown is 2 ticks.
- `VisibleProjectiles` exposes current position and eight-way `Heading`.
  `TicksUntilAdvance == 1` means it advances this tick after your movement;
  `TilesPerAdvance` and `RemainingTiles` make the danger computable.
- `context.ShotPrograms` lets Shoot privately commit an immutable skill-shot
  path: initial aim −1/0/+1 45° octants, first bend after 1–4 tiles, later
  bends every 1–3 tiles, and 1–3 bends in one direction. Validate with
  `limits.IsValid`, preview with `ShotPaths.Preview`, then return
  `Actions.Shoot(program)`. Opponents see only the projectile's exact current
  eight-way `Heading`, never its future bends. `Actions.Shoot()` stays straight.
- Vision is a 90° cone in your facing direction, plus the eight adjacent
  tiles, Chebyshev range 6, and **corner-strict**. Turning is scanning, aiming,
  and changing movement direction together; your back is genuinely blind.
  Even a diagonally adjacent wall can be hidden by a clipped corner, so
  remember observed terrain.
- Loud unseen Shot/Damage/Destroyed events within range 8 arrive one tick
  later in `context.HeardSounds` as kind + eight-way bearing +
  near/medium/far—never exact coordinates or slots. Full-detail events you see
  stay in `VisibleEvents`; an event is never delivered through both channels.
- `context.Slot` is your slot; a `VisibleEvent.Slot` is the *acting* bot (for
  Damage: the dealer). Events describe last tick. Under cone vision, full
  detail requires the event's primary tile to be visible: the shooter/mover,
  or the damaged/destroyed bot. An unseen loud event is heard only as a
  redacted cue.
- Resolution order: observe → turn → bot movement → existing projectile
  substeps/collisions → new-shot launch → simultaneous damage → territorial
  pressure → victory. Both bots destroyed on the same tick = **draw**.
- If nobody wins earlier, tick 500 breaks the tie by pressure sign, then
  health, then damage dealt. A banked lead decays while the zone stays empty
  or contested.
- A bot that faults 3 times (exception, infinite loop, out-of-memory) is
  disqualified.

Randomness must come from `context.Random` — wall clocks and `System.Random`
are neutralized in the sandbox. `context.Random` is seeded from the match seed
and your slot, so the same seed always replays the same match (that's how you
debug losses).
