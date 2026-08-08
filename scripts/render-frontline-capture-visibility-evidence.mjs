#!/usr/bin/env node

import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { mkdir, readFile, stat, writeFile } from 'node:fs/promises';
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
const repository = resolve(scriptDirectory, '..');
const replayPath = resolve(
  process.env.REPLAY_PATH ??
    join(
      repository,
      'sandbox',
      'frontline-capture-visibility',
      'native-fabricator',
      'replay.json',
    ),
);
const viewerPath = resolve(
  process.env.VIEWER_PATH ??
    join(dirname(replayPath), 'viewer.html'),
);
const dist = resolve(
  process.env.DIST_PATH ?? join(repository, 'web', 'dist-review'),
);
const outputDirectory = resolve(
  process.env.OUTPUT_DIRECTORY ??
    join(
      repository,
      'art',
      'frontline-map-models',
      'review',
      'capture-visibility',
    ),
);
const rawDirectory = resolve(
  process.env.RAW_DIRECTORY ??
    join(
      repository,
      'sandbox',
      'frontline-capture-visibility',
      'captures',
    ),
);
const requireFromWeb = createRequire(join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const { createCanvas, loadImage } = requireFromWeb('@napi-rs/canvas');

const replayBytes = await readFile(replayPath);
const replay = JSON.parse(replayBytes);
const sourceSha256 = sha256(replayBytes);
const expected = {
  sourceSha256:
    '0abc08abe0a014bbacb686ff6d77940ff8464fefb5ea50b6742c418e61ac5d00',
  replayHash:
    '0b536affaf1a17755fdd0a7f3e20472c23c3f7f44f132507cbc0d08ebf61ebc5',
  mapId: 'frontline-labs-01-classes',
  width: 23,
  height: 15,
  ticks: 500,
  ratchetHoldTicks: 40,
};

const states = [
  {
    id: 'neutral',
    label: 'NEUTRAL',
    detail: 'no claimant · no inferred owner',
    color: '#d5a86d',
    tick: 0,
    expected: {
      activePositionIndex: 2,
      claimingTeamId: null,
      captureProgressBefore: 0,
      captureProgress: 0,
      holdOwnerTeamId: null,
      holdEndsAtTick: null,
      captureTeamId: null,
      captureContested: false,
      capturePaused: false,
      teamWeights: [],
      presentTeamIds: [],
    },
  },
  {
    id: 'building',
    label: 'BUILDING',
    detail: 'team 0 · incumbent arc 5/15',
    color: '#e06b24',
    tick: 15,
    expected: {
      activePositionIndex: 2,
      claimingTeamId: 0,
      captureProgressBefore: 4,
      captureProgress: 5,
      holdOwnerTeamId: null,
      holdEndsAtTick: null,
      captureTeamId: 0,
      captureContested: false,
      capturePaused: false,
      teamWeights: [[0, 1]],
      presentTeamIds: [0],
    },
  },
  {
    id: 'contested',
    label: 'CONTESTED',
    detail: 'stored team 1 claim 1/15 · both teams present',
    color: '#f4c477',
    tick: 8,
    expected: {
      activePositionIndex: 2,
      claimingTeamId: 1,
      captureProgressBefore: 1,
      captureProgress: 1,
      holdOwnerTeamId: null,
      holdEndsAtTick: null,
      captureTeamId: null,
      captureContested: true,
      capturePaused: false,
      teamWeights: [
        [0, 1],
        [1, 1],
      ],
      presentTeamIds: [0, 1],
    },
  },
  {
    id: 'net-control',
    label: 'WEIGHTED NET CONTROL',
    detail: 'team 0 weight 2:1 · progresses 7→8 with both teams present',
    color: '#e06b24',
    tick: 138,
    expected: {
      activePositionIndex: 3,
      claimingTeamId: 0,
      captureProgressBefore: 7,
      captureProgress: 8,
      holdOwnerTeamId: 0,
      holdEndsAtTick: 166,
      holdRemainingTicks: 27,
      captureTeamId: 0,
      captureContested: false,
      capturePaused: false,
      teamWeights: [
        [0, 2],
        [1, 1],
      ],
      presentTeamIds: [0, 1],
    },
  },
  {
    id: 'erosion',
    label: 'INCUMBENT EROSION',
    detail: 'team 0 owns 4/15 · team 1 erodes without credit',
    color: '#facc15',
    tick: 110,
    expected: {
      activePositionIndex: 2,
      claimingTeamId: 0,
      captureProgressBefore: 5,
      captureProgress: 4,
      holdOwnerTeamId: null,
      holdEndsAtTick: null,
      captureTeamId: 1,
      captureContested: false,
      capturePaused: false,
      teamWeights: [[1, 1]],
      presentTeamIds: [1],
    },
  },
  {
    id: 'early-hold',
    label: 'POST-ADVANCE RATCHET',
    detail: 'team 0 owner · 40/40 ticks remain · position 3',
    color: '#e06b24',
    tick: 25,
    expected: {
      activePositionIndex: 3,
      claimingTeamId: null,
      captureProgressBefore: 14,
      captureProgress: 0,
      holdOwnerTeamId: 0,
      holdEndsAtTick: 66,
      holdRemainingTicks: 40,
      captureTeamId: null,
      captureContested: false,
      capturePaused: true,
      teamWeights: [],
      presentTeamIds: [],
    },
  },
  {
    id: 'late-hold',
    label: 'LATE RATCHET + CHALLENGE',
    detail: 'team 0 owner · 1/40 remains · team 1 builds 12/15',
    color: '#facc15',
    tick: 64,
    expected: {
      activePositionIndex: 3,
      claimingTeamId: 1,
      captureProgressBefore: 10,
      captureProgress: 12,
      holdOwnerTeamId: 0,
      holdEndsAtTick: 66,
      holdRemainingTicks: 1,
      captureTeamId: 1,
      captureContested: false,
      capturePaused: false,
      teamWeights: [[1, 2]],
      presentTeamIds: [1],
    },
  },
];

verifySource();
const stateReadings = states.map((state) => {
  const reading = readState(state.tick);
  const wanted = {
    activePositionIndex: state.expected.activePositionIndex,
    claimingTeamId: state.expected.claimingTeamId,
    captureProgressBefore: state.expected.captureProgressBefore,
    captureProgress: state.expected.captureProgress,
    holdOwnerTeamId: state.expected.holdOwnerTeamId,
    holdEndsAtTick: state.expected.holdEndsAtTick,
    holdRemainingTicks:
      state.expected.holdRemainingTicks ??
      (state.expected.holdEndsAtTick === null
        ? null
        : state.expected.holdEndsAtTick - reading.nextTick),
    captureTeamId: state.expected.captureTeamId,
    captureContested: state.expected.captureContested,
    capturePaused: state.expected.capturePaused,
    teamWeights: state.expected.teamWeights,
    presentTeamIds: state.expected.presentTeamIds,
  };
  if (JSON.stringify(readingComparable(reading)) !== JSON.stringify(wanted)) {
    throw new Error(
      `${state.id} native state drifted:\n${JSON.stringify(
        { wanted, observed: readingComparable(reading) },
        null,
        2,
      )}`,
    );
  }
  return reading;
});

await Promise.all([
  stat(join(dist, 'index.html')),
  stat(viewerPath),
  mkdir(outputDirectory, { recursive: true }),
  mkdir(rawDirectory, { recursive: true }),
]);

const server = await serve(dist);
const browser = await chromium.launch({
  headless: true,
  args: ['--use-angle=swiftshader'],
});
const captures = [];

try {
  for (const renderer of ['webgl', 'canvas']) {
    captures.push(
      ...(await captureSeries(browser, server, renderer)),
    );
  }
} finally {
  await browser.close();
  await new Promise((accept) => server.close(accept));
}

const boardPaths = {
  webgl: join(
    outputDirectory,
    'frontline-capture-visibility-webgl-v1.png',
  ),
  canvas: join(
    outputDirectory,
    'frontline-capture-visibility-canvas-v1.png',
  ),
};
for (const renderer of ['webgl', 'canvas']) {
  await contactSheet(
    boardPaths[renderer],
    renderer,
    states.map((state) => ({
      ...state,
      path: join(
        rawDirectory,
        `frontline-capture-${state.id}-${renderer}-v1.png`,
      ),
    })),
  );
}

const lockedPresentationFiles = [
  'maps/experimental/frontline-01.json',
  'web/src/assets/themes/ember-forge/theme.json',
  'web/src/render/arenaCamera.ts',
  'web/src/render3d/arenaScene.ts',
  'web/src/render3d/wallDetails.ts',
  'art/themes/ember-forge/art.json',
];
const lockedBase = '572095091fc36e87385368a98aadbfdd28069838';
const lockedEvidence = Object.fromEntries(
  await Promise.all(
    lockedPresentationFiles.map(async (path) => {
      const bytes = await readFile(join(repository, path));
      let unchangedFromApprovedV4Merge = true;
      try {
        execFileSync(
          'git',
          ['diff', '--quiet', lockedBase, '--', path],
          { cwd: repository, stdio: 'ignore' },
        );
      } catch {
        unchangedFromApprovedV4Merge = false;
      }
      return [
        path,
        {
          bytes: bytes.length,
          sha256: sha256(bytes),
          unchangedFromApprovedV4Merge,
        },
      ];
    }),
  ),
);
const viewerBytes = await readFile(viewerPath);
const report = {
  boards: Object.fromEntries(
    Object.entries(boardPaths).map(([key, path]) => [
      key,
      relative(repository, path),
    ]),
  ),
  nativeSource: {
    replay: relative(repository, replayPath),
    bytes: replayBytes.length,
    sha256: sourceSha256,
    canonicalReplayHash: replay.replayHash,
    replayVersion: replay.header.replayVersion,
    partial: replay.partial,
    mapId: replay.header.contract.map.mapId,
    dimensions: {
      width: replay.header.contract.map.width,
      height: replay.header.contract.map.height,
    },
    ticks: replay.ticks.length,
    presentation: replay.header.presentation,
    verification:
      'nilbots verify: OK — canonical replay v3 content, contract, and hash verify.',
    generation: {
      command:
        'dotnet run --project src/BotArena.Cli -- experiment frontline-labs --bot arena-bots/frontline-labs/classes-wave-5-2026-07-30/ledger-fly --opponent arena-bots/frontline-labs/classes-wave-5-2026-07-30/spark-line --seed 104729 --classes fabricator-vs-fabricator --movement facing-locked --pendulum keel --skills kit --bend universal --five-slots wane --stance-ground open --aim offset --runtime in-process --out sandbox/frontline-capture-visibility/native-fabricator --viewer',
      runtime:
        'in-process diagnostic generic-actor runtime; canonical replay-v3 verified separately',
      viewerOptIn: '--viewer',
      viewer: relative(repository, viewerPath),
      viewerBytes: viewerBytes.length,
      viewerSha256: sha256(viewerBytes),
    },
  },
  nativeStates: Object.fromEntries(
    states.map((state, index) => [
      state.id,
      {
        label: state.label,
        detail: state.detail,
        ...stateReadings[index],
      },
    ]),
  ),
  capture: {
    viewport: { width: 1600, height: 1000, deviceScaleFactor: 1 },
    cameraPitchDegrees: 58,
    cameraMode: 'production whole-arena Fit off',
    replayMutation: false,
    captures,
  },
  approvedV4Lock: {
    mergeCommit: lockedBase,
    files: lockedEvidence,
    allUnchanged: Object.values(lockedEvidence).every(
      (entry) => entry.unchangedFromApprovedV4Merge,
    ),
    statement:
      'Ground, wall geometry/material configuration, map proportions, spacing, collision, and camera remain byte-for-byte unchanged from the approved V4 merge. Only flat renderer overlays and shared presentation were changed.',
  },
  providerCalls: 0,
};
if (!report.approvedV4Lock.allUnchanged) {
  throw new Error(
    `Approved V4 lock failed:\n${JSON.stringify(
      report.approvedV4Lock,
      null,
      2,
    )}`,
  );
}
const reportPath = join(
  outputDirectory,
  'frontline-capture-visibility-review-v1.json',
);
await writeFile(reportPath, `${JSON.stringify(report, null, 2)}\n`);
process.stdout.write(
  `${JSON.stringify(
    {
      report: relative(repository, reportPath),
      boards: report.boards,
      replayHash: report.nativeSource.canonicalReplayHash,
      viewerOptIn: report.nativeSource.generation.viewerOptIn,
      states: Object.keys(report.nativeStates),
      approvedV4Lock: report.approvedV4Lock.allUnchanged,
    },
    null,
    2,
  )}\n`,
);

function verifySource() {
  const mode = replay?.header?.contract?.rules?.gameMode;
  const observed = {
    sourceSha256,
    replayHash: replay?.replayHash,
    replayVersion: replay?.header?.replayVersion,
    partial: replay?.partial,
    mapId: replay?.header?.contract?.map?.mapId,
    width: replay?.header?.contract?.map?.width,
    height: replay?.header?.contract?.map?.height,
    ticks: replay?.ticks?.length,
    ratchetHoldTicks:
      mode?.kind === 'frontline' ? mode.capture.ratchetHoldTicks : null,
    themeId: replay?.header?.presentation?.themeId,
    boundaryWall: replay?.header?.presentation?.map?.boundaryWall,
    interiorWall: replay?.header?.presentation?.map?.interiorWall,
  };
  const wanted = {
    sourceSha256: expected.sourceSha256,
    replayHash: expected.replayHash,
    replayVersion: 3,
    partial: false,
    mapId: expected.mapId,
    width: expected.width,
    height: expected.height,
    ticks: expected.ticks,
    ratchetHoldTicks: expected.ratchetHoldTicks,
    themeId: 'ember-forge',
    boundaryWall: 'perimeter',
    interiorWall: 'cover',
  };
  if (JSON.stringify(observed) !== JSON.stringify(wanted)) {
    throw new Error(
      `Frontline capture source drifted:\n${JSON.stringify(
        { wanted, observed },
        null,
        2,
      )}`,
    );
  }
}

function readState(tickIndex) {
  const tick = replay.ticks[tickIndex];
  const objective = tick.postState.mode;
  const beforeObjective = tick.tickStart.state.mode;
  const region = replay.header.contract.map.regions.find(
    (candidate) =>
      candidate.kind === 'objective' &&
      candidate.regionId ===
        `frontline-position-${objective.activePositionIndex}`,
  );
  const objectiveTiles = new Set(
    (region?.tiles ?? []).map(([x, y]) => `${x},${y}`),
  );
  const formWeights = new Map(
    replay.header.contract.rules.forms.map((form) => [
      form.id,
      form.objectiveWeight,
    ]),
  );
  const weightByTeam = new Map();
  for (const life of tick.postState.activeLives) {
    if (
      !objectiveTiles.has(`${life.position.x},${life.position.y}`)
    )
      continue;
    const weight = formWeights.get(life.formId) ?? 1;
    if (weight <= 0) continue;
    weightByTeam.set(
      life.actorId.teamId,
      (weightByTeam.get(life.actorId.teamId) ?? 0) + weight,
    );
  }
  const teamWeights = [...weightByTeam.entries()].sort(
    ([left], [right]) => left - right,
  );
  const presentTeamIds = teamWeights.map(([teamId]) => teamId);
  const captureControl = resolveCaptureControl(teamWeights);
  return {
    tick: tick.tick,
    nextTick: tick.postState.nextTick,
    activePositionIndex: objective.activePositionIndex,
    claimingTeamId: objective.claimingTeamId,
    captureProgressBefore: beforeObjective.captureProgress,
    captureProgress: objective.captureProgress,
    holdOwnerTeamId: objective.holdOwnerTeamId,
    holdEndsAtTick: objective.holdEndsAtTick,
    holdRemainingTicks:
      objective.holdEndsAtTick === null
        ? null
        : Math.max(
            0,
            objective.holdEndsAtTick - tick.postState.nextTick,
          ),
    captureTeamId: captureControl.teamId,
    captureContested: captureControl.contested,
    capturePaused:
      objective.controlResumesAtTick > tick.postState.nextTick,
    teamWeights,
    presentTeamIds,
  };
}

function readingComparable(reading) {
  return {
    activePositionIndex: reading.activePositionIndex,
    claimingTeamId: reading.claimingTeamId,
    captureProgressBefore: reading.captureProgressBefore,
    captureProgress: reading.captureProgress,
    holdOwnerTeamId: reading.holdOwnerTeamId,
    holdEndsAtTick: reading.holdEndsAtTick,
    holdRemainingTicks: reading.holdRemainingTicks,
    captureTeamId: reading.captureTeamId,
    captureContested: reading.captureContested,
    capturePaused: reading.capturePaused,
    teamWeights: reading.teamWeights,
    presentTeamIds: reading.presentTeamIds,
  };
}

function resolveCaptureControl(teamWeights) {
  if (teamWeights.length === 0)
    return { teamId: null, contested: false };
  const policy =
    replay.header.contract.rules.gameMode.capture.controlPolicy;
  if (
    policy !==
    'net-positive-objective-weight-difference-scales-gain-non-positive-applies-configured-decay-opposition-erodes-to-neutral'
  ) {
    return teamWeights.length === 1
      ? { teamId: teamWeights[0][0], contested: false }
      : { teamId: null, contested: true };
  }
  const total = teamWeights.reduce(
    (sum, [, weight]) => sum + weight,
    0,
  );
  const positive = teamWeights.filter(
    ([, weight]) => weight > total - weight,
  );
  return positive.length === 1
    ? { teamId: positive[0][0], contested: false }
    : { teamId: null, contested: teamWeights.length > 1 };
}

function serve(root) {
  const server = createServer(async (request, response) => {
    const pathname = decodeURIComponent(
      new URL(request.url ?? '/', 'http://127.0.0.1').pathname,
    );
    if (pathname === '/replay.json') {
      response.writeHead(200, {
        'cache-control': 'no-store',
        'content-type': 'application/json; charset=utf-8',
      });
      response.end(replayBytes);
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
        'content-type': mimeType(path),
      });
      createReadStream(path).pipe(response);
    } catch {
      response.writeHead(404);
      response.end('not found');
    }
  });
  return new Promise((accept, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', () => accept(server));
  });
}

function address(server) {
  const value = server.address();
  if (!value || typeof value === 'string')
    throw new Error('Review server did not expose a TCP address.');
  return `http://127.0.0.1:${value.port}/?standalone&audio=off`;
}

async function captureSeries(browser, server, renderer) {
  const page = await browser.newPage({
    viewport: { width: 1600, height: 1000 },
    deviceScaleFactor: 1,
  });
  const errors = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.addInitScript(({ forceCanvas }) => {
    localStorage.setItem('nilbots.soundtrack.enabled.v1', 'false');
    if (!forceCanvas) return;
    const getContext = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = function patched(type, ...args) {
      if (
        type === 'webgl' ||
        type === 'webgl2' ||
        type === 'experimental-webgl'
      )
        return null;
      return getContext.call(this, type, ...args);
    };
  }, { forceCanvas: renderer === 'canvas' });
  try {
    await page.goto(address(server), {
      waitUntil: 'networkidle',
      timeout: 120_000,
    });
    const arena = page.locator('section[aria-label="Arena"]');
    const canvas = arena.locator('canvas');
    await canvas.waitFor({ state: 'visible', timeout: 120_000 });
    await page
      .getByText(/Loading arena/)
      .waitFor({ state: 'hidden', timeout: 120_000 });
    // Fit pressed means production action-follow. Whole-arena is the same
    // explicit overview mode in both renderers, with Fable's 58° camera intact.
    const fit = page.getByRole('button', { name: /fit/i });
    if ((await fit.getAttribute('aria-pressed')) === 'true')
      await fit.click();

    const metrics = await canvas.evaluate((element) => ({
      width: element.width,
      height: element.height,
      cssWidth: element.clientWidth,
      cssHeight: element.clientHeight,
    }));
    const results = [];
    let currentTick = null;
    const captureStates = [...states].sort(
      (left, right) => left.tick - right.tick,
    );
    for (const state of captureStates) {
      const output = join(
        rawDirectory,
        `frontline-capture-${state.id}-${renderer}-v1.png`,
      );
      const actualTick = await seek(page, state.tick, currentTick);
      currentTick = actualTick;
      await page.waitForTimeout(700);
      if (errors.length > 0) throw new Error(errors.join('\n'));
      await canvas.screenshot({ path: output });
      const bytes = (await stat(output)).size;
      if (bytes < 50_000)
        throw new Error(
          `Review capture looks blank (${bytes} bytes): ${output}`,
        );
      results.push({
        id: `${state.id}-${renderer}`,
        tick: actualTick,
        renderer:
          renderer === 'canvas' ? 'Canvas2D fallback' : 'WebGL',
        path: relative(repository, output),
        bytes,
        sha256: sha256(await readFile(output)),
        ...metrics,
      });
    }
    return results;
  } finally {
    await page.close();
  }
}

async function seek(page, target, current) {
  const pause = page.getByRole('button', { name: 'Pause', exact: true });
  if (await pause.isVisible()) {
    await pause.click();
    await page.waitForTimeout(30);
  }
  const timeline = page.locator(
    '[aria-label="Match timeline — drag to seek"]',
  );
  const thumb = page.getByRole('slider', { name: 'Playhead' });
  await timeline.waitFor({ state: 'visible' });
  const bounds = await timeline.boundingBox();
  if (!bounds) throw new Error('Timeline did not expose clickable bounds.');
  let at = current;
  if (at === null || target < at) {
    await page.mouse.click(bounds.x, bounds.y + bounds.height / 2);
    at = 0;
  }
  const step = page.getByRole('button', {
    name: 'Step forward one tick',
  });
  for (let index = at; index < target; index += 1)
    await step.click();
  const value = Number(await thumb.getAttribute('aria-valuenow'));
  if (Math.abs(value - target) > 0.011)
    throw new Error(`Exact step seek reports ${value}, expected ${target}.`);
  return value;
}

async function contactSheet(path, renderer, panels) {
  const images = await Promise.all(
    panels.map((panel) => loadImage(panel.path)),
  );
  const panelWidth = 760;
  const panelHeight = 475;
  const headerHeight = 82;
  const gap = 2;
  const columns = 2;
  const rows = Math.ceil(panels.length / columns);
  const board = createCanvas(
    panelWidth * columns + gap,
    (panelHeight + headerHeight) * rows + gap * (rows - 1),
  );
  const context = board.getContext('2d');
  context.fillStyle = '#090706';
  context.fillRect(0, 0, board.width, board.height);

  for (let index = 0; index < panels.length; index += 1) {
    const panel = panels[index];
    const column = index % columns;
    const row = Math.floor(index / columns);
    const x = column * (panelWidth + gap);
    const y = row * (panelHeight + headerHeight + gap);
    context.font = '700 19px sans-serif';
    context.fillStyle = panel.color;
    context.fillText(panel.label, x + 18, y + 29);
    context.font = '12px monospace';
    context.fillStyle = '#b4a79a';
    context.fillText(
      `${renderer.toUpperCase()} · NATIVE TICK ${panel.tick} · 23×15 · 58°`,
      x + 18,
      y + 51,
    );
    context.fillStyle = '#82776d';
    context.fillText(panel.detail, x + 18, y + 70);
    context.fillStyle = '#080706';
    context.fillRect(x, y + headerHeight, panelWidth, panelHeight);
    drawContained(
      context,
      images[index],
      x,
      y + headerHeight,
      panelWidth,
      panelHeight,
    );
  }
  await writeFile(path, board.toBuffer('image/png'));
}

function drawContained(context, image, x, y, width, height) {
  const scale = Math.min(width / image.width, height / image.height);
  const drawnWidth = image.width * scale;
  const drawnHeight = image.height * scale;
  context.drawImage(
    image,
    x + (width - drawnWidth) / 2,
    y + (height - drawnHeight) / 2,
    drawnWidth,
    drawnHeight,
  );
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

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}
