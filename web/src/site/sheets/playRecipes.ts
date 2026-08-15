import type {
  ConditionDefinition,
  ConditionExpression,
  OrderNode,
  RoleDefinition,
  SheetDraft,
} from './sheetAuthoring';
import { materializeWorkshop, uniquePlayId } from './linkedPlays';
import type {
  LinkedPlay,
  PlayLinkedValueDefinition,
  PlayProjection,
} from './workshopTypes';

export interface AmbushRecipeRoles {
  masonRoleId: string;
  longshotRoleId: string;
  hushRoleId: string;
  reserveRoleId: string;
}

export interface RelayChainRecipeRoles {
  relayARoleId: string;
  relayBRoleId: string;
}

export function addBlankPlay(draft: SheetDraft, name = 'Untitled Play'): SheetDraft {
  const next = structuredClone(draft);
  const id = uniquePlayId('untitled-play', next.workshop);
  next.workshop.plays.push({
    id,
    name,
    recipe: 'blank',
    ownedGeometry: [],
    geometryGroups: [],
    slots: [],
    ownedConditionIds: [],
    ownedCapacityIds: [],
    projections: [],
    linkedValues: [],
  });
  return next;
}

export function addBlankPlayProjection(
  draft: SheetDraft,
  playId: string,
  roleId: string,
): SheetDraft {
  const next = structuredClone(draft);
  const play = next.workshop.plays.find((entry) => entry.id === playId);
  if (!play || play.recipe !== 'blank')
    throw new Error(`Unknown blank Play '${playId}'.`);
  const role = requireRole(next, roleId);
  if (play.projections.some((entry) => entry.roleId === roleId))
    throw new Error(`Role '${roleId}' already has a projection in this Play.`);
  const projectionId = `${play.id}-${roleId}`;
  const usedIds = new Set(role.orders.flatMap(orderIds));
  let orderId = `${play.id}-${roleId}-move`;
  for (let index = 2; usedIds.has(orderId); index++)
    orderId = `${play.id}-${roleId}-move-${index}`;
  play.projections.push({
    id: projectionId,
    roleId,
    orders: [{
      id: orderId,
      move: { to: { well: 'centre' }, posture: 'line' },
    }],
    placement: { beforeOrderKey: defaultPriorityNeighbor(role) },
    lastGenerated: [],
  });
  return applyMaterialization(next);
}

export function addNorthAmbushPlay(
  draft: SheetDraft,
  roles: AmbushRecipeRoles,
  name = 'North corridor ambush',
): SheetDraft {
  const next = structuredClone(draft);
  assertOneBodyClass(next, roles.masonRoleId, 'mason');
  assertOneBodyClass(next, roles.longshotRoleId, 'longshot');
  assertOneBodyClass(next, roles.hushRoleId, 'hush');
  const reserve = requireRole(next, roles.reserveRoleId);
  if (reserve.classes.length < 2)
    throw new Error('The reserve role needs at least two bodies.');

  const id = uniquePlayId('north-ambush', next.workshop);
  const names = ambushNames(id);
  const anchors: Record<string, [number, number]> = {
    [names.masonPost]: [23, 9],
    [names.longshotPost]: [20, 9],
    [names.hushPost]: [20, 11],
    [names.reservePostA]: [21, 7],
    [names.reservePostB]: [23, 8],
    [names.hardlight]: [24, 9],
    [names.facing]: [25, 9],
  };
  Object.assign(next.mapPlan.anchors, anchors);
  next.mapPlan.zones[names.entryZone] = { rect: [24, 7, 27, 11] };
  for (const [anchorId, zoneId] of [
    [names.masonPost, names.masonReady],
    [names.longshotPost, names.longshotReady],
    [names.hushPost, names.hushReady],
    [names.reservePostA, names.reserveReadyA],
    [names.reservePostB, names.reserveReadyB],
  ] as const) {
    const [x, y] = anchors[anchorId];
    next.mapPlan.zones[zoneId] = { rect: [x, y, x, y] };
  }

  const conditions: Record<string, ConditionDefinition> = {
    [names.traffic]: {
      fact: 'observed-enemy-entries-in-zone', operator: 'at-least', value: 1,
      zone: names.entryZone, since: 'match-start',
    },
    [names.enemies]: {
      fact: 'visible-enemies-in-zone', operator: 'at-least', value: 4,
      zone: names.entryZone,
    },
    [names.masonReadyCondition]: roleReady(roles.masonRoleId, names.masonReady),
    [names.longshotReadyCondition]: roleReady(roles.longshotRoleId, names.longshotReady),
    [names.hushReadyCondition]: roleReady(roles.hushRoleId, names.hushReady),
    [names.reserveReadyConditionA]: roleReady(roles.reserveRoleId, names.reserveReadyA),
    [names.reserveReadyConditionB]: roleReady(roles.reserveRoleId, names.reserveReadyB),
    [names.minimum]: {
      fact: 'self-order-ticks', operator: 'at-least', value: 30,
    },
    [names.success]: {
      fact: 'confirmed-enemy-destructions-in-zone', operator: 'at-least', value: 4,
      zone: names.entryZone, since: 'node-entry',
    },
    [names.signature]: {
      fact: 'signature-ready', operator: 'equals', value: 1,
    },
  };
  Object.assign(next.orders.conditions, conditions);
  next.orders.capacities[names.reserveCapacity] = 1;

  const engageWhen: ConditionExpression = { all: [
    names.enemies,
    names.masonReadyCondition,
    names.longshotReadyCondition,
    names.hushReadyCondition,
    names.reserveReadyConditionA,
    names.reserveReadyConditionB,
  ] };
  const engageUntil: ConditionExpression = { all: [names.minimum, names.success] };
  const linkedValues: PlayLinkedValueDefinition[] = [];
  const projections = [
    ambushProjection(id, roles.masonRoleId, 'mason', names, next),
    ambushProjection(id, roles.longshotRoleId, 'longshot', names, next),
    ambushProjection(id, roles.hushRoleId, 'hush', names, next),
    ambushProjection(id, roles.reserveRoleId, 'reserve', names, next),
  ];
  linkedValues.push(
    linkedLifecycle(`${id}-engage-when`, 'when', engageWhen, projections, 'engage'),
    linkedLifecycle(`${id}-engage-until`, 'until', engageUntil, projections, 'engage'),
    linkedLifecycle(`${id}-timeout`, 'timeout', 90, projections, 'engage'),
    linkedLifecycle(`${id}-assembly-when`, 'when', names.traffic, projections, 'assembly'),
    linkedLifecycle(`${id}-assembly-until`, 'until', engageWhen, projections, 'assembly'),
    linkedLifecycle(`${id}-cooldown`, 'cooldown', 30, projections, 'assembly'),
  );

  const play: LinkedPlay = {
    id,
    name,
    recipe: 'ambush',
    ownedGeometry: [
      { kind: 'zone', id: names.entryZone, purpose: 'visible-enemies-in-zone and observed-enemy-entries-in-zone' },
      ...[
        [names.masonPost, names.masonReady],
        [names.longshotPost, names.longshotReady],
        [names.hushPost, names.hushReady],
        [names.reservePostA, names.reserveReadyA],
        [names.reservePostB, names.reserveReadyB],
      ].flatMap(([anchorId, zoneId]) => [
        { kind: 'anchor' as const, id: anchorId, purpose: 'move.to' },
        { kind: 'zone' as const, id: zoneId, purpose: 'role-in-zone-count', derivedFrom: anchorId, tolerance: 0 },
      ]),
      { kind: 'anchor', id: names.hardlight, purpose: 'use.at' },
      { kind: 'anchor', id: names.facing, purpose: 'move.face' },
    ],
    geometryGroups: [{
      id: `${id}-line`,
      kind: 'selection',
      objectIds: Object.keys(anchors),
      derivedObjectIds: [
        names.masonReady, names.longshotReady, names.hushReady,
        names.reserveReadyA, names.reserveReadyB,
      ],
    }],
    slots: [
      { id: `${id}-mason-slot`, roleId: roles.masonRoleId, anchorId: names.masonPost, readinessZoneId: names.masonReady, required: true, tolerance: 0 },
      { id: `${id}-longshot-slot`, roleId: roles.longshotRoleId, anchorId: names.longshotPost, readinessZoneId: names.longshotReady, required: true, tolerance: 0 },
      { id: `${id}-hush-slot`, roleId: roles.hushRoleId, anchorId: names.hushPost, readinessZoneId: names.hushReady, required: true, tolerance: 0 },
      { id: `${id}-reserve-a-slot`, roleId: roles.reserveRoleId, anchorId: names.reservePostA, readinessZoneId: names.reserveReadyA, required: true, tolerance: 0, capacity: names.reserveCapacity },
      { id: `${id}-reserve-b-slot`, roleId: roles.reserveRoleId, anchorId: names.reservePostB, readinessZoneId: names.reserveReadyB, required: true, tolerance: 0, capacity: names.reserveCapacity },
    ],
    ownedConditionIds: Object.keys(conditions),
    ownedCapacityIds: [names.reserveCapacity],
    projections,
    linkedValues,
  };
  next.workshop.plays.push(play);
  return applyMaterialization(next);
}

export function addRelayChainPlay(
  draft: SheetDraft,
  roles: RelayChainRecipeRoles,
  name = 'Dual Relay corridor chain',
): SheetDraft {
  const next = structuredClone(draft);
  assertOneBodyClass(next, roles.relayARoleId, 'relay');
  assertOneBodyClass(next, roles.relayBRoleId, 'relay');
  if (roles.relayARoleId === roles.relayBRoleId)
    throw new Error('Choose two distinct one-body Relay roles.');

  const id = uniquePlayId('relay-chain', next.workshop);
  const stationIds = [0, 1, 2, 3].map((index) => `${id}-r${index}`);
  const zoneIds = stationIds.map((station) => `${station}-catch`);
  const points: Array<[number, number]> = [[15, 13], [12, 13], [10, 13], [9, 11]];
  stationIds.forEach((station, index) => {
    next.mapPlan.anchors[station] = points[index];
    const [x, y] = points[index];
    next.mapPlan.zones[zoneIds[index]] = { rect: [x, y, x, y] };
  });
  const routeA = `${id}-r0-to-r2`;
  const routeB = `${id}-r1-to-r3`;
  const bankRoute = `${id}-r3-to-reactor`;
  next.mapPlan.routes[routeA] = { waypoints: points.slice(0, 3) };
  next.mapPlan.routes[routeB] = { waypoints: points.slice(1, 4) };
  next.mapPlan.routes[bankRoute] = {
    waypoints: [points[3], [7, 9], [5, 7], [2, 5]],
  };

  const conditionIds = {
    carrying: `${id}-carrying-core`,
    empty: `${id}-empty-handed`,
    signature: `${id}-signature-ready`,
    a0: `${id}-a-at-r0`, a2: `${id}-a-at-r2`,
    b1: `${id}-b-at-r1`, b3: `${id}-b-at-r3`,
  };
  const conditions: Record<string, ConditionDefinition> = {
    [conditionIds.carrying]: { fact: 'self-carrying', operator: 'equals', value: 1 },
    [conditionIds.empty]: { fact: 'self-carrying', operator: 'equals', value: 0 },
    [conditionIds.signature]: { fact: 'signature-ready', operator: 'equals', value: 1 },
    [conditionIds.a0]: roleReady(roles.relayARoleId, zoneIds[0]),
    [conditionIds.a2]: roleReady(roles.relayARoleId, zoneIds[2]),
    [conditionIds.b1]: roleReady(roles.relayBRoleId, zoneIds[1]),
    [conditionIds.b3]: roleReady(roles.relayBRoleId, zoneIds[3]),
  };
  Object.assign(next.orders.conditions, conditions);

  const aProjection = relayAProjection(id, roles, stationIds, routeA, conditionIds, next);
  const bProjection = relayBProjection(id, roles, stationIds, routeB, bankRoute, conditionIds, next);
  const projections = [aProjection, bProjection];
  const linkedValues = [
    linkedLifecycle(`${id}-hop-timeout`, 'timeout', 240, projections, 'hop'),
  ];
  const play: LinkedPlay = {
    id,
    name,
    recipe: 'relay-chain',
    ownedGeometry: [
      ...stationIds.flatMap((station, index) => [
        { kind: 'anchor' as const, id: station, purpose: 'move.to' },
        { kind: 'zone' as const, id: zoneIds[index], purpose: 'role-in-zone-count', derivedFrom: station, tolerance: 0 },
      ]),
      { kind: 'route', id: routeA, purpose: 'move.via', derivedFrom: `${id}-corridor` },
      { kind: 'route', id: routeB, purpose: 'move.via', derivedFrom: `${id}-corridor` },
      { kind: 'route', id: bankRoute, purpose: 'move.via', derivedFrom: `${id}-corridor` },
    ],
    geometryGroups: [{
      id: `${id}-corridor`,
      kind: 'relay-corridor',
      objectIds: [...stationIds, ...zoneIds],
      derivedObjectIds: [routeA, routeB, bankRoute],
      waypoints: [...points, [7, 9], [5, 7], [2, 5]],
      stationWaypointIndexes: Object.fromEntries(stationIds.map((station, index) => [station, index])),
      routeSlices: [
        { routeId: routeA, fromWaypointIndex: 0, toWaypointIndex: 2 },
        { routeId: routeB, fromWaypointIndex: 1, toWaypointIndex: 3 },
        { routeId: bankRoute, fromWaypointIndex: 3, toWaypointIndex: 6 },
      ],
    }],
    slots: stationIds.map((station, index) => ({
      id: `${station}-slot`,
      roleId: index % 2 === 0 ? roles.relayARoleId : roles.relayBRoleId,
      anchorId: station,
      readinessZoneId: zoneIds[index],
      required: true,
      tolerance: 0,
      capacity: 1,
    })),
    ownedConditionIds: Object.keys(conditions),
    ownedCapacityIds: [],
    projections,
    linkedValues,
  };
  next.workshop.plays.push(play);
  return applyMaterialization(next);
}

function ambushProjection(
  playId: string,
  roleId: string,
  kind: 'mason' | 'longshot' | 'hush' | 'reserve',
  names: ReturnType<typeof ambushNames>,
  draft: SheetDraft,
): PlayProjection {
  const suffix = kind;
  const engageId = `${playId}-${suffix}-engage`;
  const assemblyId = `${playId}-${suffix}-assembly`;
  let engage: OrderNode;
  let assembly: OrderNode;
  if (kind === 'mason') {
    engage = {
      id: engageId,
      orders: [
        {
          id: `${playId}-place-hardlight`,
          when: names.signature,
          use: { skill: 'hardlight-block', target: 'nearest-ally', at: names.hardlight },
        },
        {
          id: `${playId}-mason-fight`,
          fight: { targets: ['carrier', 'weakest', 'nearest'], style: 'hold', chaseLimit: 3 },
        },
      ],
    };
    assembly = {
      id: assemblyId,
      move: { to: { anchor: names.masonPost }, face: { anchor: names.facing }, posture: 'front' },
    };
  } else if (kind === 'reserve') {
    engage = {
      id: engageId,
      fight: { targets: ['carrier', 'weakest', 'nearest'], style: 'hold', chaseLimit: 4 },
    };
    assembly = {
      id: assemblyId,
      orders: [
        { id: `${playId}-reserve-a`, capacity: names.reserveCapacity, move: { to: { anchor: names.reservePostA }, face: { anchor: names.facing }, posture: 'front' } },
        { id: `${playId}-reserve-b`, capacity: names.reserveCapacity, move: { to: { anchor: names.reservePostB }, face: { anchor: names.facing }, posture: 'front' } },
      ],
    };
  } else {
    const post = kind === 'longshot' ? names.longshotPost : names.hushPost;
    engage = {
      id: engageId,
      fight: { targets: ['carrier', 'weakest', 'nearest'], style: kind === 'longshot' ? 'hold' : 'kite', chaseLimit: 4 },
    };
    assembly = {
      id: assemblyId,
      move: { to: { anchor: post }, face: { anchor: names.facing }, posture: kind === 'longshot' ? 'rear' : 'line' },
    };
  }
  return {
    id: `${playId}-${suffix}`,
    roleId,
    orders: [engage, assembly],
    placement: { beforeOrderKey: defaultPriorityNeighbor(requireRole(draft, roleId)) },
    lastGenerated: [],
  };
}

function relayAProjection(
  playId: string,
  roles: RelayChainRecipeRoles,
  stations: string[],
  routeA: string,
  conditions: Record<string, string>,
  draft: SheetDraft,
): PlayProjection {
  return {
    id: `${playId}-relay-a`,
    roleId: roles.relayARoleId,
    placement: { beforeOrderKey: defaultPriorityNeighbor(requireRole(draft, roles.relayARoleId)) },
    lastGenerated: [],
    orders: [
      {
        id: `${playId}-a-final-toss`,
        when: { all: [conditions.carrying, conditions.a2, conditions.b3, conditions.signature] },
        use: { skill: 'arc-toss', target: { role: roles.relayBRoleId } },
      },
      {
        id: `${playId}-a-hop`,
        when: { all: [conditions.carrying, conditions.a0] },
        until: conditions.a2,
        orders: [
          {
            id: `${playId}-a-toss-r0-r1`,
            when: { all: [conditions.carrying, conditions.b1, conditions.signature] },
            use: { skill: 'arc-toss', target: { role: roles.relayBRoleId } },
          },
          { id: `${playId}-a-hold-r0`, when: conditions.carrying, move: { to: { anchor: stations[0] } } },
          { id: `${playId}-a-advance-r2`, move: { to: { anchor: stations[2] }, via: routeA } },
        ],
      },
      { id: `${playId}-a-hold-r2`, when: conditions.a2, move: { to: { anchor: stations[2] } } },
      { id: `${playId}-a-collect-r0`, move: { to: { anchor: stations[0] } } },
    ],
  };
}

function relayBProjection(
  playId: string,
  roles: RelayChainRecipeRoles,
  stations: string[],
  routeB: string,
  bankRoute: string,
  conditions: Record<string, string>,
  draft: SheetDraft,
): PlayProjection {
  return {
    id: `${playId}-relay-b`,
    roleId: roles.relayBRoleId,
    placement: { beforeOrderKey: defaultPriorityNeighbor(requireRole(draft, roles.relayBRoleId)) },
    lastGenerated: [],
    orders: [
      {
        id: `${playId}-b-bank`,
        when: { all: [conditions.carrying, conditions.b3] },
        until: conditions.empty,
        urgency: 'urgent',
        move: { to: 'own-reactor', via: bankRoute },
      },
      {
        id: `${playId}-b-hop`,
        when: { all: [conditions.carrying, conditions.b1] },
        until: conditions.b3,
        orders: [
          {
            id: `${playId}-b-toss-r1-r2`,
            when: { all: [conditions.carrying, conditions.a2, conditions.signature] },
            use: { skill: 'arc-toss', target: { role: roles.relayARoleId } },
          },
          { id: `${playId}-b-hold-r1`, when: conditions.carrying, move: { to: { anchor: stations[1] } } },
          { id: `${playId}-b-advance-r3`, move: { to: { anchor: stations[3] }, via: routeB } },
        ],
      },
      { id: `${playId}-b-hold-r3`, when: conditions.b3, move: { to: { anchor: stations[3] } } },
      { id: `${playId}-b-stage-r1`, move: { to: { anchor: stations[1] } } },
    ],
  };
}

function linkedLifecycle(
  id: string,
  field: string,
  value: PlayLinkedValueDefinition['value'],
  projections: PlayProjection[],
  orderSuffix: string,
): PlayLinkedValueDefinition {
  return {
    id,
    field,
    value,
    targets: projections.map((projection) => ({
      projectionId: projection.id,
      orderId: projection.orders.find((order) => order.id?.endsWith(orderSuffix))?.id
        ?? projection.orders.find((order) => order.id?.includes(orderSuffix))?.id
        ?? '',
      field,
    })).filter((target) => target.orderId),
  };
}

function ambushNames(id: string) {
  return {
    entryZone: `${id}-north-entry`, facing: `${id}-corridor-mouth`,
    hardlight: `${id}-hardlight-choke`, masonPost: `${id}-mason-post`,
    longshotPost: `${id}-longshot-post`, hushPost: `${id}-hush-post`,
    reservePostA: `${id}-reserve-a`, reservePostB: `${id}-reserve-b`,
    masonReady: `${id}-mason-ready`, longshotReady: `${id}-longshot-ready`,
    hushReady: `${id}-hush-ready`, reserveReadyA: `${id}-reserve-a-ready`,
    reserveReadyB: `${id}-reserve-b-ready`, traffic: `${id}-north-traffic`,
    enemies: `${id}-four-enemies`, masonReadyCondition: `${id}-mason-in-position`,
    longshotReadyCondition: `${id}-longshot-in-position`,
    hushReadyCondition: `${id}-hush-in-position`,
    reserveReadyConditionA: `${id}-reserve-a-in-position`,
    reserveReadyConditionB: `${id}-reserve-b-in-position`,
    minimum: `${id}-minimum-30`, success: `${id}-four-destructions`,
    signature: `${id}-signature-ready`, reserveCapacity: `${id}-one-per-post`,
  };
}

function roleReady(role: string, zone: string): ConditionDefinition {
  return { fact: 'role-in-zone-count', operator: 'at-least', value: 1, role, zone };
}

function assertOneBodyClass(draft: SheetDraft, roleId: string, classId: string) {
  const role = requireRole(draft, roleId);
  if (role.classes.length !== 1 || role.classes[0] !== classId)
    throw new Error(`Role '${roleId}' must contain exactly one ${classId} body.`);
}

function requireRole(draft: SheetDraft, roleId: string): RoleDefinition {
  const role = draft.orders.roles.find((entry) => entry.id === roleId);
  if (!role) throw new Error(`Unknown role '${roleId}'.`);
  return role;
}

function defaultPriorityNeighbor(role: RoleDefinition): string | null {
  const order = role.orders.at(-1);
  if (!order) return null;
  if (order.id) return `id:${order.id}`;
  if (order.shared) return `shared:${order.shared}`;
  return null;
}

function applyMaterialization(draft: SheetDraft): SheetDraft {
  const materialized = materializeWorkshop(draft.orders, draft.workshop);
  if (materialized.drifts.length > 0)
    throw new Error(materialized.drifts.map((drift) => drift.message).join(' '));
  draft.orders = materialized.orders;
  draft.workshop = materialized.workshop;
  return draft;
}

function orderIds(order: OrderNode): string[] {
  return [
    ...(order.id ? [order.id] : []),
    ...(order.orders?.flatMap(orderIds) ?? []),
  ];
}
