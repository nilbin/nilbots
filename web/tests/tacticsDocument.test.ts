import assert from 'node:assert/strict';
import test from 'node:test';
import type { TacticalSheetCatalog } from '../src/site/api';
import { instantiateRecipe, recipeById } from '../src/site/tactics/recipes';
import {
  blankDocument, followMembership, freshJob, migrateDocument,
  normalizeDocument, parseDocument,
  signatureMarkGeometry, validateDocument,
  type MembershipPocket, type Tactic, type TacticsDocument,
} from '../src/site/tactics/tacticsDocument';
import { freshGroup } from '../src/site/tactics/recipes';
import { parseTacticAttribution, attributionLabel } from '../src/site/tactics/practice';
import { summarize } from '../src/site/tactics/summary';
import { orderIdLabel } from '../src/site/tactics/practice';
import { reversedWayIn } from '../src/site/tactics/tacticsDocument';

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

test('a rout window pairs with an event row and reads back', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  // "Four destroyed recently AND a Core taken this phase": the durable
  // forms of both sides, so the AND meets across staggered timing.
  tactic.phases[0].continue = [{
    required: { killsWithin: { count: 4, ticks: 40 }, coreAcquired: true },
    to: 'next',
  }];
  document.tactics.push(tactic);
  assert.deepEqual(
    validateDocument(document, catalog).filter((issue) => issue.severity === 'error'), []);
  const sentence = summarize(tactic, (slot) => document.squad[slot]?.name ?? null);
  assert.match(sentence, /we destroyed four enemies within 40 ticks and a Core is taken this phase/);
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

test('a way-in drawn from the post outward is detected and flips', () => {
  // Cinder's real case: the route STARTED at the post and ended north, so
  // the bot marched through mid to stand on the first waypoint.
  assert.deepEqual(
    reversedWayIn([[21, 12], [19, 7]], [21, 12]),
    [[19, 7], [21, 12]]);
  // A route already in travel order is left alone.
  assert.equal(reversedWayIn([[19, 5], [18, 9]], [18, 9]), null);
  // A route near the post at both ends is fine either way.
  assert.equal(reversedWayIn([[20, 12], [22, 12]], [21, 12]), null);

  // Loading a stored document simply heals the backwards route.
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  tactic.phases[0].jobs[tactic.groups[0].id].posts![0].via = [[14, 12], [10, 8]];
  document.tactics.push(tactic);
  const healed = normalizeDocument(JSON.parse(JSON.stringify(document)));
  assert.deepEqual(
    healed.tactics[healed.tactics.length - 1].phases[0]
      .jobs[tactic.groups[0].id].posts![0].via,
    [[10, 8], [14, 12]]);
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

test('order ids resolve to the author’s names for viewer chips', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  document.tactics.push(tactic);
  // One tactic: the phase name leads; invariants name the tactic.
  assert.equal(orderIdLabel('t1-p2-g2-fight', document), 'Close the trap · fight');
  assert.equal(orderIdLabel('t1-g1-grab', document), 'Pincer · grab');
  // Baseline ids stay raw.
  assert.equal(orderIdLabel('hold-ground', document), null);
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

// ——— Group membership follows the bots ————————————————————————————————————

/** The drawn pincer plus the per-bot extras a real sheet accumulates: a way
 * in on the second post and a signature mark owned by that bot's class. */
function splitReady(document: TacticsDocument): Tactic {
  const tactic = drawnPincer(document);
  const guard = tactic.groups[0];
  const job = tactic.phases[0].jobs[guard.id];
  job.posts![1].via = [[20, 20], [16, 14]];
  job.sigs = { longshot: { line: [[15, 13], [15, 12]] } };
  return tactic;
}

function membershipSnapshot(tactic: Tactic) {
  return tactic.groups.map((group) => ({ id: group.id, slots: [...group.slots] }));
}

test('splitting bots out of a Group takes their posts, ways in and marks along', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = splitReady(document);
  document.tactics.push(tactic);
  const guard = tactic.groups[0];
  const before = membershipSnapshot(tactic);
  const fresh = freshGroup(tactic.groups);
  tactic.groups.push(fresh);
  for (const phase of tactic.phases) phase.jobs[fresh.id] = freshJob('none', 'hold');
  guard.slots = [5];
  fresh.slots = [2];
  const summary = followMembership(tactic, document.squad, before);
  assert.ok(summary && /followed/.test(summary));
  // The new Group inherited the source job per phase; the mover's post —
  // way in included — and its class's mark came along.
  const seeded = tactic.phases[0].jobs[fresh.id];
  assert.equal(seeded.kind, 'posts');
  assert.deepEqual(
    seeded.posts!.map((post) => [post.slot, post.at[0], post.at[1]]),
    [[2, 16, 14]]);
  assert.deepEqual(seeded.posts![0].via, [[20, 20], [16, 14]]);
  assert.deepEqual(Object.keys(seeded.sigs ?? {}), ['longshot']);
  // The old Group was cleaned: no post, no mark of the departed bot.
  const kept = tactic.phases[0].jobs[guard.id];
  assert.deepEqual(
    kept.posts!.map((post) => [post.slot, post.at[0], post.at[1]]),
    [[5, 14, 12]]);
  assert.equal(kept.sigs, undefined);
  // Nothing needs redrawing: the split document still validates cleanly.
  assert.deepEqual(
    validateDocument(document, catalog).filter((issue) => issue.severity === 'error'),
    []);
});

test('a two-step move through the tray keeps a bot’s belongings in the pocket', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = splitReady(document);
  document.tactics.push(tactic);
  const guard = tactic.groups[0];
  const pocket: MembershipPocket = new Map();
  // Step one: unassign the posted bot — its post and mark wait in the tray.
  let before = membershipSnapshot(tactic);
  guard.slots = [5];
  let summary = followMembership(tactic, document.squad, before, pocket);
  assert.ok(summary && /tray/.test(summary));
  assert.equal(tactic.phases[0].jobs[guard.id].posts!.length, 1);
  assert.ok(pocket.get(2)?.posts.get(tactic.phases[0].id));
  // Step two: place it into a brand-new Group — the pocket remembers where
  // it came from, so the new Group seeds and the post lands.
  before = membershipSnapshot(tactic);
  const fresh = freshGroup(tactic.groups);
  tactic.groups.push(fresh);
  for (const phase of tactic.phases) phase.jobs[fresh.id] = freshJob('none', 'hold');
  fresh.slots = [2];
  summary = followMembership(tactic, document.squad, before, pocket);
  assert.ok(summary && /followed/.test(summary));
  const seeded = tactic.phases[0].jobs[fresh.id];
  assert.equal(seeded.kind, 'posts');
  assert.deepEqual(
    seeded.posts!.map((post) => [post.slot, post.at[0], post.at[1]]),
    [[2, 16, 14]]);
  assert.deepEqual(Object.keys(seeded.sigs ?? {}), ['longshot']);
  assert.equal(pocket.has(2), false);
});

test('a post is cleaned away — never misassigned — when its bot joins area work', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = splitReady(document);
  document.tactics.push(tactic);
  const guard = tactic.groups[0];
  const flankers = tactic.groups[1];
  const before = membershipSnapshot(tactic);
  // The posted longshot joins the flankers, whose jobs are area/point work.
  guard.slots = [5];
  flankers.slots = [...flankers.slots, 2];
  const summary = followMembership(tactic, document.squad, before);
  assert.ok(summary && /cleaned/.test(summary));
  assert.equal(tactic.phases[0].jobs[guard.id].posts!.length, 1);
  assert.equal(tactic.phases[0].jobs[flankers.id].posts, undefined);
  // The mark still followed — the flankers own the class now.
  assert.deepEqual(
    Object.keys(tactic.phases[0].jobs[flankers.id].sigs ?? {}), ['longshot']);
  assert.equal(tactic.phases[0].jobs[guard.id].sigs, undefined);
});

test('swapping bots across Groups leaves no post behind on the wrong bot', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  document.tactics.push(tactic);
  const guard = tactic.groups[0];
  const flankers = tactic.groups[1];
  const before = membershipSnapshot(tactic);
  // Swap the guard's palisade (slot 5, first post) with a flanker (slot 0).
  guard.slots = [2, 0];
  flankers.slots = [1, 3, 4, 6, 7, 5];
  followMembership(tactic, document.squad, before);
  const posts = tactic.phases[0].jobs[guard.id].posts!;
  // Slot 2 keeps ITS post — the pairing must not shift onto the newcomer.
  assert.deepEqual(
    posts.map((post) => [post.slot, post.at[0], post.at[1]]),
    [[2, 16, 14]]);
  // The newcomer has no post yet, and validation names exactly who.
  const messages = validateDocument(document, catalog).map((issue) => issue.message);
  assert.ok(messages.some((message) =>
    /still needs posts for Bot0 \(1\/2 placed\)/.test(message)));
});

test('normalize stamps positional posts with slots and cleans reshuffle leftovers', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const tactic = drawnPincer(document);
  const guard = tactic.groups[0];
  const job = tactic.phases[0].jobs[guard.id];
  // A leftover post beyond the membership, an unowned mark, and a job for a
  // Group that no longer exists — the shape a pre-slot reshuffle left behind.
  job.posts!.push({ at: [18, 18], facing: 'n' });
  job.sigs = { kestrel: { line: [[10, 10], [10, 11]] } };
  tactic.phases[0].jobs['ghost-group'] = freshJob('area', 'hold');
  document.tactics.push(tactic);
  const healed = normalizeDocument(JSON.parse(JSON.stringify(document)));
  const healedTactic = healed.tactics[0];
  const healedJob = healedTactic.phases[0].jobs[guard.id];
  assert.deepEqual(
    healedJob.posts!.map((post) => [post.slot, post.at[0], post.at[1]]),
    [[5, 14, 12], [2, 16, 14]]);
  assert.equal(healedJob.sigs, undefined);
  assert.equal(healedTactic.phases[0].jobs['ghost-group'], undefined);
});
