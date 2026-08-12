# SPEC — Arc Relay Orders DSL v2

Status: normative implementation contract for this branch.

Branch posture: this work belongs to
`/Users/sebastian.lind/hobby-projects/nilbots-wt/no-carrier-deadlock` on
`codex/editor-sweep-followup`, based on `main` at `5de215fa`. The main checkout
is not a work area for this goal.

This format replaces `arc-relay-tactical-playbook-v1`. There is deliberately no
compatibility layer. The compiler does not desugar v2 back into doctrines,
custody policies, modes, rungs, engagements, cohorts, focus scopes, or the old
six verbs. The executor reads the authored order tree.

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
  "mapPlan": "../map-plans/starter-tapline-v2.json",
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
keys, and optionally `capacity`. It has no `orders`.

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

Rules:

1. Without `until`, `when` is ongoing eligibility: the node exits when it
   becomes false. With `until`, `when` gates entry and `until` owns exit.
2. A timeout, successful one-shot leaf, death, or an ancestor exit also exits
   the node. Cooldown starts on exit, not on temporary pre-emption.
3. A higher-priority sibling may pre-empt a node without resetting it. Its
   wall-clock timeout continues, and it resumes if still active.
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
`greater-than`. Qualifiers are legal only for facts that require them:

- `role` for a declared role;
- `zone` for a declared zone;
- `well` for `north`, `centre`, `south`, `own`, or `enemy`;
- `freshnessTicks` (`1..600`) for remembered facts.

An order expression is one condition name, or an explicit combination of
names:

```json
{ "all": ["hurt", "heal-zone-known"] }
{ "any": ["carrier-visible", "reactor-threatened"] }
```

`all`/`any` contain 2–8 unique names and cannot nest. `always` is the only
built-in condition name. Opposites are expressed with an atomic comparison,
not textual negation. This removes string parsing and ambiguous precedence
while keeping the editor's AND/OR control honest.

The v2 fact catalog is intentionally finite:

| scope | facts |
| --- | --- |
| body | `self-health-percent`, `self-level`, `self-carrying`, `self-stuck-ticks`, `self-order-ticks`, `signature-ready` |
| role | `role-live-count`, `role-lowest-health-percent`, `role-in-zone-count` |
| team | `live-friendlies`, `friendly-carriers`, `known-enemy-carriers`, `visible-enemy-carriers`, `known-loose-cores`, `visible-loose-cores`, `secured-cores`, `reactor-integrity`, `reactor-charge`, `ticks-without-objective-progress` |
| zone | `friendlies-in-zone`, `visible-enemies-in-zone`, `remembered-enemies-in-zone`, `known-loose-cores-in-zone` |
| Well | `well-has-outstanding-core`, `well-ticks-until-birth`, `well-socket-filled` |

`self-order-ticks` is legal in a node's `when`/`until` only. Remembered facts
require `freshnessTicks`; the sheet therefore states how old knowledge may be.
Known loose Cores use the team objective ledger and remain known until a
visible event proves pickup, handoff, bank, drop, or a new position. The ledger
does not forget a Core merely because it left current vision.

## 6. Five actions

Only leaves act. Their action key is exactly one of `move`, `fight`, `use`,
`handoff`, and `drop`.

### 6.1 `move`

`move` has exactly one motion kind:

```json
{ "move": { "to": "nearest-known-loose-core", "via": "north-ingress", "posture": "front" } }
{ "move": { "patrol": "north-loop", "posture": "line" } }
{ "move": { "follow": "runner", "posture": "rear" } }
```

- `to` accepts `own-reactor`, `nearest-heal-zone`,
  `nearest-known-loose-core`, `{ "zone": id }`, `{ "anchor": id }`, or
  `{ "well": "north|centre|south|own|enemy" }`.
- `patrol` names a route and repeats it.
- `follow` names a role and resolves the nearest live body by terrain path
  distance, then unit id. It tracks the body, not another body's current order.
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
Walking a carried Core into the owner's reactor automatically banks it. Those
are physical game rules, not hidden `move` subverbs.

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
  `medic`, and `marked` in desired order.
- `style` is `advance`, `hold`, or `kite`. It says how this body positions
  while executing the fight; the class's legal movement pattern still wins.
- `chaseLimit` is `0..12` tiles from the position at which this fight node
  entered. Zero means fight in place.

There is no hidden engage or break-off condition. The node's `when`, `until`,
`timeout`, and `cooldown` say when it owns the body. If no legal target resolves,
the next order is considered. Carrying does not implicitly drop a Core and a
class unable to shoot receives a traceable action-decline; authors normally put
an explicit empty-handed condition on combat orders.

### 6.3 `use`

```json
{ "use": { "skill": "repair-beam", "target": "weakest-ally" } }
```

`skill` is the exact signature skill of every class in the role. `target` is
one of `self`, `own-reactor`, `nearest-enemy`, `weakest-enemy`,
`enemy-carrier`, `nearest-ally`, or `weakest-ally`. The
compiler validates the selector against the signature's parameter kind. The
executor deterministically converts the resolved target to the engine's unit,
position, heading, or parameterless argument.

`use` never implies travel, target search outside known information, or another
order. If the skill is unavailable or the selector cannot resolve, lower
priority continues.

### 6.4 `handoff`

```json
{ "handoff": { "target": "nearest-ally-toward-own-reactor" } }
```

The supported targets are `nearest-ally-toward-own-reactor`,
`nearest-empty-handed-ally`, or `{ "role": id }`. Resolution is terrain path
distance, then unit id. A handoff is a direct adjacent transfer. It never drops
the Core and it never moves either body. If no legal receiver exists, the next
order runs.

The receiver does not need to author a matching order. It must be alive,
empty-handed, adjacent at resolution, and eligible to reclaim that Core.

### 6.5 `drop`

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

The range is `1..8`. A named capacity must have at least two expanded leaf
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
before a trial starts. The compiler rejects duplicate geometry under different
ids and rejects map-plan objects with no sheet consumer. There are no aliases,
bindings, corridor widths, formation anchors, fingerprints, or authored hashes.

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

The shared compiler validates the source pair and emits `ATL2`, containing:

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
wait, and automatic micro decision has a non-empty reason. In-process and WASM
must consume identical ATL2 bytes and produce the same gameplay-state digest.

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

Tomorrow's separate goal owns tactical pathfinding and squad coordination:
choke negotiation, intent-aware right-of-way beyond existing rules, formation-
like lane sharing, combat-space reservation, and coordinated path planning.
This goal preserves and exercises the existing deterministic movement governor
but does not hide a new squad planner inside the DSL rewrite.

Competent Hunter and Wellwright behavior here means they follow their authored
orders, collect and deliver Cores at a reasonable pace, and avoid max-tick
timeouts in the standard exhibit. It is not a balance claim.
