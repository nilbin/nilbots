import type { OrderNode } from './sheetAuthoring.ts';

export type PlayRecipe =
  | 'blank'
  | 'ambush'
  | 'relay-chain'
  | 'veteran-escort'
  | 'safe-return'
  | 'lane-closure';

export type PlayGeometryKind = 'zone' | 'route' | 'anchor';

export interface PlayOwnedGeometry {
  kind: PlayGeometryKind;
  id: string;
  purpose: string;
  derivedFrom?: string;
  tolerance?: number;
}

export interface PlayGeometryGroup {
  id: string;
  kind: 'selection' | 'relay-corridor';
  objectIds: string[];
  derivedObjectIds: string[];
  waypoints?: Array<[number, number]>;
  stationWaypointIndexes?: Record<string, number>;
  routeSlices?: Array<{
    routeId: string;
    fromWaypointIndex: number;
    toWaypointIndex: number;
  }>;
  tossLinks?: Array<{
    routeId: string;
    sourceWaypointIndex: number;
    receiverAnchorId: string;
    /** Static authored route progress; never sent to the executor. */
    forwardRouteTiles?: number;
  }>;
}

export interface PlaySlot {
  id: string;
  /** Human label for the map-first Post. Older sidecars derive this from id. */
  name?: string;
  roleId: string;
  anchorId: string;
  readinessZoneId: string;
  readinessConditionId?: string;
  required: boolean;
  tolerance: number;
  capacity?: number | string;
  /** Ordinary move.face anchor. A cardinal Post owns a derived aligned point. */
  facingAnchorId?: string;
  facing?: 'north' | 'east' | 'south' | 'west' | 'toward-point';
  /** Stable id of the ordinary branch that holds this Post. */
  hostOrderId?: string;
  moveOrderId?: string;
  /** Optional durable-deployment policy projected into ordinary use leaves. */
  deployment?: PlayPostDeployment;
}

export interface PlayPostDeployment {
  skill: string;
  targetAnchorId: string;
  targetZoneId: string;
  rebuildWhenMissing: boolean;
  refreshDurabilityAtMost?: number;
  refreshLifetimeAtMost?: number;
  requiredForReadiness: boolean;
  presenceConditionId?: string;
  sourceConditionId: string;
  ownedConditionIds: string[];
  useOrderIds: string[];
}

/**
 * Editor linkage for one role-qualified readiness Condition over one authored
 * zone. The condition definition remains the executable source of role, area,
 * comparison and count; this record only lets Plays render it as a formation.
 */
export interface PlayFormationBinding {
  id: string;
  projectionId: string;
  conditionId: string;
}

/**
 * Editor-only linkage saying that several independently authored participant
 * readiness checks arm as one contingent. The executable source remains the
 * flat `all` expression written onto each participant Instruction.
 */
export interface PlayReadinessGroup {
  id: string;
  name: string;
  projectionIds: string[];
  /** Static authoring constraint over the participating Post envelopes. */
  maximumSpread?: number;
}

export interface PlayProjectionPlacement {
  /**
   * Retained only so an older workshop sidecar can still be read. Program
   * materialization deliberately ignores this legacy per-role insertion point:
   * the ordered Program list is the only author-facing priority model.
   */
  beforeOrderKey: string | null;
}

export interface PlayGeneratedOrderFingerprint {
  orderId: string;
  fingerprint: string;
}

export type PlayOpportunityKind =
  | 'use'
  | 'fight'
  | 'core-recovery'
  | 'health-recovery'
  | 'handoff'
  | 'drop';

export interface PlayOpportunityBinding {
  id: string;
  /** Missing on an older sidecar and normalized to use. */
  kind?: PlayOpportunityKind;
  /** An ordinary branch in this projection. */
  hostOrderId: string;
  /**
   * A direct action child of hostOrderId. The legacy property name is kept so
   * old workshop documents normalize without rewriting generated Orders.
   */
  useOrderId: string;
  /** Visible coordination policy used to normalize inline/shared capacity. */
  capacityPolicy?: {
    target: 'core' | 'healing' | 'enemy' | 'carrier' | 'signature-target';
    limit: number | null;
    scope: 'sheet' | 'local';
    groupId?: string;
  };
  /** Conditions derived by this card, so deletion is atomic and inspectable. */
  ownedConditionIds?: string[];
}

export interface PlayProjection {
  id: string;
  roleId: string;
  /**
   * Editor-only activation choice for a configured response. The ordinary
   * generated `when` expression remains the sole runtime gate.
   */
  responseActivation?: 'program-trigger' | 'immediate';
  /**
   * Editor-only activation for the response's ordinary Fight continuation.
   * `program-response` leaves the Fight inherited from the response branch;
   * `on-contact` materializes an ordinary distance Condition plus a hold
   * fallback. Neither value is runtime Program state.
   */
  responseFightActivation?: 'program-response' | 'on-contact';
  /** Condition owned by the On-contact response Fight, when selected. */
  responseFightContactConditionId?: string;
  /**
   * Editor-owned source Orders. Fields owned by linkedValues are omitted here
   * and applied deterministically while materializing the projection.
   */
  orders: OrderNode[];
  /** @deprecated Program order supersedes projection-local placement. */
  placement?: PlayProjectionPlacement;
  lastGenerated: PlayGeneratedOrderFingerprint[];
  /** Editor linkage only; all executable fields remain in orders. */
  signatureOpportunities?: PlayOpportunityBinding[];
}

export interface PlayCustodyBinding {
  id: string;
  /** Ordered editor case that owns this ordinary Instruction branch. */
  caseId?: string;
  projectionId: string;
  hostOrderIds: string[];
  actionOrderIds: string[];
  kind: 'carrier-return' | 'escort-carrier';
  carrierRoleIds: string[];
  routeId?: string;
  coreBankValue?: {
    operator: 'equals' | 'at-least' | 'at-most';
    value: number;
  };
  well?: 'north' | 'centre' | 'south' | 'own' | 'enemy';
  ownedConditionIds: string[];
}

/**
 * One player-authored, top-to-bottom Core handling case. The executable
 * priority remains the generated sibling Instruction order.
 */
export interface PlayCustodyCase {
  id: string;
  name: string;
  coreBankValue?: {
    operator: 'equals' | 'at-least' | 'at-most';
    value: number;
  };
  well?: 'north' | 'centre' | 'south' | 'own' | 'enemy';
}

export type PlayLinkedValue =
  | string
  | number
  | boolean
  | null
  | string[]
  | { all: string[] }
  | { any: string[] };

export interface PlayLinkedValueTarget {
  projectionId: string;
  orderId: string;
  /** Dot-separated field below the authored Order, for example timeout. */
  field: string;
}

export interface PlayLinkedValueDefinition {
  id: string;
  /** Exact Orders field name shown in the editor. */
  field: string;
  value: PlayLinkedValue;
  targets: PlayLinkedValueTarget[];
}

export interface LinkedPlay {
  id: string;
  name: string;
  recipe: PlayRecipe;
  ownedGeometry: PlayOwnedGeometry[];
  geometryGroups: PlayGeometryGroup[];
  slots: PlaySlot[];
  formations?: PlayFormationBinding[];
  readinessGroups?: PlayReadinessGroup[];
  /** Human lifecycle controls projected into response Instructions. */
  lifecycle?: {
    minimumCommitmentTicks: number;
    maximumTicks: number;
    cooldownTicks: number;
    waiting: 'hold-posts' | 'prepare-conditionally' | 'fall-through';
    preparationConditionId?: string;
    successConditionIds?: string[];
  };
  /** The Program-owned trigger copied into every configured participant. */
  responseTriggerConditionId?: string;
  ownedConditionIds: string[];
  ownedCapacityIds: string[];
  projections: PlayProjection[];
  linkedValues: PlayLinkedValueDefinition[];
  /** Editor linkage for ordinary custody Instructions nested under active hosts. */
  coreCustody?: {
    cases?: PlayCustodyCase[];
    bindings: PlayCustodyBinding[];
    completionConditionId?: string;
    completionOutcomes?: Array<'bank' | 'drop' | 'loss'>;
  };
}

export interface WorkshopDocument {
  schema: 'arc-relay-workshop-v1';
  plays: LinkedPlay[];
}

export function emptyWorkshop(): WorkshopDocument {
  return { schema: 'arc-relay-workshop-v1', plays: [] };
}

/**
 * Additive v1 normalization. Existing Play-owned use children are recognized
 * without rewriting projection Orders or materialized gameplay.
 */
export function normalizeWorkshop(value: WorkshopDocument): WorkshopDocument {
  const next = structuredClone(value);
  for (const play of next.plays) {
    play.formations ??= [];
    play.readinessGroups ??= [];
    play.coreCustody ??= { bindings: [] };
    normalizeCustodyCases(play);
    for (const slot of play.slots) {
      slot.name ??= humanize(slot.id.replace(/-slot$/, ''));
      slot.capacity ??= 1;
      if (slot.deployment) slot.deployment.ownedConditionIds ??= [
        ...(slot.deployment.presenceConditionId ? [slot.deployment.presenceConditionId] : []),
        slot.deployment.sourceConditionId,
      ];
    }
    const used = new Set<string>();
    for (const projection of play.projections) {
      projection.responseActivation ??= 'program-trigger';
      projection.responseFightActivation ??= projection.responseFightContactConditionId
        ? 'on-contact' : 'program-response';
      const existing = projection.signatureOpportunities ?? [];
      projection.signatureOpportunities = existing.map((binding) => ({
        ...withoutLegacyBankLink(binding),
        kind: binding.kind ?? 'use',
        ownedConditionIds: [...(binding.ownedConditionIds ?? [])],
      }));
      existing.forEach((binding) => used.add(binding.id));
    }
    for (const projection of play.projections) {
      const bound = new Set(projection.signatureOpportunities!.map((entry) =>
        `${entry.hostOrderId}\0${entry.useOrderId}`));
      walkBranches(projection.orders, (host) => {
        if (!host.id || !host.orders) return;
        for (const [index, child] of host.orders.entries()) {
          if (!child.id || !child.use || bound.has(`${host.id}\0${child.id}`)) continue;
          if (!host.orders.slice(index + 1).some((candidate) => !candidate.use))
            continue;
          const base = `${host.id}-${child.id}-signature`;
          let id = base;
          for (let suffix = 2; used.has(id); suffix++) id = `${base}-${suffix}`;
          used.add(id);
          bound.add(`${host.id}\0${child.id}`);
          projection.signatureOpportunities!.push({
            id,
            kind: 'use',
            hostOrderId: host.id,
            useOrderId: child.id,
            ownedConditionIds: [],
          });
        }
      });
    }
  }
  return next;
}

function normalizeCustodyCases(play: LinkedPlay) {
  const custody = play.coreCustody!;
  custody.cases ??= [];
  const usedIds = new Set(custody.cases.map((entry) => entry.id));
  for (const [index, entry] of custody.cases.entries()) {
    entry.name ||= `Core case ${index + 1}`;
  }
  for (const binding of custody.bindings) {
    const assigned = custody.cases.find((entry) => entry.id === binding.caseId);
    if (assigned) continue;
    let matched = custody.cases.find((entry) =>
      sameValueFilter(entry.coreBankValue, binding.coreBankValue)
      && entry.well === binding.well);
    if (!matched) {
      const name = custodyCaseName(binding.coreBankValue, binding.well);
      const baseId = name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '')
        || 'core-case';
      let id = baseId;
      for (let suffix = 2; usedIds.has(id); suffix++) id = `${baseId}-${suffix}`;
      usedIds.add(id);
      matched = {
        id, name,
        ...(binding.coreBankValue
          ? { coreBankValue: structuredClone(binding.coreBankValue) } : {}),
        ...(binding.well ? { well: binding.well } : {}),
      };
      custody.cases.push(matched);
    }
    binding.caseId = matched.id;
  }
}

function sameValueFilter(
  left: PlayCustodyCase['coreBankValue'],
  right: PlayCustodyCase['coreBankValue'],
) {
  return left?.operator === right?.operator && left?.value === right?.value;
}

function custodyCaseName(
  value: PlayCustodyCase['coreBankValue'],
  well: PlayCustodyCase['well'],
) {
  const valueName = !value ? 'Any Core'
    : value.operator === 'equals' ? `${value.value}-point Core`
      : `${value.operator === 'at-least' ? 'At least' : 'At most'} ${value.value} points`;
  return well ? `${valueName} from ${humanize(well)} Well` : valueName;
}

function withoutLegacyBankLink(binding: PlayOpportunityBinding): PlayOpportunityBinding {
  const next = { ...binding } as PlayOpportunityBinding & { linkedBankProgramId?: string };
  delete next.linkedBankProgramId;
  return next;
}

function humanize(value: string) {
  return value.replace(/-/g, ' ').replace(/\b\w/g, (letter) => letter.toUpperCase());
}

function walkBranches(orders: OrderNode[], visit: (node: OrderNode) => void) {
  const walk = (node: OrderNode) => {
    if (node.orders) visit(node);
    node.orders?.forEach(walk);
  };
  orders.forEach(walk);
}
