# Replay format (replay version 1)

This remains the exact shipped replay-v1 contract. Its canonical bytes,
verification, fixtures, and hashes are unchanged.

An observation-complete replay v2 now exists on the **local Frontline
experimental path**. It records the immutable public match contract,
team/unit/life topology, exact per-actor pre-tick observations and legality
masks, decisions/resolutions, lifecycle/form-transition events, authoritative
post-state, and terminal stable-unit results. The web viewer has a
version-neutral normalization layer for v1 and this experimental v2.
`nilbots experiment frontline` writes canonical v2 JSON and directly embeds
it in a self-contained Canvas2D viewer. Historical `play`, App/server matches,
and the ladder still emit only v1. Replay v2 is not yet a stable public
format, and the general `replay --summary` and `verify` commands remain
v1-only; its architecture and ML use are described in
[`EXPERIMENTAL-FRONTLINE.md`](EXPERIMENTAL-FRONTLINE.md) and
[`REPLAY-NATIVE-ML-PLAN.md`](REPLAY-NATIVE-ML-PLAN.md). Do not normalize v1 to
v2 before verifying a stored v1 hash.

Replay-v2 dynamics are read separately:

```bash
python3 scripts/frontline-replay-eval.py \
  --group current=/tmp/frontline/block-1 \
  --group current=/tmp/frontline/block-2 \
  --json /tmp/frontline/report.json
```

That analyzer accepts only complete version-2 documents with contiguous
`0..result.endTick` frames. It reports raw/descriptive dimensions and does not
assign a fun score or verify the canonical hash; replay-v2 admission and a
public verification surface remain hosted-product follow-ons.

> For replay v1, a quick digest instead of raw JSON is available through
> `botarena replay <file> --summary`. It prints a compact timeline (states,
> shots, damage, debug lines) built on the conventions below.

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
`GameEvent.cs` and `MatchResult.cs`. The viewer's version-specific wire mirror
is `web/src/replayWireV1.ts`; `replayWireV2.ts` preserves the separate
Frontline-alpha generation, and both normalize into `web/src/replayModel.ts`.
A future generic generation receives its own `replayWireV3.ts` rather than
widening this frozen v1 shape. Summary:

## `header`

| Field | Meaning |
| --- | --- |
| `replayVersion` | Format version (this document: 1) |
| `engineVersion`, `gameRulesVersion`, `runtimeProtocolVersion`, `runtimeConfigurationVersion` | The version axes that pin gameplay |
| `mapId`, `mapVersion`, `themeId`, `presentation`, `mapWidth`, `mapHeight` | Which arena, its map-owned theme, and wall-family placement (`themeId` / `presentation` are omitted only for legacy/synthetic maps) |
| `mapTiles` | Array of row strings, `#` = wall, `.` = floor — the viewer is self-contained |
| `seed` | Match seed (unsigned 64-bit) |
| `maxTicks`, `maxHealth`, `visionRange` | Rule values the viewer needs; `maxHealth` is omitted only for historical/default three-health replays |
| `zoneTiles` | `[x, y]` pairs of the zone (rules with zone control); omitted otherwise — pre-zone hashes are unaffected |
| `controlPressureLimit` | Absolute shared-pressure domination limit (active-control rules only) |
| `controlBySoleOccupancy` | `true` when one physical occupant gains and a contested/empty zone decays; omitted for successful-Wait control and historical hashes |
| `participants[]` | `{ slot, name, runtimeKind, artifactHash, accent, lookId, spawnX, spawnY, spawnFacing }`; `lookId` is bot-owned and omitted only for legacy replays |

Facings are `North | East | South | West`.

## `ticks[]`

One entry per simulated tick:
`{ tick, bots, events, state, projectiles?, projectileTraversals?, controlPressure? }`.

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
  rules with an energy system. `zoneTicks` (cumulative) appears under passive
  rules with per-tick zone tallies — read it rather than re-deriving accrual;
  active-control rules use the shared `controlPressure` field instead.
- `projectiles[]` — bolts in flight after this tick (projectile rules only):
  `{ x, y, direction, ownerSlot, ticksUntilAdvance, remainingTiles,
  tilesPerAdvance, id }`. `ticksUntilAdvance` = 1 means the bolt advances on
  the NEXT tick, right after movement; `tilesPerAdvance` is its ordered
  substep count; `remainingTiles` is residual range (−1 = uncapped), lethal
  on its final tile.
- `projectileTraversals[]` — authoritative movement during this tick:
  `{ id, ownerSlot, direction, fromX, fromY, path }`, where `path` is the
  ordered list of entered `[x,y]` tiles. It includes a first- or
  second-substep impact tile even when the projectile is absent from the
  post-tick `projectiles` list.
- `controlPressure` — signed shared objective pressure after this tick;
  positive favors slot 0 and negative favors slot 1. Active-control rules
  only.

## `result`

`{ winnerSlot, reason, endTick, bots, controlPressure? }` — `winnerSlot` is `null` on a draw
(omitted in canonical JSON), `reason` ∈ `Elimination | Disqualification |
MaxTicks | Domination` (Domination = the zone threshold was reached), and
`bots[]` is
`{ slot, outcome, finalHealth, damageDealt, faults, finalStatus, zoneTicks? }`
with `outcome` ∈ `Win | Loss | Draw`; `zoneTicks` appears only under passive
zone rules and `controlPressure` only under active-control rules. All optional fields follow one rule: absent under
rulesets that predate them, so historical replay hashes never change.
