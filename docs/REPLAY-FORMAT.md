# Replay format (replay version 1)

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
| `participants[]` | `{ slot, name, runtimeKind, artifactHash, accent, spawnX, spawnY, spawnFacing }` |

Facings are `North | East | South | West`.

## `ticks[]`

One entry per simulated tick: `{ tick, bots, events, state }`.

- `bots[]` — each bot's decision that tick:
  `{ slot, chosenAction, validatedAction, result, faulted?, debug?, visibleTiles, visibleEnemies }`.
  `chosenAction` is what the bot asked for, `validatedAction` what the engine
  accepted (an illegal ask becomes `Wait`), `result` is the
  `PreviousActionResult` the bot will see next tick (`Success`, `Blocked`,
  `OnCooldown`, …). `visibleTiles` is `[x, y]` pairs; `visibleEnemies` is
  `{ slot, x, y, facing, health }`. `debug` is that bot's `Debug.Write` output.
  Debug lines are part of the canonical (hashed) replay and therefore as public
  as the replay itself — the server cannot strip them per-viewer without
  breaking hash verification (DECISIONS #39).
- `events[]` — authoritative flat records, ordered by resolution:
  `{ type, slot?, fromX?, fromY?, toX?, toY?, fromFacing?, toFacing?, hitSlot?, targetSlot?, amount?, newHealth?, message? }`
  with `type` ∈ `Turn | Move | MoveBlocked | Shot | Damage | Destroyed | Fault |
  Disqualified`. A `Shot`'s `toX/toY` is where the ray stopped (wall or bot);
  `Damage.slot` is the shooter, `targetSlot` the victim.
- `state[]` — post-tick truth per bot:
  `{ slot, x, y, facing, health, cooldown, status }` with `status` ∈
  `Active | Destroyed | Disqualified`.

## `result`

`{ winnerSlot, reason, endTick, bots }` — `winnerSlot` is `null` on a draw
(omitted in canonical JSON), `reason` ∈ `Elimination | Disqualification |
MaxTicks`, and `bots[]` is
`{ slot, outcome, finalHealth, damageDealt, faults, finalStatus }` with
`outcome` ∈ `Win | Loss | Draw`.
