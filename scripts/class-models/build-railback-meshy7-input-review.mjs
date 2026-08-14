#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const sharp = requireFromWeb('sharp');
const conceptDirectory = path.join(
  repository,
  'art/class-look-concepts/arc-relay/longshot-redesign-v2',
);
const inputDirectory = path.join(conceptDirectory, 'railback-meshy7-v1');

const views = [
  ['01-front.png', '01 · PRIMARY STRICT FRONT'],
  ['02-right.png', '02 · STRICT RIGHT SIDE'],
  ['03-back.png', '03 · STRICT REAR'],
  ['04-front-right-three-quarter.png', '04 · FRONT-RIGHT 3/4'],
];
const sourceRecords = [];
for (const [file, label] of [...views, ['canonical-top-east.png', 'CANONICAL TOP → EAST']]) {
  const bytes = await readFile(path.join(inputDirectory, file));
  const { data, info } = await sharp(bytes).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  const bounds = foregroundBounds(data, info);
  sourceRecords.push({
    file,
    label,
    bytes: bytes.length,
    sha256: createHash('sha256').update(bytes).digest('hex'),
    width: info.width,
    height: info.height,
    foregroundBounds: bounds,
    foregroundCoverage: Number((bounds.pixels / (info.width * info.height)).toFixed(4)),
  });
}

const width = 1600;
const headerHeight = 126;
const cardWidth = width / 2;
const cardHeight = 610;
const evidenceHeight = 500;
const height = headerHeight + cardHeight * 2 + evidenceHeight;
const composites = [{ input: headerSvg(width, headerHeight), left: 0, top: 0 }];

for (const [index, record] of sourceRecords.slice(0, 4).entries()) {
  const left = (index % 2) * cardWidth;
  const top = headerHeight + Math.floor(index / 2) * cardHeight;
  composites.push(
    { input: cardLabelSvg(cardWidth, 64, record), left, top },
    {
      input: await sharp(path.join(inputDirectory, record.file))
        .resize(cardWidth - 36, cardHeight - 82, { fit: 'contain', background: '#dad8d4' })
        .png()
        .toBuffer(),
      left: left + 18,
      top: top + 70,
    },
  );
}

const evidenceTop = headerHeight + cardHeight * 2;
composites.push(
  { input: evidenceBackground(width, evidenceHeight), left: 0, top: evidenceTop },
  {
    input: await sharp(path.join(inputDirectory, 'canonical-top-east.png'))
      .resize(480, 400, { fit: 'contain', background: '#dad8d4' })
      .png()
      .toBuffer(),
    left: 42,
    top: evidenceTop + 68,
  },
  {
    input: await silhouetteOverlay(),
    left: 574,
    top: evidenceTop + 68,
  },
  {
    input: await gameplayProof(),
    left: 1130,
    top: evidenceTop + 122,
  },
);

await sharp({ create: { width, height, channels: 4, background: '#080d12' } })
  .composite(composites)
  .png()
  .toFile(path.join(inputDirectory, 'input-review.png'));

const maximumDimensions = sourceRecords.slice(0, 4).map((record) =>
  Math.max(record.foregroundBounds.width, record.foregroundBounds.height),
);
const metrics = {
  schemaVersion: 1,
  generator: 'scripts/class-models/build-railback-meshy7-input-review.mjs',
  status: 'owner-approved-provider-inputs',
  primaryView: '01-front.png',
  viewOrder: views.map(([file]) => file),
  cameraContract: 'strict front, strict right side, strict rear, front-right three-quarter',
  maximumDimensionDrift: Number(
    ((Math.max(...maximumDimensions) / Math.min(...maximumDimensions) - 1) * 100).toFixed(2),
  ),
  sources: sourceRecords,
};
await writeFile(
  path.join(inputDirectory, 'input-review-metrics.json'),
  `${JSON.stringify(metrics, null, 2)}\n`,
);

console.log(
  `Built ${path.relative(repository, path.join(inputDirectory, 'input-review.png'))}; ` +
    `provider-view maximum-dimension drift ${metrics.maximumDimensionDrift}%.`,
);

function foregroundBounds(data, info) {
  let minX = info.width;
  let minY = info.height;
  let maxX = -1;
  let maxY = -1;
  let pixels = 0;
  for (let y = 0; y < info.height; y += 1) {
    for (let x = 0; x < info.width; x += 1) {
      const offset = (y * info.width + x) * info.channels;
      const red = data[offset];
      const green = data[offset + 1];
      const blue = data[offset + 2];
      const maximum = Math.max(red, green, blue);
      const minimum = Math.min(red, green, blue);
      const saturation = maximum === 0 ? 0 : (maximum - minimum) / maximum;
      const luminance = (red + green + blue) / 3;
      if (saturation <= 0.07 && luminance >= 200) continue;
      minX = Math.min(minX, x);
      minY = Math.min(minY, y);
      maxX = Math.max(maxX, x);
      maxY = Math.max(maxY, y);
      pixels += 1;
    }
  }
  if (maxX < minX || maxY < minY) throw new Error('No Railback foreground detected.');
  return {
    minX,
    minY,
    maxX,
    maxY,
    width: maxX - minX + 1,
    height: maxY - minY + 1,
    pixels,
  };
}

async function silhouetteOverlay() {
  const [approved, canonical] = await Promise.all([
    silhouette(path.join(conceptDirectory, 'railback-top.png'), '#ef4fb4'),
    silhouette(path.join(inputDirectory, 'canonical-top-east.png'), '#43d7f2'),
  ]);
  return sharp({ create: { width: 480, height: 400, channels: 4, background: '#111820' } })
    .composite([
      { input: approved, blend: 'screen' },
      { input: canonical, blend: 'screen' },
    ])
    .png()
    .toBuffer();
}

async function silhouette(file, color) {
  const { data, info } = await sharp(file).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  const bounds = foregroundBounds(data, info);
  const mask = Buffer.alloc(info.width * info.height);
  for (let y = 0; y < info.height; y += 1) {
    for (let x = 0; x < info.width; x += 1) {
      const offset = (y * info.width + x) * info.channels;
      const red = data[offset];
      const green = data[offset + 1];
      const blue = data[offset + 2];
      const maximum = Math.max(red, green, blue);
      const minimum = Math.min(red, green, blue);
      const saturation = maximum === 0 ? 0 : (maximum - minimum) / maximum;
      const luminance = (red + green + blue) / 3;
      mask[y * info.width + x] = saturation > 0.07 || luminance < 200 ? 144 : 0;
    }
  }
  return sharp(mask, { raw: { width: info.width, height: info.height, channels: 1 } })
    .extract({ left: bounds.minX, top: bounds.minY, width: bounds.width, height: bounds.height })
    .resize(420, 340, { fit: 'contain', kernel: 'nearest', background: '#000000' })
    .tint(color)
    .ensureAlpha()
    .extend({ top: 30, bottom: 30, left: 30, right: 30, background: '#00000000' })
    .png()
    .toBuffer();
}

async function gameplayProof() {
  const record = sourceRecords.find((entry) => entry.file === 'canonical-top-east.png');
  const source = sharp(path.join(inputDirectory, record.file));
  const object = await source
    .extract({
      left: record.foregroundBounds.minX,
      top: record.foregroundBounds.minY,
      width: record.foregroundBounds.width,
      height: record.foregroundBounds.height,
    })
    .resize(64, 64, { fit: 'contain', background: '#00000000' })
    .png()
    .toBuffer();
  return sharp({ create: { width: 310, height: 240, channels: 4, background: '#111820' } })
    .composite([
      { input: gridSvg(240, 240), left: 0, top: 0 },
      { input: object, left: 88, top: 88 },
    ])
    .png()
    .toBuffer();
}

function headerSvg(boardWidth, boardHeight) {
  return Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${boardWidth}" height="${boardHeight}">
    <rect width="100%" height="100%" fill="#080d12"/>
    <text x="28" y="46" fill="#edf3f6" font-family="Menlo,monospace" font-size="30" font-weight="700">RAILBACK · MESHY 7 INPUT GATE</text>
    <text x="28" y="84" fill="#a9bac4" font-family="Menlo,monospace" font-size="18">primary front first · separate square views · consistent scale · exactly three ribs · no rigging</text>
    <text x="28" y="111" fill="#d4a564" font-family="Menlo,monospace" font-size="15">review-only generation; live Longshot remains unchanged</text>
  </svg>`);
}

function cardLabelSvg(cardWidthValue, cardHeightValue, record) {
  const bounds = record.foregroundBounds;
  return Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${cardWidthValue}" height="${cardHeightValue}">
    <rect width="100%" height="100%" fill="#0d151d"/>
    <text x="18" y="28" fill="#edf3f6" font-family="Menlo,monospace" font-size="20" font-weight="700">${record.label}</text>
    <text x="18" y="51" fill="#9eb0ba" font-family="Menlo,monospace" font-size="13">1254² · foreground ${bounds.width}×${bounds.height} · ${record.sha256.slice(0, 12)}</text>
  </svg>`);
}

function evidenceBackground(boardWidth, boardHeight) {
  return Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${boardWidth}" height="${boardHeight}">
    <rect width="100%" height="100%" fill="#0b1117"/>
    <text x="42" y="38" fill="#edf3f6" font-family="Menlo,monospace" font-size="18" font-weight="700">CANONICAL STRICT TOP · FRONT EAST →</text>
    <text x="574" y="38" fill="#edf3f6" font-family="Menlo,monospace" font-size="18" font-weight="700">SAME-MAX-SPAN PLANFORM OVERLAY</text>
    <text x="574" y="61" fill="#9eb0ba" font-family="Menlo,monospace" font-size="13">pink = approved concept top · cyan = canonical identity edit</text>
    <text x="1130" y="78" fill="#edf3f6" font-family="Menlo,monospace" font-size="18" font-weight="700">≈48 PX BODY READ</text>
    <text x="1130" y="101" fill="#9eb0ba" font-family="Menlo,monospace" font-size="13">broad mass + three pale stages</text>
  </svg>`);
}

function gridSvg(gridWidth, gridHeight) {
  return Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${gridWidth}" height="${gridHeight}">
    <defs><pattern id="grid" width="48" height="48" patternUnits="userSpaceOnUse"><rect width="48" height="48" fill="#17191c"/><path d="M48 0H0V48" fill="none" stroke="#3d3029" stroke-width="1"/></pattern></defs>
    <rect width="100%" height="100%" fill="url(#grid)" stroke="#31424d"/>
  </svg>`);
}
