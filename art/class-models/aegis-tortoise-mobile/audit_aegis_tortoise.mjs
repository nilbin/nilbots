#!/usr/bin/env node

import { createRequire } from 'node:module';
import { createHash } from 'node:crypto';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, '../../..');
const requireFromWeb = createRequire(
  pathToFileURL(path.join(repoRoot, 'web/package.json')),
);
const sharp = requireFromWeb('sharp');

const args = process.argv.slice(2);
const buildFlag = args.indexOf('--build-dir');
const outputFlag = args.indexOf('--output-dir');
if (buildFlag < 0 || outputFlag < 0)
  throw new Error('usage: audit_aegis_tortoise.mjs --build-dir <path> --output-dir <path>');
const buildDir = path.resolve(args[buildFlag + 1]);
const outputDir = path.resolve(args[outputFlag + 1]);
await mkdir(outputDir, { recursive: true });

const authority = path.join(
  repoRoot,
  'art/class-look-concepts/bulwark/aegis-tortoise/concept.png',
);
const approvedSide = path.join(
  scriptDir,
  'references/approved-clean-side.png',
);
const richTop = path.join(buildDir, 'review/rich/top.png');
const richSide = path.join(buildDir, 'review/rich/side.png');
const buildReport = JSON.parse(
  await readFile(path.join(buildDir, 'build-report.json'), 'utf8'),
);

const referenceTopMask = await normalizedMask(authority, 'alpha');
const candidateTopMask = await normalizedMask(richTop, 'alpha');
const top = compareMasks(referenceTopMask, candidateTopMask);
await saveOverlay(
  path.join(outputDir, 'top-silhouette-overlay.png'),
  top.reference,
  top.candidate,
);

const referenceSideMask = await normalizedMask(approvedSide, 'flat-gray');
const candidateSideMask = await normalizedMask(richSide, 'corner-bg');
const side = compareMasks(referenceSideMask, candidateSideMask);
await saveOverlay(
  path.join(outputDir, 'side-silhouette-overlay.png'),
  side.reference,
  side.candidate,
);

await contactBoard(
  path.join(outputDir, 'fixed-authority-comparison.png'),
  [
    [authority, 'Approved rich 2D authority'],
    [richTop, 'Rebuilt rich strict top'],
    [approvedSide, 'Approved inferred side'],
    [richSide, 'Rebuilt rich strict side'],
  ],
);
await contactBoard(
  path.join(outputDir, 'team-and-tier-comparison.png'),
  [
    [path.join(buildDir, 'review/rich/gameplay-blue.png'), 'Rich · blue team'],
    [path.join(buildDir, 'review/rich/gameplay-red.png'), 'Rich · red team'],
    [path.join(buildDir, 'review/lean/gameplay-blue.png'), 'Lean · blue team'],
    [path.join(buildDir, 'review/lean/gameplay-red.png'), 'Lean · red team'],
  ],
);
await contactBoard(
  path.join(outputDir, 'walk-cycle-side.png'),
  [1, 7, 13, 19].map((frame) => [
    path.join(
      buildDir,
      `review/rich/walk-side-${String(frame).padStart(2, '0')}.png`,
    ),
    `Aegis_Walk frame ${frame}`,
  ]),
);
await contactBoard(
  path.join(outputDir, 'walk-cycle-gameplay.png'),
  [1, 7, 13, 19].map((frame) => [
    path.join(
      buildDir,
      `review/rich/walk-frame-${String(frame).padStart(2, '0')}.png`,
    ),
    `Gameplay frame ${frame}`,
  ]),
);

const rich = buildReport.tiers.find((tier) => tier.tier === 'rich');
const lean = buildReport.tiers.find((tier) => tier.tier === 'lean');
const targetSideAspect = 3.345;
const sideAspectDrift = Math.abs(
  rich.geometry.sideAspect / targetSideAspect - 1,
);
const report = {
  schemaVersion: 1,
  asset: 'aegis-tortoise-mobile',
  method: {
    direction: 'fixed East/+X',
    allowedTransform: 'uniform scale and rigid translation only',
    rotation: false,
    reflection: false,
    nonUniformWarp: false,
    canvas: 512,
    normalizedMaximumSpan: 420,
  },
  top: {
    authority: path.relative(repoRoot, authority),
    candidate: path.relative(buildDir, richTop),
    iou: top.iou,
    outerIou: top.outerIou,
    centroidResidualPixels: top.centroidResidualPixels,
    gate: top.iou >= 0.94 && top.outerIou >= 0.95 ? 'pass' : 'fail',
  },
  side: {
    authority: path.relative(repoRoot, approvedSide),
    candidate: path.relative(buildDir, richSide),
    iou: side.iou,
    outerIou: side.outerIou,
    centroidResidualPixels: side.centroidResidualPixels,
    targetAspect: targetSideAspect,
    candidateAspect: rich.geometry.sideAspect,
    aspectDrift: sideAspectDrift,
    gate:
      side.outerIou >= 0.78 && sideAspectDrift <= 0.04
        ? 'pass'
        : 'fail',
  },
  budgets: {
    rich: {
      bytes: rich.glb.bytes,
      sha256: rich.glb.sha256,
      triangles: rich.geometry.triangles,
      vertices: rich.geometry.vertices,
      textures: rich.textures,
    },
    lean: {
      bytes: lean.glb.bytes,
      sha256: lean.glb.sha256,
      triangles: lean.geometry.triangles,
      vertices: lean.geometry.vertices,
      textures: lean.textures,
    },
  },
  evidence: {
    fixedAuthorityComparison: 'fixed-authority-comparison.png',
    topOverlay: 'top-silhouette-overlay.png',
    sideOverlay: 'side-silhouette-overlay.png',
    teamAndTier: 'team-and-tier-comparison.png',
    walkCycleSide: 'walk-cycle-side.png',
    walkCycleGameplay: 'walk-cycle-gameplay.png',
  },
};
await writeFile(
  path.join(outputDir, 'audit-report.json'),
  `${JSON.stringify(report, null, 2)}\n`,
);
process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);

async function normalizedMask(file, source) {
  const { data, info } = await sharp(file)
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const mask = new Uint8Array(info.width * info.height);
  const corner = [data[0], data[1], data[2]];
  for (let index = 0; index < mask.length; index += 1) {
    const offset = index * 4;
    if (source === 'alpha') {
      mask[index] = data[offset + 3] >= 16 ? 1 : 0;
    } else if (source === 'flat-gray') {
      const distance = Math.max(
        Math.abs(data[offset] - 184),
        Math.abs(data[offset + 1] - 184),
        Math.abs(data[offset + 2] - 184),
      );
      mask[index] = distance >= 16 ? 1 : 0;
    } else {
      const distance = Math.max(
        Math.abs(data[offset] - corner[0]),
        Math.abs(data[offset + 1] - corner[1]),
        Math.abs(data[offset + 2] - corner[2]),
      );
      mask[index] = distance >= 12 ? 1 : 0;
    }
  }
  const bounds = maskBounds(mask, info.width, info.height);
  const cropped = Buffer.alloc(bounds.width * bounds.height);
  for (let y = 0; y < bounds.height; y += 1)
    for (let x = 0; x < bounds.width; x += 1)
      cropped[y * bounds.width + x] =
        mask[(y + bounds.top) * info.width + x + bounds.left] * 255;
  const scale = 420 / Math.max(bounds.width, bounds.height);
  const width = Math.max(1, Math.round(bounds.width * scale));
  const height = Math.max(1, Math.round(bounds.height * scale));
  const { data: resized, info: resizedInfo } = await sharp(cropped, {
    raw: { width: bounds.width, height: bounds.height, channels: 1 },
  })
    .resize(width, height, { kernel: 'nearest' })
    .raw()
    .toBuffer({ resolveWithObject: true });
  const result = new Uint8Array(512 * 512);
  const left = Math.floor((512 - width) / 2);
  const top = Math.floor((512 - height) / 2);
  for (let y = 0; y < height; y += 1)
    for (let x = 0; x < width; x += 1)
      result[(y + top) * 512 + x + left] =
        resized[(y * width + x) * resizedInfo.channels] >= 128 ? 1 : 0;
  return result;
}

function compareMasks(referenceInput, candidateInput) {
  const referenceCentroid = centroid(referenceInput);
  const candidateCentroid = centroid(candidateInput);
  const dx = Math.round(referenceCentroid.x - candidateCentroid.x);
  const dy = Math.round(referenceCentroid.y - candidateCentroid.y);
  const candidate = translate(candidateInput, dx, dy);
  const reference = referenceInput;
  const candidateCentroidAligned = centroid(candidate);
  return {
    reference,
    candidate,
    iou: iou(reference, candidate),
    outerIou: iou(fillHoles(reference), fillHoles(candidate)),
    centroidResidualPixels: Math.hypot(
      referenceCentroid.x - candidateCentroidAligned.x,
      referenceCentroid.y - candidateCentroidAligned.y,
    ),
  };
}

function maskBounds(mask, width, height) {
  let left = width;
  let right = -1;
  let top = height;
  let bottom = -1;
  for (let y = 0; y < height; y += 1)
    for (let x = 0; x < width; x += 1)
      if (mask[y * width + x]) {
        left = Math.min(left, x);
        right = Math.max(right, x);
        top = Math.min(top, y);
        bottom = Math.max(bottom, y);
      }
  if (right < left) throw new Error('empty silhouette mask');
  return {
    left,
    top,
    width: right - left + 1,
    height: bottom - top + 1,
  };
}

function centroid(mask) {
  let xTotal = 0;
  let yTotal = 0;
  let count = 0;
  for (let y = 0; y < 512; y += 1)
    for (let x = 0; x < 512; x += 1)
      if (mask[y * 512 + x]) {
        xTotal += x;
        yTotal += y;
        count += 1;
      }
  return { x: xTotal / count, y: yTotal / count };
}

function translate(mask, dx, dy) {
  const result = new Uint8Array(mask.length);
  for (let y = 0; y < 512; y += 1)
    for (let x = 0; x < 512; x += 1) {
      const sourceX = x - dx;
      const sourceY = y - dy;
      if (
        sourceX >= 0 &&
        sourceX < 512 &&
        sourceY >= 0 &&
        sourceY < 512
      )
        result[y * 512 + x] = mask[sourceY * 512 + sourceX];
    }
  return result;
}

function iou(a, b) {
  let intersection = 0;
  let union = 0;
  for (let index = 0; index < a.length; index += 1) {
    if (a[index] && b[index]) intersection += 1;
    if (a[index] || b[index]) union += 1;
  }
  return intersection / union;
}

function fillHoles(mask) {
  const outside = new Uint8Array(mask.length);
  const queue = new Int32Array(mask.length);
  let head = 0;
  let tail = 0;
  const enqueue = (index) => {
    if (!mask[index] && !outside[index]) {
      outside[index] = 1;
      queue[tail++] = index;
    }
  };
  for (let x = 0; x < 512; x += 1) {
    enqueue(x);
    enqueue(511 * 512 + x);
  }
  for (let y = 0; y < 512; y += 1) {
    enqueue(y * 512);
    enqueue(y * 512 + 511);
  }
  while (head < tail) {
    const index = queue[head++];
    const x = index % 512;
    const y = Math.floor(index / 512);
    if (x > 0) enqueue(index - 1);
    if (x < 511) enqueue(index + 1);
    if (y > 0) enqueue(index - 512);
    if (y < 511) enqueue(index + 512);
  }
  const filled = new Uint8Array(mask.length);
  for (let index = 0; index < mask.length; index += 1)
    filled[index] = mask[index] || !outside[index] ? 1 : 0;
  return filled;
}

async function saveOverlay(file, reference, candidate) {
  const pixels = Buffer.alloc(512 * 512 * 4);
  for (let index = 0; index < reference.length; index += 1) {
    const offset = index * 4;
    if (reference[index] && candidate[index]) {
      pixels[offset] = 245;
      pixels[offset + 1] = 245;
      pixels[offset + 2] = 245;
      pixels[offset + 3] = 255;
    } else if (candidate[index]) {
      pixels[offset] = 239;
      pixels[offset + 1] = 68;
      pixels[offset + 2] = 68;
      pixels[offset + 3] = 255;
    } else if (reference[index]) {
      pixels[offset] = 34;
      pixels[offset + 1] = 211;
      pixels[offset + 2] = 238;
      pixels[offset + 3] = 255;
    }
  }
  await sharp(pixels, {
    raw: { width: 512, height: 512, channels: 4 },
  })
    .png()
    .toFile(file);
}

async function contactBoard(file, items) {
  const panelWidth = 700;
  const panelHeight = 700;
  const gap = 18;
  const boardWidth = panelWidth * 2 + gap * 3;
  const boardHeight = panelHeight * 2 + gap * 3;
  const composites = [];
  for (let index = 0; index < items.length; index += 1) {
    const [image, label] = items[index];
    const left = gap + (index % 2) * (panelWidth + gap);
    const top = gap + Math.floor(index / 2) * (panelHeight + gap);
    composites.push({
      input: await panel(image, label, panelWidth, panelHeight),
      left,
      top,
    });
  }
  await sharp({
    create: {
      width: boardWidth,
      height: boardHeight,
      channels: 4,
      background: '#111827',
    },
  })
    .composite(composites)
    .png()
    .toFile(file);
}

async function panel(file, label, width, height) {
  const image = await sharp(file)
    .resize(width, height - 48, {
      fit: 'contain',
      background: '#4b5563',
    })
    .png()
    .toBuffer();
  const labelSvg = Buffer.from(
    `<svg width="${width}" height="48" xmlns="http://www.w3.org/2000/svg">
      <rect width="100%" height="100%" fill="#0f172a"/>
      <text x="18" y="31" fill="#e5e7eb" font-family="system-ui, sans-serif"
        font-size="20" font-weight="600">${escapeXml(label)}</text>
    </svg>`,
  );
  return sharp({
    create: {
      width,
      height,
      channels: 4,
      background: '#4b5563',
    },
  })
    .composite([
      { input: image, left: 0, top: 0 },
      { input: labelSvg, left: 0, top: height - 48 },
    ])
    .png()
    .toBuffer();
}

function escapeXml(value) {
  return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;');
}

function hash(buffer) {
  return createHash('sha256').update(buffer).digest('hex');
}
