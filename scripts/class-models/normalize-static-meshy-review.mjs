#!/usr/bin/env node

/**
 * Wrap one static Meshy GLB in Nilbots' +X/+Y, one-tile review transform.
 *
 * This is deliberately a JSON-only GLB edit. Geometry, indices, embedded PBR
 * images, and the BIN chunk remain byte-for-byte identical to the provider
 * master. It exists for honest in-game review of a static provider result, not
 * as an optimization or production-approval step.
 */

import { createHash } from 'node:crypto';
import { readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';

const options = parseArguments(process.argv.slice(2));
const inputPath = resolve(options.input);
const outputPath = resolve(options.output);
const reportPath = resolve(options.report);
const input = await readFile(inputPath);
const inputSha256 = sha256(input);
assertEqual(inputSha256, options.expectedSha, 'input SHA-256');

const glb = parseGlb(input);
const document = glb.json;
assertEqual(document.scene ?? 0, 0, 'default scene');
assertEqual(document.scenes?.length, 1, 'scene count');
assertEqual(document.scenes[0]?.nodes?.length, 1, 'root node count');
assertEqual(document.nodes?.length, 1, 'node count');
assertEqual(document.meshes?.length, 1, 'mesh count');
assertEqual(document.materials?.length, 1, 'material count');
assertEqual(document.animations?.length ?? 0, 0, 'animation count');
assertEqual(document.skins?.length ?? 0, 0, 'skin count');

const rootIndex = document.scenes[0].nodes[0];
const root = document.nodes[rootIndex];
assertEqual(root.mesh, 0, 'root mesh index');
assertDeepEqual(
  root.matrix,
  [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1],
  'provider root matrix',
);

const primitives = document.meshes[0].primitives ?? [];
assertEqual(primitives.length, 1, 'primitive count');
const primitive = primitives[0];
if (primitive.targets?.length) throw new Error('Morph targets are not static review input.');
const positionAccessor = document.accessors?.[primitive.attributes?.POSITION];
if (
  !positionAccessor ||
  !Array.isArray(positionAccessor.min) ||
  !Array.isArray(positionAccessor.max) ||
  positionAccessor.min.length !== 3 ||
  positionAccessor.max.length !== 3
) {
  throw new Error('POSITION accessor must carry three-dimensional min/max bounds.');
}
const sourceBounds = {
  min: positionAccessor.min.map(Number),
  max: positionAccessor.max.map(Number),
};
const sourceExtents = sourceBounds.max.map(
  (maximum, index) => maximum - sourceBounds.min[index],
);
const sourcePlanform =
  options.sourceUp === 'z'
    ? Math.max(sourceExtents[0], sourceExtents[1])
    : Math.max(sourceExtents[0], sourceExtents[2]);
if (!(sourcePlanform > 0)) throw new Error('Source planform span must be positive.');
const scale = options.span / sourcePlanform;
const oriented = orientedBounds(sourceBounds, options.sourceUp, scale);
const translation = [
  -(oriented.min[0] + oriented.max[0]) / 2,
  -oriented.min[1],
  -(oriented.min[2] + oriented.max[2]) / 2,
];
root.matrix = reviewMatrix(options.sourceUp, scale, translation);
root.name = root.name ?? `NilbotsReview.${options.id}`;
document.asset.extras = {
  ...(document.asset.extras ?? {}),
  nilbotsStaticReview: {
    id: options.id,
    sourceSha256: inputSha256,
    sourceFacing: '-x',
    sourceUp: `+${options.sourceUp}`,
    facing: '+x',
    up: '+y',
    planformSpan: options.span,
    geometryAndBinUnchanged: true,
    animations: 'none',
  },
};

const output = encodeGlb(document, glb.trailingChunks);
const outputSha256 = sha256(output);
await writeFile(outputPath, output, { flag: 'wx' });
const outputParsed = parseGlb(output);
for (const [index, chunk] of glb.trailingChunks.entries()) {
  const after = outputParsed.trailingChunks[index];
  assertEqual(after?.type, chunk.type, `chunk ${index} type`);
  assertEqual(sha256(after?.data ?? Buffer.alloc(0)), sha256(chunk.data), `chunk ${index} payload`);
}
const targetBounds = {
  min: oriented.min.map((value, index) => value + translation[index]),
  max: oriented.max.map((value, index) => value + translation[index]),
};
const indexAccessor = document.accessors?.[primitive.indices];
const triangles = indexAccessor ? indexAccessor.count / 3 : positionAccessor.count / 3;
if (!Number.isInteger(triangles)) throw new Error('Triangle count is not integral.');
const report = {
  schemaVersion: 1,
  status: 'PASS',
  purpose: 'static rich Meshy provider master in-game review',
  id: options.id,
  input: {
    path: inputPath,
    bytes: input.length,
    sha256: inputSha256,
    sourceFacing: '-x',
    sourceUp: `+${options.sourceUp}`,
  },
  output: {
    path: outputPath,
    bytes: output.length,
    sha256: outputSha256,
    facing: '+x',
    up: '+y',
    planformSpan: options.span,
  },
  transform: {
    scale,
    translation,
    matrix: root.matrix,
    sourceBounds,
    targetBounds,
  },
  geometry: {
    meshes: document.meshes.length,
    primitives: primitives.length,
    vertices: positionAccessor.count,
    triangles,
  },
  pbr: {
    materials: document.materials.length,
    textures: document.textures?.length ?? 0,
    images: document.images?.length ?? 0,
  },
  checks: {
    exactExpectedSource: true,
    staticNoAnimationSkinOrMorph: true,
    singleFusedProviderMesh: true,
    geometryIndicesAndEmbeddedTexturesByteExact: true,
    rigidAxisFacingScaleTranslationOnly: true,
    floorAligned: Math.abs(targetBounds.min[1]) < 1e-12,
    planformCentered:
      Math.abs(targetBounds.min[0] + targetBounds.max[0]) < 1e-12 &&
      Math.abs(targetBounds.min[2] + targetBounds.max[2]) < 1e-12,
  },
};
if (!Object.values(report.checks).every(Boolean)) throw new Error('Review normalization checks failed.');
await writeFile(reportPath, `${JSON.stringify(report, null, 2)}\n`, { flag: 'wx' });
process.stdout.write(`${JSON.stringify({ id: options.id, outputSha256, triangles, targetBounds })}\n`);

function orientedBounds(bounds, sourceUp, scale) {
  if (sourceUp === 'z') {
    return {
      min: [-bounds.max[0] * scale, bounds.min[2] * scale, bounds.min[1] * scale],
      max: [-bounds.min[0] * scale, bounds.max[2] * scale, bounds.max[1] * scale],
    };
  }
  return {
    min: [-bounds.max[0] * scale, bounds.min[1] * scale, -bounds.max[2] * scale],
    max: [-bounds.min[0] * scale, bounds.max[1] * scale, -bounds.min[2] * scale],
  };
}

function reviewMatrix(sourceUp, scale, [x, y, z]) {
  return sourceUp === 'z'
    ? [-scale, 0, 0, 0, 0, 0, scale, 0, 0, scale, 0, 0, x, y, z, 1]
    : [-scale, 0, 0, 0, 0, scale, 0, 0, 0, 0, -scale, 0, x, y, z, 1];
}

function parseGlb(bytes) {
  assertEqual(bytes.toString('ascii', 0, 4), 'glTF', 'GLB magic');
  assertEqual(bytes.readUInt32LE(4), 2, 'GLB version');
  assertEqual(bytes.readUInt32LE(8), bytes.length, 'GLB declared length');
  const chunks = [];
  for (let offset = 12; offset < bytes.length; ) {
    const length = bytes.readUInt32LE(offset);
    const type = bytes.readUInt32LE(offset + 4);
    const data = bytes.subarray(offset + 8, offset + 8 + length);
    chunks.push({ type, data });
    offset += 8 + length;
  }
  assertEqual(chunks[0]?.type, 0x4e4f534a, 'first chunk type');
  const json = JSON.parse(chunks[0].data.toString('utf8').replace(/[\u0000\u0020]+$/u, ''));
  return { json, trailingChunks: chunks.slice(1) };
}

function encodeGlb(document, trailingChunks) {
  const source = Buffer.from(JSON.stringify(document), 'utf8');
  const padding = (4 - (source.length % 4)) % 4;
  const json = Buffer.concat([source, Buffer.alloc(padding, 0x20)]);
  const total = 12 + 8 + json.length + trailingChunks.reduce((sum, chunk) => sum + 8 + chunk.data.length, 0);
  const output = Buffer.allocUnsafe(total);
  output.write('glTF', 0, 'ascii');
  output.writeUInt32LE(2, 4);
  output.writeUInt32LE(total, 8);
  let offset = 12;
  output.writeUInt32LE(json.length, offset);
  output.writeUInt32LE(0x4e4f534a, offset + 4);
  json.copy(output, offset + 8);
  offset += 8 + json.length;
  for (const chunk of trailingChunks) {
    output.writeUInt32LE(chunk.data.length, offset);
    output.writeUInt32LE(chunk.type, offset + 4);
    chunk.data.copy(output, offset + 8);
    offset += 8 + chunk.data.length;
  }
  return output;
}

function parseArguments(arguments_) {
  const result = {};
  const values = new Map([
    ['--id', 'id'],
    ['--input', 'input'],
    ['--expected-sha', 'expectedSha'],
    ['--source-up', 'sourceUp'],
    ['--span', 'span'],
    ['--output', 'output'],
    ['--report', 'report'],
  ]);
  for (let index = 0; index < arguments_.length; index += 1) {
    const key = values.get(arguments_[index]);
    if (!key) throw new Error(`Unknown option: ${arguments_[index]}`);
    const value = arguments_[index + 1];
    if (!value || value.startsWith('--')) throw new Error(`${arguments_[index]} requires a value.`);
    result[key] = value;
    index += 1;
  }
  for (const key of values.values()) if (!(key in result)) throw new Error(`Missing --${key.replace(/[A-Z]/g, (c) => `-${c.toLowerCase()}`)}.`);
  if (!/^[a-z0-9-]+$/u.test(result.id)) throw new Error('Unsafe --id.');
  if (!/^[a-f0-9]{64}$/u.test(result.expectedSha)) throw new Error('--expected-sha must be lowercase SHA-256.');
  if (!['y', 'z'].includes(result.sourceUp)) throw new Error('--source-up must be y or z.');
  result.span = Number(result.span);
  if (!(result.span > 0 && result.span <= 1.25)) throw new Error('--span must be in (0, 1.25].');
  return result;
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

function assertEqual(observed, expected, label) {
  if (observed !== expected) throw new Error(`${label}: expected ${expected}, received ${observed}.`);
}

function assertDeepEqual(observed, expected, label) {
  if (JSON.stringify(observed) !== JSON.stringify(expected)) {
    throw new Error(`${label}: expected ${JSON.stringify(expected)}, received ${JSON.stringify(observed)}.`);
  }
}
