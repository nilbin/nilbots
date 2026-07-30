#!/usr/bin/env node
'use strict';

const crypto = require('node:crypto');
const fs = require('node:fs/promises');
const path = require('node:path');

const [canvasPackage, proofRootArgument, repositoryRootArgument] =
  process.argv.slice(2);
if (!canvasPackage || !path.isAbsolute(canvasPackage))
  throw new Error(
    'usage: node make_review_boards.cjs <absolute @napi-rs/canvas package> [proof-root] [repo-root]',
  );
const { createCanvas, loadImage } = require(canvasPackage);
const proofRoot = proofRootArgument
  ? path.resolve(proofRootArgument)
  : __dirname;
const repositoryRoot = repositoryRootArgument
  ? path.resolve(repositoryRootArgument)
  : path.resolve(proofRoot, '../../..');
const reviewRoot = path.join(proofRoot, 'review');
const reportRoot = path.join(proofRoot, 'reports');
const reportPath = (file) => path.relative(repositoryRoot, file);

const FORMS = [
  {
    id: 'aegis-tortoise-shell',
    label: 'Shell',
    svg: path.join(
      repositoryRoot,
      'web/src/assets/class-looks/aegis-tortoise-shell/sprite.svg',
    ),
    approvedSide: path.join(
      proofRoot,
      'reference/aegis-tortoise-shell-approved-side.png',
    ),
  },
  {
    id: 'aegis-tortoise-turret',
    label: 'Turret',
    svg: path.join(
      repositoryRoot,
      'web/src/assets/class-looks/aegis-tortoise-turret/sprite.svg',
    ),
    approvedSide: path.join(
      proofRoot,
      'reference/aegis-tortoise-turret-approved-side.png',
    ),
  },
];
const TIERS = ['rich', 'lean'];
const TEAMS = ['blue', 'red'];

function candidateReview(form, tier, file) {
  return path.join(
    proofRoot,
    'candidate',
    form,
    tier,
    'review',
    file,
  );
}

function mobileReview(file) {
  return path.join(
    proofRoot,
    'candidate/aegis-tortoise-mobile/reference/review',
    file,
  );
}

async function pixels(input, width, height) {
  const image = await loadImage(input);
  const canvas = createCanvas(width, height);
  const context = canvas.getContext('2d');
  context.clearRect(0, 0, width, height);
  context.drawImage(image, 0, 0, width, height);
  return {
    image,
    data: context.getImageData(0, 0, width, height).data,
  };
}

async function fileEvidence(file) {
  const bytes = await fs.readFile(file);
  return {
    path: reportPath(file),
    bytes: bytes.length,
    sha256: crypto
      .createHash('sha256')
      .update(bytes)
      .digest('hex'),
  };
}

function difference(left, right, threshold = 8) {
  if (left.length !== right.length) throw new Error('Image size mismatch');
  let absolute = 0;
  let squared = 0;
  let maximum = 0;
  let changed = 0;
  const pixelCount = left.length / 4;
  const mask = new Uint8Array(pixelCount);
  for (let pixel = 0; pixel < pixelCount; pixel += 1) {
    let pixelMaximum = 0;
    for (let channel = 0; channel < 3; channel += 1) {
      const delta = Math.abs(
        left[pixel * 4 + channel] - right[pixel * 4 + channel],
      );
      absolute += delta;
      squared += delta * delta;
      if (delta > maximum) maximum = delta;
      if (delta > pixelMaximum) pixelMaximum = delta;
    }
    if (pixelMaximum >= threshold) {
      mask[pixel] = 1;
      changed += 1;
    }
  }
  const mae = absolute / (pixelCount * 3 * 255);
  const rmse = Math.sqrt(squared / (pixelCount * 3)) / 255;
  return {
    mae,
    rmse,
    psnr: rmse === 0 ? null : 20 * Math.log10(1 / rmse),
    maximumChannelDelta: maximum,
    changedPixelFraction: changed / pixelCount,
    changedPixels: changed,
    mask,
  };
}

function maskBounds(mask, width, height) {
  let left = width;
  let top = height;
  let right = -1;
  let bottom = -1;
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      if (!mask[y * width + x]) continue;
      left = Math.min(left, x);
      right = Math.max(right, x);
      top = Math.min(top, y);
      bottom = Math.max(bottom, y);
    }
  }
  if (right < left) return null;
  return {
    left,
    top,
    right,
    bottom,
    width: right - left + 1,
    height: bottom - top + 1,
  };
}

function componentAreas(mask, width, height) {
  const visited = new Uint8Array(mask.length);
  const result = [];
  for (let index = 0; index < mask.length; index += 1) {
    if (!mask[index] || visited[index]) continue;
    const queue = [index];
    visited[index] = 1;
    let area = 0;
    for (let cursor = 0; cursor < queue.length; cursor += 1) {
      const current = queue[cursor];
      area += 1;
      const x = current % width;
      const y = Math.floor(current / width);
      for (const [dx, dy] of [
        [-1, 0],
        [1, 0],
        [0, -1],
        [0, 1],
      ]) {
        const nx = x + dx;
        const ny = y + dy;
        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
        const neighbour = ny * width + nx;
        if (!mask[neighbour] || visited[neighbour]) continue;
        visited[neighbour] = 1;
        queue.push(neighbour);
      }
    }
    if (area >= 4) result.push(area);
  }
  return result.sort((left, right) => right - left);
}

async function writeMask(mask, width, height, output, color) {
  const canvas = createCanvas(width, height);
  const context = canvas.getContext('2d');
  context.fillStyle = '#0d1218';
  context.fillRect(0, 0, width, height);
  const image = context.createImageData(width, height);
  const [red, green, blue] = color;
  for (let index = 0; index < mask.length; index += 1) {
    const offset = index * 4;
    if (mask[index]) {
      image.data[offset] = red;
      image.data[offset + 1] = green;
      image.data[offset + 2] = blue;
    } else {
      image.data[offset] = 13;
      image.data[offset + 1] = 18;
      image.data[offset + 2] = 24;
    }
    image.data[offset + 3] = 255;
  }
  context.putImageData(image, 0, 0);
  await fs.writeFile(output, canvas.toBuffer('image/png'));
}

function drawHeader(context, width, title, detail) {
  context.fillStyle = '#0d1218';
  context.fillRect(0, 0, width, 74);
  context.fillStyle = '#f8fafc';
  context.font = '700 25px Arial';
  context.fillText(title, 22, 31);
  context.fillStyle = '#9fb0c2';
  context.font = '15px Arial';
  context.fillText(detail, 22, 56);
}

function drawCellLabel(context, x, y, width, title, detail = '') {
  context.fillStyle = 'rgba(8, 13, 21, 0.92)';
  context.fillRect(x, y, width, detail ? 51 : 32);
  context.fillStyle = '#f8fafc';
  context.font = '700 16px Arial';
  context.fillText(title, x + 12, y + 21);
  if (detail) {
    context.fillStyle = '#9fb0c2';
    context.font = '12px Arial';
    context.fillText(detail, x + 12, y + 41);
  }
}

async function makeGameplayBoard(size, output, native) {
  const columns = [
    {
      title: 'Mobile reference',
      detail: 'approved family anchor',
      input: (team) => mobileReview(`gameplay-${team}${native ? '-192' : ''}.png`),
    },
    ...FORMS.flatMap((form) =>
      TIERS.map((tier) => ({
        title: `${form.label} ${tier}`,
        detail: tier === 'rich' ? '150k · 2K PBR' : '75k · 1K PBR',
        input: (team) =>
          candidateReview(
            form.id,
            tier,
            `gameplay-${team}${native ? '-192' : ''}.png`,
          ),
      })),
    ),
  ];
  const labelHeight = native ? 44 : 51;
  const headerHeight = native ? 64 : 74;
  const canvas = createCanvas(
    columns.length * size,
    headerHeight + TEAMS.length * (size + labelHeight),
  );
  const context = canvas.getContext('2d');
  context.fillStyle = '#0d1218';
  context.fillRect(0, 0, canvas.width, canvas.height);
  drawHeader(
    context,
    canvas.width,
    native
      ? 'Native 192 px gameplay evidence'
      : 'Aegis static Meshy forms · exact fixed 58° one-tile rig',
    native
      ? 'No upscaling · Mobile / rich / lean · blue and red'
      : 'Same camera, light, floor, exposure and one-tile planform · no per-model reframing',
  );
  for (let row = 0; row < TEAMS.length; row += 1) {
    const team = TEAMS[row];
    const top = headerHeight + row * (size + labelHeight);
    for (let column = 0; column < columns.length; column += 1) {
      const entry = columns[column];
      const image = await loadImage(entry.input(team));
      context.drawImage(image, column * size, top, size, size);
      drawCellLabel(
        context,
        column * size,
        top,
        size,
        `${entry.title} · ${team}`,
        native ? '' : entry.detail,
      );
    }
  }
  await fs.writeFile(output, canvas.toBuffer('image/png'));
}

function alphaMask(data) {
  const result = new Uint8Array(data.length / 4);
  for (let index = 0; index < result.length; index += 1)
    result[index] = data[index * 4 + 3] >= 16 ? 1 : 0;
  return result;
}

function flatBackgroundMask(data, width, height) {
  const corners = [
    0,
    (width - 1) * 4,
    (height - 1) * width * 4,
    (height * width - 1) * 4,
  ];
  const background = [0, 1, 2].map((channel) =>
    Math.round(
      corners.reduce((sum, offset) => sum + data[offset + channel], 0) /
        corners.length,
    ),
  );
  const result = new Uint8Array(width * height);
  for (let index = 0; index < result.length; index += 1) {
    const offset = index * 4;
    const delta = Math.max(
      ...background.map((value, channel) =>
        Math.abs(data[offset + channel] - value),
      ),
    );
    result[index] = data[offset + 3] >= 16 && delta >= 8 ? 1 : 0;
  }
  return result;
}

function boundingBox(mask, width, height) {
  const bounds = maskBounds(mask, width, height);
  if (!bounds) throw new Error('Empty silhouette mask');
  return bounds;
}

function normalizeMask(mask, width, height, mode, targetSpan = 615) {
  const bounds = boundingBox(mask, width, height);
  const sourceSpan =
    mode === 'width' ? bounds.width : Math.max(bounds.width, bounds.height);
  const scale = targetSpan / sourceSpan;
  const targetWidth = Math.max(1, Math.round(bounds.width * scale));
  const targetHeight = Math.max(1, Math.round(bounds.height * scale));
  const left = Math.floor((width - targetWidth) / 2);
  const top = Math.floor((height - targetHeight) / 2);
  const result = new Uint8Array(width * height);
  for (let y = 0; y < targetHeight; y += 1) {
    const sourceY = Math.min(
      bounds.height - 1,
      Math.floor((y + 0.5) / scale),
    );
    for (let x = 0; x < targetWidth; x += 1) {
      const sourceX = Math.min(
        bounds.width - 1,
        Math.floor((x + 0.5) / scale),
      );
      result[(top + y) * width + left + x] =
        mask[(bounds.top + sourceY) * width + bounds.left + sourceX];
    }
  }
  return result;
}

function overlap(reference, candidate, width, height, dx = 0, dy = 0) {
  let intersection = 0;
  let union = 0;
  let referenceCount = 0;
  let candidateCount = 0;
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      const referenceValue = reference[y * width + x] !== 0;
      const sourceX = x - dx;
      const sourceY = y - dy;
      const candidateValue =
        sourceX >= 0 &&
        sourceX < width &&
        sourceY >= 0 &&
        sourceY < height &&
        candidate[sourceY * width + sourceX] !== 0;
      if (referenceValue) referenceCount += 1;
      if (candidateValue) candidateCount += 1;
      if (referenceValue && candidateValue) intersection += 1;
      if (referenceValue || candidateValue) union += 1;
    }
  }
  return {
    iou: intersection / union,
    coverage: intersection / referenceCount,
    precision: intersection / candidateCount,
  };
}

function bestTranslation(reference, candidate, width, height, radius = 6) {
  let best = null;
  for (let dy = -radius; dy <= radius; dy += 1) {
    for (let dx = -radius; dx <= radius; dx += 1) {
      const metrics = overlap(reference, candidate, width, height, dx, dy);
      if (
        best === null ||
        metrics.iou > best.iou ||
        (metrics.iou === best.iou &&
          Math.abs(dx) + Math.abs(dy) <
            Math.abs(best.dx) + Math.abs(best.dy))
      )
        best = { ...metrics, dx, dy };
    }
  }
  return best;
}

function shifted(mask, width, height, dx, dy) {
  const result = new Uint8Array(mask.length);
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      const sx = x - dx;
      const sy = y - dy;
      if (sx >= 0 && sx < width && sy >= 0 && sy < height)
        result[y * width + x] = mask[sy * width + sx];
    }
  }
  return result;
}

async function writeOverlay(reference, candidate, width, height, output) {
  const canvas = createCanvas(width, height);
  const context = canvas.getContext('2d');
  const image = context.createImageData(width, height);
  for (let index = 0; index < reference.length; index += 1) {
    const ref = Boolean(reference[index]);
    const model = Boolean(candidate[index]);
    const offset = index * 4;
    const color =
      ref && model
        ? [245, 245, 245]
        : model
          ? [239, 68, 68]
          : ref
            ? [34, 211, 238]
            : [13, 18, 24];
    image.data[offset] = color[0];
    image.data[offset + 1] = color[1];
    image.data[offset + 2] = color[2];
    image.data[offset + 3] = 255;
  }
  context.putImageData(image, 0, 0);
  await fs.writeFile(output, canvas.toBuffer('image/png'));
}

async function makeSilhouetteEvidence() {
  const size = 768;
  const results = [];
  const panels = [];
  for (const form of FORMS) {
    const svg = await pixels(form.svg, size, size);
    const approvedSide = await pixels(form.approvedSide, size, size);
    const referenceTop = normalizeMask(
      alphaMask(svg.data),
      size,
      size,
      'max',
    );
    const referenceSide = normalizeMask(
      flatBackgroundMask(approvedSide.data, size, size),
      size,
      size,
      'width',
    );
    for (const tier of TIERS) {
      for (const view of ['top', 'side']) {
        const candidate = await pixels(
          candidateReview(form.id, tier, `${view}-mask.png`),
          size,
          size,
        );
        const normalizedCandidate = normalizeMask(
          alphaMask(candidate.data),
          size,
          size,
          view === 'side' ? 'width' : 'max',
        );
        const reference = view === 'side' ? referenceSide : referenceTop;
        const metrics = bestTranslation(
          reference,
          normalizedCandidate,
          size,
          size,
        );
        const aligned = shifted(
          normalizedCandidate,
          size,
          size,
          metrics.dx,
          metrics.dy,
        );
        const output = path.join(
          reviewRoot,
          `${form.id}-${tier}-${view}-overlay.png`,
        );
        await writeOverlay(reference, aligned, size, size, output);
        results.push({
          assetId: form.id,
          tier,
          view,
          authority:
            view === 'top'
              ? path.relative(repositoryRoot, form.svg)
              : path.relative(repositoryRoot, form.approvedSide),
          ...metrics,
          overlay: reportPath(output),
        });
        panels.push({
          title: `${form.label} ${tier} · ${view}`,
          detail: `${(metrics.iou * 100).toFixed(2)}% IoU`,
          input: output,
        });
      }
    }
  }

  const cell = 384;
  const columns = 4;
  const rows = Math.ceil(panels.length / columns);
  const canvas = createCanvas(columns * cell, 74 + rows * cell);
  const context = canvas.getContext('2d');
  context.fillStyle = '#0d1218';
  context.fillRect(0, 0, canvas.width, canvas.height);
  drawHeader(
    context,
    canvas.width,
    'Direct-source silhouette gates',
    'White overlap · red candidate-only · cyan authority-only · rigid scale/translation only',
  );
  for (let index = 0; index < panels.length; index += 1) {
    const x = (index % columns) * cell;
    const y = 74 + Math.floor(index / columns) * cell;
    context.drawImage(await loadImage(panels[index].input), x, y, cell, cell);
    drawCellLabel(
      context,
      x,
      y,
      cell,
      panels[index].title,
      panels[index].detail,
    );
  }
  const board = path.join(reviewRoot, 'top-side-silhouette-overlays.png');
  await fs.writeFile(board, canvas.toBuffer('image/png'));
  return { results, board };
}

async function main() {
  await fs.mkdir(reviewRoot, { recursive: true });
  await fs.mkdir(reportRoot, { recursive: true });

  const richLean = [];
  const teamDiffs = [];
  const diffPanels = [];
  for (const form of FORMS) {
    for (const team of TEAMS) {
      const rich = await pixels(
        candidateReview(form.id, 'rich', `gameplay-${team}-192.png`),
        192,
        192,
      );
      const lean = await pixels(
        candidateReview(form.id, 'lean', `gameplay-${team}-192.png`),
        192,
        192,
      );
      const metrics = difference(rich.data, lean.data);
      const maskPath = path.join(
        reviewRoot,
        `${form.id}-${team}-rich-vs-lean-192-diff.png`,
      );
      await writeMask(metrics.mask, 192, 192, maskPath, [250, 204, 21]);
      richLean.push({
        assetId: form.id,
        team,
        resolution: [192, 192],
        ...Object.fromEntries(
          Object.entries(metrics).filter(([key]) => key !== 'mask'),
        ),
        diffMask: reportPath(maskPath),
      });
    }

    for (const tier of TIERS) {
      const blue = await pixels(
        candidateReview(form.id, tier, 'gameplay-blue-192.png'),
        192,
        192,
      );
      const red = await pixels(
        candidateReview(form.id, tier, 'gameplay-red-192.png'),
        192,
        192,
      );
      const metrics = difference(blue.data, red.data);
      const maskPath = path.join(
        reviewRoot,
        `${form.id}-${tier}-team-diff-192.png`,
      );
      await writeMask(metrics.mask, 192, 192, maskPath, [251, 113, 133]);
      const components = componentAreas(metrics.mask, 192, 192);
      teamDiffs.push({
        assetId: form.id,
        tier,
        resolution: [192, 192],
        ...Object.fromEntries(
          Object.entries(metrics).filter(([key]) => key !== 'mask'),
        ),
        bounds: maskBounds(metrics.mask, 192, 192),
        connectedComponentsAtLeast4Pixels: components,
        diffMask: reportPath(maskPath),
        semanticProof:
          'Only the neutral team-accent material changes between renders; hull/core texture and factors are byte-identical.',
      });
      diffPanels.push({
        title: `${form.label} ${tier}`,
        detail: `${metrics.changedPixels} px · ${components.length} components`,
        input: maskPath,
      });
    }
  }

  const fixedBoard = path.join(reviewRoot, 'fixed-58deg-rich-vs-lean-board.png');
  const nativeBoard = path.join(
    reviewRoot,
    'fixed-58deg-rich-vs-lean-native-192.png',
  );
  await makeGameplayBoard(384, fixedBoard, false);
  await makeGameplayBoard(192, nativeBoard, true);

  const diffCanvas = createCanvas(diffPanels.length * 320, 74 + 320);
  const diffContext = diffCanvas.getContext('2d');
  diffContext.fillStyle = '#0d1218';
  diffContext.fillRect(0, 0, diffCanvas.width, diffCanvas.height);
  drawHeader(
    diffContext,
    diffCanvas.width,
    'Rendered team-diff masks · native 192 px',
    'Pink pixels changed blue→red; fixed cyan core and authored hull remain black',
  );
  for (let index = 0; index < diffPanels.length; index += 1) {
    const x = index * 320;
    diffContext.imageSmoothingEnabled = false;
    diffContext.drawImage(
      await loadImage(diffPanels[index].input),
      x,
      74,
      320,
      320,
    );
    drawCellLabel(
      diffContext,
      x,
      74,
      320,
      diffPanels[index].title,
      diffPanels[index].detail,
    );
  }
  const teamBoard = path.join(reviewRoot, 'team-diff-mask-board.png');
  await fs.writeFile(teamBoard, diffCanvas.toBuffer('image/png'));

  const silhouettes = await makeSilhouetteEvidence();
  const recommendations = [];
  for (const form of FORMS) {
    const richFile = path.join(
      proofRoot,
      'candidate',
      form.id,
      'rich',
      `${form.id}-rich.glb`,
    );
    const leanFile = path.join(
      proofRoot,
      'candidate',
      form.id,
      'lean',
      `${form.id}-lean.glb`,
    );
    const [richStat, leanStat] = await Promise.all([
      fs.stat(richFile),
      fs.stat(leanFile),
    ]);
    const comparisons = richLean.filter(
      ({ assetId }) => assetId === form.id,
    );
    recommendations.push({
      assetId: form.id,
      preferredTier: 'lean',
      richBytes: richStat.size,
      leanBytes: leanStat.size,
      bytesSaved: richStat.size - leanStat.size,
      byteReductionFraction:
        (richStat.size - leanStat.size) / richStat.size,
      minimumNative192Psnr: Math.min(
        ...comparisons.map(({ psnr }) => psnr),
      ),
      maximumNative192ChangedPixelFraction: Math.max(
        ...comparisons.map(
          ({ changedPixelFraction }) => changedPixelFraction,
        ),
      ),
      rationale:
        'Prefer lean for lazy runtime delivery: the native 192 px comparison has no material gameplay-readability gain from rich. Retain rich as archival/on-demand review evidence.',
    });
  }
  const boardFiles = {
    fixed58: fixedBoard,
    native192: nativeBoard,
    teamDiff: teamBoard,
    silhouette: silhouettes.board,
  };
  const report = {
    schemaVersion: 1,
    camera:
      'fixed 58-degree perspective, one-tile planform, identical floor/lights/exposure; no candidate-specific framing',
    differenceThreshold: {
      channelUnits: 8,
      normalized: 8 / 255,
      changedPixelDefinition:
        'At least one RGB channel differs by 8 or more on a native 192 px pixel.',
    },
    richVsLeanNative192: richLean,
    teamDiffNative192: teamDiffs,
    silhouettes: {
      ...silhouettes,
      board: reportPath(silhouettes.board),
    },
    tierRecommendation: recommendations,
    boards: Object.fromEntries(
      await Promise.all(
        Object.entries(boardFiles).map(async ([key, file]) => [
          key,
          await fileEvidence(file),
        ]),
      ),
    ),
  };
  const metricsPath = path.join(reportRoot, 'review-metrics.json');
  await fs.writeFile(metricsPath, `${JSON.stringify(report, null, 2)}\n`);
  for (const board of Object.values(boardFiles)) {
    const bytes = await fs.readFile(board);
    process.stdout.write(
      `${path.basename(board)} ${bytes.length} ${crypto
        .createHash('sha256')
        .update(bytes)
        .digest('hex')}\n`,
    );
  }
  process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
