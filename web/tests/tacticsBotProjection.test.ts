import assert from 'node:assert/strict';
import test from 'node:test';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import type { TacticalSheetCatalog } from '../src/site/api.ts';
import {
  groundFingerprint, projectPhaseBots,
} from '../src/site/tactics/botProjection.ts';
import { makePathfinder } from '../src/site/tactics/pathing.ts';
import { tilePlacementIssue, type Tactic, type TacticsDocument } from '../src/site/tactics/tacticsDocument.ts';
import { BotProjectionLayer, JobOverlay, SelectedBotRanges } from './.harness/harness.entry.js';

const width = 21;
const height = 15;
const rows = Array.from({ length: height }, (_, y) =>
  Array.from({ length: width }, (_, x) =>
    x === 0 || y === 0 || x === width - 1 || y === height - 1
      || (x === 10 && y !== 5) ? '#' : '.').join(''));

const catalog = {
  map: {
    id: 'projection-test', width, height, tileRows: rows,
    regions: [], spawnAnchors: [], tileTags: [],
  },
  classes: [{
    id: 'mason', name: 'Mason', fantasy: '', unlocked: true,
    signatureName: 'Hardlight',
    visionRange: 6, visionProximityRange: 2, attackRange: 4,
    signatureAuthoring: { allowsExactAnchor: true, allowsRouteForward: false },
  }],
} as unknown as TacticalSheetCatalog;

function fixture(): { document: TacticsDocument; tactic: Tactic } {
  const tactic: Tactic = {
    id: 'operation', name: 'Operation', recipe: 'blank',
    start: { mode: 'always' }, delivery: { plan: 'none' },
    groups: [
      { id: 'area', name: 'Area team', slots: [0, 1, 2, 3], coreRule: 'ignore' },
      { id: 'route', name: 'Route team', slots: [4, 5, 6, 7], coreRule: 'ignore' },
    ],
    phases: [
      {
        id: 'set', name: 'Set', jobs: {
          area: { kind: 'area', posture: 'hold', recovery: 'fight-on', rect: [2, 2, 7, 7] },
          route: { kind: 'converge', posture: 'hold', recovery: 'fight-on', point: [7, 10] },
        },
      },
      {
        id: 'move', name: 'Move', jobs: {
          area: { kind: 'none', posture: 'aggressive', recovery: 'heal-up' },
          route: {
            kind: 'patrol', posture: 'observe', recovery: 'fight-on',
            path: [[3, 3], [17, 3], [17, 10]],
          },
        },
      },
    ],
  };
  const document: TacticsDocument = {
    schema: 'arc-relay-tactics-v2', sheetId: 'projection', mapId: catalog.map.id,
    mapWidth: width, mapHeight: height,
    squad: Array.from({ length: 8 }, (_, slot) => ({
      slot, class: 'mason', build: 'balanced', name: `Bot ${slot}`,
    })),
    tactics: [tactic],
  };
  return { document, tactic };
}

test('stable projections are deterministic, distinct and legal phase-wide', () => {
  const { document, tactic } = fixture();
  const first = projectPhaseBots(catalog, document, tactic, 0);
  const again = projectPhaseBots(catalog, document, tactic, 0);
  assert.deepEqual(again, first);
  assert.equal(first.bots.length, 8);
  assert.equal(new Set(first.bots.map((bot) => bot.at.join(','))).size, 8);
  for (const bot of first.bots) {
    assert.equal(tilePlacementIssue(catalog, bot.at, 'anchor'), null);
    assert.equal(catalog.map.tileRows[bot.at[1]][bot.at[0]], '.');
    assert.equal(bot.exact, false);
  }
});

test('selected exact bots render directional catalog vision separately from catalog attack reach', () => {
  const { document, tactic } = fixture();
  tactic.phases[0].jobs.area = {
    kind: 'posts', posture: 'hold', recovery: 'fight-on',
    posts: [{ slot: 0, at: [4, 4], facing: 'e' }],
  };
  const markup = renderToStaticMarkup(createElement(SelectedBotRanges, {
    catalog, document, tactic, phaseIndex: 0, selectedSlots: [0],
    showVision: true, showAttackRange: true,
  }));
  assert.match(markup, /data-testid="selected-bot-range-0"/);
  assert.match(markup, /<path[^>]+A 6 6/,
    'vision is a directional sector using the catalog vision range');
  assert.match(markup, /<circle[^>]+r="2"/,
    'omnidirectional proximity vision stays distinct');
  assert.match(markup, /<circle[^>]+r="4"/,
    'attack reach uses the class attack range, not the vision distance');
});

test('relevant geometry and membership change the projection, unrelated behavior does not', () => {
  const { document, tactic } = fixture();
  const baseline = projectPhaseBots(catalog, document, tactic, 0);
  const behavior = structuredClone(tactic);
  behavior.phases[0].jobs.area.posture = 'aggressive';
  behavior.phases[0].jobs.area.recovery = 'heal-up';
  assert.equal(
    groundFingerprint(behavior.phases[0].jobs.area),
    groundFingerprint(tactic.phases[0].jobs.area),
  );
  assert.deepEqual(projectPhaseBots(catalog, document, behavior, 0), baseline);

  const geometry = structuredClone(tactic);
  geometry.phases[0].jobs.area.rect = [12, 2, 17, 7];
  assert.notDeepEqual(
    projectPhaseBots(catalog, document, geometry, 0).bots
      .filter((bot) => bot.groupId === 'area').map((bot) => bot.at),
    baseline.bots.filter((bot) => bot.groupId === 'area').map((bot) => bot.at),
  );

  const membership = structuredClone(tactic);
  membership.groups[0].slots = [0, 1, 2];
  membership.groups[1].slots = [3, 4, 5, 6, 7];
  const moved = projectPhaseBots(catalog, document, membership, 0);
  assert.equal(moved.bots.filter((bot) => bot.groupId === 'area').length, 3);
  assert.equal(moved.bots.filter((bot) => bot.groupId === 'route').length, 5);
});

test('carry-forward retains the same bot tiles when ground is unchanged', () => {
  const { document, tactic } = fixture();
  const before = projectPhaseBots(catalog, document, tactic, 0);
  const after = projectPhaseBots(catalog, document, tactic, 1);
  for (const slot of tactic.groups[0].slots) {
    const from = before.bots.find((bot) => bot.slot === slot)!;
    const to = after.bots.find((bot) => bot.slot === slot)!;
    assert.deepEqual(to.at, from.at);
    assert.equal(to.carried, true);
    assert.equal(to.sourcePhaseIndex, 0);
  }
});

test('patrol projection samples the real connected walkable route through its opening', () => {
  const { document, tactic } = fixture();
  const pathfinder = makePathfinder(catalog);
  const patrol = tactic.phases[1].jobs.route.path!;
  const connected = pathfinder.connectTiles([...patrol, patrol[0]]);
  assert.ok(connected);
  assert.ok(connected.some(([x, y]) => x === 10 && y === 5),
    'the production path must use the only opening in the divider');
  for (let index = 1; index < connected!.length; index++) {
    const [ax, ay] = connected![index - 1];
    const [bx, by] = connected![index];
    assert.equal(pathfinder.floor(bx, by), true);
    assert.ok(Math.abs(ax - bx) <= 1 && Math.abs(ay - by) <= 1);
    if (ax !== bx && ay !== by) {
      assert.equal(pathfinder.floor(ax, by) && pathfinder.floor(bx, ay), true,
        'diagonal route steps must not cut a wall corner');
    }
  }
  const routeTiles = new Set(connected!.map((point) => point.join(',')));
  const projected = projectPhaseBots(catalog, document, tactic, 1).bots
    .filter((bot) => bot.groupId === 'route');
  assert.equal(projected.length, 4);
  assert.ok(projected.every((bot) => routeTiles.has(bot.at.join(','))));
});

test('a disconnected authored patrol never renders a straight line through the wall', () => {
  const { document, tactic } = fixture();
  const disconnected = structuredClone(catalog) as TacticalSheetCatalog;
  disconnected.map.tileRows = disconnected.map.tileRows.map((row) => {
    const tiles = [...row];
    tiles[10] = '#';
    return tiles.join('');
  });
  const group = tactic.groups[1];
  const markup = renderToStaticMarkup(createElement(JobOverlay, {
    catalog: disconnected, tactic, group, phaseIndex: 1,
    bots: document.squad, dim: false, editable: true,
    startDrag: () => undefined, pathfinder: makePathfinder(disconnected),
  }));
  assert.doesNotMatch(markup, /<polyline/,
    'unresolved movement is omitted rather than replaced by a wall-crossing segment');
});

test('projected bots render the production class sprite and explicit preview mark', () => {
  const { document, tactic } = fixture();
  const markup = renderToStaticMarkup(createElement(BotProjectionLayer, {
    catalog, document, tactic, phaseIndex: 0,
    pathfinder: makePathfinder(catalog),
  }));
  assert.match(markup, /data-testid="projected-bot-0"/);
  assert.match(markup, /<image href="\/assets\/sprite-[^"]+\.png"/);
  assert.match(markup, />P<\/text>/);
  assert.match(markup, /Preview only/);
});
