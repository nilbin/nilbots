import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { gunzipSync } from 'node:zlib';
import { loadReplayObject } from '../src/replayIngress';
import type { TacticalSheetCatalog } from '../src/site/api';
import {
  assignmentIdFromName,
  parseDraft,
  pinDraft,
  renameAssignment,
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
  perspective: 'team-relative',
  memory: {},
  arbitration: {},
  roles: [{ roleId: 'line', candidateClasses: classes }],
  groups: [{ groupId: 'line-group', roleIds: ['line'] }],
  formations: [],
  engagements: [],
  supportPolicies: [],
  coordination: { tasks: [] },
  custodyPolicies: [{
    custodyId: 'line-custody',
    authorizedCarrierRoles: ['line'],
    escortGroups: ['line-group'],
    sourceWells: ['north'],
    pickupReservationTicks: 8,
    transferTimeoutTicks: 8,
    deliveryTimeoutTicks: 120,
    accidentalPickup: 'deliver',
    dropRecovery: 'same-carrier',
    unreachableFallback: 'regroup',
    safeConversionAll: [{ all: [{ fact: 'always', operator: 'equals', value: 1 }] }],
  }],
  doctrines: {
    line: {
      role: 'line',
      custody: 'line-custody',
      modes: [{ patrol: 'floor-route' }],
    },
  },
  authoring: {
    predicates: {
      'always-on': { fact: 'always', operator: 'equals', value: 1 },
      'always-off': { fact: 'always', operator: 'equals', value: 0 },
    },
  },
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
  source.groups[0] = { ...source.groups[0], roleIds: ['line'] };
  source.formations = [{ placements: [{ roleId: 'line' }] }];
  source.engagements = [{ participants: ['line'], commit: { roles: ['line'] } }];
  source.supportPolicies = [{ providers: ['line'] }];
  source.custodyPolicies[0].authorizedCarrierRoles = ['line'];
  source.custodyPolicies[0].emergencyRecoveryRoles = ['line'];
  source.doctrines.line.role = 'line';
  source.doctrines.line.modes = [{
    assault: 'floor-route',
    escort: [{ role: 'line' }],
  }];
  source.authoring.predicates!['line-hurt'] = {
    fact: 'role-health', operator: 'at-most', value: 2, subject: 'line',
  };

  assert.equal(assignmentIdFromName('  Base Screen  '), 'base-screen');
  renameAssignment(source, 'line', 'base-screen');

  assert.equal(source.roles[0].roleId, 'base-screen');
  assert.equal(source.roles[0].overflowRoleId, 'base-screen');
  assert.deepEqual(source.groups[0].roleIds, ['base-screen']);
  assert.equal((source.formations[0] as { placements: { roleId: string }[] }).placements[0].roleId, 'base-screen');
  assert.deepEqual((source.engagements[0] as { participants: string[] }).participants, ['base-screen']);
  assert.deepEqual((source.supportPolicies[0] as { providers: string[] }).providers, ['base-screen']);
  assert.deepEqual(source.custodyPolicies[0].authorizedCarrierRoles, ['base-screen']);
  assert.equal(source.doctrines.line.role, 'base-screen');
  assert.equal((source.doctrines.line.modes[0].escort as { role: string }[])[0].role, 'base-screen');
  assert.equal(source.authoring.predicates!['line-hurt'].subject, 'base-screen');
});

test('assignment staffing targets keep their stable group expressible', () => {
  const source = structuredClone(playbook);
  source.roles[0] = {
    ...source.roles[0], minimum: 0, preferred: 1, maximum: 1,
  };
  source.groups[0] = {
    ...source.groups[0], minimum: 0, preferred: 1, maximum: 1,
  };

  setAssignmentTarget(source, 'line', 3);

  assert.equal(source.roles[0].preferred, 3);
  assert.equal(source.roles[0].maximum, 3);
  assert.equal(source.groups[0].preferred, 3);
  assert.equal(source.groups[0].maximum, 3);
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

test('guided checks enforce verb/floor and muster-call contracts before server compile', async () => {
  const valid = parseDraft(
    'Test', null, null,
    stringifyDocument(playbook), stringifyDocument(layout),
  );
  const prepared = await pinDraft(valid);
  assert.deepEqual(validateDraft(prepared.draft, catalog, prepared.layoutSha256), []);

  const broken = structuredClone(prepared.draft);
  broken.playbook.doctrines.line.modes = [
    { assault: 'floor-route', patrol: 'floor-route', while: 'always-on' },
    { muster: 'escort' },
  ];
  const messages = validateDraft(broken, catalog, prepared.layoutSha256)
    .map((issue) => issue.message);
  assert.ok(messages.some((message) => message.includes('exactly one mode verb')));
  assert.ok(messages.some((message) => message.includes('floor must be')));
  assert.ok(messages.some((message) => message.includes('answers no call')));
});

test('a traffic floor is rejected when any inherited fight block can break off', async () => {
  const draft = parseDraft(
    'Test', null, null,
    stringifyDocument(playbook), stringifyDocument(layout),
  );
  draft.playbook.doctrines.line.fight = { breakOff: { health: 2 } };
  draft.playbook.doctrines.line.modes = [{ patrol: 'traffic' }];
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
  delete draft.playbook.custodyPolicies[0].safeConversionAll;
  const prepared = await pinDraft(draft);
  const messages = validateDraft(prepared.draft, catalog, prepared.layoutSha256)
    .map((issue) => issue.message);
  assert.ok(messages.some((message) => message.includes("Required playbook key 'memory'")));
  assert.ok(messages.some((message) => message.includes('Safe conversion')));
});

test('trial summaries name only the authoring areas that changed', () => {
  const draft = parseDraft(
    'Test', null, null,
    stringifyDocument(playbook), stringifyDocument(layout),
  );
  const before = snapshotForTrial(draft);
  const changed = structuredClone(draft);
  changed.playbook.doctrines.line.conceal = false;
  changed.layout.routes[0].waypoints.push([4, 3]);
  const after = snapshotForTrial(changed);

  assert.deepEqual(describeTrialChanges(before, after), ['Doctrines', 'Map geometry']);
  assert.deepEqual(describeTrialChanges(after, after), ['No tactical changes']);
  assert.deepEqual(describeTrialChanges(undefined, after), ['First recorded trial']);
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
