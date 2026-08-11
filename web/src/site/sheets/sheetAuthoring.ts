import type { TacticalSheetCatalog } from '../api';

export type TacticalVerb =
  | 'patrol'
  | 'intercept'
  | 'assault'
  | 'recover'
  | 'fetch'
  | 'follow';

export type Point = [number, number];
export type JsonObject = Record<string, unknown>;

export interface TacticalMode extends JsonObject {
  patrol?: TacticalDestination;
  intercept?: string;
  assault?: TacticalDestination;
  recover?: string;
  fetch?: string;
  follow?: string;
  while?: string;
  until?: string;
  from?: string;
  focus?: string | FocusDefinition;
  cohort?: string | CohortDefinition;
  posture?: string;
  fight?: FightBlock;
}

export type TacticalDestination = string | { route: string } | { zone: string };

export interface FocusDefinition extends JsonObject {
  maximumAttackers: number;
}

export interface CohortDefinition extends JsonObject {
  take: number;
  select: 'nearest';
}

export interface FightBlock extends JsonObject {
  collect?: string;
  heal?: string;
  targets?: JsonObject;
  engage?: JsonObject;
  chase?: JsonObject;
  breakOff?: JsonObject;
  defense?: JsonObject;
}

export interface Doctrine extends JsonObject {
  custody: string;
  conceal?: boolean;
  scavenge?: string | string[];
  fight?: FightBlock;
  repair?: RepairBlock;
  modes: TacticalMode[];
}

export interface RepairBlock extends JsonObject {
  prefer: string[];
  maximumProvidersPerTarget: number;
  minimumProviderSeparation: number;
  reserveHealthPercent: number;
  survivalFallback: string;
}

export interface Predicate extends JsonObject {
  fact: string;
  operator: string;
  value: number;
  subject?: string;
  zone?: string;
  freshnessTicks?: number;
}

export interface RoleDefinition extends JsonObject {
  roleId: string;
  candidateClasses: string[];
  minimum?: number;
  preferred?: number;
  maximum?: number;
  carrierPreference?: string;
  deathPolicy?: string;
  respawnPolicy?: string;
  overflowRoleId?: string;
  build?: string[];
}

export interface CustodyPolicy extends JsonObject {
  custodyId: string;
  sourceWells: string[];
  pickupReservationTicks: number;
  transferTimeoutTicks: number;
  deliveryTimeoutTicks: number;
  accidentalPickup: string;
  dropRecovery: string;
  unreachableFallback: string;
  safeConversion: string;
  pickupSelection?: string;
  deliveryRoutes?: { zone: string; route: string }[];
  baitDrop?: ({ zone: string; reclaim: string } & JsonObject);
  forwardPass?: string;
  emergencyRecoveryZones?: string[];
  emergencyPickupRadius?: number;
  emergencyRecovery?: string;
  emergencyRecoveryRoles?: string[];
  emergencyRecoverySourceWells?: string[];
  emergencyRecoveryDisposition?: string;
  emergencyDisplacementTarget?: string;
  emergencyDisplacementReleaseRadius?: number;
}

export interface DoctrinePlane extends JsonObject {
  roles: Record<string, Doctrine>;
  cohorts: Record<string, CohortDefinition>;
  focus: Record<string, FocusDefinition>;
}

export interface PlaybookDocument extends JsonObject {
  schema: string;
  playbookId: string;
  composition: string[];
  layout: { path: string; sha256: string } & JsonObject;
  memory: {
    enemyUnavailableTicks: number;
    lastSeenEnemyTicks: number;
    securedCoreTicks: number;
    objectiveProgressTicks: number;
  } & JsonObject;
  roles: RoleDefinition[];
  custodyPolicies: CustodyPolicy[];
  doctrines: DoctrinePlane;
  conditions: Record<string, Predicate>;
}

export interface LayoutZone extends JsonObject {
  zoneId: string;
  rect: [number, number, number, number];
}

export interface LayoutRoute extends JsonObject {
  routeId: string;
  corridorWidth: number;
  waypoints: Point[];
}

export interface LayoutAnchor extends JsonObject {
  anchorId: string;
  position: Point;
}

export interface LayoutBinding extends JsonObject {
  matchContractFingerprint: string;
  ownReactorSide: string;
  transform: string;
  routeAliases: Record<string, string>;
  formationAliases?: Record<string, string>;
}

export interface LayoutDocument extends JsonObject {
  schema: string;
  layoutId: string;
  mapId: string;
  bindings: LayoutBinding[];
  zones: LayoutZone[];
  routes: LayoutRoute[];
  anchors: LayoutAnchor[];
}

export interface SheetDraft {
  /** Editor identity only. This never enters playbook.json or layout.json. */
  localId: string;
  name: string;
  sheetId: string | null;
  revision: number | null;
  enterLadder: boolean;
  playbook: PlaybookDocument;
  layout: LayoutDocument;
}

export interface SheetIssue {
  path: string;
  message: string;
  severity: 'error' | 'warning';
}

export const TACTICAL_VERBS: readonly TacticalVerb[] = [
  'patrol', 'intercept', 'assault', 'recover', 'fetch', 'follow',
];

export const CONDITION_OPERATORS = [
  'at-least', 'at-most', 'equals', 'less-than', 'greater-than',
] as const;

export const NO_SUBJECT_FACTS = [
  'self-health', 'self-level', 'self-carrying', 'self-stuck-ticks',
  'self-mode-ticks', 'always', 'tick', 'phase-state-ticks', 'live-friendlies',
  'known-enemies-unavailable', 'visible-enemy-carriers',
  'known-enemy-carriers', 'friendly-carriers', 'secured-cores',
  'visible-loose-cores', 'visible-loose-core-value',
  'outstanding-well-count', 'ticks-without-objective-progress',
  'reactor-integrity', 'reactor-charge', 'custody-state-ticks',
  'own-filled-sockets', 'enemy-filled-sockets',
] as const;

export const ZONE_FACTS = [
  'friendlies-in-zone-count',
  'visible-enemies-in-zone', 'remembered-enemies-in-zone',
  'visible-loose-cores-in-zone', 'visible-loose-core-value-in-zone',
] as const;

export const OPTIONAL_ROLE_FACTS = [
  'max-level', 'recover-ready-bodies',
] as const;

export const ROLE_FACTS = [
  'live-count', 'joining-count', 'cohesion', 'stuck-ticks',
  'role-live-count', 'role-health',
] as const;

export const ROLE_ZONE_FACTS = [
  'in-zone-count',
] as const;

export const WELL_FACTS = [
  'well-has-outstanding', 'own-socket-filled', 'enemy-socket-filled',
  'well-ticks-until-birth',
] as const;

export const CONDITION_FACTS = [
  ...NO_SUBJECT_FACTS,
  ...ZONE_FACTS,
  ...OPTIONAL_ROLE_FACTS,
  ...ROLE_FACTS,
  ...ROLE_ZONE_FACTS,
  ...WELL_FACTS,
] as const;

const IDENTIFIER = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const CONDITION = /^[a-z0-9-]+(?: (?:and|or) [a-z0-9-]+)*$/;
const DRAFT_DB = 'nilbots-sheet-authoring-v1';
const DRAFT_STORE = 'drafts';
export const ACTIVE_DRAFT_KEY = 'active';
const LOCAL_DRAFT_PREFIX = 'draft:';
export const UI_STATE_KEY = 'nilbots-sheet-authoring-ui-v1';

export function parseDraft(
  name: string,
  sheetId: string | null,
  revision: number | null,
  playbookJson: string,
  layoutJson: string,
  enterLadder = true,
  localId = sheetId ? `sheet:${sheetId}` : newLocalDraftId(),
): SheetDraft {
  const playbook = JSON.parse(playbookJson) as PlaybookDocument;
  const layout = JSON.parse(layoutJson) as LayoutDocument;
  if (!playbook || typeof playbook !== 'object' || Array.isArray(playbook))
    throw new Error('playbook.json must contain one JSON object.');
  if (!layout || typeof layout !== 'object' || Array.isArray(layout))
    throw new Error('layout.json must contain one JSON object.');
  return { localId, name, sheetId, revision, enterLadder, playbook, layout };
}

function newLocalDraftId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto)
    return `local:${crypto.randomUUID()}`;
  return `local:${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}

/** Player-facing assignments compile to role ids. Keep every role reference
 * coherent when an author renames one; the shared compiler remains the final
 * authority over the resulting document. */
export function renameAssignment(
  playbook: PlaybookDocument,
  previousId: string,
  nextId: string,
): void {
  if (previousId === nextId) return;
  const doctrine = playbook.doctrines.roles[previousId];
  if (doctrine) {
    playbook.doctrines.roles = Object.fromEntries(
      Object.entries(playbook.doctrines.roles).map(([key, value]) =>
        [key === previousId ? nextId : key, value]),
    );
  }
  rewriteRoleReferences(playbook, previousId, nextId);
}

/** Change the normal staffing target while preserving the authored band. */
export function setAssignmentTarget(
  playbook: PlaybookDocument,
  roleId: string,
  preferred: number,
): void {
  const role = playbook.roles.find((entry) => entry.roleId === roleId);
  if (!role) return;
  const nextPreferred = Math.max(0, Math.min(8, Math.trunc(preferred)));
  role.preferred = nextPreferred;
  role.minimum = Math.min(role.minimum ?? 0, nextPreferred);
  role.maximum = Math.max(role.maximum ?? nextPreferred, nextPreferred);
}

/** Add a deliberately unstaffed assignment. The player moves part of the
 * existing eight-body target into it; adding the record itself never silently
 * steals a body from another job. */
export function addAssignment(
  playbook: PlaybookDocument,
  roleId: string,
  candidateClass: string,
  fallbackRoute: string,
): void {
  if (playbook.roles.some((role) => role.roleId === roleId))
    throw new Error(`Assignment '${roleId}' already exists.`);
  const custody = playbook.custodyPolicies[0]?.custodyId ?? '';
  playbook.roles.push({
    roleId,
    candidateClasses: [candidateClass],
    minimum: 0,
    preferred: 0,
    maximum: 8,
    carrierPreference: 'allow',
    deathPolicy: 'hold-vacancy',
    respawnPolicy: 'rejoin',
    overflowRoleId: playbook.roles[0]?.roleId ?? roleId,
    build: ['vitality'],
  });
  playbook.doctrines.roles[roleId] = {
    custody,
    conceal: true,
    modes: [{
      patrol: fallbackRoute,
      focus: { maximumAttackers: 2 },
    }],
  };
}

/** Duplicate authored behavior without duplicating its staffing claim. */
export function duplicateAssignment(
  playbook: PlaybookDocument,
  sourceRoleId: string,
  nextRoleId: string,
): void {
  const source = playbook.roles.find((role) => role.roleId === sourceRoleId);
  const doctrine = playbook.doctrines.roles[sourceRoleId];
  if (!source || !doctrine) throw new Error(`Unknown assignment '${sourceRoleId}'.`);
  if (playbook.roles.some((role) => role.roleId === nextRoleId))
    throw new Error(`Assignment '${nextRoleId}' already exists.`);
  playbook.roles.push({
    ...structuredClone(source),
    roleId: nextRoleId,
    minimum: 0,
    preferred: 0,
  });
  playbook.doctrines.roles[nextRoleId] = structuredClone(doctrine);
}

export function moveAssignment(
  playbook: PlaybookDocument,
  roleId: string,
  delta: -1 | 1,
): void {
  const index = playbook.roles.findIndex((role) => role.roleId === roleId);
  const destination = index + delta;
  if (index < 0 || destination < 0 || destination >= playbook.roles.length) return;
  const [moving] = playbook.roles.splice(index, 1);
  playbook.roles.splice(destination, 0, moving);
}

/** References that must be deliberately redirected before an assignment can
 * disappear. Self-overflow is ignored because it disappears with the role. */
export function assignmentRemovalBlockers(
  playbook: PlaybookDocument,
  roleId: string,
): string[] {
  const blockers: string[] = [];
  for (const role of playbook.roles) {
    if (role.roleId !== roleId && role.overflowRoleId === roleId)
      blockers.push(`${role.roleId} overflows here`);
  }
  for (const [owner, doctrine] of Object.entries(playbook.doctrines.roles)) {
    if (owner === roleId) continue;
    if (doctrine.modes.some((mode) => mode.follow === roleId))
      blockers.push(`${owner} follows this assignment`);
  }
  for (const [conditionId, condition] of Object.entries(playbook.conditions)) {
    if (condition.subject === roleId
        && ([...ROLE_FACTS, ...OPTIONAL_ROLE_FACTS, ...ROLE_ZONE_FACTS] as readonly string[])
          .includes(condition.fact))
      blockers.push(`trigger ${conditionId} observes it`);
  }
  for (const policy of playbook.custodyPolicies) {
    if (policy.emergencyRecoveryRoles?.includes(roleId))
      blockers.push(`${policy.custodyId} authorizes it for emergency recovery`);
    const fetchers = Object.entries(playbook.doctrines.roles)
      .filter(([, doctrine]) => doctrine.modes.some((mode) => mode.fetch === policy.custodyId))
      .map(([owner]) => owner);
    if (fetchers.length === 1 && fetchers[0] === roleId)
      blockers.push(`${policy.custodyId} has no other Fetch assignment`);
  }
  return [...new Set(blockers)];
}

export function removeAssignment(
  playbook: PlaybookDocument,
  roleId: string,
): void {
  if (playbook.roles.length <= 1)
    throw new Error('A sheet needs at least one assignment.');
  const blockers = assignmentRemovalBlockers(playbook, roleId);
  if (blockers.length > 0)
    throw new Error(`Assignment '${roleId}' is still referenced.`);
  playbook.roles = playbook.roles.filter((role) => role.roleId !== roleId);
  delete playbook.doctrines.roles[roleId];
  normalizeSharedCoordination(playbook);
  removeOrphanConditions(playbook);
}

function normalizeSharedCoordination(playbook: PlaybookDocument): void {
  for (const [modeKey, catalogKey] of [
    ['focus', 'focus'],
    ['cohort', 'cohorts'],
  ] as const) {
    const catalog = playbook.doctrines[catalogKey];
    for (const [id, definition] of Object.entries(catalog)) {
      const consumers = Object.values(playbook.doctrines.roles)
        .flatMap((doctrine) => doctrine.modes)
        .filter((mode) => mode[modeKey] === id);
      if (consumers.length === 1)
        consumers[0][modeKey] = structuredClone(definition);
      if (consumers.length < 2) delete catalog[id];
    }
  }
}

function removeOrphanConditions(playbook: PlaybookDocument): void {
  const used = new Set<string>();
  const collect = (expression: string | undefined) => expression
    ?.split(/ (?:and|or) /).forEach((token) => used.add(token));
  for (const doctrine of Object.values(playbook.doctrines.roles)) {
    doctrine.modes.forEach((mode) => {
      collect(mode.while);
      collect(mode.until);
    });
  }
  for (const policy of playbook.custodyPolicies) {
    collect(policy.safeConversion);
    collect(policy.baitDrop?.reclaim);
    collect(policy.emergencyRecovery);
  }
  for (const id of Object.keys(playbook.conditions))
    if (!used.has(id)) delete playbook.conditions[id];
}

export function assignmentIdFromName(value: string): string {
  return value
    .normalize('NFKD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

const ROLE_SCALAR_KEYS = new Set([
  'roleId', 'overflowRoleId', 'follow',
]);
const ROLE_ARRAY_KEYS = new Set([
  'emergencyRecoveryRoles',
]);

function rewriteRoleReferences(
  value: unknown,
  previousId: string,
  nextId: string,
  parentKey = '',
): void {
  if (Array.isArray(value)) {
    if (ROLE_ARRAY_KEYS.has(parentKey)) {
      value.forEach((entry, index) => {
        if (entry === previousId) value[index] = nextId;
        else rewriteRoleReferences(entry, previousId, nextId, parentKey);
      });
      return;
    }
    value.forEach((entry) => rewriteRoleReferences(entry, previousId, nextId));
    return;
  }
  if (!value || typeof value !== 'object') return;
  const record = value as Record<string, unknown>;
  for (const [key, entry] of Object.entries(record)) {
    const roleSubject = key === 'subject'
      && typeof record.fact === 'string'
      && (ROLE_FACTS as readonly string[]).includes(record.fact);
    if ((ROLE_SCALAR_KEYS.has(key) || roleSubject) && entry === previousId) {
      record[key] = nextId;
      continue;
    }
    rewriteRoleReferences(entry, previousId, nextId, key);
  }

}

export function stringifyDocument(value: JsonObject): string {
  return JSON.stringify(value, null, 2);
}

export async function sha256Hex(value: string): Promise<string> {
  const bytes = new TextEncoder().encode(value);
  const digest = await crypto.subtle.digest('SHA-256', bytes);
  return [...new Uint8Array(digest)]
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('');
}

export async function pinDraft(draft: SheetDraft): Promise<{
  draft: SheetDraft;
  playbookJson: string;
  layoutJson: string;
  layoutSha256: string;
}> {
  const layoutJson = stringifyDocument(draft.layout);
  const layoutSha256 = await sha256Hex(layoutJson);
  const next = structuredClone(draft);
  next.playbook.layout = {
    ...next.playbook.layout,
    path: 'layout.json',
    sha256: layoutSha256,
  };
  return {
    draft: next,
    playbookJson: stringifyDocument(next.playbook),
    layoutJson,
    layoutSha256,
  };
}

export function verbOf(mode: TacticalMode): TacticalVerb | null {
  const verbs = TACTICAL_VERBS.filter((verb) => mode[verb] !== undefined);
  return verbs.length === 1 ? verbs[0] : null;
}

export function freshMode(
  verb: TacticalVerb,
  routes: string[],
  anchors: string[],
  zones: string[] = [],
  custodyIds: string[] = [],
  roleIds: string[] = [],
): TacticalMode {
  const focus: FocusDefinition = { maximumAttackers: 2 };
  const firstDestination = routes[0]
    ?? (zones[0] ? { zone: zones[0] } : '');
  switch (verb) {
    case 'patrol': return { patrol: firstDestination || 'traffic', focus };
    case 'intercept': return {
      intercept: 'enemy-carriers',
      from: anchors[0] ?? '',
      focus,
    };
    case 'assault': return {
      assault: firstDestination,
      focus,
      posture: 'front',
    };
    case 'recover': return { recover: 'auto', focus };
    case 'fetch': return { fetch: custodyIds[0] ?? '' };
    case 'follow': return {
      follow: roleIds[0] ?? '',
      posture: 'trail',
      focus,
    };
  }
}

export function destinationOf(mode: TacticalMode): {
  kind: 'route' | 'zone'; id: string;
} | null {
  const value = mode.patrol ?? mode.assault;
  if (typeof value === 'string') return { kind: 'route', id: value };
  if (value && typeof value === 'object' && 'route' in value
      && typeof value.route === 'string') return { kind: 'route', id: value.route };
  if (value && typeof value === 'object' && 'zone' in value
      && typeof value.zone === 'string') return { kind: 'zone', id: value.zone };
  return null;
}

export function validateDraft(
  draft: SheetDraft,
  catalog: TacticalSheetCatalog,
  currentLayoutHash: string | null,
): SheetIssue[] {
  const issues: SheetIssue[] = [];
  const error = (path: string, message: string) =>
    issues.push({ path, message, severity: 'error' });
  const warning = (path: string, message: string) =>
    issues.push({ path, message, severity: 'warning' });
  const { playbook, layout } = draft;

  const playbookKeys = [
    'schema', 'playbookId', 'auditStatus', 'composition', 'layout', 'memory',
    'roles', 'custodyPolicies', 'doctrines', 'conditions',
  ];
  for (const key of playbookKeys) {
    if (!Object.prototype.hasOwnProperty.call(playbook, key))
      error(`playbook.${key}`, `Required playbook key '${key}' is missing.`);
  }
  Object.keys(playbook).filter((key) => !playbookKeys.includes(key)).forEach((key) =>
    error(`playbook.${key}`, `'${key}' belongs to the retired sheet grammar.`));
  for (const key of [
    'schema', 'layoutId', 'mapId', 'bindings', 'zones', 'routes', 'anchors',
  ]) {
    if (!Object.prototype.hasOwnProperty.call(layout, key))
      error(`layout.${key}`, `Required layout key '${key}' is missing.`);
  }

  if (draft.name.trim().length < 1 || draft.name.trim().length > 60)
    error('name', 'Use a sheet name between 1 and 60 characters.');
  if (playbook.schema !== 'arc-relay-tactical-playbook-v1')
    error('playbook.schema', 'Only arc-relay-tactical-playbook-v1 is supported.');
  if (layout.schema !== 'arc-relay-tactical-layout-v1')
    error('layout.schema', 'Only arc-relay-tactical-layout-v1 is supported.');
  if (!IDENTIFIER.test(playbook.playbookId ?? ''))
    error('playbook.playbookId', 'Playbook id must use lowercase letters, digits and hyphens.');
  if (!IDENTIFIER.test(layout.layoutId ?? ''))
    error('layout.layoutId', 'Layout id must use lowercase letters, digits and hyphens.');
  if (layout.mapId !== catalog.map.id)
    error('layout.mapId', `This sheet must target ${catalog.map.id}.`);
  if (playbook.composition?.length !== catalog.slotCount)
    error('composition', `Choose exactly ${catalog.slotCount} classes.`);
  const classIds = new Set(catalog.classes.map((entry) => entry.id));
  const unlocked = new Set(catalog.classes
    .filter((entry) => entry.unlocked).map((entry) => entry.id));
  const copies = new Map<string, number>();
  for (const [index, classId] of (playbook.composition ?? []).entries()) {
    if (!classIds.has(classId)) error(`composition.${index}`, `Unknown class '${classId}'.`);
    else if (!unlocked.has(classId)) error(`composition.${index}`, `${classId} is locked.`);
    copies.set(classId, (copies.get(classId) ?? 0) + 1);
  }
  for (const [classId, count] of copies)
    if (count > catalog.maximumCopiesPerClass)
      error('composition', `${classId} appears ${count} times; the cap is ${catalog.maximumCopiesPerClass}.`);

  const roles = new Set((playbook.roles ?? []).map((role) => role.roleId));
  const custody = new Set((playbook.custodyPolicies ?? []).map((policy) => policy.custodyId));
  const zones = new Set((layout.zones ?? []).map((zone) => zone.zoneId));
  const routes = new Set((layout.routes ?? []).map((route) => route.routeId));
  const anchors = new Set((layout.anchors ?? []).map((anchor) => anchor.anchorId));
  const predicates = playbook.conditions ?? {};
  const predicateIds = new Set(Object.keys(predicates));

  validateIdentifiers('roles', roles, error);
  validateIdentifiers('custodyPolicies', custody, error);
  validateIdentifiers('layout.zones', zones, error);
  validateIdentifiers('layout.routes', routes, error);
  validateIdentifiers('layout.anchors', anchors, error);
  validateIdentifiers('conditions', predicateIds, error);
  duplicates((playbook.roles ?? []).map((role) => role.roleId), 'roles', error);
  duplicates((playbook.custodyPolicies ?? []).map((policy) => policy.custodyId),
    'custodyPolicies', error);
  duplicates((layout.zones ?? []).map((zone) => zone.zoneId), 'layout.zones', error);
  duplicates((layout.routes ?? []).map((route) => route.routeId), 'layout.routes', error);
  duplicates((layout.anchors ?? []).map((anchor) => anchor.anchorId), 'layout.anchors', error);

  for (const [index, role] of (playbook.roles ?? []).entries()) {
    if (!Array.isArray(role.candidateClasses) || role.candidateClasses.length < 1)
      error(`roles.${index}.candidateClasses`, 'An assignment needs at least one eligible class.');
    duplicates(role.candidateClasses ?? [], `roles.${index}.candidateClasses`, error);
    role.candidateClasses?.forEach((classId) => {
      if (!classIds.has(classId))
        error(`roles.${index}.candidateClasses`, `Unknown class '${classId}'.`);
    });
    const minimum = role.minimum;
    const preferred = role.preferred;
    const maximum = role.maximum;
    if ([minimum, preferred, maximum].every((value) => typeof value === 'number')) {
      if (minimum! < 0 || minimum! > preferred! || preferred! > maximum! || maximum! > 8)
        error(`roles.${index}.preferred`, 'Assignment staffing must satisfy 0 ≤ minimum ≤ target ≤ maximum ≤ 8.');
      if (preferred! > 0 && !role.candidateClasses.some((classId) => copies.has(classId)))
        error(`roles.${index}.candidateClasses`, 'No body in the current composition can fill this assignment.');
    }
    if (!['forbid', 'allow', 'prefer', 'require'].includes(role.carrierPreference ?? ''))
      error(`roles.${index}.carrierPreference`, 'Choose forbid, allow, prefer or require.');
    if (!['hold-vacancy', 'promote-best', 'rebalance'].includes(role.deathPolicy ?? ''))
      error(`roles.${index}.deathPolicy`, 'Choose a supported death policy.');
    if (!['resume', 'rejoin', 'rally', 'replace'].includes(role.respawnPolicy ?? ''))
      error(`roles.${index}.respawnPolicy`, 'Choose a supported respawn policy.');
    if (role.overflowRoleId && !roles.has(role.overflowRoleId))
      error(`roles.${index}.overflowRoleId`, `Unknown assignment '${role.overflowRoleId}'.`);
    if (role.build !== undefined) {
      list(role.build, 1, 8, `roles.${index}.build`, error);
      role.build?.forEach((choice) => {
        if (!['damage', 'vision', 'reach', 'vitality'].includes(choice))
          error(`roles.${index}.build`, `Unknown upgrade '${choice}'.`);
      });
    }
  }

  const minimumBodies = (playbook.roles ?? []).reduce((sum, role) => sum + (role.minimum ?? 0), 0);
  const preferredBodies = (playbook.roles ?? [])
    .reduce((sum, role) => sum + (role.preferred ?? 0), 0);
  const maximumBodies = (playbook.roles ?? []).reduce((sum, role) => sum + (role.maximum ?? 0), 0);
  if (minimumBodies > catalog.slotCount)
    error('roles', `Assignment minimums claim ${minimumBodies} bodies; only ${catalog.slotCount} are fielded.`);
  if (preferredBodies > catalog.slotCount)
    error('roles', `Assignment targets claim ${preferredBodies} bodies; only ${catalog.slotCount} are fielded.`);
  if (maximumBodies < catalog.slotCount)
    error('roles', `Assignment maximums cover only ${maximumBodies} of ${catalog.slotCount} bodies.`);

  const doctrineKeys = Object.keys(playbook.doctrines ?? {});
  if (doctrineKeys.length !== 3 || !['roles', 'cohorts', 'focus'].every((key) => doctrineKeys.includes(key)))
    error('doctrines', 'Doctrines contain exactly roles, cohorts and focus.');
  const doctrineRoles = new Set(Object.keys(playbook.doctrines?.roles ?? {}));
  const focusConsumers = new Map<string, number>();
  const cohortConsumers = new Map<string, { count: number; roles: Set<string>; destination: string }>();
  const conditionConsumers = new Map<string, number>();
  const fetchConsumers = new Map<string, number>();

  for (const [roleId, doctrine] of Object.entries(playbook.doctrines?.roles ?? {})) {
    const path = `doctrines.roles.${roleId}`;
    if (!roles.has(roleId)) error(path, `Unknown assignment '${roleId}'.`);
    if (!custody.has(doctrine.custody)) error(`${path}.custody`, `Unknown custody policy '${doctrine.custody}'.`);
    const collection = typeof doctrine.scavenge === 'string'
      ? [doctrine.scavenge] : doctrine.scavenge ?? [];
    if (collection.length > 16) error(`${path}.scavenge`, 'Scavenge accepts at most sixteen zones.');
    collection.forEach((zone) => {
      if (!zones.has(zone)) error(`${path}.scavenge`, `Unknown zone '${zone}'.`);
    });
    validateFight(doctrine.fight, `${path}.fight`, error);
    validateRepair(doctrine.repair, `${path}.repair`, error);
    const modes = doctrine.modes ?? [];
    if (modes.length < 1 || modes.length > 8)
      error(`${path}.modes`, 'A doctrine needs 1–8 ordered modes.');
    modes.forEach((mode, index) => {
      const modePath = `${path}.modes.${index}`;
      const verbs = TACTICAL_VERBS.filter((verb) => mode[verb] !== undefined);
      if (verbs.length !== 1) {
        error(modePath, 'Choose exactly one mode verb.');
        return;
      }
      const verb = verbs[0];
      const floor = index === modes.length - 1;
      if (floor && verb !== 'patrol')
        error(modePath, 'The fallback must be an unconditioned patrol.');
      if (floor && (mode.while || mode.until))
        error(modePath, 'The fallback patrol cannot have while or until conditions.');
      if (!floor && verb !== 'recover' && verb !== 'fetch' && !mode.while)
        error(`${modePath}.while`, 'This mode needs a while condition.');
      if (mode.while && !mode.until)
        warning(`${modePath}.until`, 'Without until, this mode only yields to a stronger mode.');
      if ((verb === 'recover' || verb === 'fetch') && (mode.while || mode.until))
        error(modePath, `${verb} is internally conditioned and cannot use while or until.`);
      if (verb === 'patrol' || verb === 'assault') {
        const destination = destinationOf(mode);
        if (!destination || (destination.kind === 'route' && !routes.has(destination.id))
            || (destination.kind === 'zone' && !zones.has(destination.id)))
          error(`${modePath}.${verb}`, 'Choose a declared route or zone.');
        if (mode.posture && !['front', 'core', 'rear'].includes(mode.posture))
          error(`${modePath}.posture`, 'Choose front, core or rear.');
      }
      if (verb === 'intercept') {
        if (!['enemy-carriers', 'inbound'].includes(mode.intercept!))
          error(`${modePath}.intercept`, 'Intercept must target enemy-carriers or inbound.');
        if (!mode.from || !anchors.has(mode.from))
          error(`${modePath}.from`, `Unknown anchor '${mode.from}'.`);
      }
      if (verb === 'recover' && mode.recover !== 'auto')
        error(`${modePath}.recover`, 'Recover must be auto.');
      if (verb === 'fetch') {
        if (!custody.has(mode.fetch ?? ''))
          error(`${modePath}.fetch`, `Unknown custody policy '${mode.fetch}'.`);
        else fetchConsumers.set(mode.fetch!, (fetchConsumers.get(mode.fetch!) ?? 0) + 1);
      }
      if (verb === 'follow') {
        if (!roles.has(mode.follow ?? '') || mode.follow === roleId)
          error(`${modePath}.follow`, 'Follow another declared assignment.');
        if (!['trail', 'screen'].includes(mode.posture ?? ''))
          error(`${modePath}.posture`, 'Follow posture is trail or screen.');
      }
      validateCondition(mode.while, `${modePath}.while`, predicateIds, conditionConsumers, error);
      validateCondition(mode.until, `${modePath}.until`, predicateIds, conditionConsumers, error);
      if (verb !== 'fetch') {
        if (typeof mode.focus === 'string') {
          if (!playbook.doctrines.focus[mode.focus]) error(`${modePath}.focus`, `Unknown focus '${mode.focus}'.`);
          focusConsumers.set(mode.focus, (focusConsumers.get(mode.focus) ?? 0) + 1);
        } else validateFocus(mode.focus, `${modePath}.focus`, error);
      } else if (mode.focus !== undefined) error(`${modePath}.focus`, 'Fetch has no focus field.');
      if (mode.cohort !== undefined) {
        if (!['patrol', 'assault'].includes(verb) || floor)
          error(`${modePath}.cohort`, 'Cohorts only apply to non-fallback patrol or assault commands.');
        if (typeof mode.cohort === 'string') {
          if (!playbook.doctrines.cohorts[mode.cohort]) error(`${modePath}.cohort`, `Unknown cohort '${mode.cohort}'.`);
          const destination = destinationOf(mode);
          const destinationKey = destination ? `${destination.kind}:${destination.id}` : '';
          const current = cohortConsumers.get(mode.cohort) ?? { count: 0, roles: new Set(), destination: destinationKey };
          current.count++;
          current.roles.add(roleId);
          if (current.destination !== destinationKey)
            error(`${modePath}.cohort`, 'Every shared cohort command must use the same destination.');
          cohortConsumers.set(mode.cohort, current);
        } else validateCohort(mode.cohort, `${modePath}.cohort`, error);
      }
      validateFight(mode.fight, `${modePath}.fight`, error);
    });
    const floor = modes.at(-1);
    if (floor?.patrol === 'traffic' && hasBreakOff(doctrine, modes))
      error(`${path}.modes.${modes.length - 1}.patrol`, 'A traffic floor cannot receive a break-off rally.');
  }

  for (const role of roles) {
    if (!doctrineRoles.has(role))
      error('doctrines.roles', `Assignment '${role}' needs exactly one battle plan.`);
  }
  doctrineRoles.forEach((role) => {
    if (!roles.has(role)) error(`doctrines.roles.${role}`, `No assignment named '${role}' exists.`);
  });

  for (const [id, focus] of Object.entries(playbook.doctrines?.focus ?? {})) {
    validateFocus(focus, `doctrines.focus.${id}`, error);
    if ((focusConsumers.get(id) ?? 0) < 2)
      error(`doctrines.focus.${id}`, 'A shared focus needs at least two command consumers; inline a one-off cap.');
  }
  for (const [id, cohort] of Object.entries(playbook.doctrines?.cohorts ?? {})) {
    validateCohort(cohort, `doctrines.cohorts.${id}`, error);
    const consumers = cohortConsumers.get(id);
    if (!consumers || consumers.count < 2 || consumers.roles.size < 2)
      error(`doctrines.cohorts.${id}`, 'A shared cohort needs commands on at least two assignments.');
  }
  for (const [id, predicate] of Object.entries(predicates))
    validatePredicate(id, predicate, { roles, zones }, error);
  for (const [index, policy] of (playbook.custodyPolicies ?? []).entries())
    validateCustody(policy, index, { roles, zones, routes, anchors, predicateIds, conditionConsumers }, error);
  for (const id of predicateIds) {
    if ((conditionConsumers.get(id) ?? 0) === 0)
      error(`conditions.${id}`, 'Every trigger must be used by a command or Core policy.');
  }
  for (const id of custody) {
    if ((fetchConsumers.get(id) ?? 0) === 0)
      error('custodyPolicies', `Core policy '${id}' needs at least one assignment with a fetch command.`);
  }
  for (const key of ['enemyUnavailableTicks', 'lastSeenEnemyTicks', 'securedCoreTicks', 'objectiveProgressTicks']) {
    const value = playbook.memory?.[key];
    if (typeof value !== 'number') error(`memory.${key}`, 'Choose a memory window from 1 to 600 ticks.');
    else range(value, 1, 600, `memory.${key}`, error);
  }
  validateLayout(layout, catalog, error);

  // The editor owns this internal pin and rewrites it immediately before
  // every save or practice run. A transient hash mismatch is not a player
  // error and must not make a healthy draft look broken while hashing settles.
  void currentLayoutHash;
  return issues;
}

function validateLayout(
  layout: LayoutDocument,
  catalog: TacticalSheetCatalog,
  error: (path: string, message: string) => void,
) {
  if (!Array.isArray(layout.bindings) || layout.bindings.length < 1
      || layout.bindings.length > 16)
    error('layout.bindings', 'Provide 1–16 orientation bindings.');
  if (!Array.isArray(layout.zones)) error('layout.zones', 'Zones must be a list.');
  if (!Array.isArray(layout.routes)) error('layout.routes', 'Routes must be a list.');
  if (!Array.isArray(layout.anchors)) error('layout.anchors', 'Anchors must be a list.');
  layout.zones?.forEach((zone, index) => {
    if (!Array.isArray(zone.rect) || zone.rect.length !== 4) {
      error(`layout.zones.${index}.rect`, 'Plot a rectangle on the map.');
      return;
    }
    const [x0, y0, x1, y1] = zone.rect;
    if (x0 > x1 || y0 > y1 || !inside(x0, y0, catalog) || !inside(x1, y1, catalog))
      error(`layout.zones.${index}.rect`, 'Zone corners must be ordered and inside the map.');
  });
  layout.routes?.forEach((route, index) => {
    if (!route.waypoints?.length)
      error(`layout.routes.${index}.waypoints`, 'Plot at least one waypoint.');
    route.waypoints?.forEach(([x, y]) => {
      if (!inside(x, y, catalog)) error(`layout.routes.${index}.waypoints`, 'Every waypoint must be inside the map.');
    });
    range(route.corridorWidth, 0, 8, `layout.routes.${index}.corridorWidth`, error);
  });
  layout.anchors?.forEach((anchor, index) => {
    if (!inside(anchor.position?.[0], anchor.position?.[1], catalog))
      error(`layout.anchors.${index}.position`, 'Plot the anchor inside the map.');
  });
  for (const side of ['west', 'east']) {
    const count = layout.bindings?.filter((binding) =>
      binding.ownReactorSide === side).length ?? 0;
    if (count !== 1) error('layout.bindings', `Provide exactly one ${side} binding.`);
  }
  layout.bindings?.forEach((binding, index) => {
    if (!binding.matchContractFingerprint)
      error(`layout.bindings.${index}.matchContractFingerprint`, 'Binding fingerprint is required.');
    if (!['west', 'east'].includes(binding.ownReactorSide))
      error(`layout.bindings.${index}.ownReactorSide`, 'Choose west or east.');
    if (!binding.transform)
      error(`layout.bindings.${index}.transform`, 'Binding transform is required.');
    if (!binding.routeAliases || typeof binding.routeAliases !== 'object')
      error(`layout.bindings.${index}.routeAliases`, 'Route aliases are required.');
  });
}

function validatePredicate(
  id: string,
  predicate: Predicate,
  refs: { roles: Set<string>; zones: Set<string> },
  error: (path: string, message: string) => void,
) {
  const path = `conditions.${id}`;
  if (!(CONDITION_FACTS as readonly string[]).includes(predicate.fact))
    error(`${path}.fact`, `Unknown fact '${predicate.fact}'.`);
  if (!(CONDITION_OPERATORS as readonly string[]).includes(predicate.operator))
    error(`${path}.operator`, `Unknown operator '${predicate.operator}'.`);
  range(predicate.value, 0, 100000, `${path}.value`, error);
  if ((ROLE_ZONE_FACTS as readonly string[]).includes(predicate.fact)) {
    if (!predicate.zone || !refs.zones.has(predicate.zone))
      error(`${path}.zone`, 'Choose a declared zone.');
    if (!predicate.subject || !refs.roles.has(predicate.subject))
      error(`${path}.subject`, 'Choose a declared assignment.');
  } else if ((ZONE_FACTS as readonly string[]).includes(predicate.fact)) {
    if (!predicate.zone || !refs.zones.has(predicate.zone))
      error(`${path}.zone`, 'Choose a declared zone.');
  } else if ((ROLE_FACTS as readonly string[]).includes(predicate.fact)) {
    if (!predicate.subject || !refs.roles.has(predicate.subject))
      error(`${path}.subject`, 'Choose a declared assignment.');
  } else if ((OPTIONAL_ROLE_FACTS as readonly string[]).includes(predicate.fact)
      && predicate.subject && !refs.roles.has(predicate.subject)) {
    error(`${path}.subject`, 'Choose a declared assignment or leave it team-wide.');
  } else if ((WELL_FACTS as readonly string[]).includes(predicate.fact)) {
    if (!predicate.subject || !['north', 'centre', 'south', 'own', 'enemy'].includes(predicate.subject))
      error(`${path}.subject`, 'Choose north, centre, south, own or enemy.');
  }
  const permitsFreshness = predicate.fact === 'remembered-enemies-in-zone'
    || predicate.fact === 'secured-cores';
  if (predicate.freshnessTicks !== undefined) {
    if (!permitsFreshness) error(`${path}.freshnessTicks`, 'This fact does not accept freshness.');
    range(predicate.freshnessTicks, 1, 600, `${path}.freshnessTicks`, error);
  }
}

function validateCustody(
  policy: CustodyPolicy,
  index: number,
  refs: {
    roles: Set<string>; zones: Set<string>; routes: Set<string>; anchors: Set<string>;
    predicateIds: Set<string>; conditionConsumers: Map<string, number>;
  },
  error: (path: string, message: string) => void,
) {
  const path = `custodyPolicies.${index}`;
  list(policy.sourceWells, 1, 3, `${path}.sourceWells`, error);
  policy.sourceWells?.forEach((well) => {
    if (!['north', 'centre', 'south'].includes(well)) error(`${path}.sourceWells`, `Unknown Well '${well}'.`);
  });
  range(policy.pickupReservationTicks, 1, 120, `${path}.pickupReservationTicks`, error);
  range(policy.transferTimeoutTicks, 1, 120, `${path}.transferTimeoutTicks`, error);
  range(policy.deliveryTimeoutTicks, 1, 1200, `${path}.deliveryTimeoutTicks`, error);
  if (!['transfer', 'deliver', 'drop-safe'].includes(policy.accidentalPickup))
    error(`${path}.accidentalPickup`, 'Choose transfer, deliver or drop-safe.');
  if (!['same-carrier', 'nearest-authorized', 'guard-until-safe'].includes(policy.dropRecovery))
    error(`${path}.dropRecovery`, 'Choose a supported drop recovery.');
  if (!['hold', 'guard', 'alternate-core', 'regroup'].includes(policy.unreachableFallback))
    error(`${path}.unreachableFallback`, 'Choose a supported unreachable fallback.');
  if (policy.pickupSelection !== undefined
      && !['carrier-preference', 'nearest-authorized'].includes(policy.pickupSelection))
    error(`${path}.pickupSelection`, 'Choose carrier preference or nearest authorized.');
  if (!policy.safeConversion)
    error(`${path}.safeConversion`, 'Choose when a carrier may convert a Core.');
  validateCondition(policy.safeConversion, `${path}.safeConversion`, refs.predicateIds, refs.conditionConsumers, error);
  if (policy.deliveryRoutes !== undefined)
    list(policy.deliveryRoutes, 1, 8, `${path}.deliveryRoutes`, error);
  policy.deliveryRoutes?.forEach((entry) => {
    if (!refs.zones.has(entry.zone)) error(`${path}.deliveryRoutes`, `Unknown zone '${entry.zone}'.`);
    if (!refs.routes.has(entry.route)) error(`${path}.deliveryRoutes`, `Unknown route '${entry.route}'.`);
  });
  if (policy.baitDrop) {
    if (!refs.zones.has(policy.baitDrop.zone))
      error(`${path}.baitDrop.zone`, `Unknown zone '${policy.baitDrop.zone}'.`);
    validateCondition(policy.baitDrop.reclaim, `${path}.baitDrop.reclaim`, refs.predicateIds, refs.conditionConsumers, error);
  }
  if (policy.forwardPass !== undefined
      && !['none', 'relay-catcher'].includes(policy.forwardPass))
    error(`${path}.forwardPass`, 'Choose none or relay-catcher.');
  if (policy.emergencyRecoveryZones !== undefined) {
    policy.emergencyRecoveryZones.forEach((zone) => {
      if (!refs.zones.has(zone)) error(`${path}.emergencyRecoveryZones`, `Unknown zone '${zone}'.`);
    });
    policy.emergencyRecoveryRoles?.forEach((role) => {
      if (!refs.roles.has(role)) error(`${path}.emergencyRecoveryRoles`, `Unknown assignment '${role}'.`);
    });
    validateCondition(policy.emergencyRecovery, `${path}.emergencyRecovery`, refs.predicateIds, refs.conditionConsumers, error);
    if (policy.emergencyDisplacementTarget && !refs.anchors.has(policy.emergencyDisplacementTarget))
      error(`${path}.emergencyDisplacementTarget`, `Unknown anchor '${policy.emergencyDisplacementTarget}'.`);
  }
}

function validateFight(
  fight: FightBlock | undefined,
  path: string,
  error: (path: string, message: string) => void,
) {
  if (!fight) return;
  if (fight.collect !== undefined && !['yield', 'first'].includes(fight.collect))
    error(`${path}.collect`, 'Choose yield or first.');
  if (fight.heal !== undefined && !['yield', 'first'].includes(fight.heal))
    error(`${path}.heal`, 'Choose yield or first.');
  const targets = object(fight.targets);
  range(number(targets.lone), 0, 8, `${path}.targets.lone`, error);
  const prefer = readStringArray(targets.prefer);
  if (prefer.length > 0 && prefer.length > 6)
    error(`${path}.targets.prefer`, 'Choose 1–6 target preferences.');
  duplicates(prefer, `${path}.targets.prefer`, error);
  prefer.forEach((value) => {
    if (!['carrier', 'weakest', 'anchor', 'closest', 'strongest-threat', 'freshest'].includes(value))
      error(`${path}.targets.prefer`, `Unknown target preference '${value}'.`);
  });
  const engage = object(fight.engage);
  range(number(engage.within), 0, 12, `${path}.engage.within`, error);
  range(number(engage.killableTicks), 0, 60, `${path}.engage.killableTicks`, error);
  range(number(engage.positionTicks), 1, 64, `${path}.engage.positionTicks`, error);
  if (engage.from !== undefined && engage.from !== 'behind')
    error(`${path}.engage.from`, 'Engage-from may only be behind.');
  if (engage.else !== undefined && !['strike', 'breakOff'].includes(String(engage.else)))
    error(`${path}.engage.else`, 'Choose strike or breakOff.');
  const chase = object(fight.chase);
  range(number(chase.leash), 0, 16, `${path}.chase.leash`, error);
  range(number(chase.persistTicks), 1, 120, `${path}.chase.persistTicks`, error);
  range(number(chase.executeBelowHealth), 0, 8, `${path}.chase.executeBelowHealth`, error);
  if (chase.onlyCatchable !== undefined && typeof chase.onlyCatchable !== 'boolean')
    error(`${path}.chase.onlyCatchable`, 'Only-catchable must be on or off.');
  const breakOff = object(fight.breakOff);
  range(number(breakOff.threats), 0, 8, `${path}.breakOff.threats`, error);
  range(number(breakOff.health), 0, 8, `${path}.breakOff.health`, error);
  range(number(breakOff.within), 2, 16, `${path}.breakOff.within`, error);
  range(number(breakOff.memoryTicks), 1, 120, `${path}.breakOff.memoryTicks`, error);
  range(number(breakOff.recoverTicks), 4, 120, `${path}.breakOff.recoverTicks`, error);
  if (typeof breakOff.threats === 'number' && breakOff.threats > 0
      && (breakOff.within === undefined || breakOff.memoryTicks === undefined))
    error(`${path}.breakOff`, 'A threat break-off needs both range and memory.');
  const defense = object(fight.defense);
  range(number(defense.radius), 0, 16, `${path}.defense.radius`, error);
  if (defense.return !== undefined && typeof defense.return !== 'boolean')
    error(`${path}.defense.return`, 'Defense return must be on or off.');
}

function validateRepair(
  repair: RepairBlock | undefined,
  path: string,
  error: (path: string, message: string) => void,
) {
  if (!repair) return;
  const prefer = repair.prefer ?? [];
  list(prefer, 1, 6, `${path}.prefer`, error);
  duplicates(prefer, `${path}.prefer`, error);
  prefer.forEach((value) => {
    if (!['weakest', 'carrier', 'medic', 'focus', 'anchor', 'any'].includes(value))
      error(`${path}.prefer`, `Unknown repair preference '${value}'.`);
  });
  range(repair.maximumProvidersPerTarget, 1, 8,
    `${path}.maximumProvidersPerTarget`, error);
  range(repair.minimumProviderSeparation, 0, 8,
    `${path}.minimumProviderSeparation`, error);
  range(repair.reserveHealthPercent, 0, 100,
    `${path}.reserveHealthPercent`, error);
  if (!['evade', 'regroup', 'hold', 'self-defense'].includes(repair.survivalFallback))
    error(`${path}.survivalFallback`, 'Choose evade, regroup, hold or self-defense.');
}

function validateCondition(
  value: string | undefined,
  path: string,
  predicates: Set<string>,
  consumers: Map<string, number>,
  error: (path: string, message: string) => void,
) {
  if (!value) return;
  if (!CONDITION.test(value)) {
    error(path, 'Use predicate names joined by lowercase and/or; parentheses and negation are not supported.');
    return;
  }
  value.split(/ (?:and|or) /).forEach((id) => {
    if (id === 'always' || id === 'no-live-friendlies') return;
    if (!predicates.has(id)) error(path, `Unknown trigger '${id}'.`);
    else consumers.set(id, (consumers.get(id) ?? 0) + 1);
  });
}

function validateFocus(
  focus: FocusDefinition | undefined,
  path: string,
  error: (path: string, message: string) => void,
) {
  if (!focus) {
    error(path, 'Choose how many bodies may focus one target.');
    return;
  }
  range(focus.maximumAttackers, 1, 8, `${path}.maximumAttackers`, error);
}

function validateCohort(
  cohort: CohortDefinition | undefined,
  path: string,
  error: (path: string, message: string) => void,
) {
  if (!cohort) {
    error(path, 'Choose a cohort size.');
    return;
  }
  range(cohort.take, 1, 8, `${path}.take`, error);
  if (cohort.select !== 'nearest') error(`${path}.select`, 'Cohorts select the nearest eligible bodies.');
}

function hasBreakOff(doctrine: Doctrine, modes: TacticalMode[]) {
  if (Object.keys(object(doctrine.fight?.breakOff)).length > 0) return true;
  return modes.some((mode) => Object.keys(object(mode.fight?.breakOff)).length > 0);
}

export function predicateSubjectKind(fact: string): 'zone' | 'role-zone' | 'role' | 'optional-role' | 'well' | null {
  if ((ROLE_ZONE_FACTS as readonly string[]).includes(fact)) return 'role-zone';
  if ((ZONE_FACTS as readonly string[]).includes(fact)) return 'zone';
  if ((ROLE_FACTS as readonly string[]).includes(fact)) return 'role';
  if ((OPTIONAL_ROLE_FACTS as readonly string[]).includes(fact)) return 'optional-role';
  if ((WELL_FACTS as readonly string[]).includes(fact)) return 'well';
  return null;
}

export async function saveLocalDraft(draft: SheetDraft): Promise<void> {
  if (typeof indexedDB === 'undefined') return;
  const db = await openDraftDatabase();
  await new Promise<void>((resolve, reject) => {
    const transaction = db.transaction(DRAFT_STORE, 'readwrite');
    const store = transaction.objectStore(DRAFT_STORE);
    store.put({
      key: `${LOCAL_DRAFT_PREFIX}${draft.localId}`,
      value: draft,
      updatedAt: Date.now(),
    });
    store.put({
      key: ACTIVE_DRAFT_KEY,
      localId: draft.localId,
      updatedAt: Date.now(),
    });
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
  });
  db.close();
}

export async function loadLocalDraft(localId?: string): Promise<SheetDraft | null> {
  if (typeof indexedDB === 'undefined') return null;
  const db = await openDraftDatabase();
  const active = localId ? undefined : await readLocalRecord(db, ACTIVE_DRAFT_KEY);
  // v1 stored the whole draft in the active record. Read it once and let the
  // next autosave migrate it to the multi-draft layout.
  if (!localId && active?.value) {
    db.close();
    return normalizeLocalDraft(active.value);
  }
  const resolvedId = localId ?? active?.localId;
  if (!resolvedId) {
    db.close();
    return null;
  }
  const result = await readLocalRecord(db, `${LOCAL_DRAFT_PREFIX}${resolvedId}`);
  db.close();
  return result?.value ? normalizeLocalDraft(result.value) : null;
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
  return records
    .filter((record) => record.key.startsWith(LOCAL_DRAFT_PREFIX) && record.value)
    .sort((a, b) => b.updatedAt - a.updatedAt)
    .map((record) => normalizeLocalDraft(record.value!));
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

interface LocalDraftRecord {
  key: string;
  value?: SheetDraft;
  localId?: string;
  updatedAt: number;
}

function readLocalRecord(db: IDBDatabase, key: string): Promise<LocalDraftRecord | undefined> {
  return new Promise((resolve, reject) => {
    const request = db.transaction(DRAFT_STORE).objectStore(DRAFT_STORE).get(key);
    request.onsuccess = () => resolve(request.result as LocalDraftRecord | undefined);
    request.onerror = () => reject(request.error);
  });
}

function normalizeLocalDraft(draft: SheetDraft): SheetDraft {
  if (draft.localId) return draft;
  return {
    ...draft,
    localId: draft.sheetId ? `sheet:${draft.sheetId}` : newLocalDraftId(),
  };
}

/** Convert the shared compiler's path-bearing diagnostic into the editor path model. */
export function sheetIssueFromCompilerError(cause: unknown): SheetIssue {
  const source = cause instanceof Error
    ? cause.message
    : 'The server compiler rejected this draft.';
  const match = source.match(
    /(?:^|\/)(playbook|layout)\.json(?:\.([^:]+))?:\s*(.+)$/s,
  ) ?? source.match(
    /^(playbook|layout)(?:\.([^:]+))?:\s*(.+)$/s,
  );
  if (!match) {
    return {
      path: 'server',
      message: `Compiler: ${source}`,
      severity: 'error',
    };
  }
  const [, document, suffix, message] = match;
  return {
    path: document === 'layout'
      ? `layout${suffix ? `.${suffix}` : ''}`
      : (suffix || 'playbook'),
    message: `Compiler: ${message.trim()}`,
    severity: 'error',
  };
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

function inside(x: unknown, y: unknown, catalog: TacticalSheetCatalog) {
  return typeof x === 'number' && typeof y === 'number'
    && Number.isInteger(x) && Number.isInteger(y)
    && x >= 0 && x < catalog.map.width && y >= 0 && y < catalog.map.height;
}

function validateIdentifiers(
  path: string,
  values: Set<string>,
  error: (path: string, message: string) => void,
) {
  values.forEach((value) => {
    if (!IDENTIFIER.test(value)) error(path, `'${value}' is not a lowercase identifier.`);
  });
}

function duplicates(
  values: string[],
  path: string,
  error: (path: string, message: string) => void,
) {
  const seen = new Set<string>();
  values.forEach((value) => {
    if (seen.has(value)) error(path, `Duplicate id '${value}'.`);
    seen.add(value);
  });
}

function range(
  value: number | undefined,
  minimum: number,
  maximum: number,
  path: string,
  error: (path: string, message: string) => void,
) {
  if (value === undefined) return;
  if (!Number.isInteger(value) || value < minimum || value > maximum)
    error(path, `Use an integer from ${minimum} to ${maximum}.`);
}

function list(
  value: unknown[] | undefined,
  minimum: number,
  maximum: number,
  path: string,
  error: (path: string, message: string) => void,
) {
  if (!Array.isArray(value) || value.length < minimum || value.length > maximum)
    error(path, `Choose ${minimum}–${maximum} entries.`);
}

function object(value: unknown): JsonObject {
  return value && typeof value === 'object' && !Array.isArray(value)
    ? value as JsonObject : {};
}

function number(value: unknown): number | undefined {
  return typeof value === 'number' ? value : undefined;
}

function readStringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((entry): entry is string => typeof entry === 'string') : [];
}
