#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const sharp = requireFromWeb('sharp');
const source = path.join(
  repository,
  'art/objective-models/arc-relay/reactor-recess-v1/reactor-recess-six-view.png',
);
const output = path.join(
  repository,
  'art/objective-models/arc-relay/reactor-recess-v1/meshy7-inputs-v1',
);
const expectedSourceSha256 =
  '5e869249f8a18c7322f6d852160c3040803c7679d02336fba8e531d56fc72844';
const outputSize = 1536;
// Image generation drew one-pixel white panel dividers at x=510 and y=471–472.
// Trim those separators deterministically; they are sheet layout, not object pixels.
const views = [
  {
    file: '01-front.png', left: 0, top: 0, width: 510, height: 471,
    view: 'strict-front',
  },
  {
    file: '02-right.png', left: 512, top: 0, width: 511, height: 471,
    view: 'strict-right',
  },
  {
    file: '03-top.png', left: 0, top: 473, width: 510, height: 551,
    view: 'strict-top',
  },
  {
    file: '04-front-right-three-quarter.png',
    left: 512,
    top: 473,
    width: 511,
    height: 551,
    view: 'front-right-three-quarter',
  },
];

const sourceBytes = await readFile(source);
if (sha256(sourceBytes) !== expectedSourceSha256)
  throw new Error('The approved Reactor recess concept sheet has drifted.');
const metadata = await sharp(sourceBytes).metadata();
if (metadata.width !== 1536 || metadata.height !== 1024)
  throw new Error(`Expected a 1536x1024 six-view sheet, received ${metadata.width}x${metadata.height}.`);

await mkdir(output, { recursive: true });
for (const entry of views) {
  await sharp(sourceBytes)
    .extract({ left: entry.left, top: entry.top, width: entry.width, height: entry.height })
    .resize(outputSize, outputSize, {
      fit: 'contain',
      background: '#d4d3d1',
      kernel: sharp.kernel.lanczos3,
    })
    .png({ compressionLevel: 9 })
    .toFile(path.join(output, entry.file));
  console.log(`${entry.view}: ${path.relative(repository, path.join(output, entry.file))}`);
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}
