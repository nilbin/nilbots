import assert from 'node:assert/strict';
import test from 'node:test';
import type { TacticalSheetCatalog } from '../src/site/api';
import { instantiateRecipe, recipeById } from '../src/site/tactics/recipes';
import {
  blankDocument, signatureMarkGeometry, validateDocument,
  type TacticsDocument,
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

test('an instantiated pincer with drawn geometry validates cleanly', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const { tactic } = instantiateRecipe(recipeById('pincer'), document);
  tactic.groups[0].slots = [5, 2];
  tactic.groups[0].stage.posts = [
    { at: [14, 12], facing: 'e' },
    { at: [16, 14], facing: 'n' },
  ];
  tactic.groups[1].slots = [0, 1, 3, 4, 6, 7];
  tactic.groups[1].stage.rect = [22, 3, 26, 7];
  tactic.groups[1].act.point = [15, 12];
  document.tactics.push(tactic);
  const issues = validateDocument(document, catalog);
  assert.deepEqual(issues.filter((issue) => issue.severity === 'error'), []);
});

test('missing geometry, double assignment and empty finish are named in player language', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const { tactic } = instantiateRecipe(recipeById('pincer'), document);
  tactic.groups[0].slots = [5, 2];
  tactic.groups[1].slots = [0, 1, 3, 4, 6, 5];
  tactic.finish = { outcomes: {}, maxAttempt: 'none' };
  document.tactics.push(tactic);
  const messages = validateDocument(document, catalog).map((issue) => issue.message);
  assert.ok(messages.some((message) => /assigned to two Groups/.test(message)));
  assert.ok(messages.some((message) => /needs one post per bot/.test(message)));
  assert.ok(messages.some((message) => /Pick at least one outcome/.test(message)));
});

test('a post on a wall is caught before the server sees it', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const { tactic } = instantiateRecipe(recipeById('hold-area'), document);
  tactic.groups[0].slots = [0, 1, 2, 3, 4, 5, 6, 7];
  tactic.groups[0].stage.posts = Array.from({ length: 8 }, (_, index) => ({
    at: [index === 0 ? 0 : 3 + index, 5] as [number, number],
    facing: 'e' as const,
  }));
  document.tactics.push(tactic);
  const messages = validateDocument(document, catalog).map((issue) => issue.message);
  assert.ok(messages.some((message) => /open floor/.test(message)));
});

test('an approach route validates on legal kinds and reads back in the summary', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const { tactic } = instantiateRecipe(recipeById('pincer'), document);
  tactic.groups[0].slots = [5, 2];
  tactic.groups[0].stage.posts = [
    { at: [14, 12], facing: 'e' },
    { at: [16, 14], facing: 'n' },
  ];
  tactic.groups[1].slots = [0, 1, 3, 4, 6, 7];
  tactic.groups[1].stage.rect = [22, 3, 26, 7];
  tactic.groups[1].stage.via = [[14, 3], [18, 4]];
  tactic.groups[1].act.point = [15, 12];
  document.tactics.push(tactic);
  assert.deepEqual(
    validateDocument(document, catalog).filter((issue) => issue.severity === 'error'), []);
  const sentence = summarize(tactic, (slot) => document.squad[slot]?.name ?? null);
  assert.match(sentence, /coming in by the drawn route/);

  // A one-point route and a route on a no-draw kind are both named.
  tactic.groups[1].stage.via = [[14, 3]];
  let messages = validateDocument(document, catalog).map((issue) => issue.message);
  assert.ok(messages.some((message) => /route needs two or more points/.test(message)));
  tactic.groups[1].stage.via = [[14, 3], [18, 4]];
  tactic.groups[0].act.via = [[14, 3], [18, 4]]; // guard act is 'none'
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

test('the readback summarizes a pincer as one whole operation', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const { tactic } = instantiateRecipe(recipeById('pincer'), document);
  tactic.groups[0].slots = [5, 2];
  tactic.groups[0].stage.posts = [
    { at: [14, 12], facing: 'e' },
    { at: [16, 14], facing: 'n' },
  ];
  tactic.groups[1].slots = [0, 1, 3, 4, 6, 7];
  tactic.groups[1].stage.rect = [22, 3, 26, 7];
  tactic.groups[1].act.point = [15, 12];
  const sentence = summarize(tactic, (slot) => document.squad[slot]?.name ?? null);
  assert.match(sentence, /^Two bots hold posts at Mid/);
  assert.match(sentence, /while six (stage|wait)/);
  assert.match(sentence, /converge on Mid aggressively/);
  assert.match(sentence, /Finish when three enemies destroyed, Mid cleared, or a Core is acquired — or the attempt expires\./);
});

test('practice attribution maps generated order ids back to Tactic, Group and phase', () => {
  const catalog = makeCatalog();
  const document = makeDocument(catalog);
  const { tactic } = instantiateRecipe(recipeById('pincer'), document);
  tactic.name = 'Mid Pincer';
  tactic.groups[1].name = 'Flankers';
  document.tactics.push(tactic);
  const acting = parseTacticAttribution(
    'orders:vex > t1 > t1-go > t1-g2-a-move:eligibility[…]:march@15,12', document);
  assert.ok(acting);
  assert.equal(acting.tacticOrdinal, 1);
  assert.equal(acting.tactic?.name, 'Mid Pincer');
  assert.equal(acting.groupName, 'Flankers');
  assert.equal(acting.phase, 'act');
  assert.equal(attributionLabel(acting), 'Mid Pincer · Flankers · Act');
  const staging = parseTacticAttribution(
    'orders:ember > t1 > t1-s > t1-g1-s-move:…', document);
  assert.equal(staging?.phase, 'stage');
  const baseline = parseTacticAttribution(
    'orders:cinder > hold-ground:eligibility[…]', document);
  assert.equal(baseline?.phase, 'baseline');
  assert.equal(attributionLabel(baseline!), 'between tactics');
});
