# Arc Relay sheet authoring

This is the current product contract for Arc Relay sheets, the shared compiler,
and the sheet workshop. `docs/SPEC-SHEET-ORDERS-V2.md` records the design
rationale; this document is the authoring reference.

Only Orders v2 is current. There is no compatibility parser or desugaring path
for doctrines, custody policies, modes, rungs, cohorts, focus scopes,
formations, engagements, patrol verbs, or authored hashes. Historical source
and frozen artifacts remain evidence, not valid editor input.

## 1. One sheet, two documents

A sheet is exactly:

- an Orders document with schema `arc-relay-orders-v2`; and
- a map-plan document with schema `arc-relay-map-plan-v2`.

The Orders document says which eight class bodies exist and what each role
tries in priority order. The map plan names the zones, routes, and anchors those
orders reference. Both documents are authored from west's perspective; the
executor rotates the map plan 180 degrees for east.

The shared compiler is
`BotArena.TacticalPlaybooks.ArcRelayTacticalPlaybookCompiler`. The CLI and App
call that same compiler. It validates both documents and emits one bounded
`ATL2` package that the mind executes directly. The package contains the
authored order tree; it contains no legacy intermediate representation. It may
not exceed 65,536 bytes.

Compile a file-backed sheet with:

```sh
nilbots experiment arc-relay-playbook \
  --playbook path/to/orders.json \
  --out out/arc-relay-sheet
```

The command accepts `--playbook` as a historical CLI spelling, but the input is
an Orders v2 document. It writes the normalized source pair, `playbook.atp`
(the ATL2 package), and an explain view whose paths use authored role and order
names.

## 2. Exact roots

The Orders root has exactly seven keys:

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

The map-plan root has exactly six keys:

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

Unknown or missing keys are errors. Identifiers are lowercase kebab-case,
unique in their scope, and at most 48 characters (role ids at most 24). Every
declared condition and every map object needs a consumer. A shared order or
named capacity needs at least two expanded consumers; otherwise inline it.
Empty libraries are legal.

Hashes, bindings, perspective, memory windows, allocation policies, and a
separate composition list are not authored. The compiler derives composition,
normalizes the pair, and computes identity.

## 3. Roles cast the eight bodies

`roles` is an ordered list of one to eight entries:

```json
{
  "id": "runner",
  "classes": ["relay", "switchback"],
  "upgradePath": ["reach", "vision", "vitality"],
  "orders": []
}
```

Flattening every `classes` list in source order assigns unit ids 0–7. The
flattened list must contain exactly eight launch classes, with at most two
copies of any class and at least seven distinct classes on the current
Tapline-4 line. A body keeps its role across death and respawn. There is
no candidate pool, allocator, vacancy, promotion, overflow, or reassignment.

`upgradePath` contains one to four values from `damage`, `vision`, `reach`, and
`vitality`. A body invests along the list and repeats the final entry once the
list is exhausted. Bodies may share a role only when their complete order tree
and upgrade path are the same.

The launch class ids are:

```text
kestrel  palisade  towline  patchbay  lantern  mortar  minesmith  hush
relay    switchback  longshot  mason  sunder  repulsor  veil  nest
```

The hosted App additionally validates class unlocks.

## 4. One nested priority tree

`orders` is a priority list. The executor walks it top to bottom and the first
executable leaf wins. An entry is one of:

- an inline branch with child `orders`;
- an inline leaf with one action; or
- `{ "shared": "name" }`, an exact shared-order reference.

Example:

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

Branches and leaves may use `when`, `until`, `timeout`, `cooldown`, and
`urgency`. Only a leaf may use `capacity`. A leaf has exactly one of `move`,
`fight`, `use`, `handoff`, or `drop`. Two branch levels plus a leaf is the
maximum depth.

`sharedOrders` maps a name to a complete branch or leaf. The map key is its id,
so the definition does not repeat `id`. Reuse is exact: no parameters,
overrides, inheritance, aliases, or shared-to-shared references. Each expanded
copy has independent per-body lifecycle state and occupies the reference's
priority position.

### Lifecycle

| field | contract |
| --- | --- |
| `when` | permits entry; absent means `always` |
| `until` | once entered, latches the node until this becomes true |
| `timeout` | active wall-clock ticks, `1..1200` |
| `cooldown` | ticks before this same node may enter again, `0..1200` |
| `urgency` | allied movement weight: `routine`, `normal`, or `urgent` |

- Without `until`, `when` remains the node's ongoing eligibility.
- With `until`, `when` gates entry and `until` owns exit.
- Timeout, successful one-shot action, death, or ancestor exit ends the node.
  Cooldown starts on exit.
- Higher-priority work may pre-empt an active node without resetting it; its
  timeout continues and it may resume.
- `move`, `fight`, and branches are ongoing. A `move.to` holds at its resolved
  destination until lifecycle exit. `use`, `handoff`, and `drop` complete only
  when the engine accepts the action.
- A leaf with no legal target is skipped for that tick. The executor never
  invents a different action or hidden travel.
- If no leaf is executable, the body waits with `no-eligible-order` evidence.

### Movement urgency

Order position answers **what this body does**. `urgency` answers **who gets
scarce friendly movement space** after an order has already won. It is optional,
inherits through nested orders, and defaults to `normal` at the top. The nearest
explicit value wins.

Use `routine` for work that may readily yield, `normal` for ordinary movement,
and `urgent` for explicitly time-sensitive travel. Urgency never changes order
selection, conditions, targets, capacities, combat, or enemy behavior. Sustained
allied blocking accrues deterministic movement debt, so `routine` cannot starve
behind an endless stream of fresh `urgent` traffic. Exact ties resolve by first
blocked tick, activation tick, then stable unit id. The executor still owns
collision legality, strike roots, safe movement, deadlock/cycle refusal,
oscillation protection, and route detour/rejoin.

Example inheritance:

```json
{
  "id": "score-a-core",
  "when": "carrying-core",
  "urgency": "urgent",
  "orders": [
    { "id": "return-home", "move": { "to": "own-reactor" } }
  ]
}
```

`return-home` inherits `urgent`. Its source position still determines whether it
runs; the value matters only if a friendly body contests its movement space.

Traces use authored paths, for example
`orders:runner:carry-core-home>return-home:move:...`. Generated rung or order
ids never appear on product surfaces.

## 5. Conditions

Conditions are named once:

```json
"hurt": {
  "fact": "self-health-percent",
  "operator": "at-most",
  "value": 35
}
```

Operators are `at-least`, `at-most`, `equals`, `less-than`, and
`greater-than`. An order's `when` or `until` is a condition name or one flat
combination:

```json
{ "all": ["hurt", "heal-zone-known"] }
{ "any": ["carrier-visible", "reactor-threatened"] }
```

`all` and `any` contain two to eight unique declared names and do not nest.
`always` is the sole built-in name and cannot be declared.

The finite fact catalog is:

| scope | facts |
| --- | --- |
| body | `self-health-percent`, `self-level`, `self-carrying`, `self-stuck-ticks`, `self-order-ticks`, `self-in-zone`, `self-nearest-visible-enemy-distance`, `signature-ready` |
| role | `role-live-count`, `role-lowest-health-percent`, `role-in-zone-count` |
| team | `live-friendlies`, `friendly-carriers`, `known-enemy-carriers`, `visible-enemy-carriers`, `known-loose-cores`, `visible-loose-cores`, `secured-cores`, `reactor-integrity`, `reactor-charge`, `ticks-without-objective-progress` |
| zone | `friendlies-in-zone`, `visible-enemies-in-zone`, `remembered-enemies-in-zone`, `known-loose-cores-in-zone` |
| Well | `well-has-outstanding-core`, `well-ticks-until-birth`, `well-socket-filled` |

Role facts require `role`; zone facts require `zone`; Well facts require
`well` (`north`, `centre`, `south`, `own`, or `enemy`).
`known-enemy-carriers` and `remembered-enemies-in-zone` require
`freshnessTicks` in `1..600`. Qualifiers on other facts are errors. Health
percent comparisons are `0..100`; boolean facts compare with `0` or `1`.

Known loose Cores use the team's objective ledger. A Core remains known until
a visible pickup, handoff, bank, drop, or new position updates it; leaving
vision alone does not erase the fact.

`self-nearest-visible-enemy-distance` is the fewest legal movement steps this
body needs to reach a tile from which it can attack a team-visible enemy. The
sighting may come from any allied sensor, but approach distance is evaluated
separately for every body. Known durable blockers (map walls, protected terrain,
spawn reservations and visible blocking constructs) count; allied and enemy
bodies are transient traffic and do not. Use an upper-bound comparison in
`0..31`, normally `at-most`; no reachable firing tile fails every such upper
bound.
`visible-enemies-in-zone` remains the explicit non-distance team-perception
fact for a coordinated response to contact reported anywhere in a named zone.

## 6. Five actions

### `move`

`move` has exactly one motion kind:

```json
{ "move": { "to": "nearest-known-loose-core", "via": "north-ingress", "posture": "front" } }
{ "move": { "patrol": "north-loop", "posture": "line" } }
{ "move": { "follow": "runner", "posture": "rear" } }
```

`to` accepts:

- `own-reactor`, `nearest-heal-zone`, or `nearest-known-loose-core`;
- `{ "zone": "id" }`;
- `{ "anchor": "id" }`; or
- `{ "adjacentTo": "anchor-id" }`; or
- `{ "well": "north|centre|south|own|enemy" }`.

`adjacentTo` resolves the legal eight-way ring around one authored anchor. It
is useful for placeable skills: the anchor states where the construct belongs
once, the `use.at` leaf targets it exactly, and a lower move leaf brings the
body beside it. Occupancy and casting legality remain runtime facts; no
duplicate staging zone or companion anchor is authored.

`patrol` repeats a route. `follow` tracks the nearest live body of the named
role by terrain path distance, then unit id, while standing on a legal adjacent
tile rather than asking for the leader's occupied tile. `via` is an optional
one-time ingress route for `to` or `patrol`; follow cannot use it. Combat pre-emption
does not restart ingress. Exit, death, or an impossible route does.

Optional posture is `front`, `line`, or `rear`. It is individual pre-combat
positioning relative to the destination, not a formation or combat behavior.

Routes guide intent. Reaching the destination outranks blindly visiting every
waypoint. Route hysteresis, traffic detours, legal corners, and collision
avoidance are deterministic executor/engine micro. A `via` or patrol waypoint
is satisfied within one traversable terrain step when the body has genuinely
approached it; adjacent waypoints clamp that slack to zero. This keeps routes
as corridors rather than compulsory pinholes without allowing a stationary
body to lap a route cursor.

Walking onto a legal loose Core picks it up. Walking a carried Core into the
owner's reactor banks it. Those are physical rules, not hidden action verbs.
Holding (`Wait`) on a heal-zone tile channels healing. No separate `activate`
action is authored; contention for the tile remains executor movement micro.

### `fight`

```json
{
  "fight": {
    "targets": ["carrier", "weakest", "nearest"],
    "style": "advance",
    "chaseLimit": 5
  }
}
```

`targets` contains one to five unique values from `carrier`, `weakest`,
`nearest`, `medic`, and `marked`. They are ordered preferences among targets
this body can shoot or reach through known durable obstacles within the current
fight episode. Body occupancy remains movement traffic rather than terrain. An
unavailable preference does not mask a later actionable one; if none has a
reachable firing tile, the fight order yields for that tick and the body's next
eligible authored order runs. The episode remains active, so this yield never
resets or advances its chase anchor.
`style` is `advance`, `hold`, or `kite`.
`chaseLimit` is `0..12` tiles from the body's position when the fight node
entered; zero means fight in place.

There is no hidden engage or disengage condition. Lifecycle fields own entry
and exit. An unresolved target falls through to the next order. Fighting while
carrying never implicitly drops the Core.

### `use`

```json
{ "use": { "skill": "repair-beam", "target": "weakest-ally" } }
```

At least one class in the role must own the named skill. A mixed-class role may
state one `use` order per represented skill: only a body that owns the skill can
take that leaf, while the other bodies continue down their same role ladder.
Targets are `self`, `own-reactor`, `nearest-enemy`, `weakest-enemy`,
`enemy-carrier`, `nearest-ally`, `weakest-ally`, `healthiest-ally`, or
`nearest-ally-toward-own-reactor`; the compiler restricts them further for the
skill's argument kind. Use never implies travel or an unseen target search.
For `arc-toss`, the toward-home selector resolves only an empty-handed ally on
an exact legal landing tile, so the authored pass cannot silently become a
blind Core drop.

Geographic intent stays compositional rather than hidden inside `use`:
`self-in-zone` can guard a skill leaf, while a lower-priority `move` leaf in the
same parent play gets the body into that authored zone. If the skill is not
currently legal, the skill leaf has no target and ordinary priority falls
through to movement.

Placeable constructs (`hardlight-block`, `trip-node`, and `sentinel-seed`) may
also pin their exact placement with `"at": "anchor-id"`. The anchor must be an
adjacent legal placement tile when the order runs; otherwise the leaf falls
through. `at` does not imply travel. Pair it with a lower move order targeting
`{ "adjacentTo": "anchor-id" }` when the author wants the body to stage beside
that exact point. Both leaves reuse one semantic anchor; no companion staging
anchor or duplicate zone is needed.

### `handoff`

```json
{ "handoff": { "target": "nearest-ally-toward-own-reactor" } }
```

Targets are `nearest-ally-toward-own-reactor`,
`nearest-empty-handed-ally`, or `{ "role": "id" }`. Resolution uses terrain
path distance, then unit id. The receiver must be alive, empty-handed,
adjacent, and eligible to reclaim that Core. Handoff moves neither body and
does not require a matching receiver order.

### `drop`

```json
{ "drop": true }
```

Drop chooses the legal adjacent tile with shortest terrain distance toward the
owner's reactor, then canonical coordinate tie-break. It never drops beneath
the carrier. If no adjacent tile is legal, the action declines.

## 7. Universal target capacity

A leaf may use an inline integer or shared name:

```json
"capacities": { "two-per-core": 2 }

{
  "id": "collect",
  "capacity": "two-per-core",
  "move": { "to": "nearest-known-loose-core" }
}
```

Capacity means: at most N bodies may concurrently claim the same resolved
target through leaves sharing this capacity. N is `1..8`.

The target is the resolved Core, enemy, ally, followed body, Well, zone,
anchor, route, placement tile, or carried Core. Eligible bodies resolve target
candidates first; terrain path distance and then unit id choose winners. A
loser tries its next target and then its next order. Claims recompute every
tick and are not reservations.

An inline capacity is scoped to that expanded leaf identity. A named capacity
spans every consuming leaf. Capacity has one job only: participation count. It
has no selectors, lifecycle, behavior, or per-role overrides.

## 8. Map plan

Geometry is keyed by id:

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

Rectangles are inclusive and normalized. Routes contain 2–32 waypoints.
Zones, routes, and anchors each allow at most 64 entries. Duplicate geometry
under different names is an error. Every map object must be referenced from
the Orders document.

The current hosted map is Tapline: `arc-relay-tapline-01`, 31×27. Runtime
binding validates bounds and traversable terrain against the engine-generated
map. Geometry is never projected between maps.

The workshop draws the exact map export from `GET /api/sheets/catalog`,
including walls, Wells, heal tiles, delivery shoulders, reactor pads, and
spawns. Routes, zones, and anchors are plotted and dragged on that map; raw
coordinates are only an import/export detail. The west/east preview shows the
exact 180-degree binding.

## 9. Core reclaim invariant

After a voluntary `drop`, `handoff`, or Arc Toss, the source body cannot pick
up that same Core until the existing rules-owned relocation interval expires.
The key is body life plus Core id.

- Allies and enemies may collect it immediately.
- Death clears the source body's block.
- A blocked receiver is not a legal handoff target.
- Automatic pickup checks the block before attaching the Core.
- A decline emits `core-reclaim-cooldown` evidence.

This is a game-contract invariant that prevents physical ping-pong. It is not
configurable sheet micro. An author may still put a longer tactical cooldown
on an order.

## 10. Workshop and hosted loop

The product loop is **change → practice → watch → change**.

- `GET /api/sheets/catalog` supplies the Tapline map, class/signature catalog,
  stable `signatureId`/`actionId` ownership, unlocks, starter pair, and stock
  opponents. The workshop consumes those stable IDs; it does not maintain a
  second class-to-signature table.
- `GET/POST/PUT/DELETE /api/sheets` persists owner-scoped source pairs and
  optimistic revisions. Every save compiles on the server through the shared
  compiler. The stored source-size ceilings are 192 KiB for Orders and 96 KiB
  for the map plan.
- `POST /api/sheets/{id}/trial` runs the saved draft against a selected frozen
  stock sheet and opens the replay viewer.
- Saving fields a sheet in the current ladder by default, subject to the
  account cap; opting out is deliberate.

The browser keeps a local draft before save, trial, or navigation, so compile
failure, match failure, and phone-tab eviction do not erase work. Compiler
diagnostics retain source paths and attach to the card, condition, capacity, or
map object that caused them.

The primary workshop is card-and-map based:

- class/signature cards cast the eight bodies into exact roles;
- each role shows a nested, draggable priority tree;
- only the selected node and its ancestor chain are expanded;
- map-referencing orders open beside the map;
- conditions, capacities, and exact shared orders use reusable libraries;
- one persistent Practice action opens setup rather than duplicating actions;
- the last replay's actual tracks can overlay authored geometry;
- all controls are reachable by keyboard and touch at 390 px without
  hover-only behavior.

Raw JSON import/export is an advanced utility. Authored hashes are not shown or
edited. The workshop never exposes retired vocabulary.

## 11. Runtime and determinism

ATL2 contains the exact role/class/upgrade assignment, expanded order tree
with source paths, compiled conditions, capacities, and normalized map
geometry. The direct executor evaluates those bytes in both hosts.

Every submitted action and every decline, capacity loss, target miss,
lifecycle exit, wait, and automatic micro decision carries a non-empty reason.
In-process and WASM must produce the same gameplay-state digest for identical
ATL2, map, rules, and seed.

Hosted entrant playlist v22 is the promoted Orders v2/Tapline-4 line. It pins
the coordinated EPIBT executor, body-local signature orders, the current
controlled WASM artifact, the combined depth rules, and a fresh rating book.
Playlist v21 remains immutable Tapline-3 history.

## 12. Executor-owned squad micro

Orders state tactical intent, not squad micro. The executor jointly assigns
distinct contribution tiles and resolves physical motion with deterministic
EPIBT planning. Authored urgency participates only after an order is selected;
it may settle allied right-of-way but cannot reorder the sheet. Acyclic convoy
steps into a pre-tick vacancy are legal on Tapline-4, while swaps and closed
cycles remain blocked. Do not smuggle pathing policy into author-visible orders
or add route-defense fields to compensate for executor behavior.
