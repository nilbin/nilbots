import assert from 'node:assert/strict';
import test from 'node:test';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import type { TacticalSheetCatalog } from '../src/site/api.ts';
import type { TacticsDocument } from '../src/site/tactics/tacticsDocument.ts';
import { TacticsMapCanvas } from './.harness/harness.entry.js';

const rows = Array.from({ length: 9 }, (_, y) =>
  Array.from({ length: 11 }, (_, x) =>
    x === 0 || y === 0 || x === 10 || y === 8 ? '#' : '.').join(''));

const catalog = {
  map: {
    id: 'layering', width: 11, height: 9, tileRows: rows,
    regions: [], spawnAnchors: [], tileTags: [],
  },
  classes: [{
    id: 'mason', name: 'Mason', signatureName: 'Hardlight',
    signatureAuthoring: {
      allowsExactAnchor: true, allowsRouteForward: false,
      guidanceGeometry: 'points', guidanceMarkLimit: 2,
    },
  }],
} as unknown as TacticalSheetCatalog;

const document = {
  v: 'arc-relay-tactics-v1',
  squad: [
    { slot: 0, class: 'mason', build: 'balanced', name: 'Forge' },
    { slot: 1, class: 'mason', build: 'balanced', name: 'Kiln' },
  ],
  tactics: [{
    id: 'layers', name: 'Layers', recipe: 'blank',
    start: { mode: 'always' }, delivery: { plan: 'none' },
    groups: [{ id: 'all', name: 'All', slots: [0, 1], coreRule: 'ignore' }],
    phases: [
      {
        id: 'selected', name: 'Selected', jobs: {
          all: {
            kind: 'posts', posture: 'hold', recovery: 'fight-on',
            posts: [
              { slot: 0, at: [4, 4], facing: 'e' },
              { slot: 1, at: [5, 4], facing: 'w' },
            ],
          },
        },
      },
      {
        id: 'context', name: 'Context', jobs: {
          all: {
            kind: 'posts', posture: 'hold', recovery: 'fight-on',
            posts: [
              { slot: 0, at: [4, 4], facing: 'n' },
              { slot: 1, at: [5, 4], facing: 's' },
            ],
          },
        },
      },
    ],
  }],
} as unknown as TacticsDocument;

test('selected phase and its adjacent-safe rotation grips paint last', () => {
  const html = renderToStaticMarkup(createElement(TacticsMapCanvas, {
    catalog,
    document,
    editingTacticId: 'layers',
    activePhaseIndex: 0,
    selectedTacticId: 'layers',
    showAll: false,
    showAllPhases: true,
    showProjectedBots: false,
    showAdjacentPhases: true,
    allowInheritedSpatialEditing: true,
    selectedSlots: [0],
    task: null,
    hints: null,
    onTilePicked: () => {},
    onRectDrawn: () => {},
    onGeometryLive: () => {},
    onGestureStart: () => {},
    onGestureEnd: () => {},
    onSelect: () => {},
    onClearSelection: () => {},
  }));

  const contextMarker = html.indexOf('data-select="layers:post:all:1:0"');
  const selectedMarker = html.indexOf('data-select="layers:post:all:0:0"');
  const selectedControls = html.indexOf('data-testid="phase-post-controls-0"');
  const selectedGrip = html.indexOf(
    'data-testid="post-facing-handle-0-all-0"');
  assert.ok(contextMarker >= 0 && selectedMarker > contextMarker,
    'selected phase marker must paint above the same-tile context marker');
  assert.ok(selectedControls > selectedMarker,
    'selected phase interaction layer must paint above its bot pictures');
  assert.ok(selectedGrip > selectedControls,
    'rotation grip remains the last interactive layer over adjacent bots');
});
