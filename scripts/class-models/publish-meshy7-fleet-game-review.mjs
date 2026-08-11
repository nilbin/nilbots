#!/usr/bin/env node

import { cpSync, copyFileSync, existsSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const sourceDirectory = path.join(repository, 'art/reviews/arc-relay-meshy7-fleet-v1');
const reviewDirectory = path.join(repository, 'sandbox/meshy7-pilot/fleet-game-review');
const interactiveDirectory = path.join(reviewDirectory, 'interactive');
const outputDirectory = path.join(reviewDirectory, 'public');
const evidenceDirectory = path.join(outputDirectory, 'evidence');
const files = ['gameplay-overview.png', 'fixed-orientation-sheet.png'];

if (!existsSync(path.join(interactiveDirectory, 'index.html')))
  throw new Error('Install the complete-fleet interactive review before publishing its index.');
rmSync(outputDirectory, { recursive: true, force: true });
cpSync(interactiveDirectory, outputDirectory, { recursive: true });
mkdirSync(evidenceDirectory, { recursive: true });
for (const file of files) {
  const source = path.join(sourceDirectory, file);
  if (!existsSync(source)) throw new Error(`Missing ${path.relative(repository, source)}.`);
  copyFileSync(source, path.join(evidenceDirectory, file));
}
writeFileSync(
  path.join(outputDirectory, 'public-review.json'),
  `${JSON.stringify({
    schemaVersion: 1,
    status: 'owner-review',
    runtimeAssetsPromoted: false,
    index: 'index.html',
    replayInstaller: 'scripts/class-models/install-local-replay-gallery.mjs',
    evidence: files.map((file) => `evidence/${file}`),
  }, null, 2)}\n`,
);
console.log(`Published the standard replay viewer and ${files.length} evidence images.`);
