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
  addPlayGeometry,
  configurePlayLifecycle,
  configureProgramCustody,
  configureProgramCustodyCompletion,
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

function write(name: string, value: unknown) {
  writeFileSync(resolve(output, name), `${JSON.stringify(value, null, 2)}\n`);
}
