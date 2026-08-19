import assert from 'node:assert/strict';
import test from 'node:test';
import type { TacticalSheetCatalog } from '../src/site/api';
import { instantiateRecipe, recipeById } from '../src/site/tactics/recipes';
import {
  blankDocument, migrateDocument, normalizeDocument, parseDocument,
  signatureMarkGeometry, validateDocument,
  type Tactic, type TacticsDocument,
} from '../src/site/tactics/tacticsDocument';
import { parseTacticAttribution, attributionLabel } from '../src/site/tactics/practice';
import { summarize } from '../src/site/tactics/summary';

/** A synthetic but shape-complete catalog: an open 31x27 arena with border
 * walls, and the class rows the tactics model actually consults. */
function makeCatalog(): TacticalSheetCatalog {
  const width = 31;
  const height = 27;
  const tileRows = Array.from({ length: height }, (_, y) =>
    Array.from({ length: width }, (_, x) =>
      x === 0 || y === 0 || x === width - 1 || y === height - 1 ? '#' : '.').join(''));
  const authoring = (flags: Partial<{
    exactAnchor: boolean; routeForward: boolean; friendlyRole: boolean;
  }> = {}) => ({
    category: 'strike', argumentKind: 'target', engagementRange: 6,
    cooldownTicks: 40, activationTiming: 'immediate', initialPhase: 'ready',
    maturityPhase: 'ready', cooldownAnchor: 'use', parameters: {},
    allowedSelectors: ['nearest-enemy'],
    allowsFriendlyRole: flags.friendlyRole ?? false,
    allowsMostExperiencedAlly: false,
    allowsExactAnchor: flags.exactAnchor ?? false,
    allowsRouteAheadRole: false,
    allowsRouteForward: flags.routeForward ?? false,
    durableDeployment: false,
    defaultCommitmentBehavior: 'continueNormally',
    preset: {
      id: 'p', title: 'p', description: 'p', targetKind: 'namedSelector',
      namedTarget: 'nearest-enemy', suggestsActivationZone: true,
    },
  });
  const classRow = (id: string, name: string, flags?: Parameters<typeof authoring>[0]) => ({
    id, name, signatureId: id, actionId: id, signatureName: `${name} Signature`,
    fantasy: '', starter: true, unlocked: true, signatureAuthoring: authoring(flags),
  });
  return {
    playlistKey: 'arc-relay-entrants',
    playlistVersionId: '00000000-0000-4000-8000-000000000001',
    map: {
      id: 'arc-relay-tapline-02', version: 1, formatVersion: 1,
      width, height, tileRows, regions: [], spawnAnchors: [], tileTags: [],
    },
    slotCount: 8,
    maximumCopiesPerClass: 2,
    minimumDistinctClasses: 7,
    classes: [
      classRow('kestrel', 'Kestrel', { routeForward: true }),
      classRow('switchback', 'Switchback', { friendlyRole: true }),
      classRow('longshot', 'Longshot'),
      classRow('mason', 'Mason', { exactAnchor: true }),
      classRow('lantern', 'Lantern', { exactAnchor: true }),
      classRow('palisade', 'Palisade'),
      classRow('relay', 'Relay', { friendlyRole: true }),
      classRow('patchbay', 'Patchbay', { friendlyRole: true }),
    ],
    starterSheets: [],
    stockOpponents: [],
  };
}

function makeDocument(catalog: TacticalSheetCatalog): TacticsDocument {
  const document = blankDocument(catalog);
  document.squad = [
    'kestrel', 'switchback', 'longshot', 'mason', 'lantern', 'palisade',
    'relay', 'patchbay',
  ].map((classId, slot) => ({
    slot, class: classId, build: 'balanced' as const, name: `Bot${slot}`,
  }));
  return document;
}

/** The four-phase Pincer with all the geometry a player would draw. */
function drawnPincer(document: TacticsDocument): Tactic {
  const { tactic } = instantiateRecipe(recipeById('pincer'), document);
  const guard = tactic.groups[0];
  const flankers = tactic.groups[1];
  guard.slots = [5, 2];
  flankers.slots = [0, 1, 3, 4, 6, 7];
  tactic.phases[0].jobs[guard.id].posts = [
    { at: [14, 12], facing: 'e' },
    { at: [16, 14], facing: 'n' },
  ];
  tactic.phases[0].jobs[flankers.id].rect = [22, 3, 26, 7];
  tactic.phases[1].jobs[flankers.id].point = [15, 12];
  tactic.phases[2].jobs[flankers.id].rect = [13, 11, 17, 15];
  return tactic;
}

test('an instantiated pincer with drawn geometry validates cleanly', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  document.tactics.push(tactic);
  const issues = validateDocument(document, catalog);
  assert.deepEqual(issues.filter((issue) => issue.severity === 'error'), []);
});

test('the pincer recipe is a four-phase operation with a forward-only spine', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const { tactic } = instantiateRecipe(recipeById('pincer'), document);
  assert.deepEqual(
    tactic.phases.map((phase) => phase.name),
    ['Set the trap', 'Close the trap', 'Exploit the opening', 'Extract the Core']);
  // The release rule demands every Group in place AND a trigger.
  const release = tactic.phases[0].continue![0];
  assert.equal(release.required?.groupsSet, true);
  assert.ok(release.triggers?.enemiesAt);
  assert.equal(release.to, 'next');
  // The final phase finishes on the Core landing either way.
  const finish = tactic.phases[3].continue![0];
  assert.equal(finish.to, 'finish');
  assert.equal(finish.triggers?.coreBanked, true);
  assert.equal(finish.triggers?.coreLost, true);
});

test('forward-only transitions and phase-one geometry are enforced', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  document.tactics.push(tactic);
  // A backward jump is named in player language.
  tactic.phases[2].continue = [{ triggers: { coreAcquired: true }, to: tactic.phases[0].id }];
  let messages = validateDocument(document, catalog).map((issue) => issue.message);
  assert.ok(messages.some((message) => /forward-only/.test(message)));
  tactic.phases[2].continue = [{ triggers: { coreAcquired: true }, to: 'next' }];
  // A middle phase without a continue rule is a dead end.
  delete tactic.phases[1].continue;
  messages = validateDocument(document, catalog).map((issue) => issue.message);
  assert.ok(messages.some((message) => /only the final phase may hold forever/.test(message)));
  tactic.phases[1].continue = [{ triggers: { kills: { count: 2 } }, to: 'next' }];
  // groupsSet is a Required idea — any-one-bot makes no sense.
  tactic.phases[1].continue = [{ triggers: { groupsSet: true }, to: 'next' }];
  messages = validateDocument(document, catalog).map((issue) => issue.message);
  assert.ok(messages.some((message) => /belongs in Required/.test(message)));
});

test('missing geometry and double assignment are named in player language', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const { tactic } = instantiateRecipe(recipeById('pincer'), document);
  tactic.groups[0].slots = [5, 2];
  tactic.groups[1].slots = [0, 1, 3, 4, 6, 5];
  document.tactics.push(tactic);
  const messages = validateDocument(document, catalog).map((issue) => issue.message);
  assert.ok(messages.some((message) => /assigned to two Groups/.test(message)));
  assert.ok(messages.some((message) => /needs one post per bot/.test(message)));
});

test('a post on a wall is caught before the server sees it', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const { tactic } = instantiateRecipe(recipeById('hold-area'), document);
  const holders = tactic.groups[0];
  holders.slots = [0, 1, 2, 3, 4, 5, 6, 7];
  tactic.phases[0].jobs[holders.id].posts = Array.from({ length: 8 }, (_, index) => ({
    at: [index === 0 ? 0 : 3 + index, 5] as [number, number],
    facing: 'e' as const,
  }));
  document.tactics.push(tactic);
  const messages = validateDocument(document, catalog).map((issue) => issue.message);
  assert.ok(messages.some((message) => /post at \(0,5\) stands on a wall/.test(message)));
});

test('a post facing into a wall is named with its fix', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const { tactic } = instantiateRecipe(recipeById('hold-area'), document);
  const holders = tactic.groups[0];
  holders.slots = [0, 1, 2, 3, 4, 5, 6, 7];
  tactic.phases[0].jobs[holders.id].posts = Array.from({ length: 8 }, (_, index) => ({
    at: [1 + index * 3, 5] as [number, number],
    // The first post stands beside the west wall and stares straight into it.
    facing: (index === 0 ? 'w' : 'e') as 'w' | 'e',
  }));
  document.tactics.push(tactic);
  const messages = validateDocument(document, catalog).map((issue) => issue.message);
  assert.ok(messages.some((message) =>
    /post at \(1,5\) faces a wall — turn its arrow/.test(message)));
});

test('an approach route validates on legal kinds and reads back in the summary', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  const guard = tactic.groups[0];
  const flankers = tactic.groups[1];
  tactic.phases[0].jobs[flankers.id].via = [[14, 3], [18, 4]];
  document.tactics.push(tactic);
  assert.deepEqual(
    validateDocument(document, catalog).filter((issue) => issue.severity === 'error'), []);
  const sentence = summarize(tactic, (slot) => document.squad[slot]?.name ?? null);
  assert.match(sentence, /coming in by the drawn route/);

  // A post's own way in validates and reads back like the group route.
  tactic.phases[0].jobs[guard.id].posts![0].via = [[10, 10], [12, 11]];
  assert.deepEqual(
    validateDocument(document, catalog).filter((issue) => issue.severity === 'error'), []);
  assert.match(
    summarize(tactic, (slot) => document.squad[slot]?.name ?? null),
    /posts at Mid, coming in by drawn routes/);
  tactic.phases[0].jobs[guard.id].posts![0].via = [[10, 10]];
  assert.ok(validateDocument(document, catalog).some((issue) =>
    /way in with fewer than two points/.test(issue.message)));
  delete tactic.phases[0].jobs[guard.id].posts![0].via;

  // A one-point route and a route on a no-draw kind are both named.
  tactic.phases[0].jobs[flankers.id].via = [[14, 3]];
  let messages = validateDocument(document, catalog).map((issue) => issue.message);
  assert.ok(messages.some((message) => /route needs two or more points/.test(message)));
  tactic.phases[0].jobs[flankers.id].via = [[14, 3], [18, 4]];
  tactic.phases[1].jobs[guard.id].via = [[14, 3], [18, 4]]; // guard carries on
  messages = validateDocument(document, catalog).map((issue) => issue.message);
  assert.ok(messages.some((message) => /does not take an approach route/.test(message)));
});

test('signature mark geometry derives from descriptor capabilities', () => {
  const catalog = makeCatalog();
  assert.equal(signatureMarkGeometry(catalog, 'mason')?.kind, 'points');
  assert.equal(signatureMarkGeometry(catalog, 'kestrel')?.kind, 'line');
  assert.equal(signatureMarkGeometry(catalog, 'longshot')?.kind, 'line');
  assert.equal(signatureMarkGeometry(catalog, 'palisade'), null);
});

test('core-soon and enemies-down rows validate and read back', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  tactic.start = { mode: 'when', cases: [{ triggers: { enemiesDown: { count: 3 } } }] };
  tactic.phases[0].continue = [{
    required: { groupsSet: true },
    triggers: { coreSoon: { well: 'centre', ticks: 20 } },
    to: 'next',
  }];
  document.tactics.push(tactic);
  assert.deepEqual(
    validateDocument(document, catalog).filter((issue) => issue.severity === 'error'), []);
  const sentence = summarize(tactic, (slot) => document.squad[slot]?.name ?? null);
  assert.match(sentence, /at least three enemies are down/);
  assert.match(sentence, /the Centre Well births its next Core within 20 ticks/);
});

test('two event rows AND together through Required, no trigger needed', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  // "Enemies gather at Mid AND a Core births soon" — one case, both rows in
  // Required, empty Triggered by. Alternatives stay the OR.
  tactic.phases[0].continue = [{
    required: {
      groupsSet: true,
      enemiesAt: { count: 5, zone: { rect: [12, 10, 18, 16] } },
      coreSoon: { well: 'centre', ticks: 20 },
    },
    to: 'next',
  }];
  document.tactics.push(tactic);
  assert.deepEqual(
    validateDocument(document, catalog).filter((issue) => issue.severity === 'error'), []);
  const sentence = summarize(tactic, (slot) => document.squad[slot]?.name ?? null);
  assert.match(sentence,
    /every Group is in position, at least 5 enemies are known at Mid and the Centre Well births its next Core within 20 ticks/);
});

test('healed gate, travel posture and heal-up recovery validate and read back', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  const flankers = tactic.groups[1];
  tactic.phases[0].jobs[flankers.id].travel = 'hold';
  tactic.phases[0].jobs[flankers.id].recovery = 'heal-up';
  tactic.phases[0].continue = [{
    required: { groupsSet: true, healed: { percent: 80 } },
    to: 'next',
  }];
  document.tactics.push(tactic);
  assert.deepEqual(
    validateDocument(document, catalog).filter((issue) => issue.severity === 'error'), []);
  const sentence = summarize(tactic, (slot) => document.squad[slot]?.name ?? null);
  assert.match(sentence, /everyone is healed to 80%/);
  assert.match(sentence, /answering attacks on the way/);
  assert.match(sentence, /topping up fully at the heal tile/);
  // healed is a Required idea — "any one bot is topped up" makes no sense.
  tactic.phases[0].continue = [{ triggers: { healed: { percent: 80 } }, to: 'next' }];
  const messages = validateDocument(document, catalog).map((issue) => issue.message);
  assert.ok(messages.some((message) => /belongs in Required/.test(message)));
});

test('an unless side validates, refuses readiness, and reads back inverted', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  tactic.phases[0].continue = [{
    required: { groupsSet: true },
    unless: { contact: true, coreLoose: { well: 'centre' } },
    to: 'next',
  }];
  document.tactics.push(tactic);
  assert.deepEqual(
    validateDocument(document, catalog).filter((issue) => issue.severity === 'error'), []);
  const sentence = summarize(tactic, (slot) => document.squad[slot]?.name ?? null);
  assert.match(sentence,
    /every Group is in position — unless an enemy makes contact, or a Core is up for grabs at the Centre Well/);
  // Readiness reads one way; unless refuses it.
  tactic.phases[0].continue = [{
    triggers: { contact: true }, unless: { groupsSet: true }, to: 'next',
  }];
  const messages = validateDocument(document, catalog).map((issue) => issue.message);
  assert.ok(messages.some((message) => /reads one way/.test(message)));
});

test('phase custody validates with a drawn way home and reads back', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  tactic.phases[0].custody = { plan: 'keep' };
  tactic.phases[1].custody = { plan: 'escort', via: [[15, 12], [8, 8], [3, 5]] };
  document.tactics.push(tactic);
  assert.deepEqual(
    validateDocument(document, catalog).filter((issue) => issue.severity === 'error'), []);
  const sentence = summarize(tactic, (slot) => document.squad[slot]?.name ?? null);
  assert.match(sentence, /A carrier keeps the Core in hand here\./);
  assert.match(sentence, /A carrier banks under escort by the drawn way home\./);
  // keep holds the Core — a way home makes no sense there.
  tactic.phases[0].custody = { plan: 'keep', via: [[3, 3], [4, 4]] };
  const messages = validateDocument(document, catalog).map((issue) => issue.message);
  assert.ok(messages.some((message) => /no run home to draw/.test(message)));
});

test('current-state rows read in every context and healed inverts under unless', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  tactic.start = {
    mode: 'when',
    cases: [{ required: { contact: true, healed: { percent: 50 } } }],
  };
  tactic.abort = { cases: [{ triggers: { contact: true } }] };
  tactic.phases[0].continue = [{
    required: { groupsSet: true },
    unless: { healed: { percent: 80 } },
    to: 'next',
  }];
  document.tactics.push(tactic);
  assert.deepEqual(
    validateDocument(document, catalog).filter((issue) => issue.severity === 'error'), []);
  const sentence = summarize(tactic, (slot) => document.squad[slot]?.name ?? null);
  assert.match(sentence, /unless everyone is healed to 80%/);
});

test('a well-scoped core claim validates and reads back possessively', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  tactic.phases[2].continue = [{
    required: { coreAcquired: { well: 'enemy' } }, to: 'next',
  }];
  document.tactics.push(tactic);
  assert.deepEqual(
    validateDocument(document, catalog).filter((issue) => issue.severity === 'error'), []);
  const sentence = summarize(tactic, (slot) => document.squad[slot]?.name ?? null);
  assert.match(sentence, /their Well’s Core is taken this phase/);
});

test('travel is refused where the job has no arrival to hand over at', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  const flankers = tactic.groups[1];
  const job = tactic.phases[2].jobs[flankers.id];
  job.kind = 'patrol';
  delete job.rect;
  job.path = [[13, 11], [17, 11], [13, 11]];
  job.travel = 'hold';
  document.tactics.push(tactic);
  const messages = validateDocument(document, catalog).map((issue) => issue.message);
  assert.ok(messages.some((message) => /all march/.test(message)));
});

test('the readback walks a pincer through its named phases', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  const sentence = summarize(tactic, (slot) => document.squad[slot]?.name ?? null);
  assert.match(sentence, /^Two bots hold posts at Mid/);
  assert.match(sentence, /while six (stage|wait)/);
  assert.match(sentence, /every Group is in position/);
  assert.match(sentence, /Close the trap: the six converge on Mid aggressively\./);
  assert.match(sentence, /Extract the Core/);
  assert.match(sentence, /Finish when a Core is banked, or the Core is lost — or the time limit expires\./);
});

test('a v1 document migrates to phases exactly like the server', () => {
  const v1 = {
    schema: 'arc-relay-tactics-v1',
    sheetId: 'old-sheet', mapId: 'arc-relay-tapline-02',
    mapWidth: 31, mapHeight: 27,
    squad: [],
    tactics: [{
      id: 'pincer-1', name: 'Pincer', recipe: 'pincer',
      start: { mode: 'when', conds: { enemiesDown: { count: 2 } } },
      release: { mode: 'ready-when', conds: { contact: true } },
      finish: { outcomes: { coreBanked: true, persistent: true }, maxAttempt: 'standard' },
      delivery: { plan: 'none' },
      groups: [{
        id: 'g1', name: 'Everyone', slots: [0, 1, 2, 3, 4, 5, 6, 7],
        coreRule: 'grab',
        stage: {
          kind: 'area', posture: 'wait', recovery: 'patch',
          rect: [8, 10, 13, 15],
        },
        act: { kind: 'converge', posture: 'aggressive', recovery: 'fight-on', point: [15, 12] },
        sigs: { stage: { mason: { points: [[9, 11]] } } },
      }],
    }],
  };
  const migrated = migrateDocument(v1 as unknown as Record<string, unknown>);
  assert.equal(migrated.schema, 'arc-relay-tactics-v2');
  const tactic = migrated.tactics[0];
  // Start rows became one triggers case; release became phase one's rule.
  assert.deepEqual(tactic.start.cases, [{ triggers: { enemiesDown: { count: 2 } } }]);
  assert.equal(tactic.maxDuration, 'standard');
  assert.equal(tactic.phases.length, 2);
  assert.equal(tactic.phases[0].id, 'stage');
  const release = tactic.phases[0].continue![0];
  assert.equal(release.required?.groupsSet, true);
  assert.equal(release.triggers?.contact, true);
  assert.equal(release.to, 'next');
  // Stage geometry and its marks moved into the phase-one job.
  const stageJob = tactic.phases[0].jobs.g1;
  assert.deepEqual(stageJob.rect, [8, 10, 13, 15]);
  assert.ok(stageJob.sigs?.mason);
  // Finish outcomes became the act phase's finish transition; persistent
  // was dropped from the rows (an empty final continue is the persistent
  // form, and this tactic has a real ending).
  const finish = tactic.phases[1].continue![0];
  assert.equal(finish.to, 'finish');
  assert.equal(finish.triggers?.coreBanked, true);
  assert.ok(!('persistent' in (finish.triggers ?? {})));
  // Groups shrank to membership.
  assert.ok(!('stage' in tactic.groups[0]));
  assert.ok(!('act' in tactic.groups[0]));
  // parseDocument migrates transparently.
  const reparsed = parseDocument(JSON.stringify(v1));
  assert.equal(reparsed.schema, 'arc-relay-tactics-v2');
  // A restored device draft is an in-memory OBJECT, not JSON — it takes the
  // same boundary. This is the exact shape that once opened as a blank page.
  const storedDraft = JSON.parse(JSON.stringify(v1)) as Record<string, unknown>;
  const normalized = normalizeDocument(storedDraft);
  assert.equal(normalized.schema, 'arc-relay-tactics-v2');
  assert.equal(normalized.tactics[0].phases.length, 2);
  assert.throws(() => normalizeDocument({ schema: 'something-else' }));
});

test('practice attribution maps program ids back to Tactic, Group and named phase', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const { tactic } = instantiateRecipe(recipeById('pincer'), document);
  tactic.name = 'Mid Pincer';
  tactic.groups[1].name = 'Flankers';
  document.tactics.push(tactic);
  const acting = parseTacticAttribution(
    'orders:t1/p2!go-p2[c1] vex > t1 > t1-p2 > t1-p2-g2-move:eligibility[…]:march@15,12',
    document);
  assert.ok(acting);
  assert.equal(acting.tacticOrdinal, 1);
  assert.equal(acting.tactic?.name, 'Mid Pincer');
  assert.equal(acting.groupName, 'Flankers');
  assert.equal(acting.phaseOrdinal, 2);
  assert.equal(acting.phaseName, 'Close the trap');
  assert.equal(acting.marker, 'go-p2[c1]');
  assert.equal(attributionLabel(acting), 'Mid Pincer · Flankers · Close the trap');
  const invariant = parseTacticAttribution(
    'orders:t1/p3 vex > t1 > t1-g2-grab:eligibility[…]', document);
  assert.equal(invariant?.kind, 'invariant');
  assert.equal(invariant?.groupName, 'Flankers');
  const baseline = parseTacticAttribution(
    'orders:idle cinder > hold-ground:eligibility[…]', document);
  assert.equal(baseline?.kind, 'baseline');
  assert.equal(attributionLabel(baseline!), 'between tactics');
  // Pre-program replays (the old wrapper shape) still resolve.
  const legacy = parseTacticAttribution(
    'orders:vex > t1 > t1-go > t1-g2-a-move:eligibility[…]', document);
  assert.equal(legacy?.phaseOrdinal, 2);
  assert.equal(legacy?.groupName, 'Flankers');
});
