# Map-first Tactics Editor — interaction refinement report

Date: 2026-08-20

## Outcome

Map-first is now the ordinary spatial phase editor rather than a map beside a
permanent wizard. It remains a view over the existing `TacticsDocument`, one
wizard draft, one map-task system, one validation/serialization/compiler path,
and the existing save/practice APIs. Classic remains a visible shared-state
fallback.

The ordinary authored spatial vocabulary is Posts and Patrol. Loaded Area,
Converge, and other advanced jobs remain intact and visible as saved, with an
explicit best-effort Convert to posts action. No saved schema or runtime
behavior was added for selection, projection, or smart placement.

## Interaction architecture

- `MapFirstTimeline` owns only the compact phase chronology, icon-only
  transition buttons, add-phase, and global Start/Core/Abort navigation.
- Map-first has no permanent right-hand wizard. Temporary drawers reuse
  `StartStep`, `GroupsStep`, `CoresStep`, `JobSection`, and the extracted shared
  `TransitionEditor` and `PhaseCustodyEditor`. Review is absent from Map-first.
- `MapFirstGroupToolbar` is the active Group/phase surface: Posts/Patrol,
  posture, legal travel behavior, recovery, Core custody, formation suggestion,
  routes/signatures, and selected-bot class context.
- `TacticsMapCanvas` remains the only geometry editor. Classic uses the same
  component and draft without Map-first's copy-on-write handles or selection
  chrome.
- `TacticsIcons` is a reusable inline-SVG registry with one stroke/proportion
  language, current-color states, tooltips, accessible labels, pressed,
  configured, and disabled states. A focused audition fixture renders the
  complete vocabulary together.

## Feature parity inventory

| Capability | Map-first route / status |
| --- | --- |
| Sheet load, local recovery, validation, save, Practice/compile | Unchanged shared page/query/document paths. |
| Squad composition, builds and upgrades | Existing Sheet rail and Squad modal. |
| Group create/delete/name/membership/capacity/rehoming | Full shared `GroupsStep` in the membership drawer; map-side trays reuse `followMembership`. |
| Tactic create/delete/reorder and summary/readback | Existing Sheet rail/cards and recipe flow. Review strings remain generated though Review is not Map-first navigation. |
| Start, abort, maximum duration/rearm | Compact global controls open the full shared Start/Abort editor. |
| Phase add/select/name/delete | Compact strip adds/selects; the phase details drawer renames/deletes. |
| Strict sequential transitions | One icon between each adjacent phase; it edits only that phase's existing `continue` boundary. |
| Required / Triggered by / Unless / alternatives | Full existing `CaseListEditor`, not a reduced menu. |
| All condition rows, counts/timers/Well/value controls | Shared condition rows and steppers/selectors. |
| One-to-three zones, select/redraw/remove | Shared watch tasks continue to draw on the real map. |
| Formation Ready/slack, cleared, not-fighting | Full shared condition editor in valid scopes. |
| Ordinary spatial authoring | Exactly Posts and Patrol in the compact toolbar. |
| Loaded advanced/legacy jobs | Saved kind shown explicitly; projections are labelled; optional Convert to posts is deliberate and never automatic. |
| Exact Posts, facings and per-post ways-in | Real sprites/posts; direct handles; selection translation/rotation; current and inherited per-post route controls. |
| Patrol route editing and approach routes | Real map task/pathfinder through the shared `JobSection`; inherited Patrol materializes before its first route edit. |
| Posture, travel and recovery | Compact Group-level icon controls; no per-bot runtime behavior or hidden Group split. |
| Per-class signature marks | Selected class context plus the shared capability-derived mark controls. |
| Core habits, delivery and per-phase custody | Compact Core entry opens shared delivery and custody/way-home controls. |
| Carry/effective assignment | Full-strength inherited geometry, `Carry on · <phase>`, source edit link, and copy-on-write spatial handles. |
| Classic fallback | Visible switch; same live wizard tactic/document/phase without copying or committing. |

## Copy-on-write and map selection

`materializeSpatialJob` is the sole new spatial boundary. A saved `none` job
continues to mean inheritance until the first current-phase spatial edit. That
edit deep-clones only the effective ground into the current phase while keeping
the current phase's posture, travel, recovery, and signatures. The source
phase is never mutated and untouched phases do not acquire eager JSON copies.

Map and tray selection use stable squad slots. A click selects one bot,
modifier-click adds/removes bots, and clicking a Group selects its formation.
Selected Posts translate as a rigid shape. The closest legal deterministic
delta must keep every moved Post in bounds, on a valid anchor/facing, distinct
from its selection peers, and clear of unselected Posts. Facing rotation is
all-or-nothing across the selected Posts. Cross-Group selections remain
visible, while the active Group is explicit and Group behavior controls never
pretend to be per-bot.

## Editor-only formation and reach overlays

`Suggest formation` produces ordinary slotted Posts at Mid or any of the three
Wells. Candidate ordering is deterministic, respects authoritative placement
rules and occupied Posts, prefers spacing, keeps slot identity, and chooses a
legal cardinal facing toward the objective. The result is immediately normal
editable Posts; no compiler/runtime concept survives.

Vision and attack reach use class-specific values projected from the current
authoritative actor rules into the Sheet catalog. Exact Post facing supplies
the directional facing-quadrant vision sector; omnidirectional proximity is a
separate ring. Attack reach is a separate amber class-range ring and is never
drawn as vision. A projected bot without an authored facing does not receive a
fabricated directional vision cone.

The existing seeded Area/Converge/Patrol previews remain presentation-only.
They use production sprites, authoritative map placement checks, and the real
pathfinder. Same-slot continuity lines are omitted when no legal path exists.

## Verification

Automated:

- `npx tsc -b`: passing.
- `npm test`: 611/611 passing.
- `npm run build`: passing, including six scoped CLI viewers, atlas variants,
  TypeScript, and the production Vite bundle. Only the repository's existing
  SignalR annotation and large-chunk warnings were emitted.
- Focused Map-first tests: 21/21 passing across projection, carry rendering,
  copy-on-write, selection translation/collision, selection rotation, smart
  formation, class reach, icons, layout sharing, full contextual editors, and
  legacy preservation.
- The App integration assembly builds with catalog range assertions. Its two
  PostgreSQL/Docker integration cases were skipped by this environment.

Controlled browser against the normal app host at `127.0.0.1:8080`:

- Blank Sheet/tactic: auto-filled eight real classes, distributed Group
  membership, confirmed all eight unplaced real sprite tokens, suggested Mid
  Posts, saw all eight authoritative map sprites, selected Vex plus Solder by
  modifier-click, and toggled two selected vision/attack layers.
- Added phase 2 from the strip and verified full-strength inherited Posts,
  `Carry on · Phase 1`, every carried bot title, and the direct source link.
- Opened the finish transition, added an alternative, and verified full
  Required and Triggered-by rows plus Unless and alternative controls.
- Classic then Map-first preserved the two phases and eight Posts in the same
  in-progress tactic.
- Existing `Test 1` revision 19 loaded its five phases/two Groups, exact Posts,
  custody run, carried Group, projected Converge bots, and configured condition
  counts. The Converge phase showed `Loaded converge` and `Convert to posts`
  without rewriting it. Its configured transition exposed Formation Ready
  slack and Core-soon Well/timer values in the full editor.
- Existing `Phase Pruning Acceptance` loaded its three-phase mixed Posts/Area
  sheet and truthful readback.
- At 820×900 the Sheet rail stacked above the editor, the phase strip, toolbar,
  trays, and map remained operable, and document width equalled viewport width
  (no horizontal overflow).
- Browser console: no warnings, React/hydration errors, or other errors.

## Honest remaining limits

- The browser-control drag gesture caused the inherited phase to materialize,
  but did not produce a measurable tile translation in that controlled run.
  Therefore precise pointer drag/rotation is **not claimed as browser-verified**
  here. The copy-on-write, legal rigid translation, collision, and rotation
  algorithms are covered by focused tests; a manual mouse pass should confirm
  feel before release.
- This pass did not create and later delete a server-side QA Sheet, so no new
  browser persistence round-trip is claimed. Save/readback uses the unchanged
  production mutation/API path and remains covered by the existing editor/App
  suites.
- Preview positions for non-Post jobs remain explicitly non-authoritative and
  non-draggable. Runtime execution still owns exact arrival tiles.
- Smart placement currently targets Mid and named Wells, not an arbitrary map
  click. This meets the minimum objective set and keeps the first algorithm
  deliberately small.
