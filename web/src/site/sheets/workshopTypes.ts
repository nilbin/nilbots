import type { OrderNode } from './sheetAuthoring';

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
  }>;
}

export interface PlaySlot {
  id: string;
  roleId: string;
  anchorId: string;
  readinessZoneId: string;
  required: boolean;
  tolerance: number;
  capacity?: number | string;
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

export interface PlaySignatureOpportunityBinding {
  id: string;
  /** An ordinary branch in this projection. */
  hostOrderId: string;
  /** A direct child of hostOrderId whose sole action is use. */
  useOrderId: string;
}

export interface PlayProjection {
  id: string;
  roleId: string;
  /**
   * Editor-owned source Orders. Fields owned by linkedValues are omitted here
   * and applied deterministically while materializing the projection.
   */
  orders: OrderNode[];
  /** @deprecated Program order supersedes projection-local placement. */
  placement?: PlayProjectionPlacement;
  lastGenerated: PlayGeneratedOrderFingerprint[];
  /** Editor linkage only; all executable fields remain in orders. */
  signatureOpportunities?: PlaySignatureOpportunityBinding[];
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
  /** The Program-owned trigger copied into every configured participant. */
  responseTriggerConditionId?: string;
  ownedConditionIds: string[];
  ownedCapacityIds: string[];
  projections: PlayProjection[];
  linkedValues: PlayLinkedValueDefinition[];
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
    const used = new Set<string>();
    for (const projection of play.projections) {
      const existing = projection.signatureOpportunities ?? [];
      projection.signatureOpportunities = existing.map((binding) => ({ ...binding }));
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
            hostOrderId: host.id,
            useOrderId: child.id,
          });
        }
      });
    }
  }
  return next;
}

function walkBranches(orders: OrderNode[], visit: (node: OrderNode) => void) {
  const walk = (node: OrderNode) => {
    if (node.orders) visit(node);
    node.orders?.forEach(walk);
  };
  orders.forEach(walk);
}
