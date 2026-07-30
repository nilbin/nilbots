#!/usr/bin/env node

/**
 * Deterministic raster measurements for the Blender projectile-master audit.
 *
 * The Blender script owns geometry inspection and fixed-view rendering. This
 * companion rasterizes the canonical SVG masks, rigidly normalizes longest
 * planform span, measures top overlap/apertures and prepares review overlays.
 */

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import process from 'node:process';

const SIZE = 1024;
const TARGET_SPAN = 820;
// Projectile masks intentionally use low-opacity outer energy layers. Treat
// authored visible alpha as silhouette occupancy while still ignoring tiny
// antialias quantization noise.
const MASK_THRESHOLD = 16;

const PROJECTILES = [
  {
    assetId: 'trident-spark',
    label: 'Trident Spark',
    taskId: '019fb203-d779-779d-8374-59deec6606d3',
    sourceFacing: '+x',
    lookScale: 0.58,
    comparisonDetail: 'coherent master · cleanup required',
    verdict: 'coherent-reconstruction-master-not-runtime-ready',
    rebuild: [
      'retain the coherent one-piece identity and material evidence',
      'restore the exact SVG wing/fork planform without non-uniformly warping the master',
      'rebuild the pinched underside and approved side depth while preserving the top surface',
      'retopologize and rebake to a gameplay tier with tangents',
      'author neutral projectile-mask semantics and verify both team tints',
    ],
  },
  {
    assetId: 'rebound-diamond',
    label: 'Rebound Diamond',
    taskId: '019fb206-978f-7ff8-9f18-e2b725c02584',
    sourceFacing: '-x',
    lookScale: 0.56,
    comparisonDetail: 'coherent master · aperture correction',
    verdict: 'coherent-reconstruction-master-needs-aperture-contour-correction',
    rebuild: [
      'retain the coherent one-piece ring and high-detail material evidence',
      'keep the outer long nose on target +X',
      'rebuild the open tunnel to the exact elongated-hex SVG contour; the raw aperture is rounded and directionally inconsistent',
      'retopologize and rebake to a gameplay tier with tangents',
      'author neutral projectile-mask semantics and verify both team tints',
    ],
  },
  {
    assetId: 'lattice-rivet',
    label: 'Lattice Rivet',
    taskId: '019fb209-ea51-76ae-94ed-c6b3c92275b3',
    sourceFacing: '-x',
    lookScale: 0.54,
    comparisonDetail: 'failed raw master · duplicated body',
    verdict: 'failed-duplicated-depth-master-top-reference-only',
    rebuild: [
      'separate the two exact-position-welded components without moving either',
      'retain the complete positive-depth component whose visible top supplied the audited planform',
      'delete the complete negative-depth duplicate; do not average, flatten, or bridge the two bodies',
      'rebuild only the retained component underside to the approved side depth if the post-delete side gate remains thin',
      'retopologize and rebake to a gameplay tier with tangents',
      'author neutral projectile-mask semantics and verify both team tints',
    ],
  },
];

function parseArgs(argv) {
  const result = {
    repoRoot: null,
    artifactsRoot: null,
    phase: null,
  };
  for (let index = 0; index < argv.length; index += 1) {
    const value = argv[index];
    if (value === '--repo-root') result.repoRoot = argv[++index];
    else if (value === '--artifacts-root') result.artifactsRoot = argv[++index];
    else if (value === '--phase') result.phase = argv[++index];
    else throw new Error(`Unknown argument: ${value}`);
  }
  if (!result.repoRoot || !result.artifactsRoot)
    throw new Error('--repo-root and --artifacts-root are required.');
  if (!['probe', 'final'].includes(result.phase))
    throw new Error('--phase must be probe or final.');
  result.repoRoot = path.resolve(result.repoRoot);
  result.artifactsRoot = path.resolve(result.artifactsRoot);
  return result;
}

function digest(input) {
  return createHash('sha256').update(input).digest('hex');
}

function boundingBox(mask, width = SIZE, height = SIZE) {
  let left = width;
  let right = -1;
  let top = height;
  let bottom = -1;
  for (let y = 0; y < height; y += 1) {
    const offset = y * width;
    for (let x = 0; x < width; x += 1) {
      if (!mask[offset + x]) continue;
      if (x < left) left = x;
      if (x > right) right = x;
      if (y < top) top = y;
      if (y > bottom) bottom = y;
    }
  }
  if (right < left || bottom < top) throw new Error('Mask is empty.');
  return {
    left,
    top,
    right,
    bottom,
    width: right - left + 1,
    height: bottom - top + 1,
  };
}

function normalizeLongestSpan(mask) {
  const bounds = boundingBox(mask);
  const scale = TARGET_SPAN / Math.max(bounds.width, bounds.height);
  const targetWidth = Math.max(1, Math.round(bounds.width * scale));
  const targetHeight = Math.max(1, Math.round(bounds.height * scale));
  const left = Math.floor((SIZE - targetWidth) / 2);
  const top = Math.floor((SIZE - targetHeight) / 2);
  const result = new Uint8Array(SIZE * SIZE);
  for (let y = 0; y < targetHeight; y += 1) {
    const sourceY = Math.min(
      bounds.height - 1,
      Math.floor((y + 0.5) / scale),
    );
    const sourceOffset = (bounds.top + sourceY) * SIZE + bounds.left;
    const targetOffset = (top + y) * SIZE + left;
    for (let x = 0; x < targetWidth; x += 1) {
      const sourceX = Math.min(
        bounds.width - 1,
        Math.floor((x + 0.5) / scale),
      );
      result[targetOffset + x] = mask[sourceOffset + sourceX];
    }
  }
  return {
    mask: result,
    sourceBounds: bounds,
    normalizedBounds: boundingBox(result),
    scale,
  };
}

function overlap(reference, candidate, dx = 0, dy = 0) {
  let intersection = 0;
  let union = 0;
  let referenceCount = 0;
  let candidateCount = 0;
  for (let y = 0; y < SIZE; y += 1) {
    for (let x = 0; x < SIZE; x += 1) {
      const ref = reference[y * SIZE + x] !== 0;
      const sourceX = x - dx;
      const sourceY = y - dy;
      const candidateValue =
        sourceX >= 0 &&
        sourceX < SIZE &&
        sourceY >= 0 &&
        sourceY < SIZE &&
        candidate[sourceY * SIZE + sourceX] !== 0;
      if (ref) referenceCount += 1;
      if (candidateValue) candidateCount += 1;
      if (ref && candidateValue) intersection += 1;
      if (ref || candidateValue) union += 1;
    }
  }
  return {
    iou: union === 0 ? 0 : intersection / union,
    intersection,
    union,
    referenceCount,
    candidateCount,
    coverage:
      referenceCount === 0 ? 0 : intersection / referenceCount,
    precision:
      candidateCount === 0 ? 0 : intersection / candidateCount,
  };
}

function bestTranslation(reference, candidate, radius = 8) {
  let best = null;
  for (let dy = -radius; dy <= radius; dy += 1) {
    for (let dx = -radius; dx <= radius; dx += 1) {
      const metrics = overlap(reference, candidate, dx, dy);
      if (
        best === null ||
        metrics.iou > best.iou ||
        (metrics.iou === best.iou &&
          Math.abs(dx) + Math.abs(dy) <
            Math.abs(best.dx) + Math.abs(best.dy))
      ) {
        best = { ...metrics, dx, dy };
      }
    }
  }
  return best;
}

function shifted(mask, dx, dy) {
  const result = new Uint8Array(mask.length);
  for (let y = 0; y < SIZE; y += 1) {
    const sourceY = y - dy;
    if (sourceY < 0 || sourceY >= SIZE) continue;
    for (let x = 0; x < SIZE; x += 1) {
      const sourceX = x - dx;
      if (sourceX < 0 || sourceX >= SIZE) continue;
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

  function enqueue(index) {
    if (mask[index] || visited[index]) return;
    visited[index] = 1;
    queue[tail++] = index;
  }

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

  const holes = [];
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
      if (x > 0) enqueue(index - 1);
      if (x + 1 < SIZE) enqueue(index + 1);
      if (y > 0) enqueue(index - SIZE);
      if (y + 1 < SIZE) enqueue(index + SIZE);
    }
    holes.push(area);
  }
  holes.sort((left, right) => right - left);
  const objectPixels = mask.reduce(
    (sum, value) => sum + (value ? 1 : 0),
    0,
  );
  return {
    count: holes.length,
    areasPixels: holes,
    largestAreaToObject: holes.length
      ? holes[0] / objectPixels
      : 0,
  };
}

async function alphaMask(sharp, input) {
  const { data, info } = await sharp(input)
    .resize(SIZE, SIZE, { fit: 'fill' })
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  if (info.width !== SIZE || info.height !== SIZE || info.channels !== 4)
    throw new Error(`Unexpected raster shape for ${input}`);
  const result = new Uint8Array(SIZE * SIZE);
  for (let index = 0; index < result.length; index += 1)
    result[index] =
      data[index * 4 + 3] >= MASK_THRESHOLD ? 1 : 0;
  return result;
}

async function authorityMask(sharp, svgPath) {
  const svg = await readFile(svgPath);
  return alphaMask(sharp, svg);
}

async function writeOverlay(sharp, output, reference, candidate) {
  const pixels = Buffer.alloc(SIZE * SIZE * 4);
  for (let index = 0; index < reference.length; index += 1) {
    const ref = Boolean(reference[index]);
    const model = Boolean(candidate[index]);
    const offset = index * 4;
    if (ref && model) {
      pixels[offset] = 245;
      pixels[offset + 1] = 245;
      pixels[offset + 2] = 245;
    } else if (model) {
      pixels[offset] = 239;
      pixels[offset + 1] = 68;
      pixels[offset + 2] = 68;
    } else if (ref) {
      pixels[offset] = 34;
      pixels[offset + 1] = 211;
      pixels[offset + 2] = 238;
    } else {
      pixels[offset] = 13;
      pixels[offset + 1] = 18;
      pixels[offset + 2] = 24;
    }
    pixels[offset + 3] = 255;
  }
  await sharp(pixels, {
    raw: { width: SIZE, height: SIZE, channels: 4 },
  })
    .png()
    .toFile(output);
}

async function backgroundSeparatedMask(sharp, input) {
  const { data, info } = await sharp(input)
    .resize(SIZE, SIZE, { fit: 'fill' })
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const result = new Uint8Array(SIZE * SIZE);
  for (let index = 0; index < result.length; index += 1) {
    const offset = index * info.channels;
    const alpha = data[offset + 3];
    const delta = Math.max(
      Math.abs(data[offset] - 184),
      Math.abs(data[offset + 1] - 184),
      Math.abs(data[offset + 2] - 184),
    );
    result[index] = alpha >= MASK_THRESHOLD && delta >= 7 ? 1 : 0;
  }
  return result;
}

function hueSaturationValue(red, green, blue) {
  const r = red / 255;
  const g = green / 255;
  const b = blue / 255;
  const maximum = Math.max(r, g, b);
  const minimum = Math.min(r, g, b);
  const delta = maximum - minimum;
  let hue = 0;
  if (delta !== 0) {
    if (maximum === r) hue = 60 * (((g - b) / delta) % 6);
    else if (maximum === g) hue = 60 * ((b - r) / delta + 2);
    else hue = 60 * ((r - g) / delta + 4);
  }
  if (hue < 0) hue += 360;
  return {
    hue,
    saturation: maximum === 0 ? 0 : delta / maximum,
    value: maximum,
  };
}

async function textureColorStats(sharp, input) {
  const { data, info } = await sharp(input)
    .resize(512, 512, { fit: 'fill' })
    .removeAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  let saturation = 0;
  let chromatic = 0;
  let cyan = 0;
  let red = 0;
  let orange = 0;
  let violet = 0;
  const pixels = info.width * info.height;
  for (let index = 0; index < pixels; index += 1) {
    const offset = index * info.channels;
    const hsv = hueSaturationValue(
      data[offset],
      data[offset + 1],
      data[offset + 2],
    );
    saturation += hsv.saturation;
    const strong = hsv.saturation >= 0.35 && hsv.value >= 0.22;
    if (!strong) continue;
    chromatic += 1;
    if (hsv.hue >= 165 && hsv.hue <= 210) cyan += 1;
    if (hsv.hue <= 18 || hsv.hue >= 342) red += 1;
    if (hsv.hue >= 19 && hsv.hue <= 50) orange += 1;
    if (hsv.hue >= 255 && hsv.hue <= 310) violet += 1;
  }
  return {
    sampledPixels: pixels,
    meanSaturation: saturation / pixels,
    stronglyChromaticFraction: chromatic / pixels,
    strongHueFractions: {
      cyan: cyan / pixels,
      red: red / pixels,
      orange: orange / pixels,
      violet: violet / pixels,
    },
  };
}

function escapeXml(value) {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;');
}

function labelSvg(width, height, label, detail = '') {
  return Buffer.from(
    `<svg width="${width}" height="${height}" xmlns="http://www.w3.org/2000/svg">` +
      `<rect width="100%" height="100%" fill="#0d1218"/>` +
      `<text x="20" y="31" fill="#f5f5f5" font-size="22" font-family="Arial, sans-serif" font-weight="700">${escapeXml(label)}</text>` +
      (detail
        ? `<text x="20" y="56" fill="#9fb0c2" font-size="15" font-family="Arial, sans-serif">${escapeXml(detail)}</text>`
        : '') +
      `</svg>`,
  );
}

async function reviewPanel(
  sharp,
  input,
  label,
  detail = '',
  {
    width = 512,
    height = 512,
    imageHeight = 440,
    background = '#5b5b5b',
  } = {},
) {
  const source = await sharp(input)
    .resize(width, imageHeight, {
      fit: 'contain',
      background,
    })
    .png()
    .toBuffer();
  return sharp({
    create: {
      width,
      height,
      channels: 4,
      background: '#0d1218',
    },
  })
    .composite([
      { input: source, left: 0, top: 0 },
      {
        input: labelSvg(width, height - imageHeight, label, detail),
        left: 0,
        top: imageHeight,
      },
    ])
    .png()
    .toBuffer();
}

async function writeContactSheet(sharp, output, panels, columns = 4) {
  const width = 512 * columns;
  const rows = Math.ceil(panels.length / columns);
  const composites = panels.map((input, index) => ({
    input,
    left: (index % columns) * 512,
    top: Math.floor(index / columns) * 512,
  }));
  await sharp({
    create: {
      width,
      height: rows * 512,
      channels: 4,
      background: '#0d1218',
    },
  })
    .composite(composites)
    .png()
    .toFile(output);
}

async function glbDocument(input) {
  const buffer = await readFile(input);
  if (buffer.toString('ascii', 0, 4) !== 'glTF')
    throw new Error(`Not a GLB: ${input}`);
  const version = buffer.readUInt32LE(4);
  if (version !== 2) throw new Error(`Unsupported GLB version: ${version}`);
  const jsonLength = buffer.readUInt32LE(12);
  const jsonType = buffer.readUInt32LE(16);
  if (jsonType !== 0x4e4f534a)
    throw new Error(`First GLB chunk is not JSON: ${input}`);
  return JSON.parse(
    buffer
      .subarray(20, 20 + jsonLength)
      .toString('utf8')
      .replace(/\0+$/u, '')
      .trim(),
  );
}

function auditDirectory(args, projectile) {
  return path.join(
    args.repoRoot,
    'art/class-models/provider-runs/meshy',
    projectile.assetId,
    projectile.taskId,
    'audit',
  );
}

async function probe(args, sharp) {
  for (const projectile of PROJECTILES) {
    const directory = auditDirectory(args, projectile);
    const svgPath = path.join(
      args.repoRoot,
      'web/src/assets/class-projectile-looks',
      projectile.assetId,
      'sprite.svg',
    );
    const reference = normalizeLongestSpan(
      await authorityMask(sharp, svgPath),
    );
    const candidates = {};
    for (const direction of ['+x', '-x']) {
      const suffix = direction[0];
      const candidate = normalizeLongestSpan(
        await alphaMask(
          sharp,
          path.join(directory, `probe-top-${suffix}.png`),
        ),
      );
      const metrics = bestTranslation(reference.mask, candidate.mask);
      candidates[direction] = {
        iou: metrics.iou,
        coverage: metrics.coverage,
        precision: metrics.precision,
        translationPixels: [metrics.dx, metrics.dy],
        sourceBounds: candidate.sourceBounds,
        normalizationScale: candidate.scale,
        apertures: apertures(
          shifted(candidate.mask, metrics.dx, metrics.dy),
        ),
      };
    }
    const recommended = Object.entries(candidates).sort(
      (left, right) => right[1].iou - left[1].iou,
    )[0][0];
    const report = {
      schemaVersion: 1,
      assetId: projectile.assetId,
      method:
        'longest-planform-span normalization, centered bounds, uniform scale and translation only',
      authority: {
        path: path.relative(args.repoRoot, svgPath),
        sha256: digest(await readFile(svgPath)),
        bounds: reference.sourceBounds,
        apertures: apertures(reference.mask),
      },
      candidates,
      recommendedSourceFacing: recommended,
      inspectedSourceFacing: projectile.sourceFacing,
      sourceFacingAgreement:
        recommended === projectile.sourceFacing,
    };
    await writeFile(
      path.join(directory, 'orientation-audit.json'),
      `${JSON.stringify(report, null, 2)}\n`,
    );
    console.log(
      `${projectile.assetId}: +x ${(candidates['+x'].iou * 100).toFixed(2)}%, ` +
        `-x ${(candidates['-x'].iou * 100).toFixed(2)}%, ` +
        `choose ${projectile.sourceFacing}`,
    );
  }
}

async function tintProxy(sharp, input, color, maximumSpan) {
  const trimmed = await sharp(input)
    .trim({ background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const match = /^#([0-9a-f]{6})$/iu.exec(color);
  if (!match) throw new Error(`Expected six-digit tint: ${color}`);
  const tint = [
    Number.parseInt(match[1].slice(0, 2), 16),
    Number.parseInt(match[1].slice(2, 4), 16),
    Number.parseInt(match[1].slice(4, 6), 16),
  ];
  for (let index = 0; index < trimmed.info.width * trimmed.info.height; index += 1) {
    const offset = index * 4;
    const luminance =
      (trimmed.data[offset] * 0.2126 +
        trimmed.data[offset + 1] * 0.7152 +
        trimmed.data[offset + 2] * 0.0722) /
      255;
    const value = 0.18 + luminance * 0.82;
    trimmed.data[offset] = Math.round(tint[0] * value);
    trimmed.data[offset + 1] = Math.round(tint[1] * value);
    trimmed.data[offset + 2] = Math.round(tint[2] * value);
  }
  const tinted = await sharp(trimmed.data, {
    raw: {
      width: trimmed.info.width,
      height: trimmed.info.height,
      channels: 4,
    },
  })
    .png()
    .toBuffer();
  const metadata = await sharp(tinted).metadata();
  const scale = maximumSpan / Math.max(metadata.width, metadata.height);
  return sharp(tinted)
    .resize(
      Math.max(1, Math.round(metadata.width * scale)),
      Math.max(1, Math.round(metadata.height * scale)),
      { fit: 'fill', kernel: 'lanczos3' },
    )
    .png()
    .toBuffer();
}

async function gameplayComparisonColumn(
  sharp,
  {
    label,
    detail,
    input,
    livePixels,
  },
) {
  const width = 400;
  const height = 900;
  const close = await sharp(input)
    .trim({ background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .resize(340, 340, {
      fit: 'contain',
      background: { r: 0, g: 0, b: 0, alpha: 0 },
    })
    .png()
    .toBuffer();
  const cyan = await tintProxy(sharp, input, '#22d3ee', livePixels);
  const red = await tintProxy(sharp, input, '#f43f5e', livePixels);
  const cyan4x = await tintProxy(sharp, input, '#22d3ee', livePixels * 4);
  const red4x = await tintProxy(sharp, input, '#f43f5e', livePixels * 4);
  const composites = [
    {
      input: labelSvg(width, 76, label, detail),
      left: 0,
      top: 0,
    },
    { input: close, left: 30, top: 88 },
  ];

  const rows = [
    {
      top: 445,
      height: 112,
      background: '#101822',
      label: `cyan · 1× · ${livePixels}px span`,
      image: cyan,
    },
    {
      top: 557,
      height: 112,
      background: '#d7d9dc',
      label: `red · 1× · ${livePixels}px span`,
      image: red,
    },
    {
      top: 669,
      height: 215,
      background: '#101822',
      label: '4× inspection inset · tint proxy',
      image: null,
    },
  ];
  for (const row of rows) {
    composites.push({
      input: {
        create: {
          width,
          height: row.height,
          channels: 4,
          background: row.background,
        },
      },
      left: 0,
      top: row.top,
    });
    composites.push({
      input: Buffer.from(
        `<svg width="${width}" height="28" xmlns="http://www.w3.org/2000/svg"><text x="14" y="20" fill="${row.background === '#d7d9dc' ? '#17212b' : '#d6e4f0'}" font-size="15" font-family="Arial, sans-serif">${escapeXml(row.label)}</text></svg>`,
      ),
      left: 0,
      top: row.top,
    });
  }
  const cyanMetadata = await sharp(cyan).metadata();
  const redMetadata = await sharp(red).metadata();
  const cyan4xMetadata = await sharp(cyan4x).metadata();
  const red4xMetadata = await sharp(red4x).metadata();
  composites.push(
    {
      input: cyan,
      left: Math.round((width - cyanMetadata.width) / 2),
      top: 445 + Math.round((112 - cyanMetadata.height) / 2),
    },
    {
      input: red,
      left: Math.round((width - redMetadata.width) / 2),
      top: 557 + Math.round((112 - redMetadata.height) / 2),
    },
    {
      input: cyan4x,
      left: 95 - Math.round(cyan4xMetadata.width / 2),
      top: 710 + Math.round((160 - cyan4xMetadata.height) / 2),
    },
    {
      input: red4x,
      left: 305 - Math.round(red4xMetadata.width / 2),
      top: 710 + Math.round((160 - red4xMetadata.height) / 2),
    },
  );
  return sharp({
    create: {
      width,
      height,
      channels: 4,
      background: '#0d1218',
    },
  })
    .composite(composites)
    .png()
    .toBuffer();
}

async function pulseControlAudit(args, sharp) {
  const directory = path.join(
    args.repoRoot,
    'art/class-models/controls/pulse-bolt-3d',
  );
  const review = path.join(directory, 'review');
  const svgPath = path.join(
    args.repoRoot,
    'web/src/assets/projectile-looks/pulse-bolt/sprite.svg',
  );
  const reference = normalizeLongestSpan(
    await authorityMask(sharp, svgPath),
  );
  const candidate = normalizeLongestSpan(
    await alphaMask(sharp, path.join(review, 'top-mask.png')),
  );
  const metrics = bestTranslation(reference.mask, candidate.mask);
  const aligned = shifted(candidate.mask, metrics.dx, metrics.dy);
  await writeOverlay(
    sharp,
    path.join(review, 'top-overlay.png'),
    reference.mask,
    aligned,
  );
  const glbPath = path.join(directory, 'pulse-bolt-control.glb');
  const document = await glbDocument(glbPath);
  const roles = (document.materials ?? []).map(
    (material) => material.extras?.nilbotsRole ?? null,
  );
  const report = {
    schemaVersion: 1,
    id: 'pulse-bolt',
    status: 'art-side-comparison-control-only',
    silhouette: {
      method:
        'longest-planform-span normalization, centered bounds, uniform scale and translation only',
      iou: metrics.iou,
      coverage: metrics.coverage,
      precision: metrics.precision,
      translationPixels: [metrics.dx, metrics.dy],
      authorityApertures: apertures(reference.mask),
      modelApertures: apertures(aligned),
    },
    glbStructure: {
      scenes: document.scenes?.length ?? 0,
      nodes: document.nodes?.length ?? 0,
      meshes: document.meshes?.length ?? 0,
      materials: document.materials?.length ?? 0,
      semanticRoles: roles,
      everyMaterialIsProjectileMask:
        roles.length > 0 &&
        roles.every((role) => role === 'projectile-mask'),
      cameras: document.cameras?.length ?? 0,
      animations: document.animations?.length ?? 0,
      skins: document.skins?.length ?? 0,
    },
    runtime: {
      installed: false,
      catalogEntryAdded: false,
      lookIdCreated: false,
      effectivePlanformSpanTiles: 0.54 * 0.72,
    },
    exclusions: [
      'trail',
      'halo',
      'floor glow',
      'impact',
      'hitbox',
      'motion',
    ],
  };
  await writeFile(
    path.join(directory, 'audit-report.json'),
    `${JSON.stringify(report, null, 2)}\n`,
  );
  const panels = await Promise.all([
    reviewPanel(sharp, svgPath, 'Canonical Pulse Bolt SVG', 'identity authority'),
    reviewPanel(
      sharp,
      path.join(review, 'normalized-top.png'),
      'Procedural top',
      `${(metrics.iou * 100).toFixed(2)}% IoU`,
    ),
    reviewPanel(
      sharp,
      path.join(review, 'top-overlay.png'),
      'Exact top overlay',
      'white overlap · red model · cyan SVG',
    ),
    reviewPanel(
      sharp,
      path.join(review, 'normalized-side.png'),
      'Shallow real depth',
      'no trail or halo',
    ),
    reviewPanel(
      sharp,
      path.join(review, 'normalized-iso.png'),
      'Faceted oblique',
      'neutral authoring material',
    ),
    reviewPanel(
      sharp,
      path.join(review, 'gameplay-neutral.png'),
      '58° gameplay camera',
      'neutral',
    ),
    reviewPanel(
      sharp,
      path.join(review, 'gameplay-cyan.png'),
      'Team tint proof',
      'cyan',
    ),
    reviewPanel(
      sharp,
      path.join(review, 'gameplay-red.png'),
      'Team tint proof',
      'red',
    ),
  ]);
  await writeContactSheet(
    sharp,
    path.join(review, 'contact-sheet.png'),
    panels,
  );
  return report;
}

async function finalAudit(args, sharp) {
  const comparisonDirectory = path.join(
    args.repoRoot,
    'art/class-models/provider-runs/meshy/projectile-master-audit',
  );
  await mkdir(comparisonDirectory, { recursive: true });
  const summaries = [];
  const gameplayColumns = [];

  for (const projectile of PROJECTILES) {
    const directory = auditDirectory(args, projectile);
    const svgPath = path.join(
      args.repoRoot,
      'web/src/assets/class-projectile-looks',
      projectile.assetId,
      'sprite.svg',
    );
    const reference = normalizeLongestSpan(
      await authorityMask(sharp, svgPath),
    );
    const candidate = normalizeLongestSpan(
      await alphaMask(sharp, path.join(directory, 'top-mask.png')),
    );
    const topMetrics = bestTranslation(reference.mask, candidate.mask);
    const alignedTop = shifted(
      candidate.mask,
      topMetrics.dx,
      topMetrics.dy,
    );
    await writeOverlay(
      sharp,
      path.join(directory, 'top-overlay.png'),
      reference.mask,
      alignedTop,
    );

    const cleanSidePath = path.join(
      args.repoRoot,
      'art/class-models/provider-inputs/meshy',
      projectile.assetId,
      'clean-v1/02-side.png',
    );
    const cleanSideRaw = await backgroundSeparatedMask(sharp, cleanSidePath);
    const modelSideRaw = await alphaMask(
      sharp,
      path.join(directory, 'side-mask.png'),
    );
    const cleanSide = normalizeLongestSpan(cleanSideRaw);
    const modelSide = normalizeLongestSpan(modelSideRaw);
    const sideMetrics = bestTranslation(
      cleanSide.mask,
      modelSide.mask,
    );
    const alignedSide = shifted(
      modelSide.mask,
      sideMetrics.dx,
      sideMetrics.dy,
    );
    await writeOverlay(
      sharp,
      path.join(directory, 'side-overlay.png'),
      cleanSide.mask,
      alignedSide,
    );
    const cleanSideBounds = boundingBox(cleanSideRaw);
    const modelSideBounds = boundingBox(modelSideRaw);
    const referenceDepthRatio =
      cleanSideBounds.height / cleanSideBounds.width;
    const modelDepthRatio =
      modelSideBounds.height / modelSideBounds.width;

    const blender = JSON.parse(
      await readFile(
        path.join(directory, 'render-blender-report.json'),
        'utf8',
      ),
    );
    const providerRunDirectory = path.dirname(directory);
    const requestRecord = JSON.parse(
      await readFile(
        path.join(providerRunDirectory, 'request-record.json'),
        'utf8',
      ),
    );
    const recordedMaster = requestRecord.artifacts.find(
      (artifact) => artifact.file === 'raw-model.glb',
    );
    if (
      !recordedMaster ||
      recordedMaster.sha256 !== blender.source.sha256 ||
      recordedMaster.bytes !== blender.source.bytes
    ) {
      throw new Error(
        `${projectile.assetId}: durable master does not match recorded provider artifact.`,
      );
    }
    const sourceDirectory = path.join(
      args.artifactsRoot,
      projectile.assetId,
      projectile.taskId,
    );
    const baseColorPath = path.join(
      sourceDirectory,
      'texture-0-base-color.png',
    );
    const emissionPath = path.join(
      sourceDirectory,
      'texture-0-emission.png',
    );
    const report = {
      ...blender,
      provenance: {
        requestRecord:
          path.relative(
            args.repoRoot,
            path.join(providerRunDirectory, 'request-record.json'),
          ),
        recordedBytes: recordedMaster.bytes,
        recordedSha256: recordedMaster.sha256,
        durableMasterMatchesRecord: true,
        providerThumbnailUsedForVerdict: false,
      },
      silhouette: {
        normalization:
          'fixed source-up correction and inspected yaw; longest-planform-span normalization; uniform scale and translation only',
        sourceFacing: projectile.sourceFacing,
        topIoUToRuntimeSvg: topMetrics.iou,
        topCoverage: topMetrics.coverage,
        topPrecision: topMetrics.precision,
        topTranslationPixels: [topMetrics.dx, topMetrics.dy],
        authorityApertures: apertures(reference.mask),
        modelApertures: apertures(alignedTop),
        sideIoUToApprovedCleanInputApprox: sideMetrics.iou,
        cleanSideDepthToLength: referenceDepthRatio,
        modelSideDepthToLength: modelDepthRatio,
        sideDepthDrift:
          modelDepthRatio / referenceDepthRatio - 1,
      },
      textureColor: {
        baseColor: await textureColorStats(sharp, baseColorPath),
        emission: await textureColorStats(sharp, emissionPath),
        verdict:
          'broadly team-neutral source palette; not runtime-tintable until projectile-mask semantics and both-team review are authored',
      },
      gameplayReadability: {
        cameraPitchDegrees: 58,
        cameraFovDegrees: 42,
        assumedPixelsPerTile: 50,
        effectivePlanformSpanTiles: projectile.lookScale * 0.72,
        expectedPlanformPixelsAtAssumption:
          projectile.lookScale * 0.72 * 50,
        comparison:
          'projectile-master-audit/gameplay-scale-comparison.png',
      },
      verdict: projectile.verdict,
      runtimeReady: false,
      requiredBlenderWork: projectile.rebuild,
    };
    await writeFile(
      path.join(directory, 'audit-report.json'),
      `${JSON.stringify(report, null, 2)}\n`,
    );

    const conceptPath = path.join(
      args.repoRoot,
      'art/class-models/concept-targets/projectiles',
      `${projectile.assetId}-3d-concept-v1/approval-sheet.png`,
    );
    const panels = await Promise.all([
      reviewPanel(
        sharp,
        conceptPath,
        'Owner-approved 3D concept',
        'depth/material authority',
      ),
      reviewPanel(
        sharp,
        svgPath,
        'Runtime SVG',
        'exact top authority',
      ),
      reviewPanel(
        sharp,
        path.join(directory, 'normalized-top.png'),
        'Normalized Blender top',
        `${(topMetrics.iou * 100).toFixed(2)}% IoU`,
      ),
      reviewPanel(
        sharp,
        path.join(directory, 'top-overlay.png'),
        'Exact top overlay',
        'white overlap · red model · cyan SVG',
      ),
      reviewPanel(
        sharp,
        cleanSidePath,
        'Approved clean side input',
        'depth/profile reference',
      ),
      reviewPanel(
        sharp,
        path.join(directory, 'normalized-side.png'),
        'Normalized Blender side',
        `${(report.silhouette.sideDepthDrift * 100).toFixed(1)}% depth drift`,
      ),
      reviewPanel(
        sharp,
        path.join(directory, 'normalized-iso.png'),
        'Normalized Blender oblique',
        'raw GLB, not provider thumbnail',
      ),
      reviewPanel(
        sharp,
        path.join(directory, 'gameplay-camera.png'),
        '58° gameplay camera',
        `${report.gameplayReadability.expectedPlanformPixelsAtAssumption.toFixed(1)} px at 50 px/tile`,
      ),
    ]);
    await writeContactSheet(
      sharp,
      path.join(directory, 'contact-sheet.png'),
      panels,
    );
    summaries.push({
      assetId: projectile.assetId,
      label: projectile.label,
      taskId: projectile.taskId,
      sourceSha256: blender.source.sha256,
      topIoU: topMetrics.iou,
      apertureCount: apertures(alignedTop).count,
      sideDepthDrift: report.silhouette.sideDepthDrift,
      weldedComponents: blender.topology.weldedComponents,
      triangles: blender.geometry.triangles,
      materials: blender.materials.count,
      hasProjectileMaskMaterial:
        blender.materials.hasProjectileMaskMaterial,
      verdict: projectile.verdict,
      runtimeReady: false,
    });
    gameplayColumns.push(
      await gameplayComparisonColumn(sharp, {
        label: projectile.label,
        detail: projectile.comparisonDetail,
        input: path.join(directory, 'gameplay-camera.png'),
        livePixels: Math.round(projectile.lookScale * 0.72 * 50),
      }),
    );
  }

  const pulseReport = await pulseControlAudit(args, sharp);
  gameplayColumns.unshift(
    await gameplayComparisonColumn(sharp, {
      label: 'Pulse Bolt · procedural control',
      detail: 'art-side only · canonical existing look',
      input: path.join(
        args.repoRoot,
        'art/class-models/controls/pulse-bolt-3d/review/gameplay-neutral.png',
      ),
      livePixels: Math.round(0.54 * 0.72 * 50),
    }),
  );
  await sharp({
    create: {
      width: gameplayColumns.length * 400,
      height: 900,
      channels: 4,
      background: '#0d1218',
    },
  })
    .composite(
      gameplayColumns.map((input, index) => ({
        input,
        left: index * 400,
        top: 0,
      })),
    )
    .png()
    .toFile(
      path.join(comparisonDirectory, 'gameplay-scale-comparison.png'),
    );
  const comparison = {
    schemaVersion: 1,
    blender: {
      version: '5.2.0',
      binary:
        'var/toolchains/Blender-5.2.0.app/Contents/MacOS/Blender',
    },
    sourcePolicy:
      'raw provider masters preserved; provider thumbnails are provenance only',
    normalization:
      'per-master inspected source facing; +X target, +Y target up; one-tile authored planform; no non-uniform warp',
    gameplayScaleEvidence: {
      assumedPixelsPerTile: 50,
      rendererProjectileFactor: 0.72,
      file: 'gameplay-scale-comparison.png',
      tintTreatment:
        'grayscale multiply proxy used equally for all four heads; runtime still requires real both-team replay review',
    },
    mechanicalMasters: summaries,
    pulseBoltControl: pulseReport,
    paidCallsMade: 0,
  };
  await writeFile(
    path.join(comparisonDirectory, 'comparison-report.json'),
    `${JSON.stringify(comparison, null, 2)}\n`,
  );
  for (const summary of summaries)
    console.log(
      `${summary.assetId}: top ${(summary.topIoU * 100).toFixed(2)}%, ` +
        `side drift ${(summary.sideDepthDrift * 100).toFixed(1)}%, ` +
        `${summary.weldedComponents} welded component(s), ${summary.verdict}`,
    );
  console.log(
    `pulse-bolt control: top ${(pulseReport.silhouette.iou * 100).toFixed(2)}%, semantic mask ${pulseReport.glbStructure.everyMaterialIsProjectileMask}`,
  );
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const requireFromWeb = createRequire(
    path.join(args.repoRoot, 'web/package.json'),
  );
  const sharp = requireFromWeb('sharp');
  if (args.phase === 'probe') await probe(args, sharp);
  else await finalAudit(args, sharp);
}

await main();
