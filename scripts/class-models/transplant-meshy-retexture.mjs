#!/usr/bin/env node

import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';

const JSON_CHUNK = 0x4e4f534a;
const BIN_CHUNK = 0x004e4942;
const geometryPath = path.resolve(process.argv[2] ?? '');
const textureDonorPath = path.resolve(process.argv[3] ?? '');
const outputPath = path.resolve(process.argv[4] ?? '');

if (!process.argv[2] || !process.argv[3] || !process.argv[4])
  throw new Error(
    'Usage: node scripts/class-models/transplant-meshy-retexture.mjs <geometry.glb> <texture-donor.glb> <output.glb>',
  );

const geometryGlb = parseGlb(await readFile(geometryPath));
const donorGlb = parseGlb(await readFile(textureDonorPath));
const document = geometryGlb.document;
const donor = donorGlb.document;

if (document.buffers?.length !== 1 || donor.buffers?.length !== 1)
  throw new Error('Retexture transplant requires one embedded buffer in each GLB.');
if (donor.materials?.length !== 1 || !donor.images?.length || !donor.textures?.length)
  throw new Error('Retexture donor must contain one material and embedded texture images.');
if (
  (document.meshes ?? []).some((mesh) =>
    (mesh.primitives ?? []).some((primitive) => primitive.attributes?.TEXCOORD_0 === undefined),
  )
)
  throw new Error('Geometry source must provide TEXCOORD_0 for the preserved original UVs.');

const geometryByteLength = document.buffers[0].byteLength;
let binary = Buffer.from(geometryGlb.binary.subarray(0, geometryByteLength));
const images = [];
for (const [imageIndex, image] of donor.images.entries()) {
  const view = donor.bufferViews?.[image.bufferView];
  if (!view || (view.buffer ?? 0) !== 0)
    throw new Error(`Donor image ${imageIndex} is not embedded in buffer 0.`);
  const padding = (4 - (binary.length % 4)) % 4;
  if (padding) binary = Buffer.concat([binary, Buffer.alloc(padding)]);
  const encoded = donorGlb.binary.subarray(
    view.byteOffset ?? 0,
    (view.byteOffset ?? 0) + view.byteLength,
  );
  const bufferView = document.bufferViews.length;
  document.bufferViews.push({
    buffer: 0,
    byteOffset: binary.length,
    byteLength: encoded.length,
  });
  binary = Buffer.concat([binary, encoded]);
  images.push({ ...image, bufferView });
}

document.images = images;
document.textures = structuredClone(donor.textures);
document.samplers = structuredClone(donor.samplers ?? []);
document.materials = structuredClone(donor.materials);
document.buffers[0].byteLength = binary.length;
document.asset.generator =
  `${document.asset?.generator ?? 'unknown'} + Meshy original-UV retexture transplant 1`;
document.asset.extras = {
  ...(document.asset.extras ?? {}),
  nilbotsRetextureTransplant: {
    version: 1,
    geometrySource: path.basename(geometryPath),
    textureDonor: path.basename(textureDonorPath),
    preservedOriginalUvs: true,
    donorImages: images.length,
  },
};

await writeFile(outputPath, buildGlb(document, binary));
console.log(
  JSON.stringify(
    {
      geometry: geometryPath,
      textureDonor: textureDonorPath,
      output: outputPath,
      bytes: binary.length,
      donorImages: images.length,
      preservedOriginalUvs: true,
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
