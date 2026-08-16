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

export interface VeteranEscortRecipeRoles {
  veteranRoleId: string;
  escortRoleId: string;
}

export interface SafeReturnRecipeRoles {
  carrierRoleId: string;
  relayRoleId: string;
}

export interface LaneClosureRecipeRoles {
  masonRoleId: string;
  guardRoleId: string;
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
  // One west-authored long return corridor from the enemy Well, down through
  // the southern passage, across the lower lane, and up the west approach to
  // the home reactor. Every point is an alternating Toss station; the empty
  // Relay uses the three-station slice to leapfrog one post beyond its partner.
  // The course deliberately avoids the other two Wells: an empty advancing
  // Relay must not auto-collect a second Core and poison the possession relay.
  const corridorWaypoints: Array<[number, number]> = [
    [19, 20], [18, 19], [18, 16], [15, 16], [13, 16],
    [11, 18], [9, 20], [7, 22], [7, 23], [3, 23],
    [3, 19], [5, 17], [4, 16], [5, 15], [5, 12],
    [4, 11], [4, 9], [3, 8], [4, 7], [2, 5],
  ];
  const stationWaypointIndexes = corridorWaypoints.map((_, index) => index);
  const points = stationWaypointIndexes.map((index) => corridorWaypoints[index]);
  const stationIds = points.map((_, index) => `${id}-r${index}`);
  const zoneIds = stationIds.map((station) => `${station}-catch`);
  stationIds.forEach((station, index) => {
    next.mapPlan.anchors[station] = points[index];
    const [x, y] = points[index];
    next.mapPlan.zones[zoneIds[index]] = { rect: [x, y, x, y] };
  });
  const hopRoutes = stationIds.slice(0, -2).map((_, index) => {
    const routeId = `${id}-r${index}-to-r${index + 2}`;
    next.mapPlan.routes[routeId] = {
      waypoints: corridorWaypoints.slice(
        stationWaypointIndexes[index],
        stationWaypointIndexes[index + 2] + 1,
      ),
    };
    return routeId;
  });

  const conditionIds: Record<string, string> = {
    carrying: `${id}-carrying-core`,
    signature: `${id}-signature-ready`,
  };
  const conditions: Record<string, ConditionDefinition> = {
    [conditionIds.carrying]: { fact: 'self-carrying', operator: 'equals', value: 1 },
    [conditionIds.signature]: { fact: 'signature-ready', operator: 'equals', value: 1 },
  };
  stationIds.forEach((_, index) => {
    const selfAt = `${id}-self-at-r${index}`;
    conditionIds[`selfAt${index}`] = selfAt;
    conditions[selfAt] = {
      fact: 'self-in-zone', operator: 'equals', value: 1, zone: zoneIds[index],
    };
    if (index > 0) {
      const roleAt = `${id}-${index % 2 === 0 ? 'a' : 'b'}-at-r${index}`;
      conditionIds[`roleAt${index}`] = roleAt;
      conditions[roleAt] = roleReady(
        index % 2 === 0 ? roles.relayARoleId : roles.relayBRoleId,
        zoneIds[index],
      );
    }
  });
  Object.assign(next.orders.conditions, conditions);

  const aProjection = relayProjection(
    id, 'a', roles.relayARoleId, roles.relayBRoleId,
    0, stationIds, hopRoutes, conditionIds, next,
  );
  const bProjection = relayProjection(
    id, 'b', roles.relayBRoleId, roles.relayARoleId,
    1, stationIds, hopRoutes, conditionIds, next,
  );
  const projections = [aProjection, bProjection];
  const linkedValues: PlayLinkedValueDefinition[] = [{
    id: `${id}-hop-timeout`, field: 'timeout', value: 240,
    targets: projections.flatMap((projection) => projection.orders
      .filter((order) => order.id?.includes('-hop-r'))
      .map((order) => ({
        projectionId: projection.id,
        orderId: order.id!,
        field: 'timeout',
      }))),
  }];
  const play: LinkedPlay = {
    id,
    name,
    recipe: 'relay-chain',
    ownedGeometry: [
      ...stationIds.flatMap((station, index) => [
        { kind: 'anchor' as const, id: station, purpose: 'move.to' },
        { kind: 'zone' as const, id: zoneIds[index], purpose: 'role-in-zone-count', derivedFrom: station, tolerance: 0 },
      ]),
      ...hopRoutes.map((routeId) => ({
        kind: 'route' as const, id: routeId, purpose: 'move.via', derivedFrom: `${id}-corridor`,
      })),
    ],
    geometryGroups: [{
      id: `${id}-corridor`,
      kind: 'relay-corridor',
      objectIds: [...stationIds, ...zoneIds],
      derivedObjectIds: hopRoutes,
      waypoints: corridorWaypoints,
      stationWaypointIndexes: Object.fromEntries(stationIds.map((station, index) =>
        [station, stationWaypointIndexes[index]])),
      routeSlices: hopRoutes.map((routeId, index) => ({
        routeId,
        fromWaypointIndex: stationWaypointIndexes[index],
        toWaypointIndex: stationWaypointIndexes[index + 2],
      })),
    }],
    slots: stationIds.map((station, index) => ({
      id: `${station}-slot`,
      roleId: index % 2 === 0 ? roles.relayARoleId : roles.relayBRoleId,
      anchorId: station,
      readinessZoneId: zoneIds[index],
      // Relay stations are sequential receiver posts, not a simultaneous
      // formation-readiness gate. They remain visible/editable slots without
      // claiming that one body must occupy both of its stations at once.
      required: false,
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

export function addVeteranEscortPlay(
  draft: SheetDraft,
  roles: VeteranEscortRecipeRoles,
  name = 'Veteran route escort',
): SheetDraft {
  const next = structuredClone(draft);
  const veteran = requireRole(next, roles.veteranRoleId);
  const escort = requireRole(next, roles.escortRoleId);
  if (roles.veteranRoleId === roles.escortRoleId)
    throw new Error('Choose a veteran role and a different escort role.');
  if (veteran.classes.length !== 1)
    throw new Error('The veteran route needs a one-body role so its authoring is predictable.');
  if (escort.classes.length < 1)
    throw new Error('The escort role needs at least one body.');

  const id = uniquePlayId('veteran-escort', next.workshop);
  const routeId = `${id}-route`;
  const facingId = `${id}-front`;
  const screenA = `${id}-screen-a`;
  const screenB = `${id}-screen-b`;
  const screenAZone = `${screenA}-ready`;
  const screenBZone = `${screenB}-ready`;
  next.mapPlan.routes[routeId] = { waypoints: [[6, 7], [11, 6], [12, 9], [15, 13]] };
  next.mapPlan.anchors[facingId] = [15, 13];
  next.mapPlan.anchors[screenA] = [12, 7];
  next.mapPlan.anchors[screenB] = [14, 7];
  next.mapPlan.zones[screenAZone] = { rect: [12, 7, 12, 7] };
  next.mapPlan.zones[screenBZone] = { rect: [14, 7, 14, 7] };
  const conditionIds = {
    active: `${id}-veteran-active`, hurt: `${id}-escort-hurt`,
    contact: `${id}-escort-contact`, screenA: `${id}-screen-a-filled`,
    screenB: `${id}-screen-b-filled`,
  };
  Object.assign(next.orders.conditions, {
    [conditionIds.active]: { fact: 'role-highest-level', operator: 'at-least', value: 1, role: roles.veteranRoleId },
    [conditionIds.hurt]: { fact: 'self-health-percent', operator: 'at-most', value: 35 },
    [conditionIds.contact]: { fact: 'self-nearest-visible-enemy-distance', operator: 'at-most', value: 6 },
    [conditionIds.screenA]: roleReady(roles.escortRoleId, screenAZone),
    [conditionIds.screenB]: roleReady(roles.escortRoleId, screenBZone),
  } satisfies Record<string, ConditionDefinition>);
  const capacityId = `${id}-one-per-screen`;
  next.orders.capacities[capacityId] = 1;
  const leaderProjection: PlayProjection = {
    id: `${id}-veteran`, roleId: roles.veteranRoleId,
    placement: { beforeOrderKey: defaultPriorityNeighbor(veteran) }, lastGenerated: [],
    orders: [{
      id: `${id}-veteran-route`, when: conditionIds.active,
      move: { patrol: routeId, posture: 'front' },
    }],
  };
  const escortProjection: PlayProjection = {
    id: `${id}-escort`, roleId: roles.escortRoleId,
    placement: { beforeOrderKey: defaultPriorityNeighbor(escort) }, lastGenerated: [],
    orders: [
      { id: `${id}-escort-recover`, when: conditionIds.hurt, urgency: 'urgent', move: { to: 'nearest-heal-zone' } },
      { id: `${id}-escort-fight`, when: conditionIds.contact, fight: { targets: ['carrier', 'weakest', 'nearest'], style: 'hold', chaseLimit: 4 } },
      { id: `${id}-screen-a`, when: conditionIds.active, until: conditionIds.screenA, capacity: capacityId,
        move: { to: { anchor: screenA }, face: { anchor: facingId }, posture: 'front' } },
      { id: `${id}-screen-b`, when: conditionIds.active, until: conditionIds.screenB, capacity: capacityId,
        move: { to: { anchor: screenB }, face: { anchor: facingId }, posture: 'front' } },
      { id: `${id}-follow`, when: conditionIds.active, capacity: Math.min(2, escort.classes.length),
        move: { follow: roles.veteranRoleId, posture: 'rear' } },
    ],
  };
  const projections = [leaderProjection, escortProjection];
  const play: LinkedPlay = {
    id, name, recipe: 'veteran-escort',
    ownedGeometry: [
      { kind: 'route', id: routeId, purpose: 'move.patrol' },
      { kind: 'anchor', id: facingId, purpose: 'move.face' },
      { kind: 'anchor', id: screenA, purpose: 'move.to' },
      { kind: 'zone', id: screenAZone, purpose: 'role-in-zone-count', derivedFrom: screenA, tolerance: 0 },
      { kind: 'anchor', id: screenB, purpose: 'move.to' },
      { kind: 'zone', id: screenBZone, purpose: 'role-in-zone-count', derivedFrom: screenB, tolerance: 0 },
    ],
    geometryGroups: [{
      id: `${id}-geometry`, kind: 'selection',
      objectIds: [routeId, facingId, screenA, screenB],
      derivedObjectIds: [screenAZone, screenBZone],
    }],
    slots: [
      { id: `${screenA}-slot`, roleId: roles.escortRoleId, anchorId: screenA, readinessZoneId: screenAZone, required: false, tolerance: 0, capacity: capacityId },
      { id: `${screenB}-slot`, roleId: roles.escortRoleId, anchorId: screenB, readinessZoneId: screenBZone, required: false, tolerance: 0, capacity: capacityId },
    ],
    ownedConditionIds: Object.values(conditionIds), ownedCapacityIds: [capacityId], projections,
    linkedValues: [
      linkedLifecycle(`${id}-screen-timeout`, 'timeout', 40, [escortProjection], 'screen'),
      linkedLifecycle(`${id}-screen-cooldown`, 'cooldown', 120, [escortProjection], 'screen'),
    ],
  };
  // Both screen leaves intentionally share these timing values.
  for (const linked of play.linkedValues) linked.targets = [
    { projectionId: escortProjection.id, orderId: `${id}-screen-a`, field: linked.field },
    { projectionId: escortProjection.id, orderId: `${id}-screen-b`, field: linked.field },
  ];
  next.workshop.plays.push(play);
  return applyMaterialization(next);
}

export function addSafeReturnPlay(
  draft: SheetDraft,
  roles: SafeReturnRecipeRoles,
  name = 'Safe Core return handoff',
): SheetDraft {
  const next = structuredClone(draft);
  const carrier = requireRole(next, roles.carrierRoleId);
  const relay = requireRole(next, roles.relayRoleId);
  if (roles.carrierRoleId === roles.relayRoleId)
    throw new Error('Choose a carrier role and a different Relay role.');
  assertOneBodyClass(next, roles.relayRoleId, 'relay');
  if (carrier.classes.length !== 1)
    throw new Error('The safe-return carrier needs a one-body role for a predictable handoff.');

  const id = uniquePlayId('safe-return', next.workshop);
  const pickupZone = `${id}-enemy-pickup`;
  const exchange = `${id}-exchange`;
  const exchangeZone = `${exchange}-ready`;
  const safeRoute = `${id}-safe-route`;
  const homeRoute = `${id}-home-route`;
  next.mapPlan.zones[pickupZone] = { rect: [17, 18, 21, 22] };
  next.mapPlan.anchors[exchange] = [9, 19];
  next.mapPlan.zones[exchangeZone] = { rect: [9, 19, 9, 19] };
  next.mapPlan.routes[safeRoute] = {
    waypoints: [[19, 20], [22, 23], [27, 25], [15, 25], [3, 25], [3, 23], [9, 19]],
  };
  next.mapPlan.routes[homeRoute] = {
    waypoints: [[9, 19], [8, 17], [8, 14], [10, 12], [12, 10], [12, 7], [11, 6], [7, 5], [4, 6], [2, 5]],
  };
  const conditions = {
    carrying: `${id}-carrying`, empty: `${id}-empty`, pickup: `${id}-in-enemy-pickup`,
    exchange: `${id}-at-exchange`, relay: `${id}-relay-at-exchange`,
  };
  Object.assign(next.orders.conditions, {
    [conditions.carrying]: { fact: 'self-carrying', operator: 'equals', value: 1 },
    [conditions.empty]: { fact: 'self-carrying', operator: 'equals', value: 0 },
    [conditions.pickup]: { fact: 'self-in-zone', operator: 'equals', value: 1, zone: pickupZone },
    [conditions.exchange]: { fact: 'self-in-zone', operator: 'equals', value: 1, zone: exchangeZone },
    [conditions.relay]: roleReady(roles.relayRoleId, exchangeZone),
  } satisfies Record<string, ConditionDefinition>);
  const carrierProjection: PlayProjection = {
    id: `${id}-carrier`, roleId: roles.carrierRoleId,
    placement: { beforeOrderKey: defaultPriorityNeighbor(carrier) }, lastGenerated: [],
    orders: [{
      id: `${id}-carrier-return`, when: { all: [conditions.carrying, conditions.pickup] },
      until: conditions.empty, urgency: 'urgent', orders: [
        { id: `${id}-handoff`, when: { all: [conditions.exchange, conditions.relay] }, handoff: { target: { role: roles.relayRoleId } } },
        { id: `${id}-reach-exchange`, move: { to: { anchor: exchange }, via: safeRoute } },
      ],
    }],
  };
  const relayProjection: PlayProjection = {
    id: `${id}-relay`, roleId: roles.relayRoleId,
    placement: { beforeOrderKey: defaultPriorityNeighbor(relay) }, lastGenerated: [],
    orders: [
      { id: `${id}-relay-bank`, when: conditions.carrying, until: conditions.empty, urgency: 'urgent', move: { to: 'own-reactor', via: homeRoute } },
      { id: `${id}-relay-stage`, when: conditions.empty, move: { to: { anchor: exchange }, posture: 'rear' } },
    ],
  };
  const projections = [carrierProjection, relayProjection];
  const play: LinkedPlay = {
    id, name, recipe: 'safe-return',
    ownedGeometry: [
      { kind: 'zone', id: pickupZone, purpose: 'self-in-zone' },
      { kind: 'anchor', id: exchange, purpose: 'move.to' },
      { kind: 'zone', id: exchangeZone, purpose: 'role-in-zone-count and self-in-zone', derivedFrom: exchange, tolerance: 0 },
      { kind: 'route', id: safeRoute, purpose: 'move.via' },
      { kind: 'route', id: homeRoute, purpose: 'move.via' },
    ],
    geometryGroups: [{
      id: `${id}-geometry`, kind: 'selection',
      objectIds: [pickupZone, exchange, safeRoute, homeRoute], derivedObjectIds: [exchangeZone],
    }],
    slots: [{ id: `${exchange}-slot`, roleId: roles.relayRoleId, anchorId: exchange, readinessZoneId: exchangeZone, required: false, tolerance: 0, capacity: 1 }],
    ownedConditionIds: Object.values(conditions), ownedCapacityIds: [], projections,
    linkedValues: [linkedLifecycle(`${id}-timeout`, 'timeout', 240, projections, 'return')],
  };
  play.linkedValues[0].targets = [
    { projectionId: carrierProjection.id, orderId: `${id}-carrier-return`, field: 'timeout' },
    { projectionId: relayProjection.id, orderId: `${id}-relay-bank`, field: 'timeout' },
  ];
  next.workshop.plays.push(play);
  return applyMaterialization(next);
}

export function addLaneClosurePlay(
  draft: SheetDraft,
  roles: LaneClosureRecipeRoles,
  name = 'Hardlight lane closure',
): SheetDraft {
  const next = structuredClone(draft);
  assertOneBodyClass(next, roles.masonRoleId, 'mason');
  const guard = requireRole(next, roles.guardRoleId);
  if (roles.masonRoleId === roles.guardRoleId || guard.classes.length < 2)
    throw new Error('Lane closure needs a Mason role and a different role with at least two guards.');
  const mason = requireRole(next, roles.masonRoleId);
  const id = uniquePlayId('lane-closure', next.workshop);
  const hardlight = `${id}-hardlight`;
  const facing = `${id}-open-lane`;
  const guardA = `${id}-guard-a`;
  const guardB = `${id}-guard-b`;
  next.mapPlan.anchors[hardlight] = [24, 9];
  next.mapPlan.anchors[facing] = [25, 9];
  next.mapPlan.anchors[guardA] = [21, 9];
  next.mapPlan.anchors[guardB] = [23, 11];
  const conditions = { signature: `${id}-signature-ready`, contact: `${id}-contact` };
  Object.assign(next.orders.conditions, {
    [conditions.signature]: { fact: 'signature-ready', operator: 'equals', value: 1 },
    [conditions.contact]: { fact: 'self-nearest-visible-enemy-distance', operator: 'at-most', value: 6 },
  } satisfies Record<string, ConditionDefinition>);
  const capacityId = `${id}-one-per-guard-post`;
  next.orders.capacities[capacityId] = 1;
  const masonProjection: PlayProjection = {
    id: `${id}-mason`, roleId: roles.masonRoleId,
    placement: { beforeOrderKey: defaultPriorityNeighbor(mason) }, lastGenerated: [],
    orders: [{ id: `${id}-mason-closure`, orders: [
      { id: `${id}-place-hardlight`, when: conditions.signature, use: { skill: 'hardlight-block', target: 'nearest-ally', at: hardlight } },
      { id: `${id}-stage-mason`, urgency: 'normal', move: { to: { adjacentTo: hardlight } } },
    ] }],
  };
  const guardProjection: PlayProjection = {
    id: `${id}-guards`, roleId: roles.guardRoleId,
    placement: { beforeOrderKey: defaultPriorityNeighbor(guard) }, lastGenerated: [],
    orders: [
      { id: `${id}-guard-fight`, when: conditions.contact, fight: { targets: ['carrier', 'weakest', 'nearest'], style: 'hold', chaseLimit: 3 } },
      { id: `${id}-guard-a`, capacity: capacityId, move: { to: { anchor: guardA }, face: { anchor: facing }, posture: 'front' } },
      { id: `${id}-guard-b`, capacity: capacityId, move: { to: { anchor: guardB }, face: { anchor: facing }, posture: 'rear' } },
    ],
  };
  const play: LinkedPlay = {
    id, name, recipe: 'lane-closure',
    ownedGeometry: [
      { kind: 'anchor', id: hardlight, purpose: 'use.at and move.adjacentTo' },
      { kind: 'anchor', id: facing, purpose: 'move.face' },
      { kind: 'anchor', id: guardA, purpose: 'move.to' },
      { kind: 'anchor', id: guardB, purpose: 'move.to' },
    ],
    geometryGroups: [{
      id: `${id}-geometry`, kind: 'selection', objectIds: [hardlight, facing, guardA, guardB],
      derivedObjectIds: [],
    }],
    // These are independent capacity-limited move destinations, not an arming
    // formation. Keep them as visible anchors without manufacturing unused
    // readiness zones in the gameplay map plan.
    slots: [],
    ownedConditionIds: Object.values(conditions), ownedCapacityIds: [capacityId],
    projections: [masonProjection, guardProjection], linkedValues: [],
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

function relayProjection(
  playId: string,
  label: 'a' | 'b',
  roleId: string,
  partnerRoleId: string,
  parity: 0 | 1,
  stations: string[],
  hopRoutes: string[],
  conditions: Record<string, string>,
  draft: SheetDraft,
): PlayProjection {
  const ownedStations = stations.map((_, index) => index)
    .filter((index) => index % 2 === parity);
  const launchStations = ownedStations.filter((index) => index < stations.length - 1);
  const advancingLaunches = launchStations.filter((index) => index + 2 < stations.length);
  const terminalLaunch = launchStations.find((index) => index + 2 >= stations.length);
  const finalStation = ownedStations.at(-1)!;
  const orders: OrderNode[] = [];

  if (finalStation === stations.length - 1) orders.push({
    id: `${playId}-${label}-bank-r${finalStation}`,
    when: { all: [conditions.carrying, conditions[`selfAt${finalStation}`]] },
    urgency: 'urgent',
    move: { to: 'own-reactor' },
  });

  for (const station of advancingLaunches) orders.push({
    id: `${playId}-${label}-hop-r${station}-r${station + 2}`,
    when: { all: [conditions.carrying, conditions[`selfAt${station}`]] },
    // The carrier starts this lifecycle at its launch post. After Toss makes
    // it empty-handed, the same latched node advances it exactly one
    // same-Relay hop and holds that receiver post. It releases only after the
    // partner Tosses the Core back. Ending merely on arrival would let a later
    // station rule win before possession returned and break the leapfrog.
    until: { all: [conditions.carrying, conditions[`selfAt${station + 2}`]] },
    orders: [
      {
        id: `${playId}-${label}-toss-r${station}-r${station + 1}`,
        when: {
          all: [
            conditions.carrying,
            conditions[`roleAt${station + 1}`],
            conditions.signature,
          ],
        },
        use: { skill: 'arc-toss', target: { role: partnerRoleId } },
      },
      {
        id: `${playId}-${label}-hold-r${station}`,
        when: conditions.carrying,
        move: { to: { anchor: stations[station] } },
      },
      {
        id: `${playId}-${label}-advance-r${station + 2}`,
        move: { to: { anchor: stations[station + 2] }, via: hopRoutes[station] },
      },
    ],
  });

  if (terminalLaunch !== undefined) orders.push({
    id: `${playId}-${label}-final-launch-r${terminalLaunch}`,
    when: { all: [conditions.carrying, conditions[`selfAt${terminalLaunch}`]] },
    orders: [
      {
        id: `${playId}-${label}-toss-r${terminalLaunch}-r${terminalLaunch + 1}`,
        when: {
          all: [
            conditions[`roleAt${terminalLaunch + 1}`],
            conditions.signature,
          ],
        },
        use: { skill: 'arc-toss', target: { role: partnerRoleId } },
      },
      {
        id: `${playId}-${label}-hold-r${terminalLaunch}`,
        move: { to: { anchor: stations[terminalLaunch] } },
      },
    ],
  });

  orders.push({
    id: `${playId}-${label}-${parity === 0 ? 'collect' : 'stage'}-r${parity}`,
    move: { to: { anchor: stations[parity] } },
  });

  return {
    id: `${playId}-relay-${label}`,
    roleId,
    placement: { beforeOrderKey: defaultPriorityNeighbor(requireRole(draft, roleId)) },
    lastGenerated: [],
    orders,
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
    // A linked field owns its own editor value. Reusing the same condition
    // object for engage.when and assembly.until must never alias later edits.
    value: structuredClone(value),
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
