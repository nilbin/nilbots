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
} from './sheetAuthoring';
import type {
  LinkedPlay,
  PlayGeometryKind,
  PlayLinkedValue,
} from './workshopTypes';

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

function requirePlay(draft: SheetDraft, playId: string): LinkedPlay {
  const play = draft.workshop.plays.find((entry) => entry.id === playId);
  if (!play) throw new Error(`Unknown Play '${playId}'.`);
  return play;
}
