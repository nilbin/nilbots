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
  "sheetId": "your-sheet",
  "mapPlan": "../map-plans/tapline-9/your-sheet.json",
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
  "mapPlanId": "your-sheet",
  "mapId": "arc-relay-tapline-03",
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

Branches and leaves may use `when`, `until`, `timeout`, `cooldown`, `rejoin`,
and `urgency`. Only a leaf may use `capacity`. A leaf has exactly one of `move`,
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
| `rejoin` | `true` squad-latches the gate: while any squadmate holds a node with this id active, a body enters without its `when`; requires `when`. Legacy — the v3 program phase makes it unnecessary |
| `phase` | v3 only: gates the subtree to a program tag (`"t1"` or `"t1/p2"`); dark subtrees drop per-body state without cooldown |
| `urgency` | allied movement weight: `routine`, `normal`, or `urgent` |

- Without `until`, `when` remains the node's ongoing eligibility.
- With `until`, `when` gates entry and `until` owns exit.
- `rejoin` matches by order id across roles and bypasses only the gate, never
  the body's own cooldown; the latch empties when every holder exits or dies.
  It remains for hand-authored v2 sheets; generated v3 sheets need none —
  the squad program's phase is squad state, so a respawn simply executes
  whatever phase is lit.
- On a v3 sheet the program table (SPEC-SHEET-ORDERS-V2.md §4b) owns tactic
  selection, latched forward-only phase transitions, aborts, and duration;
  `phase`-tagged wrappers light and go dark accordingly, and a dark subtree's
  per-body state evaporates without a cooldown.
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
| body | `self-health-percent`, `self-level`, `self-carrying`, `self-carried-core-bank-value`, `self-carrying-core-from-well`, `self-stuck-ticks`, `self-order-ticks`, `self-in-zone`, `self-nearest-visible-enemy-distance`, `self-nearest-visible-loose-core-distance`, `self-nearest-heal-zone-distance`, `self-ticks-since-signature-use`, `signature-ready`, `signature-commitment-active`, `deployment-active`, `self-deployment-in-zone`, `deployment-lifetime-percent`, `deployment-durability-percent` |
| role | `role-live-count`, `role-lowest-health-percent`, `role-highest-level`, `role-in-zone-count`, `role-deployments-in-zone-count` |
| team | `live-friendlies`, `live-enemies`, `friendly-carriers`, `friendly-carriers-in-roles`, `known-enemy-carriers`, `visible-enemy-carriers`, `known-loose-cores`, `visible-loose-cores`, `secured-cores`, `reactor-integrity`, `reactor-charge`, `reactor-pulses`, `enemy-reactor-charge`, `friendly-respawns-next-wave`, `enemy-respawns-next-wave`, `ticks-until-respawn-wave`, `ticks-without-objective-progress` |
| zone | `friendlies-in-zone`, `visible-enemies-in-zone`, `remembered-enemies-in-zone`, `zone-cleared`, `friendly-fights-in-zone`, `known-loose-cores-in-zone`, `observed-enemy-entries-in-zone`, `distinct-enemy-lives-observed-in-zone`, `confirmed-enemy-destructions-in-zone`, `zone-fully-observed-throughout-interval` |
| history | `friendly-core-pickups`, `friendly-core-acquisitions`, `friendly-core-banks`, `friendly-core-drops`, `friendly-core-losses`, `friendly-core-resolutions` |
| Well | `well-has-outstanding-core`, `well-ticks-until-birth`, `well-socket-filled` |

Role facts require `role`; zone facts require `zone`; Well facts require
`well` (`north`, `centre`, `south`, `own`, or `enemy`).
`well-ticks-until-birth` reads the one effective public Well clock: the
initial opening or a recharge already started by banking that Well's Core.
While the Core is still active—loose, carried, in flight, dropped, or
stolen—there is no countdown, so finite `at-most` birth windows remain false.
`self-deployment-in-zone` and `role-deployments-in-zone-count` require a
current `signature` id plus `zone`; the latter also requires `role`.
`self-carrying-core-from-well` requires `well` and says whether this body's
current carried Core came from that team-relative Well. `friendly-carriers-in-roles`
requires one or more declared `roles` and may additionally filter by `well` and
`coreBankValue`; it counts only current friendly carriers in that authored role
scope. `self-nearest-visible-loose-core-distance` may take a `coreBankValue` filter,
and all three bounded service-distance facts are legal only as walking-step
upper bounds. Core-history facts take the same explicit historical interval as
the four zone-history facts, but no zone. They may filter by declared `roles`,
`well`, and `coreBankValue`. `friendly-core-acquisitions` includes pickup,
catch, and Handoff receipt. `friendly-core-resolutions` also requires one or
more `outcomes` from `bank`, `drop`, and `loss`; with
`correlateAcquisition: true`, only the same authoritative Core identity acquired
inside the interval may satisfy the outcome. `known-enemy-carriers` and `remembered-enemies-in-zone` require
`freshnessTicks` in `1..600`. Qualifiers on other facts are errors. Health
percent comparisons are `0..100`; boolean facts compare with `0` or `1`.

The historical facts are finite, read-only team evidence. Each needs
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

Visible-Core and healing distances are deliberately body-local walking
distances. `self-nearest-visible-loose-core-distance` considers only currently
visible qualifying Cores; `self-nearest-heal-zone-distance` considers the
rules-owned heal tiles. Their matching move targets accept
`withinWalkingSteps: 0..31`; a body outside that bound falls through instead
of beginning an unbounded detour. A visible-Core target may also use one
`coreBankValue` comparison (`equals`, `at-least`, or `at-most`) and either
nearest-first or highest-value-then-nearest ranking. The Condition and move
use the same filter when the workshop derives a recovery Opportunity.

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
It means only **No enemies currently visible in this area** and is never
presented as clearance. `zone-cleared` is the epistemic form: every walkable
tile in the zone was observed through real team vision during the rolling last
20 ticks and no unresolved live-enemy observation remains there. An enemy is
resolved by destruction, later observation outside, or observing its last tile
empty; stale coverage and completely unseen ground both fail. Scan uses this
same evidence. `friendly-fights-in-zone` reads selected friendly Fight
Instructions whose fighter or target intersects the zone, plus pending strikes
aimed into it. It intentionally trails selection by one normal decision
boundary rather than creating parallel combat state.

`self-ticks-since-signature-use` is the age of this body's last successfully
accepted signature action, capped above the authored `0..1200` comparison
range. Before the body has used its signature it fails every upper-bound
comparison. This is a local transition latch; it does not create shared Play
state or imply another body's behavior.

`signature-commitment-active` is `1` only while this body's own authoritative
signature operation is in its public tell/acquisition or channel phase. It is
`0` for independent in-flight effects and persistent deployments after the
caster is free. This observes engine state; it is not an authored duration.

## 6. Six actions

### `move`

`move` has exactly one motion kind:

```json
{ "move": { "to": "nearest-known-loose-core", "via": "north-ingress", "posture": "front" } }
{ "move": { "patrol": "north-loop", "posture": "line" } }
{ "move": { "follow": "runner", "posture": "rear" } }
{ "move": { "to": { "zone": "killbox" }, "face": { "anchor": "lane-mouth" } } }
```

`to` accepts:

- `own-reactor` or `nearest-heal-zone`;
- `nearest-known-loose-core` or `highest-bank-value-known-loose-core` for the
  durable team ledger;
- `nearest-visible-loose-core` or
  `highest-bank-value-visible-loose-core` for a bounded immediate detour;
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
named role by terrain path distance, then unit id; the most-experienced live
ally by level, distance, then unit id; or the nearest friendly Core carrier,
optionally filtered by declared carrier roles, carried bank value, and source
Well. A named role may likewise select its highest veteran deterministically by
level, distance, then unit id. Follow stands on a legal adjacent tile rather
than asking for the leader's occupied tile. `via` is an optional
one-time ingress route for `to` or `patrol`; follow cannot use it. Combat pre-emption
does not restart ingress. Exit, death, or an impossible route does.

Optional posture is `front`, `line`, or `rear`. It is individual pre-combat
positioning relative to the destination, not a formation or combat behavior.

`withinWalkingSteps` is legal only on `nearest-heal-zone` and the two visible
Core targets. It measures the actual corner-strict terrain path, not bird's-eye
distance; visible bodies remain transient traffic. `coreBankValue` is legal
only on visible-Core moves and nearest-carrier Follow; the latter may also take
`roles` and `well`. These are candidate filters, not hidden movement or pickup
rules.

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

Tactics `hold` posture materializes a carrier-first Hold fight with a bounded
five-tile defence leash. The fight may begin only from the body's authored
post/area. Its activation position then remains the fixed leash anchor while
the holder closes on an escaping visible Core carrier or on a visible attacker
that can shoot it from beyond its own range. It does not chase ordinary
retreating enemies. Once the threat is no longer actionable—or the holder
itself acquires a Core—the episode ends and the ordinary ground Instruction
returns the body to its authored position and resting facing.

There is no hidden engage or disengage condition. Lifecycle fields own entry
and exit. An unresolved target falls through to the next order. Fighting while
carrying never implicitly drops the Core.

### `scan`

```json
{ "scan": { "zone": "mid-clearance", "freshWithinTicks": 20 } }
```

Scan is an advanced Orders/Instruction executor primitive, not a player-facing
Tactics job or a new engine action. It resolves to existing rotate, move and
hold commands. The body first rotates in place when that can refresh useful
stale ground; otherwise it walks to a deterministic legal viewing position
computed from the real map, walls, facing and class vision profile. A useful
view remains sticky until satisfied, invalid, unreachable or pre-empted.
Generated phase scans share one zone capacity: at most two scanners and one
body per viewing tile. They stop yielding proposals once `zone-cleared` is
true. Ordinary tree priority remains authoritative: custody, recovery, Fight
and committed signatures precede generated Scan, and passive ground follows.

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
`nearest-empty-handed-ally`, `{ "role": "id" }`, or the Program-generated
role-scoped selector:

```json
{
  "selector": "nearest-ally-toward-own-reactor",
  "roles": ["couriers", "relay"]
}
```

The scoped form is ordinary Instruction data. Program authoring uses it for
**Pass Forward**, deriving `roles` from the carrier behaviors in that exact
Core-handling case; another body in the same multi-body role is eligible.
Named Program-case receivers must likewise have carrier behavior in that case,
so the receiver has an authored continuation after transfer. Advanced
unscoped Handoff remains available outside Program custody.

Resolution uses terrain path distance, then unit id. The receiver must be
alive, empty-handed, adjacent, and eligible to reclaim that Core. Handoff moves
neither body and does not require a matching receiver order. When no eligible
receiver is adjacent, the leaf proposes nothing and ordinary sibling
Instructions continue in the same tick.

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

The current hosted map is Tapline: `arc-relay-tapline-03`, 31×27. Runtime
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

## 10. Tactics and the hosted loop

The product loop is **change → practice → watch → change**.

The player-owned document is `arc-relay-tactics-v2`
([SPEC-SHEET-TACTICS-V2.md](SPEC-SHEET-TACTICS-V2.md)): a squad of eight
named bots plus an ordered list of whole-team **Tactics**. Each Tactic is one
committed operation: Groups are membership (one to three, every bot in
exactly one), and ordered named **phases** say what every Group does — posts
with facing, an area, a picket line, a quiet approach, a convergence, a
push, a patrol, the run home, or carry-on (keep the previous ground under a
new posture) — each geometry optionally reached by a drawn `via` route. Every
phase carries a latched, forward-only "Continue when…" of bounded cases
(Required rows all hold AND any Triggered-by row fires; alternatives OR),
targeting the next phase, a later named phase, or finish. The Tactic owns
tactic-wide start and abort cases and an optional minor time limit, one
delivery plan with a dynamic carrier, per-group Core rules, recovery presets,
and spatial signature marks where the class descriptor admits them. Evidence
outcomes count from their phase's entry. Tactics read top to bottom; a
finished or aborted tactic re-arms and the list is reevaluated — a running
one is never shadowed. Saved v1 documents migrate on load (Stage → phase 1,
Release → its continue rule, Act → phase 2, Finish → the finish transition).

The server is the only materializer: every save migrates a v1 document,
validates, deterministically materializes the Orders v3 + map-plan pair,
compiles it through the shared compiler, and stores all three. The client
never generates Orders, generated output is never editable, and there is no
drift apparatus — the Programs workshop sidecar (`arc-relay-workshop-v1`) is
retired (`DECISIONS.md` #331 supersedes the editor-ownership model of
#325–#330; sections 1–9 of this document remain normative for the
materialization TARGET, with §4b of the Orders spec carrying the v3 program).

- `GET /api/sheets/catalog` supplies the Tapline map, class/signature catalog
  with authoring descriptors, unlocks, tactics-native starters, and stock
  opponents. The editor derives signature mark geometry from descriptor
  capabilities; it maintains no class-id table.
- `GET/POST/PUT/DELETE /api/sheets` persists owner-scoped Tactics documents
  with optimistic revisions: `{name, expectedRevision, tacticsJson,
  enterLadder?}` in, the document plus the materialized pair out. The raw
  tactics ceiling is 192 KiB.
- `POST /api/sheets/{id}/trial` compiles the saved Tactics source with the
  currently running compiler, runs it against a frozen stock opponent, and
  opens the fully revealed replay immediately. The match snapshots those exact
  generated Instructions and map plan for attribution and reproduction. This
  keeps Practice truthful after a local compiler restart even when the saved
  revision's older generated siblings have not been rewritten; ranked and
  scrimmage admission continue using their persisted immutable materialization.
- Saving fields a sheet in the current ladder by default, subject to the
  account cap; opting out is deliberate.

The editor is map-first: rail (the Sheet), map (the editor), drawer (the
questions). Creating a Tactic opens one guided wizard —
**Start → Groups → Phases → Cores → Review** — whose answers are the
document; the Phases step is a selectable storyboard (up to eight named
phases) whose selected phase the map draws full-strength, and reopening a
Tactic reopens the same answers. Recipes (Hold Area, Centre Rush, the
four-phase Pincer, the three-phase Core Raid, Patrol, Deliver Core, Blank)
prefill the same structure and are provenance only. Every Tactic card reads
its whole operation back phase by phase in plain language, and every drawn
footprint stays draggable on the map. The browser keeps a local draft, so
compile failure and phone-tab eviction do not erase work; server diagnostics
attach back to document fields.

The map defaults to the selected phase at full strength, its immediate previous
and next phases dimmed, and all more distant phases hidden. **Show every phase**
reveals the remaining phases more faintly and is independent from **Show every
Tactic**. Only the selected phase is editable. A phase-transition condition
labelled **Area is cleared of enemies** materializes local Scan Instructions for
groups whose phase ground overlaps or touches that area; start and abort
clearance remains observation-only. The paired **No one is currently fighting
in this area** row defaults to the same zone.

Practice attribution needs no sidecar: the materializer mints ordinal ids
(`vex > t2 > t2-p1 > t2-p1-g1-move`), the coordinator prefixes every
decision with the active pair (`orders:t2/p1 …`, `orders:idle …`) and
appends `!go-p2[c1]` / `!abort-t2[c1]` / `!finish-t2[c1]` / `!begin-t2`
markers on its event ticks — so executor `debugMessage` paths map straight
back to Tactic, Group and NAMED phase, transitions included.

## 11. Runtime and determinism

ATL2 contains the exact role/class/upgrade assignment, expanded order tree
with source paths, compiled conditions, capacities, and normalized map
geometry. The direct executor evaluates those bytes in both hosts.

Every submitted action and every decline, capacity loss, target miss,
lifecycle exit, wait, and automatic micro decision carries a non-empty reason.
For owner-only canonical Practice, a compact `eligibility[...]` clause names
inactive higher root Instructions and the exact false Condition values. The
web correlates those source ids through the immutable workshop snapshot and
removes the clause from the player-facing action label. Compact public
broadcasts carry neither the clause nor private Program attribution.
In-process and WASM must produce the same gameplay-state digest for identical
ATL2, map, rules, and seed.

Hosted entrant playlist v26 is the promoted Orders v2/Tapline-9 line. It pins
the coordinated EPIBT executor, body-local signature orders, the unchanged
controlled WASM artifact and Tempered Standard sheet, the combined depth rules,
the delayed centre opening, the equal-return map fork, the owner-approved
sealed outer-Well entrances with staggered Mid cover, bank-triggered
source-local Well supply, and a fresh rating
book. Playlists v23 (Tapline-5), v24 (Tapline-6), and v25 (Tapline-8) remain
immutable history.

## 12. Executor-owned squad micro

Orders state tactical intent, not squad micro. The executor jointly assigns
distinct contribution tiles and resolves physical motion with deterministic
EPIBT planning. Authored urgency participates only after an order is selected;
it may settle allied right-of-way but cannot reorder the sheet. Acyclic convoy
steps into a pre-tick vacancy are legal on Tapline-6, while swaps and closed
cycles remain blocked. Do not smuggle pathing policy into author-visible orders
or add route-defense fields to compensate for executor behavior.
