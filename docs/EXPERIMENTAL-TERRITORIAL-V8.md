# Experimental territorial v8: player guide

This is the single player-facing brief for the unshipped
`cone-occupancy-bolt2-arcs` experiment
(`0.5-exp-cone-occupancy-bolt2-arcs-v8`). It supplements the shipped 0.4
documentation. Bot authors may use this guide, the public SDK, template, and
CLI without reading engine or opponent source.

Pin the experiment in `botarena.json` so a dropped CLI flag cannot put practice
matches on the wrong rules:

```json
{
  "rules": "cone-occupancy-bolt2-arcs"
}
```

## Rules at a glance

| Surface | Territorial v8 |
| --- | --- |
| Objective | Shared signed pressure; positive favors slot 0, negative slot 1 |
| Sole zone occupant | Gains 1 pressure after the tick, with any action |
| Both bots in zone | Existing pressure decays 1 toward zero every 2 ticks |
| Empty zone | Existing pressure decays 1 toward zero every 2 ticks |
| Domination | `+100` for slot 0 or `-100` for slot 1 |
| Overtime | From tick 200: limit becomes ±10 and sole gain becomes 2; decay remains |
| Vision | 90° cone toward facing, plus all adjacent tiles |
| Hearing | Loud unseen events within range 8 become redacted sound cues |
| Shot | One launch tile now; then two ordered tiles on every later tick |
| Range / cooldown / damage | 8 tiles / 2 ticks / 1 health |
| Skill shot | Private immutable initial aim and optional repeated 45° bends |

## Territorial control

`context.ZoneTiles` is the complete public zone geometry from tick 0. Scoring
is evaluated after turns, bot movement, projectile movement, new-shot launch,
and simultaneous damage:

- exactly one active bot on any zone tile gains, even when it moved, turned,
  scanned, shot, was blocked, or faulted that tick;
- two active zone occupants contest the whole zone and decay existing pressure;
- no active occupant also decays existing pressure;
- dead and disqualified bots neither score nor contest.

You do not need `Wait` to score. The strategic commitment is physical: occupy
the territory alone. Pushing the opponent completely out turns the same tick
into a scoring tick for the survivor. Read `context.ControlPressure` and
`context.ControlPressureLimit` when non-null; the sign is always slot-based,
not “mine/enemy.”

At the tick limit, pressure sign decides first, then health, then damage.
Elimination can end the match before domination.

## Sight and sound

Directional sight is the forward quadrant where lateral distance is no greater
than forward distance, plus the eight adjacent tiles. Walls still use
corner-strict line of sight. Facing is therefore eyes, movement direction, and
gun direction together.

`context.HeardSounds` contains loud events from the previous tick that were
outside current sight but within hearing range. Each cue exposes only
`Kind`, eight-way `Bearing`, and `Distance` (`Near`, `Medium`, or `Far`).
It never exposes an exact position or slot. An event appears in
`VisibleEvents` or `HeardSounds`, never both.

Both collections are capabilities; use null-safe iteration:

```csharp
foreach (var sound in context.HeardSounds ?? [])
{
    // Investigate, turn, evade, or remember a coarse cue.
}
```

## Fast projectiles

A shot enters the first adjacent path tile during the firing tick. It does not
travel farther until the next tick. On every later tick it takes two ordered
substeps after bot movement. Each substep checks range, walls, strict diagonal
corners, and the first bot encountered, so a speed-two projectile never
teleports through a target or wall.

Projectile tiles are lethal to non-owners. Standing on one or moving onto one
can be hit before it advances; moving into its traversed path can be hit during
the ordered advance. The owner is immune to its own projectile.

For visible projectiles:

- `TicksUntilAdvance == 1` means it advances this tick after bot movement;
- `TilesPerAdvance == 2` is the ordered substep count;
- `RemainingTiles` is the remaining range;
- `Heading` is the exact currently manifested eight-way heading;
- `Direction` is only the original cardinal launch direction.

A revealed `Heading` does not reveal a future programmed bend.

## Programmed skill shots

`context.ShotPrograms` is the exact public numeric envelope. When it is
non-null, `Actions.Shoot(program)` privately commits the projectile to an
immutable path:

```csharp
var limits = context.ShotPrograms;
var program = new ShotProgram(
    InitialAimOffset: 1,
    BendDirection: -1,
    BendAfterTiles: 3,
    BendEveryTiles: 2,
    BendCount: 2);

if (limits is not null && limits.IsValid(program))
{
    var path = ShotPaths.Preview(
        context.Position,
        context.Facing,
        program,
        limits.MaxPathTiles,
        position => rememberedWalls.Contains(position));
    return Actions.Shoot(program);
}
```

`InitialAimOffset` and `BendDirection` are 45° octants:

- initial aim is `-1`, `0`, or `+1`;
- bend direction is `-1` or `+1`;
- first bend is after 1–4 entered tiles;
- later bends repeat every 1–3 tiles;
- a shot may bend 1–3 times in one direction.

`Actions.Shoot()` remains a straight shot. Programs are not homing, random, or
editable after launch. The owner remembers its own program; opponents see only
the projectile's current position and manifested heading. Use
`ShotPaths.Preview` with remembered walls to aim through openings or around
corners; previews obey the engine's strict diagonal-wall rule.

## Fast build and replay loop

Use the managed runtime while changing strategy, then pay for one canonical
WASM verification:

```bash
export PATH="$PWD/scripts:$PATH"
botarena play --bot . --opponent hunter --runtime in-process \
  --rules cone-occupancy-bolt2-arcs --seeds 7,42,1337

botarena build .
botarena set --bot . --opponent <opponent.wasm> \
  --rules cone-occupancy-bolt2-arcs \
  --maps basic-01,arena-01,crossfire-01 \
  --seeds 7,42,1337 --out out/final-set
```

`set --out` creates one subdirectory per game, preserving all six
`replay.json` and `viewer.html` files for comparison. Inspect a loss without
opening raw replay JSON:

```bash
botarena replay out/final-set/g01-basic-01-s7-slot0/replay.json \
  --summary --no-debug
```

On Apple Silicon and Linux arm64, the canonical compiler runs in the cached
Linux/amd64 Docker builder. Linux x64 uses the native compiler when wasi-sdk is
available. The emitted WASM is portable. Strategy edits should stay
in-process; unchanged WASM source is a content-cache hit. See
`docs/WASM-DEVELOPMENT.md` for setup, timing, and troubleshooting.

## Experiment status

This arm is not official 0.5. Its first four-doctrine native holdout passed
combat, activity, repetition, objective, determinism, and blind-viewer gates,
but remained on HOLD because Pincer owned 42.5% of decided wins against the
pre-registered 35% ceiling. The rules stay frozen while counter-doctrines test
whether adaptation broadens the field.
