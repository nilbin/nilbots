#!/usr/bin/env node

/** Deterministically separate an already-disconnected bilateral Meshy ornament. */

import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';

const JSON_CHUNK = 0x4e4f534a;
const BIN_CHUNK = 0x004e4942;
const input = path.resolve(process.argv[2] ?? '');
const output = path.resolve(process.argv[3] ?? '');
const targetGapRatio = Number(process.argv[4]);
if (
  !process.argv[2] ||
  !process.argv[3] ||
  !Number.isFinite(targetGapRatio) ||
  targetGapRatio <= 0 ||
  targetGapRatio >= 0.8
)
  throw new Error(
    'Usage: node scripts/class-models/widen-provider-split-gap.mjs ' +
      '<input.glb> <output.glb> <target-gap-to-total-width-ratio>',
  );

const chunks = parseGlb(await readFile(input));
const jsonChunk = chunks.find((chunk) => chunk.type === JSON_CHUNK);
const binaryChunk = chunks.find((chunk) => chunk.type === BIN_CHUNK);
if (!jsonChunk || !binaryChunk) throw new Error('GLB requires JSON and binary chunks.');
const document = JSON.parse(jsonChunk.data.toString('utf8').trim());
const positionAccessors = [];
for (const mesh of document.meshes ?? [])
  for (const primitive of mesh.primitives ?? []) {
    const accessorIndex = primitive.attributes?.POSITION;
    if (Number.isInteger(accessorIndex) && !positionAccessors.includes(accessorIndex))
      positionAccessors.push(accessorIndex);
  }
if (!positionAccessors.length) throw new Error('GLB contains no POSITION accessors.');

const positions = positionAccessors.map((accessorIndex) =>
  denseFloatPositions(document, binaryChunk.data, accessorIndex),
);
let minimum = Infinity;
let maximum = -Infinity;
for (const accessor of positions)
  for (let index = 0; index < accessor.count; index += 1) {
    const x = accessor.read(index, 0);
    minimum = Math.min(minimum, x);
    maximum = Math.max(maximum, x);
  }
const centre = (minimum + maximum) / 2;
let leftMaximum = -Infinity;
let rightMinimum = Infinity;
for (const accessor of positions)
  for (let index = 0; index < accessor.count; index += 1) {
    const x = accessor.read(index, 0);
    if (x <= centre) leftMaximum = Math.max(leftMaximum, x);
    else rightMinimum = Math.min(rightMinimum, x);
  }
if (!Number.isFinite(leftMaximum) || !Number.isFinite(rightMinimum))
  throw new Error('Could not identify both halves around the provider model centre.');
const gapBefore = rightMinimum - leftMaximum;
if (gapBefore <= 0) throw new Error('Provider geometry crosses the intended split centre.');
const totalWidthBefore = maximum - minimum;
const materialWidth = totalWidthBefore - gapBefore;
const targetGap = targetGapRatio * materialWidth / (1 - targetGapRatio);
const offsetPerSide = Math.max(0, (targetGap - gapBefore) / 2);

for (const accessor of positions) {
  const bounds = {
    min: [Infinity, Infinity, Infinity],
    max: [-Infinity, -Infinity, -Infinity],
  };
  for (let index = 0; index < accessor.count; index += 1) {
    const originalX = accessor.read(index, 0);
    accessor.write(
      index,
      0,
      originalX <= centre ? originalX - offsetPerSide : originalX + offsetPerSide,
    );
    for (let axis = 0; axis < 3; axis += 1) {
      const value = accessor.read(index, axis);
      bounds.min[axis] = Math.min(bounds.min[axis], value);
      bounds.max[axis] = Math.max(bounds.max[axis], value);
    }
  }
  accessor.definition.min = bounds.min;
  accessor.definition.max = bounds.max;
}

const gapAfter = gapBefore + offsetPerSide * 2;
const totalWidthAfter = totalWidthBefore + offsetPerSide * 2;
document.asset.generator =
  `${document.asset?.generator ?? 'unknown'} + Nilbots split-clearance gate 1`;
document.asset.extras = {
  ...(document.asset.extras ?? {}),
  nilbotsSplitClearance: {
    version: 1,
    axis: 'provider-x',
    method: 'translate vertices left/right about provider bounds centre; no scaling',
    targetGapRatio,
    centre,
    totalWidthBefore,
    totalWidthAfter,
    gapBefore,
    gapAfter,
    achievedGapRatio: gapAfter / totalWidthAfter,
    offsetPerSide,
  },
};
jsonChunk.data = Buffer.from(JSON.stringify(document));
await writeFile(output, buildGlb(chunks));
console.log(
  JSON.stringify(
    {
      input,
      output,
      targetGapRatio,
      totalWidthBefore,
      totalWidthAfter,
      gapBefore,
      gapAfter,
      achievedGapRatio: gapAfter / totalWidthAfter,
      offsetPerSide,
    },
    null,
    2,
  ),
);

function denseFloatPositions(gltf, binary, accessorIndex) {
  const definition = gltf.accessors?.[accessorIndex];
  const view = gltf.bufferViews?.[definition?.bufferView];
  if (
    !definition ||
    !view ||
    definition.sparse ||
    definition.componentType !== 5126 ||
    definition.type !== 'VEC3' ||
    view.buffer !== 0
  )
    throw new Error('Split clearance requires dense float32 VEC3 positions in buffer 0.');
  const stride = view.byteStride ?? 12;
  const start = (view.byteOffset ?? 0) + (definition.byteOffset ?? 0);
  return {
    definition,
    count: definition.count,
    read: (index, axis) => binary.readFloatLE(start + index * stride + axis * 4),
    write: (index, axis, value) =>
      binary.writeFloatLE(value, start + index * stride + axis * 4),
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
    const data = padding
      ? Buffer.concat([chunk.data, Buffer.alloc(padding, padByte)])
      : chunk.data;
    const header = Buffer.alloc(8);
    header.writeUInt32LE(data.length, 0);
    header.writeUInt32LE(chunk.type, 4);
    return Buffer.concat([header, data]);
  });
  const total = 12 + encoded.reduce((sum, chunk) => sum + chunk.length, 0);
  const header = Buffer.alloc(12);
  header.writeUInt32LE(0x46546c67, 0);
  header.writeUInt32LE(2, 4);
  header.writeUInt32LE(total, 8);
  return Buffer.concat([header, ...encoded]);
}
