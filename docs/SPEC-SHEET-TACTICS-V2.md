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
  "mapId": "arc-relay-tapline-03",
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
{ "required"?: { …rows… }, "triggers"?: { …rows… }, "unless"?: { …rows… } }
```

All `required` rows must hold AND any `triggers` row may fire AND no
`unless` row may hold; an absent side is vacuously true, and at least one
required or trigger row must be active (unless alone gates nothing).
Alternative cases OR. The UI hides the boolean words: "Required",
"Triggered by", "Unless", "Add alternative".

`unless` is the inverted side. Every ordinary row is one threshold
condition, so its negation compiles as an exact operator flip (at-least N →
at-most N−1 and so on) named `no-…` — no runtime grammar change. `healed`
inverts too — NOT(everyone ≥ N) = any bot below N, one nested any of the
per-role flips. Only `groupsSet` reads one way (its per-bot pairs would
need two nesting levels inverted); unless refuses it.

Row availability is uniform: every current-state row reads in every
context and on every side (healed stays out of triggers — "any one bot is
topped up" reads wrong). Only the phase-anchored rows (groupsSet, held,
kills, coreAcquired/Banked/Lost, reached, observed) need a running phase
and stay transition-scoped, coreLost also aborting. The editor buckets
rows under four headings — Our team, Enemies, Cores, Time & ground — with
no fold.

Timing across an AND: a case is re-evaluated every tick, so a snapshot row
and an event row meet whenever the snapshot holds at ANY tick after the
event has happened. The fragile pairing is snapshot∧snapshot where one
side flickers (enemiesDown under respawn waves): author the windowed or
phase-anchored form of the flickery side instead (`killsWithin`,
`lossesWithin`, `kills`, `coreAcquired`). Cases deliberately keep no
per-row memory — a sequence of things that must EACH have happened is
what phases are.

**Condition rows** (present member = active row), by context:

| row | shape | legal in | meaning |
|---|---|---|---|
| `groupsSet` | `true` | continue (required only) | every Group with ground this phase has EVERY member inside its footprint or dead — exact, per bot, no quorum |
| `held` | `{ "ticks": 1..600 }` | continue | this phase has been active N ticks |
| `healed` | `{ "percent": 1..100 }` | all (required/unless only) | every LIVING bot is at ≥N% health — exact, per bot, the dead excuse themselves |
| `enemiesAt` | `{ "count": 1..8, "zone": {…} }` | all | ≥N enemies known in the box (team memory, 40-tick freshness) |
| `contact` | `true` | all | any enemy visible to the team |
| `coreLoose` | `{ "well": … }` | all | that Well's Core is up for grabs |
| `coreSoon` | `{ "well": …, "ticks": 1..120 }` | all | that Well's next Core births within N ticks |
| `enemiesDown` | `{ "count": 1..8 }` | all | ≥N enemy bots down right now |
| `carrier` | `true` | all | a friendly carrier exists |
| `losses` | `{ "count": 1..8 }` | all | we are down N bots |
| `lossesWithin` | `{ "count": 1..8, "ticks": 1..600 }` | all | we lost N bots within the last T ticks (the remembered-casualty row — a Regroup's start and an abort may share it) |
| `killsWithin` | `{ "count": 1..8, "ticks": 1..600 }` | all | we destroyed N enemies within the last T ticks — the durable "rout" form of `enemiesDown`, which respawn waves stand back up |
| `kills` | `{ "count": 1..8 }` | continue | N confirmed destructions since THIS phase began |
| `cleared` | `{ "zone": {…} }` | all | every walkable tile was checked through team vision in the rolling last 20 ticks and no unresolved enemy observation remains |
| `notFighting` | `{ "zone": {…} }` | all | no selected friendly Fight intersects the box through its fighter, target or pending strike |
| `coreAcquired` | `true` \| `{ "well": … }` | continue | a Core was PICKED UP since this phase began (the event, not the carrier snapshot — `carrier` is the snapshot); a well narrows it to that source's Core |
| `coreBanked` / `coreLost` | `true` | continue (`coreLost` also abort) | Core evidence since phase entry |
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
  "custody"?: {                         // this phase's Core custody override
    "plan": "solo|escort|keep",         //   absent = the tactic's delivery
    "via"?: [[x,y]… 2..16]              //   the drawn carrier route home
  },                                    //   ("keep" holds the Core in hand
                                        //    and refuses a via)
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
  "posts": [ { "at": [x,y], "facing": "n|e|s|w", "slot"?: n, "via"?: […] }, … ],
  "rect": [x1,y1,x2,y2], "path": [[x,y]… 2..16], "point": [x,y],
  "via": [[x,y]… 2..16],               // posts/area/line/approach/converge/return
  "sigs": { "<classId>": mark }         // per phase now (was per stage/act)
}
```

A post's optional `slot` names the bot it belongs to. Without it (older
documents), posts pair with the group's `slots` by position. With it, every
post in the job must carry one, each must name a distinct group member, and
the compiler pairs by name regardless of array order — one post per bot
either way. The editor always writes `slot` (stamping older documents on
load), because explicit pairing is what lets a bot moved to another Group
take its post — drawn way in included — along, instead of the positional
pairing silently shifting onto the bots that stayed. On any Group membership
change the editor follows through: movers' posts and orphaned signature
marks travel to their new Group (a brand-new Group also inherits a copy of
the source Group's job per phase, so a split changes nothing on the ground
until edited apart), and whatever cannot travel is cleaned out of the old
Group's jobs rather than left behind.

`kind: "none"` means **carry on**: the group keeps its nearest earlier
geometric job under THIS phase's posture, travel and recovery. On the first
phase it means starting loose. Signature marks follow the v1 rules (descriptor
capabilities, budgets), attached to the phase that uses them.

`travel` is the optional **on-the-way posture**: how the group behaves while
MOVING to this job's ground; the main `posture` takes over on arrival. Absent
(or equal to `posture`) means no separate on-the-way behavior: a mover's
posture genuinely governs the whole way, while a standing job's fight belongs
to its ground (§2) — the march itself never stops to fight. It is
legal only where an arrival exists — posts/area/line/approach/converge/
advance, or a `none` that carries one of those forward; patrol, return and
escort are all march and refuse it.

`recovery: "heal-up"` breaks for the heal tile at any damage (<100%) and
stays until completely full, so every `healed` gate 1..100 is eventually
satisfiable — pair it with a `healed` row so the transition waits for the
team. Heal trips (`heal-up` and `heal-tile`) seek the heal tile nearest the
PHASE'S ground, not each bot's own position — a squad regrouping at drawn
ground shares one tile instead of scattering to both.

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

Custody: with no phase override, delivery emits tactic-scoped exactly as
before. Any phase-level `custody` moves the WHOLE delivery emission into
the phase wrappers (a tactic-scoped leaf would outrank and shadow every
override): each phase resolves `custody.plan ?? delivery.plan` — solo
banks alone, escort adds the screen, `keep` emits nothing (the carrier
stays on the job; bank in a later phase), and a resolved `none` keeps the
final-hold escape only. A custody `via` becomes the bank move's route
(`g-t{n}-p{k}-carry`) guiding the carrier home — gated on standing on the
phase's ground: the corridor belongs to that ground, and a Core grabbed
elsewhere banks by the direct fallback leaf (`…-bank-direct`) instead of
marching to the corridor's far mouth first.

**Custody latches at pickup.** A body that takes a Core while a phase is
active finishes that carry under the phase that began it: the pickup
phase's tags stay lit for that body alone until the Core leaves its hands
(bank, drop, death, or pass — the catcher latches fresh at its own current
phase). The squad's phase progress is untouched; attribution shows the
carrier under its pickup phase (`t1/p2 · bank`) while the squad reads the
new one.

A tactic whose final phase holds forever and whose delivery is `none` still
gets the urgent bank-escape leaf, exactly as v1's persistent form did.
Attempt timers: brief 60 / standard 180 / long 420. The baseline band
(score-a-core, collect-loose-core, recover-health, fight-visible-enemy,
hold-ground) is unchanged and untagged.

Coordinator semantics — selection, one-transition-per-tick, abort ordering,
rearm, dark-state clearing, respawns joining the current phase — are the
Orders spec §4b contract, proved in `TacticsProgramCoordinatorTests` and
`TacticsEncodingLifecycleTests`.

`cleared` is a pure condition. On a phase transition, each positive `cleared`
row also materializes visible `scan` Instructions for the phase groups whose
effective ground overlaps or touches its zone. They sit after recovery, Fight
and signature opportunities and before passive positioning. A remote area with
no local phase group is invalid; Tactic start and abort rows remain
observation-only and generate no actions. The editor labels this **Area is
cleared of enemies** and explains the 20-tick full-coverage rule.

`notFighting` compiles to the ordinary `friendly-fights-in-zone` fact compared
with zero. The common transition pairs it with `cleared` over the same zone:
the editor copies that geometry by default. It counts a friendly inside the
zone fighting out, a friendly outside fighting or striking in, and ongoing
Fight selection during rotation or cadence. Unrelated combat elsewhere and
mere enemy visibility do not count. Because program conditions run before the
current decision selects leaves, it has one documented decision-boundary delay.

A `hold` phase owns its authored ground, not its resting facing. Its empty-handed
holders rotate, track and fire at actionable enemies from every direction
without chasing outside the ground; cadence keeps combat facing. Resting facing
resumes only after the Fight leaf ends. Carriers retain their existing
non-fighting behavior.

In the map editor, the selected phase is full strength, its immediate previous
and next phases are dim, and distant phases are hidden. **Show every phase**
reveals distant phases still more faintly without changing **Show every
Tactic**; only selected-phase geometry is editable.

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
