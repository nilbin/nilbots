#!/usr/bin/env node

/**
 * Capture Aegis Tortoise texture acceptance evidence from the production viewer.
 *
 * This script deliberately does not install or modify a model. Build `web/dist-review`
 * with the candidate already present, then pass the exact source GLB hash. Captures are
 * refused unless the native replay, GLB, Ember Forge presentation, semantic clips/team
 * material, and Fable camera contract are all pinned.
 *
 * Current hash-pinned native source (2026-07-31):
 *
 *   replay file SHA-256  ad62e56a750199928cc326fd8d0bd3732ea42405f36c9cfe59d6b2854940f36e
 *   canonical hash      bca049a6083ccf89468dfe749345f1e4c80c9352ea68bb3405b466c0d0e08463
 *
 * Usage:
 *
 *   (cd web && npm run build:review)
 *   node scripts/class-models/capture_bulwark_runtime_acceptance.mjs \
 *     --expected-model-sha <sha256-of-future-ga-uv-v2-foundry-glb>
 *
 * Use `--preflight` first when only the contract and hashes should be checked. A replay
 * regenerated after a schema/SDK bump must be supplied with both of its new hashes; the
 * old replay is evidence, not permission to approve a stale contract.
 */

import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { mkdir, readFile, readdir, stat, writeFile } from 'node:fs/promises';
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
const options = parseArguments(process.argv.slice(2));
const requireFromWeb = createRequire(join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const { createCanvas, loadImage } = requireFromWeb('@napi-rs/canvas');
const THREE = requireFromWeb('three');

const paths = {
  replay: resolve(
    repository,
    options.replay ??
      'sandbox/bulwark-runtime-review-v2-blue-orange/replay.json',
  ),
  model: resolve(
    repository,
    options.model ??
      'web/src/assets/class-looks/aegis-tortoise/model.glb',
  ),
  dist: resolve(repository, options.dist ?? 'web/dist-review'),
  output: resolve(
    repository,
    options.output ?? 'sandbox/aegis-tortoise-runtime-acceptance',
  ),
};

const pins = {
  replayFileSha256:
    options.expectedReplayFileSha ??
    'ad62e56a750199928cc326fd8d0bd3732ea42405f36c9cfe59d6b2854940f36e',
  canonicalReplayHash:
    options.expectedReplayHash ??
    'bca049a6083ccf89468dfe749345f1e4c80c9352ea68bb3405b466c0d0e08463',
  modelSha256: options.expectedModelSha ?? null,
};

const VIEWPORT = { width: 1600, height: 1000, deviceScaleFactor: 1 };
const CAMERA_PITCH_DEGREES = 58;
const CAMERA_FOV_DEGREES = 42;
const ARENA_MARGIN_TILES = 0.4;
const CROP_SOURCE_PIXELS = 112;
const CROP_SCALE = 4;
const REQUIRED_CLIPS = [
  'Shell_Deploy',
  'Tower_Deploy',
  'Mobile_Recoil',
  'Shell_Recoil',
  'Tower_Recoil',
];

/**
 * Static review states. The overview captures preserve the native whole-arena footprint;
 * the follow captures use the production Fit camera after selecting the owning unit.
 * Mobile is sampled at the historical proof time and projects to 41.06 px for the 1.16
 * tile live span on the 926x579 whole-arena canvas.
 */
const SAMPLES = [
  {
    id: 'mobile-blue',
    label: 'MOBILE · BLUE',
    time: 219.95,
    teamId: 0,
    unitId: 0,
    lifeId: 4,
    formId: 'bulwark-prime',
  },
  {
    id: 'mobile-orange',
    label: 'MOBILE · ORANGE',
    time: 219.95,
    teamId: 1,
    unitId: 1,
    lifeId: 1,
    formId: 'bulwark-child',
  },
  {
    id: 'shell-blue',
    label: 'SHELL · BLUE',
    time: 270.99,
    teamId: 0,
    unitId: 1,
    lifeId: 2,
    formId: 'bulwark-child-aegis-shell',
  },
  {
    id: 'shell-orange',
    label: 'SHELL · ORANGE',
    time: 294.2,
    teamId: 1,
    unitId: 1,
    lifeId: 1,
    formId: 'bulwark-child-aegis-shell',
  },
  {
    id: 'tower-blue',
    label: 'TOWER · BLUE',
    time: 325,
    teamId: 0,
    unitId: 1,
    lifeId: 3,
    formId: 'bulwark-child-turret',
  },
  {
    id: 'tower-orange',
    label: 'TOWER · ORANGE',
    time: 227.8,
    teamId: 1,
    unitId: 1,
    lifeId: 1,
    formId: 'bulwark-child-turret',
  },
];

const replayBytes = await readFile(paths.replay);
const replay = JSON.parse(replayBytes);
const modelBytes = await readFile(paths.model);
const glb = parseGlb(modelBytes);
const preflight = await validatePreflight();

if (options.preflight) {
  process.stdout.write(`${JSON.stringify(preflight, null, 2)}\n`);
  process.exit(0);
}
if (pins.modelSha256 === null) {
  throw new Error(
    'Capture requires --expected-model-sha. Pin the reviewed candidate; do not approve whichever GLB happens to be in the worktree.',
  );
}

await Promise.all([
  stat(join(paths.dist, 'index.html')),
  mkdir(join(paths.output, 'raw'), { recursive: true }),
  mkdir(join(paths.output, 'gameplay-41px'), { recursive: true }),
]);
const bundledModel = await locatePinnedDistModel();
preflight.build.bundledModel = bundledModel;

const server = await serve(paths.dist, replayBytes);
const browser = await chromium.launch({
  headless: true,
  args: ['--use-angle=swiftshader'],
});
const captures = [];

try {
  for (const sample of SAMPLES) {
    for (const cameraMode of ['whole-arena', 'action-follow']) {
      captures.push(
        await captureSample(browser, server, sample, cameraMode),
      );
    }
  }
} finally {
  await browser.close();
  await new Promise((accept) => server.close(accept));
}

const overview = captures.filter(
  (capture) => capture.cameraMode === 'whole-arena',
);
const contactSheet = await makeRuntimeContactSheet(overview);
const dimensions = overview[0];
if (
  captures.some(
    (capture) =>
      capture.cssWidth !== dimensions.cssWidth ||
      capture.cssHeight !== dimensions.cssHeight ||
      capture.devicePixelRatio !== VIEWPORT.deviceScaleFactor,
  )
) {
  throw new Error(`Capture viewport drift:\n${JSON.stringify(captures, null, 2)}`);
}

const report = {
  schemaVersion: 1,
  verdict: 'capture-complete-owner-review-required',
  preflight,
  runtime: {
    renderer: 'production WebGL viewer',
    browser: 'headless Chromium with SwiftShader',
    viewportCssPixels: VIEWPORT,
    arenaCanvasCssPixels: {
      width: dimensions.cssWidth,
      height: dimensions.cssHeight,
    },
    cameraPitchDegrees: CAMERA_PITCH_DEGREES,
    cameraFovDegrees: CAMERA_FOV_DEGREES,
    cameraModes: {
      wholeArena: 'production Fit toggle off',
      actionFollow: 'production Fit toggle on; owning stable unit selected',
    },
    replayMutation: false,
    modelInstallationByHarness: false,
  },
  captures,
  contactSheet,
  ownerReviewRequired: [
    'Foundry value hierarchy is dark enough under Ember Forge without becoming black.',
    'Medium-scale material breakup remains authored rather than pixel noise.',
    'Blue and orange semantic emitters plus renderer-owned floor glow remain legible.',
    'Mobile, Shell, and Tower read as one material family at normal zoom and native whole-arena scale.',
  ],
};
const reportPath = join(paths.output, 'runtime-acceptance-report.json');
await writeFile(reportPath, `${JSON.stringify(report, null, 2)}\n`);
process.stdout.write(
  `${JSON.stringify(
    {
      report: relative(repository, reportPath),
      replayHash: replay.replayHash,
      modelSha256: preflight.model.sha256,
      captures: captures.length,
      contactSheet: contactSheet.path,
      native41pxCrops: overview.map((capture) => capture.crop.path),
    },
    null,
    2,
  )}\n`,
);

async function validatePreflight() {
  if (pins.modelSha256 === null && !options.preflight) {
    throw new Error('--expected-model-sha is required for capture.');
  }

  const replayFileSha256 = sha256(replayBytes);
  const modelSha256 = sha256(modelBytes);
  assertEqual(
    replayFileSha256,
    pins.replayFileSha256,
    'native replay file SHA-256',
  );
  assertEqual(
    replay.replayHash,
    pins.canonicalReplayHash,
    'canonical replay hash',
  );
  if (pins.modelSha256 !== null) {
    assertEqual(modelSha256, pins.modelSha256, 'candidate model SHA-256');
  }

  const observedReplay = {
    replayVersion: replay?.header?.replayVersion,
    partial: replay?.partial,
    mapId: replay?.header?.contract?.map?.mapId,
    mapWidth: replay?.header?.contract?.map?.width,
    mapHeight: replay?.header?.contract?.map?.height,
    ticks: replay?.ticks?.length,
    rulesetId: replay?.header?.contract?.rules?.rulesetId,
    themeId: replay?.header?.presentation?.themeId,
    boundaryWall: replay?.header?.presentation?.map?.boundaryWall,
    interiorWall: replay?.header?.presentation?.map?.interiorWall,
  };
  assertDeepEqual(observedReplay, {
    replayVersion: 3,
    partial: false,
    mapId: 'frontline-labs-01-classes',
    mapWidth: 23,
    mapHeight: 15,
    ticks: 500,
    rulesetId: 'frontline-labs-1-bulwark-vs-bulwark-rig-facing-locked',
    themeId: 'ember-forge',
    boundaryWall: 'perimeter',
    interiorWall: 'cover',
  }, 'native replay contract');

  const participants = replay.header.provenance.participants
    .map(({ participantId, teamId, name, accent }) => ({
      participantId,
      teamId,
      name,
      accent,
    }))
    .sort((left, right) => left.teamId - right.teamId);
  assertDeepEqual(participants, [
    {
      participantId: 0,
      teamId: 0,
      name: 'GateStone',
      accent: '#38bdf8',
    },
    {
      participantId: 1,
      teamId: 1,
      name: 'IronRoot',
      accent: '#fb923c',
    },
  ], 'native blue/orange participants');

  const formPresentation = new Map(
    replay.header.presentation.forms.map((form) => [
      form.formId,
      `${form.lookId}/${form.projectileLookId}`,
    ]),
  );
  for (const formId of [
    'bulwark-child',
    'bulwark-prime',
    'bulwark-child-aegis-shell',
    'bulwark-prime-aegis-shell',
    'bulwark-child-turret',
    'bulwark-prime-turret',
  ]) {
    const suffix = formId.endsWith('aegis-shell')
      ? 'aegis-tortoise-shell/rebound-diamond'
      : formId.endsWith('turret')
        ? 'aegis-tortoise-turret/rebound-diamond'
        : 'aegis-tortoise/rebound-diamond';
    assertEqual(formPresentation.get(formId), suffix, `${formId} presentation`);
  }

  for (const sample of SAMPLES) validateSampleState(sample);
  validateMotionWindows();

  const animationNames = (glb.json.animations ?? []).map(
    (animation) => animation.name,
  );
  for (const clip of REQUIRED_CLIPS) {
    if (!animationNames.includes(clip))
      throw new Error(`Candidate GLB is missing semantic clip ${clip}.`);
  }
  const teamMaterials = (glb.json.materials ?? []).filter(
    (material) => material?.extras?.nilbotsRole === 'team-accent',
  );
  if (teamMaterials.length !== 1) {
    throw new Error(
      `Candidate GLB must contain exactly one team-accent material; found ${teamMaterials.length}.`,
    );
  }

  const cameraPath = join(repository, 'web/src/render/arenaCamera.ts');
  const scenePath = join(repository, 'web/src/render3d/arenaScene.ts');
  const themePath = join(
    repository,
    'web/src/assets/themes/ember-forge/theme.json',
  );
  const [cameraBytes, sceneBytes, themeBytes, distBytes] = await Promise.all([
    readFile(cameraPath),
    readFile(scenePath),
    readFile(themePath),
    options.preflight
      ? Promise.resolve(null)
      : readFile(join(paths.dist, 'index.html')),
  ]);
  const cameraSource = cameraBytes.toString('utf8');
  const sceneSource = sceneBytes.toString('utf8');
  for (const pin of [
    'const FOCUS_MARGIN_TILES = 1.5;',
    'const MIN_SPAN_TILES = 6;',
  ]) {
    if (!cameraSource.includes(pin))
      throw new Error(`Fable camera contract drifted: missing ${pin}`);
  }
  if (!sceneSource.includes('export const CAMERA_PITCH = (58 * Math.PI) / 180;'))
    throw new Error('Production 3D camera is no longer pinned to 58 degrees.');

  let cliVerification = null;
  if (!options.skipCliVerify) {
    cliVerification = execFileSync(
      'dotnet',
      [
        'run',
        '--project',
        'src/BotArena.Cli/BotArena.Cli.csproj',
        '--',
        'verify',
        paths.replay,
      ],
      { cwd: repository, encoding: 'utf8' },
    ).trim();
    if (!cliVerification.includes('OK: canonical replay v3 content'))
      throw new Error(`CLI replay verification did not pass:\n${cliVerification}`);
  }

  return {
    replay: {
      path: relative(repository, paths.replay),
      bytes: replayBytes.length,
      sha256: replayFileSha256,
      canonicalReplayHash: replay.replayHash,
      ...observedReplay,
      participants,
      cliVerification,
    },
    model: {
      path: relative(repository, paths.model),
      bytes: modelBytes.length,
      sha256: modelSha256,
      explicitlyPinned: pins.modelSha256 !== null,
      glbVersion: glb.version,
      animationNames,
      semanticTeamMaterial: teamMaterials[0].name ?? null,
    },
    presentation: {
      formCount: formPresentation.size,
      sampleStates: SAMPLES.map(({ id, time, teamId, unitId, lifeId, formId }) => ({
        id,
        time,
        teamId,
        unitId,
        lifeId,
        formId,
      })),
      motionWindows: {
        shellDeployReverse: '220.00..222.00',
        towerDeployRecoil: '225.00..227.60',
        towerReverse: '289.00..290.60',
        shellDeflectionRecoilReverse: '293.00..296.00',
      },
    },
    camera: {
      pitchDegrees: CAMERA_PITCH_DEGREES,
      minimumSpanTiles: 6,
      focusMarginTiles: 1.5,
      cameraSourceSha256: sha256(cameraBytes),
      sceneSourceSha256: sha256(sceneBytes),
      emberForgeManifestSha256: sha256(themeBytes),
    },
    build: {
      repositoryRevision: git('rev-parse', 'HEAD'),
      distIndexSha256: distBytes === null ? null : sha256(distBytes),
    },
  };
}

function validateSampleState(sample) {
  const tick = replay.ticks[Math.floor(sample.time)];
  const life = tick?.postState?.activeLives?.find(
    (candidate) =>
      candidate.actorId.teamId === sample.teamId &&
      candidate.actorId.unitId === sample.unitId &&
      candidate.actorId.lifeId === sample.lifeId,
  );
  if (!life) throw new Error(`${sample.id} actor is not active at ${sample.time}.`);
  assertEqual(life.formId, sample.formId, `${sample.id} form`);
  sample.position = life.position;
}

function validateMotionWindows() {
  const required = [
    [220, 'form-transition-started', 'shell-bulwark-child'],
    [221, 'form-transition-started', 'unstance-bulwark-child'],
    [225, 'form-transition-started', 'anchor-bulwark-child'],
    [226, 'attack', null],
    [289, 'form-transition-started', 'mobilize-bulwark-child'],
    [293, 'form-transition-started', 'shell-bulwark-child'],
    [294, 'projectile-deflected', null],
    [295, 'form-transition-started', 'unstance-bulwark-child'],
  ];
  for (const [tickIndex, kind, transitionId] of required) {
    const event = replay.ticks[tickIndex]?.events?.find(
      (candidate) =>
        candidate.kind === kind &&
        (transitionId === null || candidate.payload?.transitionId === transitionId),
    );
    if (!event) {
      throw new Error(
        `Native motion window drifted: tick ${tickIndex} lacks ${kind}${
          transitionId === null ? '' : `/${transitionId}`
        }.`,
      );
    }
  }
}

async function captureSample(browser, server, sample, cameraMode) {
  const page = await browser.newPage({
    viewport: { width: VIEWPORT.width, height: VIEWPORT.height },
    deviceScaleFactor: VIEWPORT.deviceScaleFactor,
  });
  const errors = [];
  const glbResponses = [];
  page.on('pageerror', (error) => errors.push(error.message));
  page.on('response', (response) => {
    if (!new URL(response.url()).pathname.endsWith('.glb')) return;
    const headers = response.headers();
    glbResponses.push({
      url: response.url(),
      pathname: new URL(response.url()).pathname,
      status: response.status(),
      bytes: Number(headers['content-length'] ?? 0),
    });
  });
  await page.addInitScript(() => {
    localStorage.setItem('nilbots.soundtrack.enabled.v1', 'false');
  });

  const rawPath = join(paths.output, 'raw', `${sample.id}-${cameraMode}.png`);
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
    const playMatch = page.getByRole('button', {
      name: 'Play match',
      exact: true,
    });
    await playMatch.waitFor({ state: 'visible', timeout: 120_000 });
    await playMatch.click();
    await page
      .locator('[data-play-overlay]')
      .waitFor({ state: 'detached', timeout: 30_000 });
    const actualTime = await preciseSeek(page, sample.time);

    const fit = page.getByRole('button', { name: /fit/i });
    const fitting = (await fit.getAttribute('aria-pressed')) === 'true';
    if (cameraMode === 'whole-arena' && fitting) await fit.click();
    if (cameraMode === 'action-follow') {
      if (!fitting) await fit.click();
      const card = page
        .locator('article')
        .filter({ hasText: `team ${sample.teamId} · unit ${sample.unitId}` });
      if ((await card.count()) !== 1)
        throw new Error(`${sample.id} did not resolve to one unit card.`);
      await card.getByRole('button', { name: 'Inspect', exact: true }).click();
    }

    // The shared critically damped camera and async PBR/model upload must both settle.
    await page.waitForTimeout(3_500);
    if ((await page.locator('[data-play-overlay]').count()) !== 0) {
      throw new Error('Play overlay returned before the production capture.');
    }
    if (errors.length > 0) throw new Error(errors.join('\n'));
    if (
      !glbResponses.some(
        (response) =>
          response.pathname === `/${bundledModel.path}` &&
          response.status === 200 &&
          response.bytes === bundledModel.bytes,
      )
    ) {
      throw new Error(
        `The pinned candidate GLB was not fetched by the production viewer:\n${JSON.stringify(
          glbResponses,
          null,
          2,
        )}`,
      );
    }

    await canvas.screenshot({ path: rawPath });
    const metrics = await canvas.evaluate((element) => ({
      width: element.width,
      height: element.height,
      cssWidth: element.clientWidth,
      cssHeight: element.clientHeight,
      devicePixelRatio: window.devicePixelRatio,
    }));
    const rawBytes = await readFile(rawPath);
    if (rawBytes.length < 50_000)
      throw new Error(`Capture looks blank (${rawBytes.length} bytes): ${rawPath}`);

    let crop = null;
    if (cameraMode === 'whole-arena') {
      crop = await makeNativeCrop(rawPath, sample, metrics);
    }
    return {
      id: `${sample.id}-${cameraMode}`,
      label: sample.label,
      time: actualTime,
      tick: Math.floor(actualTime),
      actor: {
        teamId: sample.teamId,
        unitId: sample.unitId,
        lifeId: sample.lifeId,
        formId: sample.formId,
        position: sample.position,
      },
      accent:
        replay.header.provenance.participants.find(
          (participant) => participant.teamId === sample.teamId,
        )?.accent ?? null,
      cameraMode,
      path: relative(repository, rawPath),
      bytes: rawBytes.length,
      sha256: sha256(rawBytes),
      glbResponses,
      crop,
      ...metrics,
    };
  } finally {
    await page.close();
  }
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
      `Pointer seek was too imprecise (${current} for ${target}); refusing a long synthetic key walk.`,
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

async function makeNativeCrop(rawPath, sample, metrics) {
  const image = await loadImage(rawPath);
  const projection = projectWholeArena(sample.position, metrics);
  const left = Math.round(projection.x - CROP_SOURCE_PIXELS / 2);
  const top = Math.round(projection.y - CROP_SOURCE_PIXELS / 2);
  if (
    left < 0 ||
    top < 0 ||
    left + CROP_SOURCE_PIXELS > image.width ||
    top + CROP_SOURCE_PIXELS > image.height
  ) {
    throw new Error(`${sample.id} native crop falls outside the arena canvas.`);
  }
  const size = CROP_SOURCE_PIXELS * CROP_SCALE;
  const output = createCanvas(size, size);
  const context = output.getContext('2d');
  context.imageSmoothingEnabled = false;
  context.drawImage(
    image,
    left,
    top,
    CROP_SOURCE_PIXELS,
    CROP_SOURCE_PIXELS,
    0,
    0,
    size,
    size,
  );
  const cropPath = join(
    paths.output,
    'gameplay-41px',
    `${sample.id}-native-crop-4x.png`,
  );
  const bytes = output.toBuffer('image/png');
  await writeFile(cropPath, bytes);
  return {
    path: relative(repository, cropPath),
    bytes: bytes.length,
    sha256: sha256(bytes),
    sourceBounds: {
      left,
      top,
      width: CROP_SOURCE_PIXELS,
      height: CROP_SOURCE_PIXELS,
    },
    enlargement: `${CROP_SCALE}x nearest-neighbour inspection only`,
    projectedLiveSpanPixels: projection.liveSpanPixels,
    liveSpanTiles: 1.16,
  };
}

async function makeRuntimeContactSheet(overview) {
  if (overview.length !== SAMPLES.length) {
    throw new Error(
      `Expected ${SAMPLES.length} whole-arena crops; received ${overview.length}.`,
    );
  }
  const columns = 2;
  const rows = Math.ceil(overview.length / columns);
  const tile = CROP_SOURCE_PIXELS * CROP_SCALE;
  const labelHeight = 38;
  const gap = 12;
  const width = columns * tile + (columns + 1) * gap;
  const height = rows * (tile + labelHeight) + (rows + 1) * gap;
  const output = createCanvas(width, height);
  const context = output.getContext('2d');
  context.fillStyle = '#111820';
  context.fillRect(0, 0, width, height);
  context.font = '600 20px sans-serif';
  context.textBaseline = 'middle';

  for (const [index, capture] of overview.entries()) {
    if (!capture.crop) throw new Error(`${capture.id} has no native crop.`);
    const column = index % columns;
    const row = Math.floor(index / columns);
    const left = gap + column * (tile + gap);
    const top = gap + row * (tile + labelHeight + gap);
    const image = await loadImage(resolve(repository, capture.crop.path));
    context.drawImage(image, left, top, tile, tile);
    context.strokeStyle = capture.accent;
    context.lineWidth = 4;
    context.strokeRect(left + 2, top + 2, tile - 4, tile - 4);
    context.fillStyle = '#edf5fa';
    context.fillText(capture.label, left + 8, top + tile + labelHeight / 2);
  }

  const path = join(paths.output, 'runtime-contact-sheet.png');
  const bytes = output.toBuffer('image/png');
  await writeFile(path, bytes);
  return {
    path: relative(repository, path),
    bytes: bytes.length,
    sha256: sha256(bytes),
    layout: 'Mobile/Shell/Tower rows; blue/orange columns',
    source: `${CROP_SOURCE_PIXELS}px native context crops enlarged ${CROP_SCALE}x with nearest-neighbour sampling`,
  };
}

function projectWholeArena(position, metrics) {
  const aspect = metrics.cssWidth / metrics.cssHeight;
  const mapWidth = replay.header.contract.map.width;
  const mapHeight = replay.header.contract.map.height;
  const frameWidth = Math.max(
    mapWidth + ARENA_MARGIN_TILES,
    (mapHeight + ARENA_MARGIN_TILES) * aspect,
  );
  const frameHeight = frameWidth / aspect;
  const span = Math.max(frameWidth / aspect, frameHeight);
  const distance =
    span / 2 / Math.tan((CAMERA_FOV_DEGREES * Math.PI) / 360);
  const pitch = (CAMERA_PITCH_DEGREES * Math.PI) / 180;
  const camera = new THREE.PerspectiveCamera(
    CAMERA_FOV_DEGREES,
    aspect,
    0.1,
    200,
  );
  camera.position.set(
    mapWidth / 2,
    Math.sin(pitch) * distance * 1.02,
    mapHeight / 2 + Math.cos(pitch) * distance * 1.02,
  );
  camera.lookAt(mapWidth / 2, 0, mapHeight / 2);
  camera.updateMatrixWorld();
  camera.updateProjectionMatrix();

  const project = (x, z) => {
    const point = new THREE.Vector3(x, 0.25, z).project(camera);
    return {
      x: ((point.x + 1) / 2) * metrics.width,
      y: ((1 - point.y) / 2) * metrics.height,
    };
  };
  const x = position.x + 0.5;
  const z = position.y + 0.5;
  const centre = project(x, z);
  const halfLiveSpan = 1.16 / 2;
  const left = project(x - halfLiveSpan, z);
  const right = project(x + halfLiveSpan, z);
  return {
    ...centre,
    liveSpanPixels: Math.hypot(right.x - left.x, right.y - left.y),
  };
}

function serve(root, sourceReplayBytes) {
  const server = createServer(async (request, response) => {
    const pathname = decodeURIComponent(
      new URL(request.url ?? '/', 'http://127.0.0.1').pathname,
    );
    if (pathname === '/replay.json') {
      response.writeHead(200, {
        'cache-control': 'no-store',
        'content-type': 'application/json; charset=utf-8',
      });
      response.end(sourceReplayBytes);
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
  return new Promise((accept, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', () => accept(server));
  });
}

function address(server) {
  const value = server.address();
  if (!value || typeof value === 'string')
    throw new Error('Acceptance server did not expose a TCP address.');
  return `http://127.0.0.1:${value.port}/?standalone&audio=off`;
}

async function locatePinnedDistModel() {
  const assetDirectory = join(paths.dist, 'assets');
  const entries = await readdir(assetDirectory, { withFileTypes: true });
  const candidates = [];
  for (const entry of entries) {
    if (!entry.isFile() || !entry.name.endsWith('.glb')) continue;
    const path = join(assetDirectory, entry.name);
    const metadata = await stat(path);
    if (metadata.size !== modelBytes.length) continue;
    const digest = await sha256File(path);
    if (digest !== pins.modelSha256) continue;
    candidates.push({
      path: relative(paths.dist, path),
      bytes: metadata.size,
      sha256: digest,
    });
  }
  if (candidates.length !== 1) {
    throw new Error(
      `Expected one hash-pinned candidate in the review build; found ${candidates.length}.`,
    );
  }
  return candidates[0];
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

function parseGlb(bytes) {
  if (bytes.length < 20 || bytes.toString('ascii', 0, 4) !== 'glTF')
    throw new Error('Candidate is not a GLB file.');
  const version = bytes.readUInt32LE(4);
  const declaredLength = bytes.readUInt32LE(8);
  if (version !== 2 || declaredLength !== bytes.length)
    throw new Error(
      `Invalid GLB header: version ${version}, length ${declaredLength}/${bytes.length}.`,
    );
  const jsonLength = bytes.readUInt32LE(12);
  const jsonType = bytes.readUInt32LE(16);
  if (jsonType !== 0x4e4f534a)
    throw new Error('The first GLB chunk is not JSON.');
  const source = bytes
    .subarray(20, 20 + jsonLength)
    .toString('utf8')
    .replace(/[\u0000\u0020]+$/u, '');
  return { version, json: JSON.parse(source) };
}

function parseArguments(arguments_) {
  const result = {
    preflight: false,
    skipCliVerify: false,
  };
  const values = new Map([
    ['--replay', 'replay'],
    ['--model', 'model'],
    ['--dist', 'dist'],
    ['--output', 'output'],
    ['--expected-replay-file-sha', 'expectedReplayFileSha'],
    ['--expected-replay-hash', 'expectedReplayHash'],
    ['--expected-model-sha', 'expectedModelSha'],
  ]);
  for (let index = 0; index < arguments_.length; index += 1) {
    const argument = arguments_[index];
    if (argument === '--preflight') {
      result.preflight = true;
      continue;
    }
    if (argument === '--skip-cli-verify') {
      result.skipCliVerify = true;
      continue;
    }
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

function assertEqual(observed, wanted, label) {
  if (observed !== wanted)
    throw new Error(`${label} drifted: expected ${wanted}, received ${observed}.`);
}

function assertDeepEqual(observed, wanted, label) {
  if (JSON.stringify(observed) !== JSON.stringify(wanted)) {
    throw new Error(
      `${label} drifted:\n${JSON.stringify({ wanted, observed }, null, 2)}`,
    );
  }
}
