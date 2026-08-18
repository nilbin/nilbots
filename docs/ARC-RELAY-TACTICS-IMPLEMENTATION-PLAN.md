# Arc Relay Tactics — implementation plan

Status: implementation plan, ready for a single long-running session
Date: 2026-08-18
Authority: `docs/ARC-RELAY-WHOLE-TEAM-TACTICS-EDITOR-REDESIGN.md` (the design decision) plus the
owner-iterated interactive mock at `sandbox/tactics-editor-mock/` (the behavioral spec).
This plan replaces the Programs authoring layer with Tactics and replaces the web Sheet editor.
It does not change the game, the compiler grammar, the executor, or any frozen playlist.

**Pre-prod stance (owner, 2026-08-18): the game is not live — backwards compatibility is a
non-goal.** No migration paths, no dual save modes, no legacy affordances. The one durable
compatibility fact that remains is internal machinery, not product: the playlist definition
validates its own immutable bytes at startup, and stock/starter sheets stay raw Orders v2 —
so the compiler keeps its Orders v2 entry point (it is the materialization target anyway).

---

## 0. What we are building, in one paragraph

A Sheet becomes a squad of eight bots plus an ordered list of whole-team **Tactics**. Each
Tactic splits the eight into 1–3 temporary Groups with per-group Stage/Act phases, postures,
recovery presets, Core pickup rules and spatial signature marks; the Tactic owns shared
Start/Release/Finish condition sets (one tick-row condition-editor idiom for all three), a
delivery plan with dynamic carrier, and a map-first wizard in the order
**Start → Groups → Stage → Release → Act → Cores → Finish → Review**. The player-owned
document is `arc-relay-tactics-v1`; a **deterministic server-side materializer** turns it into
today's `arc-relay-orders-v2` + `arc-relay-map-plan-v2` pair, which the existing shared
compiler compiles to ATL2 exactly as it does now. Programs
(`arc-relay-workshop-v1`, the client-side generator, and the four-section editor) are removed.

---

## 1. The current system (established by code dive, verified 2026-08-18)

**Three documents.** `arc-relay-orders-v2` (executable; seven root keys
`schema,sheetId,mapPlan,roles,sharedOrders,capacities,conditions`) and
`arc-relay-map-plan-v2` (six root keys `schema,mapPlanId,mapId,zones,routes,anchors`) are the
only things the server compiles. `arc-relay-workshop-v1` is an **editor-only sidecar** the
server stores opaquely (`src/BotArena.App/Sheets/TacticalSheetWorkshopValidator.cs` pins only
≤192 KiB and root `{schema, plays}`).

**Programs are client-side sugar.** `web/src/site/sheets/linkedPlays.ts:432`
(`materializeWorkshop`) generates per-role order bands from the sidecar in the browser,
unshifts them above hand-written Instructions, and detects hand-edits with FNV fingerprints
("drift" blocks saving). The server never materializes anything.

**The shared compiler** (`src/BotArena.TacticalPlaybooks/`, three source files, **zero
dependencies**) validates both documents and emits ATL2 — which is *not* an IR but the
canonicalized source pair + SHA-256s framed in a 64 KiB envelope
(`ArcRelayTacticalPlaybookCompiler.cs:104-130`). The in-process
`ArcRelayTacticalPlaybookMind` (at `arena-bots/arc-relay/tactical-playbook-mind-v1/`)
re-parses that JSON at runtime into `OrdersPackage` and executes first-eligible-leaf-wins,
top to bottom.

**Save path.** `POST/PUT /api/sheets` takes
`{name, expectedRevision, ordersJson, mapPlanJson, enterLadder?, workshopJson?, replaceWorkshop?}`,
compiles via `Sheets/TacticalSheetCompiler.Compile` (adds App-only real-map geometry checks +
movement-zone capacity feasibility), stores `TacticalSheet{PlaybookJson(json), LayoutJson(json),
WorkshopJson(jsonb), ContentHash, Revision}` with `Revision` as the concurrency token, and
creates/updates the same-GUID `ArcRelayEntrant`. Admission recompiles and verifies
`ContentHash` before every match.

**Playlist coupling.** v24's canonical definition pins `"sheetSchema": "arc-relay-orders-v2"`
and the stock-sheet source hashes (`Competition/ArcRelayEntrantPlaylistDefinition.cs:380-384`,
immutable, startup-validated). Because Tactics materialize *into* Orders v2 and the embedded
stock/starter resources are untouched, **no playlist version or rating-era change is required**
(precedent: DECISIONS #329 — editor promotion is not a hosted migration). Adding a
`TacticsJson` column and request field does not touch the canonical definition.

**Key vocabulary facts the materializer must respect** (all enforced by
`ArcRelayOrdersSheetDsl.cs`):
- Order node = shared ref | branch | leaf(one of `move|fight|use|handoff|drop`); optional
  `when,until,timeout,cooldown,capacity,urgency,select`. Branch depth ≤2, ≤16 children,
  ≤16 root orders per role, roles ≤8, flattened classes exactly 8, ≤2 copies per class.
- 53 condition facts (body/role/team/zone/well/core-evidence groups) with strict qualifier
  subsets; expressions are `name | {all:[2..8]} | {any:[2..8]}` — **no nesting, no negation**;
  `since:"node-entry"` is legal **only in `until`**.
- Consumer honesty: shared orders and named capacities need ≥2 expanded consumers, every
  condition ≥1, every zone/route/anchor ≥1 sheet reference. Identifiers `[a-z0-9-]{1,48}`.
- Caps: ATL2 ≤64 KiB total; App source gates 192 KiB orders / 96 KiB map plan.
- Signature authoring is descriptor-driven (`ArcRelaySignatureAuthoringCatalog.cs` — 16
  class/skill pairs, named targets + capability flags `FriendlyRole|MostExperiencedAlly|
  ExactAnchor|RouteAheadRole|RouteForward`). No per-signature feature switches anywhere.

---

## 2. Architecture decision: server-authoritative materialization

**Store the Tactics document first-class and materialize on the server, in
`BotArena.TacticalPlaybooks`.** The client never generates Orders.

Why this beats repeating the Programs pattern (client-side generation + opaque sidecar):

1. **The whole drift apparatus disappears.** Programs needed fingerprints, drift inspection,
   "Regenerate/Detach", and refuse-to-save-on-drift *because* generated and hand-written
   Instructions coexisted. The redesign forbids editing generated output, so the correct
   model is: Tactics doc is the only source; Orders are regenerated wholesale on every save.
   With no hand-edit path there is nothing to drift.
2. **One materializer, not two.** The mock proved the editor needs no client-side
   materialization for UX — readback sentences and map overlays derive directly from the
   Tactics doc. Client-side generation would still require server verification to be
   trustworthy, i.e. a second implementation.
3. **It is golden-testable and CLI-reusable** in the dependency-free shared assembly, which
   is exactly where the design doc's acceptance gate ("each flagship recipe has deterministic
   materialization") wants it pinned.
4. **Practice attribution comes free**: the materializer mints stable, meaningful order ids
   (`t2-flankers-act-converge`), so the executor's `debugMessage` paths map straight back to
   Tactic/Group/phase in the practice lens without any sidecar bookkeeping.

Constraint to honor: `BotArena.TacticalPlaybooks` must stay dependency-free (System.Text.Json
+ the signature catalog only). Anything needing the real map (floor tiles, spawns, protected
homes) stays in `App/Sheets/TacticalSheetCompiler.ValidateMapPlanGeometry`, which continues to
run on the materialized pair unchanged. Tactics-level geometry checks in the pure assembly are
therefore *structural* (bounds, shape); tile-level legality still lands as compile errors with
JSON-path diagnostics that the web maps to fields (extend the `sheetIssueFromCompilerError`
pattern with `tactics.json.` paths).

**Wire/storage changes** (regenerate clients after; pre-prod, so cut hard):
- Migration `TacticsAuthoring`: add `TacticalSheets.TacticsJson (jsonb, NOT NULL for new
  rows)` and **drop `WorkshopJson`** in the same migration.
- `SaveTacticalSheetRequest` becomes `{Name, ExpectedRevision, TacticsJson, EnterLadder?}` —
  the player save API is tactics-only. The server validates, materializes Orders v2 + map
  plan, compiles, and stores tactics + the derived pair + ContentHash. `OrdersJson/
  MapPlanJson/WorkshopJson/ReplaceWorkshop` leave the request. (Raw Orders v2 authoring
  remains available via the CLI compile path only.)
- `TacticalSheetResponse` carries `TacticsJson` (+ the materialized pair for debugging/
  attribution); `TacticalStarterSheetResponse` replaces `workshopJson` with `tacticsJson`.
- `PracticeAuthoringSnapshot` swaps its workshop field for the tactics doc.
- `TacticalSheetWorkshopValidator` is deleted.

**Legacy sheets**: no compatibility owed (pre-prod). Existing player sheet rows without a
tactics document are dead weight — delete them freely (or leave them inert; either is fine,
no UI accommodates them). No import, no read-only view, no transition affordances.

---

## 3. The Tactics document — `arc-relay-tactics-v1`

Shape settled by the mock (field names final at implementation; keep kebab/camel consistent
with Orders v2 conventions — JSON keys camelCase, ids kebab):

```jsonc
{
  "schema": "arc-relay-tactics-v1",
  "sheetId": "…",
  "mapId": "arc-relay-tapline-02",
  "squad": [ { "slot": 0, "class": "kestrel", "build": "scout", "name": "vex" }, … 8 ],
  "tactics": [
    {
      "id": "pincer-1", "name": "Pincer", "recipe": "pincer",          // recipe = provenance only
      "start":   { "mode": "always|when", "conds": { …condition rows… } },
      "release": { "mode": "immediate|ready|ready-when", "conds": { … } },
      "finish":  { "outcomes": { kills{on,count}, losses{on,count}, cleared{on,zone},
                     coreAcquired, coreBanked, coreLost, reached, observed, persistent },
                   "maxAttempt": "none|brief|standard|long" },
      "delivery": { "plan": "none|solo|escort", "preferredSlots": [6] },
      "groups": [
        {
          "id": "mid-guard", "name": "Mid Guard", "slots": [5,2],
          "coreRule": "ignore|grab|hunt",
          "stage": { "kind": "posts|area|line|approach|none", "posture": "hold",
                     "recovery": "fight-on|patch|heal-tile",
                     "posts": [ { "at": [14,12], "facing": "n|e|s|w" }, … ],
                     "rect": [x1,y1,x2,y2], "path": [[x,y]…], "point": [x,y] },
          "act":   { "kind": "none|converge|advance|patrol|return|escort", "posture": "aggressive",
                     "recovery": "fight-on", "point": [x,y], "path": [[x,y]…] },
          "sigs":  { "stage": { "mason": { "points": [[17,12],[17,14]] } },
                     "act":   { "switchback": { "rect": […] }, "longshot": { "line": [[..],[..]] } } }
        }, …
      ]
    }, …
  ]
}
```

Validation rules (pure assembly): exact-keys discipline like Orders v2; every slot 0–7 in
exactly one group per tactic; groups 1–3; composition rules (8 bodies, ≤2/class, ≥7 distinct —
mirror `ArcRelayComposition`); tactic count ≤8 (the design doc's ranked cap — enforce now,
it also protects the materialization budget); geometry ints in-bounds for the declared map
size; condition parameters in the same ranges the target facts require; signature marks only
for classes present in the group and within each skill's mark budget; watch zones either a
named catalog zone id or a custom rect.

Named zones (`mid`, `your-well`, …) are an **editor/catalog concept**: the document stores the
resolved rect (plus the name for display), so the materializer never needs the zone catalog.

---

## 4. Materialization map (Tactic concept → Orders v2 encoding)

Encoding skeleton per role (order = priority; sheet tactic order preserved):

```
role.orders = [
  T1-branch, T2-branch, …            // one root branch per tactic this role participates in
  shared: score-a-core               // when delivery/coreRule implies it (Tempered pattern)
  shared: collect-loose-core         // from coreRule
  shared: recover-health             // from recovery presets
  shared: fight-visible-enemy        // baseline
  fallback move                      // hold-ground default
]
```

Each tactic branch: `when` = start conditions (mode `when`) — else omitted; `until` =
`{any: finish outcomes…}` with evidence outcomes encoded as HistoricalFacts using
**`since: "node-entry"` on the release sub-branch** so outcomes count *after release* exactly
as the UI promises; `timeout` from maxAttempt (brief/standard/long → tick values;
Tempered's 180 is the calibration reference). Inside (branch depth 2 is the ceiling — this
uses both levels):

```
T-branch(when=start, until=finish, timeout)
├─ release-branch(when = all[ready…] / any[release conds…])   // Act phase
│  └─ act leaves per this role's group (converge/advance/patrol/return/escort + posture)
└─ stage leaves (posts/area/line/approach moves + stage sig uses + stage postures)
```

Since `any`/`all` cannot nest, "ready AND (A OR B)" is encoded as sibling release-branches or
duplicated gated leaves — first-eligible-wins gives OR for free. Readiness = synthesized
`role-in-zone-count`/`friendlies-in-zone` conditions over the staging geometry (Programs
precedent; posts get a tolerance rect like Play slots did).

| Mock concept | Orders v2 encoding | Confidence |
|---|---|---|
| Group posts + 4-way facing | per-body `move.to {anchor}` + `face {anchor}` (generated facing anchor one tile in the facing direction); one post per body via inline `capacity: 1` per anchor or per-slot roles (see role strategy below) | direct |
| Stage area / line / approach | `move.to {zone}` (area→zone; line→route `via` + zone at end; approach→`{adjacentTo}`/anchor short of point) | direct |
| Stage posture wait/hold/observe | posture maps to fight gating: wait→no fight leaf until release; hold→`fight style hold, chaseLimit 0-2`; observe→no fight + vision positioning | direct |
| Act converge/advance/patrol | `move.to {zone/anchor}` at destination / `move` with `via` route / `move.patrol` route | direct |
| Act return-and-bank | `move.to "own-reactor"` when carrying (score-a-core pattern) | direct |
| Act escort-carrier | `move.follow {selector: "nearest-friendly-core-carrier"}` | direct |
| Release "every group ready" | `all[…role-in-zone-count synthesized conds…]` | direct (Programs precedent) |
| Release enemies-at box | `visible-enemies-in-zone` / `remembered-enemies-in-zone(freshnessTicks)` over generated zone | direct |
| Release contact / core-loose | `self-nearest-visible-enemy-distance` team-widened via zone variant / `well-has-outstanding-core` | direct |
| Start conditions | same fact set on the tactic branch `when` (carrier→`friendly-carriers`, losses→`live-friendlies at-most 8-N`) | direct |
| Finish kills/losses (after release) | `confirmed-enemy-destructions-in-zone` or core-evidence style facts with `since: node-entry` in `until`; losses via `live-friendlies` | direct-ish — verify per fact which historical variant fits; flag any gap in the proof phase |
| Finish cleared/reached/observed | `visible-enemies-in-zone equals 0` (+`zone-fully-observed-throughout-interval` for observed) / `self-in-zone`→role-in-zone at destination | direct |
| Core pickup ignore/grab/hunt | omit / `nearest-visible-loose-core` + `withinWalkingSteps` small / `nearest-known-loose-core` unbounded (+ fight carrier targets for hunt) | direct |
| Delivery solo/escort + preferred carrier | score-a-core (urgent, carrying→reactor) + escorts follow-carrier; preferred = `handoff {role/selector,roles}` + relay `arc-toss` toward preferred role when present | direct |
| Recovery fight-on / heal-tile | omit heal order / `hurt → move.to "nearest-heal-zone"` until `healthy` (Tempered pattern) | direct |
| Recovery patch-up-close | approximation: `hurt → move posture rear` within current geometry (no repair verb exists; patchbay heals via its own signature) | **approximation — document in UI copy** |
| Signature marks (points/zone/line) | `use {skill, target(named/preset), at: anchor}` per mark for ExactAnchor skills; switchback dash → `use vector-dash {route, progress: forward}` over a generated terminal route through the zone; longshot rail → rail-line use + posts/route along the line; gate stage-marks vs act-marks by placement inside/outside the release branch | direct for ExactAnchor skills; dash/rail need the route encoding — prove in Phase 1 |
| Posture aggressive/steal | fight `style advance` higher chaseLimit + carrier/weakest targets; steal prioritizes carrier + loose-core moves | direct |
| Attempt timer | branch `timeout` | direct |
| Persistent | no `until` outcomes and no timeout | direct |

**Role strategy** (settled with the owner 2026-08-18): Roles have **no meaning at the
Tactics level** — they are purely a materialization artifact. Materialize **one role per
squad slot**, id derived from the bot's name (kebab, ≤24 chars, slot-suffixed on collision),
so the role *is* the bot (8 roles × 1 class — legal: roles ≤8, classes per role 1..8).
This is load-bearing, not merely convenient: per-bot builds map to per-role `upgradePath`;
preferred carriers need a single-bot role to be addressable (`handoff {role}`,
`friendly-carriers-in-roles`); per-bot posts/facing are each role's own move+face leaf; and
replay/practice attribution shows bot names for free (`body.SetRole`, `orders:vex > …`).
Group readiness deliberately does NOT use per-role conditions (an 8-bot group would blow the
8-name `all[…]` cap): it is one `friendlies-in-zone` condition over the group's staging
geometry. Cost is per-role duplication of shared bands — mitigated by `sharedOrders`
(consumers ≥2 comes free with 8 roles). Budget check: 8 roles × ~12 root entries, shared definitions written once;
Tempered Standard is 7.3 KB source for 4 roles × 7 orders; a full 8-tactic sheet lands well
under the 192 KiB source gate, and ATL2's 64 KiB cap is the number to watch — **add a
materializer test that asserts a maximal document (8 tactics × 3 groups, all features) stays
under the envelope.**

**Generated ids** are deterministic and readable: `t{n}-{tacticId}-{groupId}-{phase}-{verb}`,
conditions `c-{tacticId}-…`, geometry `g-{tacticId}-…`, truncated to 48 chars with a stable
hash suffix on collision. These ids are the practice-attribution contract.

---

## 5. Backend workplan

**Phase 1 — `BotArena.TacticalPlaybooks` (pure)**
1. `ArcRelayTacticsDsl.cs` — schema/exact-keys/range validation of `arc-relay-tactics-v1`
   (partial of the compiler class, same style as `ArcRelayOrdersSheetDsl.cs`).
2. `ArcRelayTacticsMaterializer.cs` — pure function `(tacticsJson) → (ordersJson, mapPlanJson)`
   emitting canonical-friendly JSON (stable member order; the canonicalizer sorts keys anyway
   but array order is load-bearing).
3. Compiler entry `CompileTactics(byte[] tactics)` → validates, materializes, then reuses the
   existing `Compile(orders, mapPlan)`; `TacticalPlaybookCompilation` gains the tactics hash +
   materialized sources (additive record fields).
4. Tests (`tests/BotArena.Cli.Tests/ArcRelayTacticsMaterializerTests.cs` + compiler tests):
   golden materialization per flagship recipe (Pincer, Centre Rush, Hold Area, Core Raid,
   Deliver Core — byte-exact expected Orders/map-plan fixtures); determinism (same doc twice →
   identical bytes); envelope budget (maximal doc < 64 KiB); id validity; consumer honesty of
   generated output; property test that every generated sheet passes `Compile`.
5. CLI: extend `experiment arc-relay-playbook` with `--tactics <json>` (emits the same four
   outputs + `materialized-tactics` provenance in explain.json). Update CLI help.

**Phase 2 — `BotArena.App`**
1. Migration `TacticsAuthoring`: add `TacticsJson jsonb`, drop `WorkshopJson`.
2. `TacticalSheetCompiler`: tactics branch — validate size gate (192 KiB), call
   `CompileTactics`, then the existing real-map geometry validation on the materialized pair.
3. Endpoints: reshape `SaveTacticalSheetRequest` to tactics-only (see §2); POST/PUT
   validate + materialize + compile + store; response carries `TacticsJson` and the
   materialized pair. Delete the workshop validator and its request fields outright.
4. Catalog: author 1–2 tactics-native starters under
   `arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/tactics/` (materialize + verify at
   build like existing starters); `TacticalStarterSheetResponse.tacticsJson`. Embedded stock
   resources untouched.
5. Practice snapshot: include tactics doc; `programAttributionAvailable` generalizes to
   attribution-by-order-id (ids are now tactics-derived).
6. `bash scripts/generate-api-clients.sh`; commit regenerated web/mobile/CLI clients.
7. Tests: `TacticalSheetCompilerTests` tactics cases (accept/reject, geometry, zone-capacity
   feasibility on materialized output), endpoint round-trip in
   `ArcRelayEntrantProductIntegrationTests`, trial + snapshot in hosted-execution tests.

**Docs/decisions (do alongside, not after):** DECISIONS **#331** (Tactics replace Programs;
legacy sheets read-only + squad-only copy; no playlist migration — cite #329);
`docs/SPEC-SHEET-ORDERS-V2.md` stays normative for the materialization TARGET (fix the noted
staleness: missing fight targets `veteran|deployment`, several facts/qualifiers — see dive);
new `docs/SPEC-SHEET-TACTICS-V1.md` (player-owned source contract + materialization rules);
rewrite `docs/SHEET-AUTHORING.md` §10 for Tactics; mark the two Programs plan docs historical.

---

## 6. Web workplan

**New folder `web/src/site/tactics/`** (the old `sheets/` folder is deleted except extracted
shared pieces). The mock is the UX spec; port its structure, not its code style — the real
editor is React + TanStack Query + the generated schema, DRY where the mock was deliberately
dumb.

Component architecture (each its own file, nothing near the old 3k-line files):
- `TacticsEditorPage.tsx` — route host, layout (rail | map | drawer), StateView four-states,
  `errorMessage.ts` for mutation errors (fix the old editor's bent conventions).
- `useSheetDraft.ts` — draft state + undo/redo + IndexedDB autosave (port the existing
  IndexedDB layer; hook-wrap storage access; keep the local-newer-than-server merge).
- **Shared idiom components** (the DRY core the owner asked for):
  - `ConditionRows.tsx` — THE condition editor (tick rows, inline params, map-watch
    affordance), configured by a condition-kind table; used by Start, Release, Finish.
  - `SegChips.tsx`, `ChoiceCards.tsx`, `GeoStatusRow.tsx`, `PhaseStrip.tsx`, `BotChip.tsx`,
    `Modal.tsx`, `IssueSummary.tsx` (dedupe the two existing Modal/IssueList copies).
  - `useMapTask.ts` — the draw-task state machine (posts/rect/route/point/watch/sig) feeding…
  - `TacticsMapCanvas.tsx` — extends the PlayMapCanvas idiom over the extracted `BaseMap.tsx`
    (pull `BaseMap` + `MapTargetPreview` out of `MapPlanEditor.tsx` into their own files);
    overlay renderers per concern (group geometry, watch boxes, sig marks, delivery run,
    heal links, fan runs) with the z-order rule from the mock (condition footprints under
    group geometry).
  - `summary.ts` — the natural-language readback (port the mock's generator; verb agreement,
    place naming from the zone catalog).
- Wizard steps as files: `StartStep.tsx`, `GroupsStep.tsx`, `PhaseStep.tsx` (stage/act),
  `CoresStep.tsx`, `ReleaseStep.tsx`, `FinishStep.tsx`, `ReviewStep.tsx`; `recipes.ts`
  (prefills only; contextual pruning keys off document CONTENT, never `recipe` —
  owner-flagged smell in the mock).
- `tacticsDocument.ts` — TS types + client-side validation of the tactics doc (structural +
  bounds; the server compile remains the authority; map server diagnostics with an extended
  `sheetIssueFromCompilerError`).
- Reuse untouched: `sheetMapPresentation.ts`, `IdentifierInput.tsx`, `SignatureUseEditor`'s
  descriptor logic (as the source for which signature geometry each class gets — derive the
  mark-geometry table from `signatureAuthoring` capabilities: ExactAnchor→points,
  RouteForward→zone/route, etc., instead of hardcoding), `replayUnitTracks`, queries/api
  hooks (extended for `tacticsJson`), css component classes.

**Kill list** (clean cut — only `ArcRelayPage.tsx:19` imports the old editor):
`workshopTypes.ts`, `linkedPlays.ts`, `playEditing.ts`, `playRecipes.ts`, `playGeometry.ts`,
`programAnalysis.ts`, `PlaysWorkspace.tsx`, `PlayMapCanvas.tsx`, `TacticalSheetEditor.tsx`,
`OrdersWorkspace.tsx` (no editable Instructions view in the new editor; optional later:
read-only generated-source disclosure), the attribution half of `sheetTrials.ts` re-pointed
to tactics ids, `tests/linkedPlays.test.ts`, and a rewrite of `tests/seasonEntrantIa.test.ts`
to pin the NEW IA/copy. Update `DocsPage.tsx` sheet section, `ArcRelayPage` copy,
`scripts/site-shots.mjs` target, and `vite.site-review.config.ts` (feed it a tactics starter
fixture; `seasonEntrantIa` pins those paths).

**Practice lens**, v1 scope: keep trials + unit tracks + attribution mapped to
Tactic/Group/phase labels via the generated-id scheme; drop the Programs-specific readiness/
formation evidence panels (re-imagine later against Tactics concepts).

**Web tests**: unit tests for `summary.ts`, `tacticsDocument.ts` validation, materialized-id
attribution mapping; rewritten `seasonEntrantIa`; and **browser interaction tests** (the
design-doc acceptance gate) — a Playwright spec per flagship recipe driving the real editor
against the review harness, in the spirit of the mock's `drive.cjs` (43 assertions there;
port the flow). Wire as an opt-in npm script like `site-shots` (Playwright is already a dev
dependency).

---

## 7. Proofs and verification gates

Per-phase gates (run before moving on; commit at each gate):
- **G1** `dotnet build BotArena.sln` + `dotnet test tests/BotArena.Cli.Tests` (materializer
  goldens green; determinism + budget tests green).
- **G2** `dotnet test tests/BotArena.App.Tests` (+ opt-in `BOTARENA_TEST_DB` suite) and
  `bash scripts/generate-api-clients.sh` with a clean diff after commit (contract-drift).
- **G3** `(cd web && npm run build && npm test)`; review harness serves the new editor;
  browser specs green.
- **G4 — match proofs** (design-doc gate "a real match proof" per flagship recipe): for each
  recipe, author a Tactics doc, materialize via CLI (`experiment arc-relay-playbook
  --tactics`), run `nilbots arc-relay` with it in-process against a stock opponent, and
  verify the intended behavior in the replay (six-cell exhibit per the sheet-tuning skill;
  serve the gallery over a cloudflared tunnel for the owner). The Pincer proof must show:
  staged guard at Mid, flankers staging untriggered, release on the enemies-at condition,
  convergence, and finish counting only post-release.
- **G5** full `bash scripts/test.sh` sweep + `git status` clean; final report cites the SHA.

Explicitly **not** in scope / guardrails: no engine or `GameRules` change; no executor change
unless a mapping gap is proven (then: smallest possible mind change, its own exhibit, its own
commit — and note the mind's WASM mirror only needs rebuilding at release checkpoints, not
now); no touching frozen playlists, embedded stock resources, or pinned hashes; App never
references BotArena.Cli; `BotArena.TacticalPlaybooks` stays dependency-free; one top-level
class per file; TreatWarningsAsErrors stands.

---

## 8. Session running order (single long session)

0. **Setup** — branch `agent/tactics-editor` off `main`. Read pack:
   `docs/ARC-RELAY-WHOLE-TEAM-TACTICS-EDITOR-REDESIGN.md`, this plan,
   `ArcRelayOrdersSheetDsl.cs`, `ArcRelaySignatureAuthoringCatalog.cs`,
   `Sheets/TacticalSheetCompiler.cs` + `TacticalSheetEndpoints.cs`,
   `arena-bots/…/playbooks/tempered-standard-v2.json` (+ its map plan),
   `web/src/site/sheets/sheetAuthoring.ts`, `web/CLAUDE.md`, and the two code-dive substrate
   reports with line-level detail: `sandbox/tactics-dive-notes/compiler-backend-dive.md` and
   `sandbox/tactics-dive-notes/web-editor-dive.md` — and run the mock once
   (`sandbox/tactics-editor-mock/`, `python3 -m http.server`) to feel the target.
1. **Tactics grammar + materializer + goldens** (Phase 1). Start with the Pincer end-to-end
   (doc → materialized pair → compiles → CLI match proof) before widening to all recipes —
   the Pincer exercises every hard part (readiness, release conditions, post-release
   counting, per-bot posts/facing, signatures, delivery suggestion surface).
2. **App integration** (Phase 2) + contract regen.
3. **Web editor** (Phase 6 section) — build the shared idiom components first
   (ConditionRows, useMapTask, TacticsMapCanvas, summary), then the wizard steps, then the
   page; delete the Programs folder last (keep the app building at every commit).
4. **Copy/docs/tests sweep** — DocsPage, SHEET-AUTHORING §10, SPEC-TACTICS, SPEC-ORDERS
   staleness fixes, DECISIONS #331, seasonEntrantIa rewrite, site-shots, review harness.
5. **Proof gallery + final sweep** (G4, G5) — tunnel URL in the report.

Decision checkpoints to surface to the owner in the final report (proceed with the plan's
recommendation; none block the session): the exact brief/standard/long timeout tick values;
whether a read-only generated-source disclosure ships; whether existing pre-tactics sheet
rows are deleted or left inert; patch-up-close approximation wording.

## 9. Risks

- **Vocabulary gaps discovered mid-materialization** (most likely: exact post-release kill
  counting semantics, dash-zone encoding). Mitigation: Pincer-first ordering surfaces these
  in hour one of Phase 1; fallback is the documented-approximation route, executor changes
  only with proof.
- **ATL2 64 KiB budget** with 8 roles × 8 tactics. Mitigation: budget test from day one;
  shared-order factoring; if breached, collapse per-slot roles into per-group roles for
  tactics where slots within a group are homogeneous.
- **seasonEntrantIa/copy pinning churn** — the test pins exact strings; rewrite it *with* the
  new IA rather than fighting it piecemeal.
- **One-unit branch**: old editor and Programs layer are deleted in the same branch the new
  editor lands in; the branch merges whole or not at all. Pre-prod means no transition
  window is owed regardless.
