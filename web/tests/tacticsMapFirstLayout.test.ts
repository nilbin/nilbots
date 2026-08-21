import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';

const page = readFileSync(new URL('../src/site/tactics/TacticsEditorPage.tsx', import.meta.url), 'utf8');
const chrome = readFileSync(new URL('../src/site/tactics/MapFirstChrome.tsx', import.meta.url), 'utf8');
const steps = readFileSync(new URL('../src/site/tactics/WizardSteps.tsx', import.meta.url), 'utf8');
const conditions = readFileSync(new URL('../src/site/tactics/ConditionRows.tsx', import.meta.url), 'utf8');

test('the map-first workspace is the sole editor over the real draft and wizard', () => {
  assert.match(page, /data-layout="map-first"/);
  assert.match(page, /<TacticsMapCanvas[\s\S]*document=\{document\}/);
  assert.match(page, /data-testid="map-first-context-drawer"/);
  assert.doesNotMatch(page, /layoutMode|layout-switch|>Classic</);
  assert.doesNotMatch(chrome, /onChange=\{\(cases\)/,
    'timeline chrome must not own a reduced/forked condition mutation path');
});

test('tactic editing replaces the Sheet overview with one focused workspace', () => {
  assert.match(page, /!wizard && <section[\s\S]*data-testid="sheet-overview"/);
  assert.match(page, /data-testid="tactic-workspace"/);
  assert.match(page, /tacticWorkspaceRef\.current\?\.scrollIntoView/);
  assert.match(chrome, /← Back to Sheet/);
  assert.match(chrome, /Unsaved tactic changes/);
  assert.match(chrome, /data-testid="tactic-edit-squad"/);
  assert.match(chrome, /data-testid="map-first-commit">Done/);
  assert.doesNotMatch(chrome, /Save tactic/);
});

test('a fully allocated recipe opens on its phase while incomplete membership remains guided', () => {
  assert.match(page, /const membershipComplete = document\.squad\.length === 8/);
  assert.match(page, /!editing && !membershipComplete \? \{ kind: 'groups' \} : null/);
});

test('start, abort and every sequential transition route to full existing controls', () => {
  assert.match(chrome, /map-first-start/);
  assert.match(chrome, /map-first-abort/);
  assert.match(chrome, /map-first-transition-/);
  assert.match(page, /mapFirstPanel\.kind === 'transition'[\s\S]*<TransitionEditor/);
  assert.match(steps, /id="start-controls"/);
  assert.match(steps, /id="abort-controls"/);
  assert.match(steps, /id=\{`transition-controls-\$\{phase\.id\}`\}/);
  assert.match(steps, /<CaseListEditor<TacticTransition>/);
  assert.match(steps, /requiredKinds=\{\[\.\.\.TRANSITION_ROWS\]\}/);
  assert.match(steps, /triggerKinds=\{noEveryoneRows\(TRANSITION_ROWS\)\}/);
  assert.match(steps, /unlessKinds=\{noReadinessRows\(TRANSITION_ROWS\)\}/);
});

test('group trays use real sprites, truthful carry source and authoritative Post drops', () => {
  assert.match(chrome, /classSprite\(bot\.class\)/);
  assert.match(chrome, /Carried from \{tactic\.phases\[resolution\.sourcePhaseIndex\]\?\.name\}/);
  assert.match(chrome, /application\/x-nilbots-post/);
  assert.match(page, /effectiveJobResolution\(wizard\.tactic, phaseIndex, groupId\)/);
  assert.match(page, /target\.posts\.push\(\{ at: \[x, y\], facing, slot \}\)/);
  assert.match(page, /followMembership\(tactic, document\.squad, before, new Map\(\)\)/);
});

test('ordinary Map-first authoring exposes only Posts and Patrol while preserving legacy jobs', () => {
  assert.match(chrome, /effective\.kind === 'posts' \|\| effective\.kind === 'patrol'/);
  assert.match(chrome, /Loaded \{effective\.kind\}/);
  assert.match(chrome, /Convert to posts/);
  assert.match(chrome, /projectedPostsForLegacyJob/);
  assert.doesNotMatch(chrome, /label="Area"/);
  assert.doesNotMatch(chrome, /label="Converge"/);
});

test('Map-first contextual editors reuse full tactic controls and keep bot focus compact', () => {
  assert.match(page, /<StartStep \{\.\.\.stepProps\} \/>/);
  assert.match(page, /<GroupsStep \{\.\.\.stepProps\} \/>/);
  assert.match(page, /<CoresStep \{\.\.\.stepProps\} \/>/);
  assert.match(page, /<PhaseCustodyEditor \{\.\.\.stepProps\}/);
  assert.match(page, /<MapFirstBotPanel/);
  assert.match(chrome, /data-testid="map-first-bot-panel"/);
  assert.match(chrome, /Draw approach route/);
  assert.match(chrome, /Place signature marks/);
  assert.doesNotMatch(page, /Bot and Group details/);
  const mapFirstDrawer = page.slice(page.indexOf('data-testid="map-first-context-drawer"'));
  assert.doesNotMatch(mapFirstDrawer, /<ReviewStep/);
});

test('map-first placement and behavior are driven beside and directly on the map', () => {
  assert.match(chrome, /application\/x-nilbots-group/);
  assert.match(page, /onGroupDropped=\{dropWizardGroup\}/);
  assert.match(page, /translatePostSelection/);
  assert.match(page, /suggestFormationPosts/);
  assert.match(page, /<MapFirstBehaviorPanel/);
  assert.match(chrome, /data-testid="map-first-behavior-panel"/);
  assert.doesNotMatch(chrome, /Suggest explicit posts/);
  assert.doesNotMatch(page, /Tapline — your side is west/);
});

test('the map and Behavior form one workbench with explicit phase actions', () => {
  assert.match(page, /The map and Behavior are one visual workbench/);
  assert.match(page, /min-\[1240px\]:w-\[232px\]/);
  assert.match(chrome, /onSelectPhase\(index\)/);
  assert.match(chrome, /label=\{`Edit \$\{phase\.name\}`\}/);
  assert.match(chrome, /<TacticsIcon name="finish"[^>]*\/> Finish/);
  assert.match(chrome, /Earlier phases/);
  assert.match(chrome, /Later phases/);
});

test('context changes take one click and tactic editing owns local undo', () => {
  assert.match(page, /closeOnOutsidePointer/);
  assert.doesNotMatch(page, /aria-label="Close context editor"[\s\S]*fixed inset-0/);
  assert.match(page, /wizardUndoStack/);
  assert.match(page, /beginWizardGesture/);
  assert.match(page, /finishWizardGesture/);
  assert.match(page, /onCancel=\{cancelTask\}/);
});

test('condition authoring shows configured rows before its categorized picker', () => {
  assert.match(conditions, /No conditions configured on this side\./);
  assert.match(conditions, /＋ Add condition/);
  assert.match(conditions, /This fires when/);
  assert.match(conditions, /every requirement/);
});
