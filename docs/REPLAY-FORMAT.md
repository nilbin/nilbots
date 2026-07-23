# Replay format (replay version 1)

> Quick digest instead of raw JSON: `botarena replay <file> --summary` prints a
> compact timeline (states, shots, damage, debug lines) built on the
> conventions below.

## How to read a replay (the conventions that bite)

- **`ticks[i].state` is the POST-tick state** — positions, facings, health and
  cooldowns *after* tick `i` resolved. The decisions on the same entry
  (`ticks[i].bots[].chosenAction`) were made from the PRE-tick state, i.e.
  `ticks[i-1].state`. A `TurnRight` on tick `i` already shows the turned facing
  in tick `i`'s state.
- **`Damage.slot` is the DEALER; `targetSlot` is the victim** (the event sits
  at the victim's tile). Every event's `slot` is the *acting* bot.
- A `Shot`'s `toX/toY` is where the ray stopped: the wall it hit, or the tile
  of the bot it hit (`hitSlot` set on hits).
- `bots[].debug` is **absent** (not empty) on ticks where the bot wrote
  nothing; both players' debug lines are public once the replay is revealed
  (DECISIONS #39).
- `cooldown` counts 2 → 1 → 0 on the ticks after a shot: shoot on tick t, and
  ticks t+1/t+2 show cooldown 2/1, shootable again on t+3.
- Position encodings differ by section (historical, pinned by the hash):
  `visibleTiles` are `[x, y]` pairs, `state` uses `{x, y}` fields, events use
  `fromX/fromY/toX/toY`.

The file `botarena play` writes (and `GET /api/matches/{id}/replay` returns) is
one JSON document: `{ header, ticks, result, replayHash }`. Canonical encoding:
camelCase property names, enums as strings, `null` fields omitted, no
whitespace, properties in declaration order. **`replayHash` is SHA-256 over the
canonical JSON of `{ header, ticks, result }` only** (the hash field itself is
excluded); `botarena verify <replay.json>` recomputes it. Any change to this
shape is a replay-format version change.

The authoritative shapes live in `src/BotArena.Engine/Replay.cs`,
`GameEvent.cs` and `MatchResult.cs`; the TypeScript mirror the viewer uses is
`web/src/types.ts`. Summary:

## `header`

| Field | Meaning |
| --- | --- |
| `replayVersion` | Format version (this document: 1) |
| `engineVersion`, `gameRulesVersion`, `runtimeProtocolVersion`, `runtimeConfigurationVersion` | The version axes that pin gameplay |
| `mapId`, `mapVersion`, `mapWidth`, `mapHeight` | Which arena |
| `mapTiles` | Array of row strings, `#` = wall, `.` = floor — the viewer is self-contained |
| `seed` | Match seed (unsigned 64-bit) |
| `maxTicks`, `visionRange` | Rule values the viewer needs |
| `zoneTiles` | `[x, y]` pairs of the zone (rules with zone control); omitted otherwise — pre-zone hashes are unaffected |
| `participants[]` | `{ slot, name, runtimeKind, artifactHash, accent, spawnX, spawnY, spawnFacing }` |

Facings are `North | East | South | West`.

## `ticks[]`

One entry per simulated tick: `{ tick, bots, events, state, projectiles? }`.

- `bots[]` — each bot's decision that tick:
  `{ slot, chosenAction, validatedAction, result, faulted?, debug?, visibleTiles, visibleEnemies, heardSounds? }`.
  `chosenAction` is what the bot asked for, `validatedAction` what the engine
  accepted (an illegal ask becomes `Wait`), `result` is the
  `PreviousActionResult` the bot will see next tick (`Success`, `Blocked`,
  `OnCooldown`, …). `visibleTiles` is `[x, y]` pairs; `visibleEnemies` is
  `{ slot, x, y, facing, health }`. `heardSounds` (rules with hearing, and
  only on ticks with any) is `{ type, bearing, distance }` — the REDACTED
  form the bot itself received: bearing octant `0`=N clockwise to `7`=NW,
  distance band `0` near / `1` medium / `2` far. `debug` is that bot's
  `Debug.Write` output. Debug lines are part of the canonical (hashed) replay
  and therefore as public as the replay itself — the server cannot strip them
  per-viewer without breaking hash verification (DECISIONS #39).
- `events[]` — authoritative flat records, ordered by resolution:
  `{ type, slot?, fromX?, fromY?, toX?, toY?, fromFacing?, toFacing?, hitSlot?, targetSlot?, amount?, newHealth?, message? }`
  with `type` ∈ `Turn | Move | MoveBlocked | Shot | Damage | Destroyed | Fault |
  Disqualified`. A `Shot`'s `toX/toY` is where the ray stopped (wall or bot) —
  under projectile rules it is the LAUNCH tile and an eventual hit arrives as
  a later `Damage`; `Damage.slot` is the shooter, `targetSlot` the victim.
- `state[]` — post-tick truth per bot:
  `{ slot, x, y, facing, health, cooldown, status, energy?, zoneTicks? }` with
  `status` ∈ `Active | Destroyed | Disqualified`. `energy` appears only under
  rules with an energy system. `zoneTicks` (cumulative) appears under rules
  with per-tick zone tallies (the hardened 0.5 arms onward) — read it rather
  than re-deriving accrual; on older zone replays derive by the accrual rule
  or read the totals from `result`.
- `projectiles[]` — bolts in flight after this tick (projectile rules only):
  `{ x, y, direction, ownerSlot, ticksUntilAdvance, remainingTiles }`.
  `ticksUntilAdvance` = 1 means the bolt moves on the NEXT tick, right after
  movement; `remainingTiles` is residual range (−1 = uncapped), lethal on its
  final tile.

## `result`

`{ winnerSlot, reason, endTick, bots }` — `winnerSlot` is `null` on a draw
(omitted in canonical JSON), `reason` ∈ `Elimination | Disqualification |
MaxTicks | Domination` (Domination = the zone threshold was reached), and
`bots[]` is
`{ slot, outcome, finalHealth, damageDealt, faults, finalStatus, zoneTicks? }`
with `outcome` ∈ `Win | Loss | Draw`; `zoneTicks` appears only under rules
with zone control. All optional fields follow one rule: absent under
rulesets that predate them, so historical replay hashes never change.
