# Map-first Tactics Editor — parity and verification report

Date: 2026-08-20

## Outcome

The production Tactics editor now opens in a Map-first layout. This is a new
view over the existing `TacticsDocument`, wizard state, map tasks, validation,
history, save/trial APIs, and compiler-facing serialization. A visible Classic
switch retains the previous arrangement for comparison. Switching views does
not commit, normalize, or copy the current draft.

Map-first adds a whole-tactic/phase timeline above the real tactical map, Group
trays alongside it, and stable presentation-only bot position previews. It does
not introduce a second document, condition model, state machine, materializer,
or persistence path.

## Feature parity inventory

| Current editor capability | Map-first status | Evidence / route |
| --- | --- | --- |
| Load new and existing Sheets | Retained | The page still bootstraps `useTacticalSheet`, local draft recovery, and catalog normalization before either layout renders. |
| Draft mutation, gesture history, undo/redo | Retained | Both layouts share one `useSheetDraft` instance and one wizard. Map geometry still enters the established gesture/mutation path. |
| Validation and issue navigation | Retained | The shared `validateDocument` result and compiler issue mapping remain in the Sheet rail. |
| Save, server readback, summary, Practice/compile | Retained | The existing save/trial hooks, `stringifyDocument`, summary, review step, and Practice action are unchanged and outside the layout branch. |
| Squad slots, class/build selection | Retained | The existing Squad rail/modal remains visible in Map-first. Group trays use those authoritative slots and production class sprites. |
| Group create/delete/name/membership/capacity | Retained | `GroupsStep` remains the full editor. Trays link to it and also support Group-to-Group drag using the existing slot-keyed `followMembership` migration. |
| Membership rehoming of Posts, via and marks | Retained | Tray moves call `followMembership`; focused regression tests cover split, two-step move, cleanup, swaps, and normalization. |
| Tactic create/delete/reorder | Retained | The existing Sheet rail and recipe flow remain unchanged. |
| Start, Tactic-wide abort, maximum duration/rearm | Retained | Start/Core/Abort sit above the phase timeline and open the existing `StartStep`/`CoresStep`, including all cases and duration/rearm controls. |
| Phase add/delete/name/select | Retained | Timeline phase chips select the same wizard phase. The complete `PhasesStep` remains the contextual inspector. |
| Strict sequential transitions | Retained | Each boundary chip edits that phase's existing `continue` cases. No phase target or branching state was added. |
| Normal assignments | Retained | Take up Posts, occupy Area, Converge, and Patrol remain directly selectable in the inspector. |
| Loaded advanced assignments | Retained honestly | Existing `line`, `approach`, `return`, `escort`, and other non-normal content retains its warning/readout and saved behavior until explicitly replaced. Unsupported preview kinds show “No position preview”; no geometry is invented. |
| Posture, travel, recovery | Retained | The existing segmented controls stay in every Group/phase inspector section, including honest advanced-value warnings. |
| Exact Posts, facing, per-bot via | Retained and extended | Authored Posts remain authoritative, selectable, draggable, swappable and rotatable. Unposted tray bots can be dropped onto the map to create a real slotted Post. Existing Redraw/Clear and route tasks remain. |
| Area/point/patrol geometry | Retained | All authoring and direct map manipulation still uses `TacticsMapCanvas` and the existing map-task callbacks. |
| Per-class signature marks | Retained | Capability-derived marks, counts, map draw/redraw/clear, and phase-local semantics remain in `JobSection`. |
| Core habits and delivery/custody | Retained | Group Core habits, tactic delivery plan and phase custody/way-home controls remain in the existing Groups/Cores/Phases steps. |
| Complete condition rows | Retained | Start, continue, final-phase finish, and abort all route to the existing `CaseListEditor`/`ConditionRows`; no abbreviated menu exists. |
| Required / Triggered by / Unless / alternatives | Retained | Full AND/OR/inversion/case semantics, folding, alternative creation and maximum case counts remain unchanged. |
| Counts, timers, Well/value controls | Retained | Existing steppers and selectors are rendered inline beside their condition toggle. Their markup was corrected so parameter controls are no longer nested buttons. |
| One-to-three condition zones | Retained | Select/draw/redraw/remove for every zone still arms the real map watch task. |
| Formation Ready/slack | Retained | The complete Formation Ready row and Everyone/All-but-one/All-but-two slack selector remain available. |
| Cleared and not-fighting semantics | Retained | Both complete rows and their independently drawn zone unions remain available in valid scopes. |
| Carry-forward/effective assignment | Retained and made clearer | Carried ground is full-strength in the active phase, labelled `Carried from <phase> · edit source`; source navigation is direct. Inherited geometry does not receive current-phase drag handles. Current posture/recovery/travel/signatures remain phase-local. |

There is no separate tolerance control in the current post-alpha player
vocabulary; Map-first preserves the current editor rather than resurrecting a
removed control.

## Map-first architecture

- `MapFirstTimeline` and `MapFirstGroupTrays` are view/navigation chrome. They
  receive the live document/wizard and emit the same editor actions; they do
  not own authored state.
- `TacticsMapCanvas` remains the single geometry editor. Map-first supplies
  contextual selection, active-only phase focus, preview rendering, and exact
  Post drops as optional props. Classic keeps its former dim neighbouring
  phases.
- The existing inspector components (`StartStep`, `GroupsStep`, `PhasesStep`,
  `CoresStep`, `ReviewStep`) are reused. Timeline, transition, Group, bot, Post,
  start, abort and Core selections route to those controls and their anchors.
- At narrower desktop widths the three columns stack without replacing the
  editor with a phone-only variant. Map draw actions keep the existing
  narrow-layout scroll-to-map behavior.

## Position preview contract

`projectPhaseBots` is pure presentation logic. Its output never enters
`TacticsDocument`, serialization, validation, compilation, or runtime orders.

- The seed includes map identity, tactic, phase, Group, slot, and a ground
  fingerprint. A rerender or unrelated behavior change cannot move a bot.
- Area uses distinct legal tiles inside the authored rectangle with seeded
  spacing and deterministic capacity fallback.
- Converge/Approach uses distinct legal tiles around the authored point,
  expanding deterministically when necessary.
- Patrol/Advance/Line resolve the production tile path between waypoints and
  distribute bots along that legal route. Authored waypoint straight segments
  are never treated as traversable paths.
- Exact Posts use their authored slot/tile/facing and are not marked as
  previews.
- Carry-forward reuses the source phase's bot tile when the inherited ground
  fingerprint is unchanged.
- Advanced/unresolvable content produces an explicit no-preview indication
  instead of an illegal or fictional position.
- Every visible bot uses the authoritative production `classSprite`. Preview
  positions carry a dashed ring and `P` marker and are not draggable.
- Same-slot continuity lines use `makePathfinder().findPath`. When no legal
  path exists there is no straight-line fallback through a wall.

## Verification

Automated:

- `npm run harness` plus focused projection/layout/document/carry suites:
  46/46 passing.
- `npx tsc -b --pretty false`: passing.
- `npm test`: 600/600 passing.
- `npm run build`: passing, including all six scoped CLI viewers, generated
  atlas variants, TypeScript, and the production Vite bundle. Rollup emitted
  only the repository's existing SignalR annotation and chunk-size warnings.
- No shared .NET contracts or compiler code changed, so focused .NET compiler
  tests were not required for this UI-only change.

Focused tests cover deterministic repeat, relevant versus unrelated changes,
distinct/walkable tiles, carry retention, wall-aware patrol routing, production
sprite rendering, shared layout state, full-editor navigation, carried source
editing, exact Post drop wiring, and Group membership migration.

Controlled-browser review used the running production page and real API:

- Blank new Sheet: filled eight squad slots, created a Hold Area tactic,
  distributed Group membership, selected exact Posts, and confirmed all eight
  real sprite tokens were available without duplicate map bots.
- Existing complex Sheet `Test 1` (revision 19): opened a five-phase tactic
  containing exact Posts, per-bot ways in, Converge, carry-forward, signature
  marks, Core custody, Formation Ready, clearance and Core conditions. Verified
  active projected sprites and legal continuity paths, exact Post map state,
  carried source links, and the full existing transition editor.
- Timeline Start, Abort, phase and transition routing opened the correct shared
  inspector. Classic → Map-first preserved the live wizard and selected phase.
- The full transition inspector exposed Required, Triggered by, Unless,
  Formation Ready/slack, enemy/contact/down/clear/not-fighting, Core
  available/soon/taken, alternative cases, steppers/selectors, and map zones.
- At approximately 820 px the workspace stacked and remained operable.
- A fresh console pass showed no React/hydration/accessibility errors. The only
  local console errors were the known SignalR negotiation 404s caused by this
  ad-hoc Vite proxy not forwarding the live hub; they are unrelated to the
  editor code and do not occur through the normal app host.

## Known limits (truthful, non-parity-breaking)

- Area, Converge, Patrol, Approach and Line bot tiles are explicitly previews;
  runtime execution still owns exact arrival choice. Preview bots cannot be
  dragged into invented runtime semantics.
- The contextual inspector intentionally reuses the full existing wizard
  sections. Complex phases remain dense; Map-first improves selection and
  proximity without splitting their business logic into a second UI model.
- The Map-first default shows only the active phase to protect map legibility.
  `Show every phase` remains available; Classic retains its dim prior/next
  context.
- Exact Post tray placement relies on desktop HTML drag-and-drop. The existing
  `Draw on map` flow remains the keyboard/touch-compatible authoritative path.
