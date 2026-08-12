#!/usr/bin/env node

import { createRequire } from 'node:module';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const sharp = requireFromWeb('sharp');

const [inputArgument, outputArgument, viewLayoutArgument] = process.argv.slice(2);
if (!inputArgument || !outputArgument)
  throw new Error(
    'Usage: node scripts/class-models/split-meshy7-turnaround.mjs ' +
      '<sheet.png> <output-dir> [comma-separated quadrant views]',
  );

const defaultViews = ['front', 'right', 'back', 'front-right-three-quarter'];
const views = viewLayoutArgument?.split(',') ?? defaultViews;
if (
  views.length !== 4 ||
  views.some((view) => !/^[a-z][a-z0-9-]*$/.test(view)) ||
  new Set(views).size !== views.length
)
  throw new Error(
    'The optional quadrant layout must contain four distinct safe comma-separated view names.',
  );

const input = path.resolve(repository, inputArgument);
const outputDirectory = path.resolve(repository, outputArgument);
const metadata = await sharp(input).metadata();
if (
  !metadata.width ||
  !metadata.height ||
  metadata.width !== metadata.height ||
  metadata.width % 2 !== 0
)
  throw new Error(
    `Turnaround must be an even square image, received ${metadata.width}x${metadata.height}.`,
  );

const size = metadata.width / 2;
const outputWidth = 1536;
const outputHeight = 1024;
const background = '#eeeeee';
const panels = [
  { left: 0, top: 0 },
  { left: size, top: 0 },
  { left: 0, top: size },
  { left: size, top: size },
].map((panel, index) => ({
  ...panel,
  file: `${String(index + 1).padStart(2, '0')}-${views[index]}.png`,
}));

await mkdir(outputDirectory, { recursive: true });
for (const panel of panels) {
  const square = await sharp(input)
    .extract({ left: panel.left, top: panel.top, width: size, height: size })
    .resize({ width: outputHeight, height: outputHeight, fit: 'contain', background })
    .png({ compressionLevel: 9 })
    .toBuffer();
  await sharp({
    create: {
      width: outputWidth,
      height: outputHeight,
      channels: 3,
      background,
    },
  })
    .composite([{ input: square, left: (outputWidth - outputHeight) / 2, top: 0 }])
    .png({ compressionLevel: 9 })
    .toFile(path.join(outputDirectory, panel.file));
}

console.log(`Split ${path.relative(repository, input)} into four 1536x1024 Meshy inputs.`);
