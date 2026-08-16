import {
  materializeWorkshop,
  replaceProjectionOrders,
  type PlayDrift,
} from './linkedPlays';
import { synchronizePlayGeometry } from './playGeometry';
import type {
  ConditionDefinition,
  OrderNode,
  SheetDraft,
  UseOrder,
} from './sheetAuthoring';
import type {
  LinkedPlay,
  PlayGeometryKind,
  PlayLinkedValue,
  PlayProjection,
} from './workshopTypes';

export function addSignatureOpportunity(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  hostOrderId: string,
  use: UseOrder,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const located = findOrderLocation(projection.orders, hostOrderId);
  if (!located) throw new Error(`Unknown host Order '${hostOrderId}'.`);

  let host = located.node;
  if (!host.orders) {
    if (!host.move && !host.fight)
      throw new Error('Choose a durable Move, Fight, or existing branch as the continuation.');
    if (located.depth >= 2)
      throw new Error('This Order is already at the maximum branch depth. Choose its nearest parent branch.');
    const continuationId = uniqueOrderId(`${hostOrderId}-continue`, projection.orders);
    const continuation: OrderNode = {
      id: continuationId,
      ...(host.move ? { move: structuredClone(host.move) } : {}),
      ...(host.fight ? { fight: structuredClone(host.fight) } : {}),
      ...(host.capacity !== undefined ? { capacity: host.capacity } : {}),
    };
    const parent: OrderNode = {
      id: host.id,
      ...(host.when !== undefined ? { when: structuredClone(host.when) } : {}),
      ...(host.until !== undefined ? { until: structuredClone(host.until) } : {}),
      ...(host.timeout !== undefined ? { timeout: host.timeout } : {}),
      ...(host.cooldown !== undefined ? { cooldown: host.cooldown } : {}),
      ...(host.urgency !== undefined ? { urgency: host.urgency } : {}),
      orders: [continuation],
    };
    located.container[located.index] = parent;
    host = parent;
  }

  const useOrderId = uniqueOrderId(`${hostOrderId}-${use.skill}`, projection.orders);
  const useNode: OrderNode = { id: useOrderId, use: structuredClone(use) };
  const bindings = projection.signatureOpportunities ??= [];
  const boundIds = new Set(bindings.map((entry) => entry.useOrderId));
  const firstContinuation = host.orders!.findIndex((child) =>
    !child.id || !boundIds.has(child.id));
  host.orders!.splice(firstContinuation < 0 ? host.orders!.length : firstContinuation,
    0, useNode);
  const bindingBase = `${hostOrderId}-${useOrderId}-signature`;
  const bindingIds = new Set(play.projections.flatMap((entry) =>
    (entry.signatureOpportunities ?? []).map((binding) => binding.id)));
  projection.signatureOpportunities.push({
    id: uniqueString(bindingBase, bindingIds),
    hostOrderId,
    useOrderId,
  });
  return requireMaterialized(next);
}

export function updateSignatureOpportunityUse(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  bindingId: string,
  use: UseOrder,
): SheetDraft {
  return updateOpportunityNode(draft, playId, projectionId, bindingId,
    (node) => { node.use = structuredClone(use); });
}

export function setSignatureOpportunityWhen(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  bindingId: string,
  when: OrderNode['when'],
): SheetDraft {
  return updateOpportunityNode(draft, playId, projectionId, bindingId,
    (node) => {
      if (when === undefined) delete node.when;
      else node.when = structuredClone(when);
    });
}

export function setSignatureOpportunityCapacity(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  bindingId: string,
  capacity: OrderNode['capacity'],
): SheetDraft {
  return updateOpportunityNode(draft, playId, projectionId, bindingId,
    (node) => {
      if (capacity === undefined) delete node.capacity;
      else node.capacity = capacity;
    });
}

export function addSignatureOpportunityActivationZone(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  bindingId: string,
): { draft: SheetDraft; zoneId: string } {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const binding = requireBinding(projection, bindingId);
  const node = requireOpportunityNode(projection, binding);
  const zoneId = uniqueString(`${binding.useOrderId}-window`, new Set([
    ...Object.keys(next.mapPlan.zones),
    ...Object.keys(next.mapPlan.routes),
    ...Object.keys(next.mapPlan.anchors),
  ]));
  const conditionId = uniqueString(`${zoneId}-active`, new Set(
    Object.keys(next.orders.conditions)));
  const routeId = play.ownedGeometry.find((entry) => entry.kind === 'route')?.id;
  const route = routeId ? next.mapPlan.routes[routeId] : undefined;
  const anchorId = play.ownedGeometry.find((entry) =>
    entry.kind === 'anchor' && !entry.derivedFrom)?.id;
  const anchor = anchorId ? next.mapPlan.anchors[anchorId] : undefined;
  const points = route?.waypoints.slice(0, 2)
    ?? (anchor ? [anchor] : [[15, 13] as [number, number]]);
  const xs = points.map((point) => point[0]);
  const ys = points.map((point) => point[1]);
  next.mapPlan.zones[zoneId] = {
    rect: [Math.min(...xs), Math.min(...ys), Math.max(...xs), Math.max(...ys)],
  };
  next.orders.conditions[conditionId] = {
    fact: 'self-in-zone', operator: 'equals', value: 1, zone: zoneId,
  };
  play.ownedGeometry.push({
    kind: 'zone', id: zoneId, purpose: 'self-in-zone signature activation',
  });
  play.ownedConditionIds.push(conditionId);
  if (!node.when) node.when = conditionId;
  else if (typeof node.when === 'string') node.when = { all: [node.when, conditionId] };
  else if ('all' in node.when && node.when.all.length < 8)
    node.when.all.push(conditionId);
  else throw new Error('This opportunity already uses the flat condition-expression limit.');
  return { draft: requireMaterialized(next), zoneId };
}

export function moveSignatureOpportunity(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  bindingId: string,
  direction: -1 | 1,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const binding = requireBinding(projection, bindingId);
  const host = findOrderLocation(projection.orders, binding.hostOrderId)?.node;
  if (!host?.orders) throw new Error(`Opportunity host '${binding.hostOrderId}' is missing.`);
  const siblings = (projection.signatureOpportunities ?? [])
    .filter((entry) => entry.hostOrderId === binding.hostOrderId)
    .map((entry) => host.orders!.findIndex((node) => node.id === entry.useOrderId))
    .filter((index) => index >= 0)
    .sort((left, right) => left - right);
  const current = host.orders.findIndex((node) => node.id === binding.useOrderId);
  const ordinal = siblings.indexOf(current);
  const target = siblings[ordinal + direction];
  if (target === undefined) return next;
  [host.orders[current], host.orders[target]] = [host.orders[target], host.orders[current]];
  return requireMaterialized(next);
}

export function renameSignatureOpportunity(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  bindingId: string,
  nextOrderId: string,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const binding = requireBinding(projection, bindingId);
  if (findOrderLocation(projection.orders, nextOrderId))
    throw new Error(`Order '${nextOrderId}' already exists in this projection.`);
  const node = requireOpportunityNode(projection, binding);
  const previous = binding.useOrderId;
  node.id = nextOrderId;
  binding.useOrderId = nextOrderId;
  for (const linked of play.linkedValues)
    for (const target of linked.targets)
      if (target.projectionId === projection.id && target.orderId === previous)
        target.orderId = nextOrderId;
  return requireMaterialized(next);
}

export function removeSignatureOpportunity(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  bindingId: string,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const binding = requireBinding(projection, bindingId);
  const host = findOrderLocation(projection.orders, binding.hostOrderId)?.node;
  if (!host?.orders) throw new Error(`Opportunity host '${binding.hostOrderId}' is missing.`);
  host.orders = host.orders.filter((child) => child.id !== binding.useOrderId);
  projection.signatureOpportunities = (projection.signatureOpportunities ?? [])
    .filter((entry) => entry.id !== bindingId);
  for (const linked of play.linkedValues)
    linked.targets = linked.targets.filter((target) =>
      target.projectionId !== projectionId || target.orderId !== binding.useOrderId);
  play.linkedValues = play.linkedValues.filter((linked) => linked.targets.length > 0);
  return requireMaterialized(next);
}

export function rematerializeDraft(
  draft: SheetDraft,
  force = false,
): { draft: SheetDraft; drifts: PlayDrift[] } {
  const next = structuredClone(draft);
  const result = materializeWorkshop(next.orders, next.workshop, force);
  next.orders = result.orders;
  next.workshop = result.workshop;
  return { draft: next, drifts: result.drifts };
}

export function setPlayName(
  draft: SheetDraft,
  playId: string,
  name: string,
): SheetDraft {
  const next = structuredClone(draft);
  requirePlay(next, playId).name = name;
  return next;
}

export function setPlayLinkedValue(
  draft: SheetDraft,
  playId: string,
  linkedId: string,
  value: PlayLinkedValue,
): SheetDraft {
  const next = structuredClone(draft);
  const linked = requirePlay(next, playId).linkedValues.find((entry) => entry.id === linkedId);
  if (!linked) throw new Error(`Unknown linked value '${linkedId}'.`);
  linked.value = structuredClone(value);
  return requireMaterialized(next);
}

export function setPlayProjectionPlacement(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  beforeOrderKey: string | null,
): SheetDraft {
  const next = structuredClone(draft);
  const projection = requirePlay(next, playId).projections.find((entry) => entry.id === projectionId);
  if (!projection) throw new Error(`Unknown projection '${projectionId}'.`);
  projection.placement.beforeOrderKey = beforeOrderKey;
  return requireMaterialized(next);
}

export function updatePlayProjectionOrders(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  orders: OrderNode[],
): SheetDraft {
  const next = structuredClone(draft);
  next.workshop = replaceProjectionOrders(
    next.workshop,
    playId,
    projectionId,
    orders,
  );
  return requireMaterialized(next);
}

/**
 * Replace one participant role across a linked Play. Recipes use one semantic
 * role per projection, so owned readiness conditions, slots, and partner role
 * targets move with the projection in the same undoable edit.
 */
export function replacePlayRole(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  nextRoleId: string,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = play.projections.find((entry) => entry.id === projectionId);
  if (!projection) throw new Error(`Unknown projection '${projectionId}'.`);
  if (!next.orders.roles.some((role) => role.id === nextRoleId))
    throw new Error(`Unknown role '${nextRoleId}'.`);
  const previousRoleId = projection.roleId;
  if (previousRoleId === nextRoleId) return next;

  const previousRole = next.orders.roles.find((role) => role.id === previousRoleId);
  const ownedIds = new Set(projection.lastGenerated.map((entry) => entry.orderId));
  if (previousRole)
    previousRole.orders = previousRole.orders.filter((order) => !order.id || !ownedIds.has(order.id));

  projection.roleId = nextRoleId;
  projection.lastGenerated = [];
  const targetRole = next.orders.roles.find((role) => role.id === nextRoleId)!;
  if (projection.placement.beforeOrderKey
    && !targetRole.orders.some((order, index) =>
      orderKey(order, index) === projection.placement.beforeOrderKey)) {
    const fallback = targetRole.orders.at(-1);
    projection.placement.beforeOrderKey = fallback
      ? orderKey(fallback, targetRole.orders.length - 1)
      : null;
  }
  for (const slot of play.slots)
    if (slot.roleId === previousRoleId) slot.roleId = nextRoleId;
  for (const conditionId of play.ownedConditionIds) {
    const condition = next.orders.conditions[conditionId];
    if (condition?.role === previousRoleId) condition.role = nextRoleId;
  }
  for (const candidate of play.projections)
    candidate.orders.forEach((order) => walkOrder(order, (node) => replaceRoleReference(node, previousRoleId, nextRoleId)));
  return requireMaterialized(next);
}

export function updatePlayCondition(
  draft: SheetDraft,
  playId: string,
  conditionId: string,
  condition: ConditionDefinition,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  if (!play.ownedConditionIds.includes(conditionId))
    throw new Error(`Condition '${conditionId}' is not owned by this Play.`);
  next.orders.conditions[conditionId] = structuredClone(condition);
  return next;
}

/**
 * Add one more required post to an Ambush reserve pool. The post remains an
 * ordinary anchor + readiness zone + role-qualified condition, and the
 * reserve projection gains one ordinary capacity-limited move leaf. Nothing
 * in the runtime learns that the objects came from a Play.
 */
export function addAmbushReserveSlot(
  draft: SheetDraft,
  playId: string,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  if (play.recipe !== 'ambush')
    throw new Error('Pooled reserve posts are available only in the Ambush recipe.');
  const reserveProjection = play.projections.find((entry) => entry.id.endsWith('-reserve'));
  if (!reserveProjection)
    throw new Error('The Ambush has no reserve projection.');
  const reserveSlots = play.slots.filter((slot) => slot.roleId === reserveProjection.roleId);
  const ordinal = reserveSlots.length;
  const suffix = String.fromCharCode('a'.charCodeAt(0) + ordinal);
  const anchorId = `${play.id}-reserve-${suffix}`;
  const zoneId = `${anchorId}-ready`;
  const conditionId = `${anchorId}-in-position`;
  const slotId = `${anchorId}-slot`;
  if (next.mapPlan.anchors[anchorId] || next.mapPlan.zones[zoneId])
    throw new Error(`Ambush reserve post '${anchorId}' already exists.`);

  const previous = next.mapPlan.anchors[reserveSlots.at(-1)?.anchorId ?? ''] ?? [21, 8];
  const facingAnchor = play.ownedGeometry.find((entry) =>
    entry.kind === 'anchor' && entry.purpose === 'move.face')?.id;
  if (!facingAnchor) throw new Error('The Ambush corridor-facing anchor is missing.');
  const engageWhen = play.linkedValues.find((entry) => entry.id === `${play.id}-engage-when`);
  if (engageWhen && typeof engageWhen.value === 'object' && engageWhen.value !== null
    && 'all' in engageWhen.value && engageWhen.value.all.length >= 8)
    throw new Error('This Ambush already uses the eight-condition flat expression limit.');
  const point: [number, number] = [Math.max(1, previous[0] - 2), Math.min(25, previous[1] + 2)];
  next.mapPlan.anchors[anchorId] = point;
  next.mapPlan.zones[zoneId] = { rect: [point[0], point[1], point[0], point[1]] };
  next.orders.conditions[conditionId] = {
    fact: 'role-in-zone-count', operator: 'at-least', value: 1,
    role: reserveProjection.roleId, zone: zoneId,
  };
  play.ownedGeometry.push(
    { kind: 'anchor', id: anchorId, purpose: 'move.to' },
    { kind: 'zone', id: zoneId, purpose: 'role-in-zone-count', derivedFrom: anchorId, tolerance: 0 },
  );
  const group = play.geometryGroups[0];
  group?.objectIds.push(anchorId);
  group?.derivedObjectIds.push(zoneId);
  const capacity = reserveSlots[0]?.capacity ?? 1;
  play.slots.push({
    id: slotId,
    roleId: reserveProjection.roleId,
    anchorId,
    readinessZoneId: zoneId,
    required: true,
    tolerance: 0,
    capacity,
  });
  play.ownedConditionIds.push(conditionId);
  appendConditionToLinkedValue(play, `${play.id}-engage-when`, conditionId);
  appendConditionToLinkedValue(play, `${play.id}-assembly-until`, conditionId);
  const assembly = reserveProjection.orders.find((order) => order.id?.endsWith('-reserve-assembly'));
  if (!assembly?.orders)
    throw new Error('The Ambush reserve assembly branch is missing.');
  assembly.orders.push({
    id: `${play.id}-reserve-${suffix}`,
    capacity,
    move: {
      to: { anchor: anchorId },
      face: { anchor: facingAnchor },
      posture: 'front',
    },
  });
  return requireMaterialized(next);
}

export function removeAmbushReserveSlot(
  draft: SheetDraft,
  playId: string,
  slotId: string,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  if (play.recipe !== 'ambush')
    throw new Error('Pooled reserve posts are available only in the Ambush recipe.');
  const slot = play.slots.find((entry) => entry.id === slotId);
  if (!slot) throw new Error(`Unknown Ambush slot '${slotId}'.`);
  const reserveProjection = play.projections.find((entry) => entry.id.endsWith('-reserve'));
  const reserveSlots = reserveProjection
    ? play.slots.filter((entry) => entry.roleId === reserveProjection.roleId)
    : [];
  if (!reserveProjection || slot.roleId !== reserveProjection.roleId || reserveSlots.length <= 2)
    throw new Error('An Ambush keeps at least two pooled reserve posts.');
  const conditionId = play.ownedConditionIds.find((id) =>
    next.orders.conditions[id]?.zone === slot.readinessZoneId
    && next.orders.conditions[id]?.role === slot.roleId);
  play.slots = play.slots.filter((entry) => entry.id !== slot.id);
  play.ownedGeometry = play.ownedGeometry.filter((entry) =>
    entry.id !== slot.anchorId && entry.id !== slot.readinessZoneId);
  for (const group of play.geometryGroups) {
    group.objectIds = group.objectIds.filter((id) => id !== slot.anchorId && id !== slot.readinessZoneId);
    group.derivedObjectIds = group.derivedObjectIds.filter((id) => id !== slot.anchorId && id !== slot.readinessZoneId);
  }
  delete next.mapPlan.anchors[slot.anchorId];
  delete next.mapPlan.zones[slot.readinessZoneId];
  if (conditionId) {
    delete next.orders.conditions[conditionId];
    play.ownedConditionIds = play.ownedConditionIds.filter((id) => id !== conditionId);
    removeConditionFromLinkedValue(play, `${play.id}-engage-when`, conditionId);
    removeConditionFromLinkedValue(play, `${play.id}-assembly-until`, conditionId);
  }
  const assembly = reserveProjection.orders.find((order) => order.id?.endsWith('-reserve-assembly'));
  if (assembly?.orders)
    assembly.orders = assembly.orders.filter((order) => order.move?.to === undefined
      || typeof order.move.to !== 'object'
      || !('anchor' in order.move.to)
      || order.move.to.anchor !== slot.anchorId);
  return requireMaterialized(next);
}

export function movePlayAnchor(
  draft: SheetDraft,
  playId: string,
  anchorId: string,
  point: [number, number],
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  if (!play.ownedGeometry.some((entry) => entry.kind === 'anchor' && entry.id === anchorId))
    throw new Error(`Anchor '${anchorId}' is not owned by this Play.`);
  next.mapPlan.anchors[anchorId] = point;
  return synchronizePlayGeometry(next, playId);
}

export function renamePlayGeometry(
  draft: SheetDraft,
  playId: string,
  kind: PlayGeometryKind,
  previous: string,
  nextId: string,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  if (!play.ownedGeometry.some((entry) => entry.kind === kind && entry.id === previous))
    throw new Error(`${kind} '${previous}' is not owned by this Play.`);
  renameMapObject(next, kind, previous, nextId);
  for (const owned of play.ownedGeometry) {
    if (owned.kind === kind && owned.id === previous) owned.id = nextId;
    if (owned.derivedFrom === previous) owned.derivedFrom = nextId;
  }
  for (const group of play.geometryGroups) {
    group.objectIds = group.objectIds.map((id) => id === previous ? nextId : id);
    group.derivedObjectIds = group.derivedObjectIds.map((id) => id === previous ? nextId : id);
    if (group.stationWaypointIndexes?.[previous] !== undefined) {
      group.stationWaypointIndexes[nextId] = group.stationWaypointIndexes[previous];
      delete group.stationWaypointIndexes[previous];
    }
    for (const slice of group.routeSlices ?? [])
      if (slice.routeId === previous) slice.routeId = nextId;
    for (const link of group.tossLinks ?? []) {
      if (kind === 'route' && link.routeId === previous) link.routeId = nextId;
      if (kind === 'anchor' && link.receiverAnchorId === previous)
        link.receiverAnchorId = nextId;
    }
  }
  for (const slot of play.slots) {
    if (slot.anchorId === previous) slot.anchorId = nextId;
    if (slot.readinessZoneId === previous) slot.readinessZoneId = nextId;
  }
  return requireMaterialized(next, true);
}

function requireMaterialized(draft: SheetDraft, force = false): SheetDraft {
  const result = materializeWorkshop(draft.orders, draft.workshop, force);
  if (result.drifts.length > 0)
    throw new Error(result.drifts.map((entry) => entry.message).join(' '));
  draft.orders = result.orders;
  draft.workshop = result.workshop;
  return draft;
}

function appendConditionToLinkedValue(play: LinkedPlay, linkedId: string, conditionId: string) {
  const linked = play.linkedValues.find((entry) => entry.id === linkedId);
  if (!linked || typeof linked.value !== 'object' || linked.value === null || !('all' in linked.value))
    throw new Error(`Linked condition '${linkedId}' is missing.`);
  linked.value.all.push(conditionId);
}

function removeConditionFromLinkedValue(play: LinkedPlay, linkedId: string, conditionId: string) {
  const linked = play.linkedValues.find((entry) => entry.id === linkedId);
  if (linked && typeof linked.value === 'object' && linked.value !== null && 'all' in linked.value)
    linked.value.all = linked.value.all.filter((id) => id !== conditionId);
}

function renameMapObject(
  draft: SheetDraft,
  kind: PlayGeometryKind,
  previous: string,
  nextId: string,
) {
  if (kind === 'anchor') {
    if (draft.mapPlan.anchors[nextId]) throw new Error(`Anchor '${nextId}' already exists.`);
    draft.mapPlan.anchors[nextId] = draft.mapPlan.anchors[previous];
    delete draft.mapPlan.anchors[previous];
  } else if (kind === 'zone') {
    if (draft.mapPlan.zones[nextId]) throw new Error(`Zone '${nextId}' already exists.`);
    draft.mapPlan.zones[nextId] = draft.mapPlan.zones[previous];
    delete draft.mapPlan.zones[previous];
  } else {
    if (draft.mapPlan.routes[nextId]) throw new Error(`Route '${nextId}' already exists.`);
    draft.mapPlan.routes[nextId] = draft.mapPlan.routes[previous];
    delete draft.mapPlan.routes[previous];
  }
  for (const condition of Object.values(draft.orders.conditions))
    if (kind === 'zone' && condition.zone === previous) condition.zone = nextId;
  walkAllOrders(draft, (order) => replaceGeometryReference(order, kind, previous, nextId));
}

function walkAllOrders(draft: SheetDraft, visit: (order: OrderNode) => void) {
  draft.orders.roles.forEach((role) => role.orders.forEach((order) => walkOrder(order, visit)));
  Object.values(draft.orders.sharedOrders).forEach((order) => walkOrder(order, visit));
  draft.workshop.plays.forEach((play) => play.projections.forEach((projection) =>
    projection.orders.forEach((order) => walkOrder(order, visit))));
}

function walkOrder(order: OrderNode, visit: (order: OrderNode) => void) {
  visit(order);
  order.orders?.forEach((child) => walkOrder(child, visit));
}

function replaceGeometryReference(
  order: OrderNode,
  kind: PlayGeometryKind,
  previous: string,
  nextId: string,
) {
  if (kind === 'route') {
    if (order.move?.patrol === previous) order.move.patrol = nextId;
    if (order.move?.via === previous) order.move.via = nextId;
  }
  if (kind === 'zone' && typeof order.move?.to === 'object'
    && 'zone' in order.move.to && order.move.to.zone === previous)
    order.move.to.zone = nextId;
  if (kind === 'anchor' && typeof order.move?.to === 'object') {
    if ('anchor' in order.move.to && order.move.to.anchor === previous)
      order.move.to.anchor = nextId;
    if ('adjacentTo' in order.move.to && order.move.to.adjacentTo === previous)
      order.move.to.adjacentTo = nextId;
  }
  if (kind === 'anchor' && order.move?.face?.anchor === previous)
    order.move.face.anchor = nextId;
  if (kind === 'anchor' && order.use?.at === previous) order.use.at = nextId;
}

function replaceRoleReference(order: OrderNode, previous: string, nextId: string) {
  if (order.move?.follow === previous) order.move.follow = nextId;
  if (typeof order.use?.target === 'object' && 'role' in order.use.target
    && order.use.target.role === previous)
    order.use.target.role = nextId;
  if (typeof order.handoff?.target === 'object' && order.handoff.target.role === previous)
    order.handoff.target.role = nextId;
}

function orderKey(order: OrderNode, index: number): string {
  if (order.id) return `id:${order.id}`;
  if (order.shared) return `shared:${order.shared}`;
  return `index:${index}`;
}

function updateOpportunityNode(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  bindingId: string,
  mutate: (node: OrderNode) => void,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const binding = requireBinding(projection, bindingId);
  mutate(requireOpportunityNode(projection, binding));
  return requireMaterialized(next);
}

function requireProjection(play: LinkedPlay, projectionId: string): PlayProjection {
  const projection = play.projections.find((entry) => entry.id === projectionId);
  if (!projection) throw new Error(`Unknown projection '${projectionId}'.`);
  return projection;
}

function requireBinding(
  projection: PlayProjection,
  bindingId: string,
) {
  const binding = (projection.signatureOpportunities ?? [])
    .find((entry) => entry.id === bindingId);
  if (!binding) throw new Error(`Unknown signature opportunity '${bindingId}'.`);
  return binding;
}

function requireOpportunityNode(
  projection: PlayProjection,
  binding: NonNullable<PlayProjection['signatureOpportunities']>[number],
): OrderNode {
  const host = findOrderLocation(projection.orders, binding.hostOrderId)?.node;
  const node = host?.orders?.find((entry) => entry.id === binding.useOrderId);
  if (!node?.use)
    throw new Error(`Signature opportunity '${binding.id}' has no direct use child.`);
  return node;
}

function findOrderLocation(
  orders: OrderNode[],
  id: string,
  depth = 0,
): { node: OrderNode; container: OrderNode[]; index: number; depth: number } | null {
  for (const [index, node] of orders.entries()) {
    if (node.id === id) return { node, container: orders, index, depth };
    if (node.orders) {
      const nested = findOrderLocation(node.orders, id, depth + 1);
      if (nested) return nested;
    }
  }
  return null;
}

function uniqueOrderId(base: string, orders: OrderNode[]): string {
  const used = new Set<string>();
  orders.forEach((order) => walkOrder(order, (node) => {
    if (node.id) used.add(node.id);
  }));
  return uniqueString(base, used);
}

function uniqueString(base: string, used: ReadonlySet<string>): string {
  if (!used.has(base)) return base;
  for (let suffix = 2; ; suffix++)
    if (!used.has(`${base}-${suffix}`)) return `${base}-${suffix}`;
}

function requirePlay(draft: SheetDraft, playId: string): LinkedPlay {
  const play = draft.workshop.plays.find((entry) => entry.id === playId);
  if (!play) throw new Error(`Unknown Play '${playId}'.`);
  return play;
}
