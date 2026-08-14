#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web/package.json'));
const sharp = requireFromWeb('sharp');
const conceptPath = path.join(
  repository,
  'art/class-look-concepts/arc-relay/longshot-redesign-v2/railback-meshy7-v1/canonical-top-east.png',
);
const modelPath = path.join(
  repository,
  'art/reviews/arc-relay-longshot-railback-v1/provider-review/lean/top-down.png',
);
const outputDirectory = path.join(
  repository,
  'art/reviews/arc-relay-longshot-railback-v1/provider-review',
);
const boardPath = path.join(outputDirectory, 'railback-fidelity-board.png');
const metricsPath = path.join(outputDirectory, 'silhouette-metrics.json');
const check = process.argv.includes('--check');

const concept = await silhouette(conceptPath, 'concept');
const model = await silhouette(modelPath, 'model');
const normalizedSize = 512;
const normalizedSpan = 420;
const conceptMask = normalizeMask(concept, normalizedSize, normalizedSpan);
const modelMask = normalizeMask(model, normalizedSize, normalizedSpan);
const comparison = compare(conceptMask, modelMask, normalizedSize, normalizedSpan);
const metrics = {
  schemaVersion: 1,
  purpose: 'Same-scale east-facing top-silhouette comparison for Railback owner review',
  concept: {
    file: path.relative(repository, conceptPath),
    sha256: sha256(readFileSync(conceptPath)),
    sourcePixels: { width: concept.width, height: concept.height },
    foregroundBounds: concept.bounds,
  },
  model: {
    file: path.relative(repository, modelPath),
    sha256: sha256(readFileSync(modelPath)),
    sourcePixels: { width: model.width, height: model.height },
    foregroundBounds: model.bounds,
  },
  normalization: {
    facing: '+x / east / screen-right',
    canvasPixels: normalizedSize,
    maximumSpanPixels: normalizedSpan,
    centeredIndependently: true,
  },
  comparison,
  interpretation: {
    coverage: 'intersection divided by canonical silhouette area',
    centerlineDrift: 'centroid separation as percent of normalized maximum span',
    localWidthDrift: 'mean absolute transverse-width difference across occupied longitudinal columns',
  },
};
const metricsBytes = Buffer.from(`${JSON.stringify(metrics, null, 2)}\n`);
const boardBytes = await buildBoard(concept, model, conceptMask, modelMask, metrics);

if (check) {
  if (!existsSync(boardPath) || !readFileSync(boardPath).equals(boardBytes))
    throw new Error('Railback fidelity board is stale; rerun without --check.');
  if (!existsSync(metricsPath) || !readFileSync(metricsPath).equals(metricsBytes))
    throw new Error('Railback silhouette metrics are stale; rerun without --check.');
  console.log('Railback fidelity board and silhouette metrics are current.');
} else {
  mkdirSync(outputDirectory, { recursive: true });
  writeFileSync(boardPath, boardBytes);
  writeFileSync(metricsPath, metricsBytes);
  console.log(
    `Built Railback fidelity board: ${comparison.coveragePercent}% coverage, ` +
      `${comparison.centerlineDriftPercent}% centerline drift, ` +
      `${comparison.meanLocalWidthDriftPercent}% mean local-width drift.`,
  );
}

async function silhouette(file, kind) {
  const { data, info } = await sharp(file).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  const mask = new Uint8Array(info.width * info.height);
  let minX = info.width;
  let minY = info.height;
  let maxX = -1;
  let maxY = -1;
  for (let y = 0; y < info.height; y += 1) {
    for (let x = 0; x < info.width; x += 1) {
      const offset = (y * info.width + x) * info.channels;
      const r = data[offset];
      const g = data[offset + 1];
      const b = data[offset + 2];
      const luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
      const chroma = Math.max(r, g, b) - Math.min(r, g, b);
      const foreground = kind === 'concept'
        ? luminance < 202 || chroma > 22
        : Math.hypot(r - 23, g - 25, b - 28) > 24 && luminance > 34;
      if (!foreground) continue;
      mask[y * info.width + x] = 1;
      minX = Math.min(minX, x);
      minY = Math.min(minY, y);
      maxX = Math.max(maxX, x);
      maxY = Math.max(maxY, y);
    }
  }
  if (maxX < minX || maxY < minY) throw new Error(`${kind}: no foreground silhouette found.`);
  return {
    file,
    width: info.width,
    height: info.height,
    mask,
    bounds: { x: minX, y: minY, width: maxX - minX + 1, height: maxY - minY + 1 },
  };
}

function normalizeMask(source, size, targetSpan) {
  const { bounds } = source;
  const scale = targetSpan / Math.max(bounds.width, bounds.height);
  const width = Math.max(1, Math.round(bounds.width * scale));
  const height = Math.max(1, Math.round(bounds.height * scale));
  const left = Math.floor((size - width) / 2);
  const top = Math.floor((size - height) / 2);
  const output = new Uint8Array(size * size);
  for (let y = 0; y < height; y += 1) {
    const sourceY = bounds.y + Math.min(bounds.height - 1, Math.floor(y / scale));
    for (let x = 0; x < width; x += 1) {
      const sourceX = bounds.x + Math.min(bounds.width - 1, Math.floor(x / scale));
      output[(top + y) * size + left + x] = source.mask[sourceY * source.width + sourceX];
    }
  }
  return output;
}

function compare(conceptMask, modelMask, size, targetSpan) {
  let conceptArea = 0;
  let modelArea = 0;
  let intersection = 0;
  let union = 0;
  let conceptX = 0;
  let conceptY = 0;
  let modelX = 0;
  let modelY = 0;
  for (let index = 0; index < conceptMask.length; index += 1) {
    const concept = conceptMask[index] === 1;
    const model = modelMask[index] === 1;
    const x = index % size;
    const y = Math.floor(index / size);
    if (concept) {
      conceptArea += 1;
      conceptX += x;
      conceptY += y;
    }
    if (model) {
      modelArea += 1;
      modelX += x;
      modelY += y;
    }
    if (concept && model) intersection += 1;
    if (concept || model) union += 1;
  }
  const conceptCentroid = { x: conceptX / conceptArea, y: conceptY / conceptArea };
  const modelCentroid = { x: modelX / modelArea, y: modelY / modelArea };
  const centerlineDrift = Math.hypot(
    conceptCentroid.x - modelCentroid.x,
    conceptCentroid.y - modelCentroid.y,
  );
  const widthDrifts = [];
  for (let x = 0; x < size; x += 1) {
    const conceptWidth = columnWidth(conceptMask, size, x);
    const modelWidth = columnWidth(modelMask, size, x);
    if (conceptWidth === 0 && modelWidth === 0) continue;
    widthDrifts.push(Math.abs(conceptWidth - modelWidth) / targetSpan * 100);
  }
  return {
    coveragePercent: round(intersection / conceptArea * 100),
    intersectionOverUnionPercent: round(intersection / union * 100),
    modelToConceptAreaPercent: round(modelArea / conceptArea * 100),
    centerlineDriftPixels: round(centerlineDrift),
    centerlineDriftPercent: round(centerlineDrift / targetSpan * 100),
    meanLocalWidthDriftPercent: round(
      widthDrifts.reduce((total, value) => total + value, 0) / widthDrifts.length,
    ),
    maximumLocalWidthDriftPercent: round(Math.max(...widthDrifts)),
  };
}

function columnWidth(mask, size, x) {
  let min = size;
  let max = -1;
  for (let y = 0; y < size; y += 1) {
    if (!mask[y * size + x]) continue;
    min = Math.min(min, y);
    max = Math.max(max, y);
  }
  return max < min ? 0 : max - min + 1;
}

async function buildBoard(concept, model, conceptMask, modelMask, metrics) {
  const boardWidth = 1880;
  const boardHeight = 590;
  const panelSize = 420;
  const top = 132;
  const positions = [40, 500, 960, 1420];
  const conceptPanel = await imagePanel(concept.file, concept.bounds, panelSize);
  const modelPanel = await imagePanel(model.file, model.bounds, panelSize);
  const overlayPanel = await overlayImage(conceptMask, modelMask, panelSize);
  const thumbnailPanel = await thumbnailImage(model.file, model.bounds, panelSize);
  const header = Buffer.from(
    `<svg width="${boardWidth}" height="${boardHeight}" xmlns="http://www.w3.org/2000/svg">` +
      '<rect width="100%" height="100%" fill="#080d12"/>' +
      '<text x="40" y="45" fill="#eff6fa" font-size="28" font-family="Arial, sans-serif" font-weight="700">RAILBACK · CANONICAL FIDELITY GATE</text>' +
      `<text x="40" y="78" fill="#9db0bb" font-size="17" font-family="monospace">EAST FACES RIGHT · SAME MAXIMUM SPAN · ` +
      `COVERAGE ${metrics.comparison.coveragePercent}% · CENTER ${metrics.comparison.centerlineDriftPercent}% · WIDTH ${metrics.comparison.meanLocalWidthDriftPercent}%</text>` +
      label(positions[0], 112, 'CANONICAL TOP') +
      label(positions[1], 112, 'MESHY LEAN TOP') +
      label(positions[2], 112, 'SILHOUETTE OVERLAY') +
      label(positions[3], 112, 'TRUE 48 PX CHECK · 8×') +
      '</svg>',
  );
  return sharp(header)
    .composite([
      { input: conceptPanel, left: positions[0], top },
      { input: modelPanel, left: positions[1], top },
      { input: overlayPanel, left: positions[2], top },
      { input: thumbnailPanel, left: positions[3], top },
    ])
    .png({ compressionLevel: 9 })
    .toBuffer();
}

async function imagePanel(file, bounds, size) {
  return sharp(file)
    .extract({ left: bounds.x, top: bounds.y, width: bounds.width, height: bounds.height })
    .resize(size, size, { fit: 'contain', background: '#11171c' })
    .png()
    .toBuffer();
}

async function overlayImage(conceptMask, modelMask, size) {
  const sourceSize = Math.sqrt(conceptMask.length);
  const raw = Buffer.alloc(conceptMask.length * 4);
  for (let index = 0; index < conceptMask.length; index += 1) {
    const offset = index * 4;
    const concept = conceptMask[index] === 1;
    const model = modelMask[index] === 1;
    const color = concept && model
      ? [214, 232, 225]
      : concept
        ? [34, 211, 238]
        : model
          ? [249, 115, 22]
          : [17, 23, 28];
    raw[offset] = color[0];
    raw[offset + 1] = color[1];
    raw[offset + 2] = color[2];
    raw[offset + 3] = 255;
  }
  return sharp(raw, { raw: { width: sourceSize, height: sourceSize, channels: 4 } })
    .resize(size, size, { kernel: 'nearest' })
    .png()
    .toBuffer();
}

async function thumbnailImage(file, bounds, size) {
  const thumbnail = await sharp(file)
    .extract({ left: bounds.x, top: bounds.y, width: bounds.width, height: bounds.height })
    .resize(48, 48, { fit: 'contain', background: '#11171c' })
    .png()
    .toBuffer();
  const enlarged = await sharp(thumbnail)
    .resize(384, 384, { kernel: 'nearest' })
    .png()
    .toBuffer();
  return sharp({ create: { width: size, height: size, channels: 4, background: '#11171c' } })
    .composite([
      { input: enlarged, left: 0, top: 0 },
      { input: thumbnail, left: size - 48, top: size - 48 },
    ])
    .png()
    .toBuffer();
}

function label(x, y, value) {
  return `<text x="${x}" y="${y}" fill="#dbe7ef" font-size="16" font-family="monospace">${value}</text>`;
}

function round(value) {
  return Math.round(value * 100) / 100;
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
