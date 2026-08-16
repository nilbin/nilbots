import type {
  ConditionExpression,
  MapPlanDocument,
  OrderNode,
  OrdersDocument,
  SheetDraft,
} from './sheetAuthoring';
import type { TacticalSheetCatalog } from '../api';
import {
  emptyWorkshop,
  normalizeWorkshop,
  type LinkedPlay,
  type PlayGeometryKind,
  type PlayLinkedValue,
  type PlayLinkedValueDefinition,
  type PlayProjection,
  type WorkshopDocument,
} from './workshopTypes';

export interface PlayDrift {
  playId: string;
  projectionId: string;
  roleId: string;
  orderId?: string;
  kind: 'role-missing' | 'order-missing' | 'order-changed' | 'order-collision'
    | 'priority-neighbor-missing' | 'opportunity-binding-invalid'
    | 'formation-binding-invalid';
  message: string;
}

export interface PlayMaterialization {
  orders: OrdersDocument;
  workshop: WorkshopDocument;
  drifts: PlayDrift[];
}

export interface SignatureOpportunityView {
  binding: NonNullable<PlayProjection['signatureOpportunities']>[number];
  host: OrderNode;
  use: OrderNode;
  commitment: OrderNode | null;
  continuation: OrderNode | null;
  ordinal: number;
  count: number;
}

export function signatureOpportunityViews(
  projection: PlayProjection,
  conditions: OrdersDocument['conditions'] = {},
): SignatureOpportunityView[] {
  const bindings = projection.signatureOpportunities ?? [];
  const result: SignatureOpportunityView[] = [];
  for (const binding of bindings) {
    const host = findOrder(projection.orders, binding.hostOrderId);
    const use = host?.orders?.find((entry) => entry.id === binding.useOrderId);
    if (!host?.orders || !use?.use) continue;
    const hostBindings = bindings.filter((entry) =>
      entry.hostOrderId === binding.hostOrderId);
    const boundIds = new Set(hostBindings.map((entry) => entry.useOrderId));
    const ordered = host.orders.filter((entry) => entry.id && boundIds.has(entry.id));
    const index = host.orders.indexOf(use);
    const remaining = host.orders.slice(index + 1)
      .filter((entry) => !entry.id || !boundIds.has(entry.id));
    const commitment = remaining.find((entry) =>
      entry.move?.to === 'current-position' && typeof entry.when === 'string'
      && conditions[entry.when]?.fact === 'signature-commitment-active') ?? null;
    result.push({
      binding,
      host,
      use,
      commitment,
      continuation: remaining.find((entry) => entry !== commitment) ?? null,
      ordinal: ordered.indexOf(use),
      count: ordered.length,
    });
  }
  return result.sort((left, right) => {
    const leftHost = projection.orders.findIndex((entry) => entry.id === left.host.id);
    const rightHost = projection.orders.findIndex((entry) => entry.id === right.host.id);
    return leftHost - rightHost || left.ordinal - right.ordinal;
  });
}

export function parseWorkshop(workshopJson?: string | null): WorkshopDocument {
  if (!workshopJson) return emptyWorkshop();
  const value = JSON.parse(workshopJson) as WorkshopDocument;
  if (!value || value.schema !== 'arc-relay-workshop-v1' || !Array.isArray(value.plays))
    throw new Error('workshop.json must use arc-relay-workshop-v1.');
  return normalizeWorkshop(value);
}

export function inspectWorkshop(
  orders: OrdersDocument,
  workshop: WorkshopDocument,
  catalog?: Pick<TacticalSheetCatalog, 'classes'>,
): PlayDrift[] {
  const drifts: PlayDrift[] = [];
  for (const play of workshop.plays) {
    const bindingIds = new Set<string>();
    for (const formation of play.formations ?? []) {
      const projection = play.projections.find((entry) =>
        entry.id === formation.projectionId);
      const condition = orders.conditions[formation.conditionId];
      if (!projection || !condition
        || condition.fact !== 'role-in-zone-count'
        || condition.role !== projection.roleId
        || !condition.zone)
      {
        drifts.push({
          playId: play.id,
          projectionId: formation.projectionId,
          roleId: projection?.roleId ?? '',
          kind: 'formation-binding-invalid',
          message: `Formation '${formation.id}' must link its projection to one role-in-zone-count Condition.`,
        });
      }
    }
    for (const projection of play.projections) {
      const role = orders.roles.find((entry) => entry.id === projection.roleId);
      const ownedSkills = new Set(role?.classes.map((classId) =>
        catalog?.classes.find((entry) => entry.id === classId)?.signatureId)
        .filter((entry): entry is string => Boolean(entry)) ?? []);
      const bindings = projection.signatureOpportunities ?? [];
      for (const binding of bindings) {
        const path = `opportunity '${binding.id}'`;
        if (!binding.id || bindingIds.has(binding.id)) {
          drifts.push(opportunityDrift(
            play.id, projection, `${path} needs a unique stable id.`));
          continue;
        }
        bindingIds.add(binding.id);
        const host = findOrder(projection.orders, binding.hostOrderId);
        const child = host?.orders?.find((entry) =>
          entry.id === binding.useOrderId);
        const actionCount = child ? ['orders', 'move', 'fight', 'use', 'handoff', 'drop']
          .filter((key) => child[key as keyof OrderNode] !== undefined).length : 0;
        if (!host?.orders) {
          drifts.push(opportunityDrift(play.id, projection,
            `${path} names missing branch '${binding.hostOrderId}'.`));
        } else if (!child || actionCount !== 1 || !child.use) {
          drifts.push(opportunityDrift(play.id, projection,
            `${path} must name one direct use child '${binding.useOrderId}'.`));
        } else if (catalog && !ownedSkills.has(child.use.skill)) {
          drifts.push(opportunityDrift(play.id, projection,
            `${path} uses '${child.use.skill}', which role '${projection.roleId}' does not own.`));
        } else {
          const useIndex = host.orders.indexOf(child);
          const boundUseIds = new Set(bindings.map((entry) => entry.useOrderId));
          const hasContinuation = host.orders.slice(useIndex + 1)
            .some((entry) => !entry.id || !boundUseIds.has(entry.id));
          if (!hasContinuation) {
            drifts.push(opportunityDrift(play.id, projection,
              `${path} needs an ordinary continuation after its use child.`));
          }
        }
      }
      if (!role) {
        drifts.push({
          playId: play.id,
          projectionId: projection.id,
          roleId: projection.roleId,
          kind: 'role-missing',
          message: `Role '${projection.roleId}' no longer exists.`,
        });
        continue;
      }
      const generated = resolveProjectionOrders(play, projection);
      const previousIds = new Set(projection.lastGenerated.map((entry) => entry.orderId));
      if (projection.lastGenerated.length === 0) {
        for (const order of generated) {
          const orderId = requireOrderId(order);
          if (role.orders.some((entry) => entry.id === orderId)) {
            drifts.push({
              playId: play.id,
              projectionId: projection.id,
              roleId: projection.roleId,
              orderId,
              kind: 'order-collision',
              message: `Order '${orderId}' already exists outside this Play.`,
            });
          }
        }
      } else {
        for (const previous of projection.lastGenerated) {
          const materialized = role.orders.find((entry) => entry.id === previous.orderId);
          if (!materialized) {
            drifts.push({
              playId: play.id,
              projectionId: projection.id,
              roleId: projection.roleId,
              orderId: previous.orderId,
              kind: 'order-missing',
              message: `Linked Order '${previous.orderId}' is missing from '${projection.roleId}'.`,
            });
          } else if (fingerprintOrder(materialized) !== previous.fingerprint) {
            drifts.push({
              playId: play.id,
              projectionId: projection.id,
              roleId: projection.roleId,
              orderId: previous.orderId,
              kind: 'order-changed',
              message: `Linked Order '${previous.orderId}' changed outside its Play.`,
            });
          }
        }
        for (const order of generated) {
          const orderId = requireOrderId(order);
          if (!previousIds.has(orderId)
            && role.orders.some((entry) => entry.id === orderId)) {
            drifts.push({
              playId: play.id,
              projectionId: projection.id,
              roleId: projection.roleId,
              orderId,
              kind: 'order-collision',
              message: `New linked Order '${orderId}' collides with an ordinary Order.`,
            });
          }
        }
      }
      const before = projection.placement.beforeOrderKey;
      if (before && !role.orders.some((entry, index) => orderKey(entry, index) === before)
        && !generated.some((entry, index) => orderKey(entry, index) === before)) {
        drifts.push({
          playId: play.id,
          projectionId: projection.id,
          roleId: projection.roleId,
          orderId: before,
          kind: 'priority-neighbor-missing',
          message: `Priority neighbor '${before}' no longer exists in '${projection.roleId}'.`,
        });
      }
    }
  }
  return drifts;
}

function opportunityDrift(
  playId: string,
  projection: PlayProjection,
  message: string,
): PlayDrift {
  return {
    playId,
    projectionId: projection.id,
    roleId: projection.roleId,
    kind: 'opportunity-binding-invalid',
    message,
  };
}

export function materializeWorkshop(
  sourceOrders: OrdersDocument,
  sourceWorkshop: WorkshopDocument,
  force = false,
): PlayMaterialization {
  const orders = structuredClone(sourceOrders);
  const workshop = normalizeWorkshop(sourceWorkshop);
  const drifts = inspectWorkshop(orders, workshop);
  if (drifts.length > 0 && !force) return { orders, workshop, drifts };

  const ownedByRole = new Map<string, Set<string>>();
  for (const play of workshop.plays) for (const projection of play.projections) {
    const owned = ownedByRole.get(projection.roleId) ?? new Set<string>();
    projection.lastGenerated.forEach((entry) => owned.add(entry.orderId));
    resolveProjectionOrders(play, projection)
      .forEach((order) => owned.add(requireOrderId(order)));
    ownedByRole.set(projection.roleId, owned);
  }
  for (const role of orders.roles) {
    const owned = ownedByRole.get(role.id);
    if (owned) role.orders = role.orders.filter((order) => !order.id || !owned.has(order.id));
  }

  for (const play of workshop.plays) for (const projection of play.projections) {
    const role = orders.roles.find((entry) => entry.id === projection.roleId);
    if (!role) continue;
    const generated = resolveProjectionOrders(play, projection);
    const before = projection.placement.beforeOrderKey;
    const index = before
      ? role.orders.findIndex((entry, orderIndex) => orderKey(entry, orderIndex) === before)
      : role.orders.length;
    role.orders.splice(index < 0 ? role.orders.length : index, 0, ...generated);
    projection.lastGenerated = generated.map((order) => ({
      orderId: requireOrderId(order),
      fingerprint: fingerprintOrder(order),
    }));
  }

  return { orders, workshop, drifts: force ? [] : drifts };
}

export function resolveProjectionOrders(
  play: LinkedPlay,
  projection: PlayProjection,
): OrderNode[] {
  const resolved = structuredClone(projection.orders);
  for (const linked of play.linkedValues) {
    for (const target of linked.targets.filter((entry) => entry.projectionId === projection.id)) {
      const order = findOrder(resolved, target.orderId);
      if (!order) continue;
      setField(order as unknown as Record<string, unknown>, target.field, structuredClone(linked.value));
    }
  }
  return resolved;
}

export function replaceProjectionOrders(
  workshop: WorkshopDocument,
  playId: string,
  projectionId: string,
  resolvedOrders: OrderNode[],
): WorkshopDocument {
  const next = structuredClone(workshop);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const source = structuredClone(resolvedOrders);
  for (const linked of play.linkedValues) {
    const targets = linked.targets.filter((entry) => entry.projectionId === projectionId);
    for (const target of targets) {
      const order = findOrder(source, target.orderId);
      if (!order) continue;
      const value = readField(order as unknown as Record<string, unknown>, target.field);
      if (value !== undefined) linked.value = structuredClone(value) as PlayLinkedValue;
      deleteField(order as unknown as Record<string, unknown>, target.field);
    }
  }
  projection.orders = source;
  return next;
}

export function setLinkedValue(
  workshop: WorkshopDocument,
  playId: string,
  linkedValueId: string,
  value: PlayLinkedValue,
): WorkshopDocument {
  const next = structuredClone(workshop);
  const linked = requirePlay(next, playId).linkedValues
    .find((entry) => entry.id === linkedValueId);
  if (!linked) throw new Error(`Unknown linked value '${linkedValueId}'.`);
  linked.value = structuredClone(value);
  return next;
}

export function detachPlay(
  workshop: WorkshopDocument,
  playId: string,
): WorkshopDocument {
  const next = structuredClone(workshop);
  next.plays = next.plays.filter((play) => play.id !== playId);
  return next;
}

export function deletePlay(
  draft: SheetDraft,
  playId: string,
  cleanupOwned = true,
): { draft: SheetDraft; removedOrders: string[] } {
  const next = structuredClone(draft);
  const play = requirePlay(next.workshop, playId);
  const removedOrders: string[] = [];
  for (const projection of play.projections) {
    const role = next.orders.roles.find((entry) => entry.id === projection.roleId);
    if (!role) continue;
    const ids = new Set(projection.lastGenerated.map((entry) => entry.orderId));
    role.orders = role.orders.filter((order) => {
      const remove = Boolean(order.id && ids.has(order.id));
      if (remove) removedOrders.push(order.id!);
      return !remove;
    });
  }
  next.workshop.plays = next.workshop.plays.filter((entry) => entry.id !== playId);
  if (cleanupOwned) cleanupUnusedOwnedResources(next, play);
  return { draft: next, removedOrders };
}

export function playDrifts(
  orders: OrdersDocument,
  workshop: WorkshopDocument,
  playId: string,
): PlayDrift[] {
  return inspectWorkshop(orders, workshop).filter((entry) => entry.playId === playId);
}

export function projectionPriorityOptions(
  orders: OrdersDocument,
  projection: PlayProjection,
): Array<{ value: string | null; label: string }> {
  const role = orders.roles.find((entry) => entry.id === projection.roleId);
  const owned = new Set(projection.lastGenerated.map((entry) => entry.orderId));
  const ordinary = role?.orders.filter((entry) => !entry.id || !owned.has(entry.id)) ?? [];
  return [
    ...ordinary.map((order, index) => ({
      value: orderKey(order, index),
      label: `before ${humanize(order.id ?? order.shared ?? `order-${index + 1}`)}`,
    })),
    { value: null, label: 'after every existing Order' },
  ];
}

export function fingerprintOrder(order: OrderNode): string {
  const source = JSON.stringify(canonical(order));
  let hash = 0xcbf29ce484222325n;
  for (let index = 0; index < source.length; index++) {
    hash ^= BigInt(source.charCodeAt(index));
    hash = BigInt.asUintN(64, hash * 0x100000001b3n);
  }
  return `fnv1a64:${hash.toString(16).padStart(16, '0')}`;
}

export function uniquePlayId(base: string, workshop: WorkshopDocument): string {
  const used = new Set(workshop.plays.map((play) => play.id));
  if (!used.has(base)) return base;
  for (let index = 2; ; index++) if (!used.has(`${base}-${index}`)) return `${base}-${index}`;
}

export function linkedValueConsumerCount(linked: PlayLinkedValueDefinition): number {
  return linked.targets.length;
}

function cleanupUnusedOwnedResources(draft: SheetDraft, play: LinkedPlay) {
  const conditionConsumers = referencedConditions(draft.orders);
  for (const id of play.ownedConditionIds)
    if (!conditionConsumers.has(id)) delete draft.orders.conditions[id];
  const capacityConsumers = referencedCapacities(draft.orders);
  for (const id of play.ownedCapacityIds)
    if (!capacityConsumers.has(id)) delete draft.orders.capacities[id];
  const geometry = referencedGeometry(draft.orders);
  for (const owned of play.ownedGeometry) {
    if (geometry[owned.kind].has(owned.id)) continue;
    deleteGeometry(draft.mapPlan, owned.kind, owned.id);
  }
}

function referencedConditions(orders: OrdersDocument): Set<string> {
  const result = new Set<string>();
  walkOrders(orders, (order) => {
    for (const expression of [order.when, order.until]) {
      if (typeof expression === 'string' && expression !== 'always') result.add(expression);
      else if (expression && typeof expression !== 'string')
        expressionNames(expression).forEach((name) => result.add(name));
    }
  });
  return result;
}

function referencedCapacities(orders: OrdersDocument): Set<string> {
  const result = new Set<string>();
  walkOrders(orders, (order) => {
    if (typeof order.capacity === 'string') result.add(order.capacity);
  });
  return result;
}

function referencedGeometry(orders: OrdersDocument): Record<PlayGeometryKind, Set<string>> {
  const result = { zone: new Set<string>(), route: new Set<string>(), anchor: new Set<string>() };
  walkOrders(orders, (order) => {
    if (order.move?.patrol) result.route.add(order.move.patrol);
    if (order.move?.via) result.route.add(order.move.via);
    if (typeof order.use?.target === 'object' && 'role' in order.use.target
      && order.use.target.aheadVia) result.route.add(order.use.target.aheadVia);
    if (typeof order.use?.target === 'object' && 'route' in order.use.target)
      result.route.add(order.use.target.route);
    if (typeof order.move?.to === 'object') {
      if ('zone' in order.move.to) result.zone.add(order.move.to.zone);
      if ('anchor' in order.move.to) result.anchor.add(order.move.to.anchor);
      if ('adjacentTo' in order.move.to) result.anchor.add(order.move.to.adjacentTo);
    }
    if (order.move?.face) result.anchor.add(order.move.face.anchor);
    if (order.use?.at) result.anchor.add(order.use.at);
  });
  Object.values(orders.conditions).forEach((condition) => {
    if (condition.zone) result.zone.add(condition.zone);
  });
  return result;
}

function deleteGeometry(plan: MapPlanDocument, kind: PlayGeometryKind, id: string) {
  if (kind === 'zone') delete plan.zones[id];
  if (kind === 'route') delete plan.routes[id];
  if (kind === 'anchor') delete plan.anchors[id];
}

function requirePlay(workshop: WorkshopDocument, playId: string): LinkedPlay {
  const play = workshop.plays.find((entry) => entry.id === playId);
  if (!play) throw new Error(`Unknown Play '${playId}'.`);
  return play;
}

function requireProjection(play: LinkedPlay, projectionId: string): PlayProjection {
  const projection = play.projections.find((entry) => entry.id === projectionId);
  if (!projection) throw new Error(`Unknown projection '${projectionId}'.`);
  return projection;
}

function requireOrderId(order: OrderNode): string {
  if (!order.id) throw new Error('A linked root Order needs an authored id.');
  return order.id;
}

function orderKey(order: OrderNode, index: number): string {
  if (order.id) return `id:${order.id}`;
  if (order.shared) return `shared:${order.shared}`;
  return `index:${index}`;
}

function findOrder(orders: OrderNode[], orderId: string): OrderNode | undefined {
  for (const order of orders) {
    if (order.id === orderId) return order;
    const nested = order.orders && findOrder(order.orders, orderId);
    if (nested) return nested;
  }
  return undefined;
}

function walkOrders(orders: OrdersDocument, visit: (order: OrderNode) => void) {
  const walk = (order: OrderNode) => {
    visit(order);
    order.orders?.forEach(walk);
  };
  orders.roles.forEach((role) => role.orders.forEach(walk));
  Object.values(orders.sharedOrders).forEach(walk);
}

function expressionNames(expression: Exclude<ConditionExpression, string>): string[] {
  return 'all' in expression ? expression.all : expression.any;
}

function readField(source: Record<string, unknown>, field: string): unknown {
  let current: unknown = source;
  for (const segment of field.split('.')) {
    if (!current || typeof current !== 'object' || Array.isArray(current)) return undefined;
    current = (current as Record<string, unknown>)[segment];
  }
  return current;
}

function setField(source: Record<string, unknown>, field: string, value: unknown) {
  const segments = field.split('.');
  let current = source;
  for (const segment of segments.slice(0, -1)) {
    const child = current[segment];
    if (!child || typeof child !== 'object' || Array.isArray(child)) current[segment] = {};
    current = current[segment] as Record<string, unknown>;
  }
  current[segments.at(-1)!] = value;
}

function deleteField(source: Record<string, unknown>, field: string) {
  const segments = field.split('.');
  let current: Record<string, unknown> | undefined = source;
  for (const segment of segments.slice(0, -1)) {
    if (!current) return;
    const child: unknown = current[segment];
    current = child && typeof child === 'object' && !Array.isArray(child)
      ? child as Record<string, unknown>
      : undefined;
    if (!current) return;
  }
  delete current[segments.at(-1)!];
}

function canonical(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(canonical);
  if (!value || typeof value !== 'object') return value;
  return Object.fromEntries(Object.entries(value as Record<string, unknown>)
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([key, entry]) => [key, canonical(entry)]));
}

function humanize(value: string) {
  return value.replace(/[-_]+/g, ' ');
}
