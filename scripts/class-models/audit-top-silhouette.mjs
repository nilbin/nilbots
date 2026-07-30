import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const requireFromWeb = createRequire(resolve(repositoryRoot, 'web/package.json'));
const sharp = requireFromWeb('sharp');

const options = parseOptions(process.argv.slice(2));
const canonicalPath = resolve(options.canonical);
const candidatePath = resolve(options.candidate);
const outputDirectory = resolve(options.output);
await mkdir(outputDirectory, { recursive: true });

const [canonicalBytes, candidateBytes] = await Promise.all([
  readFile(canonicalPath),
  readFile(candidatePath),
]);
const [canonicalImage, candidateImage] = await Promise.all([
  alphaMask(canonicalBytes),
  alphaMask(candidateBytes),
]);

const canvasSize = 768;
const canonical = await normalizeMask(canonicalImage, canvasSize);
const candidate = await normalizeMask(candidateImage, canvasSize);
const full = compare(canonical.mask, candidate.mask);
const outerCanonical = fillHoles(canonical.mask, canvasSize, canvasSize);
const outerCandidate = fillHoles(candidate.mask, canvasSize, canvasSize);
const outer = compare(outerCanonical, outerCandidate);

const overlay = Buffer.alloc(canvasSize * canvasSize * 4);
for (let index = 0; index < canonical.mask.length; index += 1) {
  const reference = canonical.mask[index] === 1;
  const model = candidate.mask[index] === 1;
  const offset = index * 4;
  if (reference && model) {
    overlay.set([222, 228, 234, 255], offset);
  } else if (reference) {
    overlay.set([0, 220, 255, 255], offset);
  } else if (model) {
    overlay.set([255, 72, 139, 255], offset);
  } else {
    overlay.set([18, 22, 29, 255], offset);
  }
}

const overlayPath = resolve(outputDirectory, 'top-silhouette-overlay.png');
await sharp(overlay, {
  raw: { width: canvasSize, height: canvasSize, channels: 4 },
})
  .png({ compressionLevel: 9, adaptiveFiltering: false })
  .toFile(overlayPath);

const report = {
  schemaVersion: 1,
  inputs: {
    canonical: {
      file: canonicalPath,
      bytes: canonicalBytes.length,
      sha256: sha256(canonicalBytes),
    },
    candidate: {
      file: candidatePath,
      bytes: candidateBytes.length,
      sha256: sha256(candidateBytes),
    },
  },
  method: {
    alphaThreshold: 64,
    orientation: 'unchanged; both inputs face East/right',
    alignment:
      'largest-alpha bounds, one uniform candidate scale to canonical width, bounding-box centres aligned',
    prohibited: ['rotation', 'reflection', 'anisotropic scale', 'warp'],
    canvas: { width: canvasSize, height: canvasSize },
    overlayLegend: {
      overlap: '#dee4ea',
      canonicalOnly: '#00dcff',
      candidateOnly: '#ff488b',
    },
  },
  measurements: {
    canonicalBounds: canonical.sourceBounds,
    candidateBounds: candidate.sourceBounds,
    canonicalAspect: round(canonical.sourceBounds.width / canonical.sourceBounds.height),
    candidateAspect: round(candidate.sourceBounds.width / candidate.sourceBounds.height),
    full: summarize(full),
    outerEnvelope: summarize(outer),
  },
};
const reportPath = resolve(outputDirectory, 'top-silhouette-report.json');
await writeFile(reportPath, `${JSON.stringify(report, null, 2)}\n`, 'utf8');
console.log(JSON.stringify(report.measurements, null, 2));

function parseOptions(args) {
  const result = {};
  for (let index = 0; index < args.length; index += 1) {
    const key = args[index];
    if (!key.startsWith('--') || index + 1 >= args.length) {
      throw new Error(
        'Usage: audit-top-silhouette.mjs --canonical <png> --candidate <png> --output <directory>',
      );
    }
    result[key.slice(2)] = args[++index];
  }
  for (const required of ['canonical', 'candidate', 'output']) {
    if (!result[required]) throw new Error(`Missing --${required}.`);
  }
  return result;
}

async function alphaMask(bytes) {
  const { data, info } = await sharp(bytes)
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const mask = new Uint8Array(info.width * info.height);
  for (let index = 0; index < mask.length; index += 1) {
    mask[index] = data[index * info.channels + 3] >= 64 ? 1 : 0;
  }
  return { mask, width: info.width, height: info.height };
}

function bounds(mask, width, height) {
  let minX = width;
  let minY = height;
  let maxX = -1;
  let maxY = -1;
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      if (!mask[y * width + x]) continue;
      minX = Math.min(minX, x);
      minY = Math.min(minY, y);
      maxX = Math.max(maxX, x);
      maxY = Math.max(maxY, y);
    }
  }
  if (maxX < minX) throw new Error('Silhouette alpha is empty.');
  return {
    minX,
    minY,
    maxX,
    maxY,
    width: maxX - minX + 1,
    height: maxY - minY + 1,
  };
}

async function normalizeMask(image, canvasSize) {
  const sourceBounds = bounds(image.mask, image.width, image.height);
  const cropped = Buffer.alloc(sourceBounds.width * sourceBounds.height);
  for (let y = 0; y < sourceBounds.height; y += 1) {
    const source = (sourceBounds.minY + y) * image.width + sourceBounds.minX;
    image.mask
      .subarray(source, source + sourceBounds.width)
      .forEach((value, x) => {
        cropped[y * sourceBounds.width + x] = value ? 255 : 0;
      });
  }
  const targetWidth = Math.round(canvasSize * 0.82);
  const targetHeight = Math.round(
    sourceBounds.height * (targetWidth / sourceBounds.width),
  );
  const { data: resized, info: resizedInfo } = await sharp(cropped, {
    raw: {
      width: sourceBounds.width,
      height: sourceBounds.height,
      channels: 1,
    },
  })
    .resize(targetWidth, targetHeight, { kernel: 'nearest' })
    .raw()
    .toBuffer({ resolveWithObject: true });
  const mask = new Uint8Array(canvasSize * canvasSize);
  const left = Math.round((canvasSize - targetWidth) / 2);
  const top = Math.round((canvasSize - targetHeight) / 2);
  for (let y = 0; y < targetHeight; y += 1) {
    for (let x = 0; x < targetWidth; x += 1) {
      const sourceIndex =
        (y * targetWidth + x) * resizedInfo.channels;
      if (resized[sourceIndex] > 0) {
        mask[(top + y) * canvasSize + left + x] = 1;
      }
    }
  }
  return { mask, sourceBounds };
}

function fillHoles(mask, width, height) {
  const background = new Uint8Array(mask.length);
  const queue = new Int32Array(mask.length);
  let head = 0;
  let tail = 0;
  const enqueue = (index) => {
    if (index < 0 || index >= mask.length || mask[index] || background[index]) {
      return;
    }
    background[index] = 1;
    queue[tail++] = index;
  };
  for (let x = 0; x < width; x += 1) {
    enqueue(x);
    enqueue((height - 1) * width + x);
  }
  for (let y = 1; y + 1 < height; y += 1) {
    enqueue(y * width);
    enqueue(y * width + width - 1);
  }
  while (head < tail) {
    const index = queue[head++];
    const x = index % width;
    const y = Math.floor(index / width);
    if (x > 0) enqueue(index - 1);
    if (x + 1 < width) enqueue(index + 1);
    if (y > 0) enqueue(index - width);
    if (y + 1 < height) enqueue(index + width);
  }
  const filled = new Uint8Array(mask.length);
  for (let index = 0; index < filled.length; index += 1) {
    filled[index] = background[index] ? 0 : 1;
  }
  return filled;
}

function compare(left, right) {
  let intersection = 0;
  let union = 0;
  let leftArea = 0;
  let rightArea = 0;
  for (let index = 0; index < left.length; index += 1) {
    const a = left[index] === 1;
    const b = right[index] === 1;
    if (a) leftArea += 1;
    if (b) rightArea += 1;
    if (a && b) intersection += 1;
    if (a || b) union += 1;
  }
  return { intersection, union, leftArea, rightArea };
}

function summarize(measurement) {
  return {
    intersectionPixels: measurement.intersection,
    unionPixels: measurement.union,
    intersectionOverUnion: round(
      measurement.intersection / measurement.union,
    ),
    intersectionOverUnionPercent: round(
      (measurement.intersection / measurement.union) * 100,
      3,
    ),
    canonicalCoveragePercent: round(
      (measurement.intersection / measurement.leftArea) * 100,
      3,
    ),
    candidateCoveragePercent: round(
      (measurement.intersection / measurement.rightArea) * 100,
      3,
    ),
  };
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

function round(value, digits = 6) {
  return Number(value.toFixed(digits));
}
