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

test('the wizard runs Start → Groups → Stage → Release → Act → Cores → Finish → Review', () => {
  assert.match(wizard,
    /'start'[\s\S]*?'groups'[\s\S]*?'stage'[\s\S]*?'release'[\s\S]*?'act'[\s\S]*?'cores'[\s\S]*?'finish'[\s\S]*?'review'/);
  assert.match(steps, /when does this Tactic apply at all\?/);
  assert.match(steps, /Split the whole team into up to three temporary Groups/);
  assert.match(steps, /Every Group stages its own way\./);
  assert.match(steps, /when do they go\?/);
  assert.match(steps, /Release ends every Group's Stage at the same time\./);
  assert.match(steps, /when is this Tactic finished\?/);
  assert.match(steps, /Read it back\. If the sentence says what you meant, it’s ready\./);
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

test('outcomes count after release and every Group keeps a continuation', () => {
  assert.match(steps, /Outcomes start counting once the Tactic releases/);
  assert.match(steps, /a holding Group can’t finish it while the others are still staging\./);
  assert.match(steps, /Groups with no Act just keep holding after release — nobody wanders off\./);
});

test('release and start share the one tick-row condition idiom', () => {
  assert.match(conditions, /Enemies gather somewhere/);
  assert.match(conditions, /A Core is up for grabs/);
  assert.match(conditions, /We hold a Core — a friendly carrier exists/);
  assert.match(conditions, /any ticked row satisfies the question \(OR\)/i);
  assert.match(steps, /kinds={\['enemiesAt', 'coreLoose', 'carrier', 'losses'\]}/);
  assert.match(steps, /kinds={\['enemiesAt', 'contact', 'coreLoose'\]}/);
  // Release rows OR together like start rows since the order grammar's one
  // nesting level (DECISIONS #332); no single-select cap survives anywhere.
  assert.match(steps, /any ticked condition lets go/);
  assert.doesNotMatch(conditions, /single/,
    'the release latch ORs its rows — no single-select cap');
});

test('the readback speaks player language with real places', () => {
  assert.match(summary, /hold.*posts at/s);
  assert.match(summary, /converge/);
  assert.match(summary, /run.*the Core home to your reactor/);
  assert.match(summary, /screen.*the carrier the whole way/);
  assert.match(summary, /Finish when/);
  assert.match(summary, /Otherwise it persists until a higher Tactic takes over\./);
});

test('the map draws condition footprints under grabbable group geometry', () => {
  assert.match(canvas, /Condition footprints render[\s\S]*under group geometry/);
  assert.match(canvas, /ENEMIES HERE/);
  assert.match(canvas, /CLEAR TO FINISH/);
  assert.match(canvas, /CORE RUN/);
  assert.match(canvas, /data-testid="tactics-map"/);
});

test('engine vocabulary never reaches the player surface', () => {
  for (const source of allEditorSource) {
    assert.doesNotMatch(source,
      /Instructions|sharedOrders|workshop|arc-relay-orders-v2|custody|doctrine|projection/i);
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
  assert.match(docs, /outcomes count only after release/);
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
