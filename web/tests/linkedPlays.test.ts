import assert from 'node:assert/strict';
import test from 'node:test';
import type { TacticalSheetCatalog } from '../src/site/api';
import {
  parseDraft,
  stringifyDocument,
  validateDraft,
  type MapPlanDocument,
  type OrdersDocument,
} from '../src/site/sheets/sheetAuthoring';
import {
  deletePlay,
  detachPlay,
  inspectWorkshop,
  materializeWorkshop,
  setLinkedValue,
} from '../src/site/sheets/linkedPlays';
import {
  renamePlayGeometry,
  replacePlayRole,
  setPlayLinkedValue,
} from '../src/site/sheets/playEditing';
import {
  addBlankPlay,
  addBlankPlayProjection,
  addNorthAmbushPlay,
  addRelayChainPlay,
} from '../src/site/sheets/playRecipes';
import { playGeometryIssues, setPlaySlotTolerance } from '../src/site/sheets/playGeometry';

const signatures: Record<string, string> = {
  mason: 'hardlight-block', longshot: 'rail-line', hush: 'null-field',
  kestrel: 'vector-dash', palisade: 'prism-wall', relay: 'arc-toss',
  patchbay: 'repair-beam',
};

const orders: OrdersDocument = {
  schema: 'arc-relay-orders-v2',
  sheetId: 'linked-play-test',
  mapPlan: 'map-plan.json',
  roles: [
    role('mason', ['mason']),
    role('longshot', ['longshot']),
    role('hush', ['hush']),
    role('reserve', ['kestrel', 'palisade']),
    role('relay-a', ['relay']),
    role('relay-b', ['relay']),
    role('support', ['patchbay']),
  ],
  sharedOrders: {}, capacities: {}, conditions: {},
};
const mapPlan: MapPlanDocument = {
  schema: 'arc-relay-map-plan-v2', mapPlanId: 'linked-play-test',
  mapId: 'arc-relay-tapline-02', zones: {}, routes: {}, anchors: {},
};
const catalog: TacticalSheetCatalog = {
  playlistKey: 'arc-relay', playlistVersionId: '10000000-0000-0000-0000-000000000001',
  map: {
    id: mapPlan.mapId, version: 1, formatVersion: 3, width: 31, height: 27,
    tileRows: Array.from({ length: 27 }, () => '.'.repeat(31)),
    regions: [], spawnAnchors: [], tileTags: [],
  },
  slotCount: 8, maximumCopiesPerClass: 2, minimumDistinctClasses: 7,
  classes: Object.entries(signatures).map(([id, signatureId]) => ({
    id, name: id, signatureId, actionId: signatureId, signatureName: signatureId,
    fantasy: id, starter: true, unlocked: true,
  })),
  templateOrdersJson: '{}', templateMapPlanJson: '{}', stockOpponents: [],
};

test('the North ambush materializes ordinary linked Orders and detects external drift', () => {
  const source = draft();
  const linked = addNorthAmbushPlay(source, {
    masonRoleId: 'mason', longshotRoleId: 'longshot', hushRoleId: 'hush', reserveRoleId: 'reserve',
  });
  const play = linked.workshop.plays[0];
  assert.equal(play.recipe, 'ambush');
  assert.equal(play.slots.length, 5);
  assert.equal(play.projections.length, 4);
  assert.equal(inspectWorkshop(linked.orders, linked.workshop).length, 0);
  assert.equal(validateDraft(linked, catalog).filter((issue) => issue.severity === 'error').length, 0);

  const timeout = play.linkedValues.find((value) => value.field === 'timeout')!;
  const editedWorkshop = setLinkedValue(linked.workshop, play.id, timeout.id, 40);
  const rematerialized = materializeWorkshop(linked.orders, editedWorkshop);
  assert.equal(rematerialized.drifts.length, 0);
  for (const projection of editedWorkshop.plays[0].projections) {
    const role = rematerialized.orders.roles.find((entry) => entry.id === projection.roleId)!;
    const engage = role.orders.find((order) => order.id?.endsWith('-engage'))!;
    assert.equal(engage.timeout, 40);
  }

  rematerialized.orders.roles.find((entry) => entry.id === 'mason')!
    .orders.find((order) => order.id?.endsWith('-engage'))!.timeout = 41;
  assert.equal(inspectWorkshop(rematerialized.orders, rematerialized.workshop)[0].kind, 'order-changed');
});

test('required pooled readiness refuses overlap and delete cleans only Play-owned sources', () => {
  let linked = addNorthAmbushPlay(draft(), {
    masonRoleId: 'mason', longshotRoleId: 'longshot', hushRoleId: 'hush', reserveRoleId: 'reserve',
  });
  const play = linked.workshop.plays[0];
  linked = setPlaySlotTolerance(linked, play.id, `${play.id}-reserve-a-slot`, 3);
  linked = setPlaySlotTolerance(linked, play.id, `${play.id}-reserve-b-slot`, 3);
  assert.match(playGeometryIssues(linked, play.id)[0].message, /falsely fill both/);

  const ordinaryBefore = orders.roles.map((entry) => entry.orders[0].id);
  const removed = deletePlay(linked, play.id, true);
  assert.deepEqual(removed.draft.orders.roles.map((entry) => entry.orders[0].id), ordinaryBefore);
  assert.equal(removed.draft.workshop.plays.length, 0);
  assert.equal(Object.keys(removed.draft.orders.conditions).length, 0);
  assert.equal(Object.keys(removed.draft.mapPlan.anchors).length, 0);
});

test('the Relay chain uses the same projection path and leaves standalone Orders on detach', () => {
  const linked = addRelayChainPlay(draft(), {
    relayARoleId: 'relay-a', relayBRoleId: 'relay-b',
  });
  const play = linked.workshop.plays[0];
  assert.equal(play.recipe, 'relay-chain');
  assert.equal(play.projections.length, 2);
  assert.equal(play.slots.length, 4);
  assert.equal(playGeometryIssues(linked, play.id).length, 0);
  assert.equal(inspectWorkshop(linked.orders, linked.workshop).length, 0);
  assert.equal(validateDraft(linked, catalog).filter((issue) => issue.severity === 'error').length, 0);

  const detached = detachPlay(linked.workshop, play.id);
  assert.equal(detached.plays.length, 0);
  assert.ok(linked.orders.roles.find((entry) => entry.id === 'relay-a')!
    .orders.some((order) => order.id === `${play.id}-a-hop`));
});

test('reference-aware Play edits rename geometry and change a participant atomically', () => {
  let linked = addNorthAmbushPlay(draft(), {
    masonRoleId: 'mason', longshotRoleId: 'longshot', hushRoleId: 'hush', reserveRoleId: 'reserve',
  });
  const play = linked.workshop.plays[0];
  const post = play.slots.find((slot) => slot.roleId === 'mason')!.anchorId;
  linked = renamePlayGeometry(linked, play.id, 'anchor', post, 'renamed-mason-post');
  assert.equal(inspectWorkshop(linked.orders, linked.workshop).length, 0);
  assert.ok(linked.mapPlan.anchors['renamed-mason-post']);
  assert.ok(!linked.mapPlan.anchors[post]);
  assert.equal(linked.workshop.plays[0].slots.find((slot) => slot.roleId === 'mason')!.anchorId,
    'renamed-mason-post');

  linked.orders.roles.push(role('mason-alt', ['mason']));
  const masonProjection = linked.workshop.plays[0].projections.find((projection) =>
    projection.roleId === 'mason')!;
  linked = replacePlayRole(linked, play.id, masonProjection.id, 'mason-alt');
  assert.equal(inspectWorkshop(linked.orders, linked.workshop).length, 0);
  assert.ok(!linked.orders.roles.find((entry) => entry.id === 'mason')!.orders
    .some((order) => order.id?.startsWith(play.id)));
  assert.ok(linked.orders.roles.find((entry) => entry.id === 'mason-alt')!.orders
    .some((order) => order.id?.startsWith(play.id)));
});

test('a blank Play authors ordinary Orders through the generic projection path', () => {
  let linked = addBlankPlay(draft(), 'Custom linked Orders');
  const playId = linked.workshop.plays[0].id;
  linked = addBlankPlayProjection(linked, playId, 'support');
  const play = linked.workshop.plays[0];
  assert.equal(play.projections.length, 1);
  assert.equal(inspectWorkshop(linked.orders, linked.workshop).length, 0);
  const timeout = play.linkedValues[0];
  if (timeout) linked = setPlayLinkedValue(linked, playId, timeout.id, 30);
  assert.ok(linked.orders.roles.find((entry) => entry.id === 'support')!.orders
    .some((order) => order.id?.startsWith(`${playId}-support-move`)));
});

function role(id: string, classes: string[]) {
  return {
    id, classes, upgradePath: ['reach' as const, 'vitality' as const],
    orders: [{ id: `${id}-baseline`, move: { to: 'own-reactor' as const } }],
  };
}

function draft() {
  return parseDraft(
    'Linked Play test', null, null,
    stringifyDocument(orders), stringifyDocument(mapPlan), false,
  );
}
