#!/usr/bin/env node

import { copyFileSync, existsSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const sourceDirectory = path.join(repository, 'art/reviews/arc-relay-meshy7-fleet-v1');
const outputDirectory = path.join(repository, 'sandbox/meshy7-pilot/fleet-game-review');
const files = ['index.html', 'gameplay-overview.png', 'fixed-orientation-sheet.png'];

if (!existsSync(path.join(outputDirectory, 'interactive/index.html')))
  throw new Error('Install the complete-fleet interactive review before publishing its index.');
for (const file of files) {
  const source = path.join(sourceDirectory, file);
  if (!existsSync(source)) throw new Error(`Missing ${path.relative(repository, source)}.`);
  copyFileSync(source, path.join(outputDirectory, file));
}
writeFileSync(
  path.join(outputDirectory, 'public-review.json'),
  `${JSON.stringify({
    schemaVersion: 1,
    status: 'owner-review',
    runtimeAssetsPromoted: false,
    index: 'index.html',
    interactive: 'interactive/index.html',
    evidence: files.slice(1),
  }, null, 2)}\n`,
);
console.log(`Published review index and ${files.length - 1} evidence images.`);
