import assert from 'node:assert/strict';
import test from 'node:test';
import type { ReplayModel } from '../src/replayModel';
import type { TacticalSheetCatalog } from '../src/site/api';
import {
  conditionExpressionNames,
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
  parseWorkshop,
  setLinkedValue,
  signatureOpportunityViews,
} from '../src/site/sheets/linkedPlays';
import {
  addPlayGeometry,
  addPlayPost,
  addCoreRecoveryOpportunity,
  addFightOpportunity,
  addHealthRecoveryOpportunity,
  addSignatureOpportunity,
  addSignatureOpportunityActivationZone,
  addSignatureOpportunityAnchor,
  addAmbushReserveSlot,
  appendPlayRouteWaypoint,
  configurePlayContingent,
  configurePlayLifecycle,
  configurePlayPostDeployment,
  configurePlaySharedTrigger,
  moveProgram,
  movePlayAnchor,
  moveSignatureOpportunity,
  setPlayPostFacing,
  renamePlayGeometry,
  removeAmbushReserveSlot,
  removePlayReadinessGroup,
  removeSignatureOpportunity,
  renameSignatureOpportunity,
  replacePlayRole,
  setPlayLinkedValue,
  setPlayReadinessGroup,
  setPlayZoneRect,
  setSignatureOpportunityCommitmentHold,
  updateSignatureOpportunityUse,
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
import {
  bestPlayReadiness,
  signatureOpportunityPracticeSummary,
  type UnitTrack,
} from '../src/site/sheets/sheetTrials';

const signatures: Record<string, string> = {
  mason: 'hardlight-block', longshot: 'rail-line', hush: 'null-field',
  kestrel: 'vector-dash', palisade: 'prism-wall', relay: 'arc-toss',
  patchbay: 'repair-beam',
};
const authoring = (signatureId: string) => ({
  category: 'support', argumentKind: 'unit', engagementRange: 6,
  cooldownTicks: 30, activationTiming: 'before-combat', initialPhase: 'active',
  maturityPhase: 'immediate', cooldownAnchor: 'start', parameters: {},
  allowedSelectors: signatureId === 'null-field' ? ['self']
    : signatureId === 'repair-beam' ? ['weakest-ally', 'nearest-ally']
      : signatureId === 'arc-toss' ? ['nearest-ally-toward-own-reactor', 'nearest-ally', 'own-reactor']
        : ['nearest-enemy', 'weakest-enemy', 'enemy-carrier', 'nearest-ally', 'weakest-ally', 'self', 'own-reactor'],
  allowsFriendlyRole: ['repair-beam', 'arc-toss'].includes(signatureId),
  allowsMostExperiencedAlly: ['repair-beam', 'arc-toss'].includes(signatureId),
  allowsExactAnchor: signatureId === 'hardlight-block',
  allowsRouteAheadRole: signatureId === 'arc-toss',
  allowsRouteForward: signatureId === 'vector-dash',
  defaultCommitmentBehavior: 'continue-normally',
  preset: {
    id: `use-${signatureId}`, title: signatureId, description: signatureId,
    targetKind: 'named-selector', namedTarget: 'nearest-enemy',
    suggestsActivationZone: true,
  },
});

// The Running Relay recipe is authored against Tapline's real terrain. Keep
// its geometry test on that terrain so an apparently straight Toss cannot
// silently cross a wall while an all-floor fixture says it is valid.
const taplineTileRows = [
  '###############################',
  '#.............................#',
  '###.###########.###########.###',
  '#......#......................#',
  '#....#...#..###.###.##..###...#',
  '#....#...#...#...#..#....#....#',
  '#........#...#...#..#.##.#....#',
  '#........#.......#.......#....#',
  '#....#...###....##....#.......#',
  '###......###.##.#..........##.#',
  '#....#######......###..##.....#',
  '#.##......##.......#....#.....#',
  '#.....###..#.......#....#.....#',
  '#..##...##...........##...##..#',
  '#.....#....#.......#..###.....#',
  '#.....#....#.......##......##.#',
  '#.....##..###......#######....#',
  '#.##..........#.##.###......###',
  '#.......#....##....###...#....#',
  '#....#.......#.......#........#',
  '#....#.##.#..#...#...#........#',
  '#....#....#..#...#...#...#....#',
  '#...###..##.###.###..#...#....#',
  '#......................#......#',
  '###.###########.###########.###',
  '#.............................#',
  '###############################',
];

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
    signatureAuthoring: authoring(signatureId),
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
  assert.deepEqual(validateDraft(linked, catalog).filter((issue) => issue.severity === 'error'), []);

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
  assert.equal(playGeometryIssues(linked, play.id, taplineTileRows).length, 0);
  assert.equal(inspectWorkshop(linked.orders, linked.workshop).length, 0);
  assert.deepEqual(validateDraft(linked, catalog).filter((issue) => issue.severity === 'error'), []);
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
  assert.deepEqual(tossLinks.map((link) => link.sourceWaypointIndex), [5, 9, 13]);
  assert.deepEqual(tossLinks.map((link) => link.forwardRouteTiles), [3, 5, 4]);
  for (const projection of play.projections) {
    const children = projection.orders.find((order) => order.id?.endsWith('-running-return'))!.orders!;
    const toss = children.find((order) => order.use?.skill === 'arc-toss')!;
    assert.deepEqual(toss.use!.target, {
      role: projection.roleId === 'relay-a' ? 'relay-b' : 'relay-a',
    });
    assert.equal(children.find((order) => order.id?.endsWith('-carry-forward'))!.urgency, 'normal');
    assert.equal(children.find((order) => order.id?.endsWith('-carry-forward'))!.move?.patrol, corridor);
    for (const receive of projection.orders.filter((order) => order.id?.includes('-receive-'))) {
      assert.equal(receive.urgency, 'urgent');
      assert.equal(receive.timeout, 80);
      assert.equal(receive.move, undefined);
      const hold = receive.orders!.find((order) => order.id?.endsWith('-hold'))!;
      const enter = receive.orders!.find((order) => order.id?.endsWith('-enter-pocket'))!;
      const run = receive.orders!.find((order) => order.id?.endsWith('-run-corridor'))!;
      assert.equal(typeof hold.move?.to === 'object' && 'anchor' in hold.move.to, true);
      assert.equal(typeof enter.move?.to === 'object' && 'anchor' in enter.move.to, true);
      assert.equal(run.move?.patrol, corridor);
    }
  }
  assert.equal(JSON.stringify(linked.orders).includes('aheadVia'), false);
  assert.equal(JSON.stringify(linked.orders).includes('minimumForwardTiles'), false);

  const firstReceiver = tossLinks[0].receiverAnchorId;
  const moved = movePlayAnchor(linked, play.id, firstReceiver, [28, 24]);
  const movedZone = moved.workshop.plays[0].ownedGeometry.find((entry) =>
    entry.kind === 'zone' && entry.derivedFrom === firstReceiver)!.id;
  assert.deepEqual(moved.mapPlan.zones[movedZone].rect, [28, 24, 28, 24]);
  assert.match(playGeometryIssues(moved, play.id, taplineTileRows)[0].message,
    /not on a horizontal, vertical, or 45° line/);

  const blockedRows = [...taplineTileRows];
  blockedRows[23] = `${blockedRows[23].slice(0, 26)}#${blockedRows[23].slice(27)}`;
  assert.match(playGeometryIssues(linked, play.id, blockedRows)[0].message,
    /blocked by terrain at 26,23/);

  const detached = detachPlay(linked.workshop, play.id);
  assert.equal(detached.plays.length, 0);
  assert.ok(linked.orders.roles.find((entry) => entry.id === 'relay-a')!
    .orders.some((order) => order.id === `${play.id}-a-running-return`));
});

test('old v1 Plays gain opportunity linkage without changing their ordinary Orders', () => {
  for (const source of [
    addRelayChainPlay(draft(), { relayARoleId: 'relay-a', relayBRoleId: 'relay-b' }),
    addNorthAmbushPlay(draft(), {
      masonRoleId: 'mason', longshotRoleId: 'longshot', hushRoleId: 'hush', reserveRoleId: 'reserve',
    }),
  ]) {
    const oldWorkshop = structuredClone(source.workshop);
    for (const projection of oldWorkshop.plays[0].projections)
      delete projection.signatureOpportunities;
    const before = oldWorkshop.plays[0].projections.map((projection) =>
      stringifyDocument(projection.orders));
    const normalized = parseWorkshop(JSON.stringify(oldWorkshop));
    const after = normalized.plays[0].projections.map((projection) =>
      stringifyDocument(projection.orders));
    assert.deepEqual(after, before);
    assert.ok(normalized.plays[0].projections.some((projection) =>
      (projection.signatureOpportunities?.length ?? 0) > 0));
    assert.equal(inspectWorkshop(source.orders, normalized, catalog).length, 0);
  }
});

test('a signature opportunity scaffolds, edits and removes one real local priority tree', () => {
  let linked = addBlankPlay(draft(), 'Signature scaffold');
  linked = addBlankPlayProjection(linked, linked.workshop.plays[0].id, 'support');
  const playId = linked.workshop.plays[0].id;
  const projection = linked.workshop.plays[0].projections[0];
  const hostId = projection.orders[0].id!;
  const originalTarget = structuredClone(projection.orders[0].move!.to);
  projection.orders[0].when = 'always';
  projection.orders[0].timeout = 55;
  projection.orders[0].cooldown = 9;
  projection.orders[0].urgency = 'urgent';
  linked = addSignatureOpportunity(linked, playId, projection.id, hostId, {
    skill: 'repair-beam', target: { role: 'longshot' },
  }, true);

  let current = linked.workshop.plays[0].projections[0];
  let view = signatureOpportunityViews(current, linked.orders.conditions)[0];
  assert.equal(view.host.id, hostId);
  assert.equal(view.host.when, 'always');
  assert.equal(view.host.timeout, 55);
  assert.equal(view.host.cooldown, 9);
  assert.equal(view.host.urgency, 'urgent');
  assert.deepEqual(view.continuation?.move?.to, originalTarget);
  assert.equal(view.continuation?.capacity, undefined);
  const hold = view.host.orders?.find((order) => order.move?.to === 'current-position');
  assert.equal(typeof hold?.when, 'string');
  assert.equal(linked.orders.conditions[hold!.when as string]?.fact,
    'signature-commitment-active');
  assert.ok(view.host.orders!.indexOf(hold!) < view.host.orders!.indexOf(view.continuation!));
  assert.equal(inspectWorkshop(linked.orders, linked.workshop, catalog).length, 0);

  linked = updateSignatureOpportunityUse(linked, playId, current.id, view.binding.id, {
    skill: 'repair-beam', target: 'weakest-ally',
  });
  current = linked.workshop.plays[0].projections[0];
  view = signatureOpportunityViews(current, linked.orders.conditions)[0];
  linked = renameSignatureOpportunity(linked, playId, current.id,
    view.binding.id, 'protect-wounded');
  current = linked.workshop.plays[0].projections[0];
  view = signatureOpportunityViews(current, linked.orders.conditions)[0];
  assert.equal(view.use.id, 'protect-wounded');
  assert.equal(view.use.use?.target, 'weakest-ally');

  const activation = addSignatureOpportunityActivationZone(
    linked, playId, current.id, view.binding.id);
  linked = activation.draft;
  assert.ok(linked.mapPlan.zones[activation.zoneId]);
  assert.equal(linked.workshop.plays[0].ownedGeometry.some((entry) =>
    entry.kind === 'zone' && entry.id === activation.zoneId), true);
  const activationCondition = Object.entries(linked.orders.conditions).find(([, condition]) =>
    condition.fact === 'self-in-zone' && condition.zone === activation.zoneId);
  assert.ok(activationCondition, 'the map choice must derive a visible activation Condition');
  assert.ok(conditionExpressionNames(signatureOpportunityViews(
    linked.workshop.plays[0].projections[0], linked.orders.conditions)[0].use.when)
    .includes(activationCondition![0]));

  current = linked.workshop.plays[0].projections[0];
  view = signatureOpportunityViews(current, linked.orders.conditions)[0];
  const exactTarget = addSignatureOpportunityAnchor(
    linked, playId, current.id, view.binding.id);
  linked = exactTarget.draft;
  assert.ok(exactTarget.anchorId.length <= 48,
    `generated anchor id exceeded editor contract: ${exactTarget.anchorId}`);
  assert.deepEqual(linked.mapPlan.anchors[exactTarget.anchorId], [15, 13]);
  assert.equal(linked.workshop.plays[0].ownedGeometry.some((entry) =>
    entry.kind === 'anchor' && entry.id === exactTarget.anchorId
      && entry.purpose === 'use.at for repair-beam'), true);
  current = linked.workshop.plays[0].projections[0];
  view = signatureOpportunityViews(current, linked.orders.conditions)[0];
  assert.equal(view.use.use?.at, exactTarget.anchorId);

  current = linked.workshop.plays[0].projections[0];
  view = signatureOpportunityViews(current, linked.orders.conditions)[0];
  linked = setSignatureOpportunityCommitmentHold(
    linked, playId, current.id, view.binding.id, false);
  assert.equal(linked.workshop.plays[0].projections[0].orders[0].orders
    ?.some((order) => order.move?.to === 'current-position'), false);
  assert.equal(Object.values(linked.orders.conditions)
    .some((condition) => condition.fact === 'signature-commitment-active'), false);

  current = linked.workshop.plays[0].projections[0];
  view = signatureOpportunityViews(current, linked.orders.conditions)[0];
  linked = removeSignatureOpportunity(linked, playId, current.id, view.binding.id);
  current = linked.workshop.plays[0].projections[0];
  assert.equal(signatureOpportunityViews(current).length, 0);
  assert.deepEqual(current.orders[0].orders?.[0].move?.to, originalTarget);
});

test('a canonical Post projects stand, facing, readiness and deployment maintenance into Instructions', () => {
  let linked = addBlankPlay(draft(), 'Home defence');
  const playId = linked.workshop.plays[0].id;
  linked = addBlankPlayProjection(linked, playId, 'mason');
  const projectionId = linked.workshop.plays[0].projections[0].id;
  const threat = addPlayGeometry(linked, playId, 'zone');
  linked = setPlayZoneRect(threat.draft, playId, threat.id, [13, 10, 17, 16]);
  linked = configurePlaySharedTrigger(linked, playId, {
    fact: 'visible-enemies-in-zone', operator: 'at-least', value: 2, zone: threat.id,
  });
  linked = configurePlayContingent(linked, playId, projectionId, {
    triggerConditionId: linked.workshop.plays[0].responseTriggerConditionId!,
    destination: { zone: threat.id }, requiredBodies: 0, timeout: 80,
  });
  linked = addPlayPost(linked, playId, projectionId, {
    name: 'Mason post', point: [22, 9], required: true, facing: 'east',
  });
  const slot = linked.workshop.plays[0].slots[0];
  linked = configurePlayPostDeployment(linked, playId, projectionId, slot.id, {
    skill: 'hardlight-block', target: [24, 9],
    requiredForReadiness: true, rebuildWhenMissing: true,
    refreshDurabilityAtMost: 25,
  });
  linked = configurePlayLifecycle(linked, playId, {
    minimumCommitmentTicks: 20, successConditionIds: [], maximumTicks: 80,
    cooldownTicks: 15, waiting: 'hold-posts',
  });

  const play = linked.workshop.plays[0];
  const post = play.slots[0];
  const projection = play.projections[0];
  const postHost = allOrders(projection.orders).find((order) => order.id === post.hostOrderId)!;
  assert.deepEqual(postHost.orders?.at(-1)?.move, {
    to: { anchor: post.anchorId }, posture: 'line', face: { anchor: post.facingAnchorId! },
  });
  assert.equal(postHost.orders?.at(-1)?.capacity, 1);
  assert.equal(postHost.orders?.filter((order) => order.use?.skill === 'hardlight-block').length, 2);
  assert.equal(post.deployment?.requiredForReadiness, true);
  const response = allOrders(projection.orders).find((order) =>
    order.id === `${projectionId}-respond`)!;
  const responseConditions = conditionExpressionNames(response.when);
  assert.ok(responseConditions.includes(post.readinessConditionId!));
  assert.ok(responseConditions.includes(post.deployment!.presenceConditionId!));
  assert.equal(response.timeout, 80);
  assert.equal(response.cooldown, 15);
  assert.equal(typeof response.until, 'string');
  assert.equal(linked.orders.conditions[response.until as string].fact, 'self-order-ticks');
  assert.equal(inspectWorkshop(linked.orders, linked.workshop, catalog).length, 0);
  assert.deepEqual(validateDraft(linked, catalog).filter((issue) => issue.severity === 'error'), []);

  const north = setPlayPostFacing(linked, playId, post.id, 'north');
  assert.deepEqual(north.mapPlan.anchors[post.facingAnchorId!], [22, 8]);
});

test('Fight, Core and health cards share one ordered Opportunity host and ordinary fallthrough', () => {
  let linked = addBlankPlay(draft(), 'North watch');
  const playId = linked.workshop.plays[0].id;
  linked = addBlankPlayProjection(linked, playId, 'support');
  const projectionId = linked.workshop.plays[0].projections[0].id;
  const hostId = linked.workshop.plays[0].projections[0].orders[0].id!;
  linked = addHealthRecoveryOpportunity(linked, playId, projectionId, hostId, {
    startBelow: 30, recoverUntil: 70, withinWalkingSteps: 8,
    timeout: 40, cooldown: 20, patientsPerTile: 1,
    coordinateAcrossSheet: true, allowCoreCarriers: false,
  });
  linked = addFightOpportunity(linked, playId, projectionId, hostId, {
    withinWalkingSteps: 4, targets: ['carrier', 'weakest', 'nearest'],
    style: 'advance', chaseLimit: 3, capacity: null,
  });
  linked = addCoreRecoveryOpportunity(linked, playId, projectionId, hostId, {
    withinWalkingSteps: 5, preference: 'highest-value',
    coreBankValue: { operator: 'at-least', value: 2 },
    pursuersPerCore: 1, coordinateAcrossSheet: true,
  });

  const projection = linked.workshop.plays[0].projections[0];
  const bindings = projection.signatureOpportunities!;
  assert.deepEqual(bindings.map((binding) => binding.kind),
    ['health-recovery', 'fight', 'core-recovery']);
  const host = allOrders(projection.orders).find((order) => order.id === hostId)!;
  assert.deepEqual(host.orders?.slice(0, 3).map((order) =>
    order.fight ? 'fight' : order.move?.to === 'nearest-heal-zone'
      ? 'health' : 'core'), ['health', 'fight', 'core']);
  assert.deepEqual(host.orders?.at(-1)?.move?.to, { well: 'centre' });
  const core = host.orders?.find((order) =>
    order.move?.to === 'highest-bank-value-visible-loose-core')!;
  assert.equal(core.capacity, 1);
  assert.deepEqual(core.move?.coreBankValue, { operator: 'at-least', value: 2 });
  assert.equal(inspectWorkshop(linked.orders, linked.workshop, catalog).length, 0);
  assert.deepEqual(validateDraft(linked, catalog).filter((issue) => issue.severity === 'error'), []);
});

test('opportunity ordering is the authored sibling order and malformed bindings stay diagnostics', () => {
  let linked = addBlankPlay(draft(), 'Signature order');
  linked = addBlankPlayProjection(linked, linked.workshop.plays[0].id, 'support');
  const playId = linked.workshop.plays[0].id;
  const projectionId = linked.workshop.plays[0].projections[0].id;
  const hostId = linked.workshop.plays[0].projections[0].orders[0].id!;
  linked = addSignatureOpportunity(linked, playId, projectionId, hostId,
    { skill: 'repair-beam', target: 'weakest-ally' });
  linked = addSignatureOpportunity(linked, playId, projectionId, hostId,
    { skill: 'repair-beam', target: 'nearest-ally' });
  let projection = linked.workshop.plays[0].projections[0];
  const views = signatureOpportunityViews(projection);
  assert.deepEqual(views.map((entry) => entry.use.use?.target),
    ['weakest-ally', 'nearest-ally']);
  linked = moveSignatureOpportunity(linked, playId, projectionId,
    views[1].binding.id, -1);
  projection = linked.workshop.plays[0].projections[0];
  assert.deepEqual(signatureOpportunityViews(projection).map((entry) => entry.use.use?.target),
    ['nearest-ally', 'weakest-ally']);

  const broken = structuredClone(linked.workshop);
  broken.plays[0].projections[0].signatureOpportunities![0].hostOrderId = 'missing';
  assert.match(inspectWorkshop(linked.orders, broken, catalog)[0].message,
    /missing branch/);
  broken.plays[0].projections[0].signatureOpportunities![0].hostOrderId = hostId;
  broken.plays[0].projections[0].signatureOpportunities![0].useOrderId =
    projection.orders[0].orders!.at(-1)!.id!;
  assert.match(inspectWorkshop(linked.orders, broken, catalog)[0].message,
    /direct use child/);
});

test('generated opportunity ids stay inside the player authoring contract', () => {
  let linked = addBlankPlay(draft(), 'Long generated identifiers');
  const playId = linked.workshop.plays[0].id;
  linked = addBlankPlayProjection(linked, playId, 'support');
  let projection = linked.workshop.plays[0].projections[0];
  projection.orders[0].id = 'a-very-long-player-authored-order-name-near-limi';
  linked = addSignatureOpportunity(linked, playId, projection.id,
    projection.orders[0].id, { skill: 'repair-beam', target: 'weakest-ally' }, true);
  projection = linked.workshop.plays[0].projections[0];
  const binding = projection.signatureOpportunities![0];
  const target = addSignatureOpportunityAnchor(
    linked, playId, projection.id, binding.id);
  const ids: string[] = [];
  const collect = (orders: typeof projection.orders) => orders.forEach((order) => {
    if (order.id) ids.push(order.id);
    if (order.orders) collect(order.orders);
  });
  collect(target.draft.workshop.plays[0].projections[0].orders);
  ids.push(target.anchorId);
  assert.ok(ids.every((id) => id.length <= 48 && /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(id)),
    `invalid generated ids: ${ids.join(', ')}`);
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

test('one Program reorder moves every participant Instruction block together', () => {
  let linked = addBlankPlay(draft(), 'First response');
  const firstId = linked.workshop.plays[0].id;
  linked = addBlankPlayProjection(linked, firstId, 'support');
  linked = addBlankPlayProjection(linked, firstId, 'reserve');
  linked = addBlankPlay(linked, 'Second response');
  const secondId = linked.workshop.plays[1].id;
  linked = addBlankPlayProjection(linked, secondId, 'support');
  linked = addBlankPlayProjection(linked, secondId, 'reserve');

  const roots = (roleId: string) => linked.orders.roles
    .find((entry) => entry.id === roleId)!.orders.map((entry) => entry.id);
  assert.deepEqual(roots('support'), [
    `${firstId}-support-move`, `${secondId}-support-move`, 'support-baseline',
  ]);
  assert.deepEqual(roots('reserve'), [
    `${firstId}-reserve-move`, `${secondId}-reserve-move`, 'reserve-baseline',
  ]);

  linked = moveProgram(linked, secondId, -1);
  assert.deepEqual(linked.workshop.plays.map((entry) => entry.id), [secondId, firstId]);
  assert.deepEqual(roots('support'), [
    `${secondId}-support-move`, `${firstId}-support-move`, 'support-baseline',
  ]);
  assert.deepEqual(roots('reserve'), [
    `${secondId}-reserve-move`, `${firstId}-reserve-move`, 'reserve-baseline',
  ]);
  assert.equal(inspectWorkshop(linked.orders, linked.workshop, catalog).length, 0);
});

test('Program materialization filters by participant and ignores legacy insertion points', () => {
  let linked = addBlankPlay(draft(), 'Support only');
  const firstId = linked.workshop.plays[0].id;
  linked = addBlankPlayProjection(linked, firstId, 'support');
  linked.workshop.plays[0].projections[0].placement = {
    beforeOrderKey: 'id:support-baseline',
  };
  linked = addBlankPlay(linked, 'Reserve only');
  const secondId = linked.workshop.plays[1].id;
  linked = addBlankPlayProjection(linked, secondId, 'reserve');
  const result = materializeWorkshop(linked.orders, linked.workshop);
  assert.equal(result.drifts.length, 0);
  assert.deepEqual(result.orders.roles.find((entry) => entry.id === 'support')!
    .orders.map((entry) => entry.id), [`${firstId}-support-move`, 'support-baseline']);
  assert.deepEqual(result.orders.roles.find((entry) => entry.id === 'reserve')!
    .orders.map((entry) => entry.id), [`${secondId}-reserve-move`, 'reserve-baseline']);
});

test('a blank Program derives its shared trigger and refines contingents without rebuilding Instructions', () => {
  let linked = addBlankPlay(draft(), 'Generic fork');
  const playId = linked.workshop.plays[0].id;
  linked = addBlankPlayProjection(linked, playId, 'reserve');
  linked = addBlankPlayProjection(linked, playId, 'relay-a');
  const north = addPlayGeometry(linked, playId, 'zone');
  linked = setPlayZoneRect(north.draft, playId, north.id, [20, 5, 23, 8]);
  const watch = addPlayGeometry(linked, playId, 'zone');
  linked = setPlayZoneRect(watch.draft, playId, watch.id, [13, 10, 17, 16]);
  const route = addPlayGeometry(linked, playId, 'route');
  linked = appendPlayRouteWaypoint(route.draft, playId, route.id, [24, 13]);
  linked = appendPlayRouteWaypoint(linked, playId, route.id, [18, 13]);
  linked = appendPlayRouteWaypoint(linked, playId, route.id, [13, 13]);
  linked = configurePlaySharedTrigger(linked, playId, {
    fact: 'visible-enemies-in-zone', operator: 'at-least', value: 5,
    zone: watch.id,
  });
  const triggerId = linked.workshop.plays[0].responseTriggerConditionId!;
  const play = linked.workshop.plays[0];
  const reserveProjection = play.projections.find((entry) => entry.roleId === 'reserve')!;
  const relayProjection = play.projections.find((entry) => entry.roleId === 'relay-a')!;
  linked = configurePlayContingent(linked, playId, reserveProjection.id, {
    triggerConditionId: triggerId,
    destination: { zone: north.id },
    requiredBodies: 2,
    timeout: 90,
  });
  linked = configurePlayContingent(linked, playId, relayProjection.id, {
    triggerConditionId: triggerId,
    destination: { route: route.id },
    requiredBodies: 0,
    timeout: 45,
  });

  let current = linked.workshop.plays[0];
  assert.equal(current.formations?.length, 1);
  const readiness = linked.orders.conditions[current.formations![0].conditionId];
  assert.deepEqual(readiness, {
    fact: 'role-in-zone-count', operator: 'at-least', value: 2,
    role: 'reserve', zone: north.id,
  });
  const reserveResponse = current.projections.find((entry) =>
    entry.id === reserveProjection.id)!.orders[0];
  assert.deepEqual(reserveResponse.when, { all: [triggerId, current.formations![0].conditionId] });
  assert.equal(reserveResponse.timeout, 90);
  const relayResponse = current.projections.find((entry) =>
    entry.id === relayProjection.id)!.orders[0];
  assert.equal(relayResponse.when, triggerId);
  assert.equal(relayResponse.orders?.[0].move?.patrol, route.id);

  // Refinement updates the existing source ids and retains nested authored work.
  const responseId = reserveResponse.id!;
  linked = configurePlaySharedTrigger(linked, playId, {
    ...linked.orders.conditions[triggerId], value: 4,
  });
  linked = configurePlayContingent(linked, playId, reserveProjection.id, {
    triggerConditionId: triggerId,
    destination: { zone: north.id },
    requiredBodies: 1,
    timeout: 120,
  });
  current = linked.workshop.plays[0];
  assert.equal(current.projections.find((entry) => entry.id === reserveProjection.id)!
    .orders[0].id, responseId);
  assert.equal(linked.orders.conditions[current.formations![0].conditionId].value, 1);
  assert.equal(inspectWorkshop(linked.orders, linked.workshop, catalog).length, 0);
});

test('mixed-role readiness compiles one visible 5/5 ALL gate without manual wiring', () => {
  let linked = addBlankPlay(draft(), 'Mixed return ambush');
  const playId = linked.workshop.plays[0].id;
  for (const roleId of ['mason', 'longshot', 'hush', 'reserve'])
    linked = addBlankPlayProjection(linked, playId, roleId);
  const ambush = addPlayGeometry(linked, playId, 'zone');
  linked = setPlayZoneRect(ambush.draft, playId, ambush.id, [19, 15, 23, 19]);
  const watch = addPlayGeometry(linked, playId, 'zone');
  linked = setPlayZoneRect(watch.draft, playId, watch.id, [13, 10, 17, 16]);
  linked = configurePlaySharedTrigger(linked, playId, {
    fact: 'visible-enemies-in-zone', operator: 'at-least', value: 5,
    zone: watch.id,
  });
  const triggerId = linked.workshop.plays[0].responseTriggerConditionId!;
  const requiredByRole = new Map([
    ['mason', 1], ['longshot', 1], ['hush', 1], ['reserve', 2],
  ]);
  for (const projection of [...linked.workshop.plays[0].projections]) {
    linked = configurePlayContingent(linked, playId, projection.id, {
      triggerConditionId: triggerId,
      destination: { zone: ambush.id },
      requiredBodies: requiredByRole.get(projection.roleId)!,
      timeout: 90,
    });
  }
  const projectionIds = linked.workshop.plays[0].projections.map((entry) => entry.id);
  linked = setPlayReadinessGroup(
    linked, playId, null, 'Return ambush', projectionIds);

  const play = linked.workshop.plays[0];
  const readinessIds = play.formations!.map((entry) => entry.conditionId);
  assert.equal(readinessIds.reduce((sum, id) =>
    sum + linked.orders.conditions[id].value, 0), 5);
  assert.equal(Object.keys(linked.orders.conditions).length, 9,
    'four explicit not-ready Conditions keep preparation from re-entering once ready');
  assert.deepEqual(play.readinessGroups, [{
    id: 'return-ambush', name: 'Return ambush', projectionIds,
  }]);
  for (const projection of play.projections)
    assert.deepEqual(projection.orders[0].when, {
      all: [triggerId, ...readinessIds],
    });
  assert.equal(inspectWorkshop(linked.orders, linked.workshop, catalog).length, 0);

  linked = removePlayReadinessGroup(linked, playId, 'return-ambush');
  for (const projection of linked.workshop.plays[0].projections) {
    const own = linked.workshop.plays[0].formations!.find((entry) =>
      entry.projectionId === projection.id)!.conditionId;
    assert.deepEqual(projection.orders[0].when, { all: [triggerId, own] });
  }
});

test('Practice diagnostics correlate ordinary Order reasons without replay Play state', () => {
  let linked = addBlankPlay(draft(), 'Evidence');
  const playId = linked.workshop.plays[0].id;
  linked = addBlankPlayProjection(linked, playId, 'support');
  let projection = linked.workshop.plays[0].projections[0];
  const hostId = projection.orders[0].id!;
  linked = addSignatureOpportunity(linked, playId, projection.id, hostId,
    { skill: 'repair-beam', target: { role: 'reserve' } });
  projection = linked.workshop.plays[0].projections[0];
  const binding = projection.signatureOpportunities![0];
  const activation = addSignatureOpportunityActivationZone(
    linked, playId, projection.id, binding.id);
  linked = setPlayZoneRect(
    activation.draft, playId, activation.zoneId, [8, 8, 10, 10]);
  projection = linked.workshop.plays[0].projections[0];
  const useId = projection.signatureOpportunities![0].useOrderId;
  const continueId = signatureOpportunityViews(projection)[0].continuation!.id!;
  const actor = (tickPosition: [number, number]) => ({
    identity: { teamId: 0, unitId: 7, lifeId: 0 },
    position: { x: tickPosition[0], y: tickPosition[1] },
  });
  const replay = {
    initialWorld: { actors: [actor([9, 9])] },
    ticks: [
      {
        tick: 10,
        before: { actors: [actor([9, 9])] },
        after: { actors: [actor([9, 9])] },
        actorTurns: [{
          actor: { teamId: 0, unitId: 7, lifeId: 0 },
          runtimeReply: { debugMessage:
            `orders:support > ${hostId} > ${useId}:use:repair-beam:body:0:1:0` },
          acceptedDecision: { debugMessage: null },
        }],
        events: [{
          arcRelayFact: {
            kind: 'signature-changed', signatureId: 'repair-beam',
            ownerActor: { teamId: 0, unitId: 7, lifeId: 0 },
          },
        }],
      },
      {
        tick: 11,
        before: { actors: [actor([9, 9])] },
        after: { actors: [actor([10, 9])] },
        actorTurns: [{
          actor: { teamId: 0, unitId: 7, lifeId: 0 },
          runtimeReply: { debugMessage:
            `orders:support > ${hostId} > ${continueId}:march@12,9` },
          acceptedDecision: { debugMessage: null },
        }],
        events: [],
      },
    ],
  } as unknown as ReplayModel;

  const summary = signatureOpportunityPracticeSummary(
    replay, linked, projection, projection.signatureOpportunities![0]);
  assert.equal(summary?.acceptedUses, 1);
  assert.equal(summary?.evaluations, 1);
  assert.equal(summary?.signatureEvents, 1);
  assert.deepEqual(
    [summary?.firstActivationTick, summary?.lastActivationTick], [0, 11]);
  assert.equal(summary?.continuation, continueId);
  assert.deepEqual(summary?.routeContinuity, {
    before: { x: 9, y: 9 }, after: { x: 10, y: 9 }, tick: 10,
  });
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
