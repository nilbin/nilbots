#!/usr/bin/env node

import { createRequire } from 'node:module';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const sharp = requireFromWeb('sharp');

const [inputArgument, outputArgument] = process.argv.slice(2);
if (!inputArgument || !outputArgument)
  throw new Error(
    'Usage: node scripts/class-models/split-meshy7-turnaround.mjs <sheet.png> <output-dir>',
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
  { file: '01-front.png', left: 0, top: 0 },
  { file: '02-right.png', left: size, top: 0 },
  { file: '03-back.png', left: 0, top: size },
  { file: '04-front-right-three-quarter.png', left: size, top: size },
];

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
