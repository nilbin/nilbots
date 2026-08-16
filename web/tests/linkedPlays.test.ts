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
  addAmbushReserveSlot,
  movePlayAnchor,
  renamePlayGeometry,
  removeAmbushReserveSlot,
  replacePlayRole,
  setPlayLinkedValue,
} from '../src/site/sheets/playEditing';
import {
  addBlankPlay,
  addBlankPlayProjection,
  addLaneClosurePlay,
  addNorthAmbushPlay,
  addRelayChainPlay,
  addSafeReturnPlay,
  addVeteranEscortPlay,
} from '../src/site/sheets/playRecipes';
import { playGeometryIssues, setPlaySlotTolerance } from '../src/site/sheets/playGeometry';
import { bestPlayReadiness, type UnitTrack } from '../src/site/sheets/sheetTrials';

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

test('an Ambush pooled post updates readiness and reserve Orders as one reversible edit', () => {
  let linked = addNorthAmbushPlay(draft(), {
    masonRoleId: 'mason', longshotRoleId: 'longshot', hushRoleId: 'hush', reserveRoleId: 'reserve',
  });
  const playId = linked.workshop.plays[0].id;
  linked = addAmbushReserveSlot(linked, playId);
  const play = linked.workshop.plays[0];
  assert.equal(play.slots.length, 6);
  assert.match(playGeometryIssues(linked, playId)[0].message, /2 bodies for 3 required positions/);
  assert.equal(inspectWorkshop(linked.orders, linked.workshop).length, 0);
  const engageWhen = play.linkedValues.find((entry) => entry.id === `${playId}-engage-when`)!;
  assert.equal(typeof engageWhen.value === 'object' && engageWhen.value && 'all' in engageWhen.value
    ? engageWhen.value.all.length : 0, 7);
  const third = play.slots.find((slot) => slot.id === `${playId}-reserve-c-slot`)!;
  linked = removeAmbushReserveSlot(linked, playId, third.id);
  assert.equal(linked.workshop.plays[0].slots.length, 5);
  assert.equal(playGeometryIssues(linked, playId).length, 0);
  assert.equal(inspectWorkshop(linked.orders, linked.workshop).length, 0);
});

test('the Relay chain keeps its carrier moving between sparse forward receiver posts', () => {
  const linked = addRelayChainPlay(draft(), {
    relayARoleId: 'relay-a', relayBRoleId: 'relay-b',
  });
  const play = linked.workshop.plays[0];
  assert.equal(play.recipe, 'relay-chain');
  assert.equal(play.projections.length, 2);
  assert.equal(play.slots.length, 0);
  assert.equal(play.ownedGeometry.filter((entry) => entry.kind === 'route').length, 1);
  assert.equal(play.ownedGeometry.filter((entry) => entry.kind === 'anchor'
    && entry.id.includes('-receiver-')).length, 3);
  assert.equal(playGeometryIssues(linked, play.id, catalog.map.tileRows).length, 0);
  assert.equal(inspectWorkshop(linked.orders, linked.workshop).length, 0);
  assert.equal(validateDraft(linked, catalog).filter((issue) => issue.severity === 'error').length, 0);
  const generated = play.projections.flatMap((projection) => allOrders(projection.orders));
  const receiverBranches = play.projections.flatMap((projection) => projection.orders)
    .filter((order) => order.id?.includes('-receive-'));
  assert.equal(generated.filter((order) => order.use?.skill === 'arc-toss').length, 3);
  assert.equal(receiverBranches.length, 2);
  assert.equal(generated.filter((order) => order.id?.endsWith('-carry-forward')).length, 2);
  assert.equal(generated.filter((order) => order.id?.endsWith('-take-start')).length, 2);
  assert.equal(generated.filter((order) => order.id?.includes('-hop-')).length, 0);
  const corridor = play.ownedGeometry.find((entry) => entry.kind === 'route')!.id;
  const tossLinks = play.geometryGroups[0].tossLinks!;
  assert.deepEqual(tossLinks.map((link) => link.sourceWaypointIndex), [1, 7, 10]);
  for (const projection of play.projections) {
    const children = projection.orders.find((order) => order.id?.endsWith('-running-return'))!.orders!;
    const toss = children.find((order) => order.use?.skill === 'arc-toss')!;
    assert.equal(typeof toss.use!.target === 'object' && 'role' in toss.use!.target
      ? toss.use!.target.aheadVia : undefined, corridor);
    assert.equal(typeof toss.use!.target === 'object' && 'role' in toss.use!.target
      ? Number.isInteger(toss.use!.target.minimumForwardTiles) : false, true);
    assert.equal(children.find((order) => order.id?.endsWith('-carry-forward'))!.urgency, 'normal');
    assert.equal(children.find((order) => order.id?.endsWith('-carry-forward'))!.move?.patrol, corridor);
    for (const receive of projection.orders.filter((order) => order.id?.includes('-receive-'))) {
      assert.equal(receive.urgency, 'urgent');
      assert.equal(receive.timeout, 40);
      assert.equal(receive.move, undefined);
      const hold = receive.orders!.find((order) => order.id?.endsWith('-hold'))!;
      const run = receive.orders!.find((order) => order.id?.endsWith('-run-corridor'))!;
      assert.equal(typeof hold.move?.to === 'object' && 'anchor' in hold.move.to, true);
      assert.equal(run.move?.patrol, corridor);
    }
  }

  const firstReceiver = tossLinks[0].receiverAnchorId;
  const moved = movePlayAnchor(linked, play.id, firstReceiver, [13, 15]);
  const movedZone = moved.workshop.plays[0].ownedGeometry.find((entry) =>
    entry.kind === 'zone' && entry.derivedFrom === firstReceiver)!.id;
  assert.deepEqual(moved.mapPlan.zones[movedZone].rect, [13, 15, 13, 15]);
  assert.match(playGeometryIssues(moved, play.id, catalog.map.tileRows)[0].message,
    /not on a horizontal, vertical, or 45° line/);

  const blockedRows = [...catalog.map.tileRows];
  blockedRows[16] = `${blockedRows[16].slice(0, 16)}#${blockedRows[16].slice(17)}`;
  assert.match(playGeometryIssues(linked, play.id, blockedRows)[0].message,
    /blocked by terrain at 16,16/);

  const detached = detachPlay(linked.workshop, play.id);
  assert.equal(detached.plays.length, 0);
  assert.ok(linked.orders.roles.find((entry) => entry.id === 'relay-a')!
    .orders.some((order) => order.id === `${play.id}-a-running-return`));
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

test('the remaining recipe lenses materialize only valid ordinary Orders', () => {
  const candidates = [
    addVeteranEscortPlay(draft(), { veteranRoleId: 'longshot', escortRoleId: 'reserve' }),
    addSafeReturnPlay(draft(), { carrierRoleId: 'longshot', relayRoleId: 'relay-a' }),
    addLaneClosurePlay(draft(), { masonRoleId: 'mason', guardRoleId: 'reserve' }),
  ];
  assert.deepEqual(candidates.map((candidate) => candidate.workshop.plays[0].recipe),
    ['veteran-escort', 'safe-return', 'lane-closure']);
  for (const candidate of candidates) {
    const play = candidate.workshop.plays[0];
    assert.equal(inspectWorkshop(candidate.orders, candidate.workshop).length, 0);
    assert.equal(playGeometryIssues(candidate, play.id).length, 0);
    assert.deepEqual(validateDraft(candidate, catalog).filter((issue) => issue.severity === 'error'), []);
  }
});

test('Practice evidence distinguishes four of five from complete Ambush readiness', () => {
  const linked = addNorthAmbushPlay(draft(), {
    masonRoleId: 'mason', longshotRoleId: 'longshot', hushRoleId: 'hush', reserveRoleId: 'reserve',
  });
  const play = linked.workshop.plays[0];
  const track = (unitId: number, point: [number, number]): UnitTrack => ({
    key: `0:${unitId}`, teamId: 0, unitId, points: [{ x: point[0], y: point[1] }],
    samples: [{ tick: 20, position: { x: point[0], y: point[1] } }],
  });
  const tracks = play.slots.slice(0, 4).map((slot, index) =>
    track(index, linked.mapPlan.anchors[slot.anchorId]));
  assert.deepEqual(bestPlayReadiness(linked, play, tracks), {
    ready: 4, total: 5, tick: 20, readySlotIds: play.slots.slice(0, 4).map((slot) => slot.id),
  });
  tracks.push(track(4, linked.mapPlan.anchors[play.slots[4].anchorId]));
  assert.equal(bestPlayReadiness(linked, play, tracks).ready, 5);
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

function allOrders(orders: OrdersDocument['roles'][number]['orders']): OrdersDocument['roles'][number]['orders'] {
  return orders.flatMap((order) => [order, ...allOrders(order.orders ?? [])]);
}
