#!/usr/bin/env node

/** Capture optimized static GLB tiers through an existing production review build.
 *
 * The build keeps its original hashed GLB URL. The evidence server substitutes one
 * hash-pinned candidate at that exact URL, so renderer, replay, camera, lighting and UI
 * stay unchanged while only the model bytes vary. A JSON spec supplies immutable replay
 * and model inputs; it belongs in sandbox unless it describes an accepted permanent gate.
 */

import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { mkdir, readFile, readdir, stat, writeFile } from 'node:fs/promises';
import { createServer } from 'node:http';
import { createRequire } from 'node:module';
import { dirname, extname, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repository = resolve(scriptDirectory, '..', '..');
const options = parseArguments(process.argv.slice(2));
const specPath = resolve(repository, options.spec);
const spec = JSON.parse(await readFile(specPath, 'utf8'));
const dist = resolvePath(spec.dist);
const output = resolvePath(spec.output);
const requireFromWeb = createRequire(
  resolvePath(spec.webPackage ?? 'web/package.json'),
);
const { chromium } = requireFromWeb('playwright');
const { createCanvas, loadImage } = requireFromWeb('@napi-rs/canvas');
const profiles = spec.profiles ?? [
  { id: 'desktop', width: 1600, height: 1000, deviceScaleFactor: 1 },
  { id: 'phone-landscape', width: 844, height: 500, deviceScaleFactor: 2 },
];

await mkdir(join(output, 'captures'), { recursive: true });
await stat(join(dist, 'index.html'));
const cases = await preflightCases(spec.cases);
const server = await serve(dist);
const browser = await chromium.launch({
  headless: true,
  args: ['--use-angle=swiftshader'],
});
const captures = [];

try {
  for (const entry of cases) {
    for (const candidate of entry.candidates) {
      server.select({
        replay: entry.replayBytes,
        pathname: `/${entry.distModel.path}`,
        model: candidate.bytes,
      });
      for (const profile of profiles) {
        progress(`${entry.id}/${candidate.id}/${profile.id}`);
        captures.push(
          await capture(browser, server, entry, candidate, profile),
        );
      }
    }
  }
} finally {
  await browser.close();
  await new Promise((accept) => server.close(accept));
}

const contactSheets = [];
for (const profile of profiles) {
  contactSheets.push(await contactSheet(cases, captures, profile));
}
const report = {
  schemaVersion: 1,
  verdict: 'capture-complete-owner-review-required',
  rendererMutationByHarness: false,
  replayMutation: false,
  modelMutationByHarness: false,
  substitution:
    'Candidate bytes served at the production build original hashed GLB URL.',
  spec: displayPath(specPath),
  dist: displayPath(dist),
  profiles,
  cases: cases.map(({ replayBytes: _replayBytes, ...entry }) => ({
    ...entry,
    candidates: entry.candidates.map(({ bytes: _bytes, ...candidate }) => candidate),
  })),
  captures,
  contactSheets,
};
const reportPath = join(output, 'capture-report.json');
await writeFile(reportPath, `${JSON.stringify(report, null, 2)}\n`);
process.stdout.write(
  `${JSON.stringify(
    {
      report: displayPath(reportPath),
      contactSheets: contactSheets.map(({ path, sha256 }) => ({ path, sha256 })),
      captureCount: captures.length,
    },
    null,
    2,
  )}\n`,
);

async function preflightCases(rawCases) {
  const assets = await readdir(join(dist, 'assets'), { withFileTypes: true });
  const glbs = assets.filter((entry) => entry.isFile() && entry.name.endsWith('.glb'));
  const digestCache = new Map();
  const results = [];
  for (const raw of rawCases) {
    const sourcePath = resolvePath(raw.sourceModel.file);
    const replayPath = resolvePath(raw.replay.file);
    const [sourceBytes, replayBytes] = await Promise.all([
      readFile(sourcePath),
      readFile(replayPath),
    ]);
    assertEqual(sha256(sourceBytes), raw.sourceModel.sha256, `${raw.id} source model`);
    assertEqual(sha256(replayBytes), raw.replay.sha256, `${raw.id} replay file`);
    const replay = JSON.parse(replayBytes);
    assertEqual(replay.replayHash, raw.replay.canonicalHash, `${raw.id} replay hash`);
    const tick = replay.ticks?.[Math.floor(raw.replay.time)];
    const life = tick?.postState?.activeLives?.find(
      (candidate) =>
        candidate.actorId.teamId === raw.replay.actor.teamId &&
        candidate.actorId.unitId === raw.replay.actor.unitId &&
        candidate.actorId.lifeId === raw.replay.actor.lifeId,
    );
    if (!life) throw new Error(`${raw.id} actor is not active at ${raw.replay.time}.`);
    assertEqual(life.formId, raw.replay.actor.formId, `${raw.id} actor form`);

    let distModel = null;
    for (const asset of glbs) {
      const path = join(dist, 'assets', asset.name);
      const metadata = await stat(path);
      if (metadata.size !== sourceBytes.length) continue;
      let digest = digestCache.get(path);
      if (!digest) {
        digest = sha256(await readFile(path));
        digestCache.set(path, digest);
      }
      if (digest === raw.sourceModel.sha256) {
        distModel = {
          path: relative(dist, path),
          bytes: metadata.size,
          sha256: digest,
        };
        break;
      }
    }
    if (!distModel) throw new Error(`${raw.id} source GLB is absent from the review build.`);

    const candidates = [];
    for (const rawCandidate of raw.candidates) {
      const path = resolvePath(rawCandidate.file);
      const bytes = await readFile(path);
      const digest = sha256(bytes);
      if (rawCandidate.sha256) assertEqual(digest, rawCandidate.sha256, `${raw.id}/${rawCandidate.id}`);
      const glb = parseGlb(bytes);
      if ((glb.animations ?? 0) !== 0 || (glb.skins ?? 0) !== 0)
        throw new Error(`${raw.id}/${rawCandidate.id} is not a static GLB.`);
      candidates.push({
        id: rawCandidate.id,
        label: rawCandidate.label ?? rawCandidate.id,
        file: displayPath(path),
        size: bytes.length,
        sha256: digest,
        glb,
        bytes,
      });
    }
    results.push({
      id: raw.id,
      label: raw.label,
      sourceModel: {
        file: displayPath(sourcePath),
        bytes: sourceBytes.length,
        sha256: raw.sourceModel.sha256,
      },
      distModel,
      replay: { ...raw.replay, file: displayPath(replayPath) },
      replayBytes,
      candidates,
    });
  }
  return results;
}

async function capture(browser, server, entry, candidate, profile) {
  const capturePath = join(
    output,
    'captures',
    `${entry.id}-${candidate.id}-${profile.id}.png`,
  );
  if (options.resume) {
    try {
      const bytes = await readFile(capturePath);
      if (bytes.length >= 20_000) {
        const image = await loadImage(capturePath);
        return {
          id: `${entry.id}-${candidate.id}-${profile.id}`,
          classId: entry.id,
          classLabel: entry.label,
          tierId: candidate.id,
          tierLabel: candidate.label,
          profileId: profile.id,
          time: entry.replay.time,
          path: displayPath(capturePath),
          bytes: bytes.length,
          sha256: sha256(bytes),
          modelRequest: {
            pathname: `/${entry.distModel.path}`,
            status: 200,
            bytes: candidate.size,
          },
          resumedFromSameSpec: true,
          width: image.width,
          height: image.height,
          cssWidth: profile.width,
          cssHeight: profile.height,
          devicePixelRatio: profile.deviceScaleFactor,
        };
      }
    } catch {
      // No complete capture to resume.
    }
  }
  const context = await browser.newContext({
    viewport: { width: profile.width, height: profile.height },
    deviceScaleFactor: profile.deviceScaleFactor,
  });
  const page = await context.newPage();
  const pageErrors = [];
  const consoleErrors = [];
  const modelResponses = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });
  page.on('response', (response) => {
    const pathname = new URL(response.url()).pathname;
    if (!pathname.endsWith('.glb')) return;
    modelResponses.push({
      pathname,
      status: response.status(),
      bytes: Number(response.headers()['content-length'] ?? 0),
    });
  });
  await page.addInitScript(() => {
    localStorage.setItem('nilbots.soundtrack.enabled.v1', 'false');
  });

  try {
    await page.goto(address(server), {
      waitUntil: 'domcontentloaded',
      timeout: 60_000,
    });
    const arena = page.locator('section[aria-label="Arena"]');
    const canvas = arena.locator('canvas');
    await canvas.waitFor({ state: 'visible', timeout: 30_000 });
    const play = page.getByRole('button', { name: 'Play match', exact: true });
    await play.waitFor({ state: 'visible', timeout: 180_000 });
    await play.click();
    await page.locator('[data-play-overlay]').waitFor({ state: 'detached', timeout: 30_000 });
    const time = await preciseSeek(page, entry.replay.time);

    const card = page.locator('article').filter({
      hasText: `team ${entry.replay.actor.teamId} · unit ${entry.replay.actor.unitId}`,
    });
    if ((await card.count()) !== 1)
      throw new Error(`${entry.id} actor did not resolve to exactly one unit card.`);
    const inspect = card.getByRole('button', { name: 'Inspect', exact: true });
    await inspect.scrollIntoViewIfNeeded();
    await inspect.click();
    const fit = page.getByRole('button', { name: /fit/i });
    if ((await fit.count()) === 1 && (await fit.getAttribute('aria-pressed')) !== 'true')
      await fit.click();
    await page.waitForTimeout(profile.settleMilliseconds ?? 2_000);

    if (pageErrors.length > 0) throw new Error(pageErrors.join('\n'));
    const expectedPath = `/${entry.distModel.path}`;
    const fetched = modelResponses.find(
      (response) =>
        response.pathname === expectedPath &&
        response.status === 200 &&
        response.bytes === candidate.size,
    );
    if (!fetched) {
      throw new Error(
        `${entry.id}/${candidate.id} did not fetch substituted GLB: ${JSON.stringify(modelResponses)}`,
      );
    }

    await canvas.evaluate((element) =>
      element.scrollIntoView({ block: 'center', inline: 'center', behavior: 'instant' }),
    );
    await page.waitForTimeout(120);
    const box = await canvas.boundingBox();
    if (!box) throw new Error('Arena canvas has no visible bounds.');
    const cdp = await context.newCDPSession(page);
    const screenshot = await cdp.send('Page.captureScreenshot', {
      format: 'png',
      fromSurface: true,
      captureBeyondViewport: false,
    });
    await cdp.detach();
    const viewportBytes = Buffer.from(screenshot.data, 'base64');
    const viewportImage = await loadImage(viewportBytes);
    const scaleX = viewportImage.width / profile.width;
    const scaleY = viewportImage.height / profile.height;
    const left = Math.max(0, box.x);
    const top = Math.max(0, box.y);
    const right = Math.min(profile.width, box.x + box.width);
    const bottom = Math.min(profile.height, box.y + box.height);
    if (right - left < 100 || bottom - top < 100)
      throw new Error('Arena canvas is outside the captured viewport.');
    const crop = createCanvas(
      Math.round((right - left) * scaleX),
      Math.round((bottom - top) * scaleY),
    );
    crop.getContext('2d').drawImage(
      viewportImage,
      Math.round(left * scaleX),
      Math.round(top * scaleY),
      Math.round((right - left) * scaleX),
      Math.round((bottom - top) * scaleY),
      0,
      0,
      crop.width,
      crop.height,
    );
    const bytes = crop.toBuffer('image/png');
    await writeFile(capturePath, bytes);
    if (bytes.length < 20_000)
      throw new Error(`${displayPath(capturePath)} appears blank.`);
    const metrics = await canvas.evaluate((element) => ({
      width: element.width,
      height: element.height,
      cssWidth: element.clientWidth,
      cssHeight: element.clientHeight,
      devicePixelRatio: window.devicePixelRatio,
    }));
    return {
      id: `${entry.id}-${candidate.id}-${profile.id}`,
      classId: entry.id,
      classLabel: entry.label,
      tierId: candidate.id,
      tierLabel: candidate.label,
      profileId: profile.id,
      time,
      path: displayPath(capturePath),
      bytes: bytes.length,
      sha256: sha256(bytes),
      modelRequest: fetched,
      consoleErrors,
      ...metrics,
    };
  } finally {
    await context.close();
  }
}

async function preciseSeek(page, target) {
  const pause = page.getByRole('button', { name: 'Pause', exact: true });
  if (await pause.isVisible()) await pause.click();
  const timeline = page.locator('[aria-label="Match timeline — drag to seek"]');
  const thumb = page.getByRole('slider', { name: 'Playhead' });
  await timeline.waitFor({ state: 'visible' });
  const bounds = await timeline.boundingBox();
  if (!bounds) throw new Error('Timeline has no bounds.');
  const maximum = Number(await thumb.getAttribute('aria-valuemax'));
  await page.mouse.click(
    bounds.x + (bounds.width * target) / maximum,
    bounds.y + bounds.height / 2,
  );
  await thumb.focus();
  let current = Number(await thumb.getAttribute('aria-valuenow'));
  const steps = Math.round((target - current) / 0.01);
  if (Math.abs(steps) > 240) throw new Error(`Pointer seek reached ${current}, expected ${target}.`);
  const key = steps < 0 ? 'ArrowLeft' : 'ArrowRight';
  for (let index = 0; index < Math.abs(steps); index += 1) await page.keyboard.press(key);
  current = Number(await thumb.getAttribute('aria-valuenow'));
  if (Math.abs(current - target) > 0.001)
    throw new Error(`Playhead reports ${current}, expected ${target}.`);
  return current;
}

async function contactSheet(entries, allCaptures, profile) {
  const tiers = entries[0].candidates.map(({ id, label }) => ({ id, label }));
  const cellWidth = profile.id === 'desktop' ? 500 : 422;
  const cellHeight = profile.id === 'desktop' ? 313 : 250;
  const labelHeight = 52;
  const gap = 14;
  const width = tiers.length * cellWidth + (tiers.length + 1) * gap;
  const height = entries.length * (cellHeight + labelHeight) + (entries.length + 1) * gap;
  const canvas = createCanvas(width, height);
  const context = canvas.getContext('2d');
  context.fillStyle = '#080d13';
  context.fillRect(0, 0, width, height);
  context.fillStyle = '#edf5fa';
  context.font = '600 18px ui-monospace, monospace';
  context.textBaseline = 'middle';
  context.imageSmoothingEnabled = true;
  context.imageSmoothingQuality = 'high';

  for (const [row, entry] of entries.entries()) {
    for (const [column, tier] of tiers.entries()) {
      const capture = allCaptures.find(
        (item) =>
          item.classId === entry.id &&
          item.tierId === tier.id &&
          item.profileId === profile.id,
      );
      if (!capture) throw new Error(`Missing ${entry.id}/${tier.id}/${profile.id}.`);
      const left = gap + column * (cellWidth + gap);
      const top = gap + row * (cellHeight + labelHeight + gap);
      const source = await loadImage(resolvePath(capture.path));
      context.drawImage(source, left, top, cellWidth, cellHeight);
      context.strokeStyle = '#40566a';
      context.lineWidth = 2;
      context.strokeRect(left + 1, top + 1, cellWidth - 2, cellHeight - 2);
      context.fillText(
        `${entry.label} · ${tier.label}`,
        left + 8,
        top + cellHeight + labelHeight / 2,
      );
    }
  }
  const path = join(output, `static-model-tiers-${profile.id}.png`);
  const bytes = canvas.toBuffer('image/png');
  await writeFile(path, bytes);
  return {
    profileId: profile.id,
    path: displayPath(path),
    bytes: bytes.length,
    sha256: sha256(bytes),
    layout: `${entries.length} class rows × ${tiers.length} tier columns`,
  };
}

function serve(root) {
  let active = null;
  const server = createServer(async (request, response) => {
    const pathname = decodeURIComponent(
      new URL(request.url ?? '/', 'http://127.0.0.1').pathname,
    );
    if (pathname === '/replay.json') {
      if (!active) return send(response, 503, Buffer.from('replay not selected'), 'text/plain');
      return send(response, 200, active.replay, 'application/json; charset=utf-8');
    }
    if (active && pathname === active.pathname)
      return send(response, 200, active.model, 'model/gltf-binary');
    const local = pathname === '/' ? 'index.html' : pathname.slice(1);
    const path = resolve(root, local);
    if (path !== root && !path.startsWith(`${root}${sep}`))
      return send(response, 403, Buffer.from('forbidden'), 'text/plain');
    try {
      const metadata = await stat(path);
      if (!metadata.isFile()) throw new Error('not a file');
      response.writeHead(200, {
        'cache-control': 'no-store',
        'content-length': metadata.size,
        'content-type': mimeType(path),
      });
      createReadStream(path).pipe(response);
    } catch {
      send(response, 404, Buffer.from('not found'), 'text/plain');
    }
  });
  server.select = (selection) => {
    active = selection;
  };
  return new Promise((accept, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', () => accept(server));
  });
}

function send(response, status, bytes, contentType) {
  response.writeHead(status, {
    'cache-control': 'no-store',
    'content-length': bytes.length,
    'content-type': contentType,
  });
  response.end(bytes);
}

function address(server) {
  const value = server.address();
  if (!value || typeof value === 'string') throw new Error('Server has no TCP address.');
  return `http://127.0.0.1:${value.port}/?standalone&audio=off`;
}

function parseGlb(bytes) {
  if (bytes.subarray(0, 4).toString('ascii') !== 'glTF' || bytes.readUInt32LE(4) !== 2)
    throw new Error('Candidate is not a glTF 2.0 binary.');
  const jsonLength = bytes.readUInt32LE(12);
  const json = JSON.parse(
    bytes.subarray(20, 20 + jsonLength).toString('utf8').replace(/[\u0000 ]+$/u, ''),
  );
  return {
    meshes: json.meshes?.length ?? 0,
    materials: json.materials?.length ?? 0,
    animations: json.animations?.length ?? 0,
    skins: json.skins?.length ?? 0,
    extensionsRequired: json.extensionsRequired ?? [],
  };
}

function parseArguments(args) {
  if ((args.length !== 2 && args.length !== 3) || args[0] !== '--spec')
    throw new Error(
      'Usage: capture-static-model-tiers.mjs --spec <review-spec.json> [--resume]',
    );
  if (args.length === 3 && args[2] !== '--resume')
    throw new Error(`Unknown argument: ${args[2]}`);
  return { spec: args[1], resume: args[2] === '--resume' };
}

function resolvePath(value) {
  return resolve(repository, value);
}

function displayPath(path) {
  const local = relative(repository, resolve(path));
  return local.startsWith('..') ? resolve(path) : local;
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

function assertEqual(actual, expected, label) {
  if (actual !== expected)
    throw new Error(`${label} drifted: expected ${expected}, received ${actual}.`);
}

function mimeType(path) {
  return (
    {
      '.css': 'text/css; charset=utf-8',
      '.glb': 'model/gltf-binary',
      '.html': 'text/html; charset=utf-8',
      '.js': 'text/javascript; charset=utf-8',
      '.json': 'application/json; charset=utf-8',
      '.m4a': 'audio/mp4',
      '.png': 'image/png',
      '.svg': 'image/svg+xml',
      '.webp': 'image/webp',
      '.woff2': 'font/woff2',
    }[extname(path)] ?? 'application/octet-stream'
  );
}

function progress(message) {
  process.stderr.write(`[static-tier-capture] ${message}\n`);
}
