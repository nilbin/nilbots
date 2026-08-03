#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';

const JSON_CHUNK = 0x4e4f534a;
const inputPath = resolve(process.argv[2] ?? '');
const outputPath = resolve(process.argv[3] ?? '');
const yawDegrees = Number(process.argv[4]);
if (!process.argv[2] || !process.argv[3] || !Number.isFinite(yawDegrees))
  throw new Error(
    'Usage: node scripts/class-models/correct-provider-facing.mjs <input.glb> <output.glb> <yaw-degrees>',
  );

const source = await readFile(inputPath);
const chunks = parseGlb(source);
const jsonChunk = chunks.find((chunk) => chunk.type === JSON_CHUNK);
if (!jsonChunk) throw new Error('GLB has no JSON chunk.');
const document = JSON.parse(jsonChunk.data.toString('utf8').trim());
const scene = document.scenes?.[document.scene ?? 0];
const rootIndex = scene?.nodes?.[0];
const root = Number.isInteger(rootIndex) ? document.nodes?.[rootIndex] : null;
const normalization = root?.extras?.nilbotsProviderNormalization;
if (root?.name !== 'chassis' || !normalization)
  throw new Error('Expected a normalized provider chassis scene root.');

const currentYaw = normalization.facingYawDegrees ?? 0;
const correctionVersion = normalization.facingCorrectionVersion ?? 0;
let output = source;
if (currentYaw !== yawDegrees || correctionVersion !== 1) {
  if (currentYaw !== 0 && currentYaw !== yawDegrees)
    throw new Error(`Refusing to replace existing ${currentYaw}° facing correction.`);
  if (currentYaw === 0) {
    root.rotation = multiplyQuaternion(
      yawQuaternion(yawDegrees),
      root.rotation ?? [0, 0, 0, 1],
    ).map(cleanNumber);
    // World-yawing a normalized TRS means rotating its translation too. Changing only
    // the quaternion turns around the provider's pre-normalized origin and can move an
    // asymmetric body outside the tile even though its final bounds were centred.
    root.translation = rotateYaw(root.translation ?? [0, 0, 0], yawDegrees)
      .map(cleanNumber);
  }
  normalization.facingYawDegrees = yawDegrees;
  normalization.facingCorrectionVersion = 1;
  jsonChunk.data = Buffer.from(JSON.stringify(document));
  output = buildGlb(chunks);
}

await writeFile(outputPath, output);
console.log(JSON.stringify({
  input: inputPath,
  output: outputPath,
  yawDegrees,
  bytes: output.length,
  sha256: createHash('sha256').update(output).digest('hex'),
}, null, 2));

function yawQuaternion(degrees) {
  const half = degrees * Math.PI / 360;
  return [0, Math.sin(half), 0, Math.cos(half)];
}

/** Compose the world-space facing correction before the provider normalization. */
function multiplyQuaternion(left, right) {
  const [lx, ly, lz, lw] = left;
  const [rx, ry, rz, rw] = right;
  return [
    lw * rx + lx * rw + ly * rz - lz * ry,
    lw * ry - lx * rz + ly * rw + lz * rx,
    lw * rz + lx * ry - ly * rx + lz * rw,
    lw * rw - lx * rx - ly * ry - lz * rz,
  ];
}

function rotateYaw([x, y, z], degrees) {
  const radians = degrees * Math.PI / 180;
  const cosine = Math.cos(radians);
  const sine = Math.sin(radians);
  return [x * cosine + z * sine, y, -x * sine + z * cosine];
}

function cleanNumber(value) {
  if (Math.abs(value) < 1e-12) return 0;
  if (Math.abs(value - 1) < 1e-12) return 1;
  if (Math.abs(value + 1) < 1e-12) return -1;
  return value;
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
