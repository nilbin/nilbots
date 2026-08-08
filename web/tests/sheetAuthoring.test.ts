import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import test from 'node:test';
import type { TacticalSheetCatalog } from '../src/site/api';
import {
  parseDraft,
  pinDraft,
  stringifyDocument,
  validateDraft,
  type LayoutDocument,
  type PlaybookDocument,
} from '../src/site/sheets/sheetAuthoring';

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
