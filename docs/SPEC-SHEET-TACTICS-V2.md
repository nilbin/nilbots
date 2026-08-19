# SPEC — `arc-relay-tactics-v2` (player-owned Tactics document)

Status: normative for the player save surface, 2026-08-19.
Decision: `DECISIONS.md` #338 (supersedes #331's v1 shape; v1 documents
migrate on load). Materialization target:
[`SPEC-SHEET-ORDERS-V2.md`](SPEC-SHEET-ORDERS-V2.md) — the v3 orders grammar
with the §4b squad program — which stays normative for the executable pair.

A Sheet is a squad of eight bots plus an ordered list of whole-team
**Tactics**. A Tactic is one committed operation of ordered, named
**phases**. The Tactics document is the only player-owned source: the server
migrates a v1 document, validates, **deterministically materializes** the
`arc-relay-orders-v3` + `arc-relay-map-plan-v2` pair, compiles that pair
through the shared compiler, and stores all three. Generated Orders are never
hand-edited and are regenerated wholesale on every save.

## 1. Document shape

JSON object, exact keys, camelCase fields, kebab ids (`[a-z0-9-]`, ≤24 for
tactic/group/phase ids). Only objects, arrays, strings, 32-bit integers and
booleans.

```jsonc
{
  "schema": "arc-relay-tactics-v2",
  "sheetId": "my-sheet",              // identifier, ≤48
  "mapId": "arc-relay-tapline-02",
  "mapWidth": 31, "mapHeight": 27,    // declared size; hosted save asserts it
  "squad": [                          // exactly 8, slots 0..7 in order
    { "slot": 0, "class": "kestrel", "build": "scout", "name": "Vex" }, …
  ],
  "tactics": [ … 1..8 … ]
}
```

`class`/`build`/`name` are unchanged from v1 (≤2 copies per class, ≥7
distinct classes hosted; builds map to generated upgrade paths; role ids
derive from names for attribution).

### Tactic

```jsonc
{
  "id": "pincer-1", "name": "Mid Pincer", "recipe": "pincer",  // provenance only
  "start":  { "mode": "always" } | { "mode": "when", "cases": [ Case… ≤3 ] },
  "abort":  { "cases": [ Case… ≤2 ] },          // optional, checked every phase
  "maxDuration": "brief|standard|long",          // optional minor field;
                                                 //   absent = no limit
  "delivery": { "plan": "none|solo|escort", "preferredSlots": [6] },
  "groups":  [ … 1..3, membership only … ],
  "phases":  [ … 1..8, ordered … ]
}
```

Every squad slot belongs to exactly one group per tactic.

**Case** — the bounded condition model, everywhere conditions appear:

```jsonc
{ "required"?: { …rows… }, "triggers"?: { …rows… } }
```

All `required` rows must hold AND any `triggers` row may fire; an absent side
is vacuously true, and at least one row must be active. Alternative cases OR.
The UI hides the boolean words: "Required", "Triggered by", "Add
alternative".

**Condition rows** (present member = active row), by context:

| row | shape | legal in | meaning |
|---|---|---|---|
| `groupsSet` | `true` | continue (required only) | every Group with ground this phase has EVERY member inside its footprint or dead — exact, per bot, no quorum |
| `held` | `{ "ticks": 1..600 }` | continue | this phase has been active N ticks |
| `healed` | `{ "percent": 1..100 }` | continue (required only) | every LIVING bot is at ≥N% health — exact, per bot, the dead excuse themselves |
| `enemiesAt` | `{ "count": 1..8, "zone": {…} }` | start, continue, abort | ≥N enemies known in the box (team memory, 40-tick freshness) |
| `contact` | `true` | continue | any enemy visible to the team |
| `coreLoose` | `{ "well": … }` | start, continue | that Well's Core is up for grabs |
| `coreSoon` | `{ "well": …, "ticks": 1..120 }` | start, continue | that Well's next Core births within N ticks |
| `enemiesDown` | `{ "count": 1..8 }` | start, continue | ≥N enemy bots down right now |
| `carrier` | `true` | start | a friendly carrier exists |
| `losses` | `{ "count": 1..8 }` | start, continue, abort | we are down N bots |
| `lossesWithin` | `{ "count": 1..8, "ticks": 1..600 }` | start, abort | we lost N bots within the last T ticks (the remembered-casualty row — a Regroup's start and an abort may share it) |
| `kills` | `{ "count": 1..8 }` | continue | N confirmed destructions since THIS phase began |
| `cleared` | `{ "zone": {…} }` | continue | no visible enemies in the box |
| `coreAcquired` / `coreBanked` / `coreLost` | `true` | continue (`coreLost` also abort) | Core evidence; banked/lost anchor at phase entry |
| `reached` | `true` | continue | the phase's converge/advance group reached its destination |
| `observed` | `true` | continue | the watchline stayed fully observed for 120 ticks |

### Group — membership only

```jsonc
{ "id": "mid-guard", "name": "Mid Guard", "slots": [5, 2],
  "coreRule": "ignore|grab|hunt" }
```

What a group DOES lives in each phase's job for it.

### Phase

```jsonc
{
  "id": "set-the-trap", "name": "Set the trap",
  "jobs": { "<groupId>": Job },        // exactly one per group
  "continue": [                         // ≤3 rules; ABSENT only on the final
    { …Case…, "to": "next" | "<laterPhaseId>" | "finish" }
  ]                                     //   phase (= hold there forever)
}
```

- Transitions are **latched and forward-only**: rules evaluate in authored
  order once per tick (never on the phase's entry tick), the first true one
  fires, and an entered phase is never re-checked against its entry
  condition. `to` may name only a LATER phase.
- The first phase needs at least one group with drawn geometry.
- An empty/absent `continue` on the final phase is the explicit persistent
  form: the operation holds there until abort, duration, or the match.

### Job — one group's assignment for one phase

```jsonc
{ "kind": "posts|area|line|approach|converge|advance|patrol|return|escort|none",
  "posture": "wait|hold|aggressive|steal|escort|observe",
  "travel"?: "wait|hold|aggressive|steal|escort|observe",
  "recovery": "fight-on|patch|heal-tile|heal-up",
  // exactly the geometry its kind requires (as v1's stage/act):
  "posts": [ { "at": [x,y], "facing": "n|e|s|w", "via"?: […] }, … ],
  "rect": [x1,y1,x2,y2], "path": [[x,y]… 2..16], "point": [x,y],
  "via": [[x,y]… 2..16],               // posts/area/line/approach/converge/return
  "sigs": { "<classId>": mark }         // per phase now (was per stage/act)
}
```

`kind: "none"` means **carry on**: the group keeps its nearest earlier
geometric job under THIS phase's posture, travel and recovery. On the first
phase it means starting loose. Signature marks follow the v1 rules (descriptor
capabilities, budgets), attached to the phase that uses them.

`travel` is the optional **on-the-way posture**: how the group behaves while
MOVING to this job's ground; the main `posture` takes over on arrival. Absent
(or equal to `posture`) means one posture for the march and the ground. It is
legal only where an arrival exists — posts/area/line/approach/converge/
advance, or a `none` that carries one of those forward; patrol, return and
escort are all march and refuse it.

`recovery: "heal-up"` breaks for the heal tile at any damage (<100%) and
stays until completely full, so every `healed` gate 1..100 is eventually
satisfiable — pair it with a `healed` row so the transition waits for the
team.

## 2. Materialization (normative)

One Orders role per squad slot, as v1. Generated ids are ordinal and are the
practice-attribution contract: per role, each tactic materializes ONE wrapper
`t{n}` (tagged `phase:"t{n}"`) holding the tactic-scoped Core habits
(delivery, coreRule — ids `t{n}-g{k}-{kind}`) and one wrapper per phase
`t{n}-p{j}` (tagged `phase:"t{n}/p{j}"`) holding that phase's recovery,
posture fight, signature and floor leaves (ids `t{n}-p{j}-g{k}-{kind}`).
Leaves are untagged — the wrappers are the whole gate. There is no release
branch, per-body latch, stage wrapper, or `rejoin` anywhere.

The program table carries the lifecycle: start/abort cases row-for-row,
`maxDuration` → `maxTicks` (60/180/420), `rearm: 40`, and each phase's
continue rules with doc phase ids resolved to ordinals (`p{j}`).
`groupsSet` compiles to exact per-bot dead-excused pairs
(`any(role-in-zone-count{bot} ≥ 1, role-live-count{bot} ≤ 0)`) over each
geometric group's footprint — per-post zones for posts groups of ≤3, the
shared footprint otherwise; the v1 two-thirds quorum and short-handed excuse
are gone. `healed` compiles the same way per bot:
`role-lowest-health-percent{bot} ≥ N` entries (a dead role reads 100, so the
dead excuse themselves). Evidence rows anchor `since: "phase-entry"`;
`lossesWithin` rides `friendly-destructions` windows; `held` rides
`elapsed-ticks`.

`travel` shifts the phase's floor move stance to the travel posture's band
(aggressive/steal → front, hold/escort → line, else rear) and adds one
respond leaf (`t{n}-p{j}-g{k}-travel-fight`) gated on being OFF the station,
so it owns combat en route and goes silent on arrival — even under a
fightless arrived posture like `wait`. Under `hold` it answers nearby
attackers with chase 1 (respond, no pursuit); `wait` emits no leaf at all
(the quiet march). Standing kinds gate their main fight on the station as
always; converge/advance gain a destination gate (point / path end, ±2)
only when a travel posture is authored — without one, converge-aggressive
still brawls its way in by authored choice. `heal-up` materializes an urgent
recovery leaf that breaks for the nearest heal zone at any damage (≤99%) and
holds there until completely full (≥100%).

A tactic whose final phase holds forever and whose delivery is `none` still
gets the urgent bank-escape leaf, exactly as v1's persistent form did.
Attempt timers: brief 60 / standard 180 / long 420. The baseline band
(score-a-core, collect-loose-core, recover-health, fight-visible-enemy,
hold-ground) is unchanged and untagged.

Coordinator semantics — selection, one-transition-per-tick, abort ordering,
rearm, dark-state clearing, respawns joining the current phase — are the
Orders spec §4b contract, proved in `TacticsProgramCoordinatorTests` and
`TacticsEncodingLifecycleTests`.

## 3. Migration from v1

Deterministic, applied on every load path (hosted save, CLI, editor):

| v1 | v2 |
|---|---|
| `group.stage` (+ `sigs.stage`) | phase 1 ("Stage") job |
| `release ready` | `p1.continue = [{required:{groupsSet}, to:"next"}]` |
| `release ready-when` rows | the same rule plus `triggers:` rows |
| `release immediate` | single phase from act jobs (stage geometry carried for act-`none` groups) |
| `group.act` (+ `sigs.act`) | phase 2 ("Act") job (`none` stays carry-on) |
| `finish.outcomes` | final phase `continue = [{triggers: rows, to:"finish"}]` |
| `persistent` | dropped from rows — the empty final continue IS the form |
| `finish.maxAttempt` | tactic `maxDuration` |
| start rows | `start.cases = [{triggers: rows}]` |

## 4. Budgets and caps

Unchanged from v1/#331: the 160 KiB ATL2 envelope, mind payload and
MindStart wire caps, 128-entry map-plan tables, 192 KiB App save gate. The
program table costs a few KiB; `MaximalDocumentStaysInsideTheRuntimeEnvelope`
pins that a full 8-tactic, every-feature document still compiles.

## 5. Hosted save

As v1 (`POST/PUT /api/sheets`), with one addition: a submitted v1 document is
migrated first, and the MIGRATED v2 JSON is what validates, materializes, and
is stored/returned — v2 is the only persisted shape.
