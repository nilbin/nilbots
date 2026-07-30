#!/usr/bin/env node

import { mkdir, readFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { dirname, isAbsolute, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const requireFromWeb = createRequire(join(repository, 'web', 'package.json'));
const sharp = requireFromWeb('sharp');
const configPath = resolve(
  process.argv[2] ?? join(repository, 'art', 'themes', 'ember-forge', 'art.json'),
);
const configDirectory = dirname(configPath);
const config = JSON.parse(await readFile(configPath, 'utf8'));
const runtime = config.runtime3d;

if (
  !runtime ||
  typeof runtime.packagePath !== 'string' ||
  !Array.isArray(runtime.materialMaps)
)
  throw new Error(`${configPath} has no runtime3d material-map recipe.`);

const packagePath = insideRepository(runtime.packagePath);
await mkdir(packagePath, { recursive: true });

const outputs = [];
for (const entry of runtime.materialMaps) {
  if (
    typeof entry.source !== 'string' ||
    typeof entry.runtime !== 'string' ||
    !Number.isInteger(entry.size) ||
    entry.size < 64 ||
    entry.size > 2048 ||
    !Number.isInteger(entry.quality) ||
    entry.quality < 1 ||
    entry.quality > 100
  )
    throw new Error('Invalid runtime3d material-map entry.');

  const source = inside(configDirectory, entry.source);
  const output = inside(packagePath, entry.runtime);
  await sharp(source)
    .resize(entry.size, entry.size, { fit: 'fill' })
    .webp({ quality: entry.quality, effort: 6 })
    .toFile(output);
  outputs.push(relative(repository, output));
}

process.stdout.write(`${JSON.stringify({ config: relative(repository, configPath), outputs })}\n`);

function insideRepository(path) {
  return inside(repository, path);
}

function inside(root, path) {
  if (isAbsolute(path))
    throw new Error(`Expected repository-relative path, received '${path}'.`);
  const output = resolve(root, path);
  if (output !== root && !output.startsWith(`${root}${sep}`))
    throw new Error(`Path escapes '${root}': '${path}'.`);
  return output;
}
