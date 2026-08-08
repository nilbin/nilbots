# Sheet authoring: the contract for a sheet editor

This document describes the **current** Arc Relay tactical-sheet surface, as
of the commit that introduced it. It is written for a developer who has never
opened this repository, and it is the specification a sheet-editor frontend
should be built against.

**Scope rule, read this first.** Only the vocabulary described here is to be
supported. This repository contains older surfaces — hand-authored
`engagements` / `formations` / `maneuvers` / `orders` (collectively "the squad
plane"), and unrelated Frontline and legacy arc-relay sheet formats. **An
editor must not offer any of them.** They remain in the packed schema because
frozen sheets still compile through it; consult the packed schema only where
this document explicitly says to. Everything a new sheet needs is the v3
doctrine grammar below.

---

## 1. What a sheet is

A sheet is **two JSON documents**. The *playbook* (`playbooks/*.json`,
`"schema": "arc-relay-tactical-playbook-v1"`) says what the team does: its
composition, its roles, and — in the modern grammar — one `doctrines` block
per role describing how that role behaves. The *layout*
(`layouts/*.json`, `"schema": "arc-relay-tactical-layout-v1"`) says *where*,
as map-plotted geometry: named zones, routes and anchors in map coordinates,
plus one `bindings` entry per orientation so the same authored geometry serves
both sides of a rotationally symmetric map (a `transform` such as
`rotate-180`, and `routeAliases` / `formationAliases` that swap a route for
its mirror). The playbook references its layout by relative path **and pinned
`sha256`**, so editing a layout without updating the pin is a hard error. The
CLI compiler (`nilbots experiment arc-relay-playbook --playbook <file>`)
validates both, desugars the doctrine grammar into the packed executor schema,
and emits `normalized-playbook.json`, `normalized-layout.json`, `explain.json`
and a binary `playbook.atp`; **compilation is the only source of truth for
validity**, and the editor's job is to never produce something it rejects.

---

## 2. The current vocabulary

### 2.1 Playbook root

Required keys: `schema`, `playbookId`, `auditStatus`, `composition`, `layout`,
`perspective`, `memory`, `arbitration`, `roles`, `groups`, `formations`,
`engagements`, `supportPolicies`, `custodyPolicies`, `coordination`, `authoring`
and `doctrines`.

`perspective` must be exactly `"team-relative"`. `composition` is the ordered
list of class ids the team fields. `layout` is `{ "path", "sha256" }`.

For a new sheet the editor should emit `formations`, `engagements` and
`coordination.tasks` as **empty or minimal** and put all behaviour in
`doctrines`. A doctrine source must not contain root `orders`: the compiler's
strict source contract accepts exactly one of root `orders` (the frozen packed
format) or `authoring` (the current source format), then generates the packed
orders during desugaring. `doctrines` and the `authoring` block are source-only
keys and do not appear in `normalized-playbook.json`.

### 2.2 Doctrines

One entry per role, keyed by a free identifier:

```json
"doctrines": {
  "ghost": {
    "role": "hunter",
    "custody": "well-custody",
    "conceal": false,
    "collect": ["north-farm", "midfield", "enemy-backfield"],
    "fight": { ... },
    "modes": [ ... ]
  }
}
```

- **`role`** (required) — a role id; must belong to exactly one `group`.
- **`custody`** (required) — a `custodyPolicies` id.
- **`conceal`** (optional, default `true`) — concealment micro: slip out of
  seen facing cones, skip the flank walk, long ambush patience. `false` makes
  an open fighter.
- **`collect`** (optional) — one zone id or a list of up to 8. Loose Cores
  seen in those zones are worth breaking a mode for.
- **`fight`** (optional) — the doctrine's combat character, below.
- **`modes`** (required) — 1..8 entries, **highest priority first**, below.

### 2.3 The fight block

Valid at doctrine level and, identically, as a per-mode override. Every key is
optional; a mode's value wins over the doctrine's, key by key.

| key | values |
| --- | --- |
| `collect` | `"yield"` \| `"first"` — does a loose Core outrank a fight |
| `heal` | `"yield"` \| `"first"` — does an armed recover outrank a fight |
| `targets.lone` | 0..8 — only engage targets with no ally within N |
| `targets.prefer` | 1..5 of `carrier`, `weakest`, `closest`, `strongest-threat`, `freshest` |
| `engage.within` | 0..12 |
| `engage.killableTicks` | 0..60 |
| `engage.from` | `"behind"` |
| `engage.positionTicks` | 1..64 |
| `engage.else` | `"strike"` \| `"breakOff"` |
| `chase.leash` | 0..16 |
| `chase.persistTicks` | 1..120 |
| `chase.executeBelowHealth` | 0..8 |
| `breakOff.threats` | 0..8 |
| `breakOff.health` | 0..8 |
| `breakOff.within` | 2..16 |
| `breakOff.memoryTicks` | 1..120 |
| `breakOff.recoverTicks` | 4..120 |
| `defense.radius` | 0..16 |
| `defense.return` | boolean |

Either `breakOff` trigger latches a timed break-off, and a latched body
rallies to the **highest currently-active patrol-verb mode** in its own list,
falling down the list to the floor. That is why a doctrine with any `breakOff`
trigger must have a real route floor (see the floor rule).

### 2.4 Modes

An ordered list; **position is priority**, first is strongest, last is the
floor. Each mode carries **exactly one verb**:

| verb | value | meaning |
| --- | --- | --- |
| `patrol` | route id, or `"traffic"` | walk a route (or shadow enemy traffic) |
| `intercept` | `"enemy-carriers"` \| `"inbound"` | cut off couriers |
| `assault` | route id | press an attack along a route |
| `recover` | `"auto"` | go heal; condition-driven internally |
| `muster` | `"escort"` or an order id | answer a leader's escort call |
| `squad` | `true` | do the ordinary squad-plane job; emits nothing |

Per-verb optional keys:

- `patrol`: `while`, `until`, `fight`
- `intercept`: `from` (anchor id), `while`, `until`, `fight`, `patienceTicks`
  (2..120, **accepted but inert** — the no-idle watchdog it bounded is gone)
- `assault`: `while`, `until`, `fight`, `escort`
- `recover`: `fight` only — authoring `while`/`until` is an error, because its
  window is this body's own health and the beacon picture
- `muster`: `fight` only — same reason; its window is "a leader is calling"
- `squad`: nothing at all

`while` and `until` are **condition strings** (§2.5). Every mode above the
floor needs a `while` except `recover` and `muster`, which are conditioned
internally. A `while` without an `until` is a lint warning, not an error: once
active such a mode only yields to a stronger one, never back to a weaker.

**Escort lists** (`assault` only) accept three shapes:

```json
"escort": "medic"
"escort": ["medic", "lancer"]
"escort": [{ "role": "medic" }, { "role": "lancer", "posture": "screen" }]
```

`posture` is `"trail"` (default — follow behind the leader's line of travel)
or `"screen"` (interpose between the leader and the nearest known threat).
1..8 entries, no duplicates, and a role may not escort itself.

An escort entry is a **call**: it claims nobody. It is answered only by a role
whose own doctrine carries a `muster` mode (§3).

### 2.5 Conditions

`authoring.predicates` maps a predicate name to one comparison:

```json
"ghost-hurt": { "fact": "role-health", "operator": "at-most",
                "value": 2, "subject": "hunter" }
```

- `operator`: `at-least`, `at-most`, `equals`, `less-than`, `greater-than`
- `value`: 0..100000
- `subject` / `zone`: required only for the facts that take one (below)
- `freshnessTicks` (1..600): only on `remembered-enemies-in-zone` and
  `secured-cores`

A **condition string** is `predicate [and predicate ...] [or ...]` — lowercase
ASCII, digits and hyphens only, `and` binding tighter than `or`. There are no
parentheses and no negation.

**Fact vocabulary.** No subject: `always`, `tick`, `phase-state-ticks`,
`live-friendlies`, `known-enemies-unavailable`, `visible-enemy-carriers`,
`known-enemy-carriers`, `friendly-carriers`, `secured-cores`,
`visible-loose-cores`, `visible-loose-core-value`, `outstanding-well-count`,
`ticks-without-objective-progress`, `reactor-integrity`, `reactor-charge`,
`custody-state-ticks`, `own-filled-sockets`, `enemy-filled-sockets`.
Zone subject (`zone`): `friendlies-in-zone-count`, `group-in-zone-count`,
`visible-enemies-in-zone`, `remembered-enemies-in-zone`,
`visible-loose-cores-in-zone`, `visible-loose-core-value-in-zone`.
Group subject: `group-live-count`, `group-joining-count`,
`group-in-zone-count`, `group-cohesion`, `group-stuck-ticks`,
`formation-established-ticks`, `group-formation-broken`, `group-max-level`.
Role subject: `role-live-count`, `recover-ready-bodies`, **`role-health`** —
the minimum current health among live bodies of that role, and `9999` when the
role has no live body, so "hurt" is never true of the dead.
Well subject (`north`/`centre`/`south`): `well-has-outstanding`,
`own-socket-filled`, `enemy-socket-filled`, `well-ticks-until-birth`.
Order subject: `movement-complete`.

### 2.6 Custody policies

Required: `custodyId`, `authorizedCarrierRoles` (1..8 role ids), `escortGroups`
(0..8 group ids), `sourceWells` (1..3 of `north`/`centre`/`south`),
`pickupReservationTicks` (1..120), `transferTimeoutTicks` (1..120),
`deliveryTimeoutTicks` (1..1200), `accidentalPickup`
(`transfer`/`deliver`/`drop-safe`), `dropRecovery`
(`same-carrier`/`nearest-authorized`/`guard-until-safe`), `unreachableFallback`
(`hold`/`guard`/`alternate-core`/`regroup`), `safeConversionAll` (1..8
condition groups).

Optional, and the three an editor should surface first:
`deliveryRoutes` — 1..8 `{ "zone", "route" }` rules meaning *a Core lifted
inside this zone walks that route home instead of the shortest line*;
`accidentalPickup` above; and `baitDrop` — `{ "zone", "reclaimAll" }`, a
dropped Core left as a trap in that zone until the reclaim conditions hold.
`forwardPass` is `"none"` or `"relay-catcher"`. The remaining
`emergency*` keys are an advanced cluster and may be left out entirely.

### 2.7 Layout

Required: `schema`, `layoutId`, `mapId`, `bindings`, `zones`, `routes`,
`anchors`.

- **`zones`**: `{ "zoneId", "rect": [x0, y0, x1, y1] }` — inclusive map rects.
- **`routes`**: `{ "routeId", "corridorWidth", "waypoints": [[x, y], ...] }`.
  `corridorWidth` is how far off the line a body may reflow and still count as
  on-route.
- **`anchors`**: `{ "anchorId", "position": [x, y] }` — single tiles.
- **`bindings`**: 1..16 entries, one per orientation. Required
  `matchContractFingerprint` (or `"any-composition"`), `ownReactorSide`
  (`east`/`west`), `transform` (e.g. `rotate-180`), `routeAliases`; optional
  `formationAliases`, `parameterOverrides` (side-keyed re-values of declared
  `authoring.parameters`, within their declared ranges).

All coordinates are **absolute map tiles**, authored once for one orientation;
the binding's transform produces the other side.

---

## 3. Validation a frontend must mirror

The compiler is strict and its errors are the specification. The rules worth
enforcing live in the editor, because they are the ones an author trips:

1. **Exactly one verb per mode.** Zero or two is an error.
2. **Floor rule.** The last mode must be an unconditioned `patrol` or
   `squad` — no `while`, no `until`. `squad` may only be the floor.
3. **`squad` must be literally `true`.**
4. **Conditioned modes.** Every non-floor mode needs a `while`, except
   `recover` and `muster`.
5. **The muster/call two-way check.** A `muster` mode that matches no call is
   an error ("answers no call"); a call whose role has no `muster` mode is an
   error ("not recruitable"). *No muster mode means not recruitable* — this is
   the deliberate default, so adding an escort call to a leader obliges the
   editor to offer adding a `muster` mode to the recruit.
6. **Escort lists**: 1..8, no duplicate roles, no self-escort.
7. **`traffic` floor + any `breakOff` trigger** is an error: a computed
   traffic patrol is not a place a break-off can rally to.
8. **Every id is a reference.** Roles, groups, zones, routes, anchors, wells,
   custody ids and condition subjects are all checked against their
   declarations; identifiers are lowercase ASCII, digits and hyphens.
9. **Ranges** as tabulated in §2.3 and §2.6.
10. **Layout pin.** `layout.sha256` must match the layout file's bytes.

The cheapest correctness guarantee for an editor is to shell out to the
compiler on save and surface its error string verbatim; the messages are
written to be read by an author, and they carry a JSON path.

---

## 4. Frontend requirements (owner, verbatim intent)

- **User-friendly.** An author should be led through the schema, not
  confronted with it.
- **Must work on a phone.** This is a primary target, not a nice-to-have:
  layout, hit targets and the map view all have to work one-thumbed.
- **Conditional rendering.** The choices already made must narrow what is
  offered next — picking `recover` must not show a `while` field; picking
  `assault` must reveal `escort`; adding an escort call must prompt for the
  recruit's `muster` mode; a fact that takes a `zone` must offer the zone
  list, and one that takes a role must offer roles.
- **Everything spatial is PLOTTED ON THE MAP.** Positions, patrol routes,
  zones and anchors are map geometry and must be drawn and edited on a map
  view, never typed as raw coordinate pairs.

**Where the map data is.** Ordinary maps are JSON under `maps/`
(`formatVersion`, `id`, `width`, `height`, `tiles`, `spawns`, …). The Arc
Relay warren used by these sheets is **not** a file there — it is generated in
the engine (`ArcRelayMapGeometry.AmbushWarrenDeep`, selected by
`src/BotArena.Engine/ArcRelayLoopProfile.cs`, map id
`arc-relay-ambush-warren-06`, 31x27). The practical export for a frontend is
the **replay header**: every replay carries
`header.contract.map` with `width`, `height`, `tileRows`, `regions`,
`spawnAnchors` and `tileTags`, which is enough to draw the board exactly.
Running any match (`nilbots experiment arc-relay …`) produces one.

---

## 5. Persistence

**Implementation status (2026-08-08).** The local-first guarantees below
remain binding, and the hosted product now also persists tactical sheets in
the App's `Sheets` module. Every server save compiles through the shared
compiler library; the CLI and App do not carry separate validators. The
historical inventory in §5.1 records what this pass replaced.

### 5.1 What existed before the editor pass

Tactical sheets — the grammar this whole document describes — live **as files
in this repository** and are consumed by the CLI. Nothing in `src/BotArena.App`,
nothing in `contracts/BotArena.App.json`, and nothing in `web/src` references
`tactical-playbook` at all (grep returns empty). The compiler reads a path,
writes to a path, and no server is involved.

What the App *does* have is a **different, older document** that also happens
to be called a "sheet" — the arc-relay **commander sheet**:

| thing | where |
| --- | --- |
| entity (`OwnerUserId`, `Name`, `Revision`, `CanonicalJson`, `ContentHash`) | `src/BotArena.App/ArcRelay/ArcRelaySheet.cs` |
| document shape (`schemaVersion`, `mapId`, `slots[]`, `zones[]`, `rallyLines[]`, `policies`, `gambits[]`) | same file |
| canonical JSON codec, `SchemaVersion = 1` | `src/BotArena.App/ArcRelay/ArcRelayPlayerSheetCodec.cs` |
| storage | `DbSet<ArcRelaySheet>` — `src/BotArena.App/Shared/AppDbContext.cs:35`, entity config `:227` (jsonb column, `Revision` as concurrency token, unique-ish indexes on owner+name) |
| endpoints under `/api/arc-relay` | `src/BotArena.App/ArcRelay/ArcRelayEndpoints.cs` — `GET /sheets` `:74`, `POST /sheets` `:101`, `PUT /sheets/{sheetId:guid}` `:147`. No DELETE. |
| web editor | `web/src/site/pages/ArcRelayPage.tsx` (784 lines), wired in `web/src/site/api.ts` `:159-263` and `web/src/site/queries.ts` `:177,:505` |

**That format is not this format.** Slots-and-rally-lines is not
doctrines-and-modes; there is no migration path and none should be attempted.
It is stored in a database column, not through `IObjectStore`.

One coupling worth knowing before anything is deleted: `POST /sheets`
(`ArcRelayEndpoints.cs:127`) also creates an `ArcRelayEntrant` with
`Kind = ArcRelayEntrantKind.Sheet`. That enum has exactly two values, `Sheet`
and `CustomMind` (`ArcRelayEntrant.cs:3-7`), and entrants are created in
exactly two places — `:127` (sheet) and `:228` (custom mind). So the commander
sheet is one of the two ways a body reaches the arc-relay ladder, and removing
its editing surface is not purely a frontend deletion.

### 5.2 v1: local-first, always

Regardless of whether a backend is ever built, the editor must work with no
server:

- **Import** — accept the two files exactly as the compiler consumes them: a
  playbook JSON and a layout JSON. Preserve unknown keys on round-trip so an
  editor version never silently drops a field it did not know about.
- **Export** — emit the same two files, and **recompute `layout.sha256`** over
  the layout bytes on every export. A stale pin is a hard compile error and is
  the single most likely thing a naive editor gets wrong.
- **Draft persistence** — keep the working document in browser storage
  (IndexedDB for the documents, localStorage for cursor/UI state) keyed by a
  local draft id, autosaving on change. Phone browsers discard tabs; an
  unsaved hour is unacceptable.
- **Validation** — the editor mirrors §3, and the authoritative check is the
  compiler. If the editor is ever run beside a checkout, shelling out to
  `nilbots experiment arc-relay-playbook --playbook <file>` and surfacing the
  error verbatim is the cheapest correctness guarantee available.

This is enough to author, edit and ship sheets, because the consumer of a
sheet is the CLI reading files.

### 5.3 Hosted `Sheets` module

**Status: IMPLEMENTED.** The following is the retained design contract for
the hosted module.

Follow the repository's existing patterns exactly (`CLAUDE.md`, and the
commander-sheet module above as the working precedent):

- **Feature folder** `src/BotArena.App/Sheets/`, talking in-process; no broker,
  no microservice.
- **Entity** `TacticalSheet` — `Id`, `OwnerUserId`, `Name`, `Revision`
  (concurrency token), `PlaybookJson`, `LayoutJson`, `ContentHash`,
  `CreatedAt`, `UpdatedAt`. Register a `DbSet` on `AppDbContext` and add an EF
  migration (`cd src/BotArena.App && dotnet ef migrations add <Name>`, with
  `DOTNET_ROOT` exported).
- **Where the bytes go.** The two source documents are stored in PostgreSQL
  `json` columns, deliberately not `jsonb`: the layout pin hashes the exact
  submitted UTF-8 text and PostgreSQL must not rewrite its whitespace or key
  order. Compiled bytes are immutable match snapshots. Use `IObjectStore`
  (`src/BotArena.App/Storage/IObjectStore.cs`) if sheets later gain large
  attachments — database rows must never hold machine-local paths.
- **Endpoints** under `/api/sheets`, `RequireAuthorization()`:
  `GET /` (list, owner-scoped), `GET /{id}`, `POST /` (create),
  `PUT /{id}` (update, `expectedRevision` for optimistic concurrency),
  `DELETE /{id}`. The commander-sheet module has no DELETE; a drafting tool
  needs one.
- **Contract rules, non-negotiable.** Every handler returns a **named response
  record** — an anonymous type produces no schema — and carries
  `.Produces<T>()`, because `Results.Ok(...)` is untyped to ASP.NET. Then run
  `bash scripts/generate-api-clients.sh` and commit the regenerated
  `contracts/BotArena.App.json`, `web/src/api/schema.d.ts`,
  `mobile/src/api/schema.d.ts` and
  `src/BotArena.Cli/Generated/ApiContracts.cs` **with** the change. Never
  hand-edit a generated file. CI's `contract-drift` job regenerates and fails
  on any diff.
- **Compile on write, or not at all.** Either validate server-side by invoking
  the compiler and reject invalid documents, or store blobs verbatim and let
  the editor be the only validator. Do not half-validate: a sheet that the
  server accepted and the CLI rejects is the worst outcome.
- **Entrant coupling.** The original sketch left admission separate; the
  subsequent owner ruling in §6.3 deliberately supersedes that choice for the
  shipped product.

---

## 6. Legacy surfaces to remove

**The editor build removed the surfaces below.** They are the previous
generation of sheet editing and are not carried forward, migrated from, or
kept behind a flag. This section remains as the located-and-verified deletion
inventory.

### 6.1 No Frontline sheet editor exists

Searched and found nothing to remove. `find web/src mobile/src -iname
"*frontline*"` returns exactly one file, `web/src/render/frontlineCaptureVisual.ts`,
which is a renderer. Every other `frontline` hit under `web/src` is
replay/render/audio/bridge code (`replayWireV2.ts`, `replayModel.ts`,
`replayNormalize.ts`, `hostedBridge.ts`, `render/drawArena.ts`, …) — match
viewing and runtime, explicitly **out of scope and to be left alone**. No
Frontline authoring UI, no Frontline sheet endpoints.

### 6.2 The arc-relay commander-sheet editor — remove

This is the real one. It edits the `slots`/`zones`/`rallyLines`/`policies`/
`gambits` document of §5.1, which the new grammar replaces.

**Frontend**

| path | what |
| --- | --- |
| `web/src/site/pages/ArcRelayPage.tsx` | the editor (784 lines). **Not a whole-file delete** — the same page also launches matches (`launch.mutate({ entrantId, opponentEntrantId, seed })`) and lists entrants/ladder. Remove the editing half; keep or relocate the launcher. |
| `web/src/site/Site.tsx:22, 57, 105` | import and the two mount points |
| `web/src/site/api.ts:159-163, 175` | `ArcRelaySheet`, `ArcRelaySheetDocument`, `ArcRelaySheetSlot`, `ArcRelaySheetPoint`, `ArcRelaySheetGambit`, `SaveArcRelaySheetRequest` type aliases |
| `web/src/site/api.ts:224, 260-263` | `arcRelaySheets`, `createArcRelaySheet`, `updateArcRelaySheet` |
| `web/src/site/queries.ts:17, 56, 177, 505` | `SaveArcRelaySheetRequest` import, `keys.arcRelaySheets`, `useArcRelaySheets`, `useSaveArcRelaySheet` |

`web/CLAUDE.md` rules still apply to whatever remains, and
`web/src/site/structure.test.ts` plus the web test suite must be green after
the removal.

**Backend** (`src/BotArena.App`)

| path | what |
| --- | --- |
| `ArcRelay/ArcRelayEndpoints.cs:74` | `GET /api/arc-relay/sheets` |
| `ArcRelay/ArcRelayEndpoints.cs:101` | `POST /api/arc-relay/sheets` |
| `ArcRelay/ArcRelayEndpoints.cs:147` | `PUT /api/arc-relay/sheets/{sheetId:guid}` |
| `ArcRelay/ArcRelaySheet.cs` | entity + `ArcRelaySheetDocument` and its record tree |
| `ArcRelay/ArcRelayPlayerSheetCodec.cs` | canonical codec, `SchemaVersion = 1`; also feeds `GET /catalog` (`ArcRelayEndpoints.cs:44-52`), so that response shrinks rather than disappears |
| `Shared/AppDbContext.cs:35, 227` | `DbSet<ArcRelaySheet>` and its entity configuration; needs an EF migration to drop the table |

**Generated clients — mandatory follow-up.** Removing App endpoints changes
the HTTP contract, so after the deletion run `bash scripts/generate-api-clients.sh`
and commit the regenerated `contracts/BotArena.App.json`,
`web/src/api/schema.d.ts`, `mobile/src/api/schema.d.ts` and
`src/BotArena.Cli/Generated/ApiContracts.cs` in the same change. Never
hand-edit them. CI's `contract-drift` job regenerates and fails on any diff.

### 6.3 Entrant coupling ruling

**Owner ruling (2026-08-08):** saving a tactical sheet creates or revises the
same-id `Sheet` entrant and enters it into the ladder by default. The editor
offers an explicit opt-out. Save-as-copy creates a new entrant identity; an
ordinary revision preserves rating. This ruling supersedes the provisional
separation advice in the original backend sketch.

The new `POST /api/sheets` stores the exact playbook and layout, creates the
same-id `ArcRelayEntrant` with `Kind = Sheet`, and establishes its
current-ladder rating when opted in. `PUT /api/sheets/{id}` revises both views
atomically under optimistic concurrency. The destructive migration cannot
translate commander documents into tactical playbooks, so it retires their
entrants from future pairing before dropping the old table; historical match
snapshots and rating foreign-key identities remain intact.
