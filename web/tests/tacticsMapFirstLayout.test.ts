import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';

const page = readFileSync(new URL('../src/site/tactics/TacticsEditorPage.tsx', import.meta.url), 'utf8');
const chrome = readFileSync(new URL('../src/site/tactics/MapFirstChrome.tsx', import.meta.url), 'utf8');
const steps = readFileSync(new URL('../src/site/tactics/WizardSteps.tsx', import.meta.url), 'utf8');

test('the map-first workspace is the sole editor over the real draft and wizard', () => {
  assert.match(page, /data-layout="map-first"/);
  assert.match(page, /<TacticsMapCanvas[\s\S]*document=\{document\}/);
  assert.match(page, /data-testid="map-first-context-drawer"/);
  assert.doesNotMatch(page, /layoutMode|layout-switch|>Classic</);
  assert.doesNotMatch(chrome, /onChange=\{\(cases\)/,
    'timeline chrome must not own a reduced/forked condition mutation path');
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
