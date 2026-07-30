import { mkdir, readFile, writeFile } from "node:fs/promises";
import { createRequire } from "node:module";
import { join, resolve } from "node:path";

const repoRoot = resolve(import.meta.dirname, "../../..");
const requireFromWeb = createRequire(join(repoRoot, "web/package.json"));
const sharp = requireFromWeb("sharp");

const canvasSize = 1254;
const objectExtent = 990;
const flatBackground = { r: 184, g: 184, b: 184 };

const packages = [
  {
    id: "trident-spark",
    source:
      "art/class-models/provider-inputs/meshy/trident-spark/clean-v1/chroma-source-sheet.png",
    authority:
      "web/src/assets/class-projectile-looks/trident-spark/sprite.svg",
    output:
      "art/class-models/provider-inputs/meshy/trident-spark/clean-v1",
    topTexture:
      "art/class-models/provider-inputs/meshy/trident-spark/clean-v1/top-texture-source.png",
    topPalette: ["#242629", "#8b857e", "#303236"],
    topHighlight: "#f1f0eb",
    views: [
      {
        role: "top",
        file: "01-top.png",
        crop: { left: 0, top: 200, width: 775, height: 485 },
      },
      {
        role: "side",
        file: "02-side.png",
        crop: { left: 0, top: 725, width: 745, height: 260 },
      },
      {
        role: "front-three-quarter",
        file: "03-front-three-quarter.png",
        crop: { left: 775, top: 270, width: 479, height: 430 },
      },
      {
        role: "rear-three-quarter",
        file: "04-rear-three-quarter.png",
        crop: { left: 755, top: 690, width: 499, height: 340 },
      },
    ],
  },
  {
    id: "rebound-diamond",
    source:
      "art/class-models/provider-inputs/meshy/rebound-diamond/clean-v1/chroma-source-sheet.png",
    authority:
      "web/src/assets/class-projectile-looks/rebound-diamond/sprite.svg",
    output:
      "art/class-models/provider-inputs/meshy/rebound-diamond/clean-v1",
    topPalette: ["#202632", "#846f54", "#343842"],
    topHighlight: "#6f6557",
    views: [
      {
        role: "top",
        file: "01-top.png",
        crop: { left: 0, top: 60, width: 955, height: 520 },
      },
      {
        role: "side",
        file: "02-side.png",
        crop: { left: 0, top: 650, width: 900, height: 250 },
      },
      {
        role: "front-three-quarter",
        file: "03-front-three-quarter.png",
        crop: { left: 930, top: 70, width: 606, height: 450 },
      },
      {
        role: "rear-three-quarter",
        file: "04-rear-three-quarter.png",
        crop: { left: 870, top: 520, width: 666, height: 504 },
      },
    ],
  },
  {
    id: "lattice-rivet",
    source:
      "art/class-models/provider-inputs/meshy/lattice-rivet/clean-v1/chroma-source-sheet.png",
    authority:
      "web/src/assets/class-projectile-looks/lattice-rivet/sprite.svg",
    output:
      "art/class-models/provider-inputs/meshy/lattice-rivet/clean-v1",
    topTexture:
      "art/class-models/provider-inputs/meshy/lattice-rivet/clean-v1/top-texture-source.png",
    topPalette: ["#d9d5cd", "#a7a39b", "#e1ddd5"],
    topHighlight: "#454744",
    views: [
      {
        role: "top",
        file: "01-top.png",
        crop: { left: 0, top: 120, width: 805, height: 500 },
      },
      {
        role: "side",
        file: "02-side.png",
        crop: { left: 0, top: 800, width: 720, height: 240 },
      },
      {
        role: "front-three-quarter",
        file: "03-front-three-quarter.png",
        crop: { left: 720, top: 190, width: 534, height: 470 },
      },
      {
        role: "rear-three-quarter",
        file: "04-rear-three-quarter.png",
        crop: { left: 690, top: 680, width: 564, height: 430 },
      },
    ],
  },
];

// Chroma classification is intentionally global, including holes and open
// truss cells. It cannot walk across antialiasing into neutral ceramic or dark
// hardware, which is the failure mode of achromatic studio-background floods.
function chromaBackground(data, width, height, channels) {
  const background = new Uint8Array(width * height);
  for (let index = 0; index < background.length; index += 1) {
    const offset = index * channels;
    const r = data[offset];
    const g = data[offset + 1];
    const b = data[offset + 2];
    background[index] =
      g >= 135 && g - r >= 65 && g - b >= 65 ? 1 : 0;
  }
  return background;
}

function largestForegroundComponent(background, width, height) {
  const count = width * height;
  const visited = new Uint8Array(count);
  const queue = new Int32Array(count);
  let largest = [];

  for (let start = 0; start < count; start += 1) {
    if (background[start] || visited[start]) continue;
    let head = 0;
    let tail = 0;
    queue[tail++] = start;
    visited[start] = 1;
    const component = [];
    while (head < tail) {
      const current = queue[head++];
      component.push(current);
      const x = current % width;
      const y = Math.floor(current / width);
      for (let dy = -1; dy <= 1; dy += 1) {
        for (let dx = -1; dx <= 1; dx += 1) {
          if (dx === 0 && dy === 0) continue;
          const nx = x + dx;
          const ny = y + dy;
          if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
          const neighbor = ny * width + nx;
          if (background[neighbor] || visited[neighbor]) continue;
          visited[neighbor] = 1;
          queue[tail++] = neighbor;
        }
      }
    }
    if (component.length > largest.length) largest = component;
  }

  if (largest.length === 0) throw new Error("No foreground component found");
  const mask = new Uint8Array(count);
  for (const index of largest) mask[index] = 1;
  return mask;
}

function maskBounds(mask, width, height) {
  let minX = width;
  let minY = height;
  let maxX = -1;
  let maxY = -1;
  let pixels = 0;
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      if (!mask[y * width + x]) continue;
      pixels += 1;
      minX = Math.min(minX, x);
      minY = Math.min(minY, y);
      maxX = Math.max(maxX, x);
      maxY = Math.max(maxY, y);
    }
  }
  if (maxX < minX || maxY < minY) throw new Error("No mask bounds found");
  return {
    left: minX,
    top: minY,
    width: maxX - minX + 1,
    height: maxY - minY + 1,
    pixels,
  };
}

async function isolateSourceView(source, view) {
  const { data, info } = await sharp(source)
    .extract(view.crop)
    .removeAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const background = chromaBackground(
    data,
    info.width,
    info.height,
    info.channels,
  );
  const foreground = largestForegroundComponent(
    background,
    info.width,
    info.height,
  );
  const bounds = maskBounds(foreground, info.width, info.height);

  const flattened = Buffer.from(data);
  const rgba = Buffer.alloc(info.width * info.height * 4);
  for (let index = 0; index < foreground.length; index += 1) {
    const sourceOffset = index * info.channels;
    const rgbaOffset = index * 4;
    const isForeground = foreground[index] === 1;
    const sourceR = data[sourceOffset];
    const sourceG = data[sourceOffset + 1];
    const sourceB = data[sourceOffset + 2];
    const neutralCeiling = Math.max(sourceR, sourceB) + 3;
    const despilledG =
      isForeground && sourceG > neutralCeiling
        ? neutralCeiling
        : sourceG;
    flattened[sourceOffset] = isForeground
      ? sourceR
      : flatBackground.r;
    flattened[sourceOffset + 1] = isForeground
      ? despilledG
      : flatBackground.g;
    flattened[sourceOffset + 2] = isForeground
      ? sourceB
      : flatBackground.b;
    rgba[rgbaOffset] = sourceR;
    rgba[rgbaOffset + 1] = despilledG;
    rgba[rgbaOffset + 2] = sourceB;
    rgba[rgbaOffset + 3] = isForeground ? 255 : 0;
  }

  const flatObject = await sharp(flattened, {
    raw: {
      width: info.width,
      height: info.height,
      channels: info.channels,
    },
  })
    .extract(bounds)
    .resize(objectExtent, objectExtent, {
      fit: "inside",
      kernel: sharp.kernel.lanczos3,
      withoutEnlargement: false,
    })
    .png()
    .toBuffer({ resolveWithObject: true });

  const rgbaObject = await sharp(rgba, {
    raw: {
      width: info.width,
      height: info.height,
      channels: 4,
    },
  })
    .extract(bounds)
    .resize(objectExtent, objectExtent, {
      fit: "inside",
      kernel: sharp.kernel.lanczos3,
      withoutEnlargement: false,
    })
    .png()
    .toBuffer({ resolveWithObject: true });

  return {
    crop: view.crop,
    foregroundBounds: bounds,
    flatObject,
    rgbaObject,
  };
}

async function isolateWholeChroma(source) {
  const metadata = await sharp(source).metadata();
  if (!metadata.width || !metadata.height) {
    throw new Error(`Cannot read top-texture dimensions: ${source}`);
  }
  return isolateSourceView(source, {
    crop: {
      left: 0,
      top: 0,
      width: metadata.width,
      height: metadata.height,
    },
  });
}

async function renderAuthority(authorityPath) {
  return renderSvgToExactExtent(authorityPath);
}

async function renderSvgToExactExtent(input) {
  const raster = await sharp(input, { density: 600 })
    .resize(1536, 1536, { fit: "fill" })
    .ensureAlpha()
    .png()
    .toBuffer();
  const { data, info } = await sharp(raster)
    .raw()
    .toBuffer({ resolveWithObject: true });
  const mask = new Uint8Array(info.width * info.height);
  for (let index = 0; index < mask.length; index += 1) {
    mask[index] = data[index * info.channels + 3] > 8 ? 1 : 0;
  }
  const bounds = maskBounds(mask, info.width, info.height);
  return sharp(raster)
    .extract(bounds)
    .resize(objectExtent, objectExtent, {
      fit: "inside",
      kernel: sharp.kernel.lanczos3,
      withoutEnlargement: false,
    })
    .png()
    .toBuffer({ resolveWithObject: true });
}

async function renderMaterialAuthority(authorityPath, palette, highlight) {
  let svg = await readFile(authorityPath, "utf8");
  const defs = `<defs>
    <linearGradient id="nilbots-surface" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="${palette[0]}"/>
      <stop offset="52%" stop-color="${palette[1]}"/>
      <stop offset="100%" stop-color="${palette[2]}"/>
    </linearGradient>
  </defs>`;
  svg = svg.replace(/(<svg\b[^>]*>)/, `$1${defs}`);
  let fillIndex = 0;
  svg = svg.replace(/fill="#fff"/g, () => {
    fillIndex += 1;
    return fillIndex === 1
      ? 'fill="url(#nilbots-surface)"'
      : `fill="${highlight}"`;
  });
  if (fillIndex !== 2) {
    throw new Error(
      `${authorityPath} must contain exactly two canonical white paths`,
    );
  }
  return renderSvgToExactExtent(Buffer.from(svg));
}

async function prepareOrdinaryView(isolated, output) {
  const left = Math.round((canvasSize - isolated.flatObject.info.width) / 2);
  const top = Math.round((canvasSize - isolated.flatObject.info.height) / 2);
  await sharp({
    create: {
      width: canvasSize,
      height: canvasSize,
      channels: 3,
      background: flatBackground,
    },
  })
    .composite([{ input: isolated.flatObject.data, left, top }])
    .flatten({ background: flatBackground })
    .removeAlpha()
    .png()
    .toFile(output);
  return {
    outputWidth: canvasSize,
    outputHeight: canvasSize,
    objectWidth: isolated.flatObject.info.width,
    objectHeight: isolated.flatObject.info.height,
    objectLeft: left,
    objectTop: top,
  };
}

async function prepareExactTop(
  isolated,
  authorityPath,
  palette,
  highlight,
  output,
  overlayOutput,
) {
  const authority = await renderAuthority(authorityPath);
  const material = await renderMaterialAuthority(
    authorityPath,
    palette,
    highlight,
  );
  const fittedTexture = await sharp(isolated.rgbaObject.data)
    .resize(authority.info.width, authority.info.height, {
      fit: "cover",
      position: "centre",
      kernel: sharp.kernel.lanczos3,
      withoutEnlargement: false,
    })
    .png()
    .toBuffer({ resolveWithObject: true });
  const authorityLeft = Math.round((canvasSize - authority.info.width) / 2);
  const authorityTop = Math.round((canvasSize - authority.info.height) / 2);
  const textureLeft = authorityLeft;
  const textureTop = authorityTop;

  const authorityCanvas = await sharp({
    create: {
      width: canvasSize,
      height: canvasSize,
      channels: 4,
      background: { r: 0, g: 0, b: 0, alpha: 0 },
    },
  })
    .composite([
      { input: authority.data, left: authorityLeft, top: authorityTop },
    ])
    .png()
    .toBuffer();

  const exactObjectCanvas = await sharp({
    create: {
      width: canvasSize,
      height: canvasSize,
      channels: 4,
      background: { r: 0, g: 0, b: 0, alpha: 0 },
    },
  })
    .composite([
      {
        input: material.data,
        left: authorityLeft,
        top: authorityTop,
      },
      {
        input: fittedTexture.data,
        left: textureLeft,
        top: textureTop,
      },
      {
        input: authorityCanvas,
        blend: "dest-in",
      },
    ])
    .png()
    .toBuffer();

  await sharp({
    create: {
      width: canvasSize,
      height: canvasSize,
      channels: 3,
      background: flatBackground,
    },
  })
    .composite([{ input: exactObjectCanvas, left: 0, top: 0 }])
    .flatten({ background: flatBackground })
    .removeAlpha()
    .png()
    .toFile(output);

  const authorityTint = await sharp(authorityCanvas)
    .tint({ r: 0, g: 220, b: 255 })
    .png()
    .toBuffer();
  const finalTint = await sharp(exactObjectCanvas)
    .tint({ r: 255, g: 50, b: 180 })
    .png()
    .toBuffer();
  await sharp({
    create: {
      width: canvasSize,
      height: canvasSize,
      channels: 3,
      background: { r: 16, g: 20, b: 26 },
    },
  })
    .composite([
      { input: authorityTint, left: 0, top: 0, blend: "screen" },
      { input: finalTint, left: 0, top: 0, blend: "screen" },
    ])
    .png()
    .toFile(overlayOutput);

  return {
    outputWidth: canvasSize,
    outputHeight: canvasSize,
    objectWidth: authority.info.width,
    objectHeight: authority.info.height,
    objectLeft: authorityLeft,
    objectTop: authorityTop,
    textureSourceWidth: isolated.rgbaObject.info.width,
    textureSourceHeight: isolated.rgbaObject.info.height,
    textureObjectWidth: fittedTexture.info.width,
    textureObjectHeight: fittedTexture.info.height,
    textureObjectLeft: textureLeft,
    textureObjectTop: textureTop,
    method:
      "exact SVG mask with centered uniform-scale cover texture and deterministic neutral gap fill; no warp",
  };
}

async function contactSheet(files, output) {
  const tile = 512;
  const gap = 8;
  const cells = [];
  for (let index = 0; index < files.length; index += 1) {
    const input = await sharp(files[index])
      .resize(tile, tile, { fit: "fill" })
      .png()
      .toBuffer();
    cells.push({
      input,
      left: (index % 2) * (tile + gap),
      top: Math.floor(index / 2) * (tile + gap),
    });
  }
  await sharp({
    create: {
      width: tile * 2 + gap,
      height: tile * 2 + gap,
      channels: 3,
      background: { r: 232, g: 232, b: 232 },
    },
  })
    .composite(cells)
    .png()
    .toFile(output);
}

async function strictTopMetrics(output, authorityPath) {
  const authority = await renderAuthority(authorityPath);
  const left = Math.round((canvasSize - authority.info.width) / 2);
  const top = Math.round((canvasSize - authority.info.height) / 2);
  const expected = await sharp({
    create: {
      width: canvasSize,
      height: canvasSize,
      channels: 3,
      background: { r: 0, g: 0, b: 0 },
    },
  })
    .composite([
      {
        input: await sharp(authority.data)
          .extractChannel("alpha")
          .toColourspace("b-w")
          .png()
          .toBuffer(),
        left,
        top,
      },
    ])
    .extractChannel(0)
    .raw()
    .toBuffer();
  const { data, info } = await sharp(output)
    .removeAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  let intersection = 0;
  let union = 0;
  let outputPixels = 0;
  let authorityPixels = 0;
  for (let index = 0; index < canvasSize * canvasSize; index += 1) {
    const offset = index * info.channels;
    const dr = data[offset] - flatBackground.r;
    const dg = data[offset + 1] - flatBackground.g;
    const db = data[offset + 2] - flatBackground.b;
    const outputForeground = dr * dr + dg * dg + db * db > 64;
    const authorityForeground = expected[index] > 32;
    if (outputForeground) outputPixels += 1;
    if (authorityForeground) authorityPixels += 1;
    if (outputForeground && authorityForeground) intersection += 1;
    if (outputForeground || authorityForeground) union += 1;
  }
  return {
    intersectionPixels: intersection,
    unionPixels: union,
    outputPixels,
    authorityPixels,
    silhouetteIou: union === 0 ? 0 : intersection / union,
    outputCoverage:
      authorityPixels === 0 ? 0 : intersection / authorityPixels,
    authorityCoverage:
      outputPixels === 0 ? 0 : intersection / outputPixels,
  };
}

const report = {
  schemaVersion: 1,
  canvasSize,
  objectExtent,
  background: "#b8b8b8",
  topMethod:
    "Exact runtime SVG outer silhouette and apertures clip a centered, uniformly scaled focused top texture; deterministic neutral class-family material fills only uncovered mask pixels. Cover-crop may trim texture beyond the authoritative bounds; there is no rotation, reflection, nonuniform scaling, or warp.",
  packages: [],
};

for (const packageSpec of packages) {
  const source = join(repoRoot, packageSpec.source);
  const authority = join(repoRoot, packageSpec.authority);
  const outputRoot = join(repoRoot, packageSpec.output);
  await mkdir(outputRoot, { recursive: true });
  const packageReport = {
    id: packageSpec.id,
    source: packageSpec.source,
    topAuthority: packageSpec.authority,
    topTexture: packageSpec.topTexture ?? packageSpec.source,
    views: [],
  };
  const files = [];
  for (const view of packageSpec.views) {
    const output = join(outputRoot, view.file);
    const isolated = await isolateSourceView(source, view);
    const common = {
      role: view.role,
      file: view.file,
      crop: isolated.crop,
      foregroundBounds: isolated.foregroundBounds,
    };
    if (view.role === "top") {
      const topTexture = packageSpec.topTexture
        ? await isolateWholeChroma(join(repoRoot, packageSpec.topTexture))
        : isolated;
      const details = await prepareExactTop(
        topTexture,
        authority,
        packageSpec.topPalette,
        packageSpec.topHighlight,
        output,
        join(outputRoot, "top-overlay.png"),
      );
      packageReport.views.push({
        ...common,
        ...details,
        alignment: await strictTopMetrics(output, authority),
      });
    } else {
      packageReport.views.push({
        ...common,
        ...(await prepareOrdinaryView(isolated, output)),
      });
    }
    files.push(output);
  }
  await contactSheet(files, join(outputRoot, "contact-sheet.png"));
  report.packages.push(packageReport);
}

await writeFile(
  join(
    repoRoot,
    "art/class-models/provider-inputs/projectile-clean-input-report.json",
  ),
  `${JSON.stringify(report, null, 2)}\n`,
);
