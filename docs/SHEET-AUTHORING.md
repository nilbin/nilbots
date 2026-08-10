# Arc Relay sheet authoring

This is the source and validation contract for the current Arc Relay tactical
sheet editor. The compiler is authoritative; a frontend mirrors these rules
for immediate feedback and sends the same two documents to the App for final
validation.

The format is evaluation-grade, not the eventual player-facing UX schema. That
status is explicit in every playbook. The visual editor may use friendlier
labels, cards, map tools, and conditional controls, but it must produce exactly
the grammar below.

## 1. One sheet, two documents

A sheet consists of:

- a playbook with schema `arc-relay-tactical-playbook-v1`; and
- a layout with schema `arc-relay-tactical-layout-v1`.

The playbook says who the eight bodies are and gives every role one ordered
ladder. The layout holds all authored geography. The playbook's `layout.path`
locates the file in a file workflow and `layout.sha256` pins the layout's exact
UTF-8 bytes. Hosted compilation receives the same pair in memory and verifies
the same hash.

The shared compiler is
`BotArena.TacticalPlaybooks.ArcRelayTacticalPlaybookCompiler`. Both the CLI and
the App call it. It validates the authored documents, property-canonicalizes
them without reordering arrays, and links them into one bounded `ATL1` direct
ladder payload. There is no expanded intermediate playbook.

Compile a file-backed sheet with:

```sh
nilbots experiment arc-relay-playbook \
  --playbook path/to/playbook.json \
  --out out/arc-relay-playbook
```

The output file is still named `playbook.atp` for command compatibility, but
its envelope is `ATL1`. `normalized-playbook.json` and
`normalized-layout.json` are canonical versions of the authored documents;
`explain.json` lists the authored rungs as `role[position]/verb` rather than
inventing order ids. The linked payload may not exceed 65,536 bytes.

## 2. Playbook root

The root has exactly these ten keys:

```text
schema
playbookId
auditStatus
composition
layout
memory
roles
custodyPolicies
doctrines
conditions
```

Unknown or missing keys are errors. A minimal shape is:

```json
{
  "schema": "arc-relay-tactical-playbook-v1",
  "playbookId": "example-v1",
  "auditStatus": {
    "provisionalEvaluationOnly": true,
    "playerFacingProductSchema": false
  },
  "composition": [
    "relay", "relay", "switchback", "switchback",
    "towline", "towline", "lantern", "patchbay"
  ],
  "layout": {
    "path": "../layouts/example-v1.json",
    "sha256": "<64 lowercase hex characters>"
  },
  "memory": {
    "enemyUnavailableTicks": 21,
    "lastSeenEnemyTicks": 30,
    "securedCoreTicks": 40,
    "objectiveProgressTicks": 90
  },
  "roles": [],
  "custodyPolicies": [],
  "doctrines": {
    "roles": {},
    "cohorts": {},
    "focus": {}
  },
  "conditions": {}
}
```

Identifiers are 1–64 lowercase ASCII letters, digits, or hyphens.
`playbookId` is an identifier. The four memory windows are required integers
in `1..600`; they tune remembered enemy unavailability, last-seen enemies,
secured Cores, and absence of objective progress.

### Composition

`composition` is exactly eight class ids, with at most two copies of a class.
The launch ids are:

```text
kestrel  palisade  towline  patchbay  lantern  mortar  minesmith  hush
relay    switchback  longshot  mason  sunder  repulsor  veil  nest
```

The hosted App additionally checks that every selected class is unlocked for
the account.

## 3. Roles

`roles` contains `1..16` entries. A role is an assignment, not a class: it
states which chassis may fill a job and how many bodies should fill it.

```json
{
  "roleId": "runner",
  "candidateClasses": ["switchback", "relay"],
  "minimum": 1,
  "preferred": 2,
  "maximum": 2,
  "carrierPreference": "prefer",
  "deathPolicy": "hold-vacancy",
  "respawnPolicy": "rejoin",
  "overflowRoleId": "reserve",
  "build": ["damage", "vitality", "reach", "vision"]
}
```

| field | contract |
| --- | --- |
| `roleId` | unique role identifier |
| `candidateClasses` | `1..16` unique launch class ids; earlier entries are preferred when several bodies qualify |
| `minimum`, `preferred`, `maximum` | integers `0..8`, with `minimum <= preferred <= maximum` |
| `carrierPreference` | `forbid`, `allow`, `prefer`, or `require` |
| `deathPolicy` | `hold-vacancy`, `promote-best`, or `rebalance` |
| `respawnPolicy` | `resume`, `rejoin`, `rally`, or `replace` |
| `overflowRoleId` | empty string or a declared role id |
| `build` | optional ordered `1..8` entries from `damage`, `vision`, `reach`, `vitality`; duplicates are meaningful |

Across all roles, the minimum sum must be at most eight, the preferred sum at
most eight, and the maximum sum at least eight. Every class in the composition
must be eligible for at least one role, and a role's minimum must be satisfiable
by the composition.

Allocation is deterministic. Eligible surviving bodies keep a stable role
unless that role asks to rebalance. The allocator fills role minima, then
preferences, in authored role order; candidate-class order, health, and stable
unit id break selection ties. `hold-vacancy` reserves a dead body's stable
slot, `promote-best` permits an eligible replacement, and `rebalance` permits
live reassignment. Remaining bodies first use their prior role's
`overflowRoleId` when legal, then the least-filled eligible role. `replace`
does not reserve the returning body's old assignment.

`carrierPreference` guides custody selection. A `forbid` role may not contain
a `fetch` rung. A `require` role must contain at least one.

## 4. Doctrines and ordered ladders

`doctrines` has exactly three objects:

```json
"doctrines": {
  "roles": { "runner": { "custody": "farm", "modes": [] } },
  "cohorts": {},
  "focus": {}
}
```

`doctrines.roles` must have exactly one entry keyed by each declared role id.
A role doctrine requires `custody` and `modes`, and may contain `fight`,
`repair`, `conceal`, and `scavenge`:

```json
"runner": {
  "custody": "farm",
  "conceal": false,
  "scavenge": ["north-farm", "midfield"],
  "fight": { "targets": { "prefer": ["carrier", "weakest"] } },
  "modes": [
    { "fetch": "farm" },
    {
      "patrol": "north-well-loop",
      "while": "north-soon",
      "until": "north-far",
      "cohort": "well-runners",
      "posture": "core",
      "focus": "well"
    },
    {
      "patrol": "farm-loop",
      "posture": "core",
      "focus": { "maximumAttackers": 2 }
    }
  ]
}
```

`custody` is this role's default carrying policy. `conceal` defaults to `true`;
`false` makes an open fighter rather than an ambush-seeking one. `scavenge` is
one zone id or `1..16` unique zone ids in which a nearby visible loose Core may
interrupt ordinary movement. `fight` supplies role defaults; a rung's `fight`
overrides it key by key. `repair` makes bodies in this role repair providers.

### 4.1 Ladder selection and latching

`modes` has `1..8` rungs. Position is priority: index 0 is strongest. On every
tick, each body evaluates its own role's ladder and takes its highest active
rung, subject only to cohort admission.

`while` opens a rung. Once opened, the rung stays latched for that exact body
life until its `until` expression becomes true; a later-false `while` does not
close it. A stronger active rung may temporarily hide it without resetting it.
Death creates a new life and clears the latch. A conditioned rung without
`until` therefore remains active for that life and normally shadows weaker
rungs once triggered.

The last rung must be an unconditioned `patrol` floor. Every rung above it
needs `while`, except `recover` and `fetch`, whose availability is implicit.

### 4.2 Six verbs

Every rung has exactly one verb.

| verb | value | required alongside it | optional alongside it |
| --- | --- | --- | --- |
| `patrol` | route destination, or `{ "route": id }` / `{ "zone": id }` | `focus`; `while` above the floor | `until`, `fight`, `posture`, `cohort` |
| `intercept` | `enemy-carriers` or `inbound` | anchor `from`, `focus`, `while` | `until`, `fight` |
| `assault` | route/zone destination | `focus`, `while` | `until`, `fight`, `posture`, `cohort` |
| `recover` | exactly `auto` | `focus` | `fight` |
| `fetch` | custody-policy id | nothing else | nothing else |
| `follow` | another role id | `posture` (`trail` or `screen`), `focus`, `while` | `until`, `fight` |

A plain patrol/assault string names a route. The object form names exactly one
route or zone. A route literally named `traffic` selects the executor's enemy-
traffic shadowing movement and must still exist in the layout.

`recover` is active only while that body needs and can use recovery. One
activation is a 24-tick attempt at one latched beacon. Healing to full ends it;
an unsafe beacon, a timeout, or actual movement away from that beacon ends the
attempt and starts a 24-tick cooldown for that same injury. A fresh health loss
is a new trigger and bypasses the remaining cooldown. These are fixed executor
semantics, not authored timing fields: ladder position still decides whether
recovery outranks the body's other eligible modes.

`fetch` is a movement-only overlay: when an eligible loose Core exists, a fetch rung may
pre-empt the selected ordinary rung only if it appears earlier in the list.
The ordinary rung's fight and facing behavior remain in force during the fetch.

`follow` belongs entirely to the follower. It names a role, never a particular
unit and never a caller-owned operation. The body follows the nearest live body
of that role by Chebyshev distance; stable unit id breaks a tie. The rung becomes
ineligible if no such leader exists. `trail` follows behind the leader's current
motion/facing. `screen` places the follower between the leader and its nearest
known threat, falling back to trail when no usable threat line exists.

Patrol/assault `posture` is a compact per-rung spacing cue:

- `front`: two canonical tiles ahead of the destination spine;
- `core`: on the spine; and
- `rear`: two canonical tiles behind it.

Peers sharing a posture and destination spread alternately across the spine by
stable unit id. The offset applies on the route's final approach, not while
marching between earlier waypoints.

### 4.3 Cohorts: one shared count

A cohort coordinates adaptive participation without owning behavior:

```json
"cohorts": {
  "well-runners": { "take": 3, "select": "nearest" }
}
```

A shared cohort must be referenced by at least two rungs on different roles;
one role may reference it at most once. Every consumer must be a non-floor
patrol/assault rung, have a posture, and name the same route or zone. Membership
is derived—there is no role list.

On each tick, bodies whose current highest eligible rung names the cohort form
the pool. They are ordered by Chebyshev distance to the side-bound destination
(nearest route waypoint or nearest tile of a zone), then stable unit id. The
first `take` bodies run their own rungs; rejected bodies continue down their own
ladders. Membership is recalculated each tick and has no lease, timeout, state,
or hidden cooldown.

A one-rung cohort is inline instead of named:

```json
"cohort": { "take": 2, "select": "nearest" }
```

Both shared and inline `take` are `1..8`; `select` is exactly `nearest`.

### 4.4 Focus: one shared attacker cap

A focus scope shares only a maximum-attacker count:

```json
"focus": {
  "well": { "maximumAttackers": 3 }
}
```

A shared focus name needs at least two rung consumers. A one-rung cap is
inline: `{ "maximumAttackers": 1 }`. The value is `1..8`. Every non-fetch
rung must choose a shared or inline focus.

The cap is applied per enemy target across its consumers. It does not share a
target lock, target preferences, fight policy, order, or lifecycle. Bodies with
the same global preference list naturally converge on facts such as `carrier`
or `weakest`; focus only limits how many may be allocated.

## 5. Fight and repair

Every fight field is optional. A rung-level block overrides the doctrine block
one leaf at a time.

| field | legal value |
| --- | --- |
| `collect` | `yield` or `first` |
| `heal` | `yield` or `first` |
| `targets.lone` | `0..8` |
| `targets.prefer` | `1..6` unique values from `carrier`, `weakest`, `anchor`, `closest`, `strongest-threat`, `freshest` |
| `engage.within` | `0..12` |
| `engage.killableTicks` | `0..60` |
| `engage.from` | exactly `behind` |
| `engage.positionTicks` | `1..64` |
| `engage.else` | `strike` or `breakOff` |
| `chase.leash` | `0..16` |
| `chase.onlyCatchable` | boolean |
| `chase.executeBelowHealth` | `0..8` |
| `chase.persistTicks` | `1..120` |
| `breakOff.threats` | `0..8` |
| `breakOff.within` | `2..16` |
| `breakOff.memoryTicks` | `1..120` |
| `breakOff.recoverTicks` | `4..120` |
| `breakOff.health` | `0..8` |
| `defense.radius` | `0..16` |
| `defense.return` | boolean |

Positive `breakOff.threats` requires both `within` and `memoryTicks`.
Executor defaults are: target `carrier, weakest`; collect/heal `yield`; no
isolation, hold-fire, commit, chase, or break-off gate; engage fallback
`strike`; position budget 16; defense radius 2 with return enabled; break-off
recovery 24 ticks.

A repair provider declares exactly:

```json
"repair": {
  "prefer": ["carrier", "weakest", "any"],
  "maximumProvidersPerTarget": 1,
  "minimumProviderSeparation": 2,
  "reserveHealthPercent": 30,
  "survivalFallback": "evade"
}
```

`prefer` is `1..6` unique values from `weakest`, `carrier`, `medic`, `focus`,
`anchor`, `any`; the two numeric provider fields are `1..8` and `0..8`,
reserve health is `0..100`, and fallback is `evade`, `regroup`, `hold`, or
`self-defense`.

## 6. Conditions

`conditions` maps a name to one typed comparison:

```json
"north-soon": {
  "fact": "well-ticks-until-birth",
  "subject": "north",
  "operator": "at-most",
  "value": 20
}
```

Required fields are `fact`, `operator`, and a 32-bit integer `value`.
Operators are `at-least`, `at-most`, `equals`, `less-than`, and
`greater-than`. `subject`, `zone`, and `freshnessTicks` are legal only where
the fact table requires them.

A condition expression is a string:

```text
north-soon and enemy-thin or core-loose
```

`and` binds tighter than `or`. There are no parentheses or negation. `always`
and `no-live-friendlies` are built-in expression names and may not be declared.
Every declared condition must be referenced at least once.

| fact family | facts and required qualifiers |
| --- | --- |
| body-local | `self-health`, `self-level`, `self-carrying`, `self-stuck-ticks`, `self-mode-ticks`; no subject/zone |
| role | `live-count`, `joining-count`, `cohesion`, `stuck-ticks`, `role-live-count`, `role-health`; required role `subject` |
| role + zone | `in-zone-count`; required role `subject` and `zone` |
| optional role | `max-level`, `recover-ready-bodies`; optional role `subject` |
| zone | `friendlies-in-zone-count`, `visible-enemies-in-zone`, `remembered-enemies-in-zone`, `visible-loose-cores-in-zone`, `visible-loose-core-value-in-zone`; required `zone` |
| Well | `well-has-outstanding`, `own-socket-filled`, `enemy-socket-filled`, `well-ticks-until-birth`; `subject` is `north`, `centre`, or `south` |
| team/global | `always`, `tick`, `phase-state-ticks`, `live-friendlies`, `known-enemies-unavailable`, `visible-enemy-carriers`, `known-enemy-carriers`, `friendly-carriers`, `secured-cores`, `visible-loose-cores`, `visible-loose-core-value`, `outstanding-well-count`, `ticks-without-objective-progress`, `reactor-integrity`, `reactor-charge`, `custody-state-ticks`, `own-filled-sockets`, `enemy-filled-sockets` |

Only `remembered-enemies-in-zone` and `secured-cores` accept
`freshnessTicks` (`1..600`).

Self facts refer to the exact body/life evaluating a rung and are legal only in
mode `while`/`until`. `self-mode-ticks` is time since that rung latched. Team,
custody, and bait conditions may not use self facts. `role-health` means the
lowest health among live bodies in that role and reads as 9999 when none live,
so a dead role does not look injured.

## 7. Custody policies

`custodyPolicies` contains `1..16` policies. Required fields are:

```json
{
  "custodyId": "farm",
  "sourceWells": ["north", "centre", "south"],
  "pickupReservationTicks": 8,
  "transferTimeoutTicks": 8,
  "deliveryTimeoutTicks": 120,
  "accidentalPickup": "deliver",
  "dropRecovery": "nearest-authorized",
  "unreachableFallback": "guard",
  "safeConversion": "always"
}
```

| field | contract |
| --- | --- |
| `sourceWells` | `1..3` unique values from `north`, `centre`, `south` |
| `pickupReservationTicks` | `1..120` |
| `transferTimeoutTicks` | `1..120` |
| `deliveryTimeoutTicks` | `1..1200` |
| `accidentalPickup` | `transfer`, `deliver`, or `drop-safe` |
| `dropRecovery` | `same-carrier`, `nearest-authorized`, or `guard-until-safe` |
| `unreachableFallback` | `hold`, `guard`, `alternate-core`, or `regroup` |
| `safeConversion` | condition expression without self facts |

There is no carrier roster in a custody policy. Roles declare eligibility and
priority by placing `{ "fetch": "farm" }` in their own ladders. Each policy
must have at least one fetch consumer; a role may reference a policy only once.
The policy that reserved a Core remains attached throughout pickup, carrying,
handoff, delivery, and recovery.

Optional surface:

- `deliveryRoutes`: `1..8` `{ "zone", "route" }` rules. The Core's pickup
  zone chooses the side-bound delivery route.
- `forwardPass`: `none` or `relay-catcher`.
- `baitDrop`: `{ "zone", "reclaim" }`, with a non-self reclaim expression.
- emergency recovery: an all-or-nothing six-field bundle of
  `emergencyRecoveryZones` (`1..8` zones), `emergencyPickupRadius` (`1..4`),
  `emergencyRecovery` (non-self expression), `emergencyRecoveryRoles`
  (`1..8` roles), `emergencyRecoverySourceWells` (`1..3` Wells), and
  `emergencyRecoveryDisposition` (`deliver` or `displace`). `displace` also
  requires `emergencyDisplacementTarget` (anchor) and
  `emergencyDisplacementReleaseRadius` (`0..4`); `deliver` forbids them.

## 8. Layout contract

The layout has exactly:

```text
schema  layoutId  mapId  bindings  zones  routes  anchors
```

Current hosted v11 accepts the native Warren map only:
`arc-relay-ambush-warren-06` (31×27). Coordinates are absolute canonical map
tiles. The same authored geometry serves both teams through side bindings.

```json
{
  "schema": "arc-relay-tactical-layout-v1",
  "layoutId": "example-warren6-v1",
  "mapId": "arc-relay-ambush-warren-06",
  "bindings": [
    {
      "matchContractFingerprint": "any-composition",
      "ownReactorSide": "west",
      "transform": "identity",
      "routeAliases": {}
    },
    {
      "matchContractFingerprint": "any-composition",
      "ownReactorSide": "east",
      "transform": "rotate-180",
      "routeAliases": {}
    }
  ],
  "zones": [{ "zoneId": "midfield", "rect": [10, 8, 20, 18] }],
  "routes": [{
    "routeId": "farm-loop",
    "corridorWidth": 2,
    "waypoints": [[4, 13], [15, 13], [4, 13]]
  }],
  "anchors": [{ "anchorId": "home", "position": [3, 13] }]
}
```

Validation:

- `bindings`: `2..16`, unique by fingerprint + side, and at least one west
  and one east. A fingerprint is `any-composition` or a lowercase SHA-256.
  Transform is `identity`, `mirror-x`, or `rotate-180`.
  `routeAliases` has at most 32 string entries and both sides of every alias
  must name declared routes. The obsolete optional `formationAliases` is
  accepted only as an empty object; new editors omit it.
- `zones`: `1..64`; `rect` is inclusive `[x0,y0,x1,y1]`, ordered, with each
  coordinate in `0..255`.
- `routes`: `1..64`; `1..128` waypoints, each `[x,y]` in `0..255`, and
  `corridorWidth` in `0..8`.
- `anchors`: `1..64`; each position is `[x,y]` in `0..255`.

At runtime an exact match-contract binding wins; `any-composition` is the
fallback for that reactor side. Geometry is never projected between maps.
The editor draws the Warren from `/api/sheets/catalog`; replay headers expose
the same `header.contract.map` (`width`, `height`, tile rows, regions, spawn
anchors, and tile tags). Routes, zones, anchors, pickup geography, and
delivery geography are plotted and dragged on that map, never entered as raw
coordinate text in the primary UI.

## 9. Frontend and hosted integration

The product loop is edit → practice → watch → edit.

- `GET /api/sheets/catalog` supplies the current map export, class catalog,
  unlocks, starter pair, and stock opponents.
- `GET/POST/PUT/DELETE /api/sheets` is owner-scoped persistence. The App stores
  exact playbook/layout JSON, validates every save through the shared compiler,
  and uses optimistic revisions. Server limits are 192 KiB playbook source and
  96 KiB layout source, in addition to the 64 KiB linked limit.
- `POST /api/sheets/{id}/trial` runs the saved draft against a stock opponent
  and returns a replay-viewer target. Compile errors retain JSON paths and must
  be attached to the corresponding visual control.
- Creating or updating a sheet fields its entrant in the current ladder by
  default, subject to the account cap; opting out is explicit.

The browser autosaves the working draft locally before navigation or trial
admission, so a failed compile, run, or phone-tab eviction cannot lose edits.
Raw JSON import/export and SHA display are operator utilities, not primary
player controls. The editor manipulates composition, assignments, ladders,
conditions, custody, focus/cohorts, and map geometry through guided controls;
the emitted documents remain the portable truth.

## 10. Editor migration note

The current compiler accepts only the ten-key direct grammar in this document.
An editor based on the previous contract must remove controls and serializers
for `perspective`, `groups`, `formations`, `engagements`, `coordination`,
`authoring`, `supportPolicies`, `tempo`, `operations`, `staff`, `escort`,
`muster`, and `squad`. Conditions are the top-level `conditions` object;
coordination is role-local ladders plus the narrow `cohorts` count and `focus`
cap; following is the follower's `follow` rung; fetching is the courier's
`fetch` rung.

`home-siege-v2.json` is retained only as byte-frozen historical evidence. Its
source is intentionally not valid input to the current compiler or editor.
