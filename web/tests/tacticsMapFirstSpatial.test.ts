import assert from 'node:assert/strict';
import test from 'node:test';
import type { TacticalSheetCatalog } from '../src/site/api.ts';
import {
  formationObjectivePoint, materializeSpatialJob, phasePaintOrder, rotatePostSelection,
  suggestFormationPosts, translatePostSelection,
} from '../src/site/tactics/mapFirstSpatial.ts';
import {
  postFacingIssue, tilePlacementIssue, type Tactic, type TacticPost,
} from '../src/site/tactics/tacticsDocument.ts';

const width = 15;
const height = 11;
const rows = Array.from({ length: height }, (_, y) =>
  Array.from({ length: width }, (_, x) =>
    x === 0 || y === 0 || x === width - 1 || y === height - 1
      || (x === 8 && y !== 5) ? '#' : '.').join(''));

const catalog = {
  map: {
    id: 'spatial-test', width, height, tileRows: rows,
    regions: [
      { id: 'well-north', kind: 'Objective', tiles: [{ x: 4, y: 3 }] },
      { id: 'well-centre', kind: 'Objective', tiles: [{ x: 7, y: 5 }] },
      { id: 'well-south', kind: 'Objective', tiles: [{ x: 11, y: 7 }] },
    ],
    spawnAnchors: [], tileTags: [],
  },
  classes: [],
} as unknown as TacticalSheetCatalog;

function tacticFixture(): Tactic {
  return {
    id: 'operation', name: 'Operation', recipe: 'blank',
    start: { mode: 'always' }, delivery: { plan: 'none' },
    groups: [{ id: 'group', name: 'Group', slots: [0, 1], coreRule: 'ignore' }],
    phases: [
      {
        id: 'one', name: 'One', jobs: {
          group: {
            kind: 'posts', posture: 'hold', recovery: 'fight-on',
            posts: [
              { slot: 0, at: [3, 3], facing: 'e', via: [[2, 2], [3, 2]] },
              { slot: 1, at: [3, 5], facing: 'e' },
            ],
          },
        },
      },
      {
        id: 'two', name: 'Two', jobs: {
          group: {
            kind: 'none', posture: 'aggressive', travel: 'hold',
            recovery: 'heal-up', sigs: { mason: { points: [[4, 4]] } },
          },
        },
      },
    ],
  };
}

test('copy-on-write materializes inherited spatial ground without changing its source', () => {
  const tactic = tacticFixture();
  const source = structuredClone(tactic.phases[0].jobs.group);
  const materialized = materializeSpatialJob(tactic, 1, 'group')!;
  assert.equal(materialized.kind, 'posts');
  assert.deepEqual(materialized.posts, source.posts);
  assert.notEqual(materialized.posts, tactic.phases[0].jobs.group.posts);
  assert.equal(materialized.posture, 'aggressive');
  assert.equal(materialized.travel, 'hold');
  assert.equal(materialized.recovery, 'heal-up');
  assert.deepEqual(materialized.sigs, { mason: { points: [[4, 4]] } });
  materialized.posts![0].at = [5, 3];
  assert.deepEqual(tactic.phases[0].jobs.group, source);
});
test('copy-on-write independently materializes an inherited patrol', () => {
  const tactic = tacticFixture();
  tactic.phases[0].jobs.group = {
    kind: 'patrol', posture: 'observe', recovery: 'fight-on',
    path: [[2, 2], [5, 2], [5, 5]],
  };
  const source = structuredClone(tactic.phases[0].jobs.group.path);
  const materialized = materializeSpatialJob(tactic, 1, 'group')!;
  materialized.path![1] = [4, 3];
  assert.deepEqual(tactic.phases[0].jobs.group.path, source);
  assert.deepEqual(tactic.phases[1].jobs.group.path![1], [4, 3]);
});

test('phase paint order always puts the selected visible phase on top', () => {
  assert.deepEqual(phasePaintOrder(4, 1, false, true), [0, 2, 1]);
  assert.deepEqual(phasePaintOrder(4, 1, true, true), [0, 2, 3, 1]);
  assert.deepEqual(phasePaintOrder(4, 3, false, false), [3]);
});

test('selection translation preserves offsets, snaps legally, and avoids reservations', () => {
  const posts: TacticPost[] = [
    { slot: 0, at: [3, 3], facing: 'e' },
    { slot: 1, at: [3, 5], facing: 'e' },
    { slot: 2, at: [6, 5], facing: 'e' },
  ];
  const moved = translatePostSelection(catalog, posts, [0, 1], 0, [6, 3]);
  assert.ok(moved);
  const a = moved.positions.get(0)!;
  const b = moved.positions.get(1)!;
  assert.deepEqual([b[0] - a[0], b[1] - a[1]], [0, 2]);
  assert.notDeepEqual(b, posts[2].at);
  for (const at of moved.positions.values()) {
    assert.equal(tilePlacementIssue(catalog, at, 'anchor'), null);
  }
  for (const post of posts.slice(0, 2)) {
    assert.equal(postFacingIssue(catalog, {
      ...post,
      at: moved.positions.get(post.slot!)!,
      facing: moved.facings.get(post.slot!)!,
    }), null);
  }
});

test('translation keeps the requested tile and cardinal facing beside a wall', () => {
  const posts: TacticPost[] = [{ slot: 0, at: [7, 5], facing: 'e' }];
  const moved = translatePostSelection(catalog, posts, [0], 0, [7, 4]);
  assert.ok(moved);
  assert.deepEqual(moved.positions.get(0), [7, 4]);
  assert.equal(moved.facings.get(0), 'e');
  assert.equal(postFacingIssue(catalog, {
    ...posts[0], at: moved.positions.get(0)!, facing: moved.facings.get(0)!,
  }), null);
});

test('selection rotation treats a wall-facing direction as legal intent', () => {
  const clear: TacticPost[] = [
    { slot: 0, at: [3, 3], facing: 'e' },
    { slot: 1, at: [3, 5], facing: 'e' },
  ];
  assert.equal(rotatePostSelection(catalog, clear, [0, 1], 'n'), true);
  const blocked: TacticPost[] = [
    ...clear,
    { slot: 2, at: [7, 4], facing: 'w' },
  ];
  assert.equal(rotatePostSelection(catalog, blocked, [0, 2], 'e'), true);
});

test('suggested formations are deterministic, distinct, legal and face the objective', () => {
  const objective = formationObjectivePoint(catalog, 'well-centre')!;
  const first = suggestFormationPosts(catalog, [0, 1, 2, 3], objective, [], 'group:phase');
  const again = suggestFormationPosts(catalog, [0, 1, 2, 3], objective, [], 'group:phase');
  assert.deepEqual(again, first);
  assert.equal(first.length, 4);
  assert.equal(new Set(first.map((post) => post.at.join(','))).size, 4);
  for (const post of first) {
    assert.equal(tilePlacementIssue(catalog, post.at, 'anchor'), null);
    const [dx, dy] = [objective[0] - post.at[0], objective[1] - post.at[1]];
    if (Math.abs(dx) >= Math.abs(dy)) assert.equal(post.facing, dx >= 0 ? 'e' : 'w');
    else assert.equal(post.facing, dy >= 0 ? 's' : 'n');
  }
});

test('well formation objectives use the mirrored sheet perspective', () => {
  assert.deepEqual(formationObjectivePoint(catalog, 'well-own'), [11, 7]);
  assert.deepEqual(formationObjectivePoint(catalog, 'well-centre'), [7, 5]);
  assert.deepEqual(formationObjectivePoint(catalog, 'well-enemy'), [4, 3]);
});
