#!/usr/bin/env node

import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import {
  parseDraft,
  stringifyDocument,
  type MapPlanDocument,
  type OrdersDocument,
} from '../web/src/site/sheets/sheetAuthoring.ts';
import { addRelayChainPlay } from '../web/src/site/sheets/playRecipes.ts';

const output = resolve(process.argv[2] ?? '/tmp/arc-running-relay-proof');
mkdirSync(output, { recursive: true });

const orders: OrdersDocument = {
  schema: 'arc-relay-orders-v2',
  sheetId: 'running-relay-proof',
  mapPlan: 'running-relay-map.json',
  roles: [
    role('mason', ['mason']),
    role('reserve', ['kestrel', 'palisade']),
    role('longshot', ['longshot']),
    role('hush', ['hush']),
    role('relay-a', ['relay']),
    role('relay-b', ['relay']),
    role('support', ['patchbay']),
  ],
  sharedOrders: {}, capacities: {}, conditions: {
    inactive: {
      fact: 'self-health-percent',
      operator: 'less-than',
      value: 0,
    },
  },
};
const mapPlan: MapPlanDocument = {
  schema: 'arc-relay-map-plan-v2',
  mapPlanId: 'running-relay-proof-map',
  mapId: 'arc-relay-tapline-02',
  zones: {}, routes: {}, anchors: {},
};
const relay = addRelayChainPlay(parseDraft(
  'Running Relay proof', null, null,
  stringifyDocument(orders), stringifyDocument(mapPlan), false,
), { relayARoleId: 'relay-a', relayBRoleId: 'relay-b' });
write('running-relay.json', relay.orders);
write('running-relay-map.json', relay.mapPlan);
write('running-relay-workshop.json', relay.workshop);

const soloOrders = structuredClone(relay.orders);
soloOrders.sheetId = 'solo-carry-proof';
soloOrders.mapPlan = 'solo-carry-map.json';
const relayA = soloOrders.roles.find((entry) => entry.id === 'relay-a')!;
const returnRoot = relayA.orders.find((entry) =>
  entry.id?.endsWith('-running-return'))!;
returnRoot.orders = returnRoot.orders?.filter((entry) =>
  entry.id?.endsWith('-carry-forward')
    || entry.id?.endsWith('-bank-final-approach'));
relayA.orders = [
  returnRoot,
  relayA.orders.find((entry) => entry.id?.endsWith('-take-start'))!,
];
const relayB = soloOrders.roles.find((entry) => entry.id === 'relay-b')!;
relayB.orders = [{ id: 'relay-b-park', move: { to: 'own-reactor' } }];
for (const conditionId of Object.keys(soloOrders.conditions)) {
  if (!conditionId.endsWith('-carrying-core')
    && !conditionId.endsWith('-empty-handed')
    && !conditionId.endsWith('-team-carrying')
    && !conditionId.endsWith('-at-home-reactor')
    && conditionId !== 'inactive')
    delete soloOrders.conditions[conditionId];
}
const soloMap = structuredClone(relay.mapPlan);
soloMap.mapPlanId = 'solo-carry-proof-map';
const homeZone = Object.keys(soloMap.zones).find((id) =>
  id.endsWith('-home-reactor-approach'))!;
soloMap.zones = { [homeZone]: soloMap.zones[homeZone] };
const corridor = Object.keys(soloMap.routes).find((id) =>
  id.endsWith('-return-corridor'))!;
soloMap.routes = { [corridor]: soloMap.routes[corridor] };
const pickup = Object.keys(soloMap.anchors).find((id) =>
  id.endsWith('-enemy-well-pickup'))!;
const home = Object.keys(soloMap.anchors).find((id) =>
  id.endsWith('-home-reactor'))!;
soloMap.anchors = {
  [pickup]: soloMap.anchors[pickup],
  [home]: soloMap.anchors[home],
};
write('solo-carry.json', soloOrders);
write('solo-carry-map.json', soloMap);

function role(id: string, classes: string[]) {
  return {
    id,
    classes,
    upgradePath: ['reach' as const, 'vitality' as const],
    orders: [{
      id: `${id}-baseline`,
      when: 'inactive',
      move: { to: 'own-reactor' as const },
    }],
  };
}

function write(name: string, value: unknown) {
  writeFileSync(resolve(output, name), `${JSON.stringify(value, null, 2)}\n`);
}
