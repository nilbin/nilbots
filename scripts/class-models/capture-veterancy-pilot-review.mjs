#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { readGlb, triangleCount } from './model-memory.mjs';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');
const baseUrl = process.argv[2] ?? 'http://127.0.0.1:9201';
const taskId = '01a0019e-4531-7eaa-9841-c5f968b38979';
const runDirectory =
  `art/class-models/provider-runs/meshy/arc-veterancy-modules-v1/` +
  `veterancy-swept-blade/${taskId}`;
const rawModel = `${runDirectory}/raw-model.glb`;
const model = `${runDirectory}/model-normalized-rich-review.glb`;
const outputDirectory = path.join(
  repository,
  'art/reviews/arc-relay-veterancy-modules-meshy7-v1/pilot',
);
const captureDirectory = path.join(outputDirectory, 'captures');
const turntableUrl = new URL('/scripts/class-models/model-turntable-review.html', baseUrl);
const fitViewerUrl = new URL('/scripts/class-models/veterancy-module-fit-review.html', baseUrl);
const kestrel =
  'art/class-models/runtime-tiers/arc-relay/meshy7-kestrel-review-v1/arc-kestrel-lean.glb';
const longshot =
  'art/class-models/runtime-tiers/arc-relay/meshy7-longshot-railback-muted-blue-review-v3/' +
  'arc-longshot-railback-muted-blue-lean.glb';
const tileSize = 440;
const views = [
  ['front-three-quarter', 'GAMEPLAY 3/4'],
  ['positive-x', '+X CAMERA'],
  ['negative-x', '-X CAMERA'],
  ['positive-z', '+Z CAMERA'],
  ['negative-z', '-Z CAMERA'],
  ['top-down', 'STRICT TOP'],
];

mkdirSync(captureDirectory, { recursive: true });
const browser = await chromium.launch({ headless: true });
const isolatedCaptures = [];
let fitCaptures;
try {
  for (const [view, label] of views)
    isolatedCaptures.push(await captureIsolated(view, label));
  fitCaptures = [
    await captureFit('mirrored-pair', 'MIRRORED PAIR', { mode: 'pair' }),
    await captureFit('kestrel-fit', 'KESTREL STATIC FIT', {
      mode: 'fit',
      profile: 'kestrel',
      chassis: kestrel,
    }),
    await captureFit('longshot-fit', 'LONGSHOT STATIC FIT', {
      mode: 'fit',
      profile: 'longshot',
      chassis: longshot,
    }),
  ];
} finally {
  await browser.close();
}

const sixViewSheet = await contactSheet({
  file: 'swept-blade-six-view.png',
  title: 'VETERANCY SWEPT BLADE · MESHY 7 PILOT',
  subtitle: 'normalized rich GLB · 0.36-tile X span · +Y up · static review only',
  captures: isolatedCaptures,
  columns: 3,
});
const fitSheet = await contactSheet({
  file: 'swept-blade-fit-board.png',
  title: 'SWEPT BLADE · PAIR AND CHASSIS FIT GATE',
  subtitle: 'mirrored instance test · current approved Kestrel and Longshot · no runtime integration',
  captures: fitCaptures,
  columns: 3,
});
const gameplayScaleSheet = await smallScaleSheet(fitCaptures);

const rawBytes = readFileSync(path.join(repository, rawModel));
const normalizedBytes = readFileSync(path.join(repository, model));
const raw = readGlb(rawBytes, rawModel);
const normalized = readGlb(normalizedBytes, model);
const normalization = JSON.parse(
  readFileSync(path.join(repository, runDirectory, 'normalization.json'), 'utf8'),
);
const facingCorrection = JSON.parse(
  readFileSync(path.join(repository, runDirectory, 'facing-correction.json'), 'utf8'),
);
const rawStructure = JSON.parse(
  readFileSync(path.join(outputDirectory, 'raw-structure-audit.json'), 'utf8'),
);
const textureSlots = imageSlots(raw.document);
const images = (raw.document.images ?? []).map((image, imageIndex) => {
  const view = raw.document.bufferViews?.[image.bufferView];
  if (!view) return { imageIndex, mimeType: image.mimeType ?? null, embedded: false };
  const bytes = raw.binary.subarray(
    view.byteOffset ?? 0,
    (view.byteOffset ?? 0) + view.byteLength,
  );
  return {
    imageIndex,
    name: image.name ?? null,
    mimeType: image.mimeType ?? null,
    slots: [...(textureSlots.get(imageIndex) ?? [])].sort(),
    bytes: bytes.length,
    ...imageDimensions(bytes, image.mimeType),
  };
});
const normalizedBounds = normalization.normalizedBounds;
const dimensions = [0, 1, 2].map(
  (axis) => normalizedBounds.max[axis] - normalizedBounds.min[axis],
);
const audit = {
  schemaVersion: 1,
  status: 'gate-b-static-review',
  capturedAt: new Date().toISOString(),
  taskId,
  provider: 'Meshy',
  providerModel: 'meshy-7',
  credits: 30,
  renderer: 'Three.js WebGLRenderer through Playwright Chromium',
  sourceViewers: [
    'scripts/class-models/model-turntable-review.html',
    'scripts/class-models/veterancy-module-fit-review.html',
  ],
  raw: {
    file: rawModel,
    bytes: rawBytes.length,
    sha256: sha256(rawBytes),
    vertices: vertexCount(raw.document),
    triangles: triangleCount(raw.document),
    materials: raw.document.materials?.length ?? 0,
    textures: raw.document.textures?.length ?? 0,
    images,
    gltf: rawStructure.gltf,
    spatialConnectedComponents: rawStructure.spatialAudit.connectedComponents,
  },
  normalizedRich: {
    file: model,
    bytes: normalizedBytes.length,
    sha256: sha256(normalizedBytes),
    vertices: vertexCount(normalized.document),
    triangles: triangleCount(normalized.document),
    materials: normalized.document.materials?.length ?? 0,
    textures: normalized.document.textures?.length ?? 0,
    bounds: normalizedBounds,
    dimensions: { x: dimensions[0], y: dimensions[1], z: dimensions[2] },
    orientation: normalization.orientation,
    providerFacing: '-X',
    targetFacing: '+X',
    facingYawDegrees: facingCorrection.yawDegrees,
    facingEvidence:
      'The broad luminous capped end is the rear; the narrow clipped armor end points +X/front.',
    up: '+Y',
    floorY: 0,
  },
  startingEnvelope: { x: 0.36, y: 0.12, z: 0.11 },
  envelopeDeltas: {
    x: dimensions[0] / 0.36,
    y: dimensions[1] / 0.12,
    z: dimensions[2] / 0.11,
  },
  evidence: {
    sixViewSheet,
    fitSheet,
    gameplayScaleSheet,
    isolatedCaptures,
    fitCaptures,
  },
  contract: {
    purpose: 'Static art-side Gate B evidence only; no runtime promotion',
    remainingMeshyGenerationsAuthorized: 0,
    autoRigging: false,
  },
};
writeFileSync(
  path.join(outputDirectory, 'pilot-audit.json'),
  `${JSON.stringify(audit, null, 2)}\n`,
);
console.log(`Captured ${sixViewSheet.file} and ${fitSheet.file}.`);

async function captureIsolated(view, label) {
  const url = new URL(turntableUrl);
  url.searchParams.set('model', `/${model}`);
  url.searchParams.set('view', view);
  return captureCanvas({ url, ready: '__modelTurntableReady', file: `isolated-${view}.png`, view, label });
}

async function captureFit(view, label, options) {
  const url = new URL(fitViewerUrl);
  url.searchParams.set('module', `/${model}`);
  url.searchParams.set('mode', options.mode);
  url.searchParams.set('view', 'gameplay');
  if (options.profile) url.searchParams.set('profile', options.profile);
  if (options.chassis) url.searchParams.set('chassis', `/${options.chassis}`);
  return captureCanvas({ url, ready: '__veterancyFitReady', file: `${view}.png`, view, label });
}

async function captureCanvas({ url, ready, file, view, label }) {
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
  await page.waitForFunction((key) => window[key] === true, ready, { timeout: 120_000 });
  const canvasKind = await page.locator('canvas').evaluate((canvas) =>
    canvas.getContext('2d') ? '2d' : 'webgl',
  );
  if (
    canvasKind !== 'webgl' ||
    modelResponses.length < 1 ||
    modelResponses.some((response) => response.status !== 200) ||
    pageErrors.length ||
    failedRequests.length
  )
    throw new Error(
      `${view}: render failed ${JSON.stringify({ canvasKind, modelResponses, pageErrors, failedRequests })}.`,
    );
  const output = path.join(captureDirectory, file);
  await page.locator('canvas').screenshot({ path: output, animations: 'disabled' });
  await context.close();
  const bytes = readFileSync(output);
  return {
    view,
    label,
    file: path.relative(repository, output),
    sha256: sha256(bytes),
  };
}

async function contactSheet({ file, title, subtitle, captures, columns }) {
  const labelHeight = 40;
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
    const column = index % columns;
    const row = Math.floor(index / columns);
    const left = column * cellWidth;
    const top = titleHeight + row * cellHeight;
    composites.push(
      {
        input: Buffer.from(
          `<svg width="${tileSize}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg">` +
            '<rect width="100%" height="100%" fill="#10151a"/>' +
            `<text x="10" y="26" fill="#dbe7ef" font-size="14" font-family="monospace" font-weight="700">${captured.label}</text>` +
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

async function smallScaleSheet(captures) {
  const width = 680;
  const height = 230;
  const imageSize = 96;
  const cellWidth = 220;
  const composites = [
    {
      input: Buffer.from(
        `<svg width="${width}" height="${height}" xmlns="http://www.w3.org/2000/svg">` +
          '<rect width="100%" height="100%" fill="#080d12"/>' +
          '<text x="14" y="28" fill="#e8eef2" font-size="18" font-family="monospace" font-weight="700">GAMEPLAY-SCALE READABILITY CHECK</text>' +
          '<text x="14" y="50" fill="#9fb0bb" font-size="12" font-family="monospace">96px source samples · bilinear reduction · no sharpening</text>' +
          captures.map((capture, index) =>
            `<text x="${14 + index * cellWidth}" y="213" fill="#dbe7ef" font-size="12" font-family="monospace" font-weight="700">${capture.label}</text>`,
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
    composites.push({ input: thumbnail, left: 62 + index * cellWidth, top: 80 });
  }
  const output = path.join(outputDirectory, 'swept-blade-gameplay-scale.png');
  await sharp({ create: { width, height, channels: 4, background: '#080d12' } })
    .composite(composites)
    .png({ compressionLevel: 9 })
    .toFile(output);
  const bytes = readFileSync(output);
  return { file: path.relative(repository, output), bytes: bytes.length, sha256: sha256(bytes) };
}

function vertexCount(document) {
  return (document.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []).reduce(
    (sum, primitive) => sum + (document.accessors?.[primitive.attributes?.POSITION]?.count ?? 0),
    0,
  );
}

function imageSlots(document) {
  const slots = new Map();
  const add = (textureInfo, slot) => {
    if (textureInfo?.index === undefined) return;
    const texture = document.textures?.[textureInfo.index];
    const imageIndex = texture?.source;
    if (imageIndex === undefined) return;
    if (!slots.has(imageIndex)) slots.set(imageIndex, new Set());
    slots.get(imageIndex).add(slot);
  };
  for (const material of document.materials ?? []) {
    add(material.pbrMetallicRoughness?.baseColorTexture, 'baseColorTexture');
    add(material.pbrMetallicRoughness?.metallicRoughnessTexture, 'metallicRoughnessTexture');
    add(material.normalTexture, 'normalTexture');
    add(material.occlusionTexture, 'occlusionTexture');
    add(material.emissiveTexture, 'emissiveTexture');
  }
  return slots;
}

function imageDimensions(bytes, mimeType) {
  if (mimeType === 'image/png' && bytes.length >= 24)
    return { width: bytes.readUInt32BE(16), height: bytes.readUInt32BE(20) };
  if (mimeType === 'image/jpeg') {
    let offset = 2;
    while (offset + 9 < bytes.length) {
      if (bytes[offset] !== 0xff) break;
      const marker = bytes[offset + 1];
      const length = bytes.readUInt16BE(offset + 2);
      if ([0xc0, 0xc1, 0xc2].includes(marker))
        return { width: bytes.readUInt16BE(offset + 7), height: bytes.readUInt16BE(offset + 5) };
      offset += 2 + length;
    }
  }
  return { width: null, height: null };
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
