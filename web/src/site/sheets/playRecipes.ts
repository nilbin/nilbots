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

export function addBlankPlay(draft: SheetDraft, name = 'Untitled Program'): SheetDraft {
  const next = structuredClone(draft);
  const id = uniquePlayId('untitled-play', next.workshop);
  next.workshop.plays.push({
    id,
    name,
    recipe: 'blank',
    ownedGeometry: [],
    geometryGroups: [],
    slots: [],
    formations: [],
    readinessGroups: [],
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
    throw new Error(`Unknown blank Program '${playId}'.`);
  const role = requireRole(next, roleId);
  if (play.projections.some((entry) => entry.roleId === roleId))
    throw new Error(`Role '${roleId}' already participates in this Program.`);
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
    ambushProjection(id, roles.masonRoleId, 'mason', names),
    ambushProjection(id, roles.longshotRoleId, 'longshot', names),
    ambushProjection(id, roles.hushRoleId, 'hush', names),
    ambushProjection(id, roles.reserveRoleId, 'reserve', names),
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
  name = 'Running Relay corridor',
): SheetDraft {
  const next = structuredClone(draft);
  assertOneBodyClass(next, roles.relayARoleId, 'relay');
  assertOneBodyClass(next, roles.relayBRoleId, 'relay');
  if (roles.relayARoleId === roles.relayBRoleId)
    throw new Error('Choose two distinct one-body Relay roles.');

  const id = uniquePlayId('relay-chain', next.workshop);
  // The west-authored 58-step full-shadow south return from the enemy Well:
  // reach the east outer gate, cross the entire southern perimeter, then climb
  // the west approach to the home reactor. This is the map analysis contract's
  // actual outer corridor—not the shorter inner/lower return. The carrier and
  // empty runner share it continuously.
  //
  // Each rendezvous is a one-tile passing pocket beside the route. The empty
  // runner steps out of the corridor before the carrier arrives; after the
  // Toss, the former carrier takes the route tile while the new carrier's
  // ordinary relocation cooldown holds it in the pocket. That is what lets two
  // bodies alternate through a one-tile-wide outer corridor without either
  // blocking the other. Arc Toss is an eight-way straight throw, so every
  // source and pocket are an authored, terrain-clear pair.
  const corridorWaypoints: Array<[number, number]> = [
    [19, 20], [22, 23], [22, 22], [23, 21], [24, 22],
    [24, 23], [25, 23], [27, 23], [27, 25],
    [16, 25], [11, 25], [5, 25], [3, 25], [3, 23], [3, 19],
    [4, 17], [4, 16], [4, 15], [5, 14], [5, 12], [4, 11],
    [4, 9], [3, 8], [2, 7], [2, 5],
  ];
  const corridorRoute = `${id}-return-corridor`;
  const pickupAnchor = `${id}-enemy-well-pickup`;
  const homeAnchor = `${id}-home-reactor`;
  const homeZone = `${homeAnchor}-approach`;
  const rendezvousPoints: Array<[number, number]> = [
    [28, 23], [11, 25], [3, 19],
  ];
  const approachWaypointIndices = [7, 8, 12];
  const tossSourceWaypointIndices = [5, 9, 13];
  const forwardRouteTiles = [3, 5, 4];
  const rendezvous = rendezvousPoints.map((point, index) => ({
    index,
    id: `${id}-receiver-${index + 1}`,
    zone: `${id}-receiver-${index + 1}-ready`,
    approachZone: `${id}-receiver-${index + 1}-approach`,
    sourceZone: `${id}-toss-${index + 1}-source`,
    point,
    roleId: index % 2 === 0 ? roles.relayBRoleId : roles.relayARoleId,
    approachWaypointIndex: approachWaypointIndices[index],
    sourceWaypointIndex: tossSourceWaypointIndices[index],
    forwardRouteTiles: forwardRouteTiles[index],
  }));
  next.mapPlan.routes[corridorRoute] = { waypoints: corridorWaypoints };
  next.mapPlan.anchors[pickupAnchor] = [19, 20];
  next.mapPlan.anchors[homeAnchor] = [2, 5];
  for (const station of rendezvous)
    next.mapPlan.anchors[station.id] = station.point;
  for (const station of rendezvous) {
    next.mapPlan.zones[station.zone] = { rect: [...station.point, ...station.point] };
    const approach = corridorWaypoints[station.approachWaypointIndex];
    const source = corridorWaypoints[station.sourceWaypointIndex];
    if (station.index > 0)
      next.mapPlan.zones[station.approachZone] = { rect: [...approach, ...approach] };
    next.mapPlan.zones[station.sourceZone] = { rect: [...source, ...source] };
  }
  // Entering this small terminal approach switches both roles off the
  // repeating patrol before it can lap. The carrier walks the final two
  // tiles into the reactor; the empty runner holds beside, never on, the
  // scoring tile.
  next.mapPlan.zones[homeZone] = { rect: [3, 6, 5, 8] };

  const conditionIds: Record<string, string> = {
    carrying: `${id}-carrying-core`,
    empty: `${id}-empty-handed`,
    teamCarrying: `${id}-team-carrying`,
    signature: `${id}-signature-ready`,
    justTossed: `${id}-just-tossed`,
    atHome: `${id}-at-home-reactor`,
  };
  for (let index = 0; index < rendezvous.length; index += 1) {
    conditionIds[`receiver${index + 1}`] = `${id}-receiver-${index + 1}-occupied`;
    if (index > 0)
      conditionIds[`approach${index + 1}`] = `${id}-receiver-${index + 1}-at-approach`;
    conditionIds[`source${index + 1}`] = `${id}-carrier-${index + 1}-at-source`;
  }
  const conditions: Record<string, ConditionDefinition> = {
    [conditionIds.carrying]: { fact: 'self-carrying', operator: 'equals', value: 1 },
    [conditionIds.empty]: { fact: 'self-carrying', operator: 'equals', value: 0 },
    [conditionIds.teamCarrying]: { fact: 'friendly-carriers', operator: 'at-least', value: 1 },
    [conditionIds.signature]: { fact: 'signature-ready', operator: 'equals', value: 1 },
    [conditionIds.justTossed]: {
      fact: 'self-ticks-since-signature-use',
      operator: 'at-most',
      value: 12,
    },
    [conditionIds.atHome]: {
      fact: 'self-in-zone', operator: 'equals', value: 1, zone: homeZone,
    },
  };
  for (let index = 0; index < rendezvous.length; index += 1) {
    const station = rendezvous[index];
    conditions[conditionIds[`receiver${index + 1}`]] = {
      fact: 'role-in-zone-count',
      operator: 'at-least',
      value: 1,
      role: station.roleId,
      zone: station.zone,
    };
    if (index > 0) {
      conditions[conditionIds[`approach${index + 1}`]] = {
        fact: 'self-in-zone', operator: 'equals', value: 1,
        zone: station.approachZone,
      };
    }
    conditions[conditionIds[`source${index + 1}`]] = {
      fact: 'self-in-zone', operator: 'equals', value: 1,
      zone: station.sourceZone,
    };
  }
  Object.assign(next.orders.conditions, conditions);

  const receiverStagesFor = (roleId: string): RelayReceiverStage[] =>
    rendezvous.slice(1).flatMap((station, index) => station.roleId === roleId
      ? [{
        anchorId: station.id,
        previousReadyCondition: conditionIds[`receiver${index + 1}`],
        approachCondition: conditionIds[`approach${index + 2}`],
        readyCondition: conditionIds[`receiver${index + 2}`],
      }]
      : []);
  const tossStagesFor = (roleId: string): RelayTossStage[] =>
    rendezvous.flatMap((station, index) => station.roleId === roleId
      ? [{
        receiverReadyCondition: conditionIds[`receiver${index + 1}`],
        sourceCondition: conditionIds[`source${index + 1}`],
      }]
      : []);
  const aProjection = relayProjection(
    id, 'a', roles.relayARoleId, roles.relayBRoleId,
    pickupAnchor,
    corridorRoute,
    homeAnchor,
    receiverStagesFor(roles.relayARoleId),
    tossStagesFor(roles.relayBRoleId),
    conditionIds,
  );
  const bProjection = relayProjection(
    id, 'b', roles.relayBRoleId, roles.relayARoleId,
    rendezvous[0].id,
    corridorRoute,
    homeAnchor,
    receiverStagesFor(roles.relayBRoleId),
    tossStagesFor(roles.relayARoleId),
    conditionIds,
  );
  const projections = [aProjection, bProjection];
  const play: LinkedPlay = {
    id,
    name,
    recipe: 'relay-chain',
    ownedGeometry: [
      { kind: 'route', id: corridorRoute, purpose: 'shared movement continuation and staged Toss progress' },
      { kind: 'anchor', id: pickupAnchor, purpose: 'move.to' },
      { kind: 'anchor', id: homeAnchor, purpose: 'move.to' },
      ...rendezvous.map((station) => ({
        kind: 'anchor' as const,
        id: station.id,
        purpose: 'move.to and arc-toss receiver',
      })),
      ...rendezvous.map((station) => ({
        kind: 'zone' as const,
        id: station.zone,
        purpose: 'role-in-zone-count',
        derivedFrom: station.id,
        tolerance: 0,
      })),
      ...rendezvous.flatMap((station) => ([
        ...(station.index > 0 ? [{
          kind: 'zone' as const,
          id: station.approachZone,
          purpose: 'self-in-zone receiver approach',
        }] : []),
        {
          kind: 'zone' as const,
          id: station.sourceZone,
          purpose: 'self-in-zone toss source',
        },
      ])),
      { kind: 'zone', id: homeZone, purpose: 'self-in-zone' },
    ],
    geometryGroups: [{
      id: `${id}-corridor`,
      kind: 'selection',
      objectIds: [
        corridorRoute,
        pickupAnchor,
        homeAnchor,
        ...rendezvous.map((station) => station.id),
        homeZone,
      ],
      derivedObjectIds: rendezvous.map((station) => station.zone),
      tossLinks: rendezvous.map((station) => ({
        routeId: corridorRoute,
        sourceWaypointIndex: station.sourceWaypointIndex,
        receiverAnchorId: station.id,
        forwardRouteTiles: station.forwardRouteTiles,
      })),
    }],
    slots: [],
    ownedConditionIds: Object.keys(conditions),
    ownedCapacityIds: [],
    projections,
    linkedValues: [],
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
    lastGenerated: [],
    orders: [{
      id: `${id}-veteran-route`, when: conditionIds.active,
      move: { patrol: routeId, posture: 'front' },
    }],
  };
  const escortProjection: PlayProjection = {
    id: `${id}-escort`, roleId: roles.escortRoleId,
    lastGenerated: [],
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
  requireRole(next, roles.relayRoleId);
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
    lastGenerated: [],
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
    lastGenerated: [],
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
    lastGenerated: [],
    orders: [{ id: `${id}-mason-closure`, orders: [
      { id: `${id}-place-hardlight`, when: conditions.signature, use: { skill: 'hardlight-block', target: 'nearest-ally', at: hardlight } },
      { id: `${id}-stage-mason`, urgency: 'normal', move: { to: { adjacentTo: hardlight } } },
    ] }],
  };
  const guardProjection: PlayProjection = {
    id: `${id}-guards`, roleId: roles.guardRoleId,
    lastGenerated: [],
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
    lastGenerated: [],
  };
}

interface RelayReceiverStage {
  anchorId: string;
  previousReadyCondition: string;
  approachCondition: string;
  readyCondition: string;
}

interface RelayTossStage {
  receiverReadyCondition: string;
  sourceCondition: string;
}

function relayProjection(
  playId: string,
  label: 'a' | 'b',
  roleId: string,
  partnerRoleId: string,
  startAnchor: string,
  corridorRoute: string,
  homeAnchor: string,
  receiverStages: RelayReceiverStage[],
  tossStages: RelayTossStage[],
  conditions: Record<string, string>,
): PlayProjection {
  return {
    id: `${playId}-relay-${label}`,
    roleId,
    lastGenerated: [],
    orders: [
      ...receiverStages.map((stage, index): OrderNode => ({
        id: `${playId}-${label}-receive-${index + 1}`,
        when: {
          all: [
            conditions.empty,
            conditions.justTossed,
            stage.previousReadyCondition,
          ],
        },
        until: conditions.carrying,
        // A disrupted catch must not strand the former carrier forever. The
        // full outer perimeter puts 29 route steps between the first two
        // passing pockets; 80 preserves that clean run while still bounding
        // combat or pathing failure.
        timeout: 80,
        urgency: 'urgent',
        orders: [
          {
            id: `${playId}-${label}-receive-${index + 1}-hold`,
            when: stage.readyCondition,
            move: { to: { anchor: stage.anchorId } },
          },
          {
            id: `${playId}-${label}-receive-${index + 1}-enter-pocket`,
            when: stage.approachCondition,
            move: { to: { anchor: stage.anchorId } },
          },
          {
            id: `${playId}-${label}-receive-${index + 1}-run-corridor`,
            move: { patrol: corridorRoute },
          },
        ],
      })),
      {
        id: `${playId}-${label}-running-return`,
        when: conditions.teamCarrying,
        orders: [
          ...tossStages.map((stage, index): OrderNode => ({
            id: `${playId}-${label}-forward-toss-${index + 1}`,
            when: {
              all: [
                conditions.carrying,
                conditions.signature,
                stage.sourceCondition,
                stage.receiverReadyCondition,
              ],
            },
            use: {
              skill: 'arc-toss',
              // Exact source and receiver Posts already prove the staged
              // relationship. Keep the executable target an ordinary named
              // role; route progress is authoring-time geometry, not a
              // hidden race selector.
              target: { role: partnerRoleId },
            },
          })),
          {
            id: `${playId}-${label}-bank-final-approach`,
            when: { all: [conditions.carrying, conditions.atHome] },
            // Crossing the approach zone starts one banking episode. Keep it
            // latched while the carrier walks out of that small trigger zone;
            // otherwise the route rung wins again and pulls the Core backward.
            until: conditions.empty,
            urgency: 'urgent',
            move: { to: { anchor: homeAnchor } },
          },
          {
            id: `${playId}-${label}-carry-forward`,
            when: conditions.carrying,
            urgency: 'normal',
            move: { patrol: corridorRoute },
          },
        ],
      },
      {
        id: `${playId}-${label}-run-after-toss`,
        when: {
          all: [
            conditions.empty,
            conditions.justTossed,
          ],
        },
        urgency: 'normal',
        move: { patrol: corridorRoute },
      },
      {
        id: `${playId}-${label}-take-start`,
        move: { to: { anchor: startAnchor } },
      },
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
