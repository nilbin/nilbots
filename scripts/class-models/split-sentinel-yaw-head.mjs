#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';

const JSON_CHUNK = 0x4e4f534a;
const BIN_CHUNK = 0x004e4942;
const sourcePath = path.resolve(process.argv[2] ?? '');
const outputPath = path.resolve(process.argv[3] ?? '');
const componentCentroidCutY = Number(process.argv[4] ?? -0.02);

if (!process.argv[2] || !process.argv[3] || !Number.isFinite(componentCentroidCutY))
  throw new Error(
    'Usage: node scripts/class-models/split-sentinel-yaw-head.mjs <source.glb> <output.glb> [component-centroid-cut-y]',
  );

const sourceBytes = await readFile(sourcePath);
const glb = parseGlb(sourceBytes);
const document = structuredClone(glb.document);
if (
  document.buffers?.length !== 1 ||
  document.meshes?.length !== 1 ||
  document.meshes[0].primitives?.length !== 1
)
  throw new Error('Sentinel split requires one embedded buffer, mesh, and primitive.');
const primitive = document.meshes[0].primitives[0];
if (primitive.mode !== undefined && primitive.mode !== 4)
  throw new Error('Sentinel split requires TRIANGLES mode.');
const positionAccessorIndex = primitive.attributes?.POSITION;
const indexAccessorIndex = primitive.indices;
const positionAccessor = document.accessors?.[positionAccessorIndex];
const indexAccessor = document.accessors?.[indexAccessorIndex];
if (
  !positionAccessor ||
  positionAccessor.componentType !== 5126 ||
  positionAccessor.type !== 'VEC3' ||
  !indexAccessor ||
  indexAccessor.componentType !== 5125 ||
  indexAccessor.type !== 'SCALAR' ||
  indexAccessor.count % 3 !== 0
)
  throw new Error('Sentinel split expects float VEC3 positions and uint32 triangle indices.');

const positions = accessorArray(document, glb.binary, positionAccessorIndex, Float32Array, 3);
const indices = accessorArray(document, glb.binary, indexAccessorIndex, Uint32Array, 1);
const parents = new Int32Array(positionAccessor.count);
const ranks = new Uint8Array(positionAccessor.count);
for (let index = 0; index < parents.length; index += 1) parents[index] = index;
for (let index = 0; index < indices.length; index += 3) {
  union(indices[index], indices[index + 1]);
  union(indices[index], indices[index + 2]);
}

const components = new Map();
for (let index = 0; index < indices.length; index += 3) {
  const root = find(indices[index]);
  let component = components.get(root);
  if (!component) {
    component = { triangleCount: 0, ySum: 0, vertexReferences: 0 };
    components.set(root, component);
  }
  component.triangleCount += 1;
  for (let corner = 0; corner < 3; corner += 1) {
    component.ySum += positions[indices[index + corner] * 3 + 1];
    component.vertexReferences += 1;
  }
}
for (const component of components.values()) {
  component.centroidY = component.ySum / component.vertexReferences;
  component.partition = component.centroidY >= componentCentroidCutY ? 'yaw-head' : 'stationary-base';
}

const base = [];
const head = [];
const membership = Buffer.alloc(Math.ceil(indices.length / 3 / 8));
for (let index = 0; index < indices.length; index += 3) {
  const triangleIndex = index / 3;
  const component = components.get(find(indices[index]));
  const target = component.partition === 'yaw-head' ? head : base;
  if (component.partition === 'yaw-head')
    membership[triangleIndex >> 3] |= 1 << (triangleIndex & 7);
  target.push(indices[index], indices[index + 1], indices[index + 2]);
}
if (!base.length || !head.length || base.length + head.length !== indices.length)
  throw new Error('Sentinel split produced an invalid partition.');

const baseIndices = Uint32Array.from(base);
const headIndices = Uint32Array.from(head);
const indexView = document.bufferViews[indexAccessor.bufferView];
const indexOffset = (indexView.byteOffset ?? 0) + (indexAccessor.byteOffset ?? 0);
const indexBytes = indices.byteLength;
if (indexAccessor.byteOffset && indexAccessor.byteOffset !== 0)
  throw new Error('Sentinel split expects the index accessor to begin at its buffer view origin.');
if (indexView.byteLength !== indexBytes)
  throw new Error('Sentinel split expects a dedicated source index buffer view.');
const binary = Buffer.from(glb.binary);
Buffer.from(baseIndices.buffer).copy(binary, indexOffset);
Buffer.from(headIndices.buffer).copy(binary, indexOffset + baseIndices.byteLength);

indexView.byteLength = baseIndices.byteLength;
indexAccessor.count = baseIndices.length;
const headBufferViewIndex = document.bufferViews.length;
document.bufferViews.push({
  buffer: indexView.buffer ?? 0,
  byteOffset: indexOffset + baseIndices.byteLength,
  byteLength: headIndices.byteLength,
  target: indexView.target,
});
const headAccessorIndex = document.accessors.length;
document.accessors.push({
  bufferView: headBufferViewIndex,
  byteOffset: 0,
  componentType: 5125,
  normalized: false,
  count: headIndices.length,
  type: 'SCALAR',
});

const sourceMesh = document.meshes[0];
sourceMesh.name = 'stationary-base';
sourceMesh.extras = {
  ...(sourceMesh.extras ?? {}),
  nilbotsRole: 'stationary-base',
};
const headPrimitive = structuredClone(primitive);
headPrimitive.indices = headAccessorIndex;
document.meshes.push({
  name: 'yaw-head',
  primitives: [headPrimitive],
  extras: { nilbotsRole: 'yaw-head' },
});

const meshNodeIndex = document.nodes.findIndex((node) => node.mesh === 0);
if (meshNodeIndex < 0) throw new Error('Sentinel source mesh node is missing.');
document.nodes[meshNodeIndex] = {
  ...document.nodes[meshNodeIndex],
  name: 'stationary-base',
  extras: {
    ...(document.nodes[meshNodeIndex].extras ?? {}),
    nilbotsRole: 'stationary-base',
  },
};
const yawHeadMeshNodeIndex = document.nodes.length;
document.nodes.push({
  name: 'yaw-head-mesh',
  mesh: 1,
  extras: { nilbotsRole: 'yaw-head-mesh' },
});
const yawHeadNodeIndex = document.nodes.length;
document.nodes.push({
  name: 'yaw-head',
  children: [yawHeadMeshNodeIndex],
  extras: {
    nilbotsRole: 'yaw-head',
    yawAxis: '+y',
    pivot: [0, 0, 0],
  },
});
const parentNodeIndex = document.nodes.findIndex((node) =>
  (node.children ?? []).includes(meshNodeIndex),
);
if (parentNodeIndex < 0) throw new Error('Sentinel chassis node is missing.');
document.nodes[parentNodeIndex].children.splice(
  document.nodes[parentNodeIndex].children.indexOf(meshNodeIndex) + 1,
  0,
  yawHeadNodeIndex,
);
document.nodes[parentNodeIndex].extras = {
  ...(document.nodes[parentNodeIndex].extras ?? {}),
  nilbotsSentinelPartition: {
    version: 1,
    method: 'indexed connected components classified by source-space centroid Y',
    componentCentroidCutY,
    stationaryNode: 'stationary-base',
    yawNode: 'yaw-head',
    yawAxis: '+y',
    pivot: [0, 0, 0],
    preservesEverySourceTriangleAtRest: true,
  },
};
document.asset.generator = `${document.asset?.generator ?? 'unknown'} + Nilbots Sentinel yaw split 1`;
document.asset.extras = {
  ...(document.asset.extras ?? {}),
  nilbotsSentinelYawSplit: {
    version: 1,
    source: path.basename(sourcePath),
    sourceSha256: sha256(sourceBytes),
    componentCentroidCutY,
    componentCount: components.size,
    stationaryComponents: [...components.values()].filter(
      (component) => component.partition === 'stationary-base',
    ).length,
    yawHeadComponents: [...components.values()].filter(
      (component) => component.partition === 'yaw-head',
    ).length,
    sourceTriangles: indices.length / 3,
    stationaryTriangles: baseIndices.length / 3,
    yawHeadTriangles: headIndices.length / 3,
    partitionMembershipSha256: sha256(membership),
    partitionUnionVerified: true,
  },
};

const outputBytes = buildGlb(document, binary);
await writeFile(outputPath, outputBytes);
console.log(
  JSON.stringify(
    {
      source: sourcePath,
      output: outputPath,
      sourceSha256: sha256(sourceBytes),
      outputSha256: sha256(outputBytes),
      componentCentroidCutY,
      components: components.size,
      stationaryTriangles: baseIndices.length / 3,
      yawHeadTriangles: headIndices.length / 3,
      sourceTriangles: indices.length / 3,
      partitionUnionVerified: true,
    },
    null,
    2,
  ),
);

function find(value) {
  let root = value;
  while (parents[root] !== root) root = parents[root];
  while (parents[value] !== value) {
    const next = parents[value];
    parents[value] = root;
    value = next;
  }
  return root;
}

function union(left, right) {
  let leftRoot = find(left);
  let rightRoot = find(right);
  if (leftRoot === rightRoot) return;
  if (ranks[leftRoot] < ranks[rightRoot]) [leftRoot, rightRoot] = [rightRoot, leftRoot];
  parents[rightRoot] = leftRoot;
  if (ranks[leftRoot] === ranks[rightRoot]) ranks[leftRoot] += 1;
}

function accessorArray(gltf, binary, accessorIndex, TypedArray, itemSize) {
  const accessor = gltf.accessors[accessorIndex];
  const view = gltf.bufferViews[accessor.bufferView];
  if ((view.byteStride ?? TypedArray.BYTES_PER_ELEMENT * itemSize) !== TypedArray.BYTES_PER_ELEMENT * itemSize)
    throw new Error(`Accessor ${accessorIndex} is interleaved.`);
  const byteOffset = (view.byteOffset ?? 0) + (accessor.byteOffset ?? 0);
  return new TypedArray(
    binary.buffer,
    binary.byteOffset + byteOffset,
    accessor.count * itemSize,
  );
}

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

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
