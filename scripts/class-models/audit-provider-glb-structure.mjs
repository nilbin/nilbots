#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const input = path.resolve(process.argv[2] ?? '');
const output = path.resolve(process.argv[3] ?? '');
const tolerance = Number(process.argv[4] ?? 1e-5);
if (!process.argv[2] || !process.argv[3] || !Number.isFinite(tolerance) || tolerance <= 0)
  throw new Error(
    'Usage: node scripts/class-models/audit-provider-glb-structure.mjs ' +
      '<input.glb> <output.json> [position-tolerance]',
  );

const bytes = readFileSync(input);
const { document, binary } = readGlb(bytes);
if ((document.meshes?.length ?? 0) !== 1 || document.meshes[0].primitives?.length !== 1)
  throw new Error('This audit currently expects one Meshy mesh and one primitive.');
const primitive = document.meshes[0].primitives[0];
if ((primitive.mode ?? 4) !== 4 || primitive.indices === undefined)
  throw new Error('This audit requires an indexed triangle primitive.');
const positions = readAccessor(document, binary, primitive.attributes?.POSITION);
const indices = readAccessor(document, binary, primitive.indices);
if (positions.components !== 3 || indices.components !== 1 || indices.count % 3 !== 0)
  throw new Error('Unexpected POSITION or index accessor shape.');

const vertexCount = positions.count;
const triangleCount = indices.count / 3;
const exactParents = new Int32Array(vertexCount);
const exactRanks = new Uint8Array(vertexCount);
for (let index = 0; index < vertexCount; index += 1) exactParents[index] = index;

const spatialIds = new Int32Array(vertexCount);
const positionCells = new Map();
let uniquePositionCells = 0;
for (let vertex = 0; vertex < vertexCount; vertex += 1) {
  const x = positions.value(vertex, 0);
  const y = positions.value(vertex, 1);
  const z = positions.value(vertex, 2);
  const key = `${Math.round(x / tolerance)},${Math.round(y / tolerance)},${Math.round(z / tolerance)}`;
  let spatialId = positionCells.get(key);
  if (spatialId === undefined) {
    spatialId = uniquePositionCells++;
    positionCells.set(key, spatialId);
  }
  spatialIds[vertex] = spatialId;
}
const spatialParents = new Int32Array(uniquePositionCells);
const spatialRanks = new Uint8Array(uniquePositionCells);
for (let index = 0; index < uniquePositionCells; index += 1) spatialParents[index] = index;

let collapsedTriangles = 0;
let zeroAreaTriangles = 0;
for (let triangle = 0; triangle < triangleCount; triangle += 1) {
  const a = indices.value(triangle * 3, 0);
  const b = indices.value(triangle * 3 + 1, 0);
  const c = indices.value(triangle * 3 + 2, 0);
  union(exactParents, exactRanks, a, b);
  union(exactParents, exactRanks, b, c);
  union(spatialParents, spatialRanks, spatialIds[a], spatialIds[b]);
  union(spatialParents, spatialRanks, spatialIds[b], spatialIds[c]);
  if (spatialIds[a] === spatialIds[b] || spatialIds[b] === spatialIds[c] || spatialIds[c] === spatialIds[a])
    collapsedTriangles += 1;
  if (triangleAreaSquared(positions, a, b, c) <= 1e-24) zeroAreaTriangles += 1;
}

const referencedVertices = new Uint8Array(vertexCount);
const exactRoots = new Set();
const spatialRoots = new Set();
for (let index = 0; index < indices.count; index += 1) {
  const vertex = indices.value(index, 0);
  if (referencedVertices[vertex]) continue;
  referencedVertices[vertex] = 1;
  exactRoots.add(find(exactParents, vertex));
  spatialRoots.add(find(spatialParents, spatialIds[vertex]));
}

const report = {
  schemaVersion: 1,
  source: {
    file: path.basename(input),
    bytes: bytes.length,
    sha256: createHash('sha256').update(bytes).digest('hex'),
  },
  gltf: {
    generator: document.asset?.generator ?? null,
    scenes: document.scenes?.length ?? 0,
    nodes: document.nodes?.length ?? 0,
    meshes: document.meshes?.length ?? 0,
    primitives: document.meshes?.reduce((sum, mesh) => sum + (mesh.primitives?.length ?? 0), 0),
    materials: document.materials?.length ?? 0,
    animations: document.animations?.length ?? 0,
    skins: document.skins?.length ?? 0,
  },
  bounds: {
    min: document.accessors?.[primitive.attributes.POSITION]?.min ?? null,
    max: document.accessors?.[primitive.attributes.POSITION]?.max ?? null,
  },
  vertices: vertexCount,
  triangles: triangleCount,
  indexedConnectedComponents: exactRoots.size,
  spatialAudit: {
    positionTolerance: tolerance,
    uniquePositionCells,
    duplicatePositionVertices: vertexCount - uniquePositionCells,
    connectedComponents: spatialRoots.size,
    collapsedTriangles,
    zeroAreaTriangles,
  },
  semanticPartContract: {
    explicitSceneNodes: document.nodes?.length ?? 0,
    explicitMeshes: document.meshes?.length ?? 0,
    explicitPrimitives: document.meshes?.reduce(
      (sum, mesh) => sum + (mesh.primitives?.length ?? 0),
      0,
    ),
    providerOutputIsMonolithic: (document.nodes?.length ?? 0) === 1 &&
      (document.meshes?.length ?? 0) === 1 &&
      document.meshes[0].primitives?.length === 1,
  },
  method:
    'Union indexed triangle vertices, then repeat after quantizing POSITION values to the recorded tolerance. No geometry is modified.',
};
writeFileSync(output, `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));

function readGlb(value) {
  if (
    value.toString('ascii', 0, 4) !== 'glTF' ||
    value.readUInt32LE(4) !== 2 ||
    value.readUInt32LE(8) !== value.length
  )
    throw new Error('Invalid glTF 2.0 binary header.');
  let offset = 12;
  let document;
  let binary;
  while (offset < value.length) {
    const length = value.readUInt32LE(offset);
    const type = value.readUInt32LE(offset + 4);
    const data = value.subarray(offset + 8, offset + 8 + length);
    if (type === 0x4e4f534a) document = JSON.parse(data.toString('utf8').trim());
    if (type === 0x004e4942) binary = data;
    offset += 8 + length;
  }
  if (!document || !binary) throw new Error('GLB requires JSON and BIN chunks.');
  return { document, binary };
}

function readAccessor(document, binary, accessorIndex) {
  const accessor = document.accessors?.[accessorIndex];
  const view = document.bufferViews?.[accessor?.bufferView];
  if (!accessor || !view || accessor.sparse)
    throw new Error(`Accessor ${accessorIndex} must be dense and buffer-view backed.`);
  const componentBytes = { 5121: 1, 5123: 2, 5125: 4, 5126: 4 }[accessor.componentType];
  const components = { SCALAR: 1, VEC2: 2, VEC3: 3, VEC4: 4 }[accessor.type];
  if (!componentBytes || !components) throw new Error(`Unsupported accessor ${accessorIndex}.`);
  const stride = view.byteStride ?? componentBytes * components;
  const start = (view.byteOffset ?? 0) + (accessor.byteOffset ?? 0);
  const getter = {
    5121: (offset) => binary.readUInt8(offset),
    5123: (offset) => binary.readUInt16LE(offset),
    5125: (offset) => binary.readUInt32LE(offset),
    5126: (offset) => binary.readFloatLE(offset),
  }[accessor.componentType];
  return {
    count: accessor.count,
    components,
    value(index, component) {
      return getter(start + index * stride + component * componentBytes);
    },
  };
}

function find(parents, item) {
  let root = item;
  while (parents[root] !== root) root = parents[root];
  while (parents[item] !== item) {
    const next = parents[item];
    parents[item] = root;
    item = next;
  }
  return root;
}

function union(parents, ranks, left, right) {
  let leftRoot = find(parents, left);
  let rightRoot = find(parents, right);
  if (leftRoot === rightRoot) return;
  if (ranks[leftRoot] < ranks[rightRoot]) [leftRoot, rightRoot] = [rightRoot, leftRoot];
  parents[rightRoot] = leftRoot;
  if (ranks[leftRoot] === ranks[rightRoot]) ranks[leftRoot] += 1;
}

function triangleAreaSquared(positions, a, b, c) {
  const abx = positions.value(b, 0) - positions.value(a, 0);
  const aby = positions.value(b, 1) - positions.value(a, 1);
  const abz = positions.value(b, 2) - positions.value(a, 2);
  const acx = positions.value(c, 0) - positions.value(a, 0);
  const acy = positions.value(c, 1) - positions.value(a, 1);
  const acz = positions.value(c, 2) - positions.value(a, 2);
  const x = aby * acz - abz * acy;
  const y = abz * acx - abx * acz;
  const z = abx * acy - aby * acx;
  return x * x + y * y + z * z;
}
