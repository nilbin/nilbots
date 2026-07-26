#!/usr/bin/env node

import { createHash } from 'node:crypto';
import {
  readFile,
  readdir,
  rename,
  stat,
  unlink,
  writeFile,
} from 'node:fs/promises';
import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repositoryRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
);
// sharp is a web/ dependency, but this script lives in scripts/, so ESM would resolve it
// from the repository root where it is not installed.
const sharp = createRequire(path.join(repositoryRoot, 'web', 'package.json'))('sharp');

const reviewRoot = path.join(repositoryRoot, 'web', 'dist-review');
const assetsRoot = path.join(reviewRoot, 'assets');
const atlasPattern =
  /^(wall-(?:perimeter|cover)-(?:edges|shadows))-.+\.webp$/;
const targetPixels = 1_024;

const assetNames = await readdir(assetsRoot);
const atlases = assetNames.filter((name) => atlasPattern.test(name)).sort();
if (atlases.length !== 16) {
  throw new Error(`Expected 16 wall atlases, found ${atlases.length}.`);
}

const replacements = new Map();
let originalBytes = 0;
let optimizedBytes = 0;
for (const filename of atlases) {
  const source = path.join(assetsRoot, filename);
  const temporary = path.join(assetsRoot, `.${filename}.mobile.webp`);
  originalBytes += (await stat(source)).size;
  // WebP out, not PNG. This previously shelled out to macOS `sips`, which cannot encode
  // WebP — it silently writes nothing for `-s format webp` — so the author had no option
  // but PNG, and the "optimisation" shipped atlases ~30% *larger* than the WebP sources
  // it replaced, at half the resolution. sharp encodes WebP directly, and drops the
  // macOS-only requirement with it.
  await sharp(source)
    .resize(targetPixels, targetPixels, { fit: 'inside', withoutEnlargement: true })
    .webp({ quality: 82, effort: 6 })
    .toFile(temporary);
  const bytes = await readFile(temporary);
  const hash = createHash('sha256').update(bytes).digest('hex').slice(0, 8);
  const prefix = filename.match(atlasPattern)?.[1];
  if (!prefix) throw new Error(`Could not parse atlas filename '${filename}'.`);
  const outputName = `${prefix}-mobile-${hash}.webp`;
  await rename(temporary, path.join(assetsRoot, outputName));
  await unlink(source);
  replacements.set(filename, outputName);
  optimizedBytes += bytes.length;
}

const scriptNames = (await readdir(assetsRoot)).filter((name) =>
  name.endsWith('.js'),
);
if (scriptNames.length !== 1) {
  throw new Error(`Expected one review script, found ${scriptNames.length}.`);
}
const oldScriptName = scriptNames[0];
const oldScriptPath = path.join(assetsRoot, oldScriptName);
let script = await readFile(oldScriptPath, 'utf8');
for (const [before, after] of replacements) script = script.replaceAll(before, after);
for (const before of replacements.keys()) {
  if (script.includes(before))
    throw new Error(`Review script still references '${before}'.`);
}
const scriptHash = createHash('sha256')
  .update(script)
  .digest('hex')
  .slice(0, 8);
const newScriptName = `index-mobile-${scriptHash}.js`;
await writeFile(path.join(assetsRoot, newScriptName), script);
await unlink(oldScriptPath);

const indexPath = path.join(reviewRoot, 'index.html');
const index = (await readFile(indexPath, 'utf8')).replaceAll(
  oldScriptName,
  newScriptName,
);
await writeFile(indexPath, index);

const mib = (bytes) => (bytes / 1_048_576).toFixed(2);
// Say which direction it went. The previous wording reported both numbers without
// comparing them, so a 30% regression read exactly like a saving.
if (optimizedBytes > originalBytes) {
  throw new Error(
    `Atlas optimization made the payload larger: ${mib(originalBytes)} → ` +
      `${mib(optimizedBytes)} MiB. Refusing to ship that.`,
  );
}
console.log(
  `Optimized ${atlases.length} hosted-review atlases to ${targetPixels}px: ` +
    `${mib(originalBytes)} → ${mib(optimizedBytes)} MiB ` +
    `(-${(100 - (optimizedBytes / originalBytes) * 100).toFixed(0)}%).`,
);
