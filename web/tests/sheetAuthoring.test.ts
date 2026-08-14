import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { gunzipSync } from 'node:zlib';
import { loadReplayObject } from '../src/replayIngress';
import type { TacticalSheetCatalog } from '../src/site/api';
import {
  actionOf,
  compositionOf,
  freshOrder,
  moveIntentKind,
  parseDraft,
  pinDraft,
  renameCondition,
  renameRole,
  sheetIssueFromCompilerError,
  skillForClass,
  stringifyDocument,
  validateDraft,
  type MapPlanDocument,
  type OrdersDocument,
} from '../src/site/sheets/sheetAuthoring';
import {
  describeTrialChanges,
  replayUnitTracks,
  snapshotForTrial,
} from '../src/site/sheets/sheetTrials';

const classIds = ['kestrel', 'patchbay', 'relay', 'hush', 'towline', 'lantern', 'palisade', 'switchback'];
const signatureIdForClass: Record<string, string> = {
  kestrel: 'vector-dash', palisade: 'prism-wall', towline: 'tractor-hook',
  patchbay: 'repair-beam', lantern: 'survey-flare', mortar: 'falling-star',
  minesmith: 'trip-node', hush: 'null-field', relay: 'arc-toss',
  switchback: 'exchange', longshot: 'rail-line', mason: 'hardlight-block',
  sunder: 'target-paint', repulsor: 'kinetic-burst', veil: 'smoke-canister',
  nest: 'sentinel-seed',
};
const testClass = (id: string) => ({
  id,
  name: id,
  signatureId: signatureIdForClass[id]!,
  actionId: signatureIdForClass[id]!,
  signatureName: id,
  fantasy: id,
  starter: true,
  unlocked: true,
});
const mapPlan: MapPlanDocument = {
  schema: 'arc-relay-map-plan-v2',
  mapPlanId: 'test-map-plan',
  mapId: 'test-map',
  zones: {},
  routes: { 'centre-loop': { waypoints: [[1, 1], [3, 3]] } },
  anchors: {},
};
const orders: OrdersDocument = {
  schema: 'arc-relay-orders-v2',
  sheetId: 'test-sheet',
  mapPlan: 'map-plan.json',
  roles: [
    {
      id: 'runners',
      classes: classIds.slice(0, 4),
      upgradePath: ['reach', 'vision'],
      orders: [{ id: 'run-centre', move: { patrol: 'centre-loop', posture: 'front' } }],
    },
    {
      id: 'support',
      classes: classIds.slice(4),
      upgradePath: ['vitality', 'damage'],
      orders: [{ id: 'hold-centre', move: { patrol: 'centre-loop', posture: 'rear' } }],
    },
  ],
  sharedOrders: {},
  capacities: {},
  conditions: {},
};
const catalog: TacticalSheetCatalog = {
  playlistKey: 'arc-relay',
  playlistVersionId: '10000000-0000-0000-0000-000000000001',
  map: {
    id: 'test-map', version: 1, formatVersion: 1, width: 5, height: 5,
    tileRows: ['.....', '.....', '.....', '.....', '.....'],
    regions: [], spawnAnchors: [], tileTags: [],
  },
  slotCount: 8,
  maximumCopiesPerClass: 2,
  minimumDistinctClasses: 7,
  classes: classIds.map(testClass),
  templateOrdersJson: '{}', templateMapPlanJson: '{}', stockOpponents: [],
};

test('roles are the one source of composition and role renames follow semantic references', () => {
  const source = structuredClone(orders);
  source.conditions['support-hurt'] = {
    fact: 'role-lowest-health-percent', operator: 'at-most', value: 35, role: 'support',
  };
  source.roles[0].orders.unshift({
    id: 'follow-support', when: 'support-hurt', move: { follow: 'support', posture: 'rear' },
  });
  source.roles[0].orders.unshift({
    id: 'pass-support', when: 'support-hurt', handoff: { target: { role: 'support' } },
  });
  source.roles[0].orders.unshift({
    id: 'repair-support', when: 'support-hurt',
    use: { skill: 'repair-beam', target: { role: 'support' } },
  });

  renameRole(source, 'support', 'field-support');

  assert.deepEqual(compositionOf(source), classIds);
  assert.equal(source.roles[1].id, 'field-support');
  assert.equal(source.conditions['support-hurt'].role, 'field-support');
  assert.deepEqual(source.roles[0].orders[0].use?.target, { role: 'field-support' });
  assert.equal(source.roles[0].orders[1].handoff?.target && typeof source.roles[0].orders[1].handoff?.target === 'object'
    ? source.roles[0].orders[1].handoff?.target.role : '', 'field-support');
  assert.equal(source.roles[0].orders[2].move?.follow, 'field-support');
});

test('condition renames preserve bare shared references while rewriting definitions', () => {
  const source = structuredClone(orders);
  source.conditions['needs-help'] = {
    fact: 'self-health-percent', operator: 'at-most', value: 35,
  };
  source.sharedOrders['recover-health'] = {
    when: 'needs-help',
    move: { to: 'nearest-heal-zone', posture: 'rear' },
  };
  source.roles[0].orders.unshift({ shared: 'recover-health' });
  source.roles[1].orders.unshift({ shared: 'recover-health' });

  source.conditions.hurt = source.conditions['needs-help'];
  delete source.conditions['needs-help'];
  renameCondition(source, 'needs-help', 'hurt');

  assert.deepEqual(source.roles[0].orders[0], { shared: 'recover-health' });
  assert.deepEqual(source.roles[1].orders[0], { shared: 'recover-health' });
  assert.equal(source.sharedOrders['recover-health'].when, 'hurt');
  const draft = parseDraft('Test', null, null, stringifyDocument(source), stringifyDocument(mapPlan));
  assert.deepEqual(validateDraft(draft, catalog), []);
});

test('signature ownership comes from the catalog contract', () => {
  assert.equal(skillForClass(catalog, 'kestrel'), 'vector-dash');
  assert.equal(skillForClass(catalog, 'unknown'), undefined);
});

test('new signature orders persist the selected role skill and its legal default target', () => {
  assert.deepEqual(freshOrder('use', 'pass-core', 'arc-toss'), {
    id: 'pass-core',
    use: { skill: 'arc-toss', target: 'nearest-ally-toward-own-reactor' },
  });
  assert.deepEqual(freshOrder('use', 'repair', 'repair-beam'), {
    id: 'repair',
    use: { skill: 'repair-beam', target: 'weakest-ally' },
  });
});

test('the editor understands every Tapline 4 tactical word', () => {
  const draft = parseDraft('Depth pack', null, null, stringifyDocument(orders), stringifyDocument(mapPlan));
  draft.orders.conditions['ace-live'] = {
    fact: 'role-highest-level', operator: 'at-least', value: 2, role: 'runners',
  };
  draft.orders.conditions['wave-near'] = {
    fact: 'ticks-until-respawn-wave', operator: 'at-most', value: 4,
  };
  draft.orders.conditions['valuable-cargo'] = {
    fact: 'self-carried-core-bank-value', operator: 'at-least', value: 4,
  };
  draft.orders.conditions['barrier-hurt'] = {
    fact: 'deployment-durability-percent', operator: 'at-most', value: 34,
  };
  draft.orders.roles[0].orders.unshift(
    {
      id: 'clear-veteran-or-deployment', when: 'barrier-hurt',
      fight: { targets: ['veteran', 'deployment', 'nearest'], style: 'advance', chaseLimit: 5 },
    },
    {
      id: 'take-high-value-core', when: 'valuable-cargo',
      move: { to: 'highest-bank-value-known-loose-core', posture: 'front' },
    },
    {
      id: 'rally-on-ace', when: 'ace-live',
      move: { follow: { selector: 'most-experienced-ally' }, posture: 'rear' },
    },
  );
  draft.orders.roles[0].orders.unshift({
    id: 'repair-ace', when: 'wave-near',
    use: { skill: 'repair-beam', target: { selector: 'most-experienced-ally' } },
  });

  const errors = validateDraft(draft, catalog).filter((issue) => issue.severity === 'error');
  assert.deepEqual(errors, []);
});

test('order action detection has only the five physical verbs plus branch and exact sharing', () => {
  assert.equal(actionOf({ id: 'move', move: { to: 'own-reactor' } }), 'move');
  assert.equal(actionOf({ id: 'fight', fight: { targets: ['carrier'], style: 'hold', chaseLimit: 0 } }), 'fight');
  assert.equal(actionOf({ id: 'play', orders: [{ id: 'drop', drop: true }] }), 'branch');
  assert.equal(actionOf({ shared: 'score-a-core' }), 'shared');
  assert.equal(actionOf({ id: 'ambiguous', drop: true, move: { to: 'own-reactor' } }), 'move');
});

test('incomplete patrol and follow choices retain their authored movement intent', () => {
  assert.equal(moveIntentKind({ to: { well: 'centre' } }), 'to');
  assert.equal(moveIntentKind({ patrol: '' }), 'patrol');
  assert.equal(moveIntentKind({ follow: '' }), 'follow');
  assert.equal(moveIntentKind({}), 'to');
});

test('export emits the exact authoring pair without authored hashes or geometry bindings', async () => {
  const draft = parseDraft('Test', null, null, stringifyDocument(orders), stringifyDocument(mapPlan));
  const prepared = await pinDraft(draft);
  const parsedOrders = JSON.parse(prepared.ordersJson) as OrdersDocument;
  const parsedPlan = JSON.parse(prepared.mapPlanJson) as MapPlanDocument;

  assert.equal(parsedOrders.mapPlan, 'map-plan.json');
  assert.equal(parsedOrders.sheetId, 'test');
  assert.equal(parsedPlan.mapPlanId, 'test-map');
  assert.doesNotMatch(prepared.ordersJson, /sha256|binding|fingerprint/i);
  assert.doesNotMatch(prepared.mapPlanJson, /sha256|binding|fingerprint/i);
});

test('guided validation mirrors exact roots, class cap, condition use, shared use and map consumers', () => {
  const valid = parseDraft('Test', null, null, stringifyDocument(orders), stringifyDocument(mapPlan));
  assert.deepEqual(validateDraft(valid, catalog), []);

  const broken = structuredClone(valid);
  (broken.orders as OrdersDocument & { custodyPolicies?: unknown[] }).custodyPolicies = [];
  broken.orders.roles[0].classes = ['kestrel', 'kestrel', 'kestrel', 'patchbay'];
  broken.orders.conditions.unused = { fact: 'self-carrying', operator: 'equals', value: 1 };
  broken.orders.sharedOrders.once = { move: { to: 'own-reactor' } };
  broken.orders.roles[0].orders.unshift({ shared: 'once' });
  broken.mapPlan.zones.unused = { rect: [0, 0, 1, 1] };
  const messages = validateDraft(broken, catalog).map((issue) => issue.message);

  assert.ok(messages.some((message) => message.includes("Retired or unknown field 'custodyPolicies'")));
  assert.ok(messages.some((message) => message.includes('copy cap')));
  assert.ok(messages.some((message) => message.includes('at least 7 distinct classes')));
  assert.ok(messages.some((message) => message.includes('unused condition')));
  assert.ok(messages.some((message) => message.includes('at least two references')));
  assert.ok(messages.some((message) => message.includes('unused zone')));
});

test('guided validation rejects route waypoints on walls before save', () => {
  const wallCatalog = structuredClone(catalog);
  wallCatalog.map.tileRows[0] = '#....';
  const draft = parseDraft('Test', null, null, stringifyDocument(orders), stringifyDocument(mapPlan));
  draft.mapPlan.routes['centre-loop'].waypoints[0] = [0, 0];

  const issue = validateDraft(draft, wallCatalog).find((candidate) =>
    candidate.path === 'mapPlan.routes.centre-loop.waypoints.0');

  assert.match(issue?.message ?? '', /off the wall/i);
});

test('guided validation rejects route waypoints on permanently reserved body spawns', () => {
  const spawnCatalog = structuredClone(catalog);
  spawnCatalog.map.spawnAnchors = [{
    id: 'team-0-unit-0', position: { x: 1, y: 1 }, facing: 'East',
  }];
  const draft = parseDraft('Test', null, null, stringifyDocument(orders), stringifyDocument(mapPlan));

  const issue = validateDraft(draft, spawnCatalog).find((candidate) =>
    candidate.path === 'mapPlan.routes.centre-loop.waypoints.0');

  assert.match(issue?.message ?? '', /reserved body spawn/i);
});

test('movement urgency is a three-word optional node property', () => {
  const valid = parseDraft('Test', null, null, stringifyDocument(orders), stringifyDocument(mapPlan));
  valid.orders.roles[0].orders[0].urgency = 'urgent';
  assert.deepEqual(validateDraft(valid, catalog), []);

  (valid.orders.roles[0].orders[0] as { urgency?: string }).urgency = 'critical';
  assert.ok(validateDraft(valid, catalog).some((issue) => issue.path.endsWith('.urgency')));
});

test('mixed-class roles may branch by owned skill and self-in-zone geography', () => {
  const draft = parseDraft('Test', null, null, stringifyDocument(orders), stringifyDocument(mapPlan));
  draft.mapPlan.zones['north-choke'] = { rect: [1, 0, 3, 2] };
  draft.orders.conditions['at-north-choke'] = {
    fact: 'self-in-zone', operator: 'equals', value: 1, zone: 'north-choke',
  };
  draft.orders.roles[0].orders.unshift({
    id: 'dash-at-choke',
    when: 'at-north-choke',
    use: { skill: 'vector-dash', target: 'nearest-enemy' },
  });

  const issues = validateDraft(draft, catalog);
  assert.deepEqual(issues.filter((issue) => issue.severity === 'error'), []);

  draft.orders.roles[0].orders[0].use = {
    skill: 'prism-wall', target: 'nearest-enemy',
  };
  assert.ok(validateDraft(draft, catalog).some((issue) =>
    issue.message.includes('At least one body in this role must own')));
});

test('the player validator accepts every tracked Orders v2 Tapline sheet', async () => {
  const root = new URL('../../arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/', import.meta.url);
  for (const name of [
    'starter-tapline-v2',
    'tempered-standard-v1',
    'relay-circuit-v1',
    'crowned-rail-v1',
    'lockbox-v1',
    'demolition-march-v1',
    'rising-standard-v1',
    'wavebreak-v1',
  ]) {
    const playbookJson = readFileSync(new URL(`playbooks/${name}.json`, root), 'utf8');
    const source = JSON.parse(playbookJson) as OrdersDocument;
    const mapPlanJson = readFileSync(new URL(`map-plans/${name}.json`, root), 'utf8');
    const ids = [...new Set(compositionOf(source))];
    const taplineCatalog: TacticalSheetCatalog = {
      ...catalog,
      map: { ...catalog.map, id: 'arc-relay-tapline-01', width: 31, height: 27, tileRows: Array.from({ length: 27 }, () => '.'.repeat(31)) },
      classes: ids.map(testClass),
    };
    const draft = parseDraft(name, null, null, playbookJson, mapPlanJson);
    const errors = validateDraft((await pinDraft(draft)).draft, taplineCatalog)
      .filter((issue) => issue.severity === 'error');
    assert.deepEqual(errors, [], `${name} should open without client-side false errors`);
  }
});

test('trial summaries name only the Orders v2 authoring areas that changed', () => {
  const draft = parseDraft('Test', null, null, stringifyDocument(orders), stringifyDocument(mapPlan));
  const before = snapshotForTrial(draft);
  const changed = structuredClone(draft);
  changed.orders.roles[0].orders[0].move!.posture = 'rear';
  changed.mapPlan.routes['centre-loop'].waypoints.push([4, 3]);
  const after = snapshotForTrial(changed);

  assert.deepEqual(describeTrialChanges(before, after), ['Orders', 'Map geometry']);
  assert.deepEqual(describeTrialChanges(after, after), ['No tactical changes']);
  assert.deepEqual(describeTrialChanges(undefined, after), ['First recorded trial']);

  const roster = structuredClone(changed);
  roster.orders.roles[0].classes.reverse();
  roster.orders.capacities['two-per-target'] = 2;
  assert.deepEqual(describeTrialChanges(after, snapshotForTrial(roster)), ['Bodies', 'Capacity rules']);
});

test('trial tracks contain only authoritative occupied tiles', () => {
  const source = JSON.parse(gunzipSync(readFileSync(new URL(
    '../../arena-bots/arc-relay/stock-mind-v0/preflight/broadcast.json.gz', import.meta.url,
  ))).toString('utf8')) as object;
  const replay = loadReplayObject(source).replay;
  const tracks = replayUnitTracks(replay);

  assert.equal(tracks.length, replay.initialWorld?.actors.length);
  for (const track of tracks) {
    assert.ok(track.points.length > 0);
    assert.ok(track.points.every((point, index) => index === 0
      || point.x !== track.points[index - 1].x || point.y !== track.points[index - 1].y));
  }
});

test('compiler diagnostics map sheet and map-plan paths back onto the editor', () => {
  assert.deepEqual(sheetIssueFromCompilerError(new Error(
    '/tmp/new-sheet/orders.json.roles.runners.orders[0].when: unknown condition.',
  )), {
    path: 'orders.roles.runners.orders[0].when',
    message: 'Compiler: unknown condition.', severity: 'error',
  });
  assert.equal(sheetIssueFromCompilerError(new Error(
    '/tmp/new-sheet/map-plan.json.routes.home.waypoints: point is outside the map.',
  )).path, 'mapPlan.routes.home.waypoints');
});
