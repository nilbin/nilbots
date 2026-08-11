import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const shell = readFileSync(
  new URL('../src/site/Shell.tsx', import.meta.url),
  'utf8',
);
const site = readFileSync(
  new URL('../src/site/Site.tsx', import.meta.url),
  'utf8',
);
const season = readFileSync(
  new URL('../src/site/pages/SeasonPage.tsx', import.meta.url),
  'utf8',
);
const entrants = readFileSync(
  new URL('../src/site/pages/ArcRelayPage.tsx', import.meta.url),
  'utf8',
);
const workshop = readFileSync(
  new URL('../src/site/sheets/TacticalSheetEditor.tsx', import.meta.url),
  'utf8',
);
const docs = readFileSync(
  new URL('../src/site/pages/DocsPage.tsx', import.meta.url),
  'utf8',
);
const styles = readFileSync(
  new URL('../src/index.css', import.meta.url),
  'utf8',
);
const siteReview = readFileSync(
  new URL('../vite.site-review.config.ts', import.meta.url),
  'utf8',
);
const siteShots = readFileSync(
  new URL('../../scripts/site-shots.mjs', import.meta.url),
  'utf8',
);

test('primary navigation has one season-centred hierarchy on desktop and phone', () => {
  const desktop = [...shell.matchAll(/<TopLink to="([^"]+)">([^<]+)<\/TopLink>/g)]
    .map((match) => [match[1], match[2]]);
  const phone = [...shell.matchAll(/<MobileLink to="([^"]+)">([^<]+)<\/MobileLink>/g)]
    .map((match) => [match[1], match[2]]);
  const expected = [
    ['/', 'Season'],
    ['/entrants', 'Entrants'],
    ['/watch', 'Watch'],
    ['/docs', 'Docs'],
  ];

  assert.deepEqual(desktop, expected);
  assert.deepEqual(phone, expected);
  assert.doesNotMatch(shell, />Relay<|>Sheets</);
});

test('old Relay URLs redirect without becoming a second product hierarchy', () => {
  assert.match(site, /path="\/relay" element={<Navigate to="\/" replace \/>}/);
  assert.match(
    site,
    /path="\/relay\/sheets" element={<Navigate to="\/entrants\/sheets\/new" replace \/>}/,
  );
  assert.match(site, /path="\/entrants\/sheets\/:sheetId\/edit"/);
});

test('the home screen reads the authoritative season projection', () => {
  assert.match(season, /useArcRelaySeason\(\)/);
  assert.match(season, /Season standings/);
  assert.match(season, /Only settled, fully revealed results appear here/);
  assert.match(season, /Equal ratings share a rank/);
  assert.match(season, /baselineMatchesPerEntrantPerDay/);
});

test('ranked enrollment and immediate practice have different visible verbs', () => {
  assert.match(entrants, /Ranked runs automatically/);
  assert.match(entrants, /There is no ranked queue button/);
  assert.match(entrants, /Field for ranked/);
  assert.match(workshop, /Practice (?:&amp;|&) watch/);
  assert.match(workshop, /Practice is immediate, unranked/);
});

test('the workspace has one persistent practice action and separates scenario setup', () => {
  assert.equal(workshop.match(/onClick=\{\(\) => void runTrial\(\)\}/g)?.length, 1);
  assert.match(workshop, /onClick=\{\(\) => void runTrial\(\)\}/);
  assert.match(workshop, /aria-label="Choose practice opponent and scenario"/);
  assert.match(workshop, /single Practice action in the workspace bar/);
  assert.match(workshop, /Practice again/);
  assert.doesNotMatch(workshop, /btn btn-secondary fixed/);
  assert.doesNotMatch(workshop, /Import JSON pair|Export pinned pair|layout sha256/);
});

test('practice is a secondary game action rather than a competing primary', () => {
  assert.match(workshop, /btn btn-secondary min-h-11 shrink-0 gap-2/);
  assert.match(styles, /\.btn-secondary\s*\{[^}]*background:/s);
  assert.match(styles, /\.btn-secondary-symbol\s*\{[^}]*color:\s*var\(--color-arena-material\)/s);
});

test('the editor uses Squad, Commands and Map while team tactics stay contextual', () => {
  assert.doesNotMatch(workshop, /id: 'conditions'|id: 'custody'/);
  assert.match(workshop, /id: 'squad'.*id: 'commands'.*id: 'map'/s);
  assert.match(workshop, /Shared triggers, Core plans, coordination caps and memory/);
  assert.match(workshop, /title="Doctrine triggers"/);
  assert.match(workshop, /title="Core custody"/);
  assert.match(workshop, /label: 'Map'/);
});

test('doctrine authoring presents a battle-plan board over the compiler schema', () => {
  assert.match(workshop, /Order every assignment’s plays/);
  assert.match(workshop, /Six tactical verbs · one ordered ladder/);
  assert.match(workshop, /data-doctrine-select/);
  assert.match(workshop, /entry\.name} · \{entry\.signatureName/);
  assert.match(workshop, /Tactical action/);
  assert.match(workshop, /Team tactics/);
  assert.match(workshop, /Doctrine triggers/);
  assert.match(workshop, /predicateSentence\(predicate\)/);
  assert.match(workshop, /Core plans/);
  assert.match(workshop, /Coordination/);
  assert.match(workshop, /Combat baseline for this assignment/);
  assert.match(workshop, /game standard/);
  assert.match(workshop, /Adaptive participation cap/);
  assert.match(workshop, /This play calls nobody/);
  assert.doesNotMatch(workshop, /Who joins this play/);
  assert.match(workshop, /Opportunistic loose-Core pickup/);
  assert.match(workshop, /This is an\s+opportunistic exception, not planned courier duty/);
  assert.match(workshop, /＋ Add condition/);
  assert.match(workshop, /TacticalGeometryPreview/);
  assert.match(workshop, /Edit \{humanizeToken\(destination\.id\)\} on the tactical map/);
  assert.match(workshop, /Duplicate play/);
  assert.match(workshop, /Exact same footprint/);
  assert.match(workshop, /expandedModeKey/);
  assert.match(workshop, /open=\{expanded\}/);
  assert.match(styles, /details\[open\] > summary \.disclosure-chevron/);
});

test('composition turns compiler roles into editable player assignments', () => {
  assert.match(workshop, /Squad assignments/);
  assert.match(workshop, /Turn chassis into jobs/);
  assert.match(workshop, /AssignmentNameEditor/);
  assert.match(workshop, /Eligible chassis · priority order/);
  assert.match(workshop, /Normal staffing target/);
  assert.match(workshop, /entry\.unlocked \? `\$\{counts\.get\(entry\.id\) \?\? 0\}\/2` : 'locked'/);
  assert.match(workshop, /Staffing &amp; replacement rules/);
  assert.match(workshop, /These rules allocate bodies to this assignment/);
});

test('custom-mind authoring is dormant on the launch web surface', () => {
  assert.doesNotMatch(entrants, /custom mind|custom-mind|preflight|C# source/i);
  assert.doesNotMatch(docs, /custom mind|custom-mind|preflight|C# source/i);
});

test('primary actions use the forge material and the dead site footer is gone', () => {
  assert.match(styles, /\.btn-primary\s*\{[^}]*background:\s*var\(--color-arena-material\)/s);
  assert.match(styles, /\.btn\s*\{[^}]*display:\s*inline-flex[^}]*line-height:\s*1\.15/s);
  assert.match(styles, /html\[data-arena-mode='site'\]\s*\{\s*color-scheme:\s*dark;/s);
  assert.doesNotMatch(styles, /prefers-color-scheme:\s*light/);
  assert.doesNotMatch(shell, /<footer|deterministic Arc Relay/);
});

test('the controlled browser review opens the current Tapline workshop', () => {
  assert.match(siteReview, /hunter-tapline-v1\.json/);
  assert.match(siteReview, /tapline-base-v1\.json/);
  assert.match(siteReview, /\/api\/sheets\/catalog/);
  assert.match(siteShots, /\['sheet-workshop', '\/entrants\/sheets\/new'\]/);
});

test('the tactics map names every authoritative Arc Relay landmark', () => {
  assert.match(workshop, /Map landmark legend/);
  assert.match(workshop, /Healing tile/);
  assert.match(workshop, /Delivery zone/);
  assert.match(workshop, /Protected spawn pad/);
  assert.match(workshop, /Signature blocked/);
  assert.match(workshop, /relationForSpawn/);
  assert.match(workshop, /nearestSpawnTeam/);
});
