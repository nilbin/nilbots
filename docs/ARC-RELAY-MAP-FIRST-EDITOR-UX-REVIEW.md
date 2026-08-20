# Arc Relay Map-first Tactics Editor: Design and UX Review

Date: 2026-08-21  
Reviewed build: `903ac717` on `main`  
Review surface: live Docker build at `127.0.0.1:8080`, using the existing `Test 1` sheet

## Verdict

Map-first is the correct editor and should become the only player-facing mode. The phase timeline, direct spatial editing, group-colored map overlays, behavior rail, and bot-specific inspector now form a coherent product idea. This is no longer a case for another full rewrite.

It is not yet ready as a player's first encounter, however. The largest remaining problems are structural rather than cosmetic:

1. entering tactic editing leaves the entire sheet overview visible, duplicating bots and compressing the map;
2. every recipe begins with zero assigned bots, even when its group sizes already specify the intended allocation;
3. several recipes create legacy `area`/`converge` jobs that the chosen editor cannot author normally;
4. map-first mutations do not have a dependable, visible undo model, and “Redraw” currently appends to existing routes;
5. condition editing exposes nearly the full underlying data model at once.

Fix those before adding more editor features. Afterward, the editor should be suitable for a serious hands-on playability test.

## What already works

- The map is finally the conceptual center of tactic authoring.
- The timeline reads naturally as `phase → condition → phase`.
- Group colors connect tray, map geometry, and behavior controls effectively.
- Selecting one bot immediately produces a focused route/signature inspector.
- Posts, facing arrows, routes, Wells, reactors, and watched areas are distinguishable on the map.
- The three behavior questions—at destination, on the way, recovery—are a sensible player-level vocabulary.
- Draw mode gives immediate instructions, progress, Undo-point, and Done controls over the map.
- Drawers close by clicking outside or pressing Escape.
- There was no page-wide horizontal overflow at the tested 1024 px and 820 px widths.
- The new icon set is visually consistent and has accessible labels/tooltips in the markup.

These are the foundation to preserve.

## Evidence from the live review

| Observation | Evidence | Why it matters |
|---|---|---|
| The editing workspace is crowded by sheet-level UI | At 1728×962, the full 260 px Sheet rail, tactic card, and full squad remain visible beside a second bot list in Groups. | Bots and tactics are represented twice while the map—the actual editor—loses width. A disabled Sheet Save remains visible beside the tactic Save action. |
| The narrow entry flow hides the editor | At 820×900, immediately after opening `Centre Rush`, the map began about 1044 px below the viewport and the document was about 2254 px tall. The page did not move to the tactic workspace. | A player clicks Edit and appears to remain on the sheet overview. This is a functional discoverability failure, not merely a mobile concern. |
| A blank tactic is not actually ready to author | Choosing Blank Tactic opened Group membership with `0/8` assigned and three blockers. The player had to find and press `Distribute the rest` before any bot could be placed. | The most important “start from blank” path begins with bookkeeping that the system can resolve correctly itself. |
| All recipe group allocation is deferred to the player | `instantiateRecipe` creates every group with `slots: []` even though recipe groups declare sizes such as `8`, `2`, and `6`. | Every template begins with unnecessary membership work, including single-group templates. |
| Some new templates generate “legacy” map states | `Centre Rush`, `Pincer`, and `Core Raid` recipes still contain `area` and/or `converge`, while map-first only offers Posts and Patrol as normal authoring modes. | The sole editor must not create fresh tactics in states it presents as “Loaded advanced” and asks the player to convert. |
| The map-first timeline can conceal phases | At 1024×768, the fifth phase was partially cut off in the horizontally scrollable timeline with no edge fade or scroll affordance. | A player can miss later phases or the finish condition. |
| Phase details use a hidden second-click gesture | Clicking a different phase selects it; clicking the already-active phase again opens rename/delete. Only the tooltip describes this. | Renaming or deleting a phase is needlessly undiscoverable. |
| Drawers require two clicks to change context | With the Vex drawer open, the first click on a transition only closed the backdrop; the second click opened the transition editor. | The outside-click behavior is correct, but explicit context switching feels broken when the intended target does not activate. |
| The transition editor is much denser than the map editor | A single transition drawer repeated the whole Enemies and Cores catalog under both “Required” and “Triggered by”, with further controls below the fold. | Players must parse the condition schema rather than state their tactical intention. |
| Signature ownership is visually contradictory | Selecting Vex opened a seemingly bot-specific Vector Dash panel. Starting mark placement then said `Placing Rush Party’s signature marks` and showed `0/8`; the data is actually group-by-class. | A player cannot tell whether marks affect Vex, both Kestrels, or the entire group. |
| “Redraw” does not redraw | `GeoStatusRow` labels an existing route action `Redraw`, but starting the task does not clear or snapshot the route; new clicks append to `via`/`path`. | A normal player action silently creates a malformed longer route. |
| Direct manipulation lacks a trustworthy undo path | Wizard edits use `setWizard`/`changeWizard`, while global undo operates on the sheet draft. Draw mode can pop the last point, but whole-group drops, post moves, facing changes, behavior changes, and membership changes have no visible tactic-edit undo. | A map editor needs experimentation without fear. Canceling the entire tactic edit is not a substitute for undo. |
| Bot stats are too cryptic | The inspector showed `4 attack`, `7 vision`, `0 nearby`. | Values lack “tiles”, and “nearby” does not explain proximity vision. The repeated bot name in the drawer header and card adds noise. |
| Several icon-only actions are semantically risky | The Start configuration uses a Play triangle, although Practice is the actual run action. A trash can clears selection rather than deleting. | Tooltips help after discovery, but the initial meaning is misleading. |

## Priority 0: fix before the next player test

### 1. Make Map-first the only player-facing editor

Remove the Map-first/Classic switch and all routes that expose Classic to players. Open existing and new sheets directly in Map-first.

Do not delete shared internals simply because Classic disappears. `StartStep`, `GroupsStep`, `TransitionEditor`, `CoresStep`, validation, serialization, and legacy-job loading are still used by map-first drawers or data compatibility. Remove the parallel layout and wizard navigation surface, not the proven compiler/runtime model underneath it.

Acceptance:

- no “Map-first” or “Classic” terminology appears in the player UI;
- a sheet has one Edit action and one editor;
- existing documents still round-trip without silent semantic changes;
- tests target this as the canonical editor.

### 2. Enter a focused tactic workspace

When a tactic is being edited, replace the full left Sheet rail with a compact workspace header:

- Back to Sheet;
- tactic name;
- unsaved state;
- Done editing;
- Practice when valid.

Hide the tactic card list, full squad picker, sheet-name field, ladder checkbox, draft notice, and Sheet Save while editing. Keep a small Squad/Edit membership entry where it is contextually useful.

This gives the map, groups, timeline, and behavior rail the viewport. It also removes the current duplicate presentation of all eight bots.

At widths below the two-column breakpoint, opening a tactic must scroll/focus the tactic workspace rather than leaving it below the sheet overview.

### 3. Auto-assign recipe membership

Recipe selection already states intended group sizes. Use them.

- One group of eight: assign all eight immediately.
- Two groups of 2/6: assign the bots deterministically and let the player refine later.
- Null/flexible sizes: distribute the remainder predictably.
- If the squad is incomplete, guide the player to complete the squad first rather than opening an empty tactic.

Do not automatically open Group membership after recipe selection when the allocation is complete. Open directly on the first phase and emphasize the first spatial action. Membership remains available as an explicit edit.

The Blank Tactic path should be: choose Blank → all eight are in Everyone → drag Everyone onto the map.

### 4. Make every visible recipe native to the sole editor

Do not let map-first create fresh `area`, `converge`, or other “Loaded advanced” assignments.

Rewrite template output as editable Posts/Patrol geometry plus ordinary phase transitions. Recipe geometry may be generated as suggested posts, but the resulting posts must be ordinary, movable data—not a hidden recipe behavior.

For old sheets with legacy jobs:

- preserve behavior until the player chooses migration;
- show a single explicit migration card with a before/after explanation;
- never silently convert on load;
- do not call freshly-created states “legacy” or “advanced”.

### 5. Provide real tactic-edit undo and correct Redraw

Add one visible Undo/Redo pair for map-first editing and keyboard shortcuts backed by a wizard-local snapshot history. A complete gesture—dragging a group, moving a post, changing facing, editing a route—should create one undo step.

Keep point-level Undo while actively drawing, but make it part of the same understandable model.

`Redraw` must replace the existing geometry. Safe options:

1. snapshot old geometry, clear it when draw mode begins, and restore it if drawing is canceled; or
2. call the action `Edit route` and expose existing waypoints as movable handles.

Do not label an append operation `Redraw`.

## Priority 1: make the editor understandable rather than merely capable

### 6. Replace condition catalogs with an authored sentence

Keep the current condition model and executor. Change only how it is authored.

The drawer should lead with one plain relationship:

> Continue when any trigger happens, provided every requirement is true.

Then show only configured chips/rows, for example:

> When **a Core is taken this phase**, and **the Mid area is verified clear** → continue.

Below that, offer:

- `+ Add requirement`
- `+ Add trigger`
- `+ Unless`
- `+ Alternative`

Each opens a categorized picker. Do not render the complete Enemies and Cores catalog twice by default. Preserve the current sentence readback and make it sticky at the top of the drawer.

Spatial conditions should use a labeled action such as `Draw area` / `Add another area`, not an opaque symbol. Keep the up-to-three-zone support.

### 7. Make drawers targeted and allow one-click context switching

- Start opens Start conditions only.
- Abort opens Abort conditions only.
- Phase transition opens only that transition.
- Bot opens identity, ranges, route, and its relevant signature controls.
- Core plan can keep tactic delivery and current-phase custody together, but collapse the inactive section.

Clicking another explicit editor target while a drawer is open should switch directly to that target in one click. An outside click on empty space should still close it.

Remove duplicated headings in the bot drawer. Prefer `Vex · Kestrel` once, followed by `Vector Dash`.

### 8. Clarify signature scope and default-vs-authored behavior

The UI must reflect the real owner of a signature rule. If all Kestrels in Rush Party share the rule, label it:

> Kestrels in Rush Party · Vector Dash

Do not present group-by-class marks as Vex-only settings merely because Vex opened the inspector.

Use one simple policy vocabulary consistently across signatures:

- **Automatic** — the bot uses the skill when the game-level default judges it useful;
- **Prefer in marked ground** — normal use remains possible, but marks steer it;
- **Only in marked ground** — marks are a hard player restriction.

Only offer the choices that the skill can honestly support. Default to Automatic. Marking a route, zone, anchor, or facing should remain map-driven.

This recommendation defines the editor contract; class-by-class defaults belong in the separate signature inventory and balance analysis.

### 9. Make the timeline's hidden actions visible

- Phase click always selects the phase.
- Add a small visible phase menu or edit icon for rename/delete.
- Add left/right edge fades or arrows when the timeline can scroll.
- Keep full phase names available on hover/focus.
- Make the final transition visually say `Finish`, not only show the same arrow glyph.

The configured green dot is useful and should stay, but it needs a one-time legend or a tooltip that describes “condition configured”.

### 10. Improve map and selection affordances

- Give the group header a distinct drag handle and a short first-use callout over the map: `Drag the group here to place its formation`.
- Once the first group is dropped, retire the callout.
- Hide the repeated `ON MAP` state; only show exceptional states such as `Drag to map`, `Projected`, or `No preview`.
- Keep direct post dragging and facing-arrow rotation.
- Change the clear-selection trash icon to a deselect icon. Trash should always mean deletion.
- Group overlay toggles, selected-bot range toggles, and zoom into three visually separated clusters.
- If a range toggle is disabled, its tooltip should say `Select a bot first`.

### 11. Clarify save semantics

The current tactic Save only commits the tactic into the local sheet draft; the Sheet Save persists it. In the focused workspace, call the tactic action **Done** or **Apply to Sheet**, and reserve **Save Sheet** for persistence.

Show one compact state near the workspace title:

- Editing tactic;
- Unsaved sheet changes;
- Saved.

Do not show a disabled Sheet Save beside an active tactic Save.

## Priority 2: visual polish

### 12. Increase readable density, not raw density

The current visual language is cohesive, but too much explanatory text is 9–10 px and low-contrast. Keep compact labels, but use roughly 12 px for actionable labels and descriptions and reserve micro text for metadata.

Reduce the “border around every row” effect by using:

- surface separation for major regions;
- dividers within a region;
- borders for interactive or selected states.

Selected group, selected behavior, configured transition, and disabled action should not all rely on subtly different border colors.

### 13. Tighten the behavior rail

The behavior rail is the right abstraction and should remain visible beside the map on a normal desktop.

- Keep all option labels visible.
- Keep only the selected option's description expanded, as today.
- Increase selected description readability.
- Rename `Respond to contact` if testing confirms the exact behavior; player copy must describe when pursuit ends and travel resumes.
- Make the selected group's color/title more prominent so players always know whose behavior they are changing.

### 14. Improve bot readback

Use explicit units and terms:

- `Attack reach · 4 tiles`
- `Vision · 7 tiles`
- `Proximity vision · 0 tiles` or omit it when zero

Keep vision and attack overlays off by default and selected-bot only. The current circle for attack reach and facing cone plus proximity circle for vision are appropriate visual models.

## Recommended player flow

1. **Sheet overview:** pick or auto-fill eight bots; see the ordered tactic list and Practice.
2. **New Tactic:** choose a template or Blank. Templates describe intended movement/outcome, not implementation vocabulary.
3. **Immediate map workspace:** recipe membership is already allocated; the first group and first phase are selected.
4. **Place:** drag the group to create ordinary posts, or choose Patrol and draw its route. Move bots and facing directly on the map.
5. **Behave:** choose destination, travel, and recovery behavior in the right rail.
6. **Specialize:** select a bot to inspect range and configure its class signature only if desired.
7. **Sequence:** add/select phases across the top; inherited positions remain visible and directly editable.
8. **Release/finish:** click the transition between phases and author a short condition sentence.
9. **Done:** return to Sheet, then Practice; adjust from observed outcomes.

This is a loop, not a wizard. The player can move between steps freely, but the empty state always makes the next useful map action obvious.

## What should explicitly not be added

- No Classic/player-facing expert mode.
- No second Play/Order/Instruction editor beside Tactics.
- No raw JSON or generated-instruction editor in the normal product.
- No new runtime scheduler or editor-only execution semantics.
- No return to a five-step form wizard.
- No permanently expanded sidebar containing every tactic property.
- No more normal spatial modes beyond Posts and Patrol unless playtesting proves a distinct player intention cannot be expressed.
- No mandatory skill micromanagement; map marks refine strong automatic defaults.
- No confirmation dialog after every map gesture. Use undo.
- No hover-only access to essential actions such as phase rename/delete.
- No silent legacy-job conversion.
- No more templates until every existing template opens in a complete, editable, map-first-native state.

## Suggested implementation order

1. Remove the Classic switch and introduce focused tactic-workspace layout.
2. Auto-assign recipe groups and update every recipe to Posts/Patrol-native output.
3. Add wizard-local undo/redo and fix route/signature Redraw semantics.
4. Make blank/template onboarding land directly on the first map action.
5. Refactor drawers into targeted surfaces and enable one-click context switching.
6. Replace expanded condition catalogs with configured rows plus Add pickers.
7. Clarify signature scope/policy after the signature inventory defines each class's honest defaults.
8. Apply typography, icon-semantic, timeline-overflow, and visual hierarchy polish.
9. Run a five-person first-use test with one task: create a two-phase pincer from blank, practice it, and make one adjustment from the replay.

## Acceptance test for the next UX pass

A player unfamiliar with the schema should be able to:

1. open a blank sheet and field eight bots;
2. create a Blank tactic without manually assigning Everyone;
3. drag all eight to Mid, adjust two exact posts and facing, and choose Hold;
4. add a second phase, carry the formation forward, move a subgroup, and choose Aggressive;
5. set `Mid verified clear` plus `no friendly fighting in Mid` as the transition;
6. select a Kestrel, understand whether Vector Dash is automatic or mark-restricted, and optionally mark a corridor;
7. undo a mistaken group move;
8. finish editing, save the sheet, and start Practice.

Target: no Classic mode, no raw structure, no unexplained “Loaded advanced” state, no two-click context switching, and no need to discover `Distribute the rest` before the map becomes usable.
