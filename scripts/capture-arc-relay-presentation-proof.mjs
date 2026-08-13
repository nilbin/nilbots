#!/usr/bin/env node

/** Capture deterministic WebGL proof from the retained real Tapline broadcasts. */

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import {
  existsSync,
  mkdirSync,
  readdirSync,
  readFileSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
);
const options = parseOptions(process.argv.slice(2));
if (options['owner-requested-full-proof'] !== 'true')
  throw new Error(
    'Full presentation proof is owner-request-only. Re-run only after an explicit ' +
    'owner request with --owner-requested-full-proof true.',
  );
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');
const proofDirectory = absolute(
  options.proof ??
    'art/reviews/arc-relay-presentation-batch-v1/runtime-proof',
);
const output = path.join(proofDirectory, 'captures');
const browserProofPath = path.join(output, 'browser-proof.json');
const manifest = JSON.parse(
  readFileSync(path.join(proofDirectory, 'proof-manifest.json'), 'utf8'),
);
const dist = absolute(options.dist ?? 'web/dist-review');
const url = options.url ??
  'http://127.0.0.1:8931/web/dist-review/?standalone&audio=off';

if (
  manifest.schema !== 'arc-relay-presentation-runtime-proof-v1' ||
  manifest.passed !== true ||
  manifest.coverage?.signatureCount !== 16
)
  throw new Error('Runtime proof manifest is not the complete retained package.');
if (!readFileSync(path.join(dist, 'index.html'), 'utf8').includes('<title>'))
  throw new Error('Build web/dist-review and install the proof gallery first.');
// Replay-gallery installation writes timestamps and replay projections beside
// the immutable Vite build. Those inputs are validated separately below; they
// must not make an unchanged renderer look stale and trigger a 48-frame redo.
const buildSha256 = directorySha256(dist, (entryPath) =>
  !isInstalledReplayFile(relativeTo(dist, entryPath)),
);
const captureFramingVersion = 4;

const replayChoices = JSON.parse(
  readFileSync(path.join(dist, 'replays.json'), 'utf8'),
);
if (replayChoices.length !== 2)
  throw new Error('Presentation capture expects exactly two proof replays.');

mkdirSync(output, { recursive: true });

const lifecycleByMatch = Map.groupBy(
  manifest.lifecycleEvidence,
  (entry) => entry.matchId,
);
const seekSamples = chooseSeekSamples(manifest.lifecycleEvidence);
const reducedMotionSamples = [
  'falling-star',
  'repair-beam',
  'smoke-canister',
  'target-paint',
].map((signatureId) => {
  const found = manifest.lifecycleEvidence.find(
    (entry) => entry.signatureId === signatureId,
  );
  if (!found) throw new Error(`Missing reduced-motion sample ${signatureId}.`);
  return found;
});
const levelMatch = manifest.matches.find((match) => match.levelUps.length > 0);
const levelFact = levelMatch?.levelUps[0];
if (!levelMatch || !levelFact)
  throw new Error('The real proof replays contain no level-up fact.');
const retainedEvidence = loadPriorEvidence();
const evidence = {
  schema: 'arc-relay-presentation-browser-proof-v1',
  sourceRevision: manifest.sourceRevision,
  buildSha256,
  captureFramingVersion,
  url,
  passed: false,
  renderer: 'webgl',
  captures: retainedEvidence?.captures ?? [],
  lifecycle: retainedEvidence?.lifecycle ?? [],
  seekComparisons: retainedEvidence?.seekComparisons ?? [],
  reducedMotion: retainedEvidence?.reducedMotion ?? [],
  levelUp: retainedEvidence?.levelUp ?? null,
  ...(retainedEvidence?.migratedFromCapturedImages === true
    ? { migratedFromCapturedImages: true }
    : {}),
  errors: [],
};
const needsDesktop = desktopEvidenceMissing();
const needsPhone = phoneEvidenceMissing();
let browser;
let context;

try {
  if (needsDesktop || needsPhone) {
    browser = await chromium.launch({ headless: true });
    context = await browser.newContext({ deviceScaleFactor: 1 });
    await context.route('**/*', async (route) => {
      const request = route.request();
      if (request.resourceType() === 'media') {
        await route.fulfill({ status: 204, body: '' });
        return;
      }
      await route.continue();
    });
  }

  if (needsDesktop) {
    const desktop = await openSession(context, 'desktop', {
      viewport: { width: 1440, height: 900 },
      reducedMotion: 'no-preference',
      mobile: false,
    });
    for (const match of manifest.matches) {
      const overviewId = `${match.id}-desktop`;
      const missingLifecycle = (lifecycleByMatch.get(match.id) ?? []).filter(
        (lifecycle) => !retainedLifecycle(
          evidence.lifecycle.find(
            (entry) => entry.signatureId === lifecycle.signatureId,
          ),
        ),
      );
      const missingOverview = !evidence.captures.some(
        (entry) => entry.id === overviewId && retained(entry, overviewId),
      );
      if (!missingOverview && missingLifecycle.length === 0) continue;
      await selectReplay(desktop.page, match.id);
      if (missingOverview) {
        evidence.captures = replaceBy(
          evidence.captures,
          'id',
          overviewId,
          await captureOverview(desktop, match, overviewId),
        );
        writeEvidence();
      }
      for (const lifecycle of missingLifecycle) {
        evidence.lifecycle = replaceBy(
          evidence.lifecycle,
          'signatureId',
          lifecycle.signatureId,
          await captureLifecycle(desktop, lifecycle),
        );
        writeEvidence();
      }
    }

    for (const lifecycle of seekSamples) {
      if (retainedSeek(lifecycle.signatureId)) continue;
      await selectReplay(desktop.page, lifecycle.matchId);
      evidence.seekComparisons = replaceBy(
        evidence.seekComparisons,
        'signatureId',
        lifecycle.signatureId,
        await compareDirectAndContinuousSeek(desktop, lifecycle),
      );
      writeEvidence();
    }

    if (!retained(evidence.levelUp, 'level-up')) {
      await selectReplay(desktop.page, levelMatch.id);
      evidence.levelUp = await captureLevelUp(desktop, levelMatch, levelFact);
      writeEvidence();
    }

    await desktop.page.emulateMedia({ reducedMotion: 'reduce' });
    for (const lifecycle of reducedMotionSamples) {
      const existing = evidence.reducedMotion.find(
        (entry) => entry.signatureId === lifecycle.signatureId,
      );
      if (retained(existing, `${lifecycle.signatureId}-reduced`)) continue;
      await selectReplay(desktop.page, lifecycle.matchId);
      evidence.reducedMotion = replaceBy(
        evidence.reducedMotion,
        'signatureId',
        lifecycle.signatureId,
        await captureReducedMotion(desktop, lifecycle),
      );
      writeEvidence();
    }
    evidence.errors.push(...desktop.errors);
    await desktop.page.close();
  } else {
    console.log('Desktop proof is complete for this build; no page opened.');
  }

  if (needsPhone) {
    const phone = await openSession(context, 'phone', {
      viewport: { width: 844, height: 390 },
      reducedMotion: 'no-preference',
      mobile: true,
    });
    for (const match of manifest.matches) {
      const overviewId = `${match.id}-phone`;
      if (evidence.captures.some(
        (entry) => entry.id === overviewId && retained(entry, overviewId),
      )) continue;
      await selectReplay(phone.page, match.id);
      evidence.captures = replaceBy(
        evidence.captures,
        'id',
        overviewId,
        await captureOverview(phone, match, overviewId),
      );
      writeEvidence();
    }
    evidence.errors.push(...phone.errors);
    await phone.page.close();
  } else {
    console.log('Phone proof is complete for this build; no page opened.');
  }
  if (evidence.errors.length > 0)
    throw new Error(`Browser produced errors:\n${evidence.errors.join('\n')}`);
  canonicalizeEvidence();
  validateEvidence();
  evidence.passed = true;
} catch (error) {
  evidence.failure = String(error?.stack ?? error);
  throw error;
} finally {
  writeEvidence();
  await context?.close();
  await browser?.close();
}

await buildContactSheets(evidence.lifecycle);
console.log(
  `Captured ${evidence.lifecycle.length} signature lifecycle rows, ` +
  `${evidence.captures.length} device overviews, and ` +
  `${evidence.seekComparisons.length} seek comparisons.`,
);

async function openSession(context, id, settings) {
  const page = await context.newPage();
  await page.setViewportSize(settings.viewport);
  await page.emulateMedia({ reducedMotion: settings.reducedMotion });
  await page.addInitScript(() => {
    try {
      Object.defineProperty(Element.prototype, 'requestFullscreen', {
        configurable: true,
        value: undefined,
      });
    } catch {
      // CSS immersive mode is enough for fixed evidence.
    }
    localStorage.setItem('nilbots.soundtrack.enabled.v1', 'false');
  });
  const errors = [];
  page.on('pageerror', (error) => errors.push(`${id}: page: ${error.message}`));
  page.on('console', (message) => {
    if (message.type() === 'error')
      errors.push(`${id}: console: ${message.text()}`);
  });
  page.on('requestfailed', (request) =>
    errors.push(
      `${id}: request: ${request.url()} ${request.failure()?.errorText ?? ''}`,
    ),
  );
  const profile = settings.mobile ? 'mobile' : 'desktop';
  await page.goto(
    `${url}&render-profile=${profile}&proof=${id}`,
    { waitUntil: 'domcontentloaded' },
  );
  await ready(page);
  console.log(`Loaded ${id}; shared browser cache retained.`);
  return { id, page, errors, ...settings };
}

async function ready(page) {
  const overlay = page.locator('[data-play-overlay]');
  if (await overlay.count())
    await page.locator('[data-play-overlay="ready"]').waitFor({ timeout: 60_000 });
  const arena = page.getByRole('region', { name: 'Arena' });
  const canvas = arena.locator('canvas');
  await canvas.waitFor({ timeout: 60_000 });
  const kind = await canvas.evaluate((node) =>
    node.getContext('2d') ? '2d' : 'webgl',
  );
  if (kind !== 'webgl') throw new Error(`Expected WebGL, mounted ${kind}.`);
  await page.waitForFunction(() => Boolean(window.__BOTARENA_RENDER_PROOF__));
  await setCamera(page, 'overview');
  if (await overlay.isVisible()) {
    await page.getByRole('button', { name: 'Play match' }).click();
    await page
      .getByRole('button', { name: 'Pause' })
      .evaluate((button) => button.click());
  }
}

async function setCamera(page, mode) {
  const camera = page.getByRole('button', { name: /director|overview/i });
  const current = (await camera.textContent())?.toLowerCase() ?? '';
  if (mode === 'overview' && current.includes('director')) await camera.click();
  if (mode === 'director' && current.includes('overview')) await camera.click();
  await page
    .getByRole('button', { name: mode === 'overview' ? /overview/i : /director/i })
    .waitFor();
}

async function selectReplay(page, matchId) {
  const choice = replayChoices.find((entry) => entry.id === matchId);
  if (!choice) throw new Error(`Unknown replay ${matchId}.`);
  const button = page
    .getByRole('navigation', { name: 'Choose a replay to review' })
    .getByRole('button')
    .filter({ has: page.locator(`span:text-is("${labelFor(matchId)}")`) });
  if ((await button.getAttribute('aria-current')) !== 'true') {
    await button.click();
  }
  await waitForReplayIdentity(page, choice);
  const overlay = page.locator('[data-play-overlay]');
  if (await overlay.isVisible()) {
    await page.getByRole('button', { name: 'Play match' }).click();
    await page.getByRole('button', { name: 'Pause' }).evaluate((node) => node.click());
  }
  await setCamera(page, 'overview');
  console.log(`  ${matchId} ready.`);
}

async function waitForReplayIdentity(page, choice) {
  const deadline = Date.now() + 60_000;
  while (Date.now() < deadline) {
    const identity = await page.evaluate(() => ({
      renderer: window.__BOTARENA_RENDER_PROOF__?.sample(),
      transport: window.__BOTARENA_REVIEW_CONTROL__ && {
        tickCount: window.__BOTARENA_REVIEW_CONTROL__.tickCount,
        replaySeed: window.__BOTARENA_REVIEW_CONTROL__.replaySeed,
      },
    }));
    if (
      identity.renderer?.tickCount === choice.ticks &&
      identity.transport?.tickCount === choice.ticks &&
      identity.renderer?.replaySeed === identity.transport?.replaySeed
    ) return;
    await page.waitForTimeout(100);
  }
  throw new Error(`Replay identity did not settle for ${choice.id}.`);
}

function labelFor(matchId) {
  const choice = replayChoices.find((entry) => entry.id === matchId);
  if (!choice) throw new Error(`Unknown replay ${matchId}.`);
  return choice.bots.join(' v ');
}

async function captureOverview(session, match, id) {
  await seek(session.page, 14.45);
  const arena = session.page.getByRole('region', { name: 'Arena' });
  const file = path.join(output, `${id}.webp`);
  await arena.screenshot({ path: file, type: 'webp', quality: 88 });
  const bounds = await arena.boundingBox();
  const canvas = await canvasRecord(arena);
  const sample = await renderSample(session.page);
  if (
    session.id === 'phone' &&
    (canvas.renderProfile !== 'mobile' || canvas.pixelRatio > 1.5)
  )
    throw new Error('Phone capture escaped the mobile renderer budget.');
  if (
    session.id !== 'phone' &&
    canvas.renderProfile !== 'desktop'
  )
    throw new Error(`${session.id} capture escaped the desktop renderer profile.`);
  return {
    id,
    matchId: match.id,
    viewport: session.viewport,
    mobile: session.mobile,
    reducedMotion: sample.reducedMotion,
    time: sample.time,
    arenaPixels: bounds && {
      width: Math.round(bounds.width),
      height: Math.round(bounds.height),
    },
    canvas,
    image: relative(file),
    imageSha256: sha256(readFileSync(file)),
  };
}

async function captureLifecycle(session, lifecycle) {
  const phases = [];
  await preferOperation(session.page, lifecycle.operationId);
  for (const beat of ['start', 'action', 'resolve']) {
    const time = lifecycle.captureTimes[beat];
    await seek(session.page, time);
    if (beat !== 'resolve') {
      // The approval surface is the default strategic view. Earlier proof zoomed into a
      // private signature camera and therefore proved geometry the actual replay could
      // not communicate; keep every lifecycle still on the camera the owner watches.
      await setCamera(session.page, 'overview');
    }
    const sample = await renderSample(session.page);
    const visible = sample.visibleSignatures.filter(
      (entry) => entry.signatureId === lifecycle.signatureId,
    );
    if (beat !== 'resolve' && visible.length === 0)
      throw new Error(
        `${lifecycle.signatureId} ${beat} has no visible WebGL signature rig.`,
      );
    if (visible.some((entry) => entry.visibleMeshes === 0))
      throw new Error(`${lifecycle.signatureId} has an empty visible rig.`);
    const file = path.join(
      output,
      `${lifecycle.signatureId}-${beat}.webp`,
    );
    await session.page
      .getByRole('region', { name: 'Arena' })
      .screenshot({ path: file, type: 'webp', quality: 90 });
    phases.push({
      beat,
      requestedTime: time,
      actualTime: sample.time,
      camera: 'strategic-overview',
      visible,
      image: relative(file),
      imageSha256: sha256(readFileSync(file)),
    });
  }
  return {
    signatureId: lifecycle.signatureId,
    matchId: lifecycle.matchId,
    operationId: lifecycle.operationId,
    phases,
  };
}

async function preferOperation(page, operationId) {
  await page.evaluate((value) => {
    const target = new URL(window.location.href);
    target.searchParams.set('proof-operation', value);
    target.searchParams.set('proof-camera', 'overview');
    window.history.replaceState(null, '', target);
  }, operationId);
}

async function compareDirectAndContinuousSeek(session, lifecycle) {
  await preferOperation(session.page, lifecycle.operationId);
  // The comparison is about replay-derived renderer state, not a camera spring that
  // advances in wall-clock time. Pin the ordinary strategic camera explicitly before
  // both samples, just as lifecycle and reduced-motion proof do.
  await setCamera(session.page, 'overview');
  const target = lifecycle.captureTimes.action;
  await seek(session.page, target);
  const direct = await screenshotBuffer(session.page);
  await seek(session.page, target - 0.55);
  await playFor(session.page, 220);
  await seek(session.page, target);
  await setCamera(session.page, 'overview');
  const afterPlayback = await screenshotBuffer(session.page);
  const directHash = sha256(direct);
  const afterPlaybackHash = sha256(afterPlayback);
  if (directHash !== afterPlaybackHash)
    throw new Error(`${lifecycle.signatureId}: direct seek frame differs after play.`);
  return {
    signatureId: lifecycle.signatureId,
    matchId: lifecycle.matchId,
    time: target,
    directSha256: directHash,
    afterPlaybackSha256: afterPlaybackHash,
    equal: true,
  };
}

async function captureReducedMotion(session, lifecycle) {
  await preferOperation(session.page, lifecycle.operationId);
  await seek(session.page, lifecycle.captureTimes.action);
  await setCamera(session.page, 'overview');
  await session.page.waitForTimeout(90);
  const sample = await renderSample(session.page);
  if (!sample.reducedMotion)
    throw new Error('Reduced-motion browser context did not reach the renderer.');
  const visible = sample.visibleSignatures.filter(
    (entry) => entry.signatureId === lifecycle.signatureId,
  );
  if (visible.length === 0 || visible.some((entry) => entry.visibleMeshes === 0))
    throw new Error(`${lifecycle.signatureId}: reduced motion lost public truth.`);
  const file = path.join(output, `${lifecycle.signatureId}-reduced.webp`);
  await session.page
    .getByRole('region', { name: 'Arena' })
    .screenshot({ path: file, type: 'webp', quality: 90 });
  return {
    signatureId: lifecycle.signatureId,
    matchId: lifecycle.matchId,
    time: sample.time,
    visible,
    image: relative(file),
    imageSha256: sha256(readFileSync(file)),
  };
}

async function captureLevelUp(session, match, fact) {
  await setCamera(session.page, 'director');
  const time = fact.tick + 0.32;
  await seek(session.page, time);
  const sample = await renderSample(session.page);
  if (sample.levelUpBeats < 1)
    throw new Error('Real leveled-up fact did not create a WebGL transient.');
  const file = path.join(output, 'level-up.webp');
  await session.page
    .getByRole('region', { name: 'Arena' })
    .screenshot({ path: file, type: 'webp', quality: 90 });
  const record = {
    matchId: match.id,
    fact,
    time: sample.time,
    visibleBeats: sample.levelUpBeats,
    image: relative(file),
    imageSha256: sha256(readFileSync(file)),
  };
  await setCamera(session.page, 'overview');
  return record;
}

async function seek(page, value) {
  await page.evaluate((time) =>
    window.__BOTARENA_REVIEW_CONTROL__?.seek(Number(time)),
  value);
  await page.waitForTimeout(70);
  const actual = (await renderSample(page)).time;
  if (Math.abs(actual - value) > 0.021)
    throw new Error(
      `Timeline seek missed ${value.toFixed(2)} by ` +
      `${Math.abs(actual - value).toFixed(3)} ticks.`,
    );
  await page.waitForTimeout(90);
}

async function playFor(page, milliseconds) {
  await page.evaluate(() => window.__BOTARENA_REVIEW_CONTROL__?.play());
  await page.waitForTimeout(milliseconds);
  await page.evaluate(() => window.__BOTARENA_REVIEW_CONTROL__?.pause());
}

async function renderSample(page) {
  const sample = await page.evaluate(() =>
    window.__BOTARENA_RENDER_PROOF__?.sample(),
  );
  if (!sample) throw new Error('WebGL review instrumentation is unavailable.');
  return sample;
}

async function screenshotBuffer(page) {
  return page.getByRole('region', { name: 'Arena' }).screenshot({
    animations: 'disabled',
    type: 'png',
  });
}

async function canvasRecord(arena) {
  return arena.locator('canvas').evaluate((node) => ({
    kind: node.getContext('2d') ? '2d' : 'webgl',
    width: node.width,
    height: node.height,
    clientWidth: node.clientWidth,
    clientHeight: node.clientHeight,
    renderProfile: node.dataset.renderProfile ?? null,
    pixelRatio: Number(node.dataset.pixelRatio ?? 0),
    shadowMapSize: Number(node.dataset.shadowMapSize ?? 0),
    activeFramesPerSecond: Number(node.dataset.activeFps ?? 0),
    idleFramesPerSecond: Number(node.dataset.idleFps ?? 0),
  }));
}

function chooseSeekSamples(lifecycles) {
  const requested = [
    'vector-dash',
    'repair-beam',
    'survey-flare',
    'null-field',
    'smoke-canister',
    'target-paint',
  ];
  return requested.map((signatureId) => {
    const found = lifecycles.find((entry) => entry.signatureId === signatureId);
    if (!found) throw new Error(`Missing seek sample ${signatureId}.`);
    return found;
  });
}

async function buildContactSheets(lifecycles) {
  const tiles = [];
  const width = 480;
  const height = 300;
  for (const lifecycle of lifecycles) {
    for (const phase of lifecycle.phases) {
      const input = readFileSync(absolute(phase.image));
      const label = `${lifecycle.signatureId}  ·  ${phase.beat}`;
      const labelSvg = Buffer.from(
        `<svg width="${width}" height="${height}" xmlns="http://www.w3.org/2000/svg">` +
        '<rect width="100%" height="32" fill="#080d12" fill-opacity="0.82"/>' +
        `<text x="12" y="21" fill="#e7eef2" font-size="14" ` +
        `font-family="ui-monospace,monospace">${label}</text></svg>`,
      );
      tiles.push({
        input: await sharp(input)
          .resize(width, height, { fit: 'cover' })
          .composite([{ input: labelSvg, left: 0, top: 0 }])
          .webp({ quality: 86 })
          .toBuffer(),
        left: (tiles.length % 3) * width,
        top: Math.floor(tiles.length / 3) * height,
      });
    }
  }
  const rows = Math.ceil(tiles.length / 3);
  const file = path.join(output, 'signature-lifecycle-contact-sheet.webp');
  await sharp({
    create: {
      width: width * 3,
      height: height * rows,
      channels: 4,
      background: '#080d12',
    },
  })
    .composite(tiles)
    .webp({ quality: 88 })
    .toFile(file);
}

function parseOptions(args) {
  const parsed = {};
  for (let index = 0; index < args.length; index += 2) {
    const name = args[index];
    const value = args[index + 1];
    if (!name?.startsWith('--') || value === undefined)
      throw new Error('Options must be --name value pairs.');
    parsed[name.slice(2)] = value;
  }
  return parsed;
}

function absolute(value) {
  return path.isAbsolute(value) ? value : path.join(repository, value);
}

function relative(value) {
  return path.relative(repository, value);
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}

function directorySha256(directory, include) {
  const hash = createHash('sha256');
  const visit = (current) => {
    for (const entry of readdirSync(current, { withFileTypes: true })
      .sort((left, right) => left.name.localeCompare(right.name))) {
      const entryPath = path.join(current, entry.name);
      if (entry.isDirectory()) {
        visit(entryPath);
      } else if (entry.isFile() && include(entryPath)) {
        hash.update(relativeTo(directory, entryPath));
        hash.update('\0');
        hash.update(readFileSync(entryPath));
        hash.update('\0');
      }
    }
  };
  visit(directory);
  return hash.digest('hex');
}

function isInstalledReplayFile(value) {
  return value === 'replay.json' || value === 'replay.json.gz' ||
    value === 'replays.json' || value === 'replays.json.gz' ||
    value.startsWith('replays/') || value.startsWith('replay-archive/');
}

function relativeTo(directory, value) {
  return path.relative(directory, value).split(path.sep).join('/');
}

function loadPriorEvidence() {
  if (!existsSync(browserProofPath)) return null;
  const prior = JSON.parse(readFileSync(browserProofPath, 'utf8'));
  return prior.schema === 'arc-relay-presentation-browser-proof-v1' &&
      prior.sourceRevision === manifest.sourceRevision &&
      prior.buildSha256 === buildSha256 &&
      prior.captureFramingVersion === captureFramingVersion &&
      prior.url === url
    ? prior
    : null;
}

function writeEvidence() {
  writeFileSync(browserProofPath, `${JSON.stringify(evidence, null, 2)}\n`);
}

function retained(entry, fallbackId) {
  if (!entry?.image || !existsSync(absolute(entry.image))) return false;
  const id = entry.id ?? fallbackId;
  return typeof id === 'string' &&
    sha256(readFileSync(absolute(entry.image))) === entry.imageSha256;
}

function retainedLifecycle(entry) {
  return entry?.phases?.length === 3 &&
    ['start', 'action', 'resolve'].every((beat) => {
      const phase = entry.phases.find((candidate) => candidate.beat === beat);
      return retained(phase, `${entry.signatureId}-${beat}`);
    });
}

function retainedSeek(signatureId) {
  const entry = evidence.seekComparisons.find(
    (candidate) => candidate.signatureId === signatureId,
  );
  return entry?.equal === true &&
    typeof entry.directSha256 === 'string' &&
    entry.directSha256 === entry.afterPlaybackSha256;
}

function desktopEvidenceMissing() {
  const overviewsComplete = manifest.matches.every((match) => {
    const id = `${match.id}-desktop`;
    return evidence.captures.some(
      (entry) => entry.id === id && retained(entry, id),
    );
  });
  const lifecycleComplete = manifest.lifecycleEvidence.every((lifecycle) =>
    retainedLifecycle(evidence.lifecycle.find(
      (entry) => entry.signatureId === lifecycle.signatureId,
    )),
  );
  const seeksComplete = seekSamples.every((lifecycle) =>
    retainedSeek(lifecycle.signatureId),
  );
  const reducedComplete = reducedMotionSamples.every((lifecycle) =>
    retained(evidence.reducedMotion.find(
      (entry) => entry.signatureId === lifecycle.signatureId,
    ), `${lifecycle.signatureId}-reduced`),
  );
  return !overviewsComplete || !lifecycleComplete || !seeksComplete ||
    !reducedComplete || !retained(evidence.levelUp, 'level-up');
}

function phoneEvidenceMissing() {
  return manifest.matches.some((match) => {
    const id = `${match.id}-phone`;
    return !evidence.captures.some(
      (entry) => entry.id === id && retained(entry, id),
    );
  });
}

function canonicalizeEvidence() {
  const captureOrder = [
    ...manifest.matches.map((match) => `${match.id}-desktop`),
    ...manifest.matches.map((match) => `${match.id}-phone`),
  ];
  evidence.captures = orderedBy(
    evidence.captures,
    'id',
    captureOrder,
  );
  evidence.lifecycle = orderedBy(
    evidence.lifecycle,
    'signatureId',
    manifest.lifecycleEvidence.map((entry) => entry.signatureId),
  );
  evidence.seekComparisons = orderedBy(
    evidence.seekComparisons,
    'signatureId',
    seekSamples.map((entry) => entry.signatureId),
  );
  evidence.reducedMotion = orderedBy(
    evidence.reducedMotion,
    'signatureId',
    reducedMotionSamples.map((entry) => entry.signatureId),
  );
}

function validateEvidence() {
  if (evidence.captures.length !== 4 ||
      evidence.captures.some((entry) => !retained(entry, entry.id)))
    throw new Error('Browser proof does not contain four valid device overviews.');
  if (evidence.lifecycle.length !== 16 ||
      evidence.lifecycle.some((entry) => !retainedLifecycle(entry)))
    throw new Error('Browser proof does not contain 16 valid signature lifecycles.');
  if (evidence.seekComparisons.length !== 6 ||
      evidence.seekComparisons.some((entry) => !retainedSeek(entry.signatureId)))
    throw new Error('Browser proof does not contain six reproducible seeks.');
  if (evidence.reducedMotion.length !== 4 ||
      evidence.reducedMotion.some(
        (entry) => !retained(entry, `${entry.signatureId}-reduced`),
      ))
    throw new Error('Browser proof does not contain four reduced-motion frames.');
  if (!retained(evidence.levelUp, 'level-up'))
    throw new Error('Browser proof does not contain a valid level-up frame.');
}

function orderedBy(values, key, identities) {
  const byIdentity = new Map(values.map((value) => [value[key], value]));
  return identities.map((identity) => byIdentity.get(identity)).filter(Boolean);
}

function replaceBy(values, key, identity, replacement) {
  return [
    ...values.filter((value) => value[key] !== identity),
    replacement,
  ];
}
