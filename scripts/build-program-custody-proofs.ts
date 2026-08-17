#!/usr/bin/env node

import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import {
  parseDraft,
  stringifyDocument,
  type MapPlanDocument,
  type OrdersDocument,
} from '../web/src/site/sheets/sheetAuthoring.ts';
import {
  addDropOpportunity,
  addHealthRecoveryOpportunity,
  addPlayGeometry,
  addProgramCustodyCase,
  appendPlayRouteWaypoint,
  configurePlayLifecycle,
  configureProgramCustody,
  configureProgramCustodyCompletion,
  configureProgramCustodyHandoff,
  updatePlayProjectionOrders,
} from '../web/src/site/sheets/playEditing.ts';
import {
  addBlankPlay,
  addBlankPlayProjection,
} from '../web/src/site/sheets/playRecipes.ts';

const output = resolve(process.argv[2] ?? '/tmp/arc-program-custody-proofs');
mkdirSync(output, { recursive: true });

// This proof is deliberately authored through the same Program helpers as the
// visible editor. Its runner walks to the own Well without a Core-recovery
// opportunity, physically acquires the 1-point Core incidentally, drops it
// through a value/source-filtered local custody branch, and exits the durable
// Program on the correlated drop event. A long cooldown makes the single
// episode inspectable without manufacturing a hidden pickup veto.
let unwanted = blank();
unwanted = addBlankPlay(unwanted, 'Reject an incidental own Core');
const play = unwanted.workshop.plays[0];
play.id = 'reject-incidental-own-core';
unwanted = addBlankPlayProjection(unwanted, play.id, 'runner');
const projection = unwanted.workshop.plays[0].projections[0];
const destination = addPlayGeometry(unwanted, play.id, 'anchor');
unwanted = destination.draft;
unwanted.mapPlan.anchors[destination.id] = [11, 6];
unwanted = updatePlayProjectionOrders(unwanted, play.id, projection.id, [{
  id: 'reject-incidental-own-core-approach',
  move: { to: { anchor: destination.id } },
}]);
unwanted = configureProgramCustody(unwanted, play.id, projection.id, {
  kind: 'carrier-return', carrierRoleIds: ['runner'],
});
let currentPlay = unwanted.workshop.plays[0];
let currentProjection = currentPlay.projections[0];
const carrierBranch = currentPlay.coreCustody!.bindings.find((entry) =>
  entry.projectionId === currentProjection.id && entry.kind === 'carrier-return')!;
unwanted = addDropOpportunity(
  unwanted, play.id, currentProjection.id, carrierBranch.actionOrderIds[0], {
    coreBankValue: { operator: 'equals', value: 1 },
    well: 'own',
  });
unwanted = configureProgramCustodyCompletion(
  unwanted, play.id, ['runner'], ['drop']);
currentPlay = unwanted.workshop.plays[0];
unwanted = configurePlayLifecycle(unwanted, play.id, {
  minimumCommitmentTicks: 0,
  successConditionIds: [currentPlay.coreCustody!.completionConditionId!],
  maximumTicks: 120,
  cooldownTicks: 1200,
  waiting: 'fall-through',
});

write('unwanted-core-proof.json', unwanted.orders);
write('unwanted-core-proof-map.json', unwanted.mapPlan);
write('unwanted-core-proof-workshop.json', unwanted.workshop);

// A separate proof—not a refinement of Running Relay—uses two couriers to
// exercise the exact Program case semantics. Courier A acquires the 4-point
// Core and names Courier B, which has carrier behavior in the same case. The
// receiver inherits the same premium route. The escort follows
// the role-scoped current carrier, so a transfer retarget is observable.
let transfer = programProofBlank('premium-transfer-proof');
transfer = addBlankPlay(transfer, 'Premium transfer proof');
const transferPlayId = transfer.workshop.plays[0].id;
transfer = addBlankPlayProjection(transfer, transferPlayId, 'courier-a');
transfer = addBlankPlayProjection(transfer, transferPlayId, 'courier-b');
transfer = addBlankPlayProjection(transfer, transferPlayId, 'escort');
const transferPlay = () => transfer.workshop.plays[0];
const transferProjection = (roleId: string) => transferPlay().projections
  .find((entry) => entry.roleId === roleId)!;
transfer = updatePlayProjectionOrders(
  transfer, transferPlayId, transferProjection('courier-a').id, [{
    id: 'premium-courier-a-steal',
    move: { to: { well: 'enemy' } },
  }]);
const receiverPost = addPlayGeometry(transfer, transferPlayId, 'anchor');
transfer = receiverPost.draft;
transfer.mapPlan.anchors[receiverPost.id] = [18, 19];
transfer = updatePlayProjectionOrders(
  transfer, transferPlayId, transferProjection('courier-b').id, [{
    id: 'premium-courier-b-stage',
    move: { to: { anchor: receiverPost.id } },
  }]);
transfer = updatePlayProjectionOrders(
  transfer, transferPlayId, transferProjection('escort').id, [{
    id: 'premium-escort-stage',
    move: { to: 'current-position', posture: 'rear' },
  }]);
const route = addPlayGeometry(transfer, transferPlayId, 'route');
transfer = route.draft;
for (const waypoint of premiumReturnWaypoints())
  transfer = appendPlayRouteWaypoint(
    transfer, transferPlayId, route.id, waypoint);
const premium = addProgramCustodyCase(transfer, transferPlayId, {
  name: 'Premium four',
  coreBankValue: { operator: 'equals', value: 4 },
  well: 'enemy',
});
transfer = premium.draft;
transfer = configureProgramCustody(
  transfer, transferPlayId, transferProjection('courier-a').id, {
    caseId: premium.caseId,
    kind: 'carrier-return',
    carrierRoleIds: ['courier-a'],
    routeId: route.id,
  });
transfer = configureProgramCustody(
  transfer, transferPlayId, transferProjection('courier-b').id, {
    caseId: premium.caseId,
    kind: 'carrier-return',
    carrierRoleIds: ['courier-b'],
    routeId: route.id,
  });
transfer = configureProgramCustody(
  transfer, transferPlayId, transferProjection('escort').id, {
    caseId: premium.caseId,
    kind: 'escort-carrier',
    carrierRoleIds: ['courier-a', 'courier-b'],
  });
let transferBinding = transferPlay().coreCustody!.bindings.find((entry) =>
  entry.caseId === premium.caseId && entry.kind === 'carrier-return'
    && entry.projectionId === transferProjection('courier-a').id)!;
transfer = configureProgramCustodyHandoff(
  transfer, transferPlayId, transferBinding.id,
  { role: 'courier-b' });
transfer = configureProgramCustodyCompletion(
  transfer, transferPlayId, ['courier-a', 'courier-b']);
transfer = configurePlayLifecycle(transfer, transferPlayId, {
  minimumCommitmentTicks: 0,
  successConditionIds: [transferPlay().coreCustody!.completionConditionId!],
  maximumTicks: 260,
  cooldownTicks: 1200,
  waiting: 'fall-through',
});
write('premium-transfer-proof.json', transfer.orders);
write('premium-transfer-proof-map.json', transfer.mapPlan);
write('premium-transfer-proof-workshop.json', transfer.workshop);

// Common Pass Forward uses the same ordinary Handoff child, but scopes its
// selector to the carrier roles configured for this exact custody case. A
// lower-id Kestrel waits cardinally beside the handoff tile while Courier B
// waits diagonally. Without the role scope that wrong-role body wins the
// deterministic distance/id ordering; with it, Courier B receives, continues
// the same premium route, and banks.
let passForward = structuredClone(transfer);
passForward.orders.sheetId = 'premium-pass-forward-proof';
passForward.orders.mapPlan = 'premium-pass-forward-proof-map.json';
passForward.mapPlan.mapPlanId = 'premium-pass-forward-proof';
const passObservers = passForward.orders.roles.find((entry) =>
  entry.id === 'observers')!;
passObservers.classes = passObservers.classes.filter((entry) => entry !== 'kestrel');
passForward.orders.roles.splice(1, 0, {
  id: 'decoy',
  classes: ['kestrel'],
  upgradePath: ['reach', 'vitality'],
  orders: [{ id: 'decoy-fallback', move: { to: 'own-reactor' } }],
});
const passPlayId = passForward.workshop.plays[0].id;
passForward = addBlankPlayProjection(passForward, passPlayId, 'decoy');
const passPlay = () => passForward.workshop.plays[0];
const passProjection = (roleId: string) => passPlay().projections
  .find((entry) => entry.roleId === roleId)!;
const passReceiverAnchor = passPlay().ownedGeometry.find((entry) =>
  entry.kind === 'anchor')!.id;
passForward.mapPlan.anchors[passReceiverAnchor] = [4, 15];
const decoyPost = addPlayGeometry(passForward, passPlayId, 'anchor');
passForward = decoyPost.draft;
passForward.mapPlan.anchors[decoyPost.id] = [5, 15];
passForward = updatePlayProjectionOrders(
  passForward, passPlayId, passProjection('decoy').id, [{
    id: 'premium-decoy-stage',
    move: { to: { anchor: decoyPost.id } },
  }]);
const passCase = passPlay().coreCustody!.cases!.find((entry) =>
  entry.name === 'Premium four')!;
const passBinding = passPlay().coreCustody!.bindings.find((entry) =>
  entry.caseId === passCase.id && entry.kind === 'carrier-return'
    && entry.projectionId === passProjection('courier-a').id)!;
passForward = configureProgramCustodyHandoff(
  passForward, passPlayId, passBinding.id,
  'nearest-ally-toward-own-reactor');
passForward = configurePlayLifecycle(passForward, passPlayId, {
  minimumCommitmentTicks: 0,
  successConditionIds: [passPlay().coreCustody!.completionConditionId!],
  maximumTicks: 260,
  cooldownTicks: 1200,
  waiting: 'fall-through',
});
write('premium-pass-forward-proof.json', passForward.orders);
write('premium-pass-forward-proof-map.json', passForward.mapPlan);
write('premium-pass-forward-proof-workshop.json', passForward.workshop);

// The recovery variant is produced through the same visible Opportunity helper.
// Its focused runtime test drives low -> healing -> recovered and proves that
// the next ordinary sibling is the same premium route, not a different Program.
let recovery = structuredClone(transfer);
const recoveryPlay = recovery.workshop.plays[0];
const recoveryProjection = recoveryPlay.projections.find((entry) =>
  entry.roleId === 'courier-b')!;
const recoveryBinding = recoveryPlay.coreCustody!.bindings.find((entry) =>
  entry.caseId === premium.caseId && entry.kind === 'carrier-return'
    && entry.projectionId === recoveryProjection.id)!;
recovery = addHealthRecoveryOpportunity(
  recovery, recoveryPlay.id, recoveryProjection.id,
  recoveryBinding.actionOrderIds[0], {
    startBelow: 80,
    recoverUntil: 100,
    withinWalkingSteps: 12,
    timeout: 80,
    cooldown: 10,
    patientsPerTile: 1,
    coordinateAcrossSheet: true,
    allowCoreCarriers: true,
  });
recovery.orders.sheetId = 'premium-carrier-recovery-proof';
recovery.orders.mapPlan = 'premium-carrier-recovery-proof-map.json';
recovery.mapPlan.mapPlanId = 'premium-carrier-recovery-proof';
write('premium-carrier-recovery-proof.json', recovery.orders);
write('premium-carrier-recovery-proof-map.json', recovery.mapPlan);
write('premium-carrier-recovery-proof-workshop.json', recovery.workshop);

// A deterministic mechanics opponent gives the premium carrier one bounded
// combat episode, then yields. It exists only to make the recovery branch
// observable; it is not a strategy or balance opponent.
const recoveryDrill = carrierRecoveryDrill();
write('premium-carrier-recovery-drill.json', recoveryDrill.orders);
write('premium-carrier-recovery-drill-map.json', recoveryDrill.mapPlan);

function blank() {
  const orders: OrdersDocument = {
    schema: 'arc-relay-orders-v2',
    sheetId: 'unwanted-core-proof',
    mapPlan: 'unwanted-core-proof-map.json',
    roles: [
      {
        id: 'runner', classes: ['relay'], upgradePath: ['reach', 'vitality'],
        orders: [{ id: 'runner-fallback', move: { to: 'own-reactor' } }],
      },
      {
        id: 'observers',
        classes: ['kestrel', 'palisade', 'towline', 'patchbay',
          'lantern', 'mortar', 'minesmith'],
        upgradePath: ['reach', 'vitality'],
        orders: [{
          id: 'observers-remain-idle', when: 'impossible-health',
          move: { to: 'own-reactor' },
        }],
      },
    ],
    sharedOrders: {}, capacities: {},
    conditions: {
      'impossible-health': {
        fact: 'self-health-percent', operator: 'less-than', value: 0,
      },
    },
  };
  const mapPlan: MapPlanDocument = {
    schema: 'arc-relay-map-plan-v2',
    mapPlanId: 'unwanted-core-proof',
    mapId: 'arc-relay-tapline-02',
    zones: {}, routes: {}, anchors: {},
  };
  return parseDraft(
    'Unwanted Core proof', null, null,
    stringifyDocument(orders), stringifyDocument(mapPlan), false,
  );
}

function programProofBlank(sheetId: string) {
  const orders: OrdersDocument = {
    schema: 'arc-relay-orders-v2',
    sheetId,
    mapPlan: `${sheetId}-map.json`,
    roles: [
      {
        id: 'courier-a', classes: ['relay'],
        upgradePath: ['reach', 'vitality'],
        orders: [{
          id: 'courier-a-fallback', move: { to: 'own-reactor' },
        }],
      },
      {
        id: 'courier-b', classes: ['relay'],
        upgradePath: ['reach', 'vitality'],
        orders: [{
          id: 'courier-b-fallback', move: { to: 'own-reactor' },
        }],
      },
      {
        id: 'escort', classes: ['patchbay'],
        upgradePath: ['reach', 'vitality'],
        orders: [{
          id: 'escort-fallback', move: { to: 'own-reactor' },
        }],
      },
      {
        id: 'observers',
        classes: ['kestrel', 'palisade', 'towline', 'mortar', 'minesmith'],
        upgradePath: ['reach', 'vitality'],
        orders: [{
          id: 'observers-remain-idle', when: 'impossible-health',
          move: { to: 'own-reactor' },
        }],
      },
    ],
    sharedOrders: {}, capacities: {},
    conditions: {
      'impossible-health': {
        fact: 'self-health-percent', operator: 'less-than', value: 0,
      },
    },
  };
  const mapPlan: MapPlanDocument = {
    schema: 'arc-relay-map-plan-v2',
    mapPlanId: sheetId,
    mapId: 'arc-relay-tapline-02',
    zones: {}, routes: {}, anchors: {},
  };
  return parseDraft(
    sheetId, null, null,
    stringifyDocument(orders), stringifyDocument(mapPlan), false,
  );
}

function carrierRecoveryDrill() {
  const orders: OrdersDocument = {
    schema: 'arc-relay-orders-v2',
    sheetId: 'premium-carrier-recovery-drill',
    mapPlan: 'premium-carrier-recovery-drill-map.json',
    roles: [
      {
        id: 'bounded-shooter', classes: ['hush'],
        upgradePath: ['reach', 'vitality'],
        orders: [{
          id: 'tag-visible-carrier-once',
          when: 'carrier-visible',
          timeout: 8,
          cooldown: 1200,
          fight: {
            targets: ['carrier', 'nearest'],
            style: 'hold',
            chaseLimit: 0,
          },
        }],
      },
      {
        id: 'observers',
        classes: ['kestrel', 'palisade', 'towline', 'patchbay',
          'lantern', 'mortar', 'minesmith'],
        upgradePath: ['reach', 'vitality'],
        orders: [{
          id: 'observers-remain-idle', when: 'impossible-health',
          move: { to: 'own-reactor' },
        }],
      },
    ],
    sharedOrders: {}, capacities: {},
    conditions: {
      'carrier-visible': {
        fact: 'visible-enemy-carriers', operator: 'at-least', value: 1,
      },
      'impossible-health': {
        fact: 'self-health-percent', operator: 'less-than', value: 0,
      },
    },
  };
  const mapPlan: MapPlanDocument = {
    schema: 'arc-relay-map-plan-v2',
    mapPlanId: 'premium-carrier-recovery-drill',
    mapId: 'arc-relay-tapline-02',
    zones: {}, routes: {}, anchors: {},
  };
  return parseDraft(
    'Premium carrier recovery drill', null, null,
    stringifyDocument(orders), stringifyDocument(mapPlan), false,
  );
}

function premiumReturnWaypoints(): Array<[number, number]> {
  return [
    [19, 20], [22, 23], [25, 25], [16, 25], [5, 25], [3, 23],
    [3, 19], [4, 16], [5, 12], [4, 9], [2, 5],
  ];
}

function write(name: string, value: unknown) {
  writeFileSync(resolve(output, name), `${JSON.stringify(value, null, 2)}\n`);
}
