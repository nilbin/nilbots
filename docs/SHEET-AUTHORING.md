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
  "mapPlan": "../map-plans/tapline-6/starter-tapline-v2.json",
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
  "mapId": "arc-relay-tapline-02",
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
Tapline-6 line. A body keeps its role across death and respawn. There is
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
| body | `self-health-percent`, `self-level`, `self-carrying`, `self-carried-core-bank-value`, `self-stuck-ticks`, `self-order-ticks`, `self-in-zone`, `self-nearest-visible-enemy-distance`, `self-ticks-since-signature-use`, `signature-ready`, `signature-commitment-active`, `deployment-active`, `deployment-lifetime-percent`, `deployment-durability-percent` |
| role | `role-live-count`, `role-lowest-health-percent`, `role-highest-level`, `role-in-zone-count` |
| team | `live-friendlies`, `live-enemies`, `friendly-carriers`, `known-enemy-carriers`, `visible-enemy-carriers`, `known-loose-cores`, `visible-loose-cores`, `secured-cores`, `reactor-integrity`, `reactor-charge`, `reactor-pulses`, `enemy-reactor-charge`, `friendly-respawns-next-wave`, `enemy-respawns-next-wave`, `ticks-until-respawn-wave`, `ticks-without-objective-progress` |
| zone | `friendlies-in-zone`, `visible-enemies-in-zone`, `remembered-enemies-in-zone`, `known-loose-cores-in-zone`, `observed-enemy-entries-in-zone`, `distinct-enemy-lives-observed-in-zone`, `confirmed-enemy-destructions-in-zone`, `zone-fully-observed-throughout-interval` |
| Well | `well-has-outstanding-core`, `well-ticks-until-birth`, `well-socket-filled` |

Role facts require `role`; zone facts require `zone`; Well facts require
`well` (`north`, `centre`, `south`, `own`, or `enemy`).
`known-enemy-carriers` and `remembered-enemies-in-zone` require
`freshnessTicks` in `1..600`. Qualifiers on other facts are errors. Health
percent comparisons are `0..100`; boolean facts compare with `0` or `1`.

The four historical zone facts are finite, read-only team evidence. Each needs
exactly one interval: `windowTicks: 1..1200`, `since: "match-start"`, or
`since: "node-entry"`. Node-entry evidence is legal only in the active order's
`until`. At decision tick T the interval ends before T's actions. Entries count
only outside-to-inside movement whose before and after tiles were both visible;
distinct lives use observed actor-life identities; destructions use disclosed
events; full coverage is 1 only when every traversable zone tile was visible on
every completed tick. Evidence survives body death but resets between matches.
The executor reason names the interval, count/lives, and first coverage gap, so
a replay can explain why a response activated without exposing hidden enemies.

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

`self-ticks-since-signature-use` is the age of this body's last successfully
accepted signature action, capped above the authored `0..1200` comparison
range. Before the body has used its signature it fails every upper-bound
comparison. This is a local transition latch; it does not create shared Play
state or imply another body's behavior.

`signature-commitment-active` is `1` only while this body's own authoritative
signature operation is in its public tell/acquisition or channel phase. It is
`0` for independent in-flight effects and persistent deployments after the
caster is free. This observes engine state; it is not an authored duration.

## 6. Five actions

### `move`

`move` has exactly one motion kind:

```json
{ "move": { "to": "nearest-known-loose-core", "via": "north-ingress", "posture": "front" } }
{ "move": { "patrol": "north-loop", "posture": "line" } }
{ "move": { "follow": "runner", "posture": "rear" } }
{ "move": { "to": { "zone": "killbox" }, "face": { "anchor": "lane-mouth" } } }
```

`to` accepts:

- `own-reactor`, `nearest-heal-zone`, `nearest-known-loose-core`, or
  `highest-bank-value-known-loose-core`;
- `current-position`, an intentional hold at the body's already-resolved tile;
- `{ "zone": "id" }`;
- `{ "anchor": "id" }`; or
- `{ "adjacentTo": "anchor-id" }`; or
- `{ "well": "north|centre|south|own|enemy" }`.

`adjacentTo` resolves the legal eight-way ring around one authored anchor. It
is useful for placeable skills: the anchor states where the construct belongs
once, the `use.at` leaf targets it exactly, and a lower move leaf brings the
body beside it. Occupancy and casting legality remain runtime facts; no
duplicate staging zone or companion anchor is authored.

`patrol` repeats a route. `follow` tracks either the nearest live body of a
named role by terrain path distance, then unit id, or the most-experienced live
ally by level, distance, then unit id. It stands on a legal adjacent tile rather
than asking for the leader's occupied tile. `via` is an optional
one-time ingress route for `to` or `patrol`; follow cannot use it. Combat pre-emption
does not restart ingress. Exit, death, or an impossible route does.

Optional posture is `front`, `line`, or `rear`. It is individual pre-combat
positioning relative to the destination, not a formation or combat behavior.

Optional `face: { "anchor": "id" }` is legal only on a `move.to` whose
destination is an authored zone or anchor. It never changes travel. Once the
body arrives, it keeps a facing whose bare 90-degree firing quadrant contains
the anchor, spending an ordinary rotation action only when needed. Displacement
first returns it to the destination; higher-priority orders still pre-empt it.
Patrols, follows, Wells, reactors, adjacent targets, and dynamic Core/heal
targets cannot use `face`. Selected-body intent draws travel in cyan and the
watched anchor in amber.

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

`current-position` also emits an intentional `Wait`. A signature opportunity
uses it with `signature-commitment-active` to keep a public tell or channel
from being cancelled by its own movement continuation. A higher-priority
authored order may still pre-empt the parent and move, accepting the engine's
normal cancellation consequence.

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
`nearest`, `medic`, `marked`, `veteran`, and `deployment`. They are ordered preferences among targets
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
`enemy-carrier`, `veteran`, `nearest-ally`,
`weakest-ally`, `healthiest-ally`, `nearest-ally-toward-own-reactor`, a named
role, or the most-experienced ally; the compiler restricts them further for the
skill's argument kind. Use never implies travel or an unseen target search.
For `arc-toss`, the toward-home selector resolves only an empty-handed ally on
an exact legal landing tile, so the authored pass cannot silently become a
blind Core drop.

An Arc Toss to a named role may additionally require forward route progress:

```json
{
  "use": {
    "skill": "arc-toss",
    "target": {
      "role": "relay-b",
      "aheadVia": "long-return-corridor",
      "minimumForwardTiles": 4
    }
  }
}
```

Both route fields are required together, and the minimum is `1..5`. The
receiver must be at a unique point on the ordered route or at a rendezvous one
terrain step beside it; a distant or ambiguous projection is ineligible. The
executor measures remaining authored-route distance for both bodies and Tosses
only when the receiver is at least the stated number of tiles ahead. It also
requires the requested receiver tile to be the exact unobstructed Arc Toss
landing, rather than accepting an engine-clipped landing before a wall.

A descriptor-authorized movement signature may instead aim its own body
forward on one terminal route:

```json
{
  "use": {
    "skill": "vector-dash",
    "target": {
      "route": "safe-return",
      "progress": "forward",
      "minimumForwardTiles": 2
    }
  }
}
```

All three fields are required and `progress` is exactly `forward`. The route
must be terminal and the body must already own one unambiguous held forward
course on it. The executor chooses the furthest legal straight relocation up
to the signature's contract-defined extent. A bend before that extent, an
ambiguous folded projection, dynamic obstruction, an illegal heading, or less
than the authored minimum makes the leaf unavailable; its next sibling runs
that tick. A successful relocation advances the same route cursor
monotonically, so continuation cannot restart behind the body.

Geographic intent stays compositional rather than hidden inside `use`:
`self-in-zone` can guard a skill leaf, while a lower-priority `move` leaf in the
same parent play gets the body into that authored zone. If the skill is not
currently legal, the skill leaf has no target and ordinary priority falls
through to movement.

Descriptor-authorized position signatures may pin exact aim/placement with
`"at": "anchor-id"`. The current band is `survey-flare`, `falling-star`,
`trip-node`, `hardlight-block`, `smoke-canister`, and `sentinel-seed`. For a
construct, the anchor must be an adjacent legal placement tile when the order
runs; otherwise the leaf falls through. `at` does not imply travel. Pair it
with a lower move order targeting `{ "adjacentTo": "anchor-id" }` when the
author wants the body to stage beside that exact point. Both leaves reuse one
semantic anchor; no companion staging anchor or duplicate zone is needed.

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

The current hosted map is Tapline: `arc-relay-tapline-02`, 31×27. Runtime
binding validates bounds and traversable terrain against the engine-generated
map. Route waypoints cannot occupy a reserved spawn or an opponent-only home
tile. A movement zone must provide, in both team bindings, a corner-strict
connected set of legal staging tiles at least as large as the bodies its order
tree can send there; authored capacity is how a smaller zone limits entrants.
Geometry is never projected between maps.

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
  stable `signatureId`/`actionId` ownership, resolved timing/mechanics,
  signature-authoring descriptors, unlocks, starter pair, and stock opponents.
  The workshop consumes that catalog; it does not maintain another
  class-to-signature or target-capability table.
- `GET/POST/PUT/DELETE /api/sheets` persists owner-scoped source pairs and
  optimistic revisions. Every save compiles on the server through the shared
  compiler. The stored source-size ceilings are 192 KiB for Orders and 96 KiB
  for the map plan.
- `POST /api/sheets/{id}/trial` runs the saved draft against a selected frozen
  stock sheet and opens its fully revealed replay as soon as execution finishes.
  Practice has no causal broadcast countdown or paced prefix: the author can pause,
  seek and inspect the whole match immediately.
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

### Tactical Programs are editor ownership, not a gameplay document

The optional `arc-relay-workshop-v1` sidecar lets the web editor link several
ordinary **Instructions** and map objects into one editable **Program**. The
persisted implementation names remain `plays`, `PlayProjection`, `OrderNode`
and `arc-relay-orders-v2` for compatibility; those are not player vocabulary.
The sidecar is stored with the owning sheet so linkage survives refresh,
another device, and duplication. It is deliberately excluded from the
compiler, ATL2, content hash, match identity, and replay. Instructions plus map
plan remain complete standalone gameplay sources.

Programs are the normal map-first authoring surface. The Program list is read
top-to-bottom and is the sole user-facing priority order. Materialization is
deterministic authoring sugar:

1. for each role, filter the ordered Program list to Programs containing that
   role;
2. flatten that participant's generated Instruction block in the same Program
   order; and
3. place advanced unlinked compatibility Instructions after the Program band.

If a participant has no executable Instruction in one Program, ordinary
first-executable-leaf selection falls through to that role's next Program.
Reordering one Program therefore moves every participant block together. There
is no participant-specific insertion point, runtime Program state, atomic team
activation, reservation, or second scheduler.

The editor currently offers one blank Program and five guided lenses: Ambush,
Running Relay, Veteran escort, Safe return, and Lane closure. A lens is only an
authoring convenience. Its generated Instructions use the same five action
names and the same `when`, `until`, `timeout`, `cooldown`, `urgency`, and
`capacity` fields as the advanced Instructions workspace.

Each Program records stable ownership ids and fingerprints for the
Instructions, conditions, capacities, anchors, zones, and routes it generated.
A normal edit regenerates only those owned values deterministically. Generated
Instructions are an advanced read-only explanation; completing ordinary
Program authoring never requires visiting them. If materialized source no
longer matches the recorded fingerprint, save stops and asks the author to
choose:

- **Regenerate Instructions** replaces only the Program-owned blocks.
- **Detach to unlinked Instructions** preserves the materialized Instructions
  and map objects, removes the linkage, and makes that advanced source editable.

Deleting a Program previews its owned sources and can retain geometry. Atomic
multi-object edits use the editor's normal undo/redo transaction. Stale clients
that omit workshop fields preserve existing metadata; an explicit replace flag
is required to clear or change it.

Practice restores the active workspace, selected Program, opponent, and seed.
The map can overlay actual tracks for only that Program's participating roles;
an Ambush-style Program also reports the best observed required-slot readiness
frame. These diagnostics interpret the replay for the author and never feed
back into the match.

### Signature opportunities remain ordinary `use` Instructions

A participant block may link a durable plan branch to one or more **signature
opportunities**. The focused Program view calls this list **Along the way**.
Each binding points at a real direct-child `use` leaf under that branch; it
stores no second copy of the skill, target, conditions, capacity, lifecycle, or
Instruction. The use leaf's actual sibling position is its local priority. The
first following ordinary sibling is its continuation.

If a durable movement/fight leaf needs an opportunity, the editor may scaffold
one parent branch atomically: opportunity `use` leaves first, one optional
commitment hold when the catalog calls for it, then the original continuation.
If a use is unavailable, selection reaches that continuation in the same tick.
If accepted, it consumes that tick's action and the same parent resumes later.
There is no compound move-and-use action.

The map-first editor can link an opportunity to a visible activation zone,
exact anchor, named role/selector, or descriptor-authorized route relation. It
uses the same map objects and the same reusable signature control as direct
Instructions. Removing a binding removes only the linked use leaf; detaching
the Program keeps the exact materialized Instructions editable. Opportunity ids and internal Play ids
remain in the workshop sidecar and never enter ATL2, execution, match state, or
replay.

## 11. Runtime and determinism

ATL2 contains the exact role/class/upgrade assignment, expanded order tree
with source paths, compiled conditions, capacities, and normalized map
geometry. The direct executor evaluates those bytes in both hosts.

Every submitted action and every decline, capacity loss, target miss,
lifecycle exit, wait, and automatic micro decision carries a non-empty reason.
In-process and WASM must produce the same gameplay-state digest for identical
ATL2, map, rules, and seed.

Hosted entrant playlist v24 is the promoted Orders v2/Tapline-6 line. It pins
the coordinated EPIBT executor, body-local signature orders, the unchanged
controlled WASM artifact and Tempered Standard sheet, the combined depth rules,
the delayed centre opening, the equal-return map fork, and a fresh rating book.
Playlist v23 remains immutable Tapline-5 history.

## 12. Executor-owned squad micro

Orders state tactical intent, not squad micro. The executor jointly assigns
distinct contribution tiles and resolves physical motion with deterministic
EPIBT planning. Authored urgency participates only after an order is selected;
it may settle allied right-of-way but cannot reorder the sheet. Acyclic convoy
steps into a pre-tick vacancy are legal on Tapline-6, while swaps and closed
cycles remain blocked. Do not smuggle pathing policy into author-visible orders
or add route-defense fields to compensate for executor behavior.
