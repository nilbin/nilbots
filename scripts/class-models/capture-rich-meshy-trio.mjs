#!/usr/bin/env node

/**
 * Capture the three original, fully textured Meshy class models in the production viewer.
 *
 * The harness is intentionally evidence-only: it does not install or alter assets, camera
 * code, renderer code, or replay bytes. The exact source models and native replay files are
 * hash pinned, and each production-build GLB request must resolve to the same bytes before a
 * screenshot is accepted.
 *
 * Usage:
 *
 *   (cd web && npm run build:review)
 *   node scripts/class-models/capture-rich-meshy-trio.mjs
 */

import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import {
  mkdir,
  readFile,
  readdir,
  stat,
  writeFile,
} from 'node:fs/promises';
import { createServer } from 'node:http';
import { createRequire } from 'node:module';
import {
  dirname,
  extname,
  join,
  relative,
  resolve,
  sep,
} from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repository = resolve(scriptDirectory, '..', '..');
const worktrees = resolve(repository, '..');
const options = parseArguments(process.argv.slice(2));
const requireFromWeb = createRequire(join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const { createCanvas, loadImage } = requireFromWeb('@napi-rs/canvas');

const paths = {
  dist: resolve(repository, options.dist ?? 'web/dist-review'),
  output: resolve(
    repository,
    options.output ?? 'sandbox/rich-meshy-trio-review',
  ),
};

const CASES = [
  {
    id: 'striker',
    label: 'STRIKER · TRIDENT WASP',
    lookId: 'trident-wasp',
    model: 'web/src/assets/class-looks/trident-wasp/model.glb',
    modelSha256:
      '4b49e82b248c5c3203b01e4033ec77c28e5c98db91f4e274bd2690dbb6652798',
    replay:
      options.strikerReplay ??
      resolve(
        worktrees,
        'generic-labs-vertical-slice/sandbox/march-wall-w8-scratch-6d3a91fe/runs/w6-vs-vector-edge-bastion/2718281-a/replay.json',
      ),
    replayFileSha256:
      '3ded14f330d2298fe521e96e5a378aba79d9c142c53e88ea5f2fbff31ef904ca',
    replayHash:
      '4ed6ef727ee8eb0fc97d35bc355a9b488de5d13c63191621d12c62d6c5ec6b9a',
    time: 32,
    actor: { teamId: 1, unitId: 0, lifeId: 0, formId: 'striker-prime' },
  },
  {
    id: 'bulwark',
    label: 'BULWARK · AEGIS TORTOISE',
    lookId: 'aegis-tortoise',
    model: 'web/src/assets/class-looks/aegis-tortoise/model.glb',
    modelSha256:
      '39200217979706117ce47963873d6e4dac90485506fb02c3732078cd55c5b372',
    replay:
      options.bulwarkReplay ??
      resolve(
        worktrees,
        'codex-aegis-quality-comparison/sandbox/bulwark-runtime-review-v2-blue-orange/replay.json',
      ),
    replayFileSha256:
      'ad62e56a750199928cc326fd8d0bd3732ea42405f36c9cfe59d6b2854940f36e',
    replayHash:
      'bca049a6083ccf89468dfe749345f1e4c80c9352ea68bb3405b466c0d0e08463',
    time: 219.95,
    actor: { teamId: 0, unitId: 0, lifeId: 4, formId: 'bulwark-prime' },
  },
  {
    id: 'fabricator',
    label: 'FABRICATOR · LATTICE LOOM',
    lookId: 'lattice-loom',
    model: 'web/src/assets/class-looks/lattice-loom/model.glb',
    modelSha256:
      '116d6357ed9c38ceb4c25ea20cfe25d5630dd3b6b97f9a0fd3b1605f4003951c',
    replay:
      options.fabricatorReplay ??
      resolve(
        worktrees,
        'generic-labs-vertical-slice/sandbox/iron-root-w8-scratch-5c17ae92/probe/fab-arc-light/replay.json',
      ),
    replayFileSha256:
      '1db9e3ddc05ef1cde6dfe6d92f6bf465aebe6d3bcf0fe3e5405669c7d96df141',
    replayHash:
      'd634786fd2f2cee21efeb866cb740b3ed5708f65ac0757c81f6f5e795e7ab243',
    time: 16,
    actor: { teamId: 1, unitId: 0, lifeId: 0, formId: 'fabricator-prime' },
  },
];

const VIEWPORT = { width: 1600, height: 1000, deviceScaleFactor: 1 };
const CAMERA_SETTLE_MS = 3_500;
const CONTACT_CELL = { width: 620, height: 388, labelHeight: 48, gap: 14 };
const TEAM_ACCENT_FALLBACK = [
  '#38bdf8',
  '#fb923c',
  '#a78bfa',
  '#4ade80',
  '#f472b6',
  '#facc15',
];

await Promise.all([
  stat(join(paths.dist, 'index.html')),
  mkdir(join(paths.output, 'captures'), { recursive: true }),
]);

progress('preflight');
const preflight = await preflightCases();
progress('launch browser');
const server = await serve(paths.dist);
const browser = await chromium.launch({
  headless: true,
  args: ['--use-angle=swiftshader'],
});
const captures = [];

try {
  for (const entry of CASES) {
    progress(`${entry.id}: select replay`);
    server.setReplay(entry.replayBytes);
    captures.push(...(await captureCase(browser, server, entry)));
  }
} finally {
  await browser.close();
  await new Promise((accept) => server.close(accept));
}

const contactSheet = await makeContactSheet(captures);
const report = {
  schemaVersion: 1,
  verdict: 'capture-complete-owner-review-required',
  purpose:
    'Original fully textured 30-credit Meshy class models rendered by the production 3D viewer.',
  repositoryRevision: git('rev-parse', 'HEAD'),
  worktreeDirty: git('status', '--short').length > 0,
  replayMutation: false,
  rendererMutationByHarness: false,
  modelMutationByHarness: false,
  runtime: {
    renderer: 'production WebGL viewer',
    browser: 'headless Chromium with SwiftShader',
    viewportCssPixels: VIEWPORT,
    query: '?standalone&audio=off',
    cameraModes: {
      actionFollow: 'production Fit on; requested stable unit inspected',
      wholeArena: 'production Fit off; same replay time and inspected unit',
    },
    settleMillisecondsPerCamera: CAMERA_SETTLE_MS,
  },
  teamColourContract: {
    floorPool: 'renderer-owned effective team accent',
    modelInlays: 'fixed provider-authored colours in one fused material',
    approval:
      'visual model review only; semantic team-inlay extraction remains required before shipping',
  },
  preflight,
  captures,
  contactSheet,
  failures: [],
};
const reportPath = join(paths.output, 'capture-report.json');
await writeFile(reportPath, `${JSON.stringify(report, null, 2)}\n`);

process.stdout.write(
  `${JSON.stringify(
    {
      report: relative(repository, reportPath),
      contactSheet: contactSheet.path,
      captures: captures.map(({ id, path, sha256 }) => ({ id, path, sha256 })),
    },
    null,
    2,
  )}\n`,
);

async function preflightCases() {
  const distModels = await locateDistModels();
  const results = [];
  for (const entry of CASES) {
    const [modelBytes, replayBytes] = await Promise.all([
      readFile(resolve(repository, entry.model)),
      readFile(entry.replay),
    ]);
    assertEqual(sha256(modelBytes), entry.modelSha256, `${entry.id} source model`);
    assertEqual(
      sha256(replayBytes),
      entry.replayFileSha256,
      `${entry.id} replay file`,
    );
    const replay = JSON.parse(replayBytes);
    assertEqual(replay.replayHash, entry.replayHash, `${entry.id} replay hash`);
    assertEqual(
      replay.header?.presentation?.themeId,
      'ember-forge',
      `${entry.id} theme`,
    );
    const presentation = replay.header?.presentation?.forms?.find(
      (form) => form.formId === entry.actor.formId,
    );
    assertEqual(
      presentation?.lookId,
      entry.lookId,
      `${entry.id} form presentation`,
    );
    const tick = replay.ticks?.[Math.floor(entry.time)];
    const life = tick?.postState?.activeLives?.find(
      (candidate) =>
        candidate.actorId.teamId === entry.actor.teamId &&
        candidate.actorId.unitId === entry.actor.unitId &&
        candidate.actorId.lifeId === entry.actor.lifeId,
    );
    if (!life)
      throw new Error(`${entry.id} actor is not active at ${entry.time}.`);
    assertEqual(life.formId, entry.actor.formId, `${entry.id} active form`);

    const glb = parseGlb(modelBytes);
    const distModel = distModels.get(entry.id);
    if (!distModel)
      throw new Error(`${entry.id} model was not found in the production build.`);
    entry.replayBytes = replayBytes;
    entry.replayData = replay;
    entry.position = life.position;
    entry.distModel = distModel;
    results.push({
      id: entry.id,
      lookId: entry.lookId,
      model: {
        path: entry.model,
        bytes: modelBytes.length,
        sha256: entry.modelSha256,
        glbVersion: glb.version,
        meshes: glb.json.meshes?.length ?? 0,
        materials: glb.json.materials?.length ?? 0,
        animations: glb.json.animations?.length ?? 0,
        skins: glb.json.skins?.length ?? 0,
        bundled: distModel,
      },
      replay: {
        path: relative(repository, entry.replay),
        bytes: replayBytes.length,
        sha256: entry.replayFileSha256,
        canonicalHash: entry.replayHash,
        time: entry.time,
        actor: { ...entry.actor, position: entry.position },
        teamAccent: renderedTeamAccent(replay, entry.actor.teamId),
      },
    });
  }
  return results;
}

async function locateDistModels() {
  const assetDirectory = join(paths.dist, 'assets');
  const entries = await readdir(assetDirectory, { withFileTypes: true });
  const wantedBySize = new Map();
  for (const entry of CASES) {
    const source = await stat(resolve(repository, entry.model));
    const bySize = wantedBySize.get(source.size) ?? [];
    bySize.push(entry);
    wantedBySize.set(source.size, bySize);
  }

  const matches = new Map();
  for (const candidate of entries) {
    if (!candidate.isFile() || !candidate.name.endsWith('.glb')) continue;
    const path = join(assetDirectory, candidate.name);
    const metadata = await stat(path);
    const possible = wantedBySize.get(metadata.size);
    if (!possible) continue;
    const digest = await sha256File(path);
    for (const entry of possible) {
      if (digest !== entry.modelSha256) continue;
      if (matches.has(entry.id))
        throw new Error(`More than one ${entry.id} GLB matches in the build.`);
      matches.set(entry.id, {
        path: relative(paths.dist, path),
        bytes: metadata.size,
        sha256: digest,
      });
    }
  }
  return matches;
}

async function captureCase(browser, server, entry) {
  const page = await browser.newPage({
    viewport: { width: VIEWPORT.width, height: VIEWPORT.height },
    deviceScaleFactor: VIEWPORT.deviceScaleFactor,
  });
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
    const headers = response.headers();
    modelResponses.push({
      pathname,
      status: response.status(),
      bytes: Number(headers['content-length'] ?? 0),
    });
  });
  await page.addInitScript(() => {
    localStorage.setItem('nilbots.soundtrack.enabled.v1', 'false');
  });

  try {
    progress(`${entry.id}: navigate`);
    await page.goto(address(server), {
      waitUntil: 'networkidle',
      timeout: 60_000,
    });
    const arena = page.locator('section[aria-label="Arena"]');
    const canvas = arena.locator('canvas');
    progress(`${entry.id}: wait for arena canvas`);
    try {
      await canvas.waitFor({ state: 'visible', timeout: 30_000 });
    } catch (error) {
      const diagnosticPath = join(paths.output, `${entry.id}-bootstrap-failure.png`);
      await page.screenshot({ path: diagnosticPath, fullPage: true });
      const body = (await page.locator('body').innerText()).slice(0, 4_000);
      throw new Error(
        `${entry.id} did not mount the arena canvas. Diagnostic: ${relative(
          repository,
          diagnosticPath,
        )}\n${JSON.stringify({ body, pageErrors, consoleErrors }, null, 2)}\n${
          error instanceof Error ? error.message : String(error)
        }`,
      );
    }
    progress(`${entry.id}: wait for exact model and play gate`);
    const playMatch = page.getByRole('button', {
      name: 'Play match',
      exact: true,
    });
    await playMatch.waitFor({ state: 'visible', timeout: 180_000 });
    await playMatch.click();
    await page
      .locator('[data-play-overlay]')
      .waitFor({ state: 'detached', timeout: 30_000 });
    const actualTime = await preciseSeek(page, entry.time);
    progress(`${entry.id}: seek ${actualTime} and inspect actor`);

    const card = page.locator('article').filter({
      hasText: `team ${entry.actor.teamId} · unit ${entry.actor.unitId}`,
    });
    if ((await card.count()) !== 1)
      throw new Error(`${entry.id} actor did not resolve to exactly one unit card.`);
    await card.getByRole('button', { name: 'Inspect', exact: true }).click();

    const fit = page.getByRole('button', { name: /fit/i });
    if ((await fit.getAttribute('aria-pressed')) !== 'true') await fit.click();
    await page.waitForTimeout(CAMERA_SETTLE_MS);
    assertHealthyRuntime(entry, pageErrors, modelResponses);
    const action = await saveCapture(
      canvas,
      entry,
      actualTime,
      'action-follow',
      modelResponses,
    );
    progress(`${entry.id}: captured action-follow`);

    if ((await fit.getAttribute('aria-pressed')) === 'true') await fit.click();
    await page.waitForTimeout(CAMERA_SETTLE_MS);
    assertHealthyRuntime(entry, pageErrors, modelResponses);
    const whole = await saveCapture(
      canvas,
      entry,
      actualTime,
      'whole-arena',
      modelResponses,
    );
    progress(`${entry.id}: captured whole-arena`);
    return [action, whole];
  } finally {
    await page.close();
  }
}

function assertHealthyRuntime(entry, pageErrors, modelResponses) {
  if (pageErrors.length > 0)
    throw new Error(`${entry.id} page errors:\n${pageErrors.join('\n')}`);
  const expectedPath = `/${entry.distModel.path}`;
  const fetched = modelResponses.find(
    (response) =>
      response.pathname === expectedPath &&
      response.status === 200 &&
      response.bytes === entry.distModel.bytes,
  );
  if (!fetched) {
    throw new Error(
      `${entry.id} exact bundled GLB was not fetched successfully:\n${JSON.stringify(
        { expectedPath, expectedBytes: entry.distModel.bytes, modelResponses },
        null,
        2,
      )}`,
    );
  }
}

async function saveCapture(canvas, entry, time, cameraMode, modelResponses) {
  const path = join(paths.output, 'captures', `${entry.id}-${cameraMode}.png`);
  await canvas.screenshot({ path });
  const bytes = await readFile(path);
  if (bytes.length < 50_000)
    throw new Error(`${entry.id}/${cameraMode} appears blank (${bytes.length} bytes).`);
  const metrics = await canvas.evaluate((element) => ({
    width: element.width,
    height: element.height,
    cssWidth: element.clientWidth,
    cssHeight: element.clientHeight,
    devicePixelRatio: window.devicePixelRatio,
  }));
  const replay = entry.replayData;
  return {
    id: `${entry.id}-${cameraMode}`,
    classId: entry.id,
    label: entry.label,
    cameraMode,
    time,
    tick: Math.floor(time),
    actor: { ...entry.actor, position: entry.position },
    teamAccent: renderedTeamAccent(replay, entry.actor.teamId),
    path: relative(repository, path),
    bytes: bytes.length,
    sha256: sha256(bytes),
    modelRequest: modelResponses.find(
      (response) => response.pathname === `/${entry.distModel.path}`,
    ),
    ...metrics,
  };
}

async function preciseSeek(page, target) {
  const pause = page.getByRole('button', { name: 'Pause', exact: true });
  if (await pause.isVisible()) await pause.click();

  const timeline = page.locator('[aria-label="Match timeline — drag to seek"]');
  const thumb = page.getByRole('slider', { name: 'Playhead' });
  await timeline.waitFor({ state: 'visible' });
  const bounds = await timeline.boundingBox();
  if (!bounds) throw new Error('Timeline has no clickable bounds.');
  const maximum = Number(await thumb.getAttribute('aria-valuemax'));
  const x = bounds.x + (bounds.width * target) / maximum;
  await page.mouse.click(x, bounds.y + bounds.height / 2);
  await thumb.focus();

  let current = Number(await thumb.getAttribute('aria-valuenow'));
  const steps = Math.round((target - current) / 0.01);
  if (Math.abs(steps) > 200) {
    throw new Error(
      `Pointer seek was too imprecise (${current} for ${target}); refusing a long key walk.`,
    );
  }
  const key = steps < 0 ? 'ArrowLeft' : 'ArrowRight';
  for (let index = 0; index < Math.abs(steps); index += 1)
    await page.keyboard.press(key);
  current = Number(await thumb.getAttribute('aria-valuenow'));
  if (Math.abs(current - target) > 0.001)
    throw new Error(`Playhead reports ${current}, expected ${target}.`);
  return current;
}

async function makeContactSheet(captures) {
  const ordered = [];
  for (const cameraMode of ['action-follow', 'whole-arena']) {
    for (const entry of CASES) {
      const capture = captures.find(
        (candidate) =>
          candidate.classId === entry.id && candidate.cameraMode === cameraMode,
      );
      if (!capture) throw new Error(`Missing ${entry.id}/${cameraMode} capture.`);
      ordered.push(capture);
    }
  }

  const { width: cellWidth, height: imageHeight, labelHeight, gap } =
    CONTACT_CELL;
  const width = 3 * cellWidth + 4 * gap;
  const height = 2 * (imageHeight + labelHeight) + 3 * gap;
  const output = createCanvas(width, height);
  const context = output.getContext('2d');
  context.fillStyle = '#0b1118';
  context.fillRect(0, 0, width, height);
  context.font = '600 20px sans-serif';
  context.textBaseline = 'middle';
  context.imageSmoothingEnabled = true;
  context.imageSmoothingQuality = 'high';

  for (const [index, capture] of ordered.entries()) {
    const column = index % 3;
    const row = Math.floor(index / 3);
    const left = gap + column * (cellWidth + gap);
    const top = gap + row * (imageHeight + labelHeight + gap);
    const source = await loadImage(resolve(repository, capture.path));
    context.drawImage(source, left, top, cellWidth, imageHeight);
    context.strokeStyle = capture.teamAccent ?? '#9ca3af';
    context.lineWidth = 4;
    context.strokeRect(left + 2, top + 2, cellWidth - 4, imageHeight - 4);
    context.fillStyle = '#edf5fa';
    const mode = capture.cameraMode === 'action-follow' ? 'ACTION FOLLOW' : 'WHOLE ARENA';
    context.fillText(
      `${capture.label} · ${mode}`,
      left + 9,
      top + imageHeight + labelHeight / 2,
    );
  }

  const path = join(paths.output, 'rich-meshy-trio-in-game.png');
  const bytes = output.toBuffer('image/png');
  await writeFile(path, bytes);
  return {
    path: relative(repository, path),
    bytes: bytes.length,
    sha256: sha256(bytes),
    layout: 'three class columns; action-follow top row; whole-arena bottom row',
    cellPixels: { width: cellWidth, height: imageHeight },
  };
}

function serve(root) {
  let activeReplay = null;
  const server = createServer(async (request, response) => {
    const pathname = decodeURIComponent(
      new URL(request.url ?? '/', 'http://127.0.0.1').pathname,
    );
    if (pathname === '/replay.json') {
      if (!activeReplay) {
        response.writeHead(503);
        response.end('replay not selected');
        return;
      }
      response.writeHead(200, {
        'cache-control': 'no-store',
        'content-length': activeReplay.length,
        'content-type': 'application/json; charset=utf-8',
      });
      response.end(activeReplay);
      return;
    }
    const local = pathname === '/' ? 'index.html' : pathname.slice(1);
    const path = resolve(root, local);
    if (path !== root && !path.startsWith(`${root}${sep}`)) {
      response.writeHead(403);
      response.end('forbidden');
      return;
    }
    try {
      const entry = await stat(path);
      if (!entry.isFile()) throw new Error('not a file');
      response.writeHead(200, {
        'cache-control': 'no-store',
        'content-length': entry.size,
        'content-type': mimeType(path),
      });
      createReadStream(path).pipe(response);
    } catch {
      response.writeHead(404);
      response.end('not found');
    }
  });
  server.setReplay = (bytes) => {
    activeReplay = bytes;
  };
  return new Promise((accept, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', () => accept(server));
  });
}

function address(server) {
  const value = server.address();
  if (!value || typeof value === 'string')
    throw new Error('Capture server did not expose a TCP address.');
  return `http://127.0.0.1:${value.port}/?standalone&audio=off`;
}

function parseGlb(bytes) {
  if (bytes.length < 20 || bytes.toString('ascii', 0, 4) !== 'glTF')
    throw new Error('Model is not a GLB file.');
  const version = bytes.readUInt32LE(4);
  const declaredLength = bytes.readUInt32LE(8);
  if (version !== 2 || declaredLength !== bytes.length)
    throw new Error(
      `Invalid GLB header: version ${version}, length ${declaredLength}/${bytes.length}.`,
    );
  const jsonLength = bytes.readUInt32LE(12);
  const jsonType = bytes.readUInt32LE(16);
  if (jsonType !== 0x4e4f534a) throw new Error('First GLB chunk is not JSON.');
  const source = bytes
    .subarray(20, 20 + jsonLength)
    .toString('utf8')
    .replace(/[\u0000\u0020]+$/u, '');
  return { version, json: JSON.parse(source) };
}

async function sha256File(path) {
  const digest = createHash('sha256');
  await new Promise((accept, reject) => {
    const stream = createReadStream(path);
    stream.on('data', (chunk) => digest.update(chunk));
    stream.once('error', reject);
    stream.once('end', accept);
  });
  return digest.digest('hex');
}

function parseArguments(arguments_) {
  const result = {};
  const values = new Map([
    ['--dist', 'dist'],
    ['--output', 'output'],
    ['--striker-replay', 'strikerReplay'],
    ['--bulwark-replay', 'bulwarkReplay'],
    ['--fabricator-replay', 'fabricatorReplay'],
  ]);
  for (let index = 0; index < arguments_.length; index += 1) {
    const argument = arguments_[index];
    const key = values.get(argument);
    if (!key) throw new Error(`Unknown option: ${argument}`);
    const value = arguments_[index + 1];
    if (!value || value.startsWith('--'))
      throw new Error(`${argument} requires a value.`);
    result[key] = value;
    index += 1;
  }
  return result;
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

function git(...arguments_) {
  return execFileSync('git', arguments_, {
    cwd: repository,
    encoding: 'utf8',
  }).trim();
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

function assertEqual(observed, expected, label) {
  if (observed !== expected)
    throw new Error(`${label} drifted: expected ${expected}, received ${observed}.`);
}

/** Mirror the production renderer's duplicate-accent fallback for evidence labels. */
function renderedTeamAccent(replay, teamId) {
  const participants = replay.header?.provenance?.participants ?? [];
  const byTeam = new Map();
  for (const participant of participants) {
    const accents = byTeam.get(participant.teamId) ?? new Set();
    accents.add(String(participant.accent ?? '#38bdf8').toLowerCase());
    byTeam.set(participant.teamId, accents);
  }
  const teamIds = [...byTeam.keys()].sort((left, right) => left - right);
  const distinguishes =
    teamIds.length > 1 &&
    teamIds.every((candidate) => byTeam.get(candidate).size === 1) &&
    new Set(teamIds.map((candidate) => [...byTeam.get(candidate)][0])).size ===
      teamIds.length;
  if (!distinguishes) {
    const index = teamIds.indexOf(teamId);
    return TEAM_ACCENT_FALLBACK[(index < 0 ? 0 : index) % TEAM_ACCENT_FALLBACK.length];
  }
  return [...(byTeam.get(teamId) ?? [])][0] ?? '#38bdf8';
}

function progress(message) {
  process.stderr.write(`[rich-trio] ${message}\n`);
}
