import {
  materializeWorkshop,
  replaceProjectionOrders,
  type PlayDrift,
} from './linkedPlays';
import { synchronizePlayGeometry } from './playGeometry';
import {
  identifierFromName,
  type ConditionDefinition,
  type ConditionExpression,
  type OrderNode,
  type SheetDraft,
  type UseOrder,
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
  preserveCommitment = false,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const useOrderId = uniqueOrderId(`${hostOrderId}-${use.skill}`, projection.orders);
  insertOpportunity(play, projection, hostOrderId, {
    id: useOrderId,
    use: structuredClone(use),
  }, 'use');
  if (preserveCommitment)
    ensureCommitmentHold(next, play, projection,
      findOrderLocation(projection.orders, hostOrderId)!.node);
  return requireMaterialized(next);
}

export interface FightOpportunitySetup {
  withinWalkingSteps: number;
  targets: NonNullable<OrderNode['fight']>['targets'];
  style: NonNullable<OrderNode['fight']>['style'];
  chaseLimit: number;
  zone?: string;
  capacity?: number | null;
}

export function addFightOpportunity(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  hostOrderId: string,
  setup: FightOpportunitySetup,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const conditionId = uniqueIdentifier(`${hostOrderId}-contact`,
    new Set(Object.keys(next.orders.conditions)));
  next.orders.conditions[conditionId] = {
    fact: 'self-nearest-visible-enemy-distance', operator: 'at-most',
    value: Math.max(0, Math.min(31, Math.trunc(setup.withinWalkingSteps))),
    ...(setup.zone ? { zone: setup.zone } : {}),
  };
  play.ownedConditionIds.push(conditionId);
  const orderId = uniqueOrderId(`${hostOrderId}-fight`, projection.orders);
  const binding = insertOpportunity(play, projection, hostOrderId, {
    id: orderId,
    when: conditionId,
    ...(setup.capacity === null || setup.capacity === undefined
      ? {} : { capacity: Math.max(1, Math.min(8, Math.trunc(setup.capacity))) }),
    fight: {
      targets: [...setup.targets], style: setup.style,
      chaseLimit: Math.max(0, Math.min(31, Math.trunc(setup.chaseLimit))),
      ...(setup.zone ? { zone: setup.zone } : {}),
    },
  }, 'fight');
  binding.ownedConditionIds = [conditionId];
  binding.capacityPolicy = {
    target: 'enemy', limit: setup.capacity ?? null, scope: 'local',
  };
  return requireMaterialized(next);
}

export interface CoreRecoveryOpportunitySetup {
  withinWalkingSteps: number | null;
  preference: 'closest' | 'highest-value';
  coreBankValue?: { operator: 'equals' | 'at-least' | 'at-most'; value: number };
  pursuersPerCore: number | null;
  coordinateAcrossSheet: boolean;
  linkedBankProgramId?: string;
}

export function addCoreRecoveryOpportunity(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  hostOrderId: string,
  setup: CoreRecoveryOpportunitySetup,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const ids = new Set(Object.keys(next.orders.conditions));
  const emptyId = uniqueIdentifier(`${hostOrderId}-empty-handed`, ids);
  ids.add(emptyId);
  const nearbyId = uniqueIdentifier(`${hostOrderId}-core-nearby`, ids);
  next.orders.conditions[emptyId] = {
    fact: 'self-carrying', operator: 'equals', value: 0,
  };
  next.orders.conditions[nearbyId] = {
    fact: 'self-nearest-visible-loose-core-distance', operator: 'at-most',
    value: setup.withinWalkingSteps ?? 31,
    ...(setup.coreBankValue
      ? { coreBankValue: structuredClone(setup.coreBankValue) } : {}),
  };
  play.ownedConditionIds.push(emptyId, nearbyId);
  const orderId = uniqueOrderId(`${hostOrderId}-recover-core`, projection.orders);
  const binding = insertOpportunity(play, projection, hostOrderId, {
    id: orderId,
    when: { all: [emptyId, nearbyId] },
    ...(setup.pursuersPerCore === null ? {} : { capacity: setup.pursuersPerCore }),
    move: {
      to: setup.preference === 'highest-value'
        ? 'highest-bank-value-visible-loose-core'
        : 'nearest-visible-loose-core',
      ...(setup.withinWalkingSteps === null
        ? {} : { withinWalkingSteps: setup.withinWalkingSteps }),
      ...(setup.coreBankValue
        ? { coreBankValue: structuredClone(setup.coreBankValue) } : {}),
    },
  }, 'core-recovery');
  binding.ownedConditionIds = [emptyId, nearbyId];
  binding.linkedBankProgramId = setup.linkedBankProgramId;
  binding.capacityPolicy = {
    target: 'core', limit: setup.pursuersPerCore,
    scope: setup.coordinateAcrossSheet ? 'sheet' : 'local',
  };
  normalizeOpportunityCapacities(next);
  return requireMaterialized(next);
}

export interface HealthRecoveryOpportunitySetup {
  startBelow: number;
  recoverUntil: number;
  withinWalkingSteps: number | null;
  timeout?: number;
  cooldown?: number;
  patientsPerTile: number | null;
  coordinateAcrossSheet: boolean;
  allowCoreCarriers: boolean;
  zone?: string;
}

export function addHealthRecoveryOpportunity(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  hostOrderId: string,
  setup: HealthRecoveryOpportunitySetup,
): SheetDraft {
  const start = Math.max(0, Math.min(99, Math.trunc(setup.startBelow)));
  const recovered = Math.max(start + 1, Math.min(100, Math.trunc(setup.recoverUntil)));
  if ((setup.timeout ?? 0) > 0 && (setup.cooldown ?? 0) === 0)
    throw new Error('A timed-out health recovery needs a positive retry delay.');
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const ids = new Set(Object.keys(next.orders.conditions));
  const lowId = uniqueIdentifier(`${hostOrderId}-health-low`, ids); ids.add(lowId);
  const healedId = uniqueIdentifier(`${hostOrderId}-health-recovered`, ids); ids.add(healedId);
  const nearId = uniqueIdentifier(`${hostOrderId}-healing-nearby`, ids); ids.add(nearId);
  next.orders.conditions[lowId] = {
    fact: 'self-health-percent', operator: 'at-most', value: start,
  };
  next.orders.conditions[healedId] = {
    fact: 'self-health-percent', operator: 'at-least', value: recovered,
  };
  next.orders.conditions[nearId] = {
    fact: 'self-nearest-heal-zone-distance', operator: 'at-most',
    value: setup.withinWalkingSteps ?? 31,
  };
  const whenIds = [lowId, nearId];
  const untilIds = [healedId];
  if (!setup.allowCoreCarriers) {
    const emptyId = uniqueIdentifier(`${hostOrderId}-not-carrying`, ids); ids.add(emptyId);
    const carryingId = uniqueIdentifier(`${hostOrderId}-became-carrier`, ids);
    next.orders.conditions[emptyId] = {
      fact: 'self-carrying', operator: 'equals', value: 0,
    };
    next.orders.conditions[carryingId] = {
      fact: 'self-carrying', operator: 'equals', value: 1,
    };
    whenIds.push(emptyId);
    untilIds.push(carryingId);
  }
  if (setup.zone) {
    const zoneId = uniqueIdentifier(`${hostOrderId}-healing-area`, ids);
    next.orders.conditions[zoneId] = {
      fact: 'self-in-zone', operator: 'equals', value: 1, zone: setup.zone,
    };
    whenIds.push(zoneId);
  }
  for (const id of [...whenIds, ...untilIds])
    if (!play.ownedConditionIds.includes(id)) play.ownedConditionIds.push(id);
  const orderId = uniqueOrderId(`${hostOrderId}-recover-health`, projection.orders);
  const binding = insertOpportunity(play, projection, hostOrderId, {
    id: orderId,
    when: expressionOf(whenIds),
    until: untilIds.length === 1 ? untilIds[0] : { any: untilIds },
    ...(setup.timeout ? { timeout: clampTicks(setup.timeout) } : {}),
    ...(setup.cooldown ? { cooldown: clampTicks(setup.cooldown) } : {}),
    ...(setup.patientsPerTile === null ? {} : { capacity: setup.patientsPerTile }),
    move: {
      to: 'nearest-heal-zone',
      ...(setup.withinWalkingSteps === null
        ? {} : { withinWalkingSteps: setup.withinWalkingSteps }),
    },
  }, 'health-recovery');
  binding.ownedConditionIds = [...whenIds, ...untilIds];
  binding.capacityPolicy = {
    target: 'healing', limit: setup.patientsPerTile,
    scope: setup.coordinateAcrossSheet ? 'sheet' : 'local',
  };
  normalizeOpportunityCapacities(next);
  return requireMaterialized(next);
}

export function updateFightOpportunity(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  bindingId: string,
  setup: FightOpportunitySetup,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const binding = requireBinding(projection, bindingId);
  if ((binding.kind ?? 'use') !== 'fight') throw new Error('Choose a Fight opportunity.');
  const node = requireOpportunityNodeAny(projection, binding);
  const conditionId = binding.ownedConditionIds?.find((id) =>
    next.orders.conditions[id]?.fact === 'self-nearest-visible-enemy-distance');
  if (!conditionId) throw new Error('Fight contact Condition is missing.');
  next.orders.conditions[conditionId] = {
    fact: 'self-nearest-visible-enemy-distance', operator: 'at-most',
    value: Math.max(0, Math.min(31, Math.trunc(setup.withinWalkingSteps))),
    ...(setup.zone ? { zone: setup.zone } : {}),
  };
  node.fight = {
    targets: [...setup.targets], style: setup.style,
    chaseLimit: Math.max(0, Math.min(31, Math.trunc(setup.chaseLimit))),
    ...(setup.zone ? { zone: setup.zone } : {}),
  };
  if (setup.capacity === null || setup.capacity === undefined) delete node.capacity;
  else node.capacity = Math.max(1, Math.min(8, Math.trunc(setup.capacity)));
  binding.capacityPolicy = {
    target: 'enemy', limit: setup.capacity ?? null, scope: 'local',
  };
  return requireMaterialized(next);
}

export function updateCoreRecoveryOpportunity(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  bindingId: string,
  setup: CoreRecoveryOpportunitySetup,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const binding = requireBinding(projection, bindingId);
  if (binding.kind !== 'core-recovery') throw new Error('Choose a Core recovery opportunity.');
  const node = requireOpportunityNodeAny(projection, binding);
  const distanceId = binding.ownedConditionIds?.find((id) =>
    next.orders.conditions[id]?.fact === 'self-nearest-visible-loose-core-distance');
  if (!distanceId) throw new Error('Core walking-distance Condition is missing.');
  next.orders.conditions[distanceId] = {
    fact: 'self-nearest-visible-loose-core-distance', operator: 'at-most',
    value: setup.withinWalkingSteps ?? 31,
    ...(setup.coreBankValue
      ? { coreBankValue: structuredClone(setup.coreBankValue) } : {}),
  };
  node.move = {
    to: setup.preference === 'highest-value'
      ? 'highest-bank-value-visible-loose-core' : 'nearest-visible-loose-core',
    ...(setup.withinWalkingSteps === null
      ? {} : { withinWalkingSteps: setup.withinWalkingSteps }),
    ...(setup.coreBankValue
      ? { coreBankValue: structuredClone(setup.coreBankValue) } : {}),
  };
  binding.linkedBankProgramId = setup.linkedBankProgramId;
  binding.capacityPolicy = {
    target: 'core', limit: setup.pursuersPerCore,
    scope: setup.coordinateAcrossSheet ? 'sheet' : 'local',
  };
  normalizeOpportunityCapacities(next);
  return requireMaterialized(next);
}

export function updateHealthRecoveryOpportunity(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  bindingId: string,
  setup: HealthRecoveryOpportunitySetup,
): SheetDraft {
  const start = Math.max(0, Math.min(99, Math.trunc(setup.startBelow)));
  const recovered = Math.max(start + 1, Math.min(100, Math.trunc(setup.recoverUntil)));
  if ((setup.timeout ?? 0) > 0 && (setup.cooldown ?? 0) === 0)
    throw new Error('A timed-out health recovery needs a positive retry delay.');
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const binding = requireBinding(projection, bindingId);
  if (binding.kind !== 'health-recovery') throw new Error('Choose a health recovery opportunity.');
  const node = requireOpportunityNodeAny(projection, binding);
  const condition = (fact: string) => binding.ownedConditionIds?.find((id) =>
    next.orders.conditions[id]?.fact === fact);
  const lowId = condition('self-health-percent');
  const nearId = condition('self-nearest-heal-zone-distance');
  const healedId = binding.ownedConditionIds?.find((id) => {
    const candidate = next.orders.conditions[id];
    return candidate?.fact === 'self-health-percent' && candidate.operator === 'at-least';
  });
  if (!lowId || !nearId || !healedId) throw new Error('Health recovery Conditions are missing.');
  next.orders.conditions[lowId].value = start;
  next.orders.conditions[nearId].value = setup.withinWalkingSteps ?? 31;
  next.orders.conditions[healedId].value = recovered;
  node.move = {
    to: 'nearest-heal-zone',
    ...(setup.withinWalkingSteps === null
      ? {} : { withinWalkingSteps: setup.withinWalkingSteps }),
  };
  if (setup.timeout) node.timeout = clampTicks(setup.timeout); else delete node.timeout;
  if (setup.cooldown) node.cooldown = clampTicks(setup.cooldown); else delete node.cooldown;
  binding.capacityPolicy = {
    target: 'healing', limit: setup.patientsPerTile,
    scope: setup.coordinateAcrossSheet ? 'sheet' : 'local',
  };
  normalizeOpportunityCapacities(next);
  return requireMaterialized(next);
}

export function setSignatureOpportunityCommitmentHold(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  bindingId: string,
  enabled: boolean,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const binding = requireBinding(projection, bindingId);
  const host = findOrderLocation(projection.orders, binding.hostOrderId)?.node;
  if (!host?.orders) throw new Error(`Opportunity host '${binding.hostOrderId}' is missing.`);
  if (enabled) ensureCommitmentHold(next, play, projection, host);
  else host.orders = host.orders.filter((child) => !isCommitmentHold(next, child));
  return cleanupUnusedCommitmentConditions(requireMaterialized(next), play);
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
  const node = requireOpportunityNodeAny(projection, binding);
  const zoneId = uniqueIdentifier(`${binding.useOrderId}-window`, new Set([
    ...Object.keys(next.mapPlan.zones),
    ...Object.keys(next.mapPlan.routes),
    ...Object.keys(next.mapPlan.anchors),
  ]), 'zone');
  const conditionId = uniqueIdentifier(`${zoneId}-active`, new Set(
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

/**
 * Create one Play-owned exact signature target and bind it to the selected
 * ordinary `use` Order in the same edit. The author places the point visually;
 * the temporary centre seed exists only so the object is immediately visible
 * and draggable on the map.
 */
export function addSignatureOpportunityAnchor(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  bindingId: string,
): { draft: SheetDraft; anchorId: string } {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const binding = requireBinding(projection, bindingId);
  const node = requireOpportunityNode(projection, binding);
  const anchorId = uniqueString(identifierFromName(
    `${binding.useOrderId}-target`, 'signature-target', 44), new Set([
    ...Object.keys(next.mapPlan.zones),
    ...Object.keys(next.mapPlan.routes),
    ...Object.keys(next.mapPlan.anchors),
  ]));
  next.mapPlan.anchors[anchorId] = [15, 13];
  play.ownedGeometry.push({
    kind: 'anchor',
    id: anchorId,
    purpose: `use.at for ${node.use!.skill}`,
  });
  node.use!.at = anchorId;
  return { draft: requireMaterialized(next), anchorId };
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
  return cleanupUnusedCommitmentConditions(requireMaterialized(next), play);
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
    throw new Error(`Instruction '${nextOrderId}' already exists for this participant.`);
  const node = requireOpportunityNodeAny(projection, binding);
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
  for (const conditionId of binding.ownedConditionIds ?? []) {
    delete next.orders.conditions[conditionId];
    play.ownedConditionIds = play.ownedConditionIds.filter((id) => id !== conditionId);
  }
  if (!(projection.signatureOpportunities ?? []).some((entry) =>
      entry.hostOrderId === binding.hostOrderId))
    host.orders = host.orders.filter((child) => !isCommitmentHold(next, child));
  for (const linked of play.linkedValues)
    linked.targets = linked.targets.filter((target) =>
      target.projectionId !== projectionId || target.orderId !== binding.useOrderId);
  play.linkedValues = play.linkedValues.filter((linked) => linked.targets.length > 0);
  normalizeOpportunityCapacities(next);
  return cleanupUnusedCommitmentConditions(requireMaterialized(next), play);
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

/**
 * Move one Program in the sole author-facing priority list. Rematerialization
 * moves every participating role's generated Instruction block together.
 */
export function moveProgram(
  draft: SheetDraft,
  playId: string,
  direction: -1 | 1,
): SheetDraft {
  const next = structuredClone(draft);
  const index = next.workshop.plays.findIndex((entry) => entry.id === playId);
  const target = index + direction;
  if (index < 0) throw new Error(`Unknown Program '${playId}'.`);
  if (target < 0 || target >= next.workshop.plays.length) return next;
  [next.workshop.plays[index], next.workshop.plays[target]] =
    [next.workshop.plays[target], next.workshop.plays[index]];
  return requireMaterialized(next);
}

/**
 * Add one ordinary map-plan object and mark it as Play-owned. The object has
 * no behavior until a projected Order or Condition references it.
 */
export function addPlayGeometry(
  draft: SheetDraft,
  playId: string,
  kind: PlayGeometryKind,
): { draft: SheetDraft; id: string } {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const id = uniqueIdentifier(`${play.id}-${kind}`, new Set([
    ...Object.keys(next.mapPlan.zones),
    ...Object.keys(next.mapPlan.routes),
    ...Object.keys(next.mapPlan.anchors),
  ]), kind);
  if (kind === 'zone') next.mapPlan.zones[id] = { rect: [15, 13, 15, 13] };
  else if (kind === 'route') next.mapPlan.routes[id] = { waypoints: [] };
  else next.mapPlan.anchors[id] = [15, 13];
  play.ownedGeometry.push({ kind, id, purpose: 'available to this Program' });
  return { draft: next, id };
}

export interface PlayPostSetup {
  name: string;
  point: [number, number];
  tolerance?: number;
  required?: boolean;
  facing?: 'north' | 'east' | 'south' | 'west';
}

/**
 * Add one canonical map-first Post. Every field projects into ordinary map
 * geometry, Conditions, capacity and movement; the runtime never sees Post.
 */
export function addPlayPost(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  setup: PlayPostSetup,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const role = next.orders.roles.find((entry) => entry.id === projection.roleId);
  if (!role) throw new Error(`Unknown role '${projection.roleId}'.`);
  const base = identifierFromName(setup.name, `${projection.roleId}-post`, 36);
  const geometryIds = new Set([
    ...Object.keys(next.mapPlan.anchors), ...Object.keys(next.mapPlan.zones),
    ...Object.keys(next.mapPlan.routes),
  ]);
  const anchorId = uniqueIdentifier(`${play.id}-${base}`, geometryIds, 'post'); geometryIds.add(anchorId);
  const readinessZoneId = uniqueIdentifier(`${anchorId}-ready`, geometryIds, 'post-ready'); geometryIds.add(readinessZoneId);
  const facingAnchorId = uniqueIdentifier(`${anchorId}-facing`, geometryIds, 'post-facing');
  const tolerance = Math.max(0, Math.min(3, Math.trunc(setup.tolerance ?? 0)));
  const [x, y] = setup.point;
  const facing = setup.facing ?? 'east';
  next.mapPlan.anchors[anchorId] = [x, y];
  next.mapPlan.anchors[facingAnchorId] = cardinalFacingPoint([x, y], facing);
  next.mapPlan.zones[readinessZoneId] = {
    rect: [x - tolerance, y - tolerance, x + tolerance, y + tolerance],
  };
  const hostOrderId = uniqueOrderId(`${anchorId}-duty`, projection.orders);
  const moveOrderId = uniqueOrderId(`${anchorId}-stand`, projection.orders);
  projection.orders.push({
    id: hostOrderId,
    orders: [{
      id: moveOrderId,
      capacity: 1,
      move: {
        to: { anchor: anchorId }, posture: 'line',
        face: { anchor: facingAnchorId },
      },
    }],
  });
  // Blank projections start with a placeholder centre-Well move. Once a Post
  // owns the participant, retaining that placeholder would be a hidden second
  // plan below it.
  projection.orders = projection.orders.filter((order) =>
    order.id === hostOrderId || order.id !== `${projection.id}-move`);
  const slotId = uniqueIdentifier(`${anchorId}-slot`, new Set(play.slots.map((slot) => slot.id)), 'post-slot');
  const slot = {
    id: slotId,
    name: setup.name.trim() || 'Post',
    roleId: projection.roleId,
    anchorId,
    readinessZoneId,
    required: Boolean(setup.required),
    tolerance,
    capacity: 1,
    facingAnchorId,
    facing,
    hostOrderId,
    moveOrderId,
  } satisfies NonNullable<LinkedPlay['slots']>[number];
  play.slots.push(slot);
  play.ownedGeometry.push(
    { kind: 'anchor', id: anchorId, purpose: 'Post stand position' },
    { kind: 'zone', id: readinessZoneId, purpose: 'Post readiness', derivedFrom: anchorId, tolerance },
    { kind: 'anchor', id: facingAnchorId, purpose: 'Post cardinal facing', derivedFrom: anchorId },
  );
  play.geometryGroups.push({
    id: `${slotId}-geometry`, kind: 'selection',
    objectIds: [anchorId, facingAnchorId], derivedObjectIds: [readinessZoneId],
  });
  if (slot.required) ensurePostReadiness(next, play, projection, slot);
  return requireMaterialized(synchronizePlayGeometry(next, play.id));
}

export function setPlayPostFacing(
  draft: SheetDraft,
  playId: string,
  slotId: string,
  facing: 'north' | 'east' | 'south' | 'west',
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const slot = play.slots.find((entry) => entry.id === slotId);
  if (!slot?.facingAnchorId) throw new Error(`Post '${slotId}' has no facing arrow.`);
  const point = next.mapPlan.anchors[slot.anchorId];
  if (!point) throw new Error(`Post '${slotId}' has no stand position.`);
  slot.facing = facing;
  next.mapPlan.anchors[slot.facingAnchorId] = cardinalFacingPoint(point, facing);
  return requireMaterialized(next);
}

export function setPlayPostRequired(
  draft: SheetDraft,
  playId: string,
  slotId: string,
  required: boolean,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const slot = play.slots.find((entry) => entry.id === slotId);
  if (!slot) throw new Error(`Unknown Post '${slotId}'.`);
  const projection = play.projections.find((entry) => entry.roleId === slot.roleId);
  if (!projection) throw new Error(`Post '${slotId}' has no participant.`);
  slot.required = required;
  if (required) ensurePostReadiness(next, play, projection, slot);
  else removePostReadiness(next, play, slot);
  synchronizePlayResponseConditions(play);
  return requireMaterialized(next);
}

export interface PlayPostDeploymentSetup {
  skill: string;
  target: [number, number];
  requiredForReadiness: boolean;
  rebuildWhenMissing: boolean;
  refreshDurabilityAtMost?: number;
  refreshLifetimeAtMost?: number;
}

export function configurePlayPostDeployment(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  slotId: string,
  setup: PlayPostDeploymentSetup,
): SheetDraft {
  if (!setup.rebuildWhenMissing && setup.refreshDurabilityAtMost === undefined
    && setup.refreshLifetimeAtMost === undefined)
    throw new Error('Choose rebuild or at least one visible refresh policy.');
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const slot = play.slots.find((entry) => entry.id === slotId
    && entry.roleId === projection.roleId);
  if (!slot?.hostOrderId) throw new Error(`Unknown Post '${slotId}'.`);
  if (slot.deployment)
    removePostDeploymentSources(next, play, projection, slot);
  const ids = new Set([
    ...Object.keys(next.mapPlan.anchors), ...Object.keys(next.mapPlan.zones),
    ...Object.keys(next.mapPlan.routes),
  ]);
  const targetAnchorId = uniqueIdentifier(`${slot.anchorId}-effect`, ids, 'effect'); ids.add(targetAnchorId);
  const targetZoneId = uniqueIdentifier(`${targetAnchorId}-footprint`, ids, 'effect-zone');
  next.mapPlan.anchors[targetAnchorId] = [...setup.target];
  next.mapPlan.zones[targetZoneId] = { rect: [...setup.target, ...setup.target] };
  play.ownedGeometry.push(
    { kind: 'anchor', id: targetAnchorId, purpose: 'Post signature effect target' },
    { kind: 'zone', id: targetZoneId, purpose: 'Post deployment presence', derivedFrom: targetAnchorId, tolerance: 0 },
  );
  play.geometryGroups.find((entry) => entry.id === `${slot.id}-geometry`)
    ?.objectIds.push(targetAnchorId);

  const conditionIds = new Set(Object.keys(next.orders.conditions));
  const sourceConditionId = uniqueString(identifierFromName(
    `${slot.anchorId}-occupied-by-self`, 'post-occupied', 44), conditionIds);
  conditionIds.add(sourceConditionId);
  const missingConditionId = uniqueString(identifierFromName(
    `${targetAnchorId}-missing`, 'deployment-missing', 44), conditionIds);
  conditionIds.add(missingConditionId);
  const needsPresent = setup.refreshDurabilityAtMost !== undefined
    || setup.refreshLifetimeAtMost !== undefined;
  const presentConditionId = needsPresent
    ? uniqueString(identifierFromName(
      `${targetAnchorId}-present`, 'deployment-present', 44), conditionIds) : undefined;
  if (presentConditionId) conditionIds.add(presentConditionId);
  const presenceConditionId = setup.requiredForReadiness
    ? uniqueString(identifierFromName(
      `${targetAnchorId}-role-deployment-ready`, 'deployment-ready', 44), conditionIds)
    : undefined;
  next.orders.conditions[sourceConditionId] = {
    fact: 'self-in-zone', operator: 'equals', value: 1, zone: slot.readinessZoneId,
  };
  next.orders.conditions[missingConditionId] = {
    fact: 'self-deployment-in-zone', operator: 'equals', value: 0, zone: targetZoneId,
  };
  if (presentConditionId) next.orders.conditions[presentConditionId] = {
    fact: 'self-deployment-in-zone', operator: 'equals', value: 1, zone: targetZoneId,
  };
  if (presenceConditionId) next.orders.conditions[presenceConditionId] = {
    fact: 'role-deployments-in-zone-count', operator: 'at-least', value: 1,
    role: projection.roleId, zone: targetZoneId, signature: setup.skill,
  };
  const deploymentConditionIds = [
    sourceConditionId, missingConditionId,
    ...(presentConditionId ? [presentConditionId] : []),
    ...(presenceConditionId ? [presenceConditionId] : []),
  ];
  play.ownedConditionIds.push(...deploymentConditionIds);

  const useOrderIds: string[] = [];
  const addMaintenanceLeaf = (
    suffix: string,
    extraConditions: string[],
  ) => {
    const orderId = uniqueOrderId(`${slot.anchorId}-${suffix}`, projection.orders);
    const binding = insertOpportunity(play, projection, slot.hostOrderId!, {
      id: orderId,
      when: { all: [sourceConditionId, ...extraConditions] },
      capacity: 1,
      use: { skill: setup.skill, target: 'self', at: targetAnchorId },
    }, 'use');
    binding.ownedConditionIds = [];
    binding.capacityPolicy = {
      target: 'signature-target', limit: 1, scope: 'local',
    };
    useOrderIds.push(orderId);
  };
  if (setup.rebuildWhenMissing) addMaintenanceLeaf('rebuild', [missingConditionId]);
  if (setup.refreshDurabilityAtMost !== undefined) {
    const id = uniqueString(identifierFromName(
      `${targetAnchorId}-damaged`, 'deployment-damaged', 44), conditionIds); conditionIds.add(id);
    next.orders.conditions[id] = {
      fact: 'deployment-durability-percent', operator: 'at-most',
      value: Math.max(0, Math.min(100, Math.trunc(setup.refreshDurabilityAtMost))),
    };
    play.ownedConditionIds.push(id);
    deploymentConditionIds.push(id);
    addMaintenanceLeaf('refresh-durability', [presentConditionId!, id]);
  }
  if (setup.refreshLifetimeAtMost !== undefined) {
    const id = uniqueString(identifierFromName(
      `${targetAnchorId}-expiring`, 'deployment-expiring', 44), conditionIds);
    next.orders.conditions[id] = {
      fact: 'deployment-lifetime-percent', operator: 'at-most',
      value: Math.max(0, Math.min(100, Math.trunc(setup.refreshLifetimeAtMost))),
    };
    play.ownedConditionIds.push(id);
    deploymentConditionIds.push(id);
    addMaintenanceLeaf('refresh-lifetime', [presentConditionId!, id]);
  }
  slot.deployment = {
    skill: setup.skill,
    targetAnchorId,
    targetZoneId,
    rebuildWhenMissing: setup.rebuildWhenMissing,
    ...(setup.refreshDurabilityAtMost === undefined ? {} : {
      refreshDurabilityAtMost: setup.refreshDurabilityAtMost,
    }),
    ...(setup.refreshLifetimeAtMost === undefined ? {} : {
      refreshLifetimeAtMost: setup.refreshLifetimeAtMost,
    }),
    requiredForReadiness: setup.requiredForReadiness,
    presenceConditionId,
    sourceConditionId,
    ownedConditionIds: deploymentConditionIds,
    useOrderIds,
  };
  synchronizePlayResponseConditions(play);
  return requireMaterialized(next);
}

export function addPlayCondition(
  draft: SheetDraft,
  playId: string,
  baseId: string,
  condition: ConditionDefinition,
): { draft: SheetDraft; id: string } {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const id = uniqueIdentifier(baseId, new Set(Object.keys(next.orders.conditions)));
  next.orders.conditions[id] = structuredClone(condition);
  play.ownedConditionIds.push(id);
  return { draft: next, id };
}

/**
 * Author the Program's shared tactical start directly. The editor reuses one
 * visible Condition and propagates it to every configured participant; the
 * player never wires participant expressions by hand.
 */
export function configurePlaySharedTrigger(
  draft: SheetDraft,
  playId: string,
  condition: ConditionDefinition,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const readinessIds = new Set((play.formations ?? []).map((entry) =>
    entry.conditionId));
  const responseSets = play.projections.map((projection) => {
    const response = findOrderLocation(
      projection.orders, `${projection.id}-respond`)?.node;
    return response ? expressionIds(response.when).filter((id) =>
      !readinessIds.has(id)) : [];
  }).filter((ids) => ids.length > 0);
  const inferred = responseSets.length === 0 ? undefined
    : responseSets[0].find((id) => responseSets.every((ids) => ids.includes(id)));
  const conditionId = play.responseTriggerConditionId ?? inferred ?? uniqueIdentifier(
    `${play.id}-response-start`, new Set(Object.keys(next.orders.conditions)));
  next.orders.conditions[conditionId] = structuredClone(condition);
  if (!play.ownedConditionIds.includes(conditionId))
    play.ownedConditionIds.push(conditionId);
  play.responseTriggerConditionId = conditionId;
  synchronizePlayResponseConditions(play);
  return requireMaterialized(next);
}

export interface PlayContingentSetup {
  triggerConditionId: string;
  /** Whether this participant waits for the shared Program trigger. */
  activation?: 'program-trigger' | 'immediate';
  destination: { zone: string } | { route: string };
  requiredBodies: number;
  timeout: number;
}

/**
 * Scaffold or refine one generic contingent as ordinary Orders. A zone-backed
 * contingent stages until its role-qualified readiness Condition is true; a
 * route-backed contingent acts immediately when the shared trigger is true.
 * Re-running this edit preserves the existing response branch, including any
 * signature opportunity children and explicit exit Condition.
 */
export function configurePlayContingent(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  setup: PlayContingentSetup,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  if (play.recipe !== 'blank')
    throw new Error('Generic contingent setup belongs to a blank Program.');
  const projection = requireProjection(play, projectionId);
  projection.responseActivation = setup.activation ?? 'program-trigger';
  const role = next.orders.roles.find((entry) => entry.id === projection.roleId);
  if (!role) throw new Error(`Unknown role '${projection.roleId}'.`);
  if (!next.orders.conditions[setup.triggerConditionId])
    throw new Error(`Unknown shared trigger '${setup.triggerConditionId}'.`);
  const previousTriggerId = play.responseTriggerConditionId;
  play.responseTriggerConditionId = setup.triggerConditionId;
  const required = Math.max(0, Math.min(role.classes.length,
    Math.trunc(setup.requiredBodies)));
  const responseId = `${projection.id}-respond`;
  const stageId = `${projection.id}-stage`;
  let response = findOrderLocation(projection.orders, responseId)?.node;
  const previousFormation = (play.formations ?? []).find((entry) =>
    entry.projectionId === projection.id);
  const previousConditionId = previousFormation?.conditionId;
  const previousReadiness = previousConditionId
    ? next.orders.conditions[previousConditionId] : undefined;
  const previousNotReadyId = previousReadiness
    ? play.ownedConditionIds.find((id) => {
      const condition = next.orders.conditions[id];
      return condition?.fact === 'role-in-zone-count'
        && condition.operator === 'at-most'
        && condition.value === Math.max(0, Number(previousReadiness.value) - 1)
        && condition.role === previousReadiness.role
        && condition.zone === previousReadiness.zone;
    })
    : undefined;

  let readinessId: string | null = null;
  let notReadyId: string | null = null;
  if ('zone' in setup.destination && required > 0) {
    if (!next.mapPlan.zones[setup.destination.zone])
      throw new Error(`Unknown formation zone '${setup.destination.zone}'.`);
    readinessId = previousConditionId ?? uniqueIdentifier(
      `${projection.id}-ready`, new Set(Object.keys(next.orders.conditions)));
    next.orders.conditions[readinessId] = {
      fact: 'role-in-zone-count', operator: 'at-least', value: required,
      role: projection.roleId, zone: setup.destination.zone,
    };
    if (!play.ownedConditionIds.includes(readinessId))
      play.ownedConditionIds.push(readinessId);
    notReadyId = previousNotReadyId ?? uniqueIdentifier(`${readinessId}-not`, new Set(
      Object.keys(next.orders.conditions)));
    next.orders.conditions[notReadyId] = {
      fact: 'role-in-zone-count', operator: 'at-most', value: required - 1,
      role: projection.roleId, zone: setup.destination.zone,
    };
    if (!play.ownedConditionIds.includes(notReadyId))
      play.ownedConditionIds.push(notReadyId);
    play.formations = (play.formations ?? []).filter((entry) =>
      entry.projectionId !== projection.id);
    play.formations.push({
      id: `${projection.id}-formation`,
      projectionId: projection.id,
      conditionId: readinessId,
    });
  } else {
    play.formations = (play.formations ?? []).filter((entry) =>
      entry.projectionId !== projection.id);
    if (previousConditionId) {
      delete next.orders.conditions[previousConditionId];
      if (previousNotReadyId) delete next.orders.conditions[previousNotReadyId];
      play.ownedConditionIds = play.ownedConditionIds.filter((id) =>
        id !== previousConditionId && id !== previousNotReadyId);
    }
    removeProjectionFromReadinessGroups(play, projection.id);
  }

  const when = expressionOf([
    ...(projection.responseActivation === 'immediate'
      ? [] : [setup.triggerConditionId]),
    ...(readinessId ? [readinessId] : []),
  ]);
  if (!response) {
    response = {
      id: responseId,
      when,
      timeout: clampTicks(setup.timeout),
      cooldown: 20,
      orders: ['route' in setup.destination
        ? {
          id: `${responseId}-continue`,
          move: { patrol: setup.destination.route, posture: 'front' },
        }
        : {
          id: `${responseId}-continue`,
          fight: { targets: ['carrier', 'weakest', 'nearest'], style: 'hold', chaseLimit: 7 },
        }],
    };
    projection.orders = [response, ...projection.orders.filter((order) =>
      !order.id?.endsWith('-move'))];
  } else {
    response.when = when;
    response.timeout = clampTicks(setup.timeout);
    const continuation = response.orders?.find((child) =>
      !(projection.signatureOpportunities ?? []).some((binding) =>
        binding.hostOrderId === responseId && binding.useOrderId === child.id)
      && child.move?.to !== 'current-position');
    if (continuation && 'route' in setup.destination) {
      delete continuation.fight;
      continuation.move = { patrol: setup.destination.route, posture: 'front' };
    } else if (continuation) {
      delete continuation.move;
      continuation.fight = {
        targets: ['carrier', 'weakest', 'nearest'],
        style: 'hold',
        chaseLimit: 7,
      };
    }
  }

  projection.orders = projection.orders.filter((order) => order.id !== stageId);
  if ('zone' in setup.destination && readinessId) {
    projection.orders.splice(1, 0, {
      id: stageId,
      ...(notReadyId ? { when: notReadyId } : {}),
      until: readinessId,
      move: { to: { zone: setup.destination.zone }, posture: 'line' },
    });
  }
  synchronizePlayResponseConditions(play,
    previousTriggerId && previousTriggerId !== setup.triggerConditionId
      ? new Set([previousTriggerId]) : undefined);
  return requireMaterialized(next);
}

/**
 * Link several role-qualified readiness checks into one flat ALL gate. This is
 * editor metadata only: every participant still receives an ordinary `when`
 * expression over the generated Conditions.
 */
export function setPlayReadinessGroup(
  draft: SheetDraft,
  playId: string,
  groupId: string | null,
  name: string,
  projectionIds: string[],
  maximumSpread?: number,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const selected = [...new Set(projectionIds)].filter((id) =>
    play.projections.some((projection) => projection.id === id));
  if (selected.length < 2)
    throw new Error('Combined readiness needs at least two participants.');
  const formationByProjection = new Map<string, NonNullable<LinkedPlay['formations']>>();
  for (const formation of play.formations ?? []) {
    const entries = formationByProjection.get(formation.projectionId) ?? [];
    entries.push(formation);
    formationByProjection.set(formation.projectionId, entries);
  }
  for (const projectionId of selected) {
    const formations = formationByProjection.get(projectionId) ?? [];
    if (formations.length === 0 || formations.some((formation) =>
      !next.orders.conditions[formation.conditionId]))
      throw new Error('Every combined participant must first form in an area.');
  }
  const conditionCount = selected.reduce((count, projectionId) =>
    count + (formationByProjection.get(projectionId)?.length ?? 0), 0)
    + (play.responseTriggerConditionId ? 1 : 0);
  if (conditionCount > 8)
    throw new Error('This combined gate exceeds the current eight-Condition expression limit.');

  const groups = play.readinessGroups ??= [];
  const retained = groups.filter((entry) => entry.id !== groupId);
  for (const group of retained)
    group.projectionIds = group.projectionIds.filter((id) => !selected.includes(id));
  play.readinessGroups = retained.filter((entry) => entry.projectionIds.length >= 2);
  const id = groupId ?? uniqueString(
    identifierFromName(name, 'readiness', 48),
    new Set(play.readinessGroups.map((entry) => entry.id)));
  play.readinessGroups.push({
    id,
    name: name.trim() || 'Combined readiness',
    projectionIds: selected,
    ...(maximumSpread === undefined ? {} : {
      maximumSpread: Math.max(0, Math.min(31, Math.trunc(maximumSpread))),
    }),
  });
  synchronizePlayResponseConditions(play);
  return requireMaterialized(next);
}

export function removePlayReadinessGroup(
  draft: SheetDraft,
  playId: string,
  groupId: string,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  play.readinessGroups = (play.readinessGroups ?? []).filter((entry) =>
    entry.id !== groupId);
  synchronizePlayResponseConditions(play);
  return requireMaterialized(next);
}

export function setPlayContingentExit(
  draft: SheetDraft,
  playId: string,
  projectionId: string,
  conditionId: string | null,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const projection = requireProjection(play, projectionId);
  const response = findOrderLocation(
    projection.orders, `${projection.id}-respond`)?.node;
  if (!response) throw new Error('Configure this contingent before choosing its exit.');
  if (conditionId && !next.orders.conditions[conditionId])
    throw new Error(`Unknown exit Condition '${conditionId}'.`);
  if (conditionId) response.until = conditionId;
  else delete response.until;
  return requireMaterialized(next);
}

export interface PlayLifecycleSetup {
  minimumCommitmentTicks: number;
  successConditionIds: string[];
  maximumTicks: number;
  cooldownTicks: number;
  waiting: 'hold-posts' | 'prepare-conditionally' | 'fall-through';
  preparationConditionId?: string;
}

/**
 * Project the human commitment controls into the existing node lifecycle.
 * No Program latch is introduced: an authored `until` is the runtime latch.
 */
export function configurePlayLifecycle(
  draft: SheetDraft,
  playId: string,
  setup: PlayLifecycleSetup,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const minimum = Math.max(0, Math.min(1200, Math.trunc(setup.minimumCommitmentTicks)));
  const maximum = Math.max(1, Math.min(1200, Math.trunc(setup.maximumTicks)));
  if (maximum < minimum)
    throw new Error('Give up after cannot be shorter than the minimum commitment.');
  for (const id of setup.successConditionIds)
    if (!next.orders.conditions[id]) throw new Error(`Unknown success Condition '${id}'.`);
  if (setup.waiting === 'prepare-conditionally'
    && (!setup.preparationConditionId
      || !next.orders.conditions[setup.preparationConditionId]))
    throw new Error('Conditional preparation needs one visible Condition.');

  const previousMinimum = play.ownedConditionIds.find((id) =>
    next.orders.conditions[id]?.fact === 'self-order-ticks'
      && id.startsWith(`${play.id}-minimum-commitment`));
  let minimumId: string | undefined;
  if (minimum > 0) {
    minimumId = previousMinimum ?? uniqueIdentifier(`${play.id}-minimum-commitment`,
      new Set(Object.keys(next.orders.conditions)));
    next.orders.conditions[minimumId] = {
      fact: 'self-order-ticks', operator: 'at-least', value: minimum,
    };
    if (!play.ownedConditionIds.includes(minimumId)) play.ownedConditionIds.push(minimumId);
  } else if (previousMinimum) {
    delete next.orders.conditions[previousMinimum];
    play.ownedConditionIds = play.ownedConditionIds.filter((id) => id !== previousMinimum);
  }
  const untilIds = [...(minimumId ? [minimumId] : []), ...setup.successConditionIds];
  if (untilIds.length > 8)
    throw new Error('Commitment and success exceed the eight-Condition flat expression limit.');
  for (const projection of play.projections) {
    const response = findOrderLocation(projection.orders, `${projection.id}-respond`)?.node;
    if (!response) continue;
    if (untilIds.length === 0) delete response.until;
    else response.until = expressionOf(untilIds);
    response.timeout = maximum;
    response.cooldown = Math.max(0, Math.min(1200, Math.trunc(setup.cooldownTicks)));
    for (const slot of play.slots.filter((entry) => entry.roleId === projection.roleId)) {
      if (!slot.hostOrderId) continue;
      const host = findOrderLocation(projection.orders, slot.hostOrderId)?.node;
      if (!host) continue;
      if (setup.waiting === 'hold-posts') delete host.when;
      else if (setup.waiting === 'prepare-conditionally')
        host.when = setup.preparationConditionId;
      else if (play.responseTriggerConditionId)
        host.when = play.responseTriggerConditionId;
    }
  }
  play.lifecycle = {
    minimumCommitmentTicks: minimum,
    maximumTicks: maximum,
    cooldownTicks: Math.max(0, Math.min(1200, Math.trunc(setup.cooldownTicks))),
    waiting: setup.waiting,
    ...(setup.preparationConditionId
      ? { preparationConditionId: setup.preparationConditionId } : {}),
    successConditionIds: [...setup.successConditionIds],
  };
  return requireMaterialized(next);
}

export function setPlayZoneRect(
  draft: SheetDraft,
  playId: string,
  zoneId: string,
  rect: [number, number, number, number],
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  if (!play.ownedGeometry.some((entry) =>
      entry.kind === 'zone' && entry.id === zoneId && !entry.derivedFrom))
    throw new Error(`Zone '${zoneId}' is not directly owned by this Program.`);
  next.mapPlan.zones[zoneId] = { rect: [...rect] };
  return next;
}

export function appendPlayRouteWaypoint(
  draft: SheetDraft,
  playId: string,
  routeId: string,
  point: [number, number],
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  if (!play.ownedGeometry.some((entry) =>
      entry.kind === 'route' && entry.id === routeId && !entry.derivedFrom))
    throw new Error(`Route '${routeId}' is not directly owned by this Program.`);
  const route = next.mapPlan.routes[routeId];
  if (!route) throw new Error(`Route '${routeId}' is missing.`);
  const previous = route.waypoints.at(-1);
  if (!previous || previous[0] !== point[0] || previous[1] !== point[1])
    route.waypoints.push([...point]);
  return next;
}

export function removeLastPlayRouteWaypoint(
  draft: SheetDraft,
  playId: string,
  routeId: string,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  if (!play.ownedGeometry.some((entry) =>
      entry.kind === 'route' && entry.id === routeId && !entry.derivedFrom))
    throw new Error(`Route '${routeId}' is not directly owned by this Program.`);
  next.mapPlan.routes[routeId]?.waypoints.pop();
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
  if (!projection) throw new Error(`Unknown participant Instruction block '${projectionId}'.`);
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
    throw new Error(`Condition '${conditionId}' is not owned by this Program.`);
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
    throw new Error('Pooled reserve posts are available only in an Ambush Program.');
  const reserveProjection = play.projections.find((entry) => entry.id.endsWith('-reserve'));
  if (!reserveProjection)
    throw new Error('The Ambush Program has no reserve participant.');
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
    throw new Error('Pooled reserve posts are available only in an Ambush Program.');
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
    throw new Error(`Anchor '${anchorId}' is not owned by this Program.`);
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
    throw new Error(`${kind} '${previous}' is not owned by this Program.`);
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

function synchronizePlayResponseConditions(
  play: LinkedPlay,
  removedBaseIds: ReadonlySet<string> = new Set(),
) {
  const formations = play.formations ?? [];
  const readinessIds = new Set(formations.map((entry) => entry.conditionId));
  const formationByProjection = new Map<string, string[]>();
  for (const formation of formations) {
    const ids = formationByProjection.get(formation.projectionId) ?? [];
    ids.push(formation.conditionId);
    formationByProjection.set(formation.projectionId, ids);
  }
  const deploymentByProjection = new Map<string, string[]>();
  for (const projection of play.projections) {
    const ids = play.slots.filter((slot) => slot.roleId === projection.roleId
      && slot.deployment?.requiredForReadiness
      && slot.deployment.presenceConditionId)
      .map((slot) => slot.deployment!.presenceConditionId!);
    deploymentByProjection.set(projection.id, ids);
  }
  const groupByProjection = new Map<string, NonNullable<LinkedPlay['readinessGroups']>[number]>();
  for (const group of play.readinessGroups ?? [])
    for (const projectionId of group.projectionIds)
      groupByProjection.set(projectionId, group);

  for (const projection of play.projections) {
    const response = findOrderLocation(
      projection.orders, `${projection.id}-respond`)?.node;
    if (!response) continue;
    const base = expressionIds(response.when).filter((id) =>
      !readinessIds.has(id) && !removedBaseIds.has(id)
      && id !== play.responseTriggerConditionId);
    const group = groupByProjection.get(projection.id);
    const gatedProjectionIds = group?.projectionIds ?? [projection.id];
    const readiness = gatedProjectionIds.flatMap((projectionId) => [
      ...(formationByProjection.get(projectionId) ?? []),
      ...(deploymentByProjection.get(projectionId) ?? []),
    ]);
    const ids = [...new Set([
      ...(play.responseTriggerConditionId
          && projection.responseActivation !== 'immediate'
        ? [play.responseTriggerConditionId] : []),
      ...base,
      ...readiness,
    ])];
    if (ids.length > 8)
      throw new Error('This participant gate exceeds the current eight-Condition expression limit.');
    response.when = expressionOf(ids);
  }
}

function ensurePostReadiness(
  draft: SheetDraft,
  play: LinkedPlay,
  projection: PlayProjection,
  slot: LinkedPlay['slots'][number],
) {
  if (!findOrderLocation(projection.orders, `${projection.id}-respond`))
    throw new Error('Add the Program response before requiring a Post for readiness.');
  const conditionId = slot.readinessConditionId ?? uniqueIdentifier(
    `${slot.anchorId}-ready`, new Set(Object.keys(draft.orders.conditions)));
  draft.orders.conditions[conditionId] = {
    fact: 'role-in-zone-count', operator: 'at-least', value: 1,
    role: slot.roleId, zone: slot.readinessZoneId,
  };
  slot.readinessConditionId = conditionId;
  if (!play.ownedConditionIds.includes(conditionId)) play.ownedConditionIds.push(conditionId);
  if (!(play.formations ??= []).some((entry) => entry.id === `${slot.id}-formation`))
    play.formations.push({
      id: `${slot.id}-formation`, projectionId: projection.id, conditionId,
    });
  synchronizePlayResponseConditions(play);
}

function removePostReadiness(
  draft: SheetDraft,
  play: LinkedPlay,
  slot: LinkedPlay['slots'][number],
) {
  if (!slot.readinessConditionId) return;
  const conditionId = slot.readinessConditionId;
  play.formations = (play.formations ?? []).filter((entry) =>
    entry.conditionId !== conditionId);
  play.ownedConditionIds = play.ownedConditionIds.filter((id) => id !== conditionId);
  delete draft.orders.conditions[conditionId];
  delete slot.readinessConditionId;
}

function removePostDeploymentSources(
  draft: SheetDraft,
  play: LinkedPlay,
  projection: PlayProjection,
  slot: LinkedPlay['slots'][number],
) {
  const deployment = slot.deployment;
  if (!deployment) return;
  const bindings = projection.signatureOpportunities ?? [];
  const removing = new Set(bindings.filter((binding) =>
    deployment.useOrderIds.includes(binding.useOrderId)).map((binding) => binding.id));
  const host = slot.hostOrderId
    ? findOrderLocation(projection.orders, slot.hostOrderId)?.node : undefined;
  if (host?.orders)
    host.orders = host.orders.filter((entry) => !deployment.useOrderIds.includes(entry.id ?? ''));
  projection.signatureOpportunities = bindings.filter((binding) => !removing.has(binding.id));
  const conditionIds = new Set([
    ...deployment.ownedConditionIds,
    ...bindings.filter((binding) => removing.has(binding.id))
      .flatMap((binding) => binding.ownedConditionIds ?? []),
  ]);
  for (const id of conditionIds) {
    delete draft.orders.conditions[id];
    play.ownedConditionIds = play.ownedConditionIds.filter((entry) => entry !== id);
  }
  delete draft.mapPlan.anchors[deployment.targetAnchorId];
  delete draft.mapPlan.zones[deployment.targetZoneId];
  play.ownedGeometry = play.ownedGeometry.filter((entry) =>
    entry.id !== deployment.targetAnchorId && entry.id !== deployment.targetZoneId);
  delete slot.deployment;
}

function cardinalFacingPoint(
  [x, y]: [number, number],
  facing: 'north' | 'east' | 'south' | 'west',
): [number, number] {
  if (facing === 'north') return [x, y - 1];
  if (facing === 'south') return [x, y + 1];
  if (facing === 'west') return [x - 1, y];
  return [x + 1, y];
}

function removeProjectionFromReadinessGroups(
  play: LinkedPlay,
  projectionId: string,
) {
  for (const group of play.readinessGroups ?? [])
    group.projectionIds = group.projectionIds.filter((id) => id !== projectionId);
  play.readinessGroups = (play.readinessGroups ?? []).filter((entry) =>
    entry.projectionIds.length >= 2);
}

function expressionIds(expression: ConditionExpression | undefined): string[] {
  if (!expression || expression === 'always') return [];
  if (typeof expression === 'string') return [expression];
  if ('any' in expression)
    throw new Error('An Advanced ANY gate cannot be combined with Program-derived readiness without nested Conditions.');
  return [...expression.all];
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
    if (typeof order.use?.target === 'object'
      && 'role' in order.use.target && order.use.target.aheadVia === previous)
      order.use.target.aheadVia = nextId;
    if (typeof order.use?.target === 'object'
      && 'route' in order.use.target && order.use.target.route === previous)
      order.use.target.route = nextId;
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

function insertOpportunity(
  play: LinkedPlay,
  projection: PlayProjection,
  hostOrderId: string,
  action: OrderNode,
  kind: NonNullable<NonNullable<PlayProjection['signatureOpportunities']>[number]['kind']>,
) {
  const located = findOrderLocation(projection.orders, hostOrderId);
  if (!located) throw new Error(`Unknown host Instruction '${hostOrderId}'.`);
  let host = located.node;
  if (!host.orders) {
    if (!host.move && !host.fight)
      throw new Error('Choose a durable Move, Fight, or existing branch as the continuation.');
    if (located.depth >= 2)
      throw new Error('This Instruction is already at the maximum branch depth. Choose its nearest parent branch.');
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
  const bindings = projection.signatureOpportunities ??= [];
  const boundIds = new Set(bindings.map((entry) => entry.useOrderId));
  const firstContinuation = host.orders!.findIndex((child) =>
    !child.id || !boundIds.has(child.id));
  host.orders!.splice(firstContinuation < 0 ? host.orders!.length : firstContinuation,
    0, action);
  const bindingBase = `${hostOrderId}-${action.id}-${kind}`;
  const bindingIds = new Set(play.projections.flatMap((entry) =>
    (entry.signatureOpportunities ?? []).map((binding) => binding.id)));
  const binding: NonNullable<PlayProjection['signatureOpportunities']>[number] = {
    id: uniqueString(bindingBase, bindingIds),
    kind,
    hostOrderId,
    useOrderId: action.id!,
    ownedConditionIds: [],
  };
  bindings.push(binding);
  return binding;
}

/**
 * A visible sheet-wide coordination group materializes as a named capacity
 * only while several cards consume it; one consumer stays inline. This is a
 * storage normalization, never a second allocation policy.
 */
function normalizeOpportunityCapacities(draft: SheetDraft) {
  for (const target of ['core', 'healing'] as const) {
    const members = draft.workshop.plays.flatMap((play) =>
      play.projections.flatMap((projection) =>
        (projection.signatureOpportunities ?? [])
          .filter((binding) => binding.capacityPolicy?.target === target
            && binding.capacityPolicy.scope === 'sheet')
          .map((binding) => ({ play, projection, binding }))));
    const capacityId = target === 'core' ? 'core-recovery' : 'healing';
    if (members.length === 0) {
      delete draft.orders.capacities[capacityId];
      draft.workshop.plays.forEach((play) => {
        play.ownedCapacityIds = play.ownedCapacityIds.filter((id) => id !== capacityId);
      });
      continue;
    }
    const limits = new Set(members.map((entry) => entry.binding.capacityPolicy!.limit));
    if (limits.size > 1)
      throw new Error(`Every card in the sheet-wide ${target} group must use the same capacity.`);
    const limit = members[0].binding.capacityPolicy!.limit;
    for (const { projection, binding } of members) {
      const node = requireOpportunityNodeAny(projection, binding);
      if (limit === null) delete node.capacity;
      else node.capacity = members.length === 1 ? limit : capacityId;
      binding.capacityPolicy!.groupId = members.length === 1 ? undefined : capacityId;
    }
    if (members.length === 1 || limit === null) delete draft.orders.capacities[capacityId];
    else draft.orders.capacities[capacityId] = limit;
    draft.workshop.plays.forEach((play) => {
      play.ownedCapacityIds = play.ownedCapacityIds.filter((id) => id !== capacityId);
    });
    if (members.length > 1 && limit !== null)
      members[0].play.ownedCapacityIds.push(capacityId);
  }
}

function ensureCommitmentHold(
  draft: SheetDraft,
  play: LinkedPlay,
  projection: PlayProjection,
  host: OrderNode,
) {
  if (!host.orders || host.orders.some((child) => isCommitmentHold(draft, child)))
    return;
  const existing = Object.entries(draft.orders.conditions).find(([, condition]) =>
    condition.fact === 'signature-commitment-active'
      && condition.operator === 'equals' && condition.value === 1);
  const conditionId = existing?.[0] ?? uniqueString(
    'signature-commitment-active', new Set(Object.keys(draft.orders.conditions)));
  if (!existing) {
    draft.orders.conditions[conditionId] = {
      fact: 'signature-commitment-active', operator: 'equals', value: 1,
    };
    play.ownedConditionIds.push(conditionId);
  }
  const bindingIds = new Set((projection.signatureOpportunities ?? [])
    .filter((entry) => entry.hostOrderId === host.id)
    .map((entry) => entry.useOrderId));
  const continuation = host.orders.findIndex((child) =>
    !child.id || !bindingIds.has(child.id));
  host.orders.splice(continuation < 0 ? host.orders.length : continuation, 0, {
    id: uniqueOrderId(`${host.id}-maintain-signature`, projection.orders),
    when: conditionId,
    move: { to: 'current-position' },
  });
}

function isCommitmentHold(draft: SheetDraft, node: OrderNode) {
  if (node.move?.to !== 'current-position' || typeof node.when !== 'string')
    return false;
  const condition = draft.orders.conditions[node.when];
  return condition?.fact === 'signature-commitment-active'
    && condition.operator === 'equals' && condition.value === 1;
}

function cleanupUnusedCommitmentConditions(
  draft: SheetDraft,
  play: LinkedPlay,
): SheetDraft {
  const used = new Set<string>();
  const inspect = (order: OrderNode) => {
    for (const expression of [order.when, order.until]) {
      if (typeof expression === 'string' && expression !== 'always')
        used.add(expression);
      else if (expression && typeof expression !== 'string')
        ('all' in expression ? expression.all : expression.any)
          .forEach((id) => used.add(id));
    }
  };
  draft.orders.roles.forEach((role) => role.orders.forEach((order) =>
    walkOrder(order, inspect)));
  Object.values(draft.orders.sharedOrders).forEach((order) =>
    walkOrder(order, inspect));
  for (const id of [...play.ownedConditionIds]) {
    const condition = draft.orders.conditions[id];
    if (condition?.fact !== 'signature-commitment-active' || used.has(id))
      continue;
    delete draft.orders.conditions[id];
    play.ownedConditionIds = play.ownedConditionIds.filter((entry) => entry !== id);
  }
  return draft;
}

function requireProjection(play: LinkedPlay, projectionId: string): PlayProjection {
  const projection = play.projections.find((entry) => entry.id === projectionId);
  if (!projection) throw new Error(`Unknown participant Instruction block '${projectionId}'.`);
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
  const node = requireOpportunityNodeAny(projection, binding);
  if (!node.use)
    throw new Error(`Signature opportunity '${binding.id}' has no direct use child.`);
  return node;
}

function requireOpportunityNodeAny(
  projection: PlayProjection,
  binding: NonNullable<PlayProjection['signatureOpportunities']>[number],
): OrderNode {
  const host = findOrderLocation(projection.orders, binding.hostOrderId)?.node;
  const node = host?.orders?.find((entry) => entry.id === binding.useOrderId);
  if (!node)
    throw new Error(`Opportunity '${binding.id}' has no direct action child.`);
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
  const normalized = identifierFromName(base, 'order', 48);
  if (!used.has(normalized)) return normalized;
  for (let suffix = 2; ; suffix++) {
    const tail = `-${suffix}`;
    const candidate = `${identifierFromName(base, 'order', 48 - tail.length)}${tail}`;
    if (!used.has(candidate)) return candidate;
  }
}

function uniqueString(base: string, used: ReadonlySet<string>): string {
  if (!used.has(base)) return base;
  for (let suffix = 2; ; suffix++)
    if (!used.has(`${base}-${suffix}`)) return `${base}-${suffix}`;
}

function uniqueIdentifier(
  base: string,
  used: ReadonlySet<string>,
  fallback = 'condition',
): string {
  const normalized = identifierFromName(base, fallback, 48);
  if (!used.has(normalized)) return normalized;
  for (let suffix = 2; ; suffix++) {
    const tail = `-${suffix}`;
    const candidate = `${identifierFromName(base, fallback, 48 - tail.length)}${tail}`;
    if (!used.has(candidate)) return candidate;
  }
}

function expressionOf(ids: string[]): ConditionExpression {
  if (ids.length === 0) return 'always';
  return ids.length === 1 ? ids[0] : { all: ids };
}

function clampTicks(value: number): number {
  return Math.max(1, Math.min(1200, Math.trunc(value)));
}

function requirePlay(draft: SheetDraft, playId: string): LinkedPlay {
  const play = draft.workshop.plays.find((entry) => entry.id === playId);
  if (!play) throw new Error(`Unknown Program '${playId}'.`);
  return play;
}
