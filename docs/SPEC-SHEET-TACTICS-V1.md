# SPEC — `arc-relay-tactics-v1` (player-owned Tactics document)

Status: SUPERSEDED by
[`SPEC-SHEET-TACTICS-V2.md`](SPEC-SHEET-TACTICS-V2.md) (`DECISIONS.md`
#338), which replaces Stage/Release/Act/Finish with ordered named phases
and the squad program. Saved v1 documents migrate deterministically on
every load path; nothing validates or stores this shape anymore. Kept for
reading historical documents and the migration table.
Original decision: `DECISIONS.md` #331.

A Sheet is a squad of eight bots plus an ordered list of whole-team
**Tactics**. The Tactics document is the only player-owned source: the server
validates it, **deterministically materializes** the `arc-relay-orders-v2` +
`arc-relay-map-plan-v2` pair, compiles that pair through the shared compiler,
and stores all three. Generated Orders are never hand-edited and are
regenerated wholesale on every save — there is nothing to drift.

## 1. Document shape

JSON object, exact keys, camelCase fields, kebab ids (`[a-z0-9-]`, ≤24 for
tactic/group ids). Only objects, arrays, strings, 32-bit integers and
booleans.

```jsonc
{
  "schema": "arc-relay-tactics-v1",
  "sheetId": "my-sheet",              // identifier, ≤48
  "mapId": "arc-relay-tapline-02",
  "mapWidth": 31, "mapHeight": 27,    // declared size; hosted save asserts it
                                      // equals the real map
  "squad": [                          // exactly 8, slots 0..7 in order
    { "slot": 0, "class": "kestrel", "build": "scout", "name": "Vex" }, …
  ],
  "tactics": [ … 1..8 … ]
}
```

- `class`: one of the sixteen launch classes; ≤2 copies per class (the App
  additionally requires ≥7 distinct classes, from `GameRules`).
- `build`: `balanced | durable | aggressive | scout` → a generated
  `upgradePath` (balanced `damage,vitality,vision,reach`; durable
  `vitality,vitality,damage,vision`; aggressive `damage,damage,reach,vitality`;
  scout `vision,reach,damage,vitality`).
- `name`: 1..24 chars, display text. Role ids derive from it (kebab, ≤20,
  `-<slot>` on collision), so replay and practice attribution show bot names.

### Tactic

```jsonc
{
  "id": "pincer-1", "name": "Pincer", "recipe": "pincer",   // recipe = provenance only
  "start":   { "mode": "always" } | { "mode": "when", "conds": { …rows… } },
  "release": { "mode": "immediate" } | { "mode": "ready" }
           | { "mode": "ready-when", "conds": { …rows… } },
  "finish":  { "outcomes": { …flags… }, "maxAttempt": "none|brief|standard|long" },
  "delivery": { "plan": "none|solo|escort", "preferredSlots": [6] },
  "groups":  [ … 1..3 … ]
}
```

Every squad slot belongs to exactly one group per tactic. At least one group
must have a plan (stage or act kind other than `none`).

**Condition rows** (`conds` object, present member = active row):

| row | shape | meaning |
|---|---|---|
| `enemiesAt` | `{ "count": 1..8, "zone": { "name"?, "rect": [x1,y1,x2,y2] } }` | ≥N enemies known in the box (team memory, 40-tick freshness) |
| `contact` | `true` | any enemy visible to the team |
| `coreLoose` | `{ "well": "north|centre|south|own|enemy" }` | that Well's Core is up for grabs |
| `coreSoon` | `{ "well": …, "ticks": 1..120 }` | that Well's next Core births within N ticks — its one effective clock: the initial opening, then the bank-triggered recharge |
| `enemiesDown` | `{ "count": 1..8 }` | at least N enemy bots are down right now |
| `carrier` | `true` | a friendly carrier exists |
| `losses` | `{ "count": 1..7 }` | we are down N bots |

Start rows OR together; `carrier` is start-only, `contact` release-only,
`coreSoon` and `enemiesDown` are legal in both.
Release rows OR together too: the latch materializes as
`all(readiness…, any(rows…))`, using the order grammar's one nesting level,
so any ticked trigger lets go once every group is in position. Every
readiness entry is itself `any(in-position, expectation-dead)` — an
exact-post gate pairs each post with that bot's `role-live-count 0`, and an
aggregate gate pairs its two-thirds quorum with a short-handed excuse — so a
death never holds the release hostage. The generated release branch carries
the order grammar's `rejoin` flag: a body respawning after the squad has
gone enters the released phase directly — its own gate could never re-form
around squadmates who left their posts — instead of re-staging alone. Only
a full squad wipe clears that latch and returns survivors to staging for a
fresh release.

Named zones (`mid`, `their-well`, …) are an editor concept: the document
always stores the resolved rect (plus the name for display), so the
materializer never needs the zone catalog.

**Outcomes** (`finish.outcomes`, present member = selected): `kills{count}`,
`losses{count}`, `cleared{zone}`, `coreAcquired`, `coreBanked`, `coreLost`,
`reached`, `observed`, `persistent` (flags are literal `true`). At least one
outcome or a non-`none` `maxAttempt` is required. `reached` needs a group
that converges or advances; `observed` needs drawn geometry. `persistent`
suppresses every other outcome at materialization: the tactic runs until a
higher tactic takes over.

### Group

```jsonc
{
  "id": "mid-guard", "name": "Mid Guard", "slots": [5, 2],
  "coreRule": "ignore|grab|hunt",
  "stage": { "kind": "posts|area|line|approach|none",
             "posture": "wait|hold|aggressive|steal|escort|observe",
             "recovery": "fight-on|patch|heal-tile",
             // exactly the geometry key its kind requires:
             "posts": [ { "at": [14,12], "facing": "n|e|s|w", "via"?: [[x,y]…2..16] }, … ],  // one per slot, aligned with slots order; a post's own via overrides the phase via for its bot
             "rect": [x1,y1,x2,y2], "path": [[x,y]… 2..16], "point": [x,y],
             "via": [[x,y]… 2..16] },   // optional drawn approach route
  "act":   { "kind": "none|converge|advance|patrol|return|escort",
             "posture": …, "recovery": …,
             "point": [x,y],            // converge
             "path": [[x,y]… 2..16],    // advance | patrol (loops legal)
             "via": [[x,y]… 2..16] },   // optional; converge/return only
  "sigs":  { "stage": { "<classId>": mark }, "act": { … } }
}
```

`posts[i]` belongs to `slots[i]` — the assignment is positional and is also
the exact-post readiness contract. Signature marks are legal only for classes
present in the group, with geometry derived from the signature catalog's
descriptor capabilities — never a class-id switch:

- ExactAnchor classes → `{ "points": [[x,y]…] }` with budgets mason 2, nest 2,
  minesmith 3, lantern 2, mortar 2, veil 2;
- RouteForward (kestrel) → `{ "line": [[x,y]… 2..8] }`, a terminal dash lane
  (must not loop);
- longshot → `{ "line": [[p0],[p1]] }`, a rail: stand at p0, aim at p1.

Every other class's signature is bundled by posture — nothing to draw.
Stage marks deploy while staging; act marks after release.

## 2. Materialization (normative)

One Orders **role per squad slot** (roles are a materialization artifact with
no Tactics-level meaning): id = the bot's kebab name, classes = `[class]`,
upgradePath from the build. Slot order = unit order, so slot *i* is unit *i*.

Generated ids are ordinal and are the practice-attribution contract:
tactic branches `t{n}` (1-based sheet order), release branch `t{n}-go`, stage
wrapper `t{n}-s`, leaves `t{n}-g{k}-{s|a}-{kind}` (group ordinal, phase tag).
Conditions `c-t{n}-…` plus shared names (`empty-handed`, `we-hold-a-core`,
`never`, …); geometry `g-t{n}-…` interned by shape (identical rects, routes
or points share one entry across the sheet).

Per role, the root order list is:

```
t1 … tN                       // one branch per tactic (skipped for a bot
                              //   whose group contributes nothing)
shared score-a-core           // carrying → return and bank (urgent)
shared collect-loose-core     // team pair-per-core pickup
shared recover-health         // hurt → nearest heal zone
shared fight-visible-enemy    // baseline self-defence
shared hold-ground            // move to own Well, posture line
```

Each tactic branch (proved against the order-tree lifecycle in
`TacticsEncodingLifecycleTests`):

```
t{n} (when = start rows any-joined; timeout = maxAttempt; cooldown 40)
├─ phase-invariant Core leaves: delivery pass/bank/screen, coreRule grab/hunt
├─ t{n}-go (when = all[readiness…, release row];
│           until = any[outcome conds] or the never-latch; cooldown 120)
│  └─ act leaves: recovery, posture fight, signature marks, the floor
└─ t{n}-s (until = the same release expression; cooldown 120)
   └─ stage leaves: recovery, posture fight, stage marks, the floor
```

- **Release latches**: the release branch's authored `until` means its `when`
  is never re-consulted while active — a lapsed trigger cannot un-release.
- **Outcomes count from release**: evidence outcomes (`kills`, `coreBanked`,
  `coreLost`) use `since: "node-entry"` anchored at `t{n}-go`; instant
  outcomes are checked there too. `immediate` release puts the outcome until
  on the root, where activation *is* release.
- **Completion falls through**: at release the stage wrapper's until fires;
  when the outcome until fires the release branch exits too, the root yields
  `no-child-eligible`, and the next tactic takes over. Rearm is the exit
  cooldowns (120/40) — tactics are standing orders and may run again.
- **Readiness**: posts groups of ≤3 gate on each assigned bot inside a ±1
  zone around *its own* post (`role-in-zone-count` over the single-bot role);
  larger posts groups bind each distinct bot to the posts footprint; area /
  line / approach groups use `friendlies-in-zone ≥ max(1, size−1)` over their
  staging footprint (rect as drawn; bbox +1; point ±2).
- **Phases carry their own support**: per-phase recovery (`heal-tile` = the
  Tempered hurt→heal pattern; `patch` = a brief posture-rear pause in place —
  a documented approximation, no repair verb exists), posture-gated fight
  leaves (hold/aggressive/steal/escort; wait and observe stay quiet), core
  rules and signature uses, so nothing silently falls into a lower tactic.
  Delivery and Core habits are phase-invariant and sit above the release
  branch; their carrying/empty-handed gates keep post-outcome residue benign.
- **Delivery**: `solo`/`escort` add an urgent in-tactic carry-home leaf (and
  for a relay, an arc-toss pass toward the first preferred carrier);
  `escort` adds a follow-the-carrier screen gated on the carrier being in
  this group (`friendly-carriers-in-roles`). `none` means the job continues
  and banking waits for the tactic to end (the baseline banker below) —
  except in a `persistent` tactic, which never ends and would trap an
  accidental carrier, so its branch carries the same urgent bank escape the
  organized plans get.
  `coreAcquired` as an outcome ends the tactic on acquisition, yielding to a
  lower Deliver Core tactic or the baseline banker.
- Floors: posts → per-bot `move.to {anchor}` + `face` one tile along the
  facing; area → `move.to {zone}`; line → members round-robin onto the
  clicked anchors; approach → `adjacentTo` the point, posture rear;
  converge → `adjacentTo`; advance → route `via` + goal zone; patrol →
  `move.patrol`; return → `own-reactor` (the mind resolves that dynamic to
  the reactor's approach ring for an empty-handed body — banking consumes a
  Core only while the carrier stands ON the reactor tile, so the tile itself
  is carrier infrastructure and a parked returner must never blockade it);
  escort → follow the nearest friendly carrier. An act of `none` re-emits the stage floor inside the release
  branch, so stage-only groups keep holding after release. An optional `via`
  (legal on posts/area/line/approach stages and converge/return acts —
  advance and patrol already carry their own route) becomes the Orders
  `move … via {route}` ingress: each activation walks the drawn route once,
  progress-aware, then the floor takes over — the authored answer to a
  staging ground whose direct path crosses ground the group cannot survive.
  A stage `via` re-emitted by an act of `none` also routes re-staging
  respawns.
- Attempt timers: brief 60 / standard 180 / long 420 ticks; timers run from
  branch eligibility (the engine's wall-clock model).
- Group-uniform leaves are factored into per-tactic shared orders
  (`t{n}-g{k}-…`) so eight near-identical role copies stay inside the
  envelope; one-bot groups inline them.

## 3. Budgets and caps

Raised 64→128 in lockstep for this feature (owner call, `DECISIONS.md`
#331): the ATL2 envelope (128 KiB), the mind's payload cap, the MindStart
evaluation wire cap, and the map-plan zone/route/anchor tables (128 each).
The App save gate is 192 KiB of raw tactics JSON. The materializer refuses a
sheet whose geometry overflows the tables with an authoring-worded error, and
`MaximalDocumentStaysInsideTheRuntimeEnvelope` pins that a full 8-tactic ×
3-group, every-feature document compiles.

## 4. Hosted save

`POST/PUT /api/sheets` takes `{name, expectedRevision, tacticsJson,
enterLadder?}`. The server validates the document, asserts the declared map
and size, materializes, runs the same real-map geometry checks as the direct
Orders path (floor for both bindings, reserved spawns, protected homes,
movement-zone capacity feasibility), and stores the tactics doc plus the
derived pair and `ContentHash`. Responses carry `tacticsJson` and the
materialized pair for debugging and attribution. Raw Orders v2 authoring
remains available through the CLI only:
`nilbots experiment arc-relay-playbook --tactics <json>` materializes and
compiles the same way and writes the playable pair beside `playbook.atp`.
