import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { gunzipSync } from 'node:zlib';
import { loadReplayObject } from '../src/replayIngress';
import type { TacticalSheetCatalog } from '../src/site/api';
import {
  addAssignment,
  assignmentRemovalBlockers,
  assignmentIdFromName,
  duplicateAssignment,
  moveAssignment,
  parseDraft,
  pinDraft,
  renameAssignment,
  removeAssignment,
  setAssignmentTarget,
  sheetIssueFromCompilerError,
  stringifyDocument,
  validateDraft,
  type LayoutDocument,
  type PlaybookDocument,
} from '../src/site/sheets/sheetAuthoring';
import {
  describeTrialChanges,
  replayUnitTracks,
  snapshotForTrial,
} from '../src/site/sheets/sheetTrials';

const classes = ['kestrel', 'patchbay', 'relay', 'hush'];
const layout: LayoutDocument = {
  schema: 'arc-relay-tactical-layout-v1',
  layoutId: 'test-layout',
  mapId: 'test-map',
  bindings: [
    { matchContractFingerprint: 'any-composition', ownReactorSide: 'west', transform: 'identity', routeAliases: {} },
    { matchContractFingerprint: 'any-composition', ownReactorSide: 'east', transform: 'rotate-180', routeAliases: {} },
  ],
  zones: [{ zoneId: 'field', rect: [0, 0, 4, 4] }],
  routes: [{ routeId: 'floor-route', corridorWidth: 2, waypoints: [[1, 1], [3, 3]] }],
  anchors: [{ anchorId: 'gate', position: [1, 2] }],
};

const playbook: PlaybookDocument = {
  schema: 'arc-relay-tactical-playbook-v1',
  playbookId: 'test-sheet',
  auditStatus: { provisionalEvaluationOnly: false, playerFacingProductSchema: true },
  composition: [...classes, ...classes],
  layout: { path: 'layout.json', sha256: '' },
  memory: {
    enemyUnavailableTicks: 21,
    lastSeenEnemyTicks: 30,
    securedCoreTicks: 40,
    objectiveProgressTicks: 90,
  },
  roles: [{
    roleId: 'line',
    candidateClasses: classes,
    minimum: 0,
    preferred: 8,
    maximum: 8,
    carrierPreference: 'allow',
    deathPolicy: 'hold-vacancy',
    respawnPolicy: 'rejoin',
    overflowRoleId: 'line',
    build: ['damage', 'vision', 'reach', 'vitality'],
  }],
  custodyPolicies: [{
    custodyId: 'line-custody',
    sourceWells: ['north'],
    pickupReservationTicks: 8,
    transferTimeoutTicks: 8,
    deliveryTimeoutTicks: 120,
    accidentalPickup: 'deliver',
    dropRecovery: 'nearest-authorized',
    unreachableFallback: 'regroup',
    safeConversion: 'always',
  }],
  doctrines: {
    roles: {
      line: {
        custody: 'line-custody',
        modes: [
          { fetch: 'line-custody' },
          { patrol: 'floor-route', focus: { maximumAttackers: 8 } },
        ],
        conceal: false,
      },
    },
    cohorts: {},
    focus: {},
  },
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
  classes: classes.map((id) => ({
    id, name: id, signatureName: id, fantasy: id, starter: true, unlocked: true,
  })),
  templatePlaybookJson: '{}',
  templateLayoutJson: '{}',
  stockOpponents: [],
};

test('assignment names stay compiler ids and renames follow every role reference', () => {
  const source = structuredClone(playbook);
  source.roles[0] = {
    ...source.roles[0],
    roleId: 'line',
    minimum: 0,
    preferred: 1,
    maximum: 2,
    overflowRoleId: 'line',
  };
  source.custodyPolicies[0].emergencyRecoveryRoles = ['line'];
  source.doctrines.roles.line.modes = [{
    follow: 'line',
    posture: 'trail',
    focus: { maximumAttackers: 1 },
    while: 'line-hurt',
  }];
  source.conditions['line-hurt'] = {
    fact: 'role-health', operator: 'at-most', value: 2, subject: 'line',
  };

  assert.equal(assignmentIdFromName('  Base Screen  '), 'base-screen');
  renameAssignment(source, 'line', 'base-screen');

  assert.equal(source.roles[0].roleId, 'base-screen');
  assert.equal(source.roles[0].overflowRoleId, 'base-screen');
  assert.deepEqual(source.custodyPolicies[0].emergencyRecoveryRoles, ['base-screen']);
  assert.equal(source.doctrines.roles.line, undefined);
  assert.equal(source.doctrines.roles['base-screen'].modes[0].follow, 'base-screen');
  assert.equal(source.conditions['line-hurt'].subject, 'base-screen');
});

test('assignment staffing targets change only the role-owned staffing band', () => {
  const source = structuredClone(playbook);
  source.roles[0] = {
    ...source.roles[0], minimum: 0, preferred: 1, maximum: 1,
  };

  setAssignmentTarget(source, 'line', 3);

  assert.equal(source.roles[0].preferred, 3);
  assert.equal(source.roles[0].maximum, 3);
  assert.equal('groups' in source, false);
});

test('assignment authoring preserves one doctrine per role and explicit staffing order', () => {
  const source = structuredClone(playbook);
  addAssignment(source, 'scout', 'kestrel', 'floor-route');

  assert.deepEqual(source.roles.map((role) => role.roleId), ['line', 'scout']);
  assert.equal(source.roles[1].preferred, 0);
  assert.equal(source.doctrines.roles.scout.modes.at(-1)?.patrol, 'floor-route');

  duplicateAssignment(source, 'scout', 'screen');
  assert.equal(source.roles[2].preferred, 0);
  assert.notEqual(source.doctrines.roles.scout, source.doctrines.roles.screen);

  moveAssignment(source, 'screen', -1);
  assert.deepEqual(source.roles.map((role) => role.roleId), ['line', 'screen', 'scout']);
});

test('assignment removal reports semantic references and cleans one-consumer coordination', () => {
  const source = structuredClone(playbook);
  addAssignment(source, 'scout', 'kestrel', 'floor-route');
  source.roles[0].overflowRoleId = 'scout';
  assert.deepEqual(assignmentRemovalBlockers(source, 'scout'), ['line overflows here']);

  source.roles[0].overflowRoleId = 'line';
  source.doctrines.focus.shared = { maximumAttackers: 2 };
  source.doctrines.roles.line.modes[1].focus = 'shared';
  source.doctrines.roles.scout.modes[0].focus = 'shared';
  removeAssignment(source, 'scout');

  assert.deepEqual(source.roles.map((role) => role.roleId), ['line']);
  assert.deepEqual(source.doctrines.roles.line.modes[1].focus, { maximumAttackers: 2 });
  assert.equal(source.doctrines.focus.shared, undefined);
});

test('export hashes the exact emitted layout bytes and updates only the layout pin', async () => {
  const draft = parseDraft(
    'Test', null, null,
    stringifyDocument(playbook), stringifyDocument(layout),
  );
  const prepared = await pinDraft(draft);
  const expected = createHash('sha256').update(prepared.layoutJson).digest('hex');

  assert.equal(prepared.layoutSha256, expected);
  assert.equal(prepared.draft.playbook.layout.sha256, expected);
  assert.equal(prepared.draft.playbook.layout.path, 'layout.json');
  assert.deepEqual(prepared.draft.layout, draft.layout);
});

test('guided checks enforce the six-verb ladder and its patrol floor before server compile', async () => {
  const valid = parseDraft(
    'Test', null, null,
    stringifyDocument(playbook), stringifyDocument(layout),
  );
  const prepared = await pinDraft(valid);
  assert.deepEqual(validateDraft(prepared.draft, catalog, prepared.layoutSha256), []);

  const broken = structuredClone(prepared.draft);
  broken.playbook.doctrines.roles.line.modes = [
    {
      assault: 'floor-route',
      patrol: 'floor-route',
      while: 'always',
      focus: { maximumAttackers: 2 },
    },
    {
      follow: 'line',
      posture: 'trail',
      focus: { maximumAttackers: 2 },
    },
  ];
  const messages = validateDraft(broken, catalog, prepared.layoutSha256)
    .map((issue) => issue.message);
  assert.ok(messages.some((message) => message.includes('exactly one mode verb')));
  assert.ok(messages.some((message) => message.includes('fallback must be')));
  assert.ok(messages.every((message) => !message.includes('muster')));
});

test('a traffic floor is rejected when any inherited fight block can break off', async () => {
  const draft = parseDraft(
    'Test', null, null,
    stringifyDocument(playbook), stringifyDocument(layout),
  );
  draft.playbook.doctrines.roles.line.fight = { breakOff: { health: 2 } };
  draft.playbook.doctrines.roles.line.modes = [
    { fetch: 'line-custody' },
    { patrol: 'traffic', focus: { maximumAttackers: 8 } },
  ];
  const prepared = await pinDraft(draft);
  const messages = validateDraft(prepared.draft, catalog, prepared.layoutSha256)
    .map((issue) => issue.message);
  assert.ok(messages.some((message) => message.includes('traffic floor')));
});

test('required root and custody keys fail live validation before save', async () => {
  const draft = parseDraft(
    'Test', null, null,
    stringifyDocument(playbook), stringifyDocument(layout),
  );
  delete draft.playbook.memory;
  delete draft.playbook.custodyPolicies[0].safeConversion;
  const prepared = await pinDraft(draft);
  const messages = validateDraft(prepared.draft, catalog, prepared.layoutSha256)
    .map((issue) => issue.message);
  assert.ok(messages.some((message) => message.includes("Required playbook key 'memory'")));
  assert.ok(messages.some((message) => message.includes('carrier may convert')));
});

test('the player validator accepts the starter and tracked Tapline reference sheets', async () => {
  const root = new URL(
    '../../arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/',
    import.meta.url,
  );
  for (const [name, layoutName] of [
    ['starter-tapline-v1', 'starter-tapline-v1'],
    ['hunter-tapline-v1', 'tapline-base-v1'],
    ['wellwright-tapline-v1', 'tapline-base-v1'],
  ]) {
    const layoutJson = readFileSync(new URL(`layouts/${layoutName}.json`, root), 'utf8');
    const playbookJson = readFileSync(new URL(`playbooks/${name}.json`, root), 'utf8');
    const source = JSON.parse(playbookJson) as PlaybookDocument;
    const classIds = [...new Set([
      ...source.composition,
      ...source.roles.flatMap((role) => role.candidateClasses),
    ])];
    const taplineCatalog: TacticalSheetCatalog = {
      ...catalog,
      map: {
        ...catalog.map,
        id: 'arc-relay-tapline-01',
        width: 31,
        height: 27,
        tileRows: Array.from({ length: 27 }, () => '.'.repeat(31)),
      },
      classes: classIds.map((id) => ({
        id, name: id, signatureName: id, fantasy: id, starter: true, unlocked: true,
      })),
    };
    const draft = parseDraft(name, null, null, playbookJson, layoutJson);
    const prepared = await pinDraft(draft);
    assert.deepEqual(
      validateDraft(prepared.draft, taplineCatalog, prepared.layoutSha256),
      [],
      `${name} should be editable without false client errors`,
    );
  }
});

test('trial summaries name only the authoring areas that changed', () => {
  const draft = parseDraft(
    'Test', null, null,
    stringifyDocument(playbook), stringifyDocument(layout),
  );
  const before = snapshotForTrial(draft);
  const changed = structuredClone(draft);
  changed.playbook.doctrines.roles.line.conceal = true;
  changed.layout.routes[0].waypoints.push([4, 3]);
  const after = snapshotForTrial(changed);

  assert.deepEqual(describeTrialChanges(before, after), ['Commands', 'Map geometry']);
  assert.deepEqual(describeTrialChanges(after, after), ['No tactical changes']);
  assert.deepEqual(describeTrialChanges(undefined, after), ['First recorded trial']);

  const staffing = structuredClone(changed);
  staffing.playbook.roles[0].preferred = 7;
  staffing.playbook.memory.lastSeenEnemyTicks += 1;
  assert.deepEqual(
    describeTrialChanges(after, snapshotForTrial(staffing)),
    ['Assignments', 'Team memory'],
  );
  const legacySnapshot = structuredClone(before) as Partial<typeof before>;
  delete legacySnapshot.assignments;
  delete legacySnapshot.memory;
  assert.deepEqual(describeTrialChanges(legacySnapshot, before), ['No tactical changes']);
});

test('trial map tracks use recorded occupied tiles without inventing interpolation', () => {
  const source = JSON.parse(gunzipSync(readFileSync(new URL(
    '../../arena-bots/arc-relay/stock-mind-v0/preflight/broadcast.json.gz',
    import.meta.url,
  ))).toString('utf8')) as object;
  const replay = loadReplayObject(source).replay;
  const tracks = replayUnitTracks(replay);

  assert.equal(tracks.length, replay.initialWorld?.actors.length);
  for (const track of tracks) {
    assert.ok(track.points.length > 0);
    assert.ok(track.points.every((point, index) => index === 0
      || point.x !== track.points[index - 1].x
      || point.y !== track.points[index - 1].y));
    assert.ok(track.points.every((point) => replay.ticks.some((tick) =>
      tick.after.actors.some((actor) => actor.unitKey === track.key
        && actor.position.x === point.x && actor.position.y === point.y))
      || replay.initialWorld?.actors.some((actor) => actor.unitKey === track.key
        && actor.position.x === point.x && actor.position.y === point.y)));
  }
});

test('shared compiler diagnostics map back onto their authoring field', () => {
  assert.deepEqual(
    sheetIssueFromCompilerError(new Error(
      '/tmp/new-sheet/playbook.json.doctrines.ghost.modes.0.while: unknown predicate.',
    )),
    {
      path: 'doctrines.ghost.modes.0.while',
      message: 'Compiler: unknown predicate.',
      severity: 'error',
    },
  );
  assert.equal(
    sheetIssueFromCompilerError(new Error(
      '/tmp/new-sheet/layout.json.routes.waypoints: point is outside the map.',
    )).path,
    'layout.routes.waypoints',
  );
  assert.equal(
    sheetIssueFromCompilerError(new Error(
      "playbook.layout: required property 'sha256' is missing.",
    )).path,
    'layout',
  );
});
