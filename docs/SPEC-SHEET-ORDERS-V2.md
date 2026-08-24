# SPEC — Arc Relay Orders DSL v2 / v3

Status: normative implementation contract on current `main`.

This format replaces `arc-relay-tactical-playbook-v1`. There is deliberately no
compatibility layer. The compiler does not desugar v2 back into doctrines,
custody policies, modes, rungs, engagements, cohorts, focus scopes, or the old
six verbs. The executor reads the authored order tree.

`arc-relay-orders-v3` is the whole v2 grammar plus one addition: the **squad
program** table and `phase` tags (section 4b), which move whole-team lifecycle
— tactic selection, latched forward-only phase transitions, aborts, duration —
out of per-body node gates into one explicit squad-wide state machine. Frozen
v2 sheets keep validating and executing exactly as before; the Tactics
materializer emits v3 only.

## 1. Product test

Every feature has to pass all three tests:

1. **Comprehensible:** reading a role from top to bottom predicts what its
   bodies try to do.
2. **Consistent:** priority is position in an ordered list everywhere; every
   order uses the same lifecycle words; every concurrency limit uses the same
   capacity rule.
3. **Dry:** composition is derived from roles, conditions are declared once,
   shared orders are exact reuse, and derived hashes are not authored.

The player writes strategy, not micro. Movement collision avoidance, route
traversal, aiming, legal action checks, automatic Core pickup, and automatic
reactor banking are engine/executor mechanics. They must be deterministic and
traceable, but they are not duplicated in every sheet.

## 2. Documents and roots

A sheet consists of an orders document and a map-plan document.

The orders document has exactly seven root keys:

```json
{
  "schema": "arc-relay-orders-v2",
  "sheetId": "starter-tapline-v2",
  "mapPlan": "../map-plans/tapline-6/starter-tapline-v2.json",
  "roles": [],
  "sharedOrders": {},
  "capacities": {},
  "conditions": {}
}
```

The map plan has exactly six root keys:

```json
{
  "schema": "arc-relay-map-plan-v2",
  "mapPlanId": "starter-tapline-v2",
  "mapId": "arc-relay-tapline-01",
  "zones": {},
  "routes": {},
  "anchors": {}
}
```

Both are authored west-relative. The executor rotates the plan 180 degrees for
the east team. That is the only perspective and therefore is not a field.
Contract fingerprints and SHA-256 values are compiler output, not authored
configuration. The App still stores the pair and its computed hash for match
identity, but the editor does not make players manage hashes.

Unknown or missing keys are compile errors. Identifiers are unique lowercase
kebab-case strings of 1–48 characters. Every referenced identifier must exist.
Every route, zone, anchor, shared order, capacity, and condition must have at
least one expanded consumer. Empty libraries are legal.

## 3. Roles are the eight bodies

`roles` is an ordered list. A role has exactly these keys:

```json
{
  "id": "runner",
  "classes": ["relay", "switchback"],
  "upgradePath": ["reach", "vision", "vitality"],
  "orders": []
}
```

- Flattening `roles[].classes` assigns unit ids 0–7 in source order.
- The flattened list must contain exactly eight launch-class ids and no class
  may occur more than twice.
- A body keeps its role through death and respawn. There is no allocator,
  candidate class, vacancy, promotion, overflow, or assignment-order policy.
- `upgradePath` contains 1–4 entries from `damage`, `vision`, `reach`, and
  `vitality`. On a quiet legal investment tick, points consume this list in
  order and then repeat its final entry. Death resets the body's progress.
- Bodies that need different behavior are different roles. Bodies may share a
  role when their complete order tree and upgrade path are genuinely the same.

Composition is derived from the flattened class list. It is never stated a
second time.

## 4. One ordered tree

`orders` is a priority list: first executable leaf wins. An entry is exactly one
of an inline branch, an inline leaf, or a shared-order reference.

An inline branch groups child priorities under one lifecycle:

```json
{
  "id": "carry-core-home",
  "when": "carrying-core",
  "until": "empty-handed",
  "timeout": 160,
  "cooldown": 8,
  "orders": [
    {
      "id": "pass-forward",
      "when": "safe-pass-exists",
      "handoff": { "target": "nearest-ally-toward-own-reactor" }
    },
    {
      "id": "return-home",
      "move": { "to": "own-reactor", "via": "safe-home-run" }
    }
  ]
}
```

An inline leaf has an `id`, lifecycle fields, exactly one of the five action
keys, and optionally `capacity` and `urgency`. It has no `orders`.

A shared reference has exactly one key:

```json
{ "shared": "carry-core-home" }
```

`sharedOrders` maps ids to complete branch or leaf definitions; the map key is
the definition's id, so the definition does not repeat an `id` field. Reuse is
exact: no parameters, overrides, inheritance, aliases, or shared-to-shared
references. Every shared order needs at least two expanded consumers; otherwise
it belongs inline.
Each expansion receives independent per-body lifecycle state. A shared
reference occupies one priority position in the role that references it.

The maximum authored depth is two branches plus a leaf. This supports a named
play with local priorities without turning the sheet into a programming
language. The compiler rejects empty branches and duplicate expanded order ids
within one role. Trace paths use authored names, for example
`runner > carry-core-home > return-home`; neither `rung` nor generated order ids
appear on product surfaces.

### 4.1 Lifecycle

Every branch and leaf uses the same optional fields:

| field | meaning |
| --- | --- |
| `when` | condition expression that permits entry; absent means `always` |
| `until` | once entered, latch until this expression becomes true |
| `timeout` | maximum active match ticks, `1..1200` |
| `cooldown` | match ticks before this same node may enter again, `0..1200` |
| `rejoin` | `true` makes the `when` gate squad-latched: while any squadmate holds a node with this order id active, a body may enter its own without the gate; requires `when`. Legacy: the program's squad-level phase (v3) makes it unnecessary, and the materializer no longer emits it |
| `phase` | v3 only: gates the subtree to a program tag — `"t1"` (that tactic active, any phase) or `"t1/p2"` (that exact phase). A dark subtree's per-body state evaporates without cooldown |
| `urgency` | allied movement weight: `routine`, `normal`, or `urgent`; inherited, top-level default `normal` |

Rules:

1. Without `until`, `when` is ongoing eligibility: the node exits when it
   becomes false. With `until`, `when` gates entry and `until` owns exit.
2. A timeout, successful one-shot leaf, death, or an ancestor exit also exits
   the node. Cooldown starts on exit, not on temporary pre-emption.
3. A higher-priority sibling may pre-empt a node without resetting it. Its
   wall-clock timeout continues, and it resumes if still active.
4. `rejoin` matches by order id across roles — role trees are per-body, and
   marking the same id in several roles declares one squad phase. It bypasses
   only the gate, never the body's own cooldown, and the latch empties when
   every holder exits or dies. Legacy: it exists for hand-authored v2 sheets;
   the v3 squad program owns phase membership at squad level, so the tactics
   materializer no longer emits it — a respawn simply executes the lit phase.
4. `move`, `fight`, and branches are ongoing until their lifecycle exits.
   A `move.to` leaf holds at its destination, so zone defence and healing do
   not repeatedly exit and re-enter. `use`, `handoff`, and `drop` succeed only
   when the engine accepts the action.
5. An active leaf that has no legal/resolved target is unavailable for that
   tick; selection continues with the next sibling. It does not silently change
   action or invent movement.
6. If no leaf is executable, the body waits with an explicit
   `no-eligible-order` reason. The compiler warns when a role has no
   unconditional fallback but does not invent one.

### 4.2 Movement urgency is not order priority

Source position still decides what a body does. `urgency` is consulted only
after that choice, when two friendly bodies compete for movement space. It may
be written on any branch or leaf; descendants inherit it and the nearest
explicit value wins. Omission at the root means `normal`.

- `routine`: background patrol, farming, and low-cost repositioning;
- `normal`: ordinary tactical movement;
- `urgent`: time-sensitive return, recovery, or authored intervention.

Urgency does not reorder siblings, activate an order, select a target, reserve
capacity, alter combat, or affect an enemy. Class, role, action kind, and Core
possession do not manufacture a hidden tier. Within allied traffic the executor
resolves one deterministic order from effective urgency, accumulated allied-
block debt, first blocked tick, order activation tick, and unit id. Repeated
denial raises debt until even routine traffic receives an opening; strict
progress pays debt down. Physical legality, action roots, strike safety,
oscillation protection, route commitment, detour/rejoin, and closed-cycle
refusal remain executor invariants and cannot be overridden by `urgent`.

## 4b. The squad program (`arc-relay-orders-v3`)

A v3 sheet adds one required top-level key, `program`, and may tag order
nodes with `phase`. The program is a bounded whole-team state machine the
runtime coordinator executes once per tick, before any body evaluates:

```jsonc
"program": { "tactics": [ {           // 1..8, ordered = selection priority
  "id": "t1", "name": "Mid Pincer",   // id ≤24 kebab; name ≤48 display
  "start":  [ Case… ],                // optional, 1..4; absent = always
  "abort":  [ Case… ],                // optional, 1..4; checked every phase
  "abortThroughPhase": "p1",         // optional inclusive boundary; needs abort
  "maxTicks": 420,                    // optional, 1..1200, from tactic entry
  "rearm": 40,                        // 0..1200 ticks before reselection
  "phases": [ {                       // 1..8, ordered and named
    "id": "p1", "name": "Set the trap",
    "continue": [                     // ≤3 rules, authored order, first true
      { "when": Case, "to": "next" | "p3" | "finish" }
    ]                                 // absent on the FINAL phase = hold
  } ]                                 //   forever; required on every other
} ] }

Case = { "all"?: [entry…≤24], "any"?: [entry…≤24] }
// entry = condition name | one nested {"any":[2..8 names]} / {"all":[…]}
// truth = all(all…) AND any(any…); an absent side is vacuously true
```

Coordinator contract, in tick order:

1. **Select** the first tactic whose `start` passes and whose `rearm` has
   elapsed, when none is active. No active tactic ⇒ the whole team runs the
   untagged baseline band.
2. **Abort/duration**: on an active tactic (never on its entry tick),
   `abort` cases and `maxTicks` end the run, stamp `rearm`
   (`tick + 1 + rearm`), and reselect the list the same tick.
   `abortThroughPhase`, when present, stops evaluating abort cases after that
   phase; duration remains tactic-wide. The boundary names a declared phase
   and does not itself cause a transition.
3. **Transition**: the current phase's `continue` rules evaluate in authored
   order; the first true one fires — to the next phase, a strictly LATER
   named phase, or `finish` (which reselects like an abort). At most one
   transition per tick, never on the phase's entry tick.
4. **Enable**: the active pair lights tags `t{n}` (tactic-scoped nodes:
   delivery, Core habits) and `t{n}/p{k}` (that phase's subtrees). Every
   other tagged node is dark: ineligible, its per-body state removed WITHOUT
   a cooldown, so a restarted tactic begins fresh — an abandoned phase is
   never implicitly resumed.

Consequences the encoding relies on:

- **Latching is structural.** An entered phase's entry condition is never
  re-consulted; a lapsed trigger cannot rewind anything.
- **Selection is the only priority moment.** A higher tactic's `start`
  turning true mid-run cannot shadow, pause, or terminate the active
  operation.
- **Respawns join the current phase.** Phase membership is squad state, not
  a per-body gate; a fresh life evaluates the lit tags like everyone else.
  Even a full wipe does not rewind the operation.
- **Readiness is exact.** Program cases evaluate once per tick at squad
  level, so a transition may demand every member of every group be at its
  ground or dead (per-bot `any(role-in-zone-count ≥ 1, role-live-count ≤ 0)`
  pairs) — no quorum approximation and no short-handed excuse.
- Program-case conditions must be body-independent: `self-*`,
  `signature-*`, and `deployment-*` facts are rejected there, and
  `since: "phase-entry"` is legal only there.
- Validation enforces: tags reference declared ids; `to` is strictly
  forward; each declared phase owns at least one tagged node across roles;
  the program and the v3 schema come together or not at all.

## 5. Conditions

Facts are named once under `conditions`. An atomic condition is:

```json
"hurt": {
  "fact": "self-health-percent",
  "operator": "at-most",
  "value": 35
}
```

Operators are `at-least`, `at-most`, `equals`, `less-than`, and
`greater-than`. Qualifiers are legal only for facts that require (or
optionally admit) them:

- `role` for a declared role;
- `zone` for a declared zone (`self-nearest-visible-enemy-distance` may take
  an optional zone);
- `well` for `north`, `centre`, `south`, `own`, or `enemy` (optional on the
  Core-evidence facts and `friendly-carriers-in-roles`);
- `freshnessTicks` (`1..600`) for remembered facts;
- `windowTicks` (`1..1200`) **or** `since` (`match-start` | `node-entry` |
  `phase-entry`) — exactly one — for historical evidence facts;
  `since: "node-entry"` is legal only inside `until`, and
  `since: "phase-entry"` (v3) only inside program cases, where the
  coordinator anchors it at the active phase's entry tick;
- `roles` (1..8 unique declared roles) for `friendly-carriers-in-roles`
  (required) and the Core-evidence facts (optional);
- `signature` (a current signature id) for `role-deployments-in-zone-count`;
- `coreBankValue` (`{ "operator": equals|at-least|at-most, "value": 1..4 }`)
  for `self-nearest-visible-loose-core-distance`,
  `friendly-carriers-in-roles`, and the Core-evidence facts;
- `correlateAcquisition: true` only on role-filtered bank/drop/loss/resolution
  evidence, correlating events by authoritative Core identity;
- `outcomes` (1..3 unique of `bank`, `drop`, `loss`) required on
  `friendly-core-resolutions`.

An order expression is one condition name, or an explicit combination of
names:

```json
{ "all": ["hurt", "heal-zone-known"] }
{ "any": ["carrier-visible", "reactor-threatened"] }
{ "all": ["ready", { "any": ["enemies-here", "enemy-contact"] }] }
```

`all`/`any` contain 2–8 entries: condition names, plus at most one level of
nested `all`/`any` groups (whose own entries are names only) — the shape a
release latch needs to say "ready AND any trigger". `always` is the only
built-in condition name. Opposites are expressed with an atomic comparison,
not textual negation. This removes string parsing and ambiguous precedence
while keeping the editor's AND/OR control honest.

The v2 fact catalog is intentionally finite:

| scope | facts |
| --- | --- |
| body | `self-health-percent`, `self-level`, `self-carrying`, `self-carried-core-bank-value`, `self-carrying-core-from-well`, `self-stuck-ticks`, `self-order-ticks`, `self-in-zone`, `self-nearest-visible-enemy-distance`, `self-nearest-visible-loose-core-distance`, `self-nearest-heal-zone-distance`, `self-ticks-since-signature-use`, `signature-ready`, `signature-commitment-active`, `deployment-active`, `deployment-lifetime-percent`, `deployment-durability-percent`, `self-deployment-in-zone` |
| role | `role-live-count`, `role-lowest-health-percent`, `role-highest-level`, `role-in-zone-count`, `role-deployments-in-zone-count` |
| team | `live-friendlies`, `live-enemies`, `friendly-carriers`, `friendly-carriers-in-roles`, `known-enemy-carriers`, `visible-enemy-carriers`, `known-loose-cores`, `visible-loose-cores`, `secured-cores`, `reactor-integrity`, `reactor-charge`, `reactor-pulses`, `enemy-reactor-charge`, `friendly-respawns-next-wave`, `enemy-respawns-next-wave`, `ticks-until-respawn-wave`, `ticks-without-objective-progress`, `friendly-destructions` (historical: windowTicks/since), `elapsed-ticks` (the evaluation's own anchor: ticks-in-order on a node, ticks-in-phase in a program case) |
| zone | `friendlies-in-zone`, `visible-enemies-in-zone`, `remembered-enemies-in-zone`, `zone-cleared`, `friendly-fights-in-zone`, `known-loose-cores-in-zone`, `observed-enemy-entries-in-zone`, `distinct-enemy-lives-observed-in-zone`, `confirmed-enemy-destructions-in-zone`, `zone-fully-observed-throughout-interval` |
| Well | `well-has-outstanding-core`, `well-ticks-until-birth`, `well-socket-filled` |
| Core evidence | `friendly-core-pickups`, `friendly-core-acquisitions`, `friendly-core-banks`, `friendly-core-drops`, `friendly-core-losses`, `friendly-core-resolutions` |

`self-order-ticks` is legal in a node's `when`/`until` only. Remembered facts
require `freshnessTicks`; the sheet therefore states how old knowledge may be.
Known loose Cores use the team objective ledger and remain known until a
visible event proves pickup, handoff, bank, drop, or a new position. The ledger
does not forget a Core merely because it left current vision.

`self-nearest-visible-enemy-distance` is the fewest legal movement steps this
body needs to reach a tile from which it can attack a team-visible enemy. Known
durable blockers count; body occupancy is transient traffic and does not. No
reachable firing tile fails every authored upper-bound comparison.

`signature-commitment-active` is a body-local boolean view of the engine's
public tell/acquisition and channel phases. It excludes independent in-flight
effects and persistent deployments after the caster is free. It exists so an
ordinary sibling may intentionally hold without copying a signature duration.

`visible-enemies-in-zone` is the instantaneous advanced fact labelled **No
enemies currently visible in this area** when compared with zero. It is not
clearance. `zone-cleared` is one only after every walkable zone tile has been
observed through team vision in the rolling last 20 ticks and no unresolved
live-enemy observation remains there. Later observation outside the zone,
confirmed destruction, observing the last-known tile empty, or expiry resolves
that memory. `friendly-fights-in-zone` counts selected friendly Fight
Instructions whose fighter or target intersects the zone and pending strikes
aimed into it. It reads the previous normal decision's selections by design.

## 6. Six actions

Only leaves act. Their action key is exactly one of `move`, `fight`, `scan`,
`use`, `handoff`, and `drop`.

### 6.1 `move`

`move` has exactly one motion kind:

```json
{ "move": { "to": "nearest-known-loose-core", "via": "north-ingress", "posture": "front" } }
{ "move": { "patrol": "north-loop", "posture": "line" } }
{ "move": { "follow": "runner", "posture": "rear" } }
```

- `to` accepts `own-reactor`, `nearest-heal-zone`,
  `nearest-known-loose-core`, `highest-bank-value-known-loose-core`,
  `current-position`, `{ "zone": id }`, `{ "anchor": id }`,
  `{ "adjacentTo": id }`, or `{ "well": "north|centre|south|own|enemy" }`.
- `patrol` names a route and repeats it.
- `follow` names a role and resolves the nearest live body by terrain path
  distance, then unit id. It tracks the body from a legal adjacent standoff
  tile, not the leader's occupied tile or another body's current order.
- `via` names an ingress route for `to` or `patrol`. It is traversed once on
  entry, then the main destination takes over. Combat pre-emption does not
  restart it. Exit, death, or an impossible route resets it.
- `posture` is optional and is `front`, `line`, or `rear`. It is an individual
  pre-combat positioning intent relative to the resolved destination. It is not
  a formation, a combat behavior, or a promise that several roles coordinate.

Routes guide intent; reaching the resolved destination outranks blindly
visiting every waypoint. Route hysteresis, traffic detours, and corner legality
remain executor/engine responsibilities.

Walking onto a loose Core automatically picks it up when game rules allow.
Holding (`Wait`) on a heal-zone tile activates its healing channel. No separate
`activate` verb exists; contention for the tile remains executor movement micro.
Walking a carried Core into the owner's reactor automatically banks it. Those
are physical game rules, not hidden `move` subverbs.

`current-position` submits an intentional hold. The normal generated use is a
child before it and the movement/fight continuation is a child after it. A
condition on `signature-commitment-active` therefore protects only the
authoritative commitment; a higher root priority can still pre-empt and move.

### 6.2 `fight`

```json
{
  "fight": {
    "targets": ["carrier", "weakest", "nearest"],
    "style": "advance",
    "chaseLimit": 5
  }
}
```

- `targets` is 1–5 unique values from `carrier`, `weakest`, `nearest`,
  `medic`, `marked`, `veteran`, and `deployment` in desired order. The order applies among actionable
  targets: an enemy with no reachable firing tile inside the current chase
  leash, after known durable blockers, does not mask a later actionable
  preference. Body occupancy remains traffic. If no target is actionable, the
  fight order yields to the body's next eligible order for that tick without
  resetting the active fight episode or its chase anchor.
- `style` is `advance`, `hold`, or `kite`. It says how this body positions
  while executing the fight; the class's legal movement pattern still wins.
- `chaseLimit` is `0..12` tiles from the position at which this fight node
  entered. Zero means fight in place.

There is no hidden engage or break-off condition. The node's `when`, `until`,
`timeout`, and `cooldown` say when it owns the body. If no legal target resolves,
the next order is considered. Carrying does not implicitly drop a Core and a
class unable to shoot receives a traceable action-decline; authors normally put
an explicit empty-handed condition on combat orders.

### 6.3 `scan`

```json
{ "scan": { "zone": "mid-clearance", "freshWithinTicks": 20 } }
```

Scan is executor micro expressed in the Orders tree; it is not an engine action,
a Tactics job, or an allocator. The executor proposes deterministic legal
viewing positions using the actual map walls, cardinal facing and the body's
vision profile. It prefers a useful rotation in place, otherwise ordinary
movement, and holds a useful target until it is satisfied, invalid,
unreachable or pre-empted. Generated phase scans use existing target capacity:
one body per viewing tile and at most two per zone. The leaf stops proposing
when its clearance evidence is satisfied. `freshWithinTicks` is `1..600`;
Tactics-generated clearance always writes `20`.

### 6.4 `use`

```json
{ "use": { "skill": "repair-beam", "target": "weakest-ally" } }
```

At least one class in the role must own `skill`. In a mixed-class role, only a
body that owns the named skill can take that leaf; other bodies continue down
the same ordered list. `target` may be a descriptor-admitted named selector, a
declared friendly role, the most-experienced ally, Arc Toss's named-role
`aheadVia` relation, or the generic terminal-route forward relation below. A
descriptor-authorized position signature may also state `at: "anchor-id"`.
The compiler consumes the one typed signature-authoring catalog; it does not
maintain a second per-skill switch. The executor deterministically converts
the resolved target to the engine's unit, position, heading, or parameterless
argument.

`use` never implies travel, target search outside known information, or another
order. If the skill is unavailable or the selector cannot resolve, lower
priority continues. Geographic use is composed from a `self-in-zone` condition
on the skill leaf and an independently authored move to that zone.

Arc Toss's paired route qualifier remains:

```json
{ "role": "relay-b", "aheadVia": "return-route",
  "minimumForwardTiles": 3 }
```

Both qualifiers are required and mean the named receiver has unique authored
route progress at least the stated distance ahead.

The generic self-relocation relation is:

```json
{ "route": "safe-return", "progress": "forward",
  "minimumForwardTiles": 2 }
```

All fields are required; only `forward` exists. The route is terminal. The
executor reads its already-governed forward course and selects the furthest
legal straight relocation within authoritative mechanics. It declines bends,
ambiguous folded projection, illegal/blocked headings, or insufficient gain.
The following ordinary sibling runs in that same tick. A successful relocation
rejoins monotonically and cannot reset route progress behind the body.

### 6.5 `handoff`

```json
{ "handoff": { "target": "nearest-ally-toward-own-reactor" } }
```

The supported targets are `nearest-ally-toward-own-reactor`,
`nearest-empty-handed-ally`, `{ "role": id }`, or
`{ "selector": "nearest-ally-toward-own-reactor", "roles": [ … ] }` to
restrict the receiver to declared roles. Resolution is terrain path
distance, then unit id. A handoff is a direct adjacent transfer. It never drops
the Core and it never moves either body. If no legal receiver exists, the next
order runs.

The receiver does not need to author a matching order. It must be alive,
empty-handed, adjacent at resolution, and eligible to reclaim that Core.

### 6.6 `drop`

```json
{ "drop": true }
```

Drop releases the carried Core onto the legal adjacent tile with shortest
terrain distance toward the owner's reactor, then canonical coordinate
tie-break. If no adjacent tile is legal, the action is declined. A Core is never
dropped under the carrier, so it cannot be picked straight back up without
moving.

## 7. Capacity: one primitive, one job

A leaf may use an inline integer or a shared name:

```json
"capacities": { "two-runners-per-core": 2 }

{ "id": "collect", "capacity": "two-runners-per-core",
  "move": { "to": "nearest-known-loose-core" } }
```

Capacity means exactly:

> At most N bodies may concurrently claim the same resolved target through
> leaves sharing this capacity.

The range is `1..8`. An inline-capacity leaf may add
`"select": "highest-veteran"` to prefer veteran level (then walking distance,
then unit id) among claimants of its explicit selection group. A named
capacity must have at least two expanded leaf
consumers; a one-off is inline. It has no variants, lifecycle, selectors,
orders, fight fields, or role overrides.

The target key is the resolved Core, enemy, ally, leader body, Well, zone,
anchor, route, placement tile, or carried Core. Before action selection each
eligible body resolves its ordered candidate targets. Claim winners are chosen
by terrain path distance to that target, then unit id. A loser tries its next
candidate target, then its next order. Claims are recomputed every tick; they
are not reservations and do not survive death or target disappearance.

For a local integer, the capacity scope is that expanded leaf identity. For a
shared name, it spans all consuming leaves on the team. Capacity controls only
whether a body may participate. It does not author its movement or action.

## 8. Map plan

Geometry is keyed by id, not repeated arrays of objects:

```json
{
  "zones": {
    "midfield": { "rect": [9, 8, 21, 18] }
  },
  "routes": {
    "north-loop": {
      "waypoints": [[7, 9], [12, 9], [12, 5], [7, 5]]
    }
  },
  "anchors": {
    "home-gate": [7, 9]
  }
}
```

Rects are inclusive `[x1,y1,x2,y2]` and normalized. Routes contain 2–32
waypoints. The pure shared compiler checks structure and references. Runtime
binding checks that coordinates are in bounds and on traversable terrain against
the resolved engine-generated map; the App and CLI surface that binding failure
before a trial starts. Route waypoints cannot occupy a reserved spawn or an
opponent-only home tile. A movement zone needs a corner-strict connected legal
component, in both bindings, large enough for the bodies its order tree may send
there; an authored capacity can deliberately lower that count. The compiler
rejects duplicate geometry under different ids and rejects map-plan objects with
no sheet consumer. There are no aliases, bindings, corridor widths, formation
anchors, fingerprints, or authored hashes.

The editor plots these objects on the actual `contract.map` export for
`mapId`. Text coordinates are an import/export detail, never the primary UI.

## 9. Core reclaim invariant

After a voluntary `drop`, `handoff`, or Arc Toss, the source body cannot pick up
that same Core until the ruleset's existing Core relocation interval expires.
The key is `(body life, Core id)`; this reuses the rules-owned interval instead
of introducing a second tactical number:

- allies and enemies may pick it up immediately;
- death clears the source body's block;
- a receiver still blocked for that Core is not a legal handoff target;
- automatic pickup checks the block before attaching the Core;
- a declined pickup/action emits `core-reclaim-cooldown` in observable engine
  evidence.

This is a game-contract invariant because it prevents physical drop/pickup
ping-pong. It is not repeated as sheet configuration and does not stop a player
from expressing a longer tactical cooldown on an order.

## 10. Compiler and runtime contract

The shared compiler validates the source pair and emits `ATL2` (the linked
envelope is capped at 128 KiB; the map plan carries at most 128 zones, 128
routes and 128 anchors — raised from 64 for whole-team Tactics
materialization, `DECISIONS.md` #331), containing:

- the exact role/class/upgrade assignment;
- the exact expanded order tree, with source paths and authored ids;
- compiled condition nodes;
- capacity ids and values;
- normalized team-relative map geometry and its computed hash.

It emits no v1 playbook, doctrine, custody, engagement, formation, group, mode,
rung, or generated order record. The CLI and App call the same compiler.

The direct executor evaluates the ATL2 tree. Its trace prefix is:

```text
orders:<role>:<order-path>:<action>:<reason>
```

Every submitted action, decline, capacity loss, target miss, lifecycle exit,
wait, and automatic micro decision has a non-empty reason. Movement reasons
include the authored urgency and current block debt, for example
`urgency-routine+b13=>normal`, and a clearance reason names the priority it
yielded to.
In-process and WASM must consume identical ATL2 bytes and produce the same
gameplay-state digest.

## 11. Deletions and migration

This branch replaces the active Hunter, Wellwright, and starter sheets with v2
sources and Tapline-native v2 map plans. The v1 authoring contract, v1 compiler,
v1 direct ladder, v1 editor types/validation, custody plane, doctrine plane, and
obsolete active templates are deleted rather than retained behind switches.

Immutable hosted artifacts/playlists are not amended. If the new mind is made a
hosted entrant, it receives a new artifact directory and playlist version under
the repository's immutable-version rules.

The editor presents roles as eight class bodies and orders as draggable tactical
cards. Nested orders open in context; map targets open the map beside the card;
conditions and capacities are reusable tactical assets; signature skill copy is
class-aware. Raw JSON and hashes are not primary product controls.

## 12. Explicitly deferred

Urgency closes allied right-of-way, not tactical choreography. Formation-like
lane sharing, authored offsets, combat-space reservation, and a different
cooperative path planner remain separate design questions. They must not grow
inside `urgency` or silently become order-selection rules.

Competent Hunter and Wellwright behavior here means they follow their authored
orders, collect and deliver Cores at a reasonable pace, and avoid max-tick
timeouts in the standard exhibit. It is not a balance claim.

### Move `near` anchor (2026-08-19)

A `move.to: "nearest-heal-zone"` may carry `"near": "<zoneId>"` (a declared
zone): "nearest" then resolves from that zone's centre instead of the
body's own position, so a squad regrouping at authored ground shares one
heal tile rather than each bot picking whichever is closest to where it
happens to stand. Optional; absent keeps the historical body-relative
ordering byte for byte. Legal only on `nearest-heal-zone`.
