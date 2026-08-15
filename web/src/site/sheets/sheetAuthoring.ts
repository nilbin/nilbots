import type { TacticalSheetCatalog } from '../api';
import {
  emptyWorkshop,
  type WorkshopDocument,
} from './workshopTypes';

export type JsonObject = Record<string, unknown>;
export interface Point { x: number; y: number }
export type ConditionOperator = 'at-least' | 'at-most' | 'equals' | 'less-than' | 'greater-than';
export type ConditionExpression = string | { all: string[] } | { any: string[] };
export type OrderAction = 'move' | 'fight' | 'use' | 'handoff' | 'drop' | 'branch' | 'shared';
export type OrderUrgency = 'routine' | 'normal' | 'urgent';

export interface ConditionDefinition {
  fact: string;
  operator: ConditionOperator;
  value: number;
  role?: string;
  zone?: string;
  well?: 'north' | 'centre' | 'south' | 'own' | 'enemy';
  freshnessTicks?: number;
  windowTicks?: number;
  since?: 'match-start' | 'node-entry';
}

export type MoveTarget =
  | 'own-reactor'
  | 'nearest-heal-zone'
  | 'nearest-known-loose-core'
  | 'highest-bank-value-known-loose-core'
  | { zone: string }
  | { anchor: string }
  | { adjacentTo: string }
  | { well: 'north' | 'centre' | 'south' | 'own' | 'enemy' };

export interface MoveOrder {
  to?: MoveTarget;
  patrol?: string;
  follow?: string | { selector: 'most-experienced-ally' };
  via?: string;
  posture?: 'front' | 'line' | 'rear';
  face?: { anchor: string };
}

export type MoveIntentKind = 'to' | 'patrol' | 'follow';

/**
 * Keep the author's selected movement intent while it is incomplete. An empty
 * patrol/follow value is an ordinary editor state until a route/role is picked;
 * truthiness would silently turn it back into a destination move.
 */
export function moveIntentKind(move: MoveOrder): MoveIntentKind {
  if (move.patrol !== undefined) return 'patrol';
  if (move.follow !== undefined) return 'follow';
  return 'to';
}

export interface FightOrder {
  targets: Array<'carrier' | 'weakest' | 'nearest' | 'medic' | 'marked' | 'veteran' | 'deployment'>;
  style: 'advance' | 'hold' | 'kite';
  chaseLimit: number;
}

export type UseTarget = string | { role: string } | { selector: 'most-experienced-ally' };
export interface UseOrder { skill: string; target: UseTarget; at?: string }
export interface HandoffOrder {
  target: 'nearest-ally-toward-own-reactor' | 'nearest-empty-handed-ally' | { role: string };
}

export interface OrderNode {
  id?: string;
  shared?: string;
  when?: ConditionExpression;
  until?: ConditionExpression;
  timeout?: number;
  cooldown?: number;
  urgency?: OrderUrgency;
  capacity?: number | string;
  orders?: OrderNode[];
  move?: MoveOrder;
  fight?: FightOrder;
  use?: UseOrder;
  handoff?: HandoffOrder;
  drop?: true;
}

export interface RoleDefinition {
  id: string;
  classes: string[];
  upgradePath: Array<'damage' | 'vision' | 'reach' | 'vitality'>;
  orders: OrderNode[];
}

export interface OrdersDocument {
  schema: 'arc-relay-orders-v2';
  sheetId: string;
  mapPlan: string;
  roles: RoleDefinition[];
  sharedOrders: Record<string, OrderNode>;
  capacities: Record<string, number>;
  conditions: Record<string, ConditionDefinition>;
}

export interface MapZone { rect: [number, number, number, number] }
export interface MapRoute { waypoints: Array<[number, number]> }
export type MapAnchor = [number, number];
export interface MapPlanDocument {
  schema: 'arc-relay-map-plan-v2';
  mapPlanId: string;
  mapId: string;
  zones: Record<string, MapZone>;
  routes: Record<string, MapRoute>;
  anchors: Record<string, MapAnchor>;
}

export interface SheetDraft {
  localId: string;
  name: string;
  sheetId: string | null;
  revision: number | null;
  enterLadder: boolean;
  orders: OrdersDocument;
  mapPlan: MapPlanDocument;
  workshop: WorkshopDocument;
}

export interface SheetIssue {
  path: string;
  message: string;
  severity: 'error' | 'warning';
}

export const CONDITION_OPERATORS: readonly ConditionOperator[] =
  ['at-least', 'at-most', 'equals', 'less-than', 'greater-than'];
export const CONDITION_FACTS = [
  'self-health-percent', 'self-level', 'self-carrying', 'self-stuck-ticks',
  'self-order-ticks', 'self-in-zone', 'self-nearest-visible-enemy-distance',
  'self-carried-core-bank-value', 'signature-ready', 'deployment-active',
  'deployment-lifetime-percent', 'deployment-durability-percent',
  'role-live-count', 'role-highest-level',
  'role-lowest-health-percent', 'role-in-zone-count', 'live-friendlies',
  'live-enemies',
  'friendly-carriers', 'known-enemy-carriers', 'visible-enemy-carriers',
  'known-loose-cores', 'visible-loose-cores', 'secured-cores',
  'reactor-integrity', 'reactor-charge', 'reactor-pulses', 'enemy-reactor-charge',
  'ticks-until-respawn-wave', 'friendly-respawns-next-wave', 'enemy-respawns-next-wave',
  'ticks-without-objective-progress',
  'friendlies-in-zone', 'visible-enemies-in-zone',
  'remembered-enemies-in-zone', 'known-loose-cores-in-zone',
  'well-has-outstanding-core', 'well-ticks-until-birth', 'well-socket-filled',
  'observed-enemy-entries-in-zone',
  'distinct-enemy-lives-observed-in-zone',
  'confirmed-enemy-destructions-in-zone',
  'zone-fully-observed-throughout-interval',
] as const;
export type ConditionFactGroupId = 'body' | 'squad' | 'match' | 'ground' | 'history';
export const CONDITION_FACT_GROUPS: ReadonlyArray<{
  id: ConditionFactGroupId;
  label: string;
  description: string;
  facts: readonly string[];
}> = [
  {
    id: 'body',
    label: 'Body',
    description: 'Health, cargo, movement, signature readiness and this body\'s deployment.',
    facts: CONDITION_FACTS.filter((fact) => fact.startsWith('self-')
      || fact === 'signature-ready' || fact.startsWith('deployment-')),
  },
  {
    id: 'squad',
    label: 'Squad',
    description: 'Live role strength and where a named role is positioned.',
    facts: CONDITION_FACTS.filter((fact) => fact.startsWith('role-')),
  },
  {
    id: 'match',
    label: 'Match',
    description: 'Current team strength, Core economy, reactor state and respawn timing.',
    facts: CONDITION_FACTS.filter((fact) => !fact.startsWith('self-')
      && !fact.startsWith('role-')
      && !fact.startsWith('deployment-')
      && !fact.startsWith('well-')
      && !fact.includes('-in-zone')
      && ![
        'signature-ready',
        'observed-enemy-entries-in-zone',
        'distinct-enemy-lives-observed-in-zone',
        'confirmed-enemy-destructions-in-zone',
        'zone-fully-observed-throughout-interval',
      ].includes(fact)),
  },
  {
    id: 'ground',
    label: 'Ground',
    description: 'Current pressure in a plotted zone and the live state of a Core Well.',
    facts: CONDITION_FACTS.filter((fact) => fact.startsWith('well-')
      || (!fact.startsWith('self-')
        && !fact.startsWith('role-')
        && fact.includes('-in-zone')
        && ![
          'remembered-enemies-in-zone',
          'observed-enemy-entries-in-zone',
          'distinct-enemy-lives-observed-in-zone',
          'confirmed-enemy-destructions-in-zone',
        ].includes(fact))),
  },
  {
    id: 'history',
    label: 'Observed history',
    description: 'Team memory from genuinely observed play. It survives respawns and resets each match.',
    facts: [
      'remembered-enemies-in-zone',
      'observed-enemy-entries-in-zone',
      'distinct-enemy-lives-observed-in-zone',
      'confirmed-enemy-destructions-in-zone',
      'zone-fully-observed-throughout-interval',
    ],
  },
];
export const ACTIONS: readonly Exclude<OrderAction, 'branch' | 'shared'>[] =
  ['move', 'fight', 'use', 'handoff', 'drop'];
export const UI_STATE_KEY = 'nilbots-sheet-map-ui-v2';

const IDENTIFIER = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const ROLE_FACTS = new Set([
  'role-live-count', 'role-highest-level', 'role-lowest-health-percent', 'role-in-zone-count',
]);
const ZONE_FACTS = new Set([
  'self-in-zone', 'role-in-zone-count', 'friendlies-in-zone', 'visible-enemies-in-zone',
  'remembered-enemies-in-zone', 'known-loose-cores-in-zone',
  'observed-enemy-entries-in-zone',
  'distinct-enemy-lives-observed-in-zone',
  'confirmed-enemy-destructions-in-zone',
  'zone-fully-observed-throughout-interval',
]);
const WELL_FACTS = new Set(['well-has-outstanding-core', 'well-ticks-until-birth', 'well-socket-filled']);
const FRESH_FACTS = new Set(['known-enemy-carriers', 'remembered-enemies-in-zone']);
const HISTORICAL_FACTS = new Set([
  'observed-enemy-entries-in-zone',
  'distinct-enemy-lives-observed-in-zone',
  'confirmed-enemy-destructions-in-zone',
  'zone-fully-observed-throughout-interval',
]);
const PERCENT_FACTS = new Set([
  'self-health-percent', 'role-lowest-health-percent',
  'deployment-lifetime-percent', 'deployment-durability-percent',
]);
const BOOLEAN_FACTS = new Set([
  'self-carrying', 'self-in-zone', 'signature-ready', 'deployment-active',
  'zone-fully-observed-throughout-interval',
]);
const DRAFT_DB = 'nilbots-sheet-drafts-v2';
const DRAFT_STORE = 'drafts';
const ACTIVE_DRAFT_KEY = 'active';
const LOCAL_DRAFT_PREFIX = 'draft:';

export function skillForClass(
  catalog: Pick<TacticalSheetCatalog, 'classes'>,
  classId: string,
): string | undefined {
  return catalog.classes.find((entry) => entry.id === classId)?.signatureId;
}

export function parseDraft(
  name: string,
  sheetId: string | null,
  revision: number | null,
  ordersJson: string,
  mapPlanJson: string,
  enterLadder = true,
  workshopJson?: string | null,
): SheetDraft {
  const orders = JSON.parse(ordersJson) as OrdersDocument;
  const mapPlan = JSON.parse(mapPlanJson) as MapPlanDocument;
  const workshop = workshopJson
    ? JSON.parse(workshopJson) as WorkshopDocument
    : emptyWorkshop();
  if (!orders || typeof orders !== 'object' || Array.isArray(orders))
    throw new Error('sheet.json must contain one JSON object.');
  if (!mapPlan || typeof mapPlan !== 'object' || Array.isArray(mapPlan))
    throw new Error('map-plan.json must contain one JSON object.');
  if (!workshop || typeof workshop !== 'object' || Array.isArray(workshop)
    || workshop.schema !== 'arc-relay-workshop-v1'
    || !Array.isArray(workshop.plays))
    throw new Error('workshop.json must use arc-relay-workshop-v1.');
  return {
    localId: sheetId ? `sheet:${sheetId}` : newLocalDraftId(),
    name,
    sheetId,
    revision,
    enterLadder,
    orders,
    mapPlan,
    workshop,
  };
}

export function compositionOf(orders: OrdersDocument): string[] {
  return orders.roles.flatMap((role) => role.classes);
}

export function actionOf(order: OrderNode): OrderAction | null {
  if (order.shared !== undefined) return 'shared';
  if (order.orders !== undefined) return 'branch';
  return ACTIONS.find((action) => order[action] !== undefined) ?? null;
}

export function freshOrder(
  action: Exclude<OrderAction, 'branch' | 'shared'>,
  id = 'new-order',
  signatureSkill = 'vector-dash',
): OrderNode {
  if (action === 'move') return { id, move: { to: { well: 'centre' }, posture: 'line' } };
  if (action === 'fight') return {
    id,
    fight: { targets: ['carrier', 'weakest', 'nearest'], style: 'advance', chaseLimit: 3 },
  };
  if (action === 'use') return {
    id,
    use: { skill: signatureSkill, target: signatureTargets(signatureSkill)[0] ?? 'self' },
  };
  if (action === 'handoff') return { id, handoff: { target: 'nearest-ally-toward-own-reactor' } };
  return { id, drop: true };
}

export function signatureTargets(skill: string): string[] {
  if (skill === 'null-field' || skill === 'kinetic-burst') return ['self'];
  if (skill === 'repair-beam') return ['weakest-ally', 'nearest-ally'];
  if (skill === 'exchange') return ['healthiest-ally', 'nearest-ally', 'weakest-ally'];
  if (skill === 'arc-toss') return ['nearest-ally-toward-own-reactor', 'nearest-ally', 'own-reactor'];
  if (skill === 'target-paint') return ['enemy-carrier', 'veteran', 'weakest-enemy', 'nearest-enemy'];
  return ['nearest-enemy', 'weakest-enemy', 'enemy-carrier', 'nearest-ally', 'weakest-ally', 'self', 'own-reactor'];
}

export function identifierFromName(value: string, fallback = 'untitled-sheet', maximumLength = 48): string {
  const id = value.toLowerCase().normalize('NFKD')
    .replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '').slice(0, maximumLength);
  return id || fallback;
}

export function uniqueId(prefix: string, existing: Iterable<string>): string {
  const used = new Set(existing);
  if (!used.has(prefix)) return prefix;
  for (let suffix = 2; ; suffix++) if (!used.has(`${prefix}-${suffix}`)) return `${prefix}-${suffix}`;
}

export function renameRole(orders: OrdersDocument, previous: string, next: string): void {
  const role = orders.roles.find((entry) => entry.id === previous);
  if (!role) return;
  role.id = next;
  walkOrders(orders, (node) => {
    if (typeof node.move?.follow === 'string' && node.move.follow === previous)
      node.move.follow = next;
    if (typeof node.handoff?.target === 'object' && node.handoff.target.role === previous)
      node.handoff.target.role = next;
    if (typeof node.use?.target === 'object' && 'role' in node.use.target
      && node.use.target.role === previous)
      node.use.target.role = next;
  });
  for (const condition of Object.values(orders.conditions))
    if (condition.role === previous) condition.role = next;
}

export function renameCondition(orders: OrdersDocument, previous: string, nextId: string): void {
  const rewrite = (expression: ConditionExpression | undefined): ConditionExpression | undefined => {
    if (typeof expression === 'string') return expression === previous ? nextId : expression;
    if (!expression) return expression;
    if ('all' in expression) return { all: expression.all.map((id) => id === previous ? nextId : id) };
    return { any: expression.any.map((id) => id === previous ? nextId : id) };
  };
  walkOrders(orders, (node) => {
    const when = rewrite(node.when);
    const until = rewrite(node.until);
    if (when === undefined) delete node.when;
    else node.when = when;
    if (until === undefined) delete node.until;
    else node.until = until;
  });
}

export function moveRole(orders: OrdersDocument, index: number, delta: number): void {
  const target = index + delta;
  if (target < 0 || target >= orders.roles.length) return;
  const [role] = orders.roles.splice(index, 1);
  orders.roles.splice(target, 0, role);
}

export function removeRole(orders: OrdersDocument, roleId: string): void {
  orders.roles = orders.roles.filter((role) => role.id !== roleId);
}

export function walkOrders(orders: OrdersDocument, visit: (node: OrderNode) => void): void {
  const walk = (node: OrderNode) => {
    visit(node);
    node.orders?.forEach(walk);
  };
  orders.roles.forEach((role) => role.orders.forEach(walk));
  Object.values(orders.sharedOrders).forEach(walk);
}

export function conditionExpressionNames(expression: ConditionExpression | undefined): string[] {
  if (!expression) return [];
  if (typeof expression === 'string') return expression === 'always' ? [] : [expression];
  return 'all' in expression ? expression.all : expression.any;
}

export async function pinDraft(draft: SheetDraft): Promise<{
  draft: SheetDraft;
  ordersJson: string;
  mapPlanJson: string;
  workshopJson: string;
}> {
  const next = structuredClone(draft);
  const id = identifierFromName(next.name);
  next.orders.sheetId = id;
  next.orders.mapPlan = 'map-plan.json';
  next.mapPlan.mapPlanId = `${id}-map`;
  const ordersJson = stringifyDocument(next.orders);
  const mapPlanJson = stringifyDocument(next.mapPlan);
  const workshopJson = stringifyDocument(next.workshop);
  return { draft: next, ordersJson, mapPlanJson, workshopJson };
}

export function stringifyDocument(value: unknown): string {
  return `${JSON.stringify(value, null, 2)}\n`;
}

export function validateDraft(
  draft: SheetDraft,
  catalog: TacticalSheetCatalog,
): SheetIssue[] {
  const issues: SheetIssue[] = [];
  const error = (path: string, message: string) => issues.push({ path, message, severity: 'error' });
  const warning = (path: string, message: string) => issues.push({ path, message, severity: 'warning' });
  const { orders, mapPlan } = draft;
  if (!draft.name.trim()) error('name', 'Give this sheet a name.');
  exactKeys(orders as unknown as JsonObject,
    ['schema', 'sheetId', 'mapPlan', 'roles', 'sharedOrders', 'capacities', 'conditions'],
    'orders', error);
  exactKeys(mapPlan as unknown as JsonObject,
    ['schema', 'mapPlanId', 'mapId', 'zones', 'routes', 'anchors'],
    'mapPlan', error);
  if (orders.schema !== 'arc-relay-orders-v2') error('orders.schema', 'Use the Orders v2 sheet schema.');
  if (mapPlan.schema !== 'arc-relay-map-plan-v2') error('mapPlan.schema', 'Use the Orders v2 map-plan schema.');
  if (mapPlan.mapId !== catalog.map.id) error('mapPlan.mapId', `This sheet must target ${catalog.map.id}.`);

  const roles = new Set(orders.roles.map((role) => role.id));
  const classes = compositionOf(orders);
  if (classes.length !== catalog.slotCount)
    error('orders.roles', `Assign exactly ${catalog.slotCount} bodies; currently ${classes.length}.`);
  const classCounts = new Map<string, number>();
  classes.forEach((id) => classCounts.set(id, (classCounts.get(id) ?? 0) + 1));
  for (const [id, count] of classCounts) {
    const available = catalog.classes.find((entry) => entry.id === id);
    if (!available) error('orders.roles', `Unknown class '${id}'.`);
    else if (!available.unlocked) error('orders.roles', `${available.name} is not unlocked.`);
    if (count > catalog.maximumCopiesPerClass)
      error('orders.roles', `${id} exceeds the ${catalog.maximumCopiesPerClass}-copy cap.`);
  }
  if (classCounts.size < catalog.minimumDistinctClasses) {
    error('orders.roles',
      `Use at least ${catalog.minimumDistinctClasses} distinct classes; currently ${classCounts.size}.`);
  }

  const sharedUse = new Map<string, number>();
  const capacityUse = new Map<string, number>();
  const conditionUse = new Map<string, number>();
  const usedZones = new Set<string>();
  const usedRoutes = new Set<string>();
  const usedAnchors = new Set<string>();
  orders.roles.forEach((role, roleIndex) => {
    const base = `orders.roles.${roleIndex}`;
    if (!validId(role.id, 24)) error(`${base}.id`, 'Role id must be kebab-case and at most 24 characters.');
    if (orders.roles.findIndex((entry) => entry.id === role.id) !== roleIndex)
      error(`${base}.id`, `Role '${role.id}' is duplicated.`);
    if (role.classes.length < 1) error(`${base}.classes`, 'A role needs at least one body.');
    if (role.upgradePath.length < 1 || role.upgradePath.length > 4)
      error(`${base}.upgradePath`, 'Choose one to four upgrade priorities.');
    if (role.orders.length < 1) error(`${base}.orders`, 'A role needs at least one order.');
    const ids = new Set<string>();
    role.orders.forEach((node, index) => validateOrder(
      node, `${base}.orders.${index}`, role, orders, catalog, roles, ids, 0,
      sharedUse, capacityUse, conditionUse, usedZones, usedRoutes, usedAnchors,
      error, warning, index < role.orders.length - 1, undefined, true,
    ));
  });
  // Role references are checked again after the complete role set is known.
  walkOrders(orders, (node) => {
    if (typeof node.move?.follow === 'string' && !roles.has(node.move.follow))
      error('orders.roles', `Follow names unknown role '${node.move.follow}'.`);
    if (typeof node.handoff?.target === 'object' && !roles.has(node.handoff.target.role))
      error('orders.roles', `Handoff names unknown role '${node.handoff.target.role}'.`);
    if (typeof node.use?.target === 'object' && 'role' in node.use.target
      && !roles.has(node.use.target.role))
      error('orders.roles', `Use names unknown role '${node.use.target.role}'.`);
  });

  for (const [id, definition] of Object.entries(orders.sharedOrders)) {
    if (!validId(id)) error(`orders.sharedOrders.${id}`, 'Shared order id must be kebab-case.');
    if ((sharedUse.get(id) ?? 0) < 2)
      error(`orders.sharedOrders.${id}`, 'A shared order needs at least two references; inline a one-off.');
    if (definition.id || definition.shared)
      error(`orders.sharedOrders.${id}`, 'The map key is the id; shared definitions cannot chain.');
  }
  for (const [id, value] of Object.entries(orders.capacities)) {
    if (!validId(id)) error(`orders.capacities.${id}`, 'Capacity id must be kebab-case.');
    if (!Number.isInteger(value) || value < 1 || value > 8)
      error(`orders.capacities.${id}`, 'Capacity is one to eight bodies.');
    if ((capacityUse.get(id) ?? 0) < 2)
      error(`orders.capacities.${id}`, 'A named capacity needs at least two order consumers.');
  }
  for (const [id, condition] of Object.entries(orders.conditions)) {
    const path = `orders.conditions.${id}`;
    exactKeys(condition as unknown as JsonObject,
      ['fact', 'operator', 'value'], path, error,
      ['role', 'zone', 'well', 'freshnessTicks', 'windowTicks', 'since']);
    if (!validId(id)) error(path, 'Condition id must be kebab-case.');
    if (!(CONDITION_FACTS as readonly string[]).includes(condition.fact)) error(`${path}.fact`, 'Choose a published fact.');
    if (!CONDITION_OPERATORS.includes(condition.operator)) error(`${path}.operator`, 'Choose a comparison.');
    if (!Number.isInteger(condition.value)) error(`${path}.value`, 'Comparison value must be a whole number.');
    if (PERCENT_FACTS.has(condition.fact) && (condition.value < 0 || condition.value > 100))
      error(`${path}.value`, 'Percent facts compare from 0 to 100.');
    if (BOOLEAN_FACTS.has(condition.fact) && condition.value !== 0 && condition.value !== 1)
      error(`${path}.value`, 'This yes/no fact compares with 0 or 1.');
    if (condition.fact === 'self-carried-core-bank-value'
      && (condition.value < 0 || condition.value > 4))
      error(`${path}.value`, 'Carried Core value is 0 to 4.');
    if (['friendly-respawns-next-wave', 'enemy-respawns-next-wave', 'live-enemies']
      .includes(condition.fact) && (condition.value < 0 || condition.value > 8))
      error(`${path}.value`, 'Body counts are 0 to 8.');
    if (condition.fact === 'self-nearest-visible-enemy-distance'
      && (condition.value < 0 || condition.value > 31))
      error(`${path}.value`, 'Firing-approach distance is zero to 31 steps.');
    if (ROLE_FACTS.has(condition.fact) && (!condition.role || !roles.has(condition.role)))
      error(`${path}.role`, 'This fact needs a declared role.');
    if (ZONE_FACTS.has(condition.fact)) {
      if (!condition.zone) error(`${path}.zone`, 'This fact needs a map zone.');
      else usedZones.add(condition.zone);
    }
    if (WELL_FACTS.has(condition.fact) && !condition.well)
      error(`${path}.well`, 'This fact needs a Well selector.');
    if (FRESH_FACTS.has(condition.fact)
      && (!Number.isInteger(condition.freshnessTicks) || condition.freshnessTicks! < 1 || condition.freshnessTicks! > 600))
      error(`${path}.freshnessTicks`, 'Remembered facts need freshness from 1 to 600 ticks.');
    const intervalCount = Number(condition.windowTicks !== undefined)
      + Number(condition.since !== undefined);
    if (HISTORICAL_FACTS.has(condition.fact)) {
      if (intervalCount !== 1)
        error(path, 'Historical evidence needs exactly one interval.');
      if (condition.windowTicks !== undefined
        && (!Number.isInteger(condition.windowTicks)
          || condition.windowTicks < 1 || condition.windowTicks > 1200))
        error(`${path}.windowTicks`, 'The rolling window is 1–1200 ticks.');
      if (condition.since !== undefined
        && !['match-start', 'node-entry'].includes(condition.since))
        error(`${path}.since`, 'Choose match start or this order episode.');
      if (condition.value < 0)
        error(`${path}.value`, 'Historical evidence cannot compare below zero.');
    } else if (intervalCount > 0) {
      error(path, 'Only historical evidence facts take an interval.');
    }
    if ((conditionUse.get(id) ?? 0) === 0)
      error(path, 'Delete this unused condition or use it in an order.');
  }

  validateGeometry(mapPlan, catalog, usedZones, usedRoutes, usedAnchors, error);
  return issues;
}

function validateOrder(
  node: OrderNode,
  path: string,
  role: RoleDefinition,
  orders: OrdersDocument,
  catalog: Pick<TacticalSheetCatalog, 'classes'>,
  roles: ReadonlySet<string>,
  ids: Set<string>,
  depth: number,
  sharedUse: Map<string, number>,
  capacityUse: Map<string, number>,
  conditionUse: Map<string, number>,
  usedZones: Set<string>,
  usedRoutes: Set<string>,
  usedAnchors: Set<string>,
  error: (path: string, message: string) => void,
  warning: (path: string, message: string) => void,
  hasLaterSibling: boolean,
  externalId?: string,
  allowShared = true,
): void {
  const actionKeys = ['orders', 'move', 'fight', 'use', 'handoff', 'drop'] as const;
  const found = actionKeys.filter((key) => node[key] !== undefined);
  const action = actionOf(node);
  if (action === 'shared') {
    exactKeys(node as unknown as JsonObject, ['shared'], path, error);
    const definition = node.shared ? orders.sharedOrders[node.shared] : undefined;
    if (!allowShared) error(path, 'Shared orders cannot reference another shared order.');
    else if (!node.shared || !definition) error(`${path}.shared`, `Unknown shared order '${node.shared ?? ''}'.`);
    else {
      sharedUse.set(node.shared, (sharedUse.get(node.shared) ?? 0) + 1);
      validateOrder(
        definition, `${path}.shared(${node.shared})`, role, orders, catalog,
        roles, ids,
        depth, sharedUse, capacityUse, conditionUse, usedZones, usedRoutes,
        usedAnchors, error, warning, hasLaterSibling, node.shared, false,
      );
    }
    return;
  }
  if (!action || found.length !== 1) {
    error(path, 'Choose exactly one order action.');
    return;
  }
  const id = externalId ?? node.id;
  if (!id || !validId(id)) error(`${path}.id`, 'Order id must be kebab-case.');
  else if (ids.has(id)) error(`${path}.id`, `Order '${id}' is duplicated in ${role.id}.`);
  else ids.add(id);
  const expected = [...(externalId ? [] : ['id']), found[0]];
  exactKeys(node as unknown as JsonObject, expected,
    path, error, ['when', 'until', 'timeout', 'cooldown', 'capacity', 'urgency']);
  for (const expression of [['when', node.when], ['until', node.until]] as const)
    validateExpression(expression[1], `${path}.${expression[0]}`, orders, conditionUse, error,
      expression[0] === 'until');
  if (node.timeout !== undefined && (!Number.isInteger(node.timeout) || node.timeout < 1 || node.timeout > 1200))
    error(`${path}.timeout`, 'Timeout is 1–1200 ticks.');
  if (node.cooldown !== undefined && (!Number.isInteger(node.cooldown) || node.cooldown < 0 || node.cooldown > 1200))
    error(`${path}.cooldown`, 'Cooldown is 0–1200 ticks.');
  if (node.urgency !== undefined && !(['routine', 'normal', 'urgent'] as const).includes(node.urgency))
    error(`${path}.urgency`, 'Urgency is routine, normal, or urgent.');
  if (typeof node.capacity === 'number' && (!Number.isInteger(node.capacity) || node.capacity < 1 || node.capacity > 8))
    error(`${path}.capacity`, 'Inline capacity is one to eight bodies.');
  if (typeof node.capacity === 'string') {
    if (orders.capacities[node.capacity] === undefined) error(`${path}.capacity`, `Unknown capacity '${node.capacity}'.`);
    else capacityUse.set(node.capacity, (capacityUse.get(node.capacity) ?? 0) + 1);
  }
  if (action === 'branch') {
    if (node.capacity !== undefined)
      error(`${path}.capacity`, 'Capacity belongs on an action leaf, not a branch.');
    if (depth >= 2) error(path, 'Orders may nest through at most two branches.');
    if (!node.orders?.length) error(`${path}.orders`, 'A branch needs child orders.');
    node.orders?.forEach((child, index) => validateOrder(
      child, `${path}.orders.${index}`, role, orders, catalog, roles, ids,
      depth + 1,
      sharedUse, capacityUse, conditionUse, usedZones, usedRoutes, usedAnchors,
      error, warning, index < node.orders!.length - 1, undefined, true,
    ));
    return;
  }
  if (action === 'move') {
    const move = node.move!;
    const kinds = [move.to !== undefined, move.patrol !== undefined, move.follow !== undefined].filter(Boolean).length;
    if (kinds !== 1) error(`${path}.move`, 'Choose one destination, patrol route, or followed role.');
    const requiredMoveKey = move.to !== undefined ? 'to'
      : move.patrol !== undefined ? 'patrol' : 'follow';
    exactKeys(move as unknown as JsonObject, [requiredMoveKey],
      `${path}.move`, error, ['via', 'posture', 'face']);
    if (move.patrol) usedRoutes.add(move.patrol);
    if (move.via) usedRoutes.add(move.via);
    // The complete role set is checked after every role has been walked. At
    // this point a valid forward reference may not have been visited yet.
    if (typeof move.follow === 'string' && move.follow === role.id)
      error(`${path}.move.follow`, 'Follow another declared role.');
    if (typeof move.to === 'object') {
      if ('zone' in move.to) usedZones.add(move.to.zone);
      if ('anchor' in move.to) usedAnchors.add(move.to.anchor);
      if ('adjacentTo' in move.to) usedAnchors.add(move.to.adjacentTo);
    }
    if (move.face) {
      exactKeys(move.face as unknown as JsonObject, ['anchor'],
        `${path}.move.face`, error);
      usedAnchors.add(move.face.anchor);
      const authoredDestination = typeof move.to === 'object'
        && ('zone' in move.to || 'anchor' in move.to);
      if (!authoredDestination)
        error(`${path}.move.face`, 'Facing needs an authored zone or anchor destination.');
    }
  }
  if (action === 'fight') {
    if (!node.fight!.targets.length) error(`${path}.fight.targets`, 'Choose at least one target priority.');
    if (!Number.isInteger(node.fight!.chaseLimit) || node.fight!.chaseLimit < 0 || node.fight!.chaseLimit > 12)
      error(`${path}.fight.chaseLimit`, 'Chase limit is 0–12 tiles.');
  }
  if (action === 'use') {
    exactKeys(node.use! as unknown as JsonObject, ['skill', 'target'],
      `${path}.use`, error, ['at']);
    const skills = new Set(
      role.classes.map((classId) => skillForClass(catalog, classId)),
    );
    if (!skills.has(node.use!.skill))
      error(`${path}.use.skill`, 'At least one body in this role must own this signature skill.');
    if (typeof node.use!.target === 'object') {
      const target = node.use!.target;
      if ('role' in target && !roles.has(target.role))
        error(`${path}.use.target`, 'Target an existing role.');
      if ('selector' in target && target.selector !== 'most-experienced-ally')
        error(`${path}.use.target`, 'Choose a published friendly selector.');
      if (!['repair-beam', 'exchange', 'arc-toss'].includes(node.use!.skill))
        error(`${path}.use.target`, 'Only friendly-target skills can name a role or selector.');
    }
    if (node.use!.at) {
      usedAnchors.add(node.use!.at);
      if (!['hardlight-block', 'trip-node', 'sentinel-seed']
        .includes(node.use!.skill))
        error(`${path}.use.at`, 'Only placeable constructs can use an exact map anchor.');
    }
  }
  if (!node.when && !node.until && hasLaterSibling)
    warning(path, 'An unconditional order above later orders will always win when it has a target.');
}

function validateExpression(
  expression: ConditionExpression | undefined,
  path: string,
  orders: OrdersDocument,
  uses: Map<string, number>,
  error: (path: string, message: string) => void,
  isUntil: boolean,
) {
  if (!expression) return;
  const names = conditionExpressionNames(expression);
  if (typeof expression !== 'string' && (names.length < 2 || names.length > 8))
    error(path, 'AND/OR combines two to eight conditions.');
  if (new Set(names).size !== names.length) error(path, 'Do not repeat a condition in one expression.');
  for (const name of names) {
    const condition = orders.conditions[name];
    if (!condition) error(path, `Unknown condition '${name}'.`);
    else {
      if (condition.since === 'node-entry' && !isUntil)
        error(path, `'${name}' reads this order episode and is legal only in Until.`);
      uses.set(name, (uses.get(name) ?? 0) + 1);
    }
  }
}

function validateGeometry(
  mapPlan: MapPlanDocument,
  catalog: TacticalSheetCatalog,
  usedZones: ReadonlySet<string>,
  usedRoutes: ReadonlySet<string>,
  usedAnchors: ReadonlySet<string>,
  error: (path: string, message: string) => void,
) {
  for (const [id, zone] of Object.entries(mapPlan.zones)) {
    const path = `mapPlan.zones.${id}`;
    if (!validId(id)) error(path, 'Zone id must be kebab-case.');
    const [x1, y1, x2, y2] = zone.rect;
    if (![x1, y1, x2, y2].every(Number.isInteger) || x1 > x2 || y1 > y2
      || !inside(x1, y1, catalog) || !inside(x2, y2, catalog))
      error(`${path}.rect`, 'Draw a normalized rectangle inside the map.');
    if (!usedZones.has(id)) error(path, 'Delete this unused zone or target it from an order/condition.');
  }
  for (const [id, route] of Object.entries(mapPlan.routes)) {
    const path = `mapPlan.routes.${id}`;
    if (!validId(id)) error(path, 'Route id must be kebab-case.');
    if (route.waypoints.length < 2 || route.waypoints.length > 32)
      error(`${path}.waypoints`, 'A route needs 2–32 waypoints.');
    route.waypoints.forEach(([x, y], index) => {
      if (!inside(x, y, catalog)) error(`${path}.waypoints.${index}`, 'Plot this waypoint inside the map.');
      else if (!traversableForBothSides(x, y, catalog))
        error(`${path}.waypoints.${index}`, 'Move this waypoint off the wall so both team-side previews can traverse it.');
      else if (reservedSpawnForEitherSide(x, y, catalog))
        error(`${path}.waypoints.${index}`, 'Move this waypoint off the reserved body spawn so neither team is routed onto a permanently occupied tile.');
    });
    if (!usedRoutes.has(id)) error(path, 'Delete this unused route or use it in a move order.');
  }
  for (const [id, [x, y]] of Object.entries(mapPlan.anchors)) {
    const path = `mapPlan.anchors.${id}`;
    if (!validId(id)) error(path, 'Anchor id must be kebab-case.');
    if (!inside(x, y, catalog)) error(path, 'Plot this anchor inside the map.');
    else if (!traversableForBothSides(x, y, catalog))
      error(path, 'Move this anchor off the wall so both team-side previews can traverse it.');
    else if (reservedSpawnForEitherSide(x, y, catalog))
      error(path, 'Move this anchor off the reserved body spawn so neither team targets a permanently occupied tile.');
    if (!usedAnchors.has(id)) error(path, 'Delete this unused anchor or target it from an order.');
  }
}

function traversableForBothSides(
  x: number,
  y: number,
  catalog: TacticalSheetCatalog,
) {
  const west = catalog.map.tileRows[y]?.[x];
  const east = catalog.map.tileRows[catalog.map.height - 1 - y]?.[
    catalog.map.width - 1 - x
  ];
  return west !== undefined && west !== '#' && east !== undefined && east !== '#';
}

function reservedSpawnForEitherSide(
  x: number,
  y: number,
  catalog: TacticalSheetCatalog,
) {
  const eastX = catalog.map.width - 1 - x;
  const eastY = catalog.map.height - 1 - y;
  return catalog.map.spawnAnchors.some(({ position }) =>
    (position.x === x && position.y === y)
    || (position.x === eastX && position.y === eastY));
}

function exactKeys(
  value: JsonObject,
  expected: string[],
  path: string,
  error: (path: string, message: string) => void,
  optional: string[] = [],
) {
  const actual = Object.keys(value);
  for (const key of expected) if (!(key in value)) error(path, `Required field '${key}' is missing.`);
  for (const key of actual) if (!expected.includes(key) && !optional.includes(key))
    error(`${path}.${key}`, `Retired or unknown field '${key}'.`);
}

function validId(value: string, maximum = 48) {
  return value.length >= 1 && value.length <= maximum && IDENTIFIER.test(value);
}

function inside(x: unknown, y: unknown, catalog: TacticalSheetCatalog) {
  return typeof x === 'number' && typeof y === 'number'
    && Number.isInteger(x) && Number.isInteger(y)
    && x >= 0 && x < catalog.map.width && y >= 0 && y < catalog.map.height;
}

function newLocalDraftId() {
  return `local:${typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(16).slice(2)}`}`;
}

export async function saveLocalDraft(draft: SheetDraft): Promise<void> {
  if (typeof indexedDB === 'undefined') return;
  const db = await openDraftDatabase();
  await new Promise<void>((resolve, reject) => {
    const transaction = db.transaction(DRAFT_STORE, 'readwrite');
    const store = transaction.objectStore(DRAFT_STORE);
    store.put({ key: `${LOCAL_DRAFT_PREFIX}${draft.localId}`, value: draft, updatedAt: Date.now() });
    store.put({ key: ACTIVE_DRAFT_KEY, localId: draft.localId, updatedAt: Date.now() });
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
  });
  db.close();
}

export async function loadLocalDraft(localId?: string): Promise<SheetDraft | null> {
  if (typeof indexedDB === 'undefined') return null;
  const db = await openDraftDatabase();
  const active = localId ? undefined : await readLocalRecord(db, ACTIVE_DRAFT_KEY);
  const resolvedId = localId ?? active?.localId;
  if (!resolvedId) { db.close(); return null; }
  const result = await readLocalRecord(db, `${LOCAL_DRAFT_PREFIX}${resolvedId}`);
  db.close();
  return result?.value ? withWorkshop(result.value) : null;
}

export async function listLocalDrafts(): Promise<SheetDraft[]> {
  if (typeof indexedDB === 'undefined') return [];
  const db = await openDraftDatabase();
  const records = await new Promise<LocalDraftRecord[]>((resolve, reject) => {
    const request = db.transaction(DRAFT_STORE).objectStore(DRAFT_STORE).getAll();
    request.onsuccess = () => resolve(request.result as LocalDraftRecord[]);
    request.onerror = () => reject(request.error);
  });
  db.close();
  return records.filter((record) => record.key.startsWith(LOCAL_DRAFT_PREFIX) && record.value)
    .sort((a, b) => b.updatedAt - a.updatedAt).map((record) => withWorkshop(record.value!));
}

export async function deleteLocalDraft(localId: string): Promise<void> {
  if (typeof indexedDB === 'undefined') return;
  const db = await openDraftDatabase();
  await new Promise<void>((resolve, reject) => {
    const transaction = db.transaction(DRAFT_STORE, 'readwrite');
    transaction.objectStore(DRAFT_STORE).delete(`${LOCAL_DRAFT_PREFIX}${localId}`);
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
  });
  db.close();
}

interface LocalDraftRecord { key: string; value?: SheetDraft; localId?: string; updatedAt: number }

function readLocalRecord(db: IDBDatabase, key: string): Promise<LocalDraftRecord | undefined> {
  return new Promise((resolve, reject) => {
    const request = db.transaction(DRAFT_STORE).objectStore(DRAFT_STORE).get(key);
    request.onsuccess = () => resolve(request.result as LocalDraftRecord | undefined);
    request.onerror = () => reject(request.error);
  });
}

function openDraftDatabase(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DRAFT_DB, 1);
    request.onupgradeneeded = () => {
      if (!request.result.objectStoreNames.contains(DRAFT_STORE))
        request.result.createObjectStore(DRAFT_STORE, { keyPath: 'key' });
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

function withWorkshop(draft: SheetDraft): SheetDraft {
  if (draft.workshop?.schema === 'arc-relay-workshop-v1') return draft;
  return { ...draft, workshop: emptyWorkshop() };
}

/** Map the shared compiler's path-bearing diagnostic onto the editor's panels. */
export function sheetIssueFromCompilerError(cause: unknown): SheetIssue {
  const source = cause instanceof Error ? cause.message : 'The server compiler rejected this draft.';
  const match = source.match(/(?:^|\/)(orders|map-plan)\.json(?:\.([^:]+))?:\s*(.+)$/s)
    ?? source.match(/^(orders|map-plan)(?:\.([^:]+))?:\s*(.+)$/s);
  if (!match) return { path: 'server', message: `Compiler: ${source}`, severity: 'error' };
  const [, document, suffix, message] = match;
  return {
    path: document === 'map-plan'
      ? `mapPlan${suffix ? `.${suffix}` : ''}`
      : `orders${suffix ? `.${suffix}` : ''}`,
    message: `Compiler: ${message.trim()}`,
    severity: 'error',
  };
}

export function conditionSubjectKind(fact: string): 'zone' | 'role-zone' | 'role' | 'well' | null {
  if (fact === 'role-in-zone-count') return 'role-zone';
  if (ZONE_FACTS.has(fact)) return 'zone';
  if (ROLE_FACTS.has(fact)) return 'role';
  if (WELL_FACTS.has(fact)) return 'well';
  return null;
}
