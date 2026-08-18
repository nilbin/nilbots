#!/usr/bin/env node
/**
 * Browser interaction specs for the Tactics editor: one spec per flagship
 * recipe, driving the real editor against the typed site-review server — the
 * design doc's acceptance gate ("each flagship recipe has … an automated
 * browser interaction test"), in the spirit of the mock's drive.cjs.
 *
 * Usage:
 *   (cd web && npm run site-review)        # serve the harness on :4181
 *   (cd web && npm run tactics-specs)      # run these specs against it
 *
 * BASE overrides the target. Exits non-zero on the first failed assertion.
 */

import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const requireFromWeb = createRequire(
  join(scriptDirectory, '..', 'web', 'package.json'),
);
const { chromium } = requireFromWeb('playwright');

const base = (process.env.BASE ?? 'http://127.0.0.1:4181').replace(/\/$/, '');
const healthUrl = `${base}/__site-review/health`;

let checks = 0;
function pass(name) {
  checks += 1;
  console.log(`  ✓ ${name}`);
}

async function assertVisible(page, locator, name) {
  await locator.first().waitFor({ state: 'visible', timeout: 8000 });
  pass(name);
}

/** Click a tile on the SVG map, addressing it in tile units. */
async function clickTile(page, x, y) {
  const map = page.getByTestId('tactics-map');
  const box = await map.boundingBox();
  if (!box) throw new Error('The tactics map is not on screen.');
  await page.mouse.click(
    box.x + ((x + 0.5) / 31) * box.width,
    box.y + ((y + 0.5) / 27) * box.height,
  );
}

/** Drag a box on the map, in tile units. */
async function dragRect(page, x0, y0, x1, y1) {
  const map = page.getByTestId('tactics-map');
  const box = await map.boundingBox();
  if (!box) throw new Error('The tactics map is not on screen.');
  const at = (x, y) => [
    box.x + ((x + 0.5) / 31) * box.width,
    box.y + ((y + 0.5) / 27) * box.height,
  ];
  const [sx, sy] = at(x0, y0);
  const [ex, ey] = at(x1, y1);
  await page.mouse.move(sx, sy);
  await page.mouse.down();
  await page.mouse.move((sx + ex) / 2, (sy + ey) / 2, { steps: 4 });
  await page.mouse.move(ex, ey, { steps: 4 });
  await page.mouse.up();
}

async function openFreshEditor(page) {
  // The editor keeps unsent drafts in IndexedDB by design; specs want a
  // clean slate every run.
  await page.goto(`${base}/entrants/sheets/new`);
  await page.evaluate(() => indexedDB.deleteDatabase('nilbots-tactics-drafts-v1'));
  await page.reload();
  await page.getByTestId('create-tactic').waitFor({ timeout: 15000 });
}

async function fillSquad(page) {
  await page.getByTestId('open-squad').click();
  const classes = [
    'kestrel', 'switchback', 'longshot', 'mason',
    'lantern', 'palisade', 'relay', 'patchbay',
  ];
  for (const classId of classes) {
    await page.getByTestId(`class-${classId}`).click();
  }
  await page.getByTestId('squad-done').click();
  await assertVisible(page, page.getByText('8/8').first(), 'all eight squad slots filled');
}

async function nextStep(page) {
  await page.getByTestId('wizard-next').click();
}

async function startRecipe(page, recipeId) {
  await page.getByTestId('create-tactic').click();
  await page.getByTestId(`recipe-${recipeId}`).click();
  await assertVisible(page, page.getByTestId('wizard-steps'),
    `${recipeId}: wizard opened at Start`);
}

async function distributeGroups(page, expectedAssigned) {
  await page.getByRole('button', { name: 'Distribute the rest' }).click();
  await assertVisible(page, page.getByText(`${expectedAssigned}/8`).first(),
    'all eight bots visibly assigned exactly once');
}

async function commitTactic(page, expectedSummary) {
  const commit = page.getByTestId('commit-tactic');
  await commit.waitFor();
  if (!(await commit.isEnabled())) {
    const gate = await page.getByTestId('drawer').textContent();
    throw new Error(`Create Tactic is gated: ${gate?.slice(-260)}`);
  }
  await commit.click();
  for (const fragment of expectedSummary) {
    await assertVisible(page,
      page.getByTestId('tactic-list').getByText(fragment, { exact: false }),
      `summary reads “${fragment}”`);
  }
}

const specs = {
  async pincer(page) {
    await startRecipe(page, 'pincer');
    await nextStep(page); // Start → Groups
    await distributeGroups(page, 8);
    await nextStep(page); // Groups → Stage
    // Two exact posts for the guard; the wizard auto-arms the draw task from
    // the section button.
    await page.getByRole('button', { name: 'Draw on map' }).first().click();
    await clickTile(page, 14, 12);
    await clickTile(page, 16, 14);
    // Flankers assemble behind their lines.
    await page.getByRole('button', { name: 'Draw on map' }).first().click();
    await dragRect(page, 22, 3, 26, 7);
    await nextStep(page); // Stage → Release
    await assertVisible(page, page.getByText('Enemies gather somewhere'),
      'pincer: release trigger row present');
    await nextStep(page); // Release → Act
    await page.getByRole('button', { name: 'Draw on map' }).first().click();
    await clickTile(page, 15, 12);
    await nextStep(page); // Act → Cores
    await nextStep(page); // Cores → Finish
    await assertVisible(page, page.getByText('Outcomes start counting once the Tactic releases'),
      'pincer: post-release counting promise on the finish step');
    await nextStep(page); // Finish → Review
    await commitTactic(page, ['hold posts at Mid', 'converge on Mid']);
    // Save the sheet through the simulated API: the first save navigates to
    // the sheet's own edit URL and reloads the same draft from the server.
    await page.getByTestId('save-sheet').click();
    await page.waitForURL(/\/entrants\/sheets\/[0-9a-f-]+\/edit/, { timeout: 8000 });
    await assertVisible(page,
      page.getByTestId('tactic-list').getByText('hold posts at Mid'),
      'pincer: saved sheet reloads with the same Tactic');
  },

  async 'hold-area'(page) {
    await startRecipe(page, 'hold-area');
    await nextStep(page);
    await distributeGroups(page, 8);
    await nextStep(page);
    await page.getByRole('button', { name: 'Draw on map' }).first().click();
    const posts = [[13, 12], [15, 12], [17, 12], [14, 14], [16, 13], [13, 14], [17, 14], [15, 15]];
    for (const [x, y] of posts) await clickTile(page, x, y);
    await nextStep(page); // Release
    await nextStep(page); // Act
    await nextStep(page); // Cores
    await nextStep(page); // Finish
    await nextStep(page); // Review
    await commitTactic(page, ['hold posts at Mid', 'persists until a higher Tactic']);
  },

  async 'centre-rush'(page) {
    await startRecipe(page, 'centre-rush');
    await nextStep(page);
    await distributeGroups(page, 8);
    await nextStep(page);
    await page.getByRole('button', { name: 'Draw on map' }).first().click();
    await dragRect(page, 9, 11, 13, 14);
    await nextStep(page); // Release
    await nextStep(page); // Act
    await page.getByRole('button', { name: 'Draw on map' }).first().click();
    await clickTile(page, 15, 13);
    await clickTile(page, 18, 13);
    await clickTile(page, 23, 19);
    await page.getByRole('button', { name: 'Done' }).click();
    await nextStep(page); // Cores
    await nextStep(page); // Finish
    await nextStep(page); // Review
    await commitTactic(page, ['push toward', 'Finish when']);
  },

  async 'core-raid'(page) {
    await startRecipe(page, 'core-raid');
    await nextStep(page);
    await distributeGroups(page, 8);
    await nextStep(page);
    await page.getByRole('button', { name: 'Draw on map' }).first().click();
    await clickTile(page, 19, 19);
    await page.getByRole('button', { name: 'Draw on map' }).first().click();
    await dragRect(page, 13, 14, 18, 19);
    await nextStep(page); // Release
    await assertVisible(page, page.getByText('A Core is up for grabs'),
      'core-raid: core-loose trigger row present');
    await nextStep(page); // Act
    await nextStep(page); // Cores
    await assertVisible(page, page.getByText('Carrier breaks off and banks'),
      'core-raid: solo delivery preset');
    await nextStep(page); // Finish
    await nextStep(page); // Review
    await commitTactic(page, ['move up short of their Well', 'the Core home to your reactor']);
  },

  async 'deliver-core'(page) {
    await startRecipe(page, 'deliver-core');
    await assertVisible(page, page.getByText('We hold a Core — a friendly carrier exists'),
      'deliver-core: carrier start condition ticked');
    await nextStep(page);
    await distributeGroups(page, 8);
    await nextStep(page); // Stage
    await page.getByRole('button', { name: 'Draw on map' }).first().click();
    await dragRect(page, 4, 10, 9, 16);
    await nextStep(page); // Release
    await nextStep(page); // Act
    await nextStep(page); // Cores
    await assertVisible(page, page.getByText('banks it under escort'),
      'deliver-core: escorted delivery readback');
    await nextStep(page); // Finish
    await nextStep(page); // Review
    await commitTactic(page, ['the Core home to your reactor', 'the carrier the whole way']);
  },

  async patrol(page) {
    await startRecipe(page, 'patrol');
    await nextStep(page);
    await distributeGroups(page, 8);
    await nextStep(page); // Stage (none — nothing to draw)
    await nextStep(page); // Release
    await nextStep(page); // Act
    await page.getByRole('button', { name: 'Draw on map' }).first().click();
    const loop = [[7, 9], [15, 10], [22, 12], [15, 16]];
    for (const [x, y] of loop) await clickTile(page, x, y);
    await page.getByRole('button', { name: 'Done' }).click();
    await nextStep(page); // Cores
    await nextStep(page); // Finish
    await nextStep(page); // Review
    await commitTactic(page, ['walk their patrol loop', 'persists until a higher Tactic']);
  },
};

async function main() {
  const health = await fetch(healthUrl).catch(() => null);
  if (!health?.ok) {
    console.error(`Site-review server is not reachable at ${base}.`);
    console.error('Start it first: (cd web && npm run site-review)');
    process.exit(2);
  }
  const browser = await chromium.launch();
  try {
    for (const [name, spec] of Object.entries(specs)) {
      console.log(`spec: ${name}`);
      const context = await browser.newContext({
        viewport: { width: 1500, height: 1000 },
      });
      const page = await context.newPage();
      page.setDefaultTimeout(8000);
      await openFreshEditor(page);
      await fillSquad(page);
      await spec(page);
      await context.close();
    }
  } finally {
    await browser.close();
  }
  console.log(`\nAll flagship recipe specs passed (${checks} assertions).`);
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
