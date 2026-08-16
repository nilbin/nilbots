#!/usr/bin/env node

import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import {
  parseDraft,
  stringifyDocument,
  type MapPlanDocument,
  type OrderNode,
  type OrdersDocument,
  type SheetDraft,
} from '../web/src/site/sheets/sheetAuthoring.ts';
import {
  addCoreRecoveryOpportunity,
  addFightOpportunity,
  addHealthRecoveryOpportunity,
  addPlayCondition,
  addPlayGeometry,
  addPlayPost,
  addSignatureOpportunity,
  addSignatureOpportunityActivationZone,
  appendPlayRouteWaypoint,
  configurePlayContingent,
  configurePlayLifecycle,
  configurePlayPostDeployment,
  configurePlaySharedTrigger,
  setPlayReadinessGroup,
  setPlayZoneRect,
  setSignatureOpportunityWhen,
  updatePlayProjectionOrders,
  updateSignatureOpportunityUse,
} from '../web/src/site/sheets/playEditing.ts';
import {
  addBlankPlay,
  addBlankPlayProjection,
  addRelayChainPlay,
} from '../web/src/site/sheets/playRecipes.ts';

const output = resolve(process.argv[2] ?? '/tmp/arc-player-ready-programs');
mkdirSync(output, { recursive: true });

const midRoles = [
  role('home-mason', 'mason'),
  role('home-lantern', 'lantern'),
  role('return-mason', 'mason'),
  role('return-rail', 'longshot'),
  role('return-hush', 'hush'),
  role('return-medic', 'patchbay'),
  role('return-breaker', 'repulsor'),
  role('raider', 'kestrel'),
];
let mid = blank('mid-overload-fork', midRoles);
let bank;
({ draft: mid, playId: bank } = addBankProgram(mid, 'Bank carried Core'));
mid = buildMidOverload(mid);
mid = addUsefulFallback(mid, bank, 'Field work');
writeStarter('mid-overload-fork-v1', mid);

const relayRoles = [
  role('relay-a', 'relay'),
  role('relay-b', 'relay'),
  role('hook', 'towline'),
  role('raider', 'kestrel'),
  role('guard', 'palisade'),
  role('medic', 'patchbay'),
  role('dampener', 'hush'),
  role('scout', 'lantern'),
];
let relay = blank('running-relay', relayRoles);
relay = addRelayChainPlay(relay, {
  relayARoleId: 'relay-a', relayBRoleId: 'relay-b',
}, 'Running Relay');
let relayBank;
({ draft: relay, playId: relayBank } = addBankProgram(relay, 'Bank carried Core'));
relay = addUsefulFallback(relay, relayBank, 'Open-field fallback');
writeStarter('running-relay-v1', relay);
writeSoloRelayControl();

writeDrill();

function buildMidOverload(source: SheetDraft) {
  let draft = addNamedBlankPlay(source, 'Mid Overload Fork', 'mid-fork');
  const playId = draft.workshop.plays.at(-1)!.id;
  for (const entry of midRoles)
    draft = addBlankPlayProjection(draft, playId, entry.id);

  const midZone = geometry(draft, playId, 'zone');
  draft = setPlayZoneRect(midZone.draft, playId, midZone.id, [20, 9, 29, 23]);
  const homeKillzone = geometry(draft, playId, 'zone');
  draft = setPlayZoneRect(homeKillzone.draft, playId, homeKillzone.id, [9, 5, 13, 9]);
  const returnKillzone = geometry(draft, playId, 'zone');
  draft = setPlayZoneRect(returnKillzone.draft, playId, returnKillzone.id, [19, 11, 25, 15]);
  const raidRoute = geometry(draft, playId, 'route');
  draft = raidRoute.draft;
  for (const point of [
    [2, 5], [2, 7], [3, 8], [4, 9], [4, 11], [5, 12],
    [5, 14], [4, 15], [4, 16], [4, 17], [3, 19], [3, 23],
    [3, 25], [5, 25], [11, 25], [16, 25], [25, 23],
    [24, 23], [24, 22], [23, 21], [22, 22], [22, 23],
    [19, 20],
  ] as Array<[number, number]>)
    draft = appendPlayRouteWaypoint(draft, playId, raidRoute.id, point);

  draft = configurePlaySharedTrigger(draft, playId, {
    fact: 'remembered-enemies-in-zone', operator: 'at-least', value: 5,
    zone: midZone.id, freshnessTicks: 45,
  });
  const triggerId = currentPlay(draft, playId).responseTriggerConditionId!;
  const home = ['home-mason', 'home-lantern'];
  const returning = [
    'return-mason', 'return-rail', 'return-hush', 'return-medic', 'return-breaker',
  ];
  const postPoints: Record<string, [number, number]> = {
    'home-mason': [11, 7], 'home-lantern': [10, 7],
    'return-mason': [20, 13], 'return-rail': [21, 14],
    'return-hush': [22, 12], 'return-medic': [23, 12],
    'return-breaker': [24, 13],
  };
  const postFacings: Record<string, 'north' | 'east' | 'south' | 'west'> = {
    'home-mason': 'north', 'home-lantern': 'north',
    'return-mason': 'north', 'return-rail': 'west',
    'return-hush': 'north', 'return-medic': 'north',
    'return-breaker': 'east',
  };
  const projections = () => currentPlay(draft, playId).projections;
  for (const roleId of [...home, ...returning]) {
    let projection = projections().find((entry) => entry.roleId === roleId)!;
    draft = configurePlayContingent(draft, playId, projection.id, {
      triggerConditionId: triggerId,
      activation: 'program-trigger',
      destination: { zone: home.includes(roleId) ? homeKillzone.id : returnKillzone.id },
      requiredBodies: 0,
      timeout: 110,
    });
    projection = projections().find((entry) => entry.roleId === roleId)!;
    draft = addPlayPost(draft, playId, projection.id, {
      name: roleId.replace(/-/g, ' '), point: postPoints[roleId],
      required: true, facing: postFacings[roleId],
    });
  }

  for (const [roleId, target] of [
    ['home-mason', [12, 7]],
    ['return-mason', [19, 13]],
  ] as Array<[string, [number, number]]>) {
    let play = currentPlay(draft, playId);
    const projection = play.projections.find((entry) => entry.roleId === roleId)!;
    const slot = play.slots.find((entry) => entry.roleId === roleId)!;
    draft = configurePlayPostDeployment(draft, playId, projection.id, slot.id, {
      skill: 'hardlight-block', target, requiredForReadiness: true,
      rebuildWhenMissing: true, refreshDurabilityAtMost: 25,
    });
  }

  // A known/remembered mid trigger still needs vision. The home Lantern keeps
  // one ordinary Post-local Survey Flare over the watched area; this is not a
  // hidden Program sensor or drill-only omniscience.
  const flareTarget = addPlayGeometry(draft, playId, 'anchor');
  draft = flareTarget.draft;
  draft.mapPlan.anchors[flareTarget.id] = [17, 14];
  let homeLantern = projections().find((entry) =>
    entry.roleId === 'home-lantern')!;
  const homeLanternPost = currentPlay(draft, playId).slots.find((entry) =>
    entry.roleId === 'home-lantern')!;
  const atPost = addPlayCondition(draft, playId, 'home-lantern-at-post', {
    fact: 'self-in-zone', operator: 'equals', value: 1,
    zone: homeLanternPost.readinessZoneId,
  });
  draft = atPost.draft;
  draft = addSignatureOpportunity(
    draft, playId, homeLantern.id, homeLanternPost.hostOrderId!, {
      skill: 'survey-flare', target: 'self', at: flareTarget.id,
    });
  homeLantern = projections().find((entry) =>
    entry.roleId === 'home-lantern')!;
  const flare = homeLantern.signatureOpportunities!.find((entry) =>
    entry.hostOrderId === homeLanternPost.hostOrderId)!;
  draft = setSignatureOpportunityWhen(
    draft, playId, homeLantern.id, flare.id, atPost.id);

  draft = setPlayReadinessGroup(draft, playId, null, 'Home defence ready',
    home.map((roleId) => projections().find((entry) => entry.roleId === roleId)!.id), 3);
  draft = setPlayReadinessGroup(draft, playId, null, 'Return ambush ready',
    returning.map((roleId) => projections().find((entry) => entry.roleId === roleId)!.id), 6);

  const success = addPlayCondition(draft, playId, `${playId}-response-success`, {
    fact: 'confirmed-enemy-destructions-in-zone', operator: 'at-least', value: 3,
    zone: returnKillzone.id, since: 'node-entry',
  });
  draft = configurePlayLifecycle(success.draft, playId, {
    minimumCommitmentTicks: 25,
    successConditionIds: [success.id],
    maximumTicks: 110,
    cooldownTicks: 20,
    waiting: 'hold-posts',
  });

  for (const roleId of [...home, ...returning]) {
    const projection = currentPlay(draft, playId).projections.find((entry) =>
      entry.roleId === roleId)!;
    const zone = home.includes(roleId) ? homeKillzone.id : returnKillzone.id;
    const orders = structuredClone(projection.orders);
    const response = find(orders, `${projection.id}-respond`)!;
    const continuation = response.orders!.find((entry) => entry.fight)!;
    continuation.fight = {
      targets: ['carrier', 'weakest', 'nearest'], style: 'hold', chaseLimit: 5, zone,
    };
    continuation.capacity = 2;
    draft = updatePlayProjectionOrders(draft, playId, projection.id, orders);
  }

  // The response is above the Post duty. Mirror each Mason's visible
  // maintenance opportunity into that response so a destroyed wall is rebuilt
  // without inventing response state.
  for (const roleId of ['home-mason', 'return-mason']) {
    let play = currentPlay(draft, playId);
    let projection = play.projections.find((entry) => entry.roleId === roleId)!;
    const slot = play.slots.find((entry) => entry.roleId === roleId)!;
    const responseId = `${projection.id}-respond`;
    const existingBindings = new Set((projection.signatureOpportunities ?? [])
      .map((entry) => entry.id));
    draft = addSignatureOpportunity(draft, playId, projection.id, responseId, {
      skill: 'hardlight-block', target: 'self', at: slot.deployment!.targetAnchorId,
    });
    play = currentPlay(draft, playId);
    projection = play.projections.find((entry) => entry.roleId === roleId)!;
    const binding = projection.signatureOpportunities!.find((entry) =>
      entry.hostOrderId === responseId && !existingBindings.has(entry.id))!;
    const source = slot.deployment!.sourceConditionId;
    const missing = slot.deployment!.ownedConditionIds.find((id) =>
      draft.orders.conditions[id]?.fact === 'self-deployment-in-zone'
        && draft.orders.conditions[id].value === 0)!;
    draft = setSignatureOpportunityWhen(draft, playId, projection.id, binding.id,
      { all: [source, missing] });
  }

  // The raid is part of the same Program but explicitly independent of the
  // shared ambush trigger. Its response is still ordinary Instructions.
  let raid = projections().find((entry) => entry.roleId === 'raider')!;
  draft = configurePlayContingent(draft, playId, raid.id, {
    triggerConditionId: triggerId,
    activation: 'immediate',
    destination: { route: raidRoute.id }, requiredBodies: 0, timeout: 220,
  });
  raid = projections().find((entry) => entry.roleId === 'raider')!;
  let raidOrders = structuredClone(raid.orders);
  const raidResponse = find(raidOrders, `${raid.id}-respond`)!;
  const raidContinuation = raidResponse.orders!.find((entry) => entry.move)!;
  raidContinuation.move = {
    to: { well: 'enemy' }, via: raidRoute.id, posture: 'front',
  };
  delete raidResponse.until;
  raidResponse.timeout = 220;
  raidResponse.cooldown = 0;
  draft = updatePlayProjectionOrders(draft, playId, raid.id, raidOrders);
  raid = projections().find((entry) => entry.roleId === 'raider')!;
  draft = addSignatureOpportunity(draft, playId, raid.id, `${raid.id}-respond`, {
    skill: 'vector-dash',
    target: { route: raidRoute.id, progress: 'forward', minimumForwardTiles: 2 },
  });
  raid = projections().find((entry) => entry.roleId === 'raider')!;
  const dash = raid.signatureOpportunities!.find((entry) =>
    entry.hostOrderId === `${raid.id}-respond`)!;
  const window = addSignatureOpportunityActivationZone(
    draft, playId, raid.id, dash.id);
  draft = setPlayZoneRect(window.draft, playId, window.zoneId, [3, 23, 27, 25]);
  draft = updateSignatureOpportunityUse(draft, playId, raid.id, dash.id, {
    skill: 'vector-dash',
    target: { route: raidRoute.id, progress: 'forward', minimumForwardTiles: 2 },
  });
  return draft;
}

function addBankProgram(source: SheetDraft, name: string) {
  let draft = addNamedBlankPlay(source, name, 'bank-core');
  const playId = draft.workshop.plays.at(-1)!.id;
  let condition = addPlayCondition(draft, playId, `${playId}-carrying`, {
    fact: 'self-carrying', operator: 'equals', value: 1,
  });
  draft = condition.draft;
  const carrying = condition.id;
  condition = addPlayCondition(draft, playId, `${playId}-empty`, {
    fact: 'self-carrying', operator: 'equals', value: 0,
  });
  draft = condition.draft;
  const empty = condition.id;
  for (const entry of draft.orders.roles) {
    draft = addBlankPlayProjection(draft, playId, entry.id);
    const projection = currentPlay(draft, playId).projections.find((value) =>
      value.roleId === entry.id)!;
    draft = updatePlayProjectionOrders(draft, playId, projection.id, [{
      id: `${projection.id}-bank`, when: carrying, until: empty,
      urgency: 'urgent', move: { to: 'own-reactor' },
    }]);
  }
  return { draft, playId };
}

function addUsefulFallback(source: SheetDraft, bankProgramId: string, name: string) {
  let draft = addNamedBlankPlay(source, name,
    name === 'Field work' ? 'field-work' : 'open-field');
  const playId = draft.workshop.plays.at(-1)!.id;
  const wells = ['own', 'centre', 'enemy'] as const;
  for (const [index, entry] of draft.orders.roles.entries()) {
    draft = addBlankPlayProjection(draft, playId, entry.id);
    let projection = currentPlay(draft, playId).projections.find((value) =>
      value.roleId === entry.id)!;
    const hostId = `${projection.id}-field-work`;
    draft = updatePlayProjectionOrders(draft, playId, projection.id, [{
      id: hostId,
      move: { to: { well: wells[index % wells.length] }, posture: index < 3 ? 'front' : 'line' },
    }]);
    projection = currentPlay(draft, playId).projections.find((value) =>
      value.roleId === entry.id)!;
    draft = addCoreRecoveryOpportunity(draft, playId, projection.id, hostId, {
      withinWalkingSteps: 5, preference: 'highest-value',
      pursuersPerCore: 1, coordinateAcrossSheet: true,
      linkedBankProgramId: bankProgramId,
    });
    draft = addHealthRecoveryOpportunity(draft, playId, projection.id, hostId, {
      startBelow: 35, recoverUntil: 70, withinWalkingSteps: 8,
      timeout: 55, cooldown: 20, patientsPerTile: 1,
      coordinateAcrossSheet: true, allowCoreCarriers: false,
    });
    draft = addFightOpportunity(draft, playId, projection.id, hostId, {
      withinWalkingSteps: 6,
      targets: ['carrier', 'weakest', 'nearest'],
      style: index % 3 === 0 ? 'hold' : 'advance', chaseLimit: 5,
      capacity: 3,
    });
  }
  return draft;
}

function writeDrill() {
  const drillOrders: OrdersDocument = {
    schema: 'arc-relay-orders-v2', sheetId: 'mid-overload-drill-v1',
    mapPlan: '../map-plans/tapline-6/mid-overload-drill-v1.json',
    roles: [
      role('wave-a', 'palisade'), role('wave-b', 'palisade'),
      role('pressure', 'kestrel'), role('line', 'hush'),
      role('support', 'patchbay'), role('scout', 'lantern'),
      role('hook', 'towline'), role('exchange', 'switchback'),
    ].map((entry) => ({ ...entry, orders: [
      {
        id: `${entry.id}-wait-for-posts`, until: 'posts-have-time',
        cooldown: 600, move: { to: 'current-position' },
      },
      {
        id: `${entry.id}-enter-mid`, timeout: 110,
        cooldown: 600, move: { to: { well: 'centre' }, posture: 'front' },
      },
      {
        id: `${entry.id}-return-through-ambush`,
        move: { to: 'own-reactor', posture: 'front' },
      },
    ] })),
    sharedOrders: {}, capacities: {}, conditions: {
      'posts-have-time': {
        fact: 'self-order-ticks', operator: 'at-least', value: 50,
      },
    },
  };
  const drillMap: MapPlanDocument = {
    schema: 'arc-relay-map-plan-v2', mapPlanId: 'mid-overload-drill-v1',
    mapId: 'arc-relay-tapline-02',
    zones: {},
    anchors: {}, routes: {},
  };
  write('playbooks/mid-overload-drill-v1.json', drillOrders);
  write('map-plans/tapline-6/mid-overload-drill-v1.json', drillMap);
}

function writeSoloRelayControl() {
  let solo = blank('running-relay-solo', relayRoles);
  const routeId = 'relay-chain-return-corridor';
  const pickupAnchor = 'relay-chain-enemy-well-pickup';
  const homeAnchor = 'relay-chain-home-reactor';
  solo.mapPlan.routes[routeId] = structuredClone(
    relay.mapPlan.routes[routeId]);
  solo.mapPlan.anchors[pickupAnchor] = structuredClone(
    relay.mapPlan.anchors[pickupAnchor]);
  solo.mapPlan.anchors[homeAnchor] = structuredClone(
    relay.mapPlan.anchors[homeAnchor]);

  solo = addNamedBlankPlay(solo, 'Solo corridor control', 'solo-corridor');
  const playId = 'solo-corridor';
  let condition = addPlayCondition(solo, playId, 'solo-carrying', {
    fact: 'self-carrying', operator: 'equals', value: 1,
  });
  solo = condition.draft;
  const carrying = condition.id;
  condition = addPlayCondition(solo, playId, 'solo-empty', {
    fact: 'self-carrying', operator: 'equals', value: 0,
  });
  solo = condition.draft;
  const empty = condition.id;
  solo = addBlankPlayProjection(solo, playId, 'relay-a');
  const projection = currentPlay(solo, playId).projections[0];
  solo = updatePlayProjectionOrders(solo, playId, projection.id, [
    {
      id: 'solo-corridor-return', when: carrying, until: empty,
      urgency: 'normal',
      move: { to: { anchor: homeAnchor }, via: routeId },
    },
    {
      id: 'solo-corridor-take-start',
      move: { to: { anchor: pickupAnchor } },
    },
  ]);
  currentPlay(solo, playId).ownedGeometry.push(
    { kind: 'route', id: routeId, purpose: 'same movement route as Running Relay' },
    { kind: 'anchor', id: pickupAnchor, purpose: 'move.to' },
    { kind: 'anchor', id: homeAnchor, purpose: 'move.to' },
  );

  let bankId;
  ({ draft: solo, playId: bankId } = addBankProgram(
    solo, 'Bank carried Core'));
  solo = addUsefulFallback(solo, bankId, 'Open-field fallback');
  writeStarter('running-relay-solo-v1', solo);
}

function blank(sheetId: string, roles: ReturnType<typeof role>[]) {
  const orders: OrdersDocument = {
    schema: 'arc-relay-orders-v2', sheetId, mapPlan: 'map-plan.json',
    roles, sharedOrders: {}, capacities: {}, conditions: {},
  };
  const map: MapPlanDocument = {
    schema: 'arc-relay-map-plan-v2', mapPlanId: sheetId,
    mapId: 'arc-relay-tapline-02', zones: {}, routes: {}, anchors: {},
  };
  return parseDraft(sheetId.replace(/-/g, ' '), null, null,
    stringifyDocument(orders), stringifyDocument(map), false);
}

function role(id: string, classId: string, copies = 1) {
  return {
    id, classes: Array.from({ length: copies }, () => classId),
    upgradePath: ['reach' as const, 'vitality' as const],
    orders: [{ id: `${id}-park`, move: { to: 'own-reactor' as const } }],
  };
}

function geometry(draft: SheetDraft, playId: string, kind: 'zone' | 'route' | 'anchor') {
  return addPlayGeometry(draft, playId, kind);
}

function addNamedBlankPlay(source: SheetDraft, name: string, id: string) {
  const draft = addBlankPlay(source, name);
  const play = draft.workshop.plays.at(-1)!;
  if (draft.workshop.plays.some((entry) => entry !== play && entry.id === id))
    throw new Error(`Duplicate generated Program id '${id}'.`);
  play.id = id;
  return draft;
}

function currentPlay(draft: SheetDraft, playId: string) {
  return draft.workshop.plays.find((entry) => entry.id === playId)!;
}

function find(orders: OrderNode[], id: string): OrderNode | undefined {
  for (const order of orders) {
    if (order.id === id) return order;
    const nested = find(order.orders ?? [], id);
    if (nested) return nested;
  }
  return undefined;
}

function writeStarter(id: string, draft: SheetDraft) {
  const orders = structuredClone(draft.orders);
  orders.sheetId = id;
  orders.mapPlan = `../map-plans/tapline-6/${id}.json`;
  const map = structuredClone(draft.mapPlan);
  map.mapPlanId = id;
  write(`playbooks/${id}.json`, orders);
  write(`map-plans/tapline-6/${id}.json`, map);
  write(`workshops/${id}.json`, draft.workshop);
}

function write(name: string, value: unknown) {
  const path = resolve(output, name);
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, `${JSON.stringify(value, null, 2)}\n`);
}
