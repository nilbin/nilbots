#!/usr/bin/env node

import { createRequire } from 'node:module';
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const JSON_CHUNK = 0x4e4f534a;
const BIN_CHUNK = 0x004e4942;
const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web/package.json'));
const sharp = requireFromWeb('sharp');
const inputPath = path.resolve(process.argv[2] ?? '');
const outputPath = path.resolve(process.argv[3] ?? '');
const floor = Number(process.argv[4]);

if (!process.argv[2] || !process.argv[3] || !Number.isFinite(floor) || floor < 0 || floor > 1)
  throw new Error(
    'Usage: node scripts/class-models/enforce-matte-roughness.mjs <input.glb> <output.glb> <roughness-floor-0..1>',
  );

const { document, binary } = parseGlb(await readFile(inputPath));
if (document.buffers?.length !== 1 || document.materials?.length !== 1)
  throw new Error('Matte enforcement requires one embedded buffer and one material.');
const material = document.materials[0];
const textureIndex = material.pbrMetallicRoughness?.metallicRoughnessTexture?.index;
const texture = document.textures?.[textureIndex];
const imageIndex = texture?.source ?? texture?.extensions?.EXT_texture_webp?.source;
const image = document.images?.[imageIndex];
const view = document.bufferViews?.[image?.bufferView];
if (textureIndex === undefined || imageIndex === undefined || !image || !view)
  throw new Error('Matte enforcement requires an embedded metallic-roughness texture.');
const encoded = binary.subarray(
  view.byteOffset ?? 0,
  (view.byteOffset ?? 0) + view.byteLength,
);
const { data, info } = await sharp(encoded).raw().toBuffer({ resolveWithObject: true });
if (info.channels < 3) throw new Error('Metallic-roughness texture must provide RGB channels.');
// A floor is a lower bound. Round upward so the 8-bit channel never lands a fraction
// below the declared value (for example, 0.82 otherwise rounds to 209/255 = 0.8196).
const threshold = Math.ceil(floor * 255);
let pixelsRaised = 0;
let minimumBefore = 255;
let minimumAfter = 255;
for (let offset = 0; offset < data.length; offset += info.channels) {
  const roughness = data[offset + 1];
  minimumBefore = Math.min(minimumBefore, roughness);
  if (roughness < threshold) {
    data[offset + 1] = threshold;
    pixelsRaised += 1;
  }
  minimumAfter = Math.min(minimumAfter, data[offset + 1]);
}
const replacement = await sharp(data, { raw: info }).png().toBuffer();
let outputBinary = Buffer.from(binary.subarray(0, document.buffers[0].byteLength));
const padding = (4 - (outputBinary.length % 4)) % 4;
if (padding) outputBinary = Buffer.concat([outputBinary, Buffer.alloc(padding)]);
const replacementView = document.bufferViews.length;
document.bufferViews.push({
  buffer: 0,
  byteOffset: outputBinary.length,
  byteLength: replacement.length,
});
outputBinary = Buffer.concat([outputBinary, replacement]);
image.bufferView = replacementView;
image.mimeType = 'image/png';
document.buffers[0].byteLength = outputBinary.length;
document.asset.generator = `${document.asset?.generator ?? 'unknown'} + Nilbots matte roughness gate 1`;
document.asset.extras = {
  ...(document.asset.extras ?? {}),
  nilbotsMatteRoughness: {
    version: 1,
    floor,
    channel: 'metallicRoughnessTexture.green',
    pixels: info.width * info.height,
    pixelsRaised,
    minimumBefore: minimumBefore / 255,
    minimumAfter: minimumAfter / 255,
    baseColorChanged: false,
    metallicChanged: false,
    normalChanged: false,
  },
};

await writeFile(outputPath, buildGlb(document, outputBinary));
console.log(
  JSON.stringify(
    {
      input: inputPath,
      output: outputPath,
      floor,
      pixelsRaised,
      pixels: info.width * info.height,
      minimumBefore: minimumBefore / 255,
      minimumAfter: minimumAfter / 255,
    },
    null,
    2,
  ),
);

function parseGlb(bytes) {
  if (
    bytes.toString('ascii', 0, 4) !== 'glTF' ||
    bytes.readUInt32LE(4) !== 2 ||
    bytes.readUInt32LE(8) !== bytes.length
  )
    throw new Error('Expected a valid glTF 2.0 binary.');
  let document = null;
  let binary = null;
  let offset = 12;
  while (offset < bytes.length) {
    const length = bytes.readUInt32LE(offset);
    const type = bytes.readUInt32LE(offset + 4);
    const data = bytes.subarray(offset + 8, offset + 8 + length);
    if (type === JSON_CHUNK) document = JSON.parse(data.toString('utf8').trim());
    if (type === BIN_CHUNK) binary = data;
    offset += 8 + length;
  }
  if (!document || !binary) throw new Error('GLB requires JSON and BIN chunks.');
  return { document, binary };
}

function buildGlb(gltf, binary) {
  const json = padded(Buffer.from(JSON.stringify(gltf)), 0x20);
  const bin = padded(binary, 0);
  const totalLength = 12 + 8 + json.length + 8 + bin.length;
  const output = Buffer.alloc(totalLength);
  output.write('glTF', 0, 4, 'ascii');
  output.writeUInt32LE(2, 4);
  output.writeUInt32LE(totalLength, 8);
  output.writeUInt32LE(json.length, 12);
  output.writeUInt32LE(JSON_CHUNK, 16);
  json.copy(output, 20);
  const binHeader = 20 + json.length;
  output.writeUInt32LE(bin.length, binHeader);
  output.writeUInt32LE(BIN_CHUNK, binHeader + 4);
  bin.copy(output, binHeader + 8);
  return output;
}

function padded(value, byte) {
  const padding = (4 - (value.length % 4)) % 4;
  return padding ? Buffer.concat([value, Buffer.alloc(padding, byte)]) : value;
}
