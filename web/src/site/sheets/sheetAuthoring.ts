import type { TacticalSheetCatalog } from '../api';

export type TacticalVerb =
  | 'patrol'
  | 'intercept'
  | 'assault'
  | 'recover'
  | 'muster'
  | 'squad';

export type Point = [number, number];
export type JsonObject = Record<string, unknown>;

export interface TacticalMode extends JsonObject {
  patrol?: string;
  intercept?: string;
  assault?: string;
  recover?: string;
  muster?: string;
  squad?: boolean;
  while?: string;
  until?: string;
  from?: string;
  patienceTicks?: number;
  escort?: unknown;
  fight?: FightBlock;
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
  role: string;
  custody: string;
  conceal?: boolean;
  collect?: string | string[];
  fight?: FightBlock;
  modes: TacticalMode[];
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
}

export interface GroupDefinition extends JsonObject {
  groupId: string;
  roleIds: string[];
}

export interface CustodyPolicy extends JsonObject {
  custodyId: string;
  authorizedCarrierRoles: string[];
  escortGroups: string[];
  sourceWells: string[];
  pickupReservationTicks: number;
  transferTimeoutTicks: number;
  deliveryTimeoutTicks: number;
  accidentalPickup: string;
  dropRecovery: string;
  unreachableFallback: string;
  safeConversionAll?: unknown[];
  deliveryRoutes?: { zone: string; route: string }[];
  baitDrop?: ({
    zone: string;
    reclaimAll?: unknown[];
  } & JsonObject);
  forwardPass?: string;
}

export interface PlaybookDocument extends JsonObject {
  schema: string;
  playbookId: string;
  composition: string[];
  layout: { path: string; sha256: string } & JsonObject;
  roles: RoleDefinition[];
  groups: GroupDefinition[];
  custodyPolicies: CustodyPolicy[];
  doctrines: Record<string, Doctrine>;
  authoring: JsonObject & {
    predicates: Record<string, Predicate>;
    conditionSets?: Record<string, string[][]>;
  };
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
  'patrol', 'intercept', 'assault', 'recover', 'muster', 'squad',
];

export const CONDITION_OPERATORS = [
  'at-least', 'at-most', 'equals', 'less-than', 'greater-than',
] as const;

export const NO_SUBJECT_FACTS = [
  'always', 'tick', 'phase-state-ticks', 'live-friendlies',
  'known-enemies-unavailable', 'visible-enemy-carriers',
  'known-enemy-carriers', 'friendly-carriers', 'secured-cores',
  'visible-loose-cores', 'visible-loose-core-value',
  'outstanding-well-count', 'ticks-without-objective-progress',
  'reactor-integrity', 'reactor-charge', 'custody-state-ticks',
  'own-filled-sockets', 'enemy-filled-sockets',
] as const;

export const ZONE_FACTS = [
  'friendlies-in-zone-count', 'group-in-zone-count',
  'visible-enemies-in-zone', 'remembered-enemies-in-zone',
  'visible-loose-cores-in-zone', 'visible-loose-core-value-in-zone',
] as const;

export const GROUP_FACTS = [
  'group-live-count', 'group-joining-count', 'group-cohesion',
  'group-stuck-ticks', 'formation-established-ticks',
  'group-formation-broken', 'group-max-level',
] as const;

export const ROLE_FACTS = [
  'role-live-count', 'recover-ready-bodies', 'role-health',
] as const;

export const WELL_FACTS = [
  'well-has-outstanding', 'own-socket-filled', 'enemy-socket-filled',
  'well-ticks-until-birth',
] as const;

export const ORDER_FACTS = ['movement-complete'] as const;

export const CONDITION_FACTS = [
  ...NO_SUBJECT_FACTS,
  ...ZONE_FACTS,
  ...GROUP_FACTS,
  ...ROLE_FACTS,
  ...WELL_FACTS,
  ...ORDER_FACTS,
] as const;

const IDENTIFIER = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const CONDITION = /^[a-z0-9-]+(?: (?:and|or) [a-z0-9-]+)*$/;
const DRAFT_DB = 'nilbots-sheet-authoring-v1';
const DRAFT_STORE = 'drafts';
export const ACTIVE_DRAFT_KEY = 'active';
export const UI_STATE_KEY = 'nilbots-sheet-authoring-ui-v1';

export function parseDraft(
  name: string,
  sheetId: string | null,
  revision: number | null,
  playbookJson: string,
  layoutJson: string,
  enterLadder = true,
): SheetDraft {
  const playbook = JSON.parse(playbookJson) as PlaybookDocument;
  const layout = JSON.parse(layoutJson) as LayoutDocument;
  if (!playbook || typeof playbook !== 'object' || Array.isArray(playbook))
    throw new Error('playbook.json must contain one JSON object.');
  if (!layout || typeof layout !== 'object' || Array.isArray(layout))
    throw new Error('layout.json must contain one JSON object.');
  return { name, sheetId, revision, enterLadder, playbook, layout };
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

export function doctrineOrderIds(playbook: PlaybookDocument): string[] {
  const result: string[] = [];
  for (const [doctrineId, doctrine] of Object.entries(playbook.doctrines ?? {})) {
    const counts = new Map<TacticalVerb, number>();
    doctrine.modes.forEach((mode) => {
      const verb = verbOf(mode);
      if (verb) counts.set(verb, (counts.get(verb) ?? 0) + 1);
    });
    const seen = new Map<TacticalVerb, number>();
    doctrine.modes.forEach((mode) => {
      const verb = verbOf(mode);
      if (!verb || verb === 'muster' || verb === 'squad') return;
      const occurrence = (seen.get(verb) ?? 0) + 1;
      seen.set(verb, occurrence);
      result.push((counts.get(verb) ?? 0) > 1
        ? `${doctrineId}-${verb}-${occurrence}`
        : `${doctrineId}-${verb}`);
    });
  }
  return result;
}

export function freshMode(
  verb: TacticalVerb,
  routes: string[],
  anchors: string[],
): TacticalMode {
  switch (verb) {
    case 'patrol': return { patrol: routes[0] ?? 'traffic' };
    case 'intercept': return {
      intercept: 'enemy-carriers',
      from: anchors[0] ?? undefined,
      while: 'always-on',
      until: 'always-off',
    };
    case 'assault': return {
      assault: routes[0] ?? '',
      while: 'always-on',
      until: 'always-off',
    };
    case 'recover': return { recover: 'auto' };
    case 'muster': return { muster: 'escort' };
    case 'squad': return { squad: true };
  }
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

  for (const key of [
    'schema', 'playbookId', 'auditStatus', 'composition', 'layout',
    'perspective', 'memory', 'arbitration', 'roles', 'groups', 'formations',
    'engagements', 'supportPolicies', 'custodyPolicies',
    'coordination', 'doctrines', 'authoring',
  ]) {
    if (!Object.prototype.hasOwnProperty.call(playbook, key))
      error(`playbook.${key}`, `Required playbook key '${key}' is missing.`);
  }
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
  if (playbook.perspective !== 'team-relative')
    error('playbook.perspective', 'Perspective must be team-relative.');
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
  const groups = new Set((playbook.groups ?? []).map((group) => group.groupId));
  const custody = new Set((playbook.custodyPolicies ?? []).map((policy) => policy.custodyId));
  const zones = new Set((layout.zones ?? []).map((zone) => zone.zoneId));
  const routes = new Set((layout.routes ?? []).map((route) => route.routeId));
  const anchors = new Set((layout.anchors ?? []).map((anchor) => anchor.anchorId));
  const orders = new Set(doctrineOrderIds(playbook));
  const predicates = playbook.authoring?.predicates ?? {};
  const predicateIds = new Set(Object.keys(predicates));

  validateIdentifiers('roles', roles, error);
  validateIdentifiers('groups', groups, error);
  validateIdentifiers('custodyPolicies', custody, error);
  validateIdentifiers('layout.zones', zones, error);
  validateIdentifiers('layout.routes', routes, error);
  validateIdentifiers('layout.anchors', anchors, error);
  validateIdentifiers('authoring.predicates', predicateIds, error);
  duplicates((playbook.roles ?? []).map((role) => role.roleId), 'roles', error);
  duplicates((playbook.groups ?? []).map((group) => group.groupId), 'groups', error);
  duplicates((playbook.custodyPolicies ?? []).map((policy) => policy.custodyId),
    'custodyPolicies', error);
  duplicates((layout.zones ?? []).map((zone) => zone.zoneId), 'layout.zones', error);
  duplicates((layout.routes ?? []).map((route) => route.routeId), 'layout.routes', error);
  duplicates((layout.anchors ?? []).map((anchor) => anchor.anchorId), 'layout.anchors', error);

  for (const [index, role] of (playbook.roles ?? []).entries()) {
    if (!Array.isArray(role.candidateClasses) || role.candidateClasses.length < 1)
      error(`roles.${index}.candidateClasses`, 'A role needs at least one candidate class.');
    role.candidateClasses?.forEach((classId) => {
      if (!classIds.has(classId))
        error(`roles.${index}.candidateClasses`, `Unknown class '${classId}'.`);
    });
  }

  const groupCountByRole = new Map<string, number>();
  for (const group of playbook.groups ?? []) {
    duplicates(group.roleIds ?? [], `groups.${group.groupId}.roleIds`, error);
    for (const role of group.roleIds ?? []) {
      if (!roles.has(role)) error(`groups.${group.groupId}`, `Unknown role '${role}'.`);
      groupCountByRole.set(role, (groupCountByRole.get(role) ?? 0) + 1);
    }
  }

  const escortCalls = new Map<string, string[]>();
  const musterRoles = new Set<string>();
  const doctrineRoles = new Set<string>();
  for (const [doctrineId, doctrine] of Object.entries(playbook.doctrines ?? {})) {
    const path = `doctrines.${doctrineId}`;
    if (!IDENTIFIER.test(doctrineId)) error(path, 'Doctrine ids use lowercase letters, digits and hyphens.');
    if (!roles.has(doctrine.role)) error(`${path}.role`, `Unknown role '${doctrine.role}'.`);
    if (doctrineRoles.has(doctrine.role)) error(`${path}.role`, `Role '${doctrine.role}' already has a doctrine.`);
    doctrineRoles.add(doctrine.role);
    if ((groupCountByRole.get(doctrine.role) ?? 0) !== 1)
      error(`${path}.role`, `Role '${doctrine.role}' must belong to exactly one group.`);
    if (!custody.has(doctrine.custody)) error(`${path}.custody`, `Unknown custody policy '${doctrine.custody}'.`);
    const collection = typeof doctrine.collect === 'string'
      ? [doctrine.collect] : doctrine.collect ?? [];
    if (collection.length > 8) error(`${path}.collect`, 'Collect accepts at most eight zones.');
    collection.forEach((zone) => {
      if (!zones.has(zone)) error(`${path}.collect`, `Unknown zone '${zone}'.`);
    });
    validateFight(doctrine.fight, `${path}.fight`, error);
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
      if (floor && verb !== 'patrol' && verb !== 'squad')
        error(modePath, 'The floor must be an unconditioned patrol or squad.');
      if (floor && (mode.while || mode.until))
        error(modePath, 'The floor cannot have while or until conditions.');
      if (!floor && verb !== 'recover' && verb !== 'muster' && !mode.while)
        error(`${modePath}.while`, 'This mode needs a while condition.');
      if (mode.while && !mode.until)
        warning(`${modePath}.until`, 'Without until, this mode only yields to a stronger mode.');
      if ((verb === 'recover' || verb === 'muster') && (mode.while || mode.until))
        error(modePath, `${verb} is internally conditioned and cannot use while or until.`);
      if (verb === 'squad') {
        if (mode.squad !== true) error(`${modePath}.squad`, 'Squad must be literally true.');
        if (!floor) error(modePath, 'Squad may only be the floor.');
      }
      if (verb === 'patrol' && mode.patrol !== 'traffic' && !routes.has(mode.patrol!))
        error(`${modePath}.patrol`, `Unknown route '${mode.patrol}'.`);
      if (verb === 'assault' && !routes.has(mode.assault!))
        error(`${modePath}.assault`, `Unknown route '${mode.assault}'.`);
      if (verb === 'intercept') {
        if (!['enemy-carriers', 'inbound'].includes(mode.intercept!))
          error(`${modePath}.intercept`, 'Intercept must target enemy-carriers or inbound.');
        if (mode.from && !anchors.has(mode.from))
          error(`${modePath}.from`, `Unknown anchor '${mode.from}'.`);
        range(mode.patienceTicks, 2, 120, `${modePath}.patienceTicks`, error);
      }
      if (verb === 'recover' && mode.recover !== 'auto')
        error(`${modePath}.recover`, 'Recover must be auto.');
      if (verb === 'muster') {
        musterRoles.add(doctrine.role);
        if (mode.muster !== 'escort' && !orders.has(mode.muster!))
          error(`${modePath}.muster`, `Unknown order '${mode.muster}'.`);
      }
      if (verb === 'assault' && mode.escort !== undefined) {
        const escorts = normalizeEscorts(mode.escort);
        if (escorts.length < 1 || escorts.length > 8)
          error(`${modePath}.escort`, 'Escort calls need 1–8 roles.');
        const seen = new Set<string>();
        escorts.forEach(({ role, posture }) => {
          if (!roles.has(role)) error(`${modePath}.escort`, `Unknown role '${role}'.`);
          if (role === doctrine.role) error(`${modePath}.escort`, 'A role cannot escort itself.');
          if (seen.has(role)) error(`${modePath}.escort`, `Duplicate escort role '${role}'.`);
          seen.add(role);
          if (posture && !['trail', 'screen'].includes(posture))
            error(`${modePath}.escort`, `Unknown posture '${posture}'.`);
          escortCalls.set(role, [...(escortCalls.get(role) ?? []), modePath]);
        });
      }
      validateCondition(mode.while, `${modePath}.while`, predicateIds, error);
      validateCondition(mode.until, `${modePath}.until`, predicateIds, error);
      validateFight(mode.fight, `${modePath}.fight`, error);
    });
    const floor = modes.at(-1);
    if (floor?.patrol === 'traffic' && hasBreakOff(doctrine, modes))
      error(`${path}.modes.${modes.length - 1}.patrol`, 'A traffic floor cannot receive a break-off rally.');
  }

  for (const role of roles) {
    if ((groupCountByRole.get(role) ?? 0) !== 1)
      error('groups', `Role '${role}' must belong to exactly one group.`);
    if (!doctrineRoles.has(role))
      error('doctrines', `Role '${role}' needs exactly one doctrine.`);
  }

  for (const [role, paths] of escortCalls) {
    if (!musterRoles.has(role))
      paths.forEach((path) => error(`${path}.escort`, `Role '${role}' is not recruitable; add a muster mode to its doctrine.`));
  }
  for (const role of musterRoles)
    if (!escortCalls.has(role)) error(`doctrines`, `Role '${role}' has a muster mode that answers no call.`);

  for (const [id, predicate] of Object.entries(predicates))
    validatePredicate(id, predicate, { roles, groups, zones, orders }, error);
  for (const [index, policy] of (playbook.custodyPolicies ?? []).entries())
    validateCustody(policy, index, { roles, groups, zones, routes, predicateIds }, error);
  validateLayout(layout, catalog, error);

  if (currentLayoutHash && playbook.layout?.sha256 !== currentLayoutHash)
    error('playbook.layout.sha256', 'Layout changed; save or export will repin it automatically.');
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
  refs: { roles: Set<string>; groups: Set<string>; zones: Set<string>; orders: Set<string> },
  error: (path: string, message: string) => void,
) {
  const path = `authoring.predicates.${id}`;
  if (!(CONDITION_FACTS as readonly string[]).includes(predicate.fact))
    error(`${path}.fact`, `Unknown fact '${predicate.fact}'.`);
  if (!(CONDITION_OPERATORS as readonly string[]).includes(predicate.operator))
    error(`${path}.operator`, `Unknown operator '${predicate.operator}'.`);
  range(predicate.value, 0, 100000, `${path}.value`, error);
  if (predicate.fact === 'group-in-zone-count') {
    if (!predicate.zone || !refs.zones.has(predicate.zone))
      error(`${path}.zone`, 'Choose a declared zone.');
    if (!predicate.subject || !refs.groups.has(predicate.subject))
      error(`${path}.subject`, 'Choose a declared group.');
  } else if ((ZONE_FACTS as readonly string[]).includes(predicate.fact)) {
    if (!predicate.zone || !refs.zones.has(predicate.zone))
      error(`${path}.zone`, 'Choose a declared zone.');
  } else if ((GROUP_FACTS as readonly string[]).includes(predicate.fact)) {
    if (!predicate.subject || !refs.groups.has(predicate.subject))
      error(`${path}.subject`, 'Choose a declared group.');
  } else if ((ROLE_FACTS as readonly string[]).includes(predicate.fact)) {
    if (!predicate.subject || !refs.roles.has(predicate.subject))
      error(`${path}.subject`, 'Choose a declared role.');
  } else if ((WELL_FACTS as readonly string[]).includes(predicate.fact)) {
    if (!predicate.subject || !['north', 'centre', 'south'].includes(predicate.subject))
      error(`${path}.subject`, 'Choose north, centre or south.');
  } else if ((ORDER_FACTS as readonly string[]).includes(predicate.fact)) {
    if (!predicate.subject || !refs.orders.has(predicate.subject))
      error(`${path}.subject`, 'Choose a declared order.');
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
    roles: Set<string>; groups: Set<string>; zones: Set<string>;
    routes: Set<string>; predicateIds: Set<string>;
  },
  error: (path: string, message: string) => void,
) {
  const path = `custodyPolicies.${index}`;
  list(policy.authorizedCarrierRoles, 1, 8, `${path}.authorizedCarrierRoles`, error);
  policy.authorizedCarrierRoles?.forEach((role) => {
    if (!refs.roles.has(role)) error(`${path}.authorizedCarrierRoles`, `Unknown role '${role}'.`);
  });
  list(policy.escortGroups, 0, 8, `${path}.escortGroups`, error);
  policy.escortGroups?.forEach((group) => {
    if (!refs.groups.has(group)) error(`${path}.escortGroups`, `Unknown group '${group}'.`);
  });
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
  if (policy.safeConversionAll === undefined)
    error(`${path}.safeConversionAll`, 'Safe conversion needs 1–8 condition groups.');
  validateConditionGroups(policy.safeConversionAll, `${path}.safeConversionAll`, error);
  if (policy.deliveryRoutes !== undefined)
    list(policy.deliveryRoutes, 1, 8, `${path}.deliveryRoutes`, error);
  policy.deliveryRoutes?.forEach((entry) => {
    if (!refs.zones.has(entry.zone)) error(`${path}.deliveryRoutes`, `Unknown zone '${entry.zone}'.`);
    if (!refs.routes.has(entry.route)) error(`${path}.deliveryRoutes`, `Unknown route '${entry.route}'.`);
  });
  if (policy.baitDrop) {
    if (!refs.zones.has(policy.baitDrop.zone))
      error(`${path}.baitDrop.zone`, `Unknown zone '${policy.baitDrop.zone}'.`);
    if (policy.baitDrop.reclaimAll !== undefined)
      validateConditionGroups(policy.baitDrop.reclaimAll, `${path}.baitDrop.reclaimAll`, error);
    else error(`${path}.baitDrop.reclaimAll`, 'Bait drop needs reclaim conditions.');
  }
  if (policy.forwardPass !== undefined
      && !['none', 'relay-catcher'].includes(policy.forwardPass))
    error(`${path}.forwardPass`, 'Choose none or relay-catcher.');
}

function validateConditionGroups(
  value: unknown,
  path: string,
  error: (path: string, message: string) => void,
) {
  if (value === undefined) return;
  if (!Array.isArray(value) || value.length < 1 || value.length > 8) {
    error(path, 'Choose 1–8 condition groups.');
    return;
  }
  value.forEach((group, index) => {
    const all = object(group).all;
    if (!Array.isArray(all) || all.length < 1)
      error(`${path}.${index}`, 'Each condition group needs at least one fact.');
  });
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
  if (prefer.length > 0 && (prefer.length < 1 || prefer.length > 5))
    error(`${path}.targets.prefer`, 'Choose 1–5 target preferences.');
  duplicates(prefer, `${path}.targets.prefer`, error);
  prefer.forEach((value) => {
    if (!['carrier', 'weakest', 'closest', 'strongest-threat', 'freshest'].includes(value))
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
  const breakOff = object(fight.breakOff);
  range(number(breakOff.threats), 0, 8, `${path}.breakOff.threats`, error);
  range(number(breakOff.health), 0, 8, `${path}.breakOff.health`, error);
  range(number(breakOff.within), 2, 16, `${path}.breakOff.within`, error);
  range(number(breakOff.memoryTicks), 1, 120, `${path}.breakOff.memoryTicks`, error);
  range(number(breakOff.recoverTicks), 4, 120, `${path}.breakOff.recoverTicks`, error);
  const defense = object(fight.defense);
  range(number(defense.radius), 0, 16, `${path}.defense.radius`, error);
}

function validateCondition(
  value: string | undefined,
  path: string,
  predicates: Set<string>,
  error: (path: string, message: string) => void,
) {
  if (!value) return;
  if (!CONDITION.test(value)) {
    error(path, 'Use predicate names joined by lowercase and/or; parentheses and negation are not supported.');
    return;
  }
  value.split(/ (?:and|or) /).forEach((id) => {
    if (!predicates.has(id)) error(path, `Unknown predicate '${id}'.`);
  });
}

function hasBreakOff(doctrine: Doctrine, modes: TacticalMode[]) {
  if (Object.keys(object(doctrine.fight?.breakOff)).length > 0) return true;
  return modes.some((mode) => Object.keys(object(mode.fight?.breakOff)).length > 0);
}

export function normalizeEscorts(value: unknown): { role: string; posture?: string }[] {
  const values = Array.isArray(value) ? value : value === undefined ? [] : [value];
  return values.map((entry) => typeof entry === 'string'
    ? { role: entry }
    : { role: readString(entry, 'role'), posture: readOptionalString(entry, 'posture') });
}

export function predicateSubjectKind(fact: string): 'zone' | 'group' | 'group-zone' | 'role' | 'well' | 'order' | null {
  if (fact === 'group-in-zone-count') return 'group-zone';
  if ((ZONE_FACTS as readonly string[]).includes(fact)) return 'zone';
  if ((GROUP_FACTS as readonly string[]).includes(fact)) return 'group';
  if ((ROLE_FACTS as readonly string[]).includes(fact)) return 'role';
  if ((WELL_FACTS as readonly string[]).includes(fact)) return 'well';
  if ((ORDER_FACTS as readonly string[]).includes(fact)) return 'order';
  return null;
}

export async function saveLocalDraft(draft: SheetDraft): Promise<void> {
  if (typeof indexedDB === 'undefined') return;
  const db = await openDraftDatabase();
  await new Promise<void>((resolve, reject) => {
    const transaction = db.transaction(DRAFT_STORE, 'readwrite');
    transaction.objectStore(DRAFT_STORE).put({
      key: ACTIVE_DRAFT_KEY,
      value: draft,
      updatedAt: Date.now(),
    });
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
  });
  db.close();
}

export async function loadLocalDraft(): Promise<SheetDraft | null> {
  if (typeof indexedDB === 'undefined') return null;
  const db = await openDraftDatabase();
  const result = await new Promise<{ value?: SheetDraft } | undefined>((resolve, reject) => {
    const request = db.transaction(DRAFT_STORE).objectStore(DRAFT_STORE).get(ACTIVE_DRAFT_KEY);
    request.onsuccess = () => resolve(request.result as { value?: SheetDraft } | undefined);
    request.onerror = () => reject(request.error);
  });
  db.close();
  return result?.value ?? null;
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

function readString(value: unknown, key: string): string {
  const result = object(value)[key];
  return typeof result === 'string' ? result : '';
}

function readOptionalString(value: unknown, key: string): string | undefined {
  const result = object(value)[key];
  return typeof result === 'string' ? result : undefined;
}

function readStringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((entry): entry is string => typeof entry === 'string') : [];
}
