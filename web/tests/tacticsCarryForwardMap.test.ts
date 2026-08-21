import assert from 'node:assert/strict';
import test from 'node:test';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import type { TacticalSheetCatalog } from '../src/site/api.ts';
import { makePathfinder } from '../src/site/tactics/pathing.ts';
import type {
  Tactic, TacticGroup, TacticJob,
} from '../src/site/tactics/tacticsDocument.ts';
import { JobOverlay } from './.harness/harness.entry.js';

const rows = Array.from({ length: 27 }, (_, y) =>
  Array.from({ length: 31 }, (_, x) =>
    x === 0 || y === 0 || x === 30 || y === 26 ? '#' : '.').join(''));

const catalog = {
  map: {
    id: 'arc-relay-tapline-03', width: 31, height: 27,
    tileRows: rows, regions: [], spawnAnchors: [], tileTags: [],
  },
  classes: [{
    id: 'mason', name: 'Mason', signatureName: 'Hardlight',
    signatureAuthoring: {
      allowsExactAnchor: true, allowsRouteForward: false,
      guidanceGeometry: 'points', guidanceMarkLimit: 2,
    },
  }],
} as unknown as TacticalSheetCatalog;

const group: TacticGroup = {
  id: 'guard', name: 'Guard', slots: [0], coreRule: 'ignore',
};

function render(source: TacticJob, current: TacticJob = {
  kind: 'none', posture: 'aggressive', recovery: 'heal-up',
}): string {
  const tactic = {
    id: 'truth', name: 'Truth', recipe: 'blank',
    start: { mode: 'always' }, delivery: { plan: 'none' },
    groups: [group], phases: [
      { id: 'setup', name: 'Setup', jobs: { guard: source } },
      { id: 'hold', name: 'Hold', jobs: { guard: current } },
    ],
  } as Tactic;
  return renderToStaticMarkup(createElement(JobOverlay, {
    tactic, group, phaseIndex: 1,
    bots: [{ slot: 0, class: 'mason', build: 'balanced', name: 'Forge' }],
    catalog, dim: false, editable: true,
    startDrag: () => {}, pathfinder: makePathfinder(catalog),
  }));
}

test('carry-forward visibly renders every authored ground family and via', () => {
  const posts = render({
    kind: 'posts', posture: 'hold', recovery: 'fight-on',
    posts: [{ at: [5, 5], facing: 'e', slot: 0 }],
  });
  assert.match(posts, /carried from Setup/);
  assert.match(posts, /data-select="truth:post:guard:1:0"/);
  assert.doesNotMatch(posts, /fill-opacity="0\.001"/,
    'inherited posts and facings have no current-phase drag handles');

  const area = render({
    kind: 'area', posture: 'hold', recovery: 'fight-on', rect: [5, 5, 8, 8],
  });
  assert.match(area, /<rect x="5" y="5" width="4" height="4"/);
  assert.doesNotMatch(area, /fill-opacity="0\.001"/);

  const converge = render({
    kind: 'converge', posture: 'hold', recovery: 'fight-on', point: [8, 8],
  });
  assert.match(converge, /data-testid="converge-fan"/);
  assert.doesNotMatch(converge, /fill-opacity="0\.001"/);

  const patrol = render({
    kind: 'patrol', posture: 'hold', recovery: 'fight-on',
    path: [[5, 5], [8, 8], [10, 5]],
  });
  assert.match(patrol, /↻/);
  assert.doesNotMatch(patrol, /fill-opacity="0\.001"/);

  const via = render({
    kind: 'area', posture: 'hold', recovery: 'fight-on', rect: [8, 8, 10, 10],
    via: [[3, 5], [5, 7], [8, 8]],
  });
  assert.match(via, /stroke-dasharray="\.12 \.26"/);
});

test('carry-forward renders only current-phase signature marks and keeps them editable', () => {
  const html = render({
    kind: 'area', posture: 'hold', recovery: 'fight-on', rect: [5, 5, 8, 8],
    sigs: { '0': { policy: 'only', points: [[3, 3]] } },
  }, {
    kind: 'none', posture: 'aggressive', recovery: 'heal-up',
    sigs: { '0': { policy: 'only', points: [[12, 12]] } },
  });
  assert.match(html, /cx="12\.5"/);
  assert.doesNotMatch(html, /cx="3\.5"/,
    'source-phase signature marks are not runtime carry-forward state');
  assert.match(html, /fill-opacity="\.001"/,
    'the current-phase signature mark retains its edit handle');
  assert.match(html, /FROM SETUP/);
});
