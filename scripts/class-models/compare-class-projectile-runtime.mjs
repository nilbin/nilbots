#!/usr/bin/env node

/**
 * Independent top-planform gate for the authored class-projectile GLBs.
 *
 * Blender owns geometry/export and writes a strict orthographic top render.
 * This script compares that render's alpha against the canonical runtime SVG
 * after uniform longest-span normalization. It never non-uniformly warps,
 * rotates or mirrors either source.
 */

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import process from 'node:process';

const SIZE = 512;
const TARGET_SPAN = 410;
const MASK_THRESHOLD = 16;
const IDS = [
  'trident-spark',
  'rebound-diamond',
  'lattice-rivet',
];

function digest(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

function bounds(mask) {
  let left = SIZE;
  let right = -1;
  let top = SIZE;
  let bottom = -1;
  for (let y = 0; y < SIZE; y += 1) {
    for (let x = 0; x < SIZE; x += 1) {
      if (!mask[y * SIZE + x]) continue;
      left = Math.min(left, x);
      right = Math.max(right, x);
      top = Math.min(top, y);
      bottom = Math.max(bottom, y);
    }
  }
  if (right < left) throw new Error('Empty silhouette.');
  return {
    left,
    right,
    top,
    bottom,
    width: right - left + 1,
    height: bottom - top + 1,
  };
}

async function alphaMask(sharp, input) {
  const { data, info } = await sharp(input)
    .resize(SIZE, SIZE, { fit: 'fill' })
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  if (info.width !== SIZE || info.height !== SIZE || info.channels !== 4)
    throw new Error('Unexpected raster dimensions.');
  const mask = new Uint8Array(SIZE * SIZE);
  for (let index = 0; index < mask.length; index += 1)
    mask[index] = data[index * 4 + 3] >= MASK_THRESHOLD ? 1 : 0;
  return mask;
}

function normalized(mask) {
  const source = bounds(mask);
  const scale = TARGET_SPAN / Math.max(source.width, source.height);
  const width = Math.max(1, Math.round(source.width * scale));
  const height = Math.max(1, Math.round(source.height * scale));
  const left = Math.floor((SIZE - width) / 2);
  const top = Math.floor((SIZE - height) / 2);
  const result = new Uint8Array(SIZE * SIZE);
  for (let y = 0; y < height; y += 1) {
    const sourceY = Math.min(
      source.height - 1,
      Math.floor((y + 0.5) / scale),
    );
    for (let x = 0; x < width; x += 1) {
      const sourceX = Math.min(
        source.width - 1,
        Math.floor((x + 0.5) / scale),
      );
      result[(top + y) * SIZE + left + x] =
        mask[(source.top + sourceY) * SIZE + source.left + sourceX];
    }
  }
  return result;
}

function overlap(reference, candidate, dx, dy) {
  let intersection = 0;
  let union = 0;
  let referenceCount = 0;
  let candidateCount = 0;
  for (let y = 0; y < SIZE; y += 1) {
    for (let x = 0; x < SIZE; x += 1) {
      const referenceValue = Boolean(reference[y * SIZE + x]);
      const sourceX = x - dx;
      const sourceY = y - dy;
      const candidateValue =
        sourceX >= 0 &&
        sourceX < SIZE &&
        sourceY >= 0 &&
        sourceY < SIZE &&
        Boolean(candidate[sourceY * SIZE + sourceX]);
      referenceCount += referenceValue ? 1 : 0;
      candidateCount += candidateValue ? 1 : 0;
      intersection += referenceValue && candidateValue ? 1 : 0;
      union += referenceValue || candidateValue ? 1 : 0;
    }
  }
  return {
    dx,
    dy,
    iou: intersection / union,
    coverage: intersection / referenceCount,
    precision: intersection / candidateCount,
  };
}

function bestTranslation(reference, candidate) {
  let best = overlap(reference, candidate, 0, 0);
  for (let dy = -6; dy <= 6; dy += 1) {
    for (let dx = -6; dx <= 6; dx += 1) {
      const current = overlap(reference, candidate, dx, dy);
      if (
        current.iou > best.iou ||
        (current.iou === best.iou &&
          Math.abs(dx) + Math.abs(dy) <
            Math.abs(best.dx) + Math.abs(best.dy))
      )
        best = current;
    }
  }
  return best;
}

function shifted(mask, dx, dy) {
  const result = new Uint8Array(mask.length);
  for (let y = 0; y < SIZE; y += 1) {
    for (let x = 0; x < SIZE; x += 1) {
      const sourceX = x - dx;
      const sourceY = y - dy;
      if (
        sourceX >= 0 &&
        sourceX < SIZE &&
        sourceY >= 0 &&
        sourceY < SIZE
      )
        result[y * SIZE + x] = mask[sourceY * SIZE + sourceX];
    }
  }
  return result;
}

function apertures(mask) {
  const visited = new Uint8Array(mask.length);
  const queue = new Int32Array(mask.length);
  let head = 0;
  let tail = 0;
  const enqueue = (index) => {
    if (mask[index] || visited[index]) return;
    visited[index] = 1;
    queue[tail++] = index;
  };
  for (let x = 0; x < SIZE; x += 1) {
    enqueue(x);
    enqueue((SIZE - 1) * SIZE + x);
  }
  for (let y = 0; y < SIZE; y += 1) {
    enqueue(y * SIZE);
    enqueue(y * SIZE + SIZE - 1);
  }
  while (head < tail) {
    const index = queue[head++];
    const x = index % SIZE;
    const y = Math.floor(index / SIZE);
    if (x > 0) enqueue(index - 1);
    if (x + 1 < SIZE) enqueue(index + 1);
    if (y > 0) enqueue(index - SIZE);
    if (y + 1 < SIZE) enqueue(index + SIZE);
  }
  const areas = [];
  for (let start = 0; start < mask.length; start += 1) {
    if (mask[start] || visited[start]) continue;
    let area = 0;
    head = 0;
    tail = 0;
    visited[start] = 1;
    queue[tail++] = start;
    while (head < tail) {
      const index = queue[head++];
      area += 1;
      const x = index % SIZE;
      const y = Math.floor(index / SIZE);
      for (const next of [
        x > 0 ? index - 1 : -1,
        x + 1 < SIZE ? index + 1 : -1,
        y > 0 ? index - SIZE : -1,
        y + 1 < SIZE ? index + SIZE : -1,
      ]) {
        if (next >= 0 && !mask[next] && !visited[next]) {
          visited[next] = 1;
          queue[tail++] = next;
        }
      }
    }
    areas.push(area);
  }
  return areas.sort((left, right) => right - left);
}

async function writeOverlay(sharp, output, reference, candidate) {
  const pixels = Buffer.alloc(SIZE * SIZE * 4);
  for (let index = 0; index < reference.length; index += 1) {
    const canonical = Boolean(reference[index]);
    const model = Boolean(candidate[index]);
    const offset = index * 4;
    const color =
      canonical && model
        ? [236, 241, 245]
        : model
          ? [239, 68, 68]
          : canonical
            ? [34, 211, 238]
            : [13, 18, 24];
    pixels[offset] = color[0];
    pixels[offset + 1] = color[1];
    pixels[offset + 2] = color[2];
    pixels[offset + 3] = 255;
  }
  await sharp(pixels, {
    raw: { width: SIZE, height: SIZE, channels: 4 },
  })
    .png()
    .toFile(output);
}

async function main() {
  const repoRoot = path.resolve(process.argv[2] ?? '.');
  const requireFromWeb = createRequire(path.join(repoRoot, 'web/package.json'));
  const sharp = requireFromWeb('sharp');
  const results = {};

  for (const id of IDS) {
    const svgPath = path.join(
      repoRoot,
      'web/src/assets/class-projectile-looks',
      id,
      'sprite.svg',
    );
    const modelPath = path.join(
      repoRoot,
      'web/src/assets/class-projectile-looks',
      id,
      'model.glb',
    );
    const reviewDirectory = path.join(
      repoRoot,
      'art/class-models/class-projectiles/review',
      id,
    );
    const topPath = path.join(reviewDirectory, 'top-neutral.png');
    const svgBytes = await readFile(svgPath);
    const modelBytes = await readFile(modelPath);
    const topBytes = await readFile(topPath);
    const reference = normalized(await alphaMask(sharp, svgBytes));
    const candidate = normalized(await alphaMask(sharp, topBytes));
    const metrics = bestTranslation(reference, candidate);
    const aligned = shifted(candidate, metrics.dx, metrics.dy);
    const authorityApertures = apertures(reference);
    const modelApertures = apertures(aligned);
    if (metrics.iou < 0.92)
      throw new Error(`${id}: top-planform IoU ${metrics.iou} is below 0.92.`);
    if (authorityApertures.length !== modelApertures.length)
      throw new Error(`${id}: canonical aperture count was not preserved.`);
    await writeOverlay(
      sharp,
      path.join(reviewDirectory, 'top-overlay.png'),
      reference,
      aligned,
    );
    results[id] = {
      canonicalSvgSha256: digest(svgBytes),
      modelGlbSha256: digest(modelBytes),
      topRenderSha256: digest(topBytes),
      topPlanform: {
        method:
          'orthographic alpha; uniform longest-span normalization and translation only',
        iou: metrics.iou,
        coverage: metrics.coverage,
        precision: metrics.precision,
        translationPixels: [metrics.dx, metrics.dy],
        canonicalAperturesPixels: authorityApertures,
        modelAperturesPixels: modelApertures,
      },
    };
    console.log(`${id}: ${(metrics.iou * 100).toFixed(2)}% top IoU`);
  }

  await writeFile(
    path.join(
      repoRoot,
      'art/class-models/class-projectiles/silhouette-report.json',
    ),
    `${JSON.stringify(
      {
        schemaVersion: 1,
        generatedBy: 'scripts/class-models/compare-class-projectile-runtime.mjs',
        maskThreshold: MASK_THRESHOLD,
        results,
      },
      null,
      2,
    )}\n`,
  );
}

await main();
