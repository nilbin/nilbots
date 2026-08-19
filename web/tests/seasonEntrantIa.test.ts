import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

/**
 * Pins the Season/entrant information architecture and the Tactics editor's
 * player-facing language. These are source-text pins on purpose: copy IS the
 * product model here, and a drive-by rename quietly shipping engine
 * vocabulary ("Instructions", "roles", "workshop") to players is exactly what
 * this suite exists to catch.
 */

const read = (path: string) => readFileSync(new URL(path, import.meta.url), 'utf8');
const shell = read('../src/site/Shell.tsx');
const site = read('../src/site/Site.tsx');
const season = read('../src/site/pages/SeasonPage.tsx');
const entrants = read('../src/site/pages/ArcRelayPage.tsx');
const editor = read('../src/site/tactics/TacticsEditorPage.tsx');
const steps = read('../src/site/tactics/WizardSteps.tsx');
const wizard = read('../src/site/tactics/wizardState.ts');
const recipes = read('../src/site/tactics/recipes.ts');
const conditions = read('../src/site/tactics/ConditionRows.tsx');
const canvas = read('../src/site/tactics/TacticsMapCanvas.tsx');
const summary = read('../src/site/tactics/summary.ts');
const documentModel = read('../src/site/tactics/tacticsDocument.ts');
const docs = read('../src/site/pages/DocsPage.tsx');
const styles = read('../src/index.css');
const siteReview = read('../vite.site-review.config.ts');
const siteShots = read('../../scripts/site-shots.mjs');

const allEditorSource = [editor, steps, wizard, recipes, conditions, canvas, summary];

test('primary navigation has one season-centred hierarchy on desktop and phone', () => {
  const desktop = [...shell.matchAll(/<TopLink to="([^"]+)">([^<]+)<\/TopLink>/g)].map((match) => [match[1], match[2]]);
  const phone = [...shell.matchAll(/<MobileLink to="([^"]+)">([^<]+)<\/MobileLink>/g)].map((match) => [match[1], match[2]]);
  const expected = [['/', 'Season'], ['/entrants', 'Entrants'], ['/watch', 'Watch'], ['/docs', 'Docs']];
  assert.deepEqual(desktop, expected);
  assert.deepEqual(phone, expected);
  assert.doesNotMatch(shell, />Relay<|>Sheets</);
});

test('old Relay URLs redirect without becoming a second product hierarchy', () => {
  assert.match(site, /path="\/relay" element={<Navigate to="\/" replace \/>}/);
  assert.match(site, /path="\/relay\/sheets" element={<Navigate to="\/entrants\/sheets\/new" replace \/>}/);
  assert.match(site, /path="\/entrants\/sheets\/:sheetId\/edit"/);
});

test('the home screen reads the authoritative season projection', () => {
  assert.match(season, /useArcRelaySeason\(\)/);
  assert.match(season, /Season standings/);
  assert.match(season, /Only settled, fully revealed results appear here/);
  assert.match(season, /baselineMatchesPerEntrantPerDay/);
});

test('a sheet is an ordered list of whole-team Tactics, and the copy says so', () => {
  assert.match(editor, /An ordered list of whole-team <b>Tactics<\/b>/);
  assert.match(editor, /No Tactics yet\. The Sheet runs top to bottom/);
  assert.match(editor, /＋ Create Tactic/);
  assert.match(editor, /The map is the editor — the drawer only asks the questions\./);
  assert.match(editor, /Editing reopens the same answers you gave/);
});

test('the wizard runs Start → Groups → Phases → Cores → Review', () => {
  assert.match(wizard,
    /'start'[\s\S]*?'groups'[\s\S]*?'phases'[\s\S]*?'cores'[\s\S]*?'review'/);
  assert.match(steps, /when does this Tactic apply at all\?/);
  assert.match(steps, /it is committed: it keeps control until it finishes, aborts/);
  assert.match(steps, /Split the whole team into up to three temporary Groups/);
  assert.match(steps, /The operation, phase by phase\./);
  assert.match(steps, /Abort when…/);
  assert.match(steps, /Read it back\. If the sentences say what you meant, it’s ready\./);
});

test('recipes prefill one universal structure and pruning keys off content, never the recipe', () => {
  assert.match(editor, /Recipes prefill the same structure — they are shortcuts, not systems\./);
  for (const label of ['Hold Area', 'Centre Rush', 'Pincer', 'Core Raid', 'Patrol', 'Deliver Core', 'Blank Tactic']) {
    assert.match(recipes, new RegExp(`label: '${label}'`));
  }
  assert.match(recipes, /recipe id is provenance only/);
  assert.doesNotMatch(steps, /recipeId|\.recipe\b/,
    'wizard pruning must key off document content, never the creating recipe');
});

test('phases are latched, forward-only, and carry-on keeps everyone busy', () => {
  assert.match(steps, /its “Continue when…” is latched/);
  assert.match(steps, /Transitions only ever go forward\./);
  assert.match(steps, /the moment it fires, the operation moves forward/);
  assert.match(documentModel, /Keep the previous phase’s job under this phase’s posture\./);
  assert.match(steps, /leave empty to hold here forever/);
});

test('cases hide boolean words behind Required, Triggered by, and Add alternative', () => {
  assert.match(conditions, /Enemies gather somewhere/);
  assert.match(conditions, /A Core is up for grabs/);
  assert.match(conditions, /We hold a Core — a friendly carrier exists/);
  assert.match(conditions, /Required — all must hold/);
  assert.match(conditions, /Triggered by — any may fire/);
  assert.match(conditions, /＋ Add alternative/);
  assert.match(conditions, /Every Group is in position \(the dead excuse themselves\)/);
  // Rows are bucketed under small group headings instead of a fold, so a
  // player scans for "the Core rows" rather than a flat list.
  assert.match(conditions, /Our team/);
  assert.match(conditions, /Enemies/);
  assert.match(conditions, /Cores/);
  assert.match(conditions, /Time & ground/);
  assert.match(conditions, /Unless — none of these/);
  assert.doesNotMatch(conditions, /\bAND\b|\bOR\b/,
    'no bare boolean vocabulary on the player surface');
});

test('the readback speaks player language with real places', () => {
  assert.match(summary, /hold.*posts at/s);
  assert.match(summary, /converge/);
  assert.match(summary, /run.*the Core home to your reactor/);
  assert.match(summary, /screen.*the carrier the whole way/);
  assert.match(summary, /Finish when/);
  assert.match(summary, /holds there until a Tactic-wide abort/);
});

test('the map draws condition footprints under grabbable group geometry', () => {
  assert.match(canvas, /footprints render under group geometry/);
  assert.match(canvas, /ENEMIES HERE/);
  assert.match(canvas, /CLEARED HERE/);
  assert.match(canvas, /CORE RUN/);
  assert.match(canvas, /data-testid="tactics-map"/);
});

test('engine vocabulary never reaches the player surface', () => {
  for (const source of allEditorSource) {
    assert.doesNotMatch(source,
      // "custody" left this list when the owner adopted "Core custody" as
      // the player-facing phase concept (DECISIONS #341).
      /Instructions|sharedOrders|workshop|arc-relay-orders-v2|doctrine|projection/i);
  }
  assert.doesNotMatch(`${editor}${steps}`, /\bRoles\b/,
    'Groups replaced roles on the player surface');
});

test('the document model mirrors the server grammar and stays the only saved content', () => {
  assert.match(documentModel, /arc-relay-tactics-v1/);
  assert.match(documentModel, /descriptor capabilities — never from a class-id switch/);
  assert.match(editor, /tacticsJson: stringifyDocument/);
  assert.doesNotMatch(editor, /ordersJson:|mapPlanJson:/,
    'the client never generates or submits Orders');
});

test('docs describe Tactics, not the retired Programs workshop', () => {
  assert.match(docs, /whole-team[\s\S]{0,20}<b> Tactics<\/b>/);
  assert.match(docs, /ordered, named[\s\S]{0,20}phases/);
  assert.match(docs, /Transitions[\s\S]{0,20}only ever go forward/);
  assert.match(docs, /Respawned[\s\S]{0,20}bots join the current phase\./);
  assert.match(docs, /deterministically materializes your Tactics/);
  assert.doesNotMatch(docs, /Programs|workshop|Instructions/);
});

test('primary and practice actions use the established forge controls', () => {
  assert.match(styles, /\.btn-primary\s*\{[^}]*background:\s*var\(--color-arena-material\)/s);
  assert.match(styles, /\.btn-secondary\s*\{[^}]*background:/s);
  assert.doesNotMatch(shell, /<footer|deterministic Arc Relay/);
});

test('the controlled browser review opens the Tactics editor on a tactics starter', () => {
  assert.match(siteReview, /tactics\/first-stand-v1\.json/);
  assert.match(siteReview, /tacticsJson/);
  assert.match(siteReview, /\/api\/sheets\/catalog/);
  assert.match(siteShots, /\['tactics-editor', '\/entrants\/sheets\/new'\]/);
});
