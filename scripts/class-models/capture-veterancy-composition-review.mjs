#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');
const baseUrl = process.argv[2] ?? 'http://127.0.0.1:9201';
const viewerUrl = new URL('/scripts/class-models/veterancy-composition-review.html', baseUrl);
const outputDirectory = path.join(
  repository,
  'art/reviews/arc-relay-veterancy-modules-meshy7-v1/gate-d',
);
const captureDirectory = path.join(outputDirectory, 'captures');
const tileSize = 440;
mkdirSync(captureDirectory, { recursive: true });

const modules = Object.fromEntries(
  [
    ['crown', 'veterancy-crown-spine-v2'],
    ['swept', 'veterancy-swept-blade'],
    ['square', 'veterancy-square-mantle'],
    ['radial', 'veterancy-radial-petal'],
    ['curved', 'veterancy-curved-rail'],
  ].map(([kind, moduleId]) => [kind, modelPath(moduleId)]),
);
const chassis = {
  kestrel:
    'art/class-models/runtime-tiers/arc-relay/meshy7-kestrel-review-v1/arc-kestrel-lean.glb',
  sunder:
    'art/class-models/runtime-tiers/arc-relay/meshy7-fleet-review-v3/arc-sunder-lean.glb',
  repulsor:
    'art/class-models/runtime-tiers/arc-relay/meshy7-fleet-review-v3/arc-repulsor-lean.glb',
  longshot:
    'art/class-models/runtime-tiers/arc-relay/meshy7-longshot-railback-muted-blue-review-v3/' +
    'arc-longshot-railback-muted-blue-lean.glb',
};
const configurations = {
  'kestrel-crown-feathers': {
    chassis: chassis.kestrel,
    profile: 'long',
    innerKind: 'swept',
    outerKind: 'radial',
  },
  'sunder-crown-feathers': {
    chassis: chassis.sunder,
    profile: 'standard',
    innerKind: 'swept',
    outerKind: 'radial',
  },
  'repulsor-full-feathers': {
    chassis: chassis.repulsor,
    profile: 'round',
    innerKind: 'radial',
    outerKind: 'radial',
  },
  'longshot-canonical': {
    chassis: chassis.longshot,
    profile: 'long',
    innerKind: 'swept',
    outerKind: 'swept',
  },
};

const browser = await chromium.launch({ headless: true });
const heroCaptures = [];
const rankCaptures = [];
try {
  heroCaptures.push(
    await captureComposition('kestrel-level5-gameplay', 'KESTREL · CROWN + FEATHERS', {
      configuration: 'kestrel-crown-feathers',
      rank: 5,
      view: 'gameplay',
    }),
    await captureComposition('kestrel-level5-top', 'KESTREL · STRICT TOP', {
      configuration: 'kestrel-crown-feathers',
      rank: 5,
      view: 'top-down',
    }),
    await captureComposition('sunder-level5-gameplay', 'SUNDER · CROWN + FEATHERS', {
      configuration: 'sunder-crown-feathers',
      rank: 5,
      view: 'gameplay',
    }),
    await captureComposition('sunder-level5-top', 'SUNDER · STRICT TOP', {
      configuration: 'sunder-crown-feathers',
      rank: 5,
      view: 'top-down',
    }),
    await captureComposition('repulsor-level5-gameplay', 'REPULSOR · FOUR PETALS', {
      configuration: 'repulsor-full-feathers',
      rank: 5,
      view: 'gameplay',
    }),
    await captureComposition('repulsor-level5-top', 'REPULSOR · STRICT TOP', {
      configuration: 'repulsor-full-feathers',
      rank: 5,
      view: 'top-down',
    }),
    await captureComposition('longshot-level5-gameplay-real-crown', 'LONGSHOT · REAL CROWN', {
      configuration: 'longshot-canonical',
      rank: 5,
      view: 'gameplay',
    }),
    await captureComposition('longshot-level5-top-real-crown', 'LONGSHOT · STRICT TOP', {
      configuration: 'longshot-canonical',
      rank: 5,
      view: 'top-down',
    }),
  );
  for (let rank = 1; rank <= 5; rank += 1)
    rankCaptures.push(
      await captureComposition(`kestrel-rank-${rank}`, `RANK ${rank}`, {
        configuration: 'kestrel-crown-feathers',
        rank,
        view: 'gameplay',
      }),
    );
} finally {
  await browser.close();
}

const heroSheet = await contactSheet({
  file: 'real-crown-and-feathers-level5.png',
  title: 'VETERANCY LEVEL 5 · REAL CROWN AND FEATHER MODULES',
  subtitle: 'Meshy 7 geometry only · Kestrel module-mix proof · Sunder canonical swept + radial · Repulsor radial + radial · Longshot swept + swept',
  captures: heroCaptures,
  columns: 3,
});
const rankStrip = await contactSheet({
  file: 'kestrel-real-rank-1-5-strip.png',
  title: 'KESTREL · REAL VETERANCY RANK 1–5 MODULE-MIX PROOF',
  subtitle: 'rank 1 crown · rank 2 inner blades · rank 4 outer feather/petals · rank 5 strongest spread',
  captures: rankCaptures,
  columns: 5,
});
const gameplayScale = await gameplayScaleSheet(heroCaptures.slice(0, 5));
const moduleSheet = await moduleFamilySheet();
const record = {
  schemaVersion: 1,
  capturedAt: new Date().toISOString(),
  renderer: 'Three.js WebGLRenderer through Playwright Chromium',
  runtimePromoted: false,
  modules,
  heroSheet,
  rankStrip,
  gameplayScale,
  moduleSheet,
  heroCaptures: heroCaptures.map(cleanCapture),
  rankCaptures: rankCaptures.map(cleanCapture),
};
writeFileSync(
  path.join(outputDirectory, 'composition-capture-record.json'),
  `${JSON.stringify(record, null, 2)}\n`,
);
console.log(`Captured ${heroSheet.file}, ${rankStrip.file}, and ${moduleSheet.file}.`);

function modelPath(moduleId) {
  const manifestFile = path.join(
    repository,
    'art/class-models/provider-runs/meshy/arc-veterancy-modules-v1/inputs',
    moduleId,
    'manifest.json',
  );
  const manifest = JSON.parse(readFileSync(manifestFile, 'utf8'));
  const taskId = manifest.providerRun?.taskId;
  if (!taskId) throw new Error(`${moduleId} has no completed provider task.`);
  return (
    `art/class-models/provider-runs/meshy/arc-veterancy-modules-v1/${moduleId}/` +
    `${taskId}/model-normalized-rich-review.glb`
  );
}

async function captureComposition(file, label, options) {
  const configuration = configurations[options.configuration];
  const url = new URL(viewerUrl);
  url.searchParams.set('chassis', `/${configuration.chassis}`);
  url.searchParams.set('crown', `/${modules.crown}`);
  url.searchParams.set('inner', `/${modules[configuration.innerKind]}`);
  url.searchParams.set('outer', `/${modules[configuration.outerKind]}`);
  url.searchParams.set('innerKind', configuration.innerKind);
  url.searchParams.set('outerKind', configuration.outerKind);
  url.searchParams.set('profile', configuration.profile);
  url.searchParams.set('rank', options.rank);
  url.searchParams.set('view', options.view);
  const context = await browser.newContext({
    viewport: { width: tileSize, height: tileSize },
    deviceScaleFactor: 1,
  });
  const page = await context.newPage();
  const pageErrors = [];
  const failedRequests = [];
  const modelResponses = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('requestfailed', (request) =>
    failedRequests.push(`${request.method()} ${request.url()} ${request.failure()?.errorText ?? ''}`),
  );
  page.on('response', (response) => {
    if (new URL(response.url()).pathname.endsWith('.glb'))
      modelResponses.push({ url: response.url(), status: response.status() });
  });
  await page.goto(url.toString(), { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => window.__veterancyCompositionReady === true, null, {
    timeout: 180_000,
  });
  const canvasKind = await page.locator('canvas').evaluate((canvas) =>
    canvas.getContext('2d') ? '2d' : 'webgl',
  );
  if (
    canvasKind !== 'webgl' ||
    modelResponses.length < 3 ||
    modelResponses.some((response) => response.status !== 200) ||
    pageErrors.length ||
    failedRequests.length
  )
    throw new Error(
      `${file}: render failed ${JSON.stringify({ canvasKind, modelResponses, pageErrors, failedRequests })}.`,
    );
  const output = path.join(captureDirectory, `${file}.png`);
  await page.locator('canvas').screenshot({ path: output, animations: 'disabled' });
  await context.close();
  const bytes = readFileSync(output);
  return {
    file: path.relative(repository, output),
    label,
    configuration: options.configuration,
    rank: options.rank,
    view: options.view,
    bytes: bytes.length,
    sha256: sha256(bytes),
  };
}

async function contactSheet({ file, title, subtitle, captures, columns }) {
  const labelHeight = 38;
  const titleHeight = 82;
  const gap = 8;
  const rows = Math.ceil(captures.length / columns);
  const cellWidth = tileSize + gap;
  const cellHeight = tileSize + labelHeight + gap;
  const width = cellWidth * columns - gap;
  const height = titleHeight + cellHeight * rows - gap;
  const composites = [
    {
      input: Buffer.from(
        `<svg width="${width}" height="${titleHeight}" xmlns="http://www.w3.org/2000/svg">` +
          '<rect width="100%" height="100%" fill="#080d12"/>' +
          `<text x="16" y="31" fill="#e8eef2" font-size="20" font-family="monospace" font-weight="700">${title}</text>` +
          `<text x="16" y="57" fill="#9fb0bb" font-size="13" font-family="monospace">${subtitle}</text>` +
          '</svg>',
      ),
      left: 0,
      top: 0,
    },
  ];
  for (const [index, captured] of captures.entries()) {
    const left = (index % columns) * cellWidth;
    const top = titleHeight + Math.floor(index / columns) * cellHeight;
    composites.push(
      {
        input: Buffer.from(
          `<svg width="${tileSize}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg">` +
            '<rect width="100%" height="100%" fill="#10151a"/>' +
            `<text x="10" y="25" fill="#dbe7ef" font-size="14" font-family="monospace" font-weight="700">${captured.label}</text>` +
            '</svg>',
        ),
        left,
        top,
      },
      { input: readFileSync(path.join(repository, captured.file)), left, top: top + labelHeight },
    );
  }
  const output = path.join(outputDirectory, file);
  await sharp({ create: { width, height, channels: 4, background: '#080d12' } })
    .composite(composites)
    .png({ compressionLevel: 9 })
    .toFile(output);
  const bytes = readFileSync(output);
  return { file: path.relative(repository, output), bytes: bytes.length, sha256: sha256(bytes) };
}

async function gameplayScaleSheet(captures) {
  const imageSize = 104;
  const cellWidth = 188;
  const width = 960;
  const height = 230;
  const composites = [
    {
      input: Buffer.from(
        `<svg width="${width}" height="${height}" xmlns="http://www.w3.org/2000/svg">` +
          '<rect width="100%" height="100%" fill="#080d12"/>' +
          '<text x="14" y="28" fill="#e8eef2" font-size="18" font-family="monospace" font-weight="700">LEVEL 5 GAMEPLAY-SCALE READABILITY</text>' +
          '<text x="14" y="50" fill="#9fb0bb" font-size="12" font-family="monospace">104px samples · bilinear reduction · no sharpening</text>' +
          captures.map((capture, index) =>
            `<text x="${10 + index * cellWidth}" y="214" fill="#dbe7ef" font-size="10" font-family="monospace" font-weight="700">${capture.label.split(' · ')[0]}</text>`,
          ).join('') +
          '</svg>',
      ),
      left: 0,
      top: 0,
    },
  ];
  for (const [index, capture] of captures.entries()) {
    const thumbnail = await sharp(path.join(repository, capture.file))
      .resize(imageSize, imageSize, { fit: 'cover', kernel: sharp.kernel.lanczos3 })
      .png()
      .toBuffer();
    composites.push({ input: thumbnail, left: 42 + index * cellWidth, top: 79 });
  }
  const output = path.join(outputDirectory, 'real-level5-gameplay-scale.png');
  await sharp({ create: { width, height, channels: 4, background: '#080d12' } })
    .composite(composites)
    .png({ compressionLevel: 9 })
    .toFile(output);
  const bytes = readFileSync(output);
  return { file: path.relative(repository, output), bytes: bytes.length, sha256: sha256(bytes) };
}

async function moduleFamilySheet() {
  const entries = [
    ['PETALLED CROWN SPINE', 'veterancy-crown-spine-v2'],
    ['SWEPT BLADE', 'veterancy-swept-blade'],
    ['RADIAL FEATHER / PETAL', 'veterancy-radial-petal'],
    ['SQUARE MANTLE', 'veterancy-square-mantle'],
    ['CURVED RAIL', 'veterancy-curved-rail'],
  ];
  const size = 320;
  const gap = 8;
  const titleHeight = 82;
  const labelHeight = 38;
  const width = entries.length * (size + gap) - gap;
  const height = titleHeight + labelHeight + size;
  const composites = [
    {
      input: Buffer.from(
        `<svg width="${width}" height="${titleHeight}" xmlns="http://www.w3.org/2000/svg">` +
          '<rect width="100%" height="100%" fill="#080d12"/>' +
          '<text x="16" y="31" fill="#e8eef2" font-size="20" font-family="monospace" font-weight="700">VETERANCY KIT · FIVE REAL MESHY 7 MODULES</text>' +
          '<text x="16" y="57" fill="#9fb0bb" font-size="13" font-family="monospace">one centered crown · four reusable side-piece families · normalized rich review</text>' +
          '</svg>',
      ),
      left: 0,
      top: 0,
    },
  ];
  for (const [index, [label, moduleId]] of entries.entries()) {
    const left = index * (size + gap);
    const source = path.join(
      repository,
      `art/reviews/arc-relay-veterancy-modules-meshy7-v1/gate-c/${moduleId}/captures/front-three-quarter.png`,
    );
    const image = await sharp(source).resize(size, size, { fit: 'cover' }).png().toBuffer();
    composites.push(
      {
        input: Buffer.from(
          `<svg width="${size}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg">` +
            '<rect width="100%" height="100%" fill="#10151a"/>' +
            `<text x="10" y="25" fill="#dbe7ef" font-size="13" font-family="monospace" font-weight="700">${label}</text>` +
            '</svg>',
        ),
        left,
        top: titleHeight,
      },
      { input: image, left, top: titleHeight + labelHeight },
    );
  }
  const output = path.join(outputDirectory, 'five-real-modules.png');
  await sharp({ create: { width, height, channels: 4, background: '#080d12' } })
    .composite(composites)
    .png({ compressionLevel: 9 })
    .toFile(output);
  const bytes = readFileSync(output);
  return { file: path.relative(repository, output), bytes: bytes.length, sha256: sha256(bytes) };
}

function cleanCapture(capture) {
  return { ...capture };
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
