import { readFile, writeFile } from "node:fs/promises";
import { createRequire } from "node:module";
import { join, resolve } from "node:path";

const repoRoot = resolve(import.meta.dirname, "../../..");
const requireFromWeb = createRequire(join(repoRoot, "web/package.json"));
const sharp = requireFromWeb("sharp");

const analysisSize = 512;
const flatBackground = { r: 184, g: 184, b: 184 };
const packages = [
  {
    id: "aegis-tortoise-shell",
    root: "art/class-models/provider-inputs/meshy/aegis-tortoise-shell/clean-v1",
    authority:
      "web/src/assets/class-looks/aegis-tortoise-shell/sprite.svg",
    previousIou: 0.8237,
    expectedTopCyanComponents: 5,
  },
  {
    id: "aegis-tortoise-turret",
    root: "art/class-models/provider-inputs/meshy/aegis-tortoise-turret/clean-v1",
    authority:
      "web/src/assets/class-looks/aegis-tortoise-turret/sprite.svg",
    previousIou: 0.8016,
    expectedTopCyanComponents: 5,
  },
];

function bounds(mask, width, height) {
  let minX = width;
  let minY = height;
  let maxX = -1;
  let maxY = -1;
  let area = 0;
  let sumX = 0;
  let sumY = 0;
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      if (!mask[y * width + x]) continue;
      minX = Math.min(minX, x);
      minY = Math.min(minY, y);
      maxX = Math.max(maxX, x);
      maxY = Math.max(maxY, y);
      area += 1;
      sumX += x;
      sumY += y;
    }
  }
  if (!area) throw new Error("Empty foreground mask");
  return {
    minX,
    minY,
    maxX,
    maxY,
    width: maxX - minX + 1,
    height: maxY - minY + 1,
    area,
    centroid: { x: sumX / area, y: sumY / area },
  };
}

function components(mask, width, height) {
  const seen = new Uint8Array(mask.length);
  const queue = new Int32Array(mask.length);
  const output = [];
  for (let start = 0; start < mask.length; start += 1) {
    if (!mask[start] || seen[start]) continue;
    let head = 0;
    let tail = 0;
    const points = [];
    seen[start] = 1;
    queue[tail++] = start;
    while (head < tail) {
      const current = queue[head++];
      points.push(current);
      const x = current % width;
      const y = Math.floor(current / width);
      for (let oy = -1; oy <= 1; oy += 1) {
        for (let ox = -1; ox <= 1; ox += 1) {
          if (!ox && !oy) continue;
          const nx = x + ox;
          const ny = y + oy;
          if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
          const next = ny * width + nx;
          if (!mask[next] || seen[next]) continue;
          seen[next] = 1;
          queue[tail++] = next;
        }
      }
    }
    output.push(points);
  }
  return output.sort((left, right) => right.length - left.length);
}

function foregroundMask(data, width, height, channels) {
  const raw = new Uint8Array(width * height);
  for (let index = 0; index < raw.length; index += 1) {
    const offset = index * channels;
    const dr = data[offset] - flatBackground.r;
    const dg = data[offset + 1] - flatBackground.g;
    const db = data[offset + 2] - flatBackground.b;
    raw[index] = Math.sqrt(dr * dr + dg * dg + db * db) > 12 ? 1 : 0;
  }
  const found = components(raw, width, height);
  const largest = found[0];
  if (!largest) throw new Error("No foreground components");
  const retained = new Uint8Array(raw.length);
  const largestMask = new Uint8Array(raw.length);
  for (const point of largest) largestMask[point] = 1;
  const envelope = bounds(largestMask, width, height);
  for (const component of found) {
    if (component.length < Math.max(80, largest.length * 0.0005)) continue;
    let minX = width;
    let minY = height;
    let maxX = -1;
    let maxY = -1;
    for (const point of component) {
      const x = point % width;
      const y = Math.floor(point / width);
      minX = Math.min(minX, x);
      minY = Math.min(minY, y);
      maxX = Math.max(maxX, x);
      maxY = Math.max(maxY, y);
    }
    const nearEnvelope =
      maxX >= envelope.minX - 24 &&
      minX <= envelope.maxX + 24 &&
      maxY >= envelope.minY - 24 &&
      minY <= envelope.maxY + 24;
    if (!nearEnvelope) continue;
    for (const point of component) retained[point] = 1;
  }
  return retained;
}

async function referenceMask(path) {
  const { data, info } = await sharp(path)
    .resize(analysisSize, analysisSize, { fit: "fill" })
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const mask = new Uint8Array(analysisSize * analysisSize);
  for (let index = 0; index < mask.length; index += 1) {
    mask[index] = data[index * info.channels + 3] >= 128 ? 1 : 0;
  }
  return mask;
}

function transformMask(source, sourceWidth, sourceHeight, transform) {
  const output = new Uint8Array(analysisSize * analysisSize);
  const inverseScale = 1 / transform.scale;
  for (let y = 0; y < analysisSize; y += 1) {
    for (let x = 0; x < analysisSize; x += 1) {
      const sourceX = Math.round(
        (x - transform.centerX) * inverseScale + transform.sourceCenterX,
      );
      const sourceY = Math.round(
        (y - transform.centerY) * inverseScale + transform.sourceCenterY,
      );
      if (
        sourceX >= 0 &&
        sourceX < sourceWidth &&
        sourceY >= 0 &&
        sourceY < sourceHeight
      ) {
        output[y * analysisSize + x] =
          source[sourceY * sourceWidth + sourceX];
      }
    }
  }
  return output;
}

function fillHoles(mask, width, height) {
  const outside = new Uint8Array(mask.length);
  const queue = new Int32Array(mask.length);
  let head = 0;
  let tail = 0;
  const enqueue = (index) => {
    if (mask[index] || outside[index]) return;
    outside[index] = 1;
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
    const current = queue[head++];
    const x = current % width;
    const y = Math.floor(current / width);
    if (x > 0) enqueue(current - 1);
    if (x + 1 < width) enqueue(current + 1);
    if (y > 0) enqueue(current - width);
    if (y + 1 < height) enqueue(current + width);
  }
  const output = new Uint8Array(mask.length);
  for (let index = 0; index < output.length; index += 1) {
    output[index] = outside[index] ? 0 : 1;
  }
  return output;
}

function measure(reference, candidate) {
  let intersection = 0;
  let union = 0;
  let referenceArea = 0;
  let candidateArea = 0;
  for (let index = 0; index < reference.length; index += 1) {
    const ref = reference[index] === 1;
    const cand = candidate[index] === 1;
    if (ref) referenceArea += 1;
    if (cand) candidateArea += 1;
    if (ref && cand) intersection += 1;
    if (ref || cand) union += 1;
  }
  return {
    iou: intersection / union,
    referenceCoverage: intersection / referenceArea,
    candidateCoverage: intersection / candidateArea,
    areaDelta: candidateArea / referenceArea - 1,
  };
}

function optimiseRigid(candidate, width, height, reference) {
  const candidateBounds = bounds(candidate, width, height);
  const referenceBounds = bounds(reference, analysisSize, analysisSize);
  const widthScale = referenceBounds.width / candidateBounds.width;
  let best;
  for (let step = -25; step <= 25; step += 1) {
    const scale = widthScale * (1 + step * 0.002);
    const transformed = transformMask(candidate, width, height, {
      scale,
      sourceCenterX: candidateBounds.centroid.x,
      sourceCenterY: candidateBounds.centroid.y,
      centerX: referenceBounds.centroid.x,
      centerY: referenceBounds.centroid.y,
    });
    const metrics = measure(reference, transformed);
    if (!best || metrics.iou > best.metrics.iou) {
      best = { scale, transformed, metrics };
    }
  }
  best.metrics.outer = measure(
    fillHoles(reference, analysisSize, analysisSize),
    fillHoles(best.transformed, analysisSize, analysisSize),
  );
  return best;
}

async function saveOverlay(reference, candidate, path) {
  const rgba = Buffer.alloc(reference.length * 4);
  for (let index = 0; index < reference.length; index += 1) {
    const offset = index * 4;
    if (reference[index] && candidate[index]) {
      rgba[offset] = 218;
      rgba[offset + 1] = 225;
      rgba[offset + 2] = 232;
    } else if (reference[index]) {
      rgba[offset] = 255;
      rgba[offset + 1] = 72;
      rgba[offset + 2] = 139;
    } else if (candidate[index]) {
      rgba[offset] = 0;
      rgba[offset + 1] = 220;
      rgba[offset + 2] = 255;
    } else {
      rgba[offset] = 18;
      rgba[offset + 1] = 22;
      rgba[offset + 2] = 29;
    }
    rgba[offset + 3] = 255;
  }
  await sharp(rgba, {
    raw: { width: analysisSize, height: analysisSize, channels: 4 },
  })
    .png()
    .toFile(path);
}

function cyanRegionCount(data, width, height, channels) {
  const mask = new Uint8Array(width * height);
  for (let index = 0; index < mask.length; index += 1) {
    const offset = index * channels;
    const r = data[offset];
    const g = data[offset + 1];
    const b = data[offset + 2];
    mask[index] = b >= 150 && g >= 115 && b - r >= 45 ? 1 : 0;
  }
  return components(mask, width, height).filter(
    (component) => component.length >= 120,
  ).length;
}

async function inspectView(path) {
  const { data, info } = await sharp(path)
    .removeAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  if (info.width !== 1254 || info.height !== 1254 || info.channels !== 3) {
    throw new Error(
      `${path}: expected 1254x1254 RGB, got ${info.width}x${info.height}x${info.channels}`,
    );
  }
  const mask = foregroundMask(
    data,
    info.width,
    info.height,
    info.channels,
  );
  const foregroundBounds = bounds(mask, info.width, info.height);
  let nonFlatBorderPixels = 0;
  for (let y = 0; y < info.height; y += 1) {
    for (let x = 0; x < info.width; x += 1) {
      if (x >= 32 && x < info.width - 32 && y >= 32 && y < info.height - 32) {
        continue;
      }
      const offset = (y * info.width + x) * info.channels;
      if (
        data[offset] !== flatBackground.r ||
        data[offset + 1] !== flatBackground.g ||
        data[offset + 2] !== flatBackground.b
      ) {
        nonFlatBorderPixels += 1;
      }
    }
  }
  return {
    width: info.width,
    height: info.height,
    channels: info.channels,
    foregroundBounds,
    nonFlatBorderPixels,
    data,
    mask,
  };
}

const report = {
  schemaVersion: 1,
  method: {
    analysisSize,
    background: "#b8b8b8",
    alignment:
      "unchanged orientation, one uniform scale, centroid translation; no rotation, reflection, deformation, or nonuniform warp",
  },
  packages: [],
};

for (const packageSpec of packages) {
  const views = [];
  for (const file of [
    "01-top.png",
    "02-side.png",
    "03-front-three-quarter.png",
    "04-rear-three-quarter.png",
  ]) {
    const relative = join(packageSpec.root, file);
    const inspection = await inspectView(join(repoRoot, relative));
    views.push({
      file,
      width: inspection.width,
      height: inspection.height,
      channels: inspection.channels,
      foregroundBounds: inspection.foregroundBounds,
      nonFlatBorderPixels: inspection.nonFlatBorderPixels,
    });
  }

  const topPath = join(repoRoot, packageSpec.root, "01-top.png");
  const top = await inspectView(topPath);
  const authority = await referenceMask(join(repoRoot, packageSpec.authority));
  const aligned = optimiseRigid(
    top.mask,
    top.width,
    top.height,
    authority,
  );
  const overlay = join(packageSpec.root, "top-overlay.png");
  await saveOverlay(
    authority,
    aligned.transformed,
    join(repoRoot, overlay),
  );
  const cyanRegions = cyanRegionCount(
    top.data,
    top.width,
    top.height,
    top.channels,
  );

  report.packages.push({
    id: packageSpec.id,
    authority: packageSpec.authority,
    views,
    topAlignment: {
      previousIou: packageSpec.previousIou,
      iou: aligned.metrics.iou,
      outerIou: aligned.metrics.outer.iou,
      referenceCoverage: aligned.metrics.referenceCoverage,
      candidateCoverage: aligned.metrics.candidateCoverage,
      areaDelta: aligned.metrics.areaDelta,
      uniformScale: aligned.scale,
      improvement: aligned.metrics.iou - packageSpec.previousIou,
      overlay,
    },
    semanticTopCheck: {
      note:
        "Threshold component count is a regression diagnostic only; the retained manual audit classifies team inlays separately from the authored class-energy core.",
      expectedCyanThresholdComponents:
        packageSpec.expectedTopCyanComponents,
      observedCyanThresholdComponents: cyanRegions,
      passed: cyanRegions === packageSpec.expectedTopCyanComponents,
    },
  });
}

await writeFile(
  join(
    repoRoot,
    "art/class-models/provider-inputs/bulwark-form-input-audit.json",
  ),
  `${JSON.stringify(report, null, 2)}\n`,
);
console.log(JSON.stringify(report, null, 2));
