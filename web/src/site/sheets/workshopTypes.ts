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

export interface PlayProjectionPlacement {
  /** Null means after every ordinary top-level Order. */
  beforeOrderKey: string | null;
}

export interface PlayGeneratedOrderFingerprint {
  orderId: string;
  fingerprint: string;
}

export interface PlayProjection {
  id: string;
  roleId: string;
  /**
   * Editor-owned source Orders. Fields owned by linkedValues are omitted here
   * and applied deterministically while materializing the projection.
   */
  orders: OrderNode[];
  placement: PlayProjectionPlacement;
  lastGenerated: PlayGeneratedOrderFingerprint[];
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
