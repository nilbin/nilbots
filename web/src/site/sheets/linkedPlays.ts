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
    | 'opportunity-binding-invalid' | 'formation-binding-invalid'
    | 'readiness-group-invalid' | 'program-condition-invalid';
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

export interface OpportunityView {
  binding: NonNullable<PlayProjection['signatureOpportunities']>[number];
  host: OrderNode;
  action: OrderNode;
  commitment: OrderNode | null;
  continuation: OrderNode | null;
  ordinal: number;
  count: number;
}

/**
 * One source of truth for every player-facing Opportunity. The sidecar keeps
 * its historical field name, but the ordered children may be any supported
 * physical action and remain ordinary Instructions.
 */
export function opportunityViews(
  projection: PlayProjection,
  conditions: OrdersDocument['conditions'] = {},
): OpportunityView[] {
  const bindings = projection.signatureOpportunities ?? [];
  const result: OpportunityView[] = [];
  for (const binding of bindings) {
    const host = findOrder(projection.orders, binding.hostOrderId);
    const action = host?.orders?.find((entry) => entry.id === binding.useOrderId);
    if (!host?.orders || !action) continue;
    const hostBindings = bindings.filter((entry) =>
      entry.hostOrderId === binding.hostOrderId);
    const boundIds = new Set(hostBindings.map((entry) => entry.useOrderId));
    const ordered = host.orders.filter((entry) => entry.id && boundIds.has(entry.id));
    const index = host.orders.indexOf(action);
    const remaining = host.orders.slice(index + 1)
      .filter((entry) => !entry.id || !boundIds.has(entry.id));
    const commitment = remaining.find((entry) =>
      entry.move?.to === 'current-position' && typeof entry.when === 'string'
      && conditions[entry.when]?.fact === 'signature-commitment-active') ?? null;
    result.push({
      binding,
      host,
      action,
      commitment,
      continuation: remaining.find((entry) => entry !== commitment) ?? null,
      ordinal: ordered.indexOf(action),
      count: ordered.length,
    });
  }
  return result.sort((left, right) => {
    const leftHost = projection.orders.findIndex((entry) => entry.id === left.host.id);
    const rightHost = projection.orders.findIndex((entry) => entry.id === right.host.id);
    return leftHost - rightHost || left.ordinal - right.ordinal;
  });
}

export function signatureOpportunityViews(
  projection: PlayProjection,
  conditions: OrdersDocument['conditions'] = {},
): SignatureOpportunityView[] {
  return opportunityViews(projection, conditions)
    .filter((view) => (view.binding.kind ?? 'use') === 'use' && Boolean(view.action.use))
    .map((view) => ({ ...view, use: view.action }));
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
    if (play.responseTriggerConditionId) {
      const triggerId = play.responseTriggerConditionId;
      if (!orders.conditions[triggerId] || !play.ownedConditionIds.includes(triggerId)) {
        drifts.push({
          playId: play.id,
          projectionId: '',
          roleId: '',
          kind: 'program-condition-invalid',
          message: `Program trigger '${triggerId}' must remain a visible Program-owned Condition.`,
        });
      }
      for (const projection of play.projections) {
        const response = findOrder(projection.orders, `${projection.id}-respond`);
        if (!response) continue;
        if (projection.responseActivation === 'immediate') continue;
        const names = response.when === undefined ? []
          : typeof response.when === 'string' ? [response.when]
            : expressionNames(response.when);
        if (!names.includes(triggerId)) {
          drifts.push({
            playId: play.id,
            projectionId: projection.id,
            roleId: projection.roleId,
            kind: 'program-condition-invalid',
            message: `Program trigger '${triggerId}' is not compiled into '${projection.roleId}'.`,
          });
        }
      }
    }
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
    const groupedProjections = new Set<string>();
    for (const group of play.readinessGroups ?? []) {
      const projectionIds = [...new Set(group.projectionIds)];
      const formationByProjection = new Map<string, string[]>();
      for (const formation of play.formations ?? []) {
        const ids = formationByProjection.get(formation.projectionId) ?? [];
        ids.push(formation.conditionId);
        formationByProjection.set(formation.projectionId, ids);
      }
      const conditionIds = projectionIds.flatMap((id) =>
        [
          ...(formationByProjection.get(id) ?? []),
          ...play.slots.filter((slot) => slot.deployment?.requiredForReadiness
            && slot.deployment.presenceConditionId
            && play.projections.find((projection) => projection.id === id)?.roleId === slot.roleId)
            .map((slot) => slot.deployment!.presenceConditionId!),
        ]);
      const invalid = projectionIds.length < 2
        || projectionIds.some((id) => (formationByProjection.get(id)?.length ?? 0) === 0)
        || projectionIds.some((id) => groupedProjections.has(id));
      if (invalid) {
        drifts.push({
          playId: play.id,
          projectionId: projectionIds[0] ?? '',
          roleId: '',
          kind: 'readiness-group-invalid',
          message: `Combined readiness '${group.name}' needs at least two unique, area-formed participants and may not overlap another group.`,
        });
        continue;
      }
      projectionIds.forEach((id) => groupedProjections.add(id));
      for (const projectionId of projectionIds) {
        const projection = play.projections.find((entry) => entry.id === projectionId);
        const response = projection && findOrder(
          projection.orders, `${projection.id}-respond`);
        const names = response?.when === undefined ? []
          : typeof response.when === 'string' ? [response.when]
            : expressionNames(response.when);
        if (!response || conditionIds.some((id) => !names.includes(id))) {
          drifts.push({
            playId: play.id,
            projectionId,
            roleId: projection?.roleId ?? '',
            kind: 'readiness-group-invalid',
            message: `Combined readiness '${group.name}' is not compiled into every participant's Instruction gate.`,
          });
        }
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
        const kind = binding.kind ?? 'use';
        const matchesKind = child && (
          kind === 'use' && Boolean(child.use)
          || kind === 'fight' && Boolean(child.fight)
          || (kind === 'core-recovery' || kind === 'health-recovery') && Boolean(child.move)
          || kind === 'handoff' && Boolean(child.handoff)
          || kind === 'drop' && Boolean(child.drop));
        if (!host?.orders) {
          drifts.push(opportunityDrift(play.id, projection,
            `${path} names missing branch '${binding.hostOrderId}'.`));
        } else if (!child || actionCount !== 1 || !matchesKind) {
          drifts.push(opportunityDrift(play.id, projection,
            `${path} must name one direct ${kind} child '${binding.useOrderId}'.`));
        } else if (kind === 'use' && catalog && child.use
          && !ownedSkills.has(child.use.skill)) {
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
              message: `Instruction '${orderId}' already exists outside this Program.`,
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
              message: `Generated Instruction '${previous.orderId}' is missing from '${projection.roleId}'.`,
            });
          } else if (fingerprintOrder(materialized) !== previous.fingerprint) {
            drifts.push({
              playId: play.id,
              projectionId: projection.id,
              roleId: projection.roleId,
              orderId: previous.orderId,
              kind: 'order-changed',
              message: `Generated Instruction '${previous.orderId}' changed outside its Program.`,
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
              message: `New generated Instruction '${orderId}' collides with an unlinked Instruction.`,
            });
          }
        }
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

  // Programs are authoring sugar over the one runtime Instruction list. For
  // each role, filter the global Program list to participating Programs and
  // flatten their participant blocks in exactly that order. Advanced unlinked
  // Instructions remain after the Program band as a compatibility fallback.
  for (const role of orders.roles) {
    const generatedBand: OrderNode[] = [];
    for (const play of workshop.plays) {
      for (const projection of play.projections.filter((entry) =>
        entry.roleId === role.id)) {
        const generated = resolveProjectionOrders(play, projection);
        generatedBand.push(...generated);
        projection.lastGenerated = generated.map((order) => ({
          orderId: requireOrderId(order),
          fingerprint: fingerprintOrder(order),
        }));
      }
    }
    role.orders.unshift(...generatedBand);
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
    if (order.fight?.zone) result.zone.add(order.fight.zone);
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
  if (!play) throw new Error(`Unknown Program '${playId}'.`);
  return play;
}

function requireProjection(play: LinkedPlay, projectionId: string): PlayProjection {
  const projection = play.projections.find((entry) => entry.id === projectionId);
  if (!projection) throw new Error(`Unknown participant Instruction block '${projectionId}'.`);
  return projection;
}

function requireOrderId(order: OrderNode): string {
  if (!order.id) throw new Error('A generated root Instruction needs an authored id.');
  return order.id;
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
