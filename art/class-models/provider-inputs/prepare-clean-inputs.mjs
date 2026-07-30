import { mkdir, readFile, writeFile } from "node:fs/promises";
import { createRequire } from "node:module";
import { dirname, join, resolve } from "node:path";

const repoRoot = resolve(import.meta.dirname, "../../..");
const requireFromWeb = createRequire(join(repoRoot, "web/package.json"));
const sharp = requireFromWeb("sharp");

const canvasSize = 1254;
const objectExtent = 990;
const flatBackground = { r: 184, g: 184, b: 184 };

const packages = [
  {
    id: "aegis-tortoise-mobile",
    source:
      "art/class-models/provider-inputs/meshy/aegis-tortoise-mobile/clean-v1/source-sheet.png",
    output:
      "art/class-models/provider-inputs/meshy/aegis-tortoise-mobile/clean-v1",
    views: [
      {
        file: "01-top.png",
        crop: { left: 40, top: 15, width: 710, height: 620 },
      },
      {
        file: "02-side.png",
        crop: { left: 35, top: 625, width: 710, height: 320 },
      },
      {
        file: "03-front-three-quarter.png",
        crop: { left: 760, top: 80, width: 740, height: 525 },
      },
      {
        file: "04-rear-three-quarter.png",
        crop: { left: 775, top: 595, width: 715, height: 395 },
      },
    ],
  },
  {
    id: "lattice-loom",
    source:
      "art/class-models/provider-inputs/meshy/lattice-loom/clean-v1/source-sheet.png",
    output: "art/class-models/provider-inputs/meshy/lattice-loom/clean-v1",
    views: [
      {
        file: "01-top.png",
        crop: { left: 25, top: 25, width: 760, height: 650 },
      },
      {
        file: "02-side.png",
        crop: { left: 25, top: 680, width: 770, height: 280 },
      },
      {
        file: "03-front-three-quarter.png",
        crop: { left: 790, top: 80, width: 725, height: 560 },
      },
      {
        file: "04-rear-three-quarter.png",
        crop: { left: 805, top: 620, width: 700, height: 360 },
      },
    ],
  },
];

function colorDistance(data, left, right, channels) {
  const lo = left * channels;
  const ro = right * channels;
  const dr = data[lo] - data[ro];
  const dg = data[lo + 1] - data[ro + 1];
  const db = data[lo + 2] - data[ro + 2];
  return Math.sqrt(dr * dr + dg * dg + db * db);
}

function saturation(data, index, channels) {
  const offset = index * channels;
  const r = data[offset];
  const g = data[offset + 1];
  const b = data[offset + 2];
  return Math.max(r, g, b) - Math.min(r, g, b);
}

// The clean imagegen edits have a connected, nearly achromatic studio
// backdrop and no cast/contact shadow. Flood only that border-connected
// backdrop. Unlike the silhouette-audit mask, this deliberately does not
// classify every enclosed neutral pixel as background: neutral gun, leg,
// ceramic, and truss hardware must survive exactly.
function connectedStudioBackground(data, width, height, channels) {
  const count = width * height;
  const background = new Uint8Array(count);
  const queue = new Int32Array(count);
  let head = 0;
  let tail = 0;

  const enqueue = (index) => {
    if (background[index]) return;
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
    const current = queue[head++];
    const x = current % width;
    const y = Math.floor(current / width);
    const neighbors = [];
    if (x > 0) neighbors.push(current - 1);
    if (x + 1 < width) neighbors.push(current + 1);
    if (y > 0) neighbors.push(current - width);
    if (y + 1 < height) neighbors.push(current + width);
    for (const neighbor of neighbors) {
      if (background[neighbor]) continue;
      if (
        colorDistance(data, current, neighbor, channels) <= 10 &&
        saturation(data, neighbor, channels) <= 22
      ) {
        background[neighbor] = 1;
        queue[tail++] = neighbor;
      }
    }
  }

  return background;
}

function foregroundBounds(background, width, height) {
  let minX = width;
  let minY = height;
  let maxX = -1;
  let maxY = -1;
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      if (background[y * width + x]) continue;
      minX = Math.min(minX, x);
      minY = Math.min(minY, y);
      maxX = Math.max(maxX, x);
      maxY = Math.max(maxY, y);
    }
  }
  if (maxX < minX || maxY < minY) throw new Error("No foreground found");
  return {
    left: minX,
    top: minY,
    width: maxX - minX + 1,
    height: maxY - minY + 1,
  };
}

async function prepareView(source, crop, output) {
  const { data, info } = await sharp(source)
    .extract(crop)
    .removeAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const background = connectedStudioBackground(
    data,
    info.width,
    info.height,
    info.channels,
  );
  for (let index = 0; index < background.length; index += 1) {
    if (!background[index]) continue;
    const offset = index * info.channels;
    data[offset] = flatBackground.r;
    data[offset + 1] = flatBackground.g;
    data[offset + 2] = flatBackground.b;
  }

  const bounds = foregroundBounds(background, info.width, info.height);
  const object = await sharp(data, {
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

  const left = Math.round((canvasSize - object.info.width) / 2);
  const top = Math.round((canvasSize - object.info.height) / 2);
  await sharp({
    create: {
      width: canvasSize,
      height: canvasSize,
      channels: 3,
      background: flatBackground,
    },
  })
    .composite([{ input: object.data, left, top }])
    .png()
    .toFile(output);

  return {
    crop,
    foregroundBounds: bounds,
    outputWidth: canvasSize,
    outputHeight: canvasSize,
    objectWidth: object.info.width,
    objectHeight: object.info.height,
    objectLeft: left,
    objectTop: top,
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

const report = {
  schemaVersion: 1,
  canvasSize,
  objectExtent,
  background: "#b8b8b8",
  packages: [],
};

for (const packageSpec of packages) {
  const source = join(repoRoot, packageSpec.source);
  const outputRoot = join(repoRoot, packageSpec.output);
  await mkdir(outputRoot, { recursive: true });
  const packageReport = {
    id: packageSpec.id,
    source: packageSpec.source,
    views: [],
  };
  const files = [];
  for (const view of packageSpec.views) {
    const output = join(outputRoot, view.file);
    const details = await prepareView(source, view.crop, output);
    packageReport.views.push({ file: view.file, ...details });
    files.push(output);
  }
  await contactSheet(files, join(outputRoot, "contact-sheet.png"));
  report.packages.push(packageReport);
}

await writeFile(
  join(repoRoot, "art/class-models/provider-inputs/clean-input-report.json"),
  `${JSON.stringify(report, null, 2)}\n`,
);
