#!/usr/bin/env node

// Reproduce with:
//   dotnet run --project src/BotArena.Cli/BotArena.Cli.csproj -- \
//     experiment frontline-labs \
//     --bot arena-bots/frontline-labs/classes-wave-4-2026-07-30/iron-root \
//     --opponent arena-bots/frontline-labs/classes-wave-4-2026-07-30/iron-root \
//     --runtime in-process --seed 42 \
//     --out /tmp/nilbots-aegis-runtime-review-mirror
//   (cd web && npm run build:review)
//   PRESENTATION_OVERLAY_PATH=/path/to/explicit-overlay.json \
//     node scripts/render-aegis-runtime-review.mjs
//
// REPLAY_PATH, PRESENTATION_OVERLAY_PATH, DIST_DIRECTORY, OUTPUT_DIRECTORY and
// REVIEW_TICK may override the defaults. A source replay with null presentation is
// never rendered directly: the explicit overlay is applied to a non-canonical review
// document with partial=true, replayHash=null and result=null. Pass --audit-only to
// validate the source/overlay pair without launching a browser or writing evidence.

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
import { basename, dirname, extname, join, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repository = resolve(scriptDirectory, '..');
const replayPath = resolve(
  process.env.REPLAY_PATH ??
    '/tmp/nilbots-aegis-runtime-review-mirror/replay.json',
);
const presentationOverlayPath = process.env.PRESENTATION_OVERLAY_PATH
  ? resolve(process.env.PRESENTATION_OVERLAY_PATH)
  : null;
const auditOnly = process.argv.includes('--audit-only');
const dist = resolve(
  process.env.DIST_DIRECTORY ??
    join(repository, 'web', 'dist-review'),
);
const outputDirectory = resolve(
  process.env.OUTPUT_DIRECTORY ??
    join(
      repository,
      'art',
      'class-models',
      'aegis-tortoise-mobile',
      'review',
      'runtime-presentation-candidate',
    ),
);
const sourceModelPath = join(
  repository,
  'web',
  'src',
  'assets',
  'class-looks',
  'aegis-tortoise',
  'model.glb',
);
const requireFromWeb = createRequire(
  join(repository, 'web', 'package.json'),
);
const { chromium } = requireFromWeb('playwright');
const { createCanvas, loadImage } = requireFromWeb('@napi-rs/canvas');

const viewport = { width: 1600, height: 1000, deviceScaleFactor: 1 };
const movementTick = Number(process.env.REVIEW_TICK ?? 5);
const idleTick = movementTick + 1;
const phases = [0.25, 0.5, 0.75];
const MIME = {
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
};
const sourceReplayBytes = await readFile(replayPath);
const sourceReplay = JSON.parse(sourceReplayBytes);
validateSourceReplay(sourceReplay);
const sourceReplayHash = sha256(sourceReplayBytes);
const preparedReplay = await prepareReviewReplay({
  sourceReplay,
  sourceReplayBytes,
  sourceReplayHash,
  presentationOverlayPath,
});
const replay = preparedReplay.document;
const replayBytes = preparedReplay.bytes;
const presentationReview = preparedReplay.presentationReview;
const sourceModelBytes = await readFile(sourceModelPath);
const sourceModelHash = sha256(sourceModelBytes);
const sourceModelJson = glbJson(sourceModelBytes);
const animationClips =
  sourceModelJson.animations?.map((animation) => ({
    name: animation.name,
    channelCount: animation.channels?.length ?? 0,
    samplerCount: animation.samplers?.length ?? 0,
  })) ?? [];
const expectedClips = ['Aegis_Idle', 'Aegis_Walk'];

for (const name of expectedClips)
  if (!animationClips.some((clip) => clip.name === name))
    throw new Error(`Aegis GLB is missing declared clip '${name}'.`);

const topology = replay.header?.contract?.topology;
const teamClasses = topology?.teams?.map((team) => ({
  teamId: team.teamId,
  classId: team.classId,
})) ?? [];
const participantAccents =
  replay.header?.provenance?.participants?.map(
    (participant) => participant.accent,
  ) ?? [];
if (
  teamClasses.length !== 2 ||
  teamClasses.some((team) => team.classId !== 'bulwark')
)
  throw new Error(
    `Review replay must be Bulwark versus Bulwark, got ${JSON.stringify(teamClasses)}.`,
  );
if (
  participantAccents.length !== 2 ||
  new Set(participantAccents).size !== 1
)
  throw new Error(
    `Review replay entrants must share one accent so team fallback is exercised, got ${JSON.stringify(participantAccents)}.`,
  );

const movement = movementEvidence(replay, movementTick);
if (
  movement.length !== 2 ||
  movement.some(
    (actor) =>
      actor.formId !== 'bulwark-prime' ||
      (actor.open.x === actor.close.x && actor.open.y === actor.close.y),
  )
)
  throw new Error(
    `Tick ${movementTick} must move both Bulwark primes: ${JSON.stringify(movement)}.`,
  );
const idle = movementEvidence(replay, idleTick);
if (
  idle.length !== 2 ||
  idle.some(
    (actor) =>
      actor.open.x !== actor.close.x ||
      actor.open.y !== actor.close.y,
  )
)
  throw new Error(
    `Tick ${idleTick} must hold both Bulwark primes still: ${JSON.stringify(idle)}.`,
  );

if (auditOnly) {
  process.stdout.write(
    `${JSON.stringify(
      {
        status: 'audited',
        generatedBy: 'scripts/render-aegis-runtime-review.mjs',
        sourceReplay: sourceReplayEvidence({
          replay: sourceReplay,
          bytes: sourceReplayBytes,
          path: replayPath,
        }),
        reviewDocument: reviewDocumentEvidence({
          replay,
          bytes: replayBytes,
          presentationReview,
        }),
        presentationReview,
        model: {
          source: 'web/src/assets/class-looks/aegis-tortoise/model.glb',
          byteLength: sourceModelBytes.length,
          sha256: sourceModelHash,
          animations: animationClips,
        },
      },
      null,
      2,
    )}\n`,
  );
  process.exit(0);
}

await stat(join(dist, 'index.html'));
await mkdir(outputDirectory, { recursive: true });
const aegisAsset = await findBuiltAsset(
  join(dist, 'assets'),
  sourceModelHash,
);
const aegisRelative = `assets/${basename(aegisAsset)}`;

const candidateServer = await serve({ rejectAegis: false });
const fallbackServer = await serve({ rejectAegis: true });
const browser = await chromium.launch({
  headless: true,
  args: ['--use-angle=swiftshader'],
});

const paths = {
  auto: join(outputDirectory, 'aegis-presentation-review-auto-fit-v2.png'),
  walk25: join(
    outputDirectory,
    'aegis-presentation-review-walk-025-v2.png',
  ),
  walk50: join(
    outputDirectory,
    'aegis-presentation-review-walk-050-v2.png',
  ),
  walk75: join(
    outputDirectory,
    'aegis-presentation-review-walk-075-v2.png',
  ),
  idle: join(outputDirectory, 'aegis-presentation-review-idle-v2.png'),
  walk25Repeat: join(
    outputDirectory,
    'aegis-presentation-review-walk-025-repeat-v2.png',
  ),
  fallback: join(
    outputDirectory,
    'aegis-presentation-review-svg-fallback-v2.png',
  ),
  comparison: join(
    outputDirectory,
    'aegis-presentation-review-glb-fallback-comparison-v2.png',
  ),
  phases: join(
    outputDirectory,
    'aegis-presentation-review-walk-phases-v2.png',
  ),
  evidence: join(outputDirectory, 'aegis-runtime-review-v2.json'),
};

let candidate;
let fallback;
try {
  candidate = await captureCandidate(browser, candidateServer);
  fallback = await captureFallback(browser, fallbackServer);

  await comparisonBoard(
    paths.walk50,
    paths.fallback,
    paths.comparison,
  );
  await phaseBoard(
    [paths.walk25, paths.walk50, paths.walk75],
    paths.phases,
  );

  const screenshotHashes = {};
  for (const [key, path] of Object.entries(paths)) {
    if (key === 'evidence') continue;
    screenshotHashes[key] = sha256(await readFile(path));
  }
  const repeatDifference = await pixelDifference(
    paths.walk25,
    paths.walk25Repeat,
  );
  // SwiftShader can round a fragment one 8-bit value differently between otherwise
  // identical draws. Keep that floor explicit and extremely tight; exact transform
  // equality is covered by lookAnimation3d.test.ts, while this checks the whole browser.
  const deterministicRepeat =
    repeatDifference.changedPixels <= 8 &&
    repeatDifference.maxChannelDelta <= 1;
  const fallbackIsDistinct =
    screenshotHashes.walk50 !== screenshotHashes.fallback;
  if (!deterministicRepeat)
    throw new Error(
      `Returning to movement phase 0.25 exceeded the raster-equivalence floor: ${JSON.stringify(repeatDifference)}.`,
    );
  if (!fallbackIsDistinct)
    throw new Error('The GLB and forced SVG fallback captures are identical.');

  const evidence = {
    version: 2,
    generatedBy: 'scripts/render-aegis-runtime-review.mjs',
    reviewStatus: 'candidate',
    approvalEvidence: false,
    approvalClaim: null,
    sourceReplay: {
      generatedWith:
        'dotnet run --project src/BotArena.Cli/BotArena.Cli.csproj -- experiment frontline-labs --bot arena-bots/frontline-labs/classes-wave-4-2026-07-30/iron-root --opponent arena-bots/frontline-labs/classes-wave-4-2026-07-30/iron-root --runtime in-process --seed 42 --out /tmp/nilbots-aegis-runtime-review-mirror',
      ...sourceReplayEvidence({
        replay: sourceReplay,
        bytes: sourceReplayBytes,
        path: replayPath,
      }),
      runtime: 'in-process diagnostic',
      teams: teamClasses,
      participantAccents,
    },
    reviewDocument: reviewDocumentEvidence({
      replay,
      bytes: replayBytes,
      presentationReview,
    }),
    presentationReview,
    frame: {
      tick: movementTick,
      phases,
      movement,
      idle: {
        time: idleTick + 0.5,
        actors: idle,
      },
      cameraPitchDegrees: 58,
      autoFitCapture: true,
      closeCapture: {
        minSpanTiles: 8,
        method:
          'turn fit off, then use one supported wheel-zoom gesture after the camera settles',
      },
      viewport,
      canvas: candidate.canvas,
      teamAccentFallback: {
        team0: '#38bdf8',
        team1: '#fb923c',
      },
    },
    model: {
      source: 'web/src/assets/class-looks/aegis-tortoise/model.glb',
      byteLength: sourceModelBytes.length,
      sha256: sourceModelHash,
      builtAsset: aegisRelative,
      animations: animationClips,
      skinCount: sourceModelJson.skins?.length ?? 0,
      teamAccentMaterialCount:
        sourceModelJson.materials?.filter(
          (material) =>
            material.extras?.nilbotsRole === 'team-accent',
        ).length ?? 0,
      candidateRequest: candidate.aegisRequest,
      forcedFallbackRequest: fallback.aegisRequest,
    },
    assertions: {
      sourceIsFinalReplayV3: true,
      sourceIsFrontlineLabs23By15: true,
      explicitNonControlRoomPresentation: true,
      sourceReplayHashPreservedSeparately: true,
      presentationOverlayIsNonCanonical:
        presentationReview.mode !== 'presentation-only-overlay' ||
        (replay.partial === true &&
          replay.replayHash === null &&
          replay.result === null),
      declaredClipsPresent: true,
      bothBulwarkTeamsMoveAcrossReviewedTick: true,
      bothBulwarkTeamsIdleAcrossReviewedTick: true,
      candidateAegisGlbServed: candidate.aegisRequest.status === 200,
      forcedAegisGlbRejected: fallback.aegisRequest.status === 404,
      glbAndSvgFallbackAreVisuallyDistinct: fallbackIsDistinct,
      repeatedReplayPhaseIsRasterEquivalent: deterministicRepeat,
      repeatedReplayPhaseDifference: repeatDifference,
      exactReplayTimeSamplingTest:
        'web/tests/lookAnimation3d.test.ts asserts exact history-independent object transforms',
      pageErrors: {
        candidate: candidate.errors,
        fallback: fallback.errors,
      },
    },
    screenshots: Object.fromEntries(
      Object.entries(paths)
        .filter(([key]) => key !== 'evidence')
        .map(([key, path]) => [
          key,
          {
            file: basename(path),
            sha256: screenshotHashes[key],
          },
        ]),
    ),
    limitations: [
      'The visual replay uses the fast in-process runtime. Its replay is engine-produced and deterministic, but it is not submission-equivalent WASM balance evidence.',
      'This is candidate model/runtime evidence only. approvalEvidence remains false and no Frontline V4 or map-theme approval is claimed.',
      presentationReview.mode === 'presentation-only-overlay'
        ? 'The map/form presentation is an explicitly labelled review-only overlay. The served document is partial, has null replayHash and result, and cannot be presented as the canonical source replay.'
        : 'The presentation is authored by the canonical source replay; its explicit non-Control-Room theme is still not treated as an approval claim.',
      'The fallback case is forced by returning HTTP 404 for only the hashed Aegis GLB; every other candidate build asset is unchanged.',
      'This stage covers the mobile Aegis body. The shell, turret and projectile remain on their canonical SVG-derived fallback until their own reviewed GLBs land.',
      'The browser repeat uses an explicit SwiftShader raster floor of at most eight changed pixels and one 8-bit channel value; the underlying animation sampler is covered separately by exact numeric assertions.',
    ],
  };
  if (
    evidence.assertions.pageErrors.candidate.length > 0 ||
    evidence.assertions.pageErrors.fallback.length > 0
  )
    throw new Error(
      `Browser review emitted page errors: ${JSON.stringify(evidence.assertions.pageErrors)}`,
    );
  if (!evidence.assertions.candidateAegisGlbServed)
    throw new Error('Candidate browser review never received the Aegis GLB.');
  if (!evidence.assertions.forcedAegisGlbRejected)
    throw new Error('Fallback browser review did not reject the Aegis GLB.');

  await writeFile(
    paths.evidence,
    `${JSON.stringify(evidence, null, 2)}\n`,
  );
  process.stdout.write(`${JSON.stringify(evidence, null, 2)}\n`);
} finally {
  await browser.close();
  await Promise.all([
    close(candidateServer.server),
    close(fallbackServer.server),
  ]);
}

async function captureCandidate(browserInstance, reviewServer) {
  const page = await reviewPage(browserInstance);
  try {
    const aegisResponse = waitForAegis(page);
    await openReplay(page, reviewServer.server);
    const response = await aegisResponse;
    await seek(page, movementTick + 0.5);
    await page.waitForTimeout(6_000);
    await screenshot(page, paths.auto);

    await minimumZoom(page);
    await seek(page, movementTick + phases[0]);
    await settleFrames(page);
    await screenshot(page, paths.walk25);
    await seek(page, movementTick + phases[1]);
    await settleFrames(page);
    await screenshot(page, paths.walk50);
    await seek(page, movementTick + phases[2]);
    await settleFrames(page);
    await screenshot(page, paths.walk75);

    // Absolute replay-time seeking must be history-independent. Return to the first
    // sampled phase after visiting two other phases and require raster-equivalent output.
    await seek(page, movementTick + phases[0]);
    await settleFrames(page);
    await screenshot(page, paths.walk25Repeat);
    await seek(page, idleTick + 0.5);
    await settleFrames(page);
    await screenshot(page, paths.idle);

    return {
      canvas: await canvasSize(page),
      errors: page.reviewErrors,
      aegisRequest: responseSummary(response),
    };
  } finally {
    await page.close();
  }
}

async function captureFallback(browserInstance, reviewServer) {
  const page = await reviewPage(browserInstance);
  try {
    const aegisResponse = waitForAegis(page);
    await openReplay(page, reviewServer.server);
    const response = await aegisResponse;
    await seek(page, movementTick + 0.5);
    await page.waitForTimeout(6_000);
    await minimumZoom(page);
    await seek(page, movementTick + 0.5);
    await settleFrames(page);
    await screenshot(page, paths.fallback);
    return {
      canvas: await canvasSize(page),
      errors: page.reviewErrors,
      aegisRequest: responseSummary(response),
    };
  } finally {
    await page.close();
  }
}

async function reviewPage(browserInstance) {
  const page = await browserInstance.newPage({
    viewport: { width: viewport.width, height: viewport.height },
    deviceScaleFactor: viewport.deviceScaleFactor,
  });
  page.reviewErrors = [];
  page.on('pageerror', (error) =>
    page.reviewErrors.push(error.message),
  );
  await page.addInitScript(() => {
    localStorage.setItem('nilbots.soundtrack.enabled.v1', 'false');
  });
  return page;
}

function waitForAegis(page) {
  return page.waitForResponse(
    (response) =>
      new URL(response.url()).pathname === `/${aegisRelative}`,
    { timeout: 120_000 },
  );
}

async function openReplay(page, server) {
  await page.goto(address(server), {
    waitUntil: 'domcontentloaded',
    timeout: 120_000,
  });
  const arena = page.locator('section[aria-label="Arena"]');
  await arena.locator('canvas').waitFor({
    state: 'visible',
    timeout: 120_000,
  });
  await page
    .getByText(/Loading arena/)
    .waitFor({ state: 'hidden', timeout: 120_000 });
  // One explicit step pauses the transport immediately. Otherwise a slow headless load
  // can advance hundreds of ticks while the review waits for model parsing.
  await page.keyboard.press('ArrowLeft');
}

async function seek(page, time) {
  const root = page.locator(
    '[aria-label="Match timeline — drag to seek"]',
  );
  const thumb = page.locator(
    '[role="slider"][aria-label="Playhead"]',
  );
  await root.waitFor({ state: 'visible', timeout: 120_000 });
  await thumb.waitFor({ state: 'attached', timeout: 120_000 });
  await root.scrollIntoViewIfNeeded();
  const maximum = Number(await thumb.getAttribute('aria-valuemax'));
  if (!Number.isFinite(maximum) || time < 0 || time > maximum)
    throw new Error(
      `Cannot seek to ${time}; timeline maximum is ${maximum}.`,
    );
  const box = await root.boundingBox();
  if (!box) throw new Error('Timeline has no measurable bounds.');

  let ratio = time / maximum;
  for (let attempt = 0; attempt < 6; attempt += 1) {
    await page.mouse.click(
      box.x + box.width * Math.max(0, Math.min(ratio, 1)),
      box.y + box.height / 2,
    );
    await settleFrames(page);
    const current = Number(await thumb.getAttribute('aria-valuenow'));
    if (Math.abs(current - time) <= 0.011) return;
    ratio += (time - current) / maximum;
  }
  throw new Error(
    `Timeline settled at ${await thumb.getAttribute('aria-valuenow')} instead of ${time}.`,
  );
}

async function minimumZoom(page) {
  const canvas = page.locator('section[aria-label="Arena"] canvas');
  const box = await canvas.boundingBox();
  if (!box) throw new Error('Arena canvas has no measurable bounds.');
  const fit = page
    .getByRole('button')
    .filter({ hasText: 'fit' })
    .first();
  if ((await fit.getAttribute('aria-pressed')) === 'true') {
    await fit.click();
    await page.waitForFunction(() => {
      const button = [...document.querySelectorAll('button')].find(
        (candidate) => candidate.textContent?.includes('fit'),
      );
      return button?.getAttribute('aria-pressed') === 'false';
    });
    await settleFrames(page);
  }
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.wheel(0, -5_000);
  await page.waitForFunction(() => {
    const fit = [...document.querySelectorAll('button')].find((button) =>
      button.textContent?.includes('fit'),
    );
    return fit?.getAttribute('aria-pressed') === 'false';
  });
  // The product camera is a critically damped spring. Eight seconds puts its residual
  // below raster precision before the exact phase comparisons begin.
  await page.waitForTimeout(8_000);
}

async function settleFrames(page) {
  await page.evaluate(
    () =>
      new Promise((accept) =>
        requestAnimationFrame(() =>
          requestAnimationFrame(() =>
            requestAnimationFrame(accept),
          ),
        ),
      ),
  );
}

async function screenshot(page, path) {
  await page
    .locator('section[aria-label="Arena"] canvas')
    .screenshot({ path });
}

async function canvasSize(page) {
  return page
    .locator('section[aria-label="Arena"] canvas')
    .evaluate((canvas) => ({
      width: canvas.width,
      height: canvas.height,
      cssWidth: canvas.clientWidth,
      cssHeight: canvas.clientHeight,
    }));
}

function responseSummary(response) {
  return {
    status: response.status(),
    urlPath: new URL(response.url()).pathname,
    contentLength: Number(
      response.headers()['content-length'] ?? 0,
    ),
  };
}

async function comparisonBoard(candidatePath, fallbackPath, output) {
  const [candidateImage, fallbackImage] = await Promise.all([
    loadImage(candidatePath),
    loadImage(fallbackPath),
  ]);
  const header = 76;
  const gap = 2;
  const board = createCanvas(
    candidateImage.width + fallbackImage.width + gap,
    candidateImage.height + header,
  );
  const context = board.getContext('2d');
  context.fillStyle = '#090706';
  context.fillRect(0, 0, board.width, board.height);
  context.font = '700 20px sans-serif';
  context.fillStyle = '#8bd5a6';
  context.fillText('AUTHORED AEGIS GLB', 22, 32);
  context.fillStyle = '#f2a36d';
  context.fillText(
    'FORCED SVG-DERIVED FALLBACK',
    candidateImage.width + gap + 22,
    32,
  );
  context.font = '13px monospace';
  context.fillStyle = '#a9947d';
  const subtitle =
    `${presentationReview.banner} · t=${movementTick + 0.5} · 58° camera`;
  context.fillText(subtitle, 22, 58);
  context.fillText(
    subtitle,
    candidateImage.width + gap + 22,
    58,
  );
  context.drawImage(candidateImage, 0, header);
  context.drawImage(
    fallbackImage,
    candidateImage.width + gap,
    header,
  );
  await writeFile(output, board.toBuffer('image/png'));
}

async function phaseBoard(inputs, output) {
  const images = await Promise.all(inputs.map((path) => loadImage(path)));
  const header = 76;
  const gap = 2;
  const width =
    images.reduce((sum, image) => sum + image.width, 0) +
    gap * (images.length - 1);
  const board = createCanvas(width, images[0].height + header);
  const context = board.getContext('2d');
  context.fillStyle = '#090706';
  context.fillRect(0, 0, board.width, board.height);
  context.font = '700 20px sans-serif';
  context.fillStyle = '#8bd5a6';
  let x = 0;
  for (let index = 0; index < images.length; index += 1) {
    context.fillText(
      `WALK PHASE ${phases[index].toFixed(2)}`,
      x + 22,
      32,
    );
    context.drawImage(images[index], x, header);
    x += images[index].width + gap;
  }
  context.font = '13px monospace';
  context.fillStyle = '#a9947d';
  context.fillText(
    `${presentationReview.banner} · tick ${movementTick} · paused seeks`,
    22,
    58,
  );
  await writeFile(output, board.toBuffer('image/png'));
}

async function pixelDifference(firstPath, secondPath) {
  const [first, second] = await Promise.all([
    loadImage(firstPath),
    loadImage(secondPath),
  ]);
  if (first.width !== second.width || first.height !== second.height)
    throw new Error('Repeated phase captures have different dimensions.');
  const canvas = createCanvas(first.width, first.height);
  const context = canvas.getContext('2d');
  context.drawImage(first, 0, 0);
  const left = context.getImageData(
    0,
    0,
    first.width,
    first.height,
  ).data;
  context.clearRect(0, 0, first.width, first.height);
  context.drawImage(second, 0, 0);
  const right = context.getImageData(
    0,
    0,
    second.width,
    second.height,
  ).data;
  let changedPixels = 0;
  let maxChannelDelta = 0;
  for (let offset = 0; offset < left.length; offset += 4) {
    let pixelDelta = 0;
    for (let channel = 0; channel < 4; channel += 1)
      pixelDelta = Math.max(
        pixelDelta,
        Math.abs(left[offset + channel] - right[offset + channel]),
      );
    if (pixelDelta === 0) continue;
    changedPixels += 1;
    maxChannelDelta = Math.max(maxChannelDelta, pixelDelta);
  }
  return {
    changedPixels,
    totalPixels: first.width * first.height,
    changedPercent:
      (changedPixels / (first.width * first.height)) * 100,
    maxChannelDelta,
    allowed: {
      changedPixels: 8,
      maxChannelDelta: 1,
    },
  };
}

async function findBuiltAsset(directory, wantedHash) {
  for (const entry of await readdir(directory)) {
    if (extname(entry) !== '.glb') continue;
    const path = join(directory, entry);
    if (sha256(await readFile(path)) === wantedHash) return path;
  }
  throw new Error(
    `No built GLB in ${directory} matches Aegis ${wantedHash}. Run npm run build:review first.`,
  );
}

async function serve({ rejectAegis }) {
  const server = createServer(async (request, response) => {
    const pathname = decodeURIComponent(
      new URL(request.url ?? '/', 'http://127.0.0.1').pathname,
    );
    if (pathname === '/replay.json') {
      response.writeHead(200, {
        'cache-control': 'no-store',
        'content-length': replayBytes.length,
        'content-type': 'application/json; charset=utf-8',
      });
      response.end(replayBytes);
      return;
    }

    const relative = pathname === '/' ? 'index.html' : pathname.slice(1);
    if (rejectAegis && relative === aegisRelative) {
      response.writeHead(404, {
        'cache-control': 'no-store',
        'content-length': 0,
      });
      response.end();
      return;
    }
    const path = resolve(dist, relative);
    if (path !== dist && !path.startsWith(`${dist}${sep}`)) {
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
        'content-type':
          MIME[extname(path)] ?? 'application/octet-stream',
      });
      createReadStream(path).pipe(response);
    } catch {
      response.writeHead(404);
      response.end('not found');
    }
  });
  await new Promise((accept, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', accept);
  });
  return { server };
}

function address(server) {
  const value = server.address();
  if (!value || typeof value === 'string')
    throw new Error('Review server did not expose a TCP address.');
  return `http://127.0.0.1:${value.port}/?standalone&audio=off`;
}

function close(server) {
  return new Promise((accept) => server.close(accept));
}

function validateSourceReplay(document) {
  const replayDocument = jsonObject(document, 'source replay');
  const header = jsonObject(replayDocument.header, 'source replay.header');
  if (header.replayVersion !== 3)
    throw new Error(
      `Review source must be replay-v3, got ${JSON.stringify(header.replayVersion)}.`,
    );

  const contract = jsonObject(
    header.contract,
    'source replay.header.contract',
  );
  const map = jsonObject(
    contract.map,
    'source replay.header.contract.map',
  );
  if (
    map.mapId !== 'frontline-labs-01-classes' ||
    map.width !== 23 ||
    map.height !== 15
  )
    throw new Error(
      `Review source must be the 23x15 Frontline Labs class map; got ${JSON.stringify(
        {
          mapId: map.mapId,
          width: map.width,
          height: map.height,
        },
      )}.`,
    );

  const rules = jsonObject(
    contract.rules,
    'source replay.header.contract.rules',
  );
  const gameMode = jsonObject(
    rules.gameMode,
    'source replay.header.contract.rules.gameMode',
  );
  const modeMapBinding = jsonObject(
    contract.modeMapBinding,
    'source replay.header.contract.modeMapBinding',
  );
  if (
    gameMode.kind !== 'frontline' ||
    gameMode.modeId !== 'frontline' ||
    modeMapBinding.kind !== 'frontline'
  )
    throw new Error(
      `Review source must use the Frontline mode and binding, got ${JSON.stringify(
        {
          gameModeKind: gameMode.kind,
          modeId: gameMode.modeId,
          modeMapBindingKind: modeMapBinding.kind,
        },
      )}.`,
    );

  if (
    replayDocument.partial !== false ||
    !/^[0-9a-f]{64}$/u.test(String(replayDocument.replayHash)) ||
    replayDocument.result === null ||
    replayDocument.result === undefined
  )
    throw new Error(
      'Review source must be a final canonical replay with partial=false, a SHA-256 replayHash and a non-null result.',
    );
}

async function prepareReviewReplay({
  sourceReplay: source,
  sourceReplayBytes: sourceBytes,
  sourceReplayHash: sourceHash,
  presentationOverlayPath: overlayPath,
}) {
  const sourcePresentation = source.header.presentation;
  if (sourcePresentation !== null) {
    if (overlayPath)
      throw new Error(
        'Source replay already has an explicit presentation; refusing to replace it with PRESENTATION_OVERLAY_PATH.',
      );
    validateReviewPresentation(
      sourcePresentation,
      'source replay.header.presentation',
    );
    return {
      document: source,
      bytes: sourceBytes,
      presentationReview: {
        mode: 'source-authored',
        banner: 'SOURCE-AUTHORED PRESENTATION · NOT AN APPROVAL',
        approvalClaim: null,
        sourcePresentationWasNull: false,
        themeId: sourcePresentation.themeId,
        themeSourceCommit: null,
        overlay: null,
        presentation: sourcePresentation,
      },
    };
  }

  if (!overlayPath)
    throw new Error(
      'Source replay header.presentation is null; refusing implicit Control Room fallback. Supply an explicitly labelled PRESENTATION_OVERLAY_PATH for a review-only document.',
    );

  const overlayBytes = await readFile(overlayPath);
  const overlay = JSON.parse(overlayBytes);
  validatePresentationOverlay(overlay, sourceHash);
  const review = structuredClone(source);
  review.header.presentation = structuredClone(overlay.presentation);
  review.partial = true;
  review.replayHash = null;
  review.result = null;
  const reviewBytes = Buffer.from(JSON.stringify(review));
  return {
    document: review,
    bytes: reviewBytes,
    presentationReview: {
      mode: 'presentation-only-overlay',
      banner:
        `PRESENTATION-ONLY OVERLAY · ${overlay.label} · NOT CANONICAL`,
      approvalClaim: null,
      sourcePresentationWasNull: true,
      themeId: overlay.presentation.themeId,
      themeSourceCommit: overlay.themeSourceCommit,
      overlay: {
        fileName: basename(overlayPath),
        byteLength: overlayBytes.length,
        sha256: sha256(overlayBytes),
        version: overlay.version,
        kind: overlay.kind,
        label: overlay.label,
        sourceReplaySha256: overlay.sourceReplaySha256,
        themeSourceCommit: overlay.themeSourceCommit,
        approvalClaim: overlay.approvalClaim,
      },
      presentation: overlay.presentation,
    },
  };
}

function validatePresentationOverlay(overlayValue, sourceReplayHash) {
  const overlay = exactJsonObject(
    overlayValue,
    'presentation overlay',
    [
      'version',
      'kind',
      'label',
      'sourceReplaySha256',
      'themeSourceCommit',
      'approvalClaim',
      'presentation',
    ],
  );
  if (overlay.version !== 1)
    throw new Error(
      `presentation overlay.version must be 1, got ${JSON.stringify(overlay.version)}.`,
    );
  if (overlay.kind !== 'nilbots-presentation-only-overlay')
    throw new Error(
      `presentation overlay.kind must be 'nilbots-presentation-only-overlay', got ${JSON.stringify(overlay.kind)}.`,
    );
  const label = nonEmptyString(overlay.label, 'presentation overlay.label');
  if (label.length > 80)
    throw new Error('presentation overlay.label must be at most 80 characters.');
  if (/\bapproved\b/iu.test(label))
    throw new Error(
      "presentation overlay.label must not claim that the candidate is 'approved'.",
    );
  if (
    overlay.sourceReplaySha256 !== sourceReplayHash ||
    !/^[0-9a-f]{64}$/u.test(String(overlay.sourceReplaySha256))
  )
    throw new Error(
      `presentation overlay.sourceReplaySha256 must match the source bytes (${sourceReplayHash}).`,
    );
  if (!/^[0-9a-f]{40}$/u.test(String(overlay.themeSourceCommit)))
    throw new Error(
      'presentation overlay.themeSourceCommit must be a full 40-character Git commit id.',
    );
  if (overlay.approvalClaim !== null)
    throw new Error(
      'presentation overlay.approvalClaim must be null; this harness does not mint visual approval.',
    );
  validateReviewPresentation(
    overlay.presentation,
    'presentation overlay.presentation',
  );
}

function validateReviewPresentation(presentationValue, path) {
  const presentation = exactJsonObject(presentationValue, path, [
    'themeId',
    'map',
    'forms',
  ]);
  const themeId = nonEmptyString(
    presentation.themeId,
    `${path}.themeId`,
  );
  if (themeId.toLowerCase() === 'control-room')
    throw new Error(
      `${path}.themeId cannot be 'control-room' for a Frontline V4-target review.`,
    );
  if (presentation.map === null)
    throw new Error(`${path}.map must be explicit and non-null.`);
  const map = exactJsonObject(presentation.map, `${path}.map`, [
    'boundaryWall',
    'interiorWall',
    'wallGroups',
  ]);
  nonEmptyString(map.boundaryWall, `${path}.map.boundaryWall`);
  nonEmptyString(map.interiorWall, `${path}.map.interiorWall`);
  if (!Array.isArray(map.wallGroups))
    throw new Error(`${path}.map.wallGroups must be an array.`);
  map.wallGroups.forEach((groupValue, groupIndex) => {
    const groupPath = `${path}.map.wallGroups[${groupIndex}]`;
    const group = exactJsonObject(groupValue, groupPath, [
      'family',
      'tiles',
    ]);
    nonEmptyString(group.family, `${groupPath}.family`);
    if (!Array.isArray(group.tiles))
      throw new Error(`${groupPath}.tiles must be an array.`);
    group.tiles.forEach((tileValue, tileIndex) => {
      const tilePath = `${groupPath}.tiles[${tileIndex}]`;
      const tile = exactJsonObject(tileValue, tilePath, ['x', 'y']);
      if (
        !Number.isInteger(tile.x) ||
        !Number.isInteger(tile.y) ||
        tile.x < 0 ||
        tile.x >= 23 ||
        tile.y < 0 ||
        tile.y >= 15
      )
        throw new Error(
          `${tilePath} must be an integer position within the 23x15 source map.`,
        );
    });
  });

  if (!Array.isArray(presentation.forms))
    throw new Error(`${path}.forms must be an array.`);
  const formIds = new Set();
  presentation.forms.forEach((formValue, index) => {
    const formPath = `${path}.forms[${index}]`;
    const form = exactJsonObject(formValue, formPath, [
      'formId',
      'lookId',
      'projectileLookId',
    ]);
    const formId = nonEmptyString(form.formId, `${formPath}.formId`);
    if (formIds.has(formId))
      throw new Error(`${formPath}.formId duplicates '${formId}'.`);
    formIds.add(formId);
    nullableNonEmptyString(form.lookId, `${formPath}.lookId`);
    nullableNonEmptyString(
      form.projectileLookId,
      `${formPath}.projectileLookId`,
    );
  });
}

function sourceReplayEvidence({ replay: document, bytes, path }) {
  return {
    fileName: basename(path),
    byteLength: bytes.length,
    sha256: sha256(bytes),
    replayHash: document.replayHash,
    partial: document.partial,
    resultPresent: document.result !== null,
    headerPresentation: document.header.presentation,
    gameRulesVersion: document.header.gameRulesVersion,
    matchContractFingerprint:
      document.header.contract.matchContractFingerprint,
    map: {
      mapId: document.header.contract.map.mapId,
      width: document.header.contract.map.width,
      height: document.header.contract.map.height,
      mapFingerprint: document.header.contract.map.mapFingerprint,
    },
    tickCount: document.ticks.length,
  };
}

function reviewDocumentEvidence({
  replay: document,
  bytes,
  presentationReview: review,
}) {
  return {
    byteLength: bytes.length,
    sha256: sha256(bytes),
    canonicalSourceBytes: review.mode === 'source-authored',
    partial: document.partial,
    replayHash: document.replayHash,
    result: document.result === null ? null : 'present-in-source',
    approvalClaim: null,
    presentationMode: review.mode,
    presentation: document.header.presentation,
  };
}

function exactJsonObject(value, path, keys) {
  const item = jsonObject(value, path);
  const allowed = new Set(keys);
  for (const key of Object.keys(item))
    if (!allowed.has(key))
      throw new Error(`${path}.${key} is not an allowed property.`);
  for (const key of keys)
    if (!Object.prototype.hasOwnProperty.call(item, key))
      throw new Error(`${path}.${key} is required.`);
  return item;
}

function jsonObject(value, path) {
  if (value === null || typeof value !== 'object' || Array.isArray(value))
    throw new Error(`${path} must be an object.`);
  return value;
}

function nonEmptyString(value, path) {
  if (typeof value !== 'string' || value.trim().length === 0)
    throw new Error(`${path} must be a non-empty string.`);
  return value;
}

function nullableNonEmptyString(value, path) {
  if (value !== null) nonEmptyString(value, path);
}

function movementEvidence(document, tick) {
  const open = document.ticks.find((entry) => entry.tick === tick);
  const close = document.ticks.find((entry) => entry.tick === tick + 1);
  if (!open || !close) return [];
  const primes = (entry) =>
    new Map(
      entry.postState.activeLives
        .filter(
          (actor) =>
            actor.actorId.unitId === 0 &&
            actor.formId === 'bulwark-prime',
        )
        .map((actor) => [actor.actorId.teamId, actor]),
    );
  const opening = primes(open);
  const closing = primes(close);
  return [...opening.entries()]
    .filter(([teamId]) => closing.has(teamId))
    .sort(([left], [right]) => left - right)
    .map(([teamId, actor]) => ({
      teamId,
      formId: actor.formId,
      facing: actor.facing,
      open: actor.position,
      close: closing.get(teamId).position,
    }));
}

function glbJson(bytes) {
  if (bytes.toString('utf8', 0, 4) !== 'glTF')
    throw new Error('Aegis model is not a binary glTF.');
  const jsonLength = bytes.readUInt32LE(12);
  const jsonType = bytes.readUInt32LE(16);
  if (jsonType !== 0x4e4f534a)
    throw new Error('Aegis GLB does not begin with a JSON chunk.');
  return JSON.parse(
    bytes
      .subarray(20, 20 + jsonLength)
      .toString('utf8')
      .trim(),
  );
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}
