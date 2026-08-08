#!/usr/bin/env node

import { readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import process from 'node:process';

const JSON_CHUNK = 0x4e4f534a;
const inputPath = resolve(process.argv[2] ?? '');
const outputPath = resolve(process.argv[3] ?? '');
const targetSpan = Number(process.argv[4] ?? 0.9);
const orientation = process.argv[5] ?? 'identity';
if (!process.argv[2] || !process.argv[3] || !Number.isFinite(targetSpan) || targetSpan <= 0)
  throw new Error(
    'Usage: node scripts/class-models/normalize-provider-glb.mjs <input.glb> <output.glb> [target-planform-span] [identity|lay-flat-x]',
  );
if (!['identity', 'lay-flat-x'].includes(orientation))
  throw new Error('Orientation must be identity or lay-flat-x.');

const source = await readFile(inputPath);
const chunks = parseGlb(source);
const jsonChunk = chunks.find((chunk) => chunk.type === JSON_CHUNK);
if (!jsonChunk) throw new Error('GLB has no JSON chunk.');
const document = JSON.parse(jsonChunk.data.toString('utf8').trim());
const sceneIndex = document.scene ?? 0;
const scene = document.scenes?.[sceneIndex];
if (!scene || !Array.isArray(scene.nodes) || scene.nodes.length === 0)
  throw new Error('GLB has no default scene roots.');

const bounds = meshBounds(document);
const orientedBounds = transformBounds(bounds, orientation);
const width = orientedBounds.max[0] - orientedBounds.min[0];
const depth = orientedBounds.max[2] - orientedBounds.min[2];
const scale = targetSpan / Math.max(width, depth);
const centreX = (orientedBounds.min[0] + orientedBounds.max[0]) / 2;
const centreZ = (orientedBounds.min[2] + orientedBounds.max[2]) / 2;
const sourceRoots = [...scene.nodes];
const chassisIndex = document.nodes.length;
document.nodes.push({
  name: 'chassis',
  children: sourceRoots,
  scale: [scale, scale, scale],
  ...(orientation === 'lay-flat-x'
    ? { rotation: [-Math.SQRT1_2, 0, 0, Math.SQRT1_2] }
    : {}),
  translation: [
    -centreX * scale,
    -orientedBounds.min[1] * scale,
    -centreZ * scale,
  ],
  extras: {
    nilbotsProviderNormalization: {
      facing: '+x',
      up: '+y',
      floorY: 0,
      orientation,
      targetPlanformSpan: targetSpan,
      sourceBounds: bounds,
    },
  },
});
for (const name of [
  'underbody-locomotion',
  'weapon-hardware',
  'team-accents',
  'emissives',
]) {
  const nodeIndex = document.nodes.length;
  document.nodes.push({ name });
  document.nodes[chassisIndex].children.push(nodeIndex);
}
scene.nodes = [chassisIndex];
document.asset.generator = `${document.asset?.generator ?? 'unknown'} + Nilbots provider normalization 1`;

jsonChunk.data = Buffer.from(JSON.stringify(document));
const output = buildGlb(chunks);
await writeFile(outputPath, output);
console.log(
  JSON.stringify(
    {
      input: inputPath,
      output: outputPath,
      bytes: output.length,
      sourceBounds: bounds,
      orientation,
      scale,
      targetPlanformSpan: targetSpan,
      normalizedBounds: {
        min: [
          (orientedBounds.min[0] - centreX) * scale,
          0,
          (orientedBounds.min[2] - centreZ) * scale,
        ],
        max: [
          (orientedBounds.max[0] - centreX) * scale,
          (orientedBounds.max[1] - orientedBounds.min[1]) * scale,
          (orientedBounds.max[2] - centreZ) * scale,
        ],
      },
    },
    null,
    2,
  ),
);

function meshBounds(gltf) {
  const accessors = [];
  for (const mesh of gltf.meshes ?? [])
    for (const primitive of mesh.primitives ?? []) {
      const index = primitive.attributes?.POSITION;
      if (Number.isInteger(index)) accessors.push(gltf.accessors?.[index]);
    }
  if (accessors.length === 0 || accessors.some((entry) => !entry?.min || !entry?.max))
    throw new Error('Every POSITION accessor must provide min/max bounds.');
  return {
    min: [0, 1, 2].map((axis) => Math.min(...accessors.map((entry) => entry.min[axis]))),
    max: [0, 1, 2].map((axis) => Math.max(...accessors.map((entry) => entry.max[axis]))),
  };
}

function transformBounds(bounds, selectedOrientation) {
  const points = [];
  for (const x of [bounds.min[0], bounds.max[0]])
    for (const y of [bounds.min[1], bounds.max[1]])
      for (const z of [bounds.min[2], bounds.max[2]])
        points.push(
          selectedOrientation === 'lay-flat-x'
            ? [x, z, -y]
            : [x, y, z],
        );
  return {
    min: [0, 1, 2].map((axis) => Math.min(...points.map((point) => point[axis]))),
    max: [0, 1, 2].map((axis) => Math.max(...points.map((point) => point[axis]))),
  };
}

function parseGlb(bytes) {
  if (bytes.readUInt32LE(0) !== 0x46546c67 || bytes.readUInt32LE(4) !== 2)
    throw new Error('Expected a glTF 2.0 binary.');
  const chunks = [];
  let offset = 12;
  while (offset < bytes.length) {
    const length = bytes.readUInt32LE(offset);
    const type = bytes.readUInt32LE(offset + 4);
    chunks.push({ type, data: Buffer.from(bytes.subarray(offset + 8, offset + 8 + length)) });
    offset += 8 + length;
  }
  return chunks;
}

function buildGlb(chunks) {
  const encoded = chunks.map((chunk) => {
    const padding = (4 - (chunk.data.length % 4)) % 4;
    const padByte = chunk.type === JSON_CHUNK ? 0x20 : 0;
    const data = padding === 0
      ? chunk.data
      : Buffer.concat([chunk.data, Buffer.alloc(padding, padByte)]);
    const header = Buffer.alloc(8);
    header.writeUInt32LE(data.length, 0);
    header.writeUInt32LE(chunk.type, 4);
    return Buffer.concat([header, data]);
  });
  const totalLength = 12 + encoded.reduce((sum, chunk) => sum + chunk.length, 0);
  const header = Buffer.alloc(12);
  header.writeUInt32LE(0x46546c67, 0);
  header.writeUInt32LE(2, 4);
  header.writeUInt32LE(totalLength, 8);
  return Buffer.concat([header, ...encoded]);
}
