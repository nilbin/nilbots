import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';

const page = readFileSync(new URL('../src/site/tactics/TacticsEditorPage.tsx', import.meta.url), 'utf8');
const chrome = readFileSync(new URL('../src/site/tactics/MapFirstChrome.tsx', import.meta.url), 'utf8');
const steps = readFileSync(new URL('../src/site/tactics/WizardSteps.tsx', import.meta.url), 'utf8');

test('Map-first and Classic are views over the same draft and wizard', () => {
  assert.match(page, /useState<'map-first' \| 'classic'>\('map-first'\)/);
  assert.match(page, /data-layout=\{layoutMode\}/);
  assert.match(page, /<TacticsMapCanvas[\s\S]*document=\{document\}/);
  assert.match(page, /<PhasesStep \{\.\.\.stepProps\} \/>/,
    'Map-first keeps the existing full phases editor instead of a parallel form');
  assert.doesNotMatch(chrome, /onChange=\{\(cases\)/,
    'timeline chrome must not own a reduced/forked condition mutation path');
});

test('start, abort and every sequential transition route to full existing controls', () => {
  assert.match(chrome, /map-first-start/);
  assert.match(chrome, /map-first-abort/);
  assert.match(chrome, /map-first-transition-/);
  assert.match(page, /focus\.kind === 'transition'[\s\S]*transition-controls-/);
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
  assert.match(page, /job\?\.kind !== 'posts'/);
  assert.match(page, /target\.posts\.push\(\{ at: \[x, y\], facing, slot \}\)/);
  assert.match(page, /followMembership\(tactic, document\.squad, before, new Map\(\)\)/);
});
