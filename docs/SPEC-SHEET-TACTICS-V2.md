# SPEC — `arc-relay-tactics-v2` (player-owned Tactics document)

Status: normative for the player save surface, 2026-08-20.
Decisions: `DECISIONS.md` #338 and #353 (supersedes #331's v1 shape; v1
documents migrate on load). Materialization target:
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

The executable grammar continues to accept the full table below so older
drafts remain truthful. Normal phase authoring exposes only: formation ready,
healed, contact, recently seen enemies, enemies down, verified clear, no local
friendly fight, Core loose/soon/acquired. An active hidden row is shown as
**Existing advanced condition** until removed; it is never silently discarded.
`healed` and `groupsSet` remain Required-only.

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
| `groupsSet` | `true` or `{ "allowedLate": 0..2 }` | continue (required only) | phase-entry lives are checked against their own ground; zero, one or two may remain late; dead/respawned lives cannot make readiness flap |
| `held` | `{ "ticks": 1..600 }` | continue | this phase has been active N ticks |
| `healed` | `{ "percent": 1..100 }` | all (required/unless only) | every LIVING bot is at ≥N% health — exact, per bot, the dead excuse themselves |
| `enemiesAt` | `{ "count": 1..8, "zones": [{…}] }` | all | ≥N distinct enemies known across the one-to-three-zone union (team memory, 20-tick freshness) |
| `contact` | `true` | all | any enemy visible to the team |
| `coreLoose` | `{ "well": … }` | all | that Well's Core is up for grabs |
| `coreSoon` | `{ "well": …, "ticks": 1..120 }` | all | that Well's next Core births within N ticks |
| `enemiesDown` | `{ "count": 1..8 }` | all | ≥N enemy bots down right now |
| `carrier` | `true` | all | a friendly carrier exists |
| `losses` | `{ "count": 1..8 }` | all | we are down N bots |
| `lossesWithin` | `{ "count": 1..8, "ticks": 1..600 }` | all | we lost N bots within the last T ticks (the remembered-casualty row — a Regroup's start and an abort may share it) |
| `killsWithin` | `{ "count": 1..8, "ticks": 1..600 }` | all | we destroyed N enemies within the last T ticks — the durable "rout" form of `enemiesDown`, which respawn waves stand back up |
| `kills` | `{ "count": 1..8 }` | continue | N confirmed destructions since THIS phase began |
| `cleared` | `{ "zones": [{…}] }` | all | every walkable tile in the one-to-three-zone union was checked through team vision in the rolling last 20 ticks and no unresolved enemy observation remains |
| `notFighting` | `{ "zones": [{…}] }` | all | no selected friendly Fight intersects any tile in the union through its fighter, target or pending strike |
| `coreAcquired` | `true` \| `{ "well": … }` | continue | a Core was PICKED UP since this phase began (the event, not the carrier snapshot — `carrier` is the snapshot); a well narrows it to that source's Core |
| `coreBanked` / `coreLost` | `true` | continue (`coreLost` also abort) | Core evidence since phase entry |
| `reached` | `true` | continue | the phase's converge/advance group reached its destination |
| `observed` | `true` | continue | the watchline stayed fully observed for 120 ticks |
| `workshopReady` | `{ "workshop": "north"\|"south"\|"own"\|"enemy"\|"either" }` | start, continue | that Workshop (either = any) is publicly Ready |
| `workshopTraining` | `{ "workshop": …, "team"?: "self"\|"enemy"\|"any" }` | start, continue | a body of that team is publicly channeling there (default any) |
| `workshopContested` | `{ "workshop": … }` | start, continue, abort | living enemies stand in that Workshop's contest region |
| `workshopDormant` | `{ "workshop": … }` | start, continue, abort | that Workshop is publicly recharging |
| `workshopDone` | `{ "workshop": …, "count"?: 1..8 }` | continue | ≥N friendly completions at that Workshop since THIS phase began (derived from the public Training→Dormant transition — never a fogged fact) |
| `veterancy` | `{ "slots": [0..7, ...], "count": 1..N, "level": 2..5 }` | continue | at least N distinct selected squad slots have living bodies at that level |

Workshop rows read the public neutral-objective signal every seat can see;
they never reveal fogged positions, only Workshop state.

New zone rows write `zones` with one to three rectangles. A singular `zone`
remains a one-area load compatibility spelling and normalizes on intentional
edit/save. Union evaluation canonicalizes overlapping tiles and enemy lives, so
overlap never multiplies enemy counts, clearance work, Scan targets or fights.

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
    { …Case… }
  ]                                     //   phase (= hold there forever)
}
```

- Transitions are **latched and strictly sequential**: rules evaluate in
  authored order once per tick (never on the phase's entry tick), the first
  true one activates the next array phase, and the final phase finishes the
  Tactic. An entered phase is never re-checked against its entry condition.
  New documents omit `to`; matching legacy `next`/`finish` spellings normalize
  away, while arbitrary jumps are rejected.
- The first phase needs at least one group with drawn geometry.
- An empty/absent `continue` on the final phase is the explicit persistent
  form: the operation holds there until abort, duration, or the match.

### Job — one group's assignment for one phase

```jsonc
{ "kind": "posts|area|converge|patrol|train|none",
  "posture": "observe|hold|aggressive",
  "travel"?: "observe|hold|aggressive",
  "recovery": "fight-on|heal-tile|heal-up",
  // exactly the geometry its kind requires (as v1's stage/act):
  "posts": [ { "at": [x,y], "facing": "n|e|s|w", "slot"?: n, "via"?: […] }, … ],
  "rect": [x1,y1,x2,y2], "path": [[x,y]… 2..16], "point": [x,y],
  "via": [[x,y]… 2..16],               // posts/area/line/approach/converge/return
  "sigs": { "<classId>": mark }         // per phase now (was per stage/act)
}
```

Those are the normal authoring values. The parser retains the older job,
posture, travel and recovery values as visible **Existing advanced** content;
new controls do not create them.

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

`kind: "train"` is **Train at Workshop**: the group must hold exactly one
slot, `posture` must be `"wait"`, signature marks are refused, and the job
carries `"workshop": "north"|"south"|"own"|"enemy"|"either"` instead of drawn geometry —
the rules-owned training tile is resolved from the match contract's Workshop
binding, never from authored coordinates, so the same document stays honest
on any future map revision. North/south are global map identities. Own/enemy
resolve at match time to the Workshop nearest/farthest from that team's own
Well, so a strategic side choice mirrors with the entrant just like drawn
ground; `either` keeps its deterministic availability/distance choice.
`travel` chooses the on-the-way behavior as usual; on arrival the body waits
on the tile and the rules run the channel. Core custody outranks training: a
`coreRule` that hands the trainee a Core suspends the channel until the Core
is gone, and the public Workshop state says why progress stopped.

`kind: "none"` is the internal carry-forward encoding: when a phase does not
replace a Group's ground, it keeps the nearest earlier geometric job under this
phase's posture, travel and recovery. The editor explains that inheritance but
does not offer Carry on as a fifth job. On the first phase it means starting
loose. Signature marks follow the v1 rules (descriptor capabilities, budgets),
attached to the phase that uses them.

`travel` is the optional **on-the-way behavior**. `observe` = Just march,
`hold` = Respond to contact with the short leash, and `aggressive` = Brawl
through. The destination posture takes over on arrival. It is legal only where
an arrival exists; patrol is all march and refuses it.

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
`maxDuration` → `maxTicks` (60/180/420), `rearm: 0`, and each phase's
continue rules resolved to the next ordinal (`p{j}`) or finish. Completion
keeps the ended Tactic dark on tick T; another eligible Tactic may begin on T,
or the same one may begin fresh on T+1.

`groupsSet` compiles to one squad-level `formation-late-count` fact. It freezes
the lives present at phase entry, pairs every slot with its own effective
ground, and compares the late count to `allowedLate`. A life dead at entry is
excused; death while assembling excuses that life permanently for this phase;
a respawn performs the active job but never re-enters the wait cohort. Posts
always receive one per-slot Chebyshev-one zone, including eight-body groups, so
one body cannot satisfy another slot's assigned post. `healed` compiles per bot:
`role-lowest-health-percent{bot} ≥ N` entries (a dead role reads 100, so the
dead excuse themselves). Evidence rows anchor `since: "phase-entry"`;
`lossesWithin` rides `friendly-destructions` windows; `held` rides
`elapsed-ticks`.

`travel` shifts the phase's floor move stance to the travel behavior's band
(aggressive → front, hold → line, observe → rear) and adds one
respond leaf (`t{n}-p{j}-g{k}-travel-fight`) gated on being OFF the station,
so it owns combat en route and goes silent on arrival — even under a
fightless arrived posture like `observe`. Under `hold` it answers nearby
attackers with chase 1 (respond, no pursuit); `observe` emits no leaf at all
(the quiet march). Standing kinds gate their main fight on the station as
always; converge gains a destination gate (point, ±2)
when a travel behavior is authored — without one, converge-aggressive
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
active finishes that carry under the phase that began it. For escorted custody,
the pickup-phase tags remain lit for the carrier and the potential escorts in
that carrier's pickup-phase Group; unrelated Groups are never recruited. The
latch ends for the group when the Core leaves the carrier's hands (bank, drop,
death or loss; a catcher latches fresh at its own current phase). Squad phase
progress is untouched; attribution shows the pickup phase while the rest of
the squad may already be in the next phase.

A tactic whose final phase holds forever and whose delivery is `none` still
gets the urgent bank-escape leaf, exactly as v1's persistent form did.
Attempt timers: brief 60 / standard 180 / long 420. The baseline band
(score-a-core, collect-loose-core, recover-health, fight-visible-enemy,
hold-ground) is unchanged and untagged.

Coordinator semantics — selection, one-transition-per-tick, abort ordering,
the one-evaluation dark boundary, dark-state clearing, respawns joining the current phase — are the
Orders spec §4b contract, proved in `TacticsProgramCoordinatorTests` and
`TacticsEncodingLifecycleTests`.

`cleared` is a pure condition. On a phase transition, each positive `cleared`
area materializes a visible `scan` Instruction only for phase Groups whose
effective ground overlaps or touches that particular area. A disconnected
multi-area clearance therefore uses nearby allies at each area rather than
recruiting a distant Group. Scans sit after recovery, Fight
and signature opportunities and before passive positioning. A remote area with
no local phase group is invalid; Tactic start and abort rows remain
observation-only and generate no actions. The editor labels this **This area
has been verified clear** and explains the 20-tick full-coverage rule.

`notFighting` compiles to the ordinary `friendly-fights-in-zone` fact compared
with zero over the canonicalized zone union. The common transition pairs it
with `cleared`; the editor copies that geometry by default. It counts a friendly inside the
union fighting out, a friendly outside fighting or striking in, and ongoing
Fight selection during rotation or cadence. Unrelated combat elsewhere and
mere enemy visibility do not count. Because program conditions run before the
current decision selects leaves, it has one documented decision-boundary delay.

A `hold` phase owns its authored ground, not its resting facing. Its empty-handed
holders rotate, track and fire at actionable enemies from every direction;
cadence keeps combat facing. A Hold fight starts only from authored ground and
normally stays there, with one bounded exception: it may close at most five
terrain steps from its fixed activation anchor to stop an escaping visible Core
carrier or answer a visible attacker that can shoot it from beyond its own
range. It does not pursue an ordinary retreating enemy. The episode ends when
the threat is no longer actionable or the holder acquires a Core, after which
the floor Instruction returns it to ground and resting facing. Carriers retain
their existing non-fighting behavior.

In the map editor, the selected phase is full strength, its immediate previous
and next phases are dim, and distant phases are hidden. **Show every phase**
reveals distant phases still more faintly without changing **Show every
Tactic**; only selected-phase geometry is editable.

## 3. Migration from v1

Deterministic, applied on every load path (hosted save, CLI, editor):

| v1 | v2 |
|---|---|
| `group.stage` (+ `sigs.stage`) | phase 1 ("Stage") job |
| `release ready` | `p1.continue = [{required:{groupsSet}}]` |
| `release ready-when` rows | the same rule plus `triggers:` rows |
| `release immediate` | single phase from act jobs (stage geometry carried for act-`none` groups) |
| `group.act` (+ `sigs.act`) | phase 2 ("Act") job (`none` stays carry-on) |
| `finish.outcomes` | final phase `continue = [{triggers: rows}]` |
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
