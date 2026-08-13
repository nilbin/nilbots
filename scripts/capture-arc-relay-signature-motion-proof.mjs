#!/usr/bin/env node

/** Record deterministic ordinary gameplay-camera motion clips for Arc signatures. */

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
import { spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import os from 'node:os';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const options = parseOptions(process.argv.slice(2));
if (options['owner-requested-full-proof'] !== 'true')
  throw new Error(
    'Full signature motion proof is owner-request-only. Re-run only after an explicit ' +
    'owner request with --owner-requested-full-proof true.',
  );
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');
const ffmpeg = resolveFfmpeg();
const only = new Set(
  String(options.only ?? '')
    .split(',')
    .map((value) => value.trim())
    .filter(Boolean),
);
const cameraMode = options.camera === 'overview' ? 'overview' : 'director';
const cameraLabel = cameraMode === 'overview'
  ? 'ordinary-strategic-overview'
  : 'ordinary-director';
const proofDirectory = absolute(options.proof ??
  'art/reviews/arc-relay-presentation-batch-v1/runtime-proof');
const manifest = JSON.parse(readFileSync(path.join(proofDirectory, 'proof-manifest.json'), 'utf8'));
const dist = path.join(repository, 'web', 'dist-review');
const replayChoices = JSON.parse(
  readFileSync(path.join(dist, 'replays.json'), 'utf8'),
);
const buildSha256 = directorySha256(dist, (entryPath) =>
  !isInstalledReplayFile(path.relative(dist, entryPath).split(path.sep).join('/')),
);
const captureVersion = 29;
const output = absolute(options.output ?? path.join(
  relative(proofDirectory),
  cameraMode === 'overview' ? 'motion-overview' : 'motion',
));
const evidencePath = path.join(output, 'evidence.json');
const url = options.url ??
  `http://127.0.0.1:9093/web/dist-review/?standalone&audio=off&camera=${cameraMode}`;
const clips = manifest.lifecycleEvidence.map((lifecycle) => {
  const startTime = Math.max(0, lifecycle.captureTimes.start - 0.35);
  // A persistent field can live for thirty ticks, while its authored deployment occupies
  // the first one or two. Sampling the whole lifetime made the motion proof five copies of
  // the settled prop. Keep the clip on tell/travel/deploy/resolve, and leave the separately
  // retained lifecycle stills to prove expiry and capacity later in the operation.
  const startedTick = lifecycle.facts?.find((fact) => fact.reason === 'started')?.tick ??
    Math.floor(lifecycle.captureTimes.start);
  const landedTick = lifecycle.facts?.find((fact) => fact.reason === 'landed')?.tick;
  const maturedTick = lifecycle.facts?.find((fact) =>
    ['activated', 'acquired', 'deployed'].includes(fact.reason),
  )?.tick;
  const terminalTick = lifecycle.facts?.find(
    (fact) => fact.phase === null && fact.reason !== 'expired',
  )?.tick;
  const deploymentEnd = startedTick + 2.35;
  const travelAndDeployEnd = landedTick === undefined ? Infinity : landedTick + 1.35;
  const oneShotOutcomeEnd = terminalTick === undefined ? Infinity : terminalTick + 1.55;
  const beginsAlreadyActive = lifecycle.facts?.[0]?.phase === 'active';
  return {
    ...lifecycle,
    startTime,
    endTime: Math.min(
      lifecycle.captureTimes.resolve + 0.82,
      beginsAlreadyActive ? deploymentEnd : Infinity,
      travelAndDeployEnd,
      maturedTick === undefined ? Infinity : maturedTick + 1.35,
      oneShotOutcomeEnd,
    ),
  };
});
const prior = existsSync(evidencePath)
  ? JSON.parse(readFileSync(evidencePath, 'utf8'))
  : null;
const evidence = {
  schema: 'arc-relay-signature-motion-proof-v1',
  sourceRevision: manifest.sourceRevision,
  buildSha256,
  captureVersion,
  url,
  camera: cameraLabel,
  playbackSpeed: 1,
  clock: 'deterministic-1x-frame-step',
  passed: false,
  captures:
    prior?.buildSha256 === buildSha256 && prior?.captureVersion === captureVersion
      ? prior.captures ?? []
      : [],
  errors: [],
};
mkdirSync(output, { recursive: true });

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 });
let page = null;

try {
  let selected = null;
  await openProofPage();
  for (const clip of clips) {
    const retained = evidence.captures.find((entry) => entry.signatureId === clip.signatureId);
    if (valid(retained)) {
      console.log(`Retained ${clip.signatureId}.`);
      continue;
    }
    if (only.size > 0 && !only.has(clip.signatureId)) {
      console.log(`Skipped ${clip.signatureId}; outside --only.`);
      continue;
    }
    if (selected !== clip.matchId) {
      await selectReplay(clip.matchId);
      selected = clip.matchId;
    }
    await preferOperation(clip.operationId, clip.startTime);
    const record = await capture(clip);
    evidence.captures = replaceBy(evidence.captures, 'signatureId', clip.signatureId, record);
    writeEvidence();
    console.log(`Captured ${clip.signatureId}: ${record.durationSeconds.toFixed(2)}s ${cameraLabel}.`);
  }
  if (evidence.errors.length > 0)
    throw new Error(`Browser errors:\n${evidence.errors.join('\n')}`);
  evidence.captures.sort((left, right) => left.signatureId.localeCompare(right.signatureId));
  evidence.passed = only.size === 0 && evidence.captures.length === clips.length && evidence.captures.every(valid);
  if (only.size === 0 && !evidence.passed) throw new Error('Motion proof is incomplete.');
} catch (error) {
  evidence.failure = String(error?.stack ?? error);
  throw error;
} finally {
  writeEvidence();
  await page?.close();
  await context.close();
  await browser.close();
}

async function openProofPage() {
  page = await context.newPage();
  page.on('pageerror', (error) => evidence.errors.push(`page: ${error.message}`));
  page.on('console', (message) => {
    if (message.type() === 'error') evidence.errors.push(`console: ${message.text()}`);
  });
  page.on('requestfailed', (request) => evidence.errors.push(
    `request: ${request.url()} ${request.failure()?.errorText ?? ''}`,
  ));
  await page.addInitScript(() => {
    localStorage.setItem('nilbots.soundtrack.enabled.v1', 'false');
    try {
      Object.defineProperty(Element.prototype, 'requestFullscreen', {
        configurable: true,
        value: undefined,
      });
    } catch {
      // CSS immersive mode is sufficient for deterministic review capture.
    }
  });
  await page.goto(
    `${url}&proof=ordinary-motion`,
    { waitUntil: 'domcontentloaded' },
  );
  await ready();
}

async function preferOperation(operationId, startTime) {
  await page.evaluate(({ value, time }) => {
    const target = new URL(window.location.href);
    target.searchParams.set('proof-operation', value);
    target.searchParams.set(
      'proof-camera',
      target.searchParams.get('camera') === 'overview' ? 'overview' : 'director',
    );
    window.history.replaceState(null, '', target);
    // Make the director observe the preference while this operation is public. If the
    // query changes at an unrelated playhead, a prior target can survive its deadband and
    // the proof records the right effect at the wrong theater.
    window.__BOTARENA_REVIEW_CONTROL__?.seek(time);
  }, { value: operationId, time: startTime });
  await page.waitForTimeout(70);
}

async function ready(expectedTicks) {
  const overlay = page.locator('[data-play-overlay]');
  if (await overlay.count())
    await page.locator('[data-play-overlay="ready"]').waitFor({ timeout: 60_000 });
  const canvas = page.getByRole('region', { name: 'Arena' }).locator('canvas');
  await canvas.waitFor({ timeout: 60_000 });
  const kind = await canvas.evaluate((node) => node.getContext('2d') ? '2d' : 'webgl');
  if (kind !== 'webgl') throw new Error(`Expected WebGL, mounted ${kind}.`);
  await page.waitForFunction(
    (ticks) => {
      const control = window.__BOTARENA_REVIEW_CONTROL__;
      const proof = window.__BOTARENA_RENDER_PROOF__?.sample();
      return Boolean(control && proof) &&
        (ticks === undefined ||
          (control.tickCount === ticks && proof.tickCount === ticks));
    },
    expectedTicks,
    { timeout: 60_000 },
  );
  if (await overlay.isVisible()) {
    await page.getByRole('button', { name: 'Play match' }).click();
    await page.getByRole('button', { name: 'Pause' }).evaluate((button) => button.click());
    await page.waitForFunction(
      () => window.__BOTARENA_REVIEW_CONTROL__?.playing === false,
      undefined,
      { timeout: 10_000 },
    );
  }
  const camera = page.getByRole('button', { name: /director|overview/i });
  const current = (await camera.textContent())?.toLowerCase() ?? '';
  if (cameraMode === 'director' && current.includes('overview')) await camera.click();
  if (cameraMode === 'overview' && current.includes('director')) await camera.click();
  await page.getByRole('button', {
    name: cameraMode === 'director' ? /director/i : /overview/i,
  }).waitFor();
  const normal = page.getByRole('button', { name: '1×', exact: true });
  if ((await normal.getAttribute('aria-pressed')) !== 'true') await normal.click();
}

async function selectReplay(matchId) {
  const choice = replayChoices.find((entry) => entry.id === matchId);
  if (!choice) throw new Error(`Unknown replay ${matchId}.`);
  const button = page
    .getByRole('navigation', { name: 'Choose a replay to review' })
    .getByRole('button')
    .filter({ has: page.locator(`span:text-is("${choice.bots.join(' v ')}")`) });
  if ((await button.getAttribute('aria-current')) !== 'true') {
    console.log(`Selecting ${matchId} (${choice.ticks} ticks).`);
    await button.click();
    await page.waitForFunction(
      (label) => [...document.querySelectorAll('button')].some(
        (candidate) => candidate.getAttribute('aria-current') === 'true' &&
          candidate.textContent?.includes(label),
      ),
      choice.bots.join(' v '),
    );
  }
  console.log(`Waiting for ${matchId} renderer identity.`);
  await waitForReplayIdentity(choice);
  const overlay = page.locator('[data-play-overlay]');
  if (await overlay.isVisible()) {
    await page.getByRole('button', { name: 'Play match' }).click();
    await page.getByRole('button', { name: 'Pause' }).evaluate((button) => button.click());
    await page.waitForFunction(
      () => window.__BOTARENA_REVIEW_CONTROL__?.playing === false,
      undefined,
      { timeout: 10_000 },
    );
  }
  console.log(`Renderer identity settled for ${matchId}.`);
}

async function waitForReplayIdentity(choice) {
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

async function capture(clip) {
  const durationTicks = Math.max(0.5, clip.endTime - clip.startTime);
  const expectedSeconds = durationTicks / 2.5;
  const arena = page.getByRole('region', { name: 'Arena' });
  const canvas = arena.locator('canvas');
  const captureFramesPerSecond = 5;
  const frameCount = Math.max(6, Math.ceil(expectedSeconds * captureFramesPerSecond) + 1);
  const times = Array.from({ length: frameCount }, (_, index) =>
    clip.startTime + durationTicks * index / (frameCount - 1));
  const frames = [];
  const observedPhases = new Set();
  let maxVisibleMeshes = 0;
  let identity = null;
  for (const time of times) {
    await seekAndSettle(time);
    const current = await page.evaluate(() => window.__BOTARENA_RENDER_PROOF__?.sample());
    identity = current;
    for (const signature of current?.visibleSignatures ?? []) {
      if (signature.operationId !== clip.operationId) continue;
      if (signature.phase !== null) observedPhases.add(signature.phase);
      maxVisibleMeshes = Math.max(maxVisibleMeshes, signature.visibleMeshes);
    }
    frames.push(await canvas.screenshot({ type: 'png' }));
  }
  const videoPath = path.join(output, `${clip.signatureId}.webm`);
  await encodeFrames(frames, captureFramesPerSecond, videoPath);
  const video = readFileSync(videoPath);
  const contactPath = path.join(output, `${clip.signatureId}-contact.webp`);
  const posterPath = path.join(output, `${clip.signatureId}-poster.webp`);
  const media = await contactSheet(video, contactPath, posterPath);
  const sample = identity;
  return {
    signatureId: clip.signatureId,
    matchId: clip.matchId,
    operationId: clip.operationId,
    proofOperation: clip.operationId,
    startTime: clip.startTime,
    endTime: clip.endTime,
    durationSeconds: media.duration,
    authoredWindow: true,
    camera: cameraLabel,
    clock: 'deterministic-1x-frame-step',
    frameCount,
    frameRate: captureFramesPerSecond,
    video: relative(videoPath),
    videoBytes: video.length,
    videoSha256: sha256(video),
    poster: relative(posterPath),
    posterSha256: sha256(readFileSync(posterPath)),
    contactSheet: relative(contactPath),
    contactSheetSha256: sha256(readFileSync(contactPath)),
    finalTime: sample?.time ?? null,
    replayTickCount: sample?.tickCount ?? null,
    replaySeed: sample?.replaySeed ?? null,
    observedPhases: [...observedPhases].sort(),
    maxVisibleMeshes,
    finalCameraFrame: sample?.cameraFrame ?? null,
  };
}

async function encodeFrames(frames, framesPerSecond, destination) {
  const temporary = mkdtempSync(path.join(os.tmpdir(), 'nilbots-signature-motion-'));
  try {
    const decoded = await Promise.all(frames.map((frame) =>
      sharp(frame).removeAlpha().toColourspace('srgb').raw().toBuffer({ resolveWithObject: true })));
    const [{ info }] = decoded;
    if (!decoded.every((entry) =>
      entry.info.width === info.width && entry.info.height === info.height && entry.info.channels === 3))
      throw new Error('Motion-proof frames do not share one RGB extent.');
    const rawPath = path.join(temporary, 'frames.rgb');
    writeFileSync(rawPath, Buffer.concat(decoded.map((entry) => entry.data)));
    const result = spawnSync(ffmpeg, [
      '-hide_banner', '-loglevel', 'error', '-y',
      '-f', 'rawvideo', '-pixel_format', 'rgb24',
      '-video_size', `${info.width}x${info.height}`,
      '-framerate', String(framesPerSecond),
      '-i', rawPath,
      '-c:v', 'libvpx', '-b:v', '4M', '-pix_fmt', 'yuv420p', destination,
    ], { encoding: 'utf8' });
    if (result.status !== 0)
      throw new Error(`ffmpeg could not encode motion proof: ${result.stderr}`);
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

function resolveFfmpeg() {
  const candidates = [
    process.env.FFMPEG,
    'ffmpeg',
    '/usr/local/share/CentraStage/AEMAgent/RMM.WebRemote/15.0.1.11713/ffmpeg/ffmpeg',
  ].filter(Boolean);
  for (const candidate of candidates) {
    const result = spawnSync(candidate, ['-version'], { stdio: 'ignore' });
    if (result.status === 0) return candidate;
  }
  throw new Error('Motion proof requires ffmpeg (or FFMPEG=/absolute/path).');
}

async function waitForProofTime(expected) {
  const deadline = Date.now() + 10_000;
  while (Date.now() < deadline) {
    const actual = await page.evaluate(() => window.__BOTARENA_RENDER_PROOF__?.sample().time);
    if (Number.isFinite(actual) && Math.abs(actual - expected) < 0.025) return;
    // A replay switch can replace the review bridge after the first seek. Seek through the
    // current bridge until the WebGL sample confirms the requested frame.
    await page.evaluate((time) => window.__BOTARENA_REVIEW_CONTROL__?.seek(time), expected);
    await page.waitForTimeout(80);
  }
  throw new Error(`WebGL did not settle at proof tick ${expected}.`);
}

async function seekAndSettle(expected) {
  await page.evaluate((time) => window.__BOTARENA_REVIEW_CONTROL__?.seek(time), expected);
  await waitForProofTime(expected);
  // Wait through two draws after React publishes the exact playhead. The second frame is
  // the one screenshot tooling retains; this also lets model transforms and overlays use
  // the same timestamp after a gallery replay switch.
  await page.waitForTimeout(55);
}

async function contactSheet(video, destination, posterDestination) {
  const inspection = await context.newPage();
  try {
    const source = `data:video/webm;base64,${video.toString('base64')}`;
    await inspection.setContent('<video muted playsinline></video><canvas></canvas>');
    const metadata = await inspection.evaluate(async (dataUrl) => {
      const node = document.querySelector('video');
      node.src = dataUrl;
      await new Promise((resolve, reject) => {
        node.addEventListener('loadedmetadata', resolve, { once: true });
        node.addEventListener('error', reject, { once: true });
      });
      if (!Number.isFinite(node.duration)) {
        node.currentTime = 1e7;
        await new Promise((resolve) => node.addEventListener('seeked', resolve, { once: true }));
        node.currentTime = 0;
        await new Promise((resolve) => node.addEventListener('seeked', resolve, { once: true }));
      }
      return { duration: node.duration, width: node.videoWidth, height: node.videoHeight };
    }, source);
    const times = [0.12, 0.32, 0.52, 0.72, 0.9].map((fraction) =>
      Math.max(0, Math.min(metadata.duration - 0.03, metadata.duration * fraction)));
    const frames = [];
    for (const at of times) {
      const png = await inspection.evaluate(async (time) => {
        const video = document.querySelector('video');
        const canvas = document.querySelector('canvas');
        video.currentTime = time;
        await new Promise((resolve) => video.addEventListener('seeked', resolve, { once: true }));
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        canvas.getContext('2d').drawImage(video, 0, 0);
        return canvas.toDataURL('image/png');
      }, at);
      frames.push(Buffer.from(png.slice(png.indexOf(',') + 1), 'base64'));
    }
    await sharp(frames[Math.floor(frames.length / 2)])
      .webp({ quality: 90 })
      .toFile(posterDestination);
    const width = 540;
    const height = Math.round(width * metadata.height / metadata.width);
    const composites = await Promise.all(frames.map(async (input, index) => ({
      input: await sharp(input).resize(width, height).webp({ quality: 86 }).toBuffer(),
      left: index * width,
      top: 0,
    })));
    await sharp({
      create: { width: width * frames.length, height, channels: 4, background: '#080d12' },
    }).composite(composites).webp({ quality: 88 }).toFile(destination);
    return metadata;
  } finally {
    await inspection.close();
  }
}

function valid(entry) {
  const replayChoice = replayChoices.find((choice) => choice.id === entry?.matchId);
  return entry?.camera === cameraLabel &&
    entry?.clock === 'deterministic-1x-frame-step' &&
    entry?.proofOperation === entry?.operationId &&
    replayChoice !== undefined &&
    entry?.replayTickCount === replayChoice.ticks &&
    typeof entry?.replaySeed === 'string' &&
    entry.replaySeed.length > 0 &&
    Array.isArray(entry?.observedPhases) &&
    entry.observedPhases.length > 0 &&
    entry?.maxVisibleMeshes > 0 &&
    Number.isFinite(entry?.finalTime) &&
    entry.finalTime >= entry.endTime - 0.02 &&
    entry.finalTime <= entry.endTime + 1 &&
    entry.finalCameraFrame !== null &&
    existsSync(absolute(entry.video)) &&
    existsSync(absolute(entry.poster)) &&
    existsSync(absolute(entry.contactSheet)) &&
    sha256(readFileSync(absolute(entry.video))) === entry.videoSha256 &&
    sha256(readFileSync(absolute(entry.poster))) === entry.posterSha256 &&
    sha256(readFileSync(absolute(entry.contactSheet))) === entry.contactSheetSha256;
}

function replaceBy(values, key, id, value) {
  return [...values.filter((entry) => entry[key] !== id), value];
}

function writeEvidence() {
  writeFileSync(evidencePath, `${JSON.stringify(evidence, null, 2)}\n`);
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
      if (entry.isDirectory()) visit(entryPath);
      else if (entry.isFile() && include(entryPath)) {
        hash.update(path.relative(directory, entryPath).split(path.sep).join('/'));
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
