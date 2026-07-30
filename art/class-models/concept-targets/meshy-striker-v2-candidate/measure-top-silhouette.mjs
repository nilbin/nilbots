import { createHash } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";
import { createRequire } from "node:module";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(scriptDirectory, "../../../..");
const requireFromWeb = createRequire(join(repositoryRoot, "web/package.json"));
const sharp = requireFromWeb("sharp");

const candidatePath = join(scriptDirectory, "01-top.png");
const canonicalPath = join(
  repositoryRoot,
  "web/src/assets/class-looks/trident-wasp/sprite.svg",
);
const overlayPath = join(scriptDirectory, "01-top-silhouette-overlay.png");
const metricsPath = join(scriptDirectory, "silhouette-metrics.json");

const BACKGROUND_FRAME_PIXELS = 16;
const BACKGROUND_CHANNEL_TOLERANCE = 12;
const CANONICAL_ALPHA_THRESHOLD = 128;

function sha256(buffer) {
  return createHash("sha256").update(buffer).digest("hex");
}

function median(values) {
  values.sort((left, right) => left - right);
  return values[Math.floor(values.length / 2)];
}

function estimateBorderBackground(data, width, height, channels) {
  const samples = [[], [], []];

  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      if (
        x >= BACKGROUND_FRAME_PIXELS &&
        x < width - BACKGROUND_FRAME_PIXELS &&
        y >= BACKGROUND_FRAME_PIXELS &&
        y < height - BACKGROUND_FRAME_PIXELS
      ) {
        continue;
      }

      const offset = (y * width + x) * channels;
      samples[0].push(data[offset]);
      samples[1].push(data[offset + 1]);
      samples[2].push(data[offset + 2]);
    }
  }

  return samples.map(median);
}

function floodBorderBackground(data, width, height, channels, background) {
  const pixelCount = width * height;
  const visited = new Uint8Array(pixelCount);
  const queue = new Int32Array(pixelCount);
  let head = 0;
  let tail = 0;

  const isBackgroundCompatible = (pixelIndex) => {
    const offset = pixelIndex * channels;
    return (
      Math.abs(data[offset] - background[0]) <=
        BACKGROUND_CHANNEL_TOLERANCE &&
      Math.abs(data[offset + 1] - background[1]) <=
        BACKGROUND_CHANNEL_TOLERANCE &&
      Math.abs(data[offset + 2] - background[2]) <=
        BACKGROUND_CHANNEL_TOLERANCE
    );
  };

  const enqueue = (pixelIndex) => {
    if (visited[pixelIndex] || !isBackgroundCompatible(pixelIndex)) {
      return;
    }
    visited[pixelIndex] = 1;
    queue[tail] = pixelIndex;
    tail += 1;
  };

  for (let x = 0; x < width; x += 1) {
    enqueue(x);
    enqueue((height - 1) * width + x);
  }
  for (let y = 1; y < height - 1; y += 1) {
    enqueue(y * width);
    enqueue(y * width + width - 1);
  }

  while (head < tail) {
    const pixelIndex = queue[head];
    head += 1;
    const x = pixelIndex % width;
    const y = Math.floor(pixelIndex / width);

    if (x > 0) {
      enqueue(pixelIndex - 1);
    }
    if (x + 1 < width) {
      enqueue(pixelIndex + 1);
    }
    if (y > 0) {
      enqueue(pixelIndex - width);
    }
    if (y + 1 < height) {
      enqueue(pixelIndex + width);
    }
  }

  return visited;
}

function largestConnectedForeground(backgroundMask, width, height) {
  const pixelCount = width * height;
  const labels = new Int32Array(pixelCount);
  const queue = new Int32Array(pixelCount);
  const componentSizes = [0];
  let nextLabel = 1;

  for (let start = 0; start < pixelCount; start += 1) {
    if (backgroundMask[start] || labels[start] !== 0) {
      continue;
    }

    let head = 0;
    let tail = 0;
    let size = 0;
    labels[start] = nextLabel;
    queue[tail] = start;
    tail += 1;

    while (head < tail) {
      const pixelIndex = queue[head];
      head += 1;
      size += 1;
      const x = pixelIndex % width;
      const y = Math.floor(pixelIndex / width);

      for (let offsetY = -1; offsetY <= 1; offsetY += 1) {
        const neighborY = y + offsetY;
        if (neighborY < 0 || neighborY >= height) {
          continue;
        }
        for (let offsetX = -1; offsetX <= 1; offsetX += 1) {
          if (offsetX === 0 && offsetY === 0) {
            continue;
          }
          const neighborX = x + offsetX;
          if (neighborX < 0 || neighborX >= width) {
            continue;
          }
          const neighbor = neighborY * width + neighborX;
          if (backgroundMask[neighbor] || labels[neighbor] !== 0) {
            continue;
          }
          labels[neighbor] = nextLabel;
          queue[tail] = neighbor;
          tail += 1;
        }
      }
    }

    componentSizes.push(size);
    nextLabel += 1;
  }

  let largestLabel = 0;
  for (let label = 1; label < componentSizes.length; label += 1) {
    if (componentSizes[label] > componentSizes[largestLabel]) {
      largestLabel = label;
    }
  }

  const mask = new Uint8Array(pixelCount);
  for (let index = 0; index < pixelCount; index += 1) {
    mask[index] = labels[index] === largestLabel ? 1 : 0;
  }
  return mask;
}

function boundingBox(mask, width, height) {
  let minX = width;
  let minY = height;
  let maxX = -1;
  let maxY = -1;
  let area = 0;

  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      if (!mask[y * width + x]) {
        continue;
      }
      area += 1;
      minX = Math.min(minX, x);
      minY = Math.min(minY, y);
      maxX = Math.max(maxX, x);
      maxY = Math.max(maxY, y);
    }
  }

  if (maxX < minX || maxY < minY) {
    throw new Error("Silhouette mask is empty.");
  }

  return {
    minX,
    minY,
    maxX,
    maxY,
    width: maxX - minX + 1,
    height: maxY - minY + 1,
    area,
  };
}

function cropMask(mask, imageWidth, bounds) {
  const cropped = new Uint8Array(bounds.width * bounds.height);
  for (let y = 0; y < bounds.height; y += 1) {
    const sourceOffset = (bounds.minY + y) * imageWidth + bounds.minX;
    const targetOffset = y * bounds.width;
    cropped.set(
      mask.subarray(sourceOffset, sourceOffset + bounds.width),
      targetOffset,
    );
  }
  return cropped;
}

async function resizeBinaryMask(mask, width, height, targetWidth, targetHeight) {
  const { data, info } = await sharp(Buffer.from(mask), {
    raw: { width, height, channels: 1 },
  })
    .resize(targetWidth, targetHeight, { kernel: "nearest" })
    .raw()
    .toBuffer({ resolveWithObject: true });

  const binary = new Uint8Array(targetWidth * targetHeight);
  for (let index = 0; index < binary.length; index += 1) {
    binary[index] = data[index * info.channels] >= 1 ? 1 : 0;
  }
  return binary;
}

function placeCentered(mask, maskWidth, maskHeight, canvasWidth, canvasHeight, center) {
  const placed = new Uint8Array(canvasWidth * canvasHeight);
  const left = Math.round(center.x - maskWidth / 2);
  const top = Math.round(center.y - maskHeight / 2);

  for (let y = 0; y < maskHeight; y += 1) {
    const targetY = top + y;
    if (targetY < 0 || targetY >= canvasHeight) {
      continue;
    }
    for (let x = 0; x < maskWidth; x += 1) {
      if (!mask[y * maskWidth + x]) {
        continue;
      }
      const targetX = left + x;
      if (targetX >= 0 && targetX < canvasWidth) {
        placed[targetY * canvasWidth + targetX] = 1;
      }
    }
  }

  return { mask: placed, left, top };
}

function centroid(mask, width, height) {
  let sumX = 0;
  let sumY = 0;
  let area = 0;
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      if (!mask[y * width + x]) {
        continue;
      }
      sumX += x;
      sumY += y;
      area += 1;
    }
  }
  return { x: sumX / area, y: sumY / area };
}

function percent(value) {
  return Number((value * 100).toFixed(3));
}

const candidateBuffer = await readFile(candidatePath);
const canonicalBuffer = await readFile(canonicalPath);
const candidateRaw = await sharp(candidateBuffer)
  .removeAlpha()
  .raw()
  .toBuffer({ resolveWithObject: true });
const { width, height, channels } = candidateRaw.info;

if (width !== height) {
  throw new Error(`Expected a square candidate, received ${width}×${height}.`);
}

const background = estimateBorderBackground(
  candidateRaw.data,
  width,
  height,
  channels,
);
const candidateBackground = floodBorderBackground(
  candidateRaw.data,
  width,
  height,
  channels,
  background,
);
const candidateMask = largestConnectedForeground(
  candidateBackground,
  width,
  height,
);
const candidateBounds = boundingBox(candidateMask, width, height);

const canonicalRaw = await sharp(canonicalBuffer)
  .resize(width, height, { fit: "fill" })
  .ensureAlpha()
  .raw()
  .toBuffer({ resolveWithObject: true });
const canonicalMask = new Uint8Array(width * height);
for (let index = 0; index < canonicalMask.length; index += 1) {
  canonicalMask[index] =
    canonicalRaw.data[index * canonicalRaw.info.channels + 3] >=
    CANONICAL_ALPHA_THRESHOLD
      ? 1
      : 0;
}
const canonicalBounds = boundingBox(canonicalMask, width, height);

const scale = canonicalBounds.width / candidateBounds.width;
const alignedCandidateWidth = canonicalBounds.width;
const alignedCandidateHeight = Math.round(candidateBounds.height * scale);
const croppedCandidate = cropMask(candidateMask, width, candidateBounds);
const resizedCandidate = await resizeBinaryMask(
  croppedCandidate,
  candidateBounds.width,
  candidateBounds.height,
  alignedCandidateWidth,
  alignedCandidateHeight,
);
const canonicalCenter = {
  x: (canonicalBounds.minX + canonicalBounds.maxX + 1) / 2,
  y: (canonicalBounds.minY + canonicalBounds.maxY + 1) / 2,
};
const alignedCandidate = placeCentered(
  resizedCandidate,
  alignedCandidateWidth,
  alignedCandidateHeight,
  width,
  height,
  canonicalCenter,
);

let intersection = 0;
let union = 0;
let canonicalOnly = 0;
let candidateOnly = 0;
let canonicalArea = 0;
let candidateArea = 0;
const overlay = Buffer.alloc(width * height * 4);

for (let index = 0; index < canonicalMask.length; index += 1) {
  const canonical = canonicalMask[index] === 1;
  const candidate = alignedCandidate.mask[index] === 1;
  const output = index * 4;

  if (canonical) {
    canonicalArea += 1;
  }
  if (candidate) {
    candidateArea += 1;
  }
  if (canonical && candidate) {
    intersection += 1;
    union += 1;
    overlay[output] = 218;
    overlay[output + 1] = 225;
    overlay[output + 2] = 232;
  } else if (canonical) {
    canonicalOnly += 1;
    union += 1;
    overlay[output] = 255;
    overlay[output + 1] = 72;
    overlay[output + 2] = 139;
  } else if (candidate) {
    candidateOnly += 1;
    union += 1;
    overlay[output] = 0;
    overlay[output + 1] = 220;
    overlay[output + 2] = 255;
  } else {
    overlay[output] = 18;
    overlay[output + 1] = 22;
    overlay[output + 2] = 29;
  }
  overlay[output + 3] = 255;
}

const canonicalCentroid = centroid(canonicalMask, width, height);
const candidateCentroid = centroid(alignedCandidate.mask, width, height);
const metrics = {
  schemaVersion: 1,
  inputs: {
    canonical: {
      file: "../../../../web/src/assets/class-looks/trident-wasp/sprite.svg",
      sha256: sha256(canonicalBuffer),
    },
    candidate: {
      file: "01-top.png",
      sha256: sha256(candidateBuffer),
    },
  },
  method: {
    analysisCanvas: { width, height },
    canonicalRasterization: {
      alphaThreshold: CANONICAL_ALPHA_THRESHOLD,
    },
    candidateSegmentation: {
      borderFramePixels: BACKGROUND_FRAME_PIXELS,
      medianBackgroundRgb: background,
      perChannelTolerance: BACKGROUND_CHANNEL_TOLERANCE,
      connectivity: "4-connected border background, largest 8-connected foreground",
    },
    alignment: {
      orientation: "unchanged; both inputs face East/right",
      scale: "uniform candidate scale to canonical outer-silhouette bounding-box width",
      translation: "bounding-box centers aligned; no rotation, anisotropic fit, or IoU optimization",
      candidateScaleFactor: Number(scale.toFixed(9)),
      placedCandidateOrigin: {
        x: alignedCandidate.left,
        y: alignedCandidate.top,
      },
    },
    overlayLegend: {
      overlap: "#dae1e8",
      canonicalOnly: "#ff488b",
      candidateOnly: "#00dcff",
      background: "#12161d",
    },
  },
  measurements: {
    canonicalBoundingBox: canonicalBounds,
    candidateBoundingBoxBeforeAlignment: candidateBounds,
    candidateAlignedBoundingBoxSize: {
      width: alignedCandidateWidth,
      height: alignedCandidateHeight,
    },
    canonicalAspectRatio: Number(
      (canonicalBounds.width / canonicalBounds.height).toFixed(6),
    ),
    candidateAspectRatio: Number(
      (candidateBounds.width / candidateBounds.height).toFixed(6),
    ),
    candidateHeightDeltaAtEqualWidthPercent: Number(
      (
        ((alignedCandidateHeight - canonicalBounds.height) /
          canonicalBounds.height) *
        100
      ).toFixed(3),
    ),
    intersectionPixels: intersection,
    unionPixels: union,
    canonicalOnlyPixels: canonicalOnly,
    candidateOnlyPixels: candidateOnly,
    canonicalAreaPixels: canonicalArea,
    alignedCandidateAreaPixels: candidateArea,
    intersectionOverUnion: Number((intersection / union).toFixed(9)),
    canonicalCoverage: Number((intersection / canonicalArea).toFixed(9)),
    candidateCoverage: Number((intersection / candidateArea).toFixed(9)),
    intersectionOverUnionPercent: percent(intersection / union),
    canonicalCoveragePercent: percent(intersection / canonicalArea),
    candidateCoveragePercent: percent(intersection / candidateArea),
    alignedCandidateAreaDeltaPercent: Number(
      (((candidateArea - canonicalArea) / canonicalArea) * 100).toFixed(3),
    ),
    centroidDeltaPixels: {
      x: Number((candidateCentroid.x - canonicalCentroid.x).toFixed(3)),
      y: Number((candidateCentroid.y - canonicalCentroid.y).toFixed(3)),
    },
  },
};

await sharp(overlay, {
  raw: { width, height, channels: 4 },
})
  .png({ compressionLevel: 9, adaptiveFiltering: false })
  .toFile(overlayPath);
await writeFile(metricsPath, `${JSON.stringify(metrics, null, 2)}\n`, "utf8");

console.log(JSON.stringify(metrics.measurements, null, 2));
