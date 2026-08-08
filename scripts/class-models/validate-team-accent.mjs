import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFile, writeFile } from 'node:fs/promises';
import { basename, resolve, sep } from 'node:path';

const repositoryRoot = resolve(import.meta.dirname, '../..');
const [recipeArgument, outputArgument, validationArgument] =
  process.argv.slice(2);
if (!recipeArgument || !outputArgument) {
  throw new Error(
    'Usage: node scripts/class-models/validate-team-accent.mjs <recipe.json> <output-directory> [validation-report.json]',
  );
}

const recipePath = resolve(recipeArgument);
const outputDirectory = resolve(outputArgument);
const recipeBytes = await readFile(recipePath);
const recipe = JSON.parse(recipeBytes);
validateRecipe(recipe);

const sourcePath = repositoryFile(recipe.inputs.model.file, 'normalized model');
const baseColorPath = repositoryFile(
  recipe.inputs.baseColor.file,
  'lossless base-color map',
);
const emissionPath = repositoryFile(
  recipe.inputs.emission.file,
  'lossless emission map',
);
const outputPath = outputFile(outputDirectory, recipe.outputs.model);
const reportPath = resolve(
  outputDirectory,
  recipe.reportFilename ?? 'team-accent-report.json',
);
const maskPath = outputFile(outputDirectory, recipe.outputs.losslessMask);
const neutralBasePath = outputFile(
  outputDirectory,
  recipe.outputs.neutralHullBase,
);
const neutralEmissionPath = outputFile(
  outputDirectory,
  recipe.outputs.neutralHullEmission,
);
const validationPath = resolve(
  validationArgument ??
    resolve(outputDirectory, 'structural-validation.json'),
);

const [
  sourceBytes,
  outputBytes,
  reportBytes,
  maskBytes,
  neutralBaseBytes,
  neutralEmissionBytes,
  baseColorBytes,
  emissionBytes,
] = await Promise.all([
  readFile(sourcePath),
  readFile(outputPath),
  readFile(reportPath),
  readFile(maskPath),
  readFile(neutralBasePath),
  readFile(neutralEmissionPath),
  readFile(baseColorPath),
  readFile(emissionPath),
]);
const source = parseGlb(sourceBytes, sourcePath);
const output = parseGlb(outputBytes, outputPath);
const report = JSON.parse(reportBytes);
const checks = [];

check('recipe pins every source and generated artifact', () => {
  assertPinnedArtifact('normalized model', sourceBytes, recipe.inputs.model);
  assertPinnedArtifact(
    'lossless base-color map',
    baseColorBytes,
    recipe.inputs.baseColor,
  );
  assertPinnedArtifact(
    'lossless emission map',
    emissionBytes,
    recipe.inputs.emission,
  );
  assertPinnedArtifact(
    'team-accent model',
    outputBytes,
    recipe.outputs.model,
  );
  assertPinnedArtifact(
    'lossless mask',
    maskBytes,
    recipe.outputs.losslessMask,
  );
  assertPinnedArtifact(
    'neutral hull base',
    neutralBaseBytes,
    recipe.outputs.neutralHullBase,
  );
  assertPinnedArtifact(
    'neutral hull emission',
    neutralEmissionBytes,
    recipe.outputs.neutralHullEmission,
  );
});
check('source has one primitive', () => {
  assert.equal(source.document.meshes?.length, 1);
  assert.equal(source.document.meshes[0].primitives?.length, 1);
});
check('output has exactly hull + team-accent primitives', () => {
  assert.equal(output.document.meshes?.length, 1);
  assert.equal(output.document.meshes[0].primitives?.length, 2);
});

const sourcePrimitive = source.document.meshes[0].primitives[0];
const [hullPrimitive, teamPrimitive] = output.document.meshes[0].primitives;
const hullMaterial = output.document.materials[hullPrimitive.material];
const teamMaterial = output.document.materials[teamPrimitive.material];
const coordinateBounds = meshWorldBounds(output, 0);
const tangentSpaceWarnings = generatedTangentSpaceWarnings(output.document);

check('both output primitives share the untouched geometry accessors', () => {
  assert.deepEqual(hullPrimitive.attributes, sourcePrimitive.attributes);
  assert.deepEqual(teamPrimitive.attributes, sourcePrimitive.attributes);
  for (const accessorIndex of Object.values(sourcePrimitive.attributes)) {
    assert.deepEqual(
      output.document.accessors[accessorIndex],
      source.document.accessors[accessorIndex],
    );
    assert.equal(
      accessorPayloadHash(output, accessorIndex),
      accessorPayloadHash(source, accessorIndex),
    );
  }
});
check('scene graph and transforms preserve facing exactly', () => {
  assert.equal(output.document.scene, source.document.scene);
  assert.deepEqual(output.document.scenes, source.document.scenes);
  assert.deepEqual(output.document.nodes, source.document.nodes);
});
check('coordinate bounds preserve the pinned +X/+Y normalized source', () => {
  assert.equal(recipe.coordinateContract.facing, '+X');
  assert.equal(recipe.coordinateContract.up, '+Y');
  assertNearArray(
    coordinateBounds.min,
    recipe.coordinateContract.boundsMin,
    recipe.coordinateContract.tolerance,
  );
  assertNearArray(
    coordinateBounds.max,
    recipe.coordinateContract.boundsMax,
    recipe.coordinateContract.tolerance,
  );
  assert.ok(
    Math.abs(
      Math.max(
        coordinateBounds.max[0] - coordinateBounds.min[0],
        coordinateBounds.max[2] - coordinateBounds.min[2],
      ) - recipe.coordinateContract.maxPlanformSpan
    ) <= recipe.coordinateContract.tolerance,
  );
  assert.ok(
    Math.abs(coordinateBounds.min[1] - recipe.coordinateContract.floorY) <=
      recipe.coordinateContract.tolerance,
  );
});
check('face split is a lossless partition of source triangles', () => {
  const sourceIndices = readAccessor(source, sourcePrimitive.indices);
  const hullIndices = readAccessor(output, hullPrimitive.indices);
  const teamIndices = readAccessor(output, teamPrimitive.indices);
  assert.equal(
    sourceIndices.length,
    hullIndices.length + teamIndices.length,
  );
  assert.equal(
    triangleSetHash(sourceIndices),
    triangleSetHash([...hullIndices, ...teamIndices]),
  );
});
check('team role is semantic on primitive and material', () => {
  assert.equal(teamPrimitive.extras?.nilbotsRole, 'team-accent');
  assert.equal(teamMaterial.extras?.nilbotsRole, 'team-accent');
});
check('team material is tintable without fixed color maps', () => {
  assert.deepEqual(
    teamMaterial.pbrMetallicRoughness?.baseColorFactor,
    [1, 1, 1, 1],
  );
  assert.equal(
    teamMaterial.pbrMetallicRoughness?.baseColorTexture,
    undefined,
  );
  assert.equal(teamMaterial.emissiveTexture, undefined);
  assert.deepEqual(teamMaterial.emissiveFactor, [1, 1, 1]);
});
check('team material keeps normal and metallic/roughness PBR', () => {
  assert.ok(teamMaterial.normalTexture);
  assert.ok(
    teamMaterial.pbrMetallicRoughness?.metallicRoughnessTexture,
  );
  assert.deepEqual(teamMaterial.normalTexture, hullMaterial.normalTexture);
  assert.deepEqual(
    teamMaterial.pbrMetallicRoughness.metallicRoughnessTexture,
    hullMaterial.pbrMetallicRoughness.metallicRoughnessTexture,
  );
});
check('hull retains all four PBR texture roles', () => {
  assert.ok(hullMaterial.pbrMetallicRoughness?.baseColorTexture);
  assert.ok(
    hullMaterial.pbrMetallicRoughness?.metallicRoughnessTexture,
  );
  assert.ok(hullMaterial.normalTexture);
  assert.ok(hullMaterial.emissiveTexture);
});
check('normal and metallic/roughness payloads are byte-identical', () => {
  assert.equal(
    texturePayloadHash(output, teamMaterial.normalTexture.index),
    texturePayloadHash(source, source.document.materials[0].normalTexture.index),
  );
  assert.equal(
    texturePayloadHash(
      output,
      teamMaterial.pbrMetallicRoughness.metallicRoughnessTexture.index,
    ),
    texturePayloadHash(
      source,
      source.document.materials[0].pbrMetallicRoughness
        .metallicRoughnessTexture.index,
    ),
  );
});
check('hull color and emission are embedded lossless PNGs', () => {
  assert.equal(
    textureImage(
      output,
      hullMaterial.pbrMetallicRoughness.baseColorTexture.index,
    ).mimeType,
    'image/png',
  );
  assert.equal(
    textureImage(output, hullMaterial.emissiveTexture.index).mimeType,
    'image/png',
  );
});
check('generated PNGs retain their pinned resolutions', () => {
  assert.deepEqual(pngDimensions(maskBytes), [
    recipe.outputs.losslessMask.width,
    recipe.outputs.losslessMask.height,
  ]);
  assert.deepEqual(pngDimensions(neutralBaseBytes), [
    recipe.outputs.neutralHullBase.width,
    recipe.outputs.neutralHullBase.height,
  ]);
  assert.deepEqual(pngDimensions(neutralEmissionBytes), [
    recipe.outputs.neutralHullEmission.width,
    recipe.outputs.neutralHullEmission.height,
  ]);
});
check('runtime model remains inside its pinned transfer budget', () => {
  assert.ok(outputBytes.length <= recipe.validation.runtimeBudgetBytes);
});
check('known tangent-space portability warnings remain explicit', () => {
  assert.deepEqual(
    tangentSpaceWarnings,
    recipe.validation.generatedTangentSpaceWarnings,
  );
});
check('runtime model contains no camera, light, or animation', () => {
  assert.equal(output.document.cameras?.length ?? 0, 0);
  assert.equal(output.document.animations?.length ?? 0, 0);
  assert.equal(
    output.document.extensions?.KHR_lights_punctual?.lights?.length ?? 0,
    0,
  );
});
check('report hashes and internal checks match generated files', () => {
  assert.equal(report.recipe.file, relative(recipePath));
  assert.equal(report.recipe.sha256, sha256(recipeBytes));
  assert.equal(report.source.taskId, recipe.taskId);
  assert.equal(report.source.imageEnhancement, recipe.imageEnhancement);
  assert.equal(report.source.modelSha256, recipe.inputs.model.sha256);
  assert.equal(report.source.baseColor.sha256, recipe.inputs.baseColor.sha256);
  assert.equal(report.source.emission.sha256, recipe.inputs.emission.sha256);
  assert.equal(report.outputs.model.sha256, sha256(outputBytes));
  assert.equal(report.outputs.losslessMask.sha256, sha256(maskBytes));
  assert.equal(
    report.outputs.neutralHullBase.sha256,
    sha256(neutralBaseBytes),
  );
  assert.equal(
    report.outputs.neutralHullEmission.sha256,
    sha256(neutralEmissionBytes),
  );
  assert.deepEqual(report.thresholds, recipe.thresholds);
  assert.equal(report.validation.pass, true);
  assert.ok(
    Object.values(report.validation.checks).every((value) => value === true),
  );
  assert.equal(
    report.triangleSplit.sourceTriangles,
    report.triangleSplit.hullTriangles +
      report.triangleSplit.teamAccentTriangles,
  );
  assert.equal(
    report.triangleSplit.hullTriangleHash,
    triangleSetHash(readAccessor(output, hullPrimitive.indices)),
  );
  assert.equal(
    report.triangleSplit.teamAccentTriangleHash,
    triangleSetHash(readAccessor(output, teamPrimitive.indices)),
  );
});

const result = {
  version: 1,
  recipe: {
    file: relative(recipePath),
    sha256: sha256(recipeBytes),
  },
  source: relative(sourcePath),
  output: relative(outputPath),
  artifacts: {
    sourceModelSha256: sha256(sourceBytes),
    outputModelSha256: sha256(outputBytes),
    outputModelBytes: outputBytes.length,
  },
  coordinateContract: {
    facing: recipe.coordinateContract.facing,
    up: recipe.coordinateContract.up,
    boundsMin: coordinateBounds.min,
    boundsMax: coordinateBounds.max,
    maxPlanformSpan: Math.max(
      coordinateBounds.max[0] - coordinateBounds.min[0],
      coordinateBounds.max[2] - coordinateBounds.min[2],
    ),
  },
  portabilityWarnings: tangentSpaceWarnings,
  pass: checks.every(({ pass }) => pass),
  checks,
};
await writeFile(validationPath, `${JSON.stringify(result, null, 2)}\n`);
if (!result.pass) {
  throw new Error(
    `Structural validation failed: ${checks
      .filter(({ pass }) => !pass)
      .map(({ name, error }) => `${name}: ${error}`)
      .join('; ')}`,
  );
}
console.log(
  `PASS ${checks.length}/${checks.length}: ${relative(outputPath)} preserves geometry/facing and exposes one renderer-compatible team-accent material.`,
);
console.log(`Wrote ${relative(validationPath)}.`);

function check(name, action) {
  try {
    action();
    checks.push({ name, pass: true });
  } catch (error) {
    checks.push({
      name,
      pass: false,
      error: error instanceof Error ? error.message : String(error),
    });
  }
}

function parseGlb(bytes, label) {
  assert.equal(bytes.toString('ascii', 0, 4), 'glTF', `${label} GLB magic`);
  assert.equal(bytes.readUInt32LE(4), 2, `${label} glTF version`);
  const jsonLength = bytes.readUInt32LE(12);
  assert.equal(bytes.readUInt32LE(16), 0x4e4f534a, `${label} JSON chunk`);
  const jsonEnd = 20 + jsonLength;
  const document = JSON.parse(
    bytes
      .subarray(20, jsonEnd)
      .toString('utf8')
      .replace(/[\u0000\u0020]+$/u, ''),
  );
  assert.equal(
    bytes.readUInt32LE(jsonEnd + 4),
    0x004e4942,
    `${label} BIN chunk`,
  );
  const binaryLength = bytes.readUInt32LE(jsonEnd);
  const binary = bytes.subarray(jsonEnd + 8, jsonEnd + 8 + binaryLength);
  return { document, binary };
}

function readAccessor(glb, accessorIndex) {
  const accessor = glb.document.accessors[accessorIndex];
  const view = glb.document.bufferViews[accessor.bufferView];
  assert.equal(accessor.type, 'SCALAR');
  const byteSize = componentSize(accessor.componentType);
  const stride = view.byteStride ?? byteSize;
  const start = (view.byteOffset ?? 0) + (accessor.byteOffset ?? 0);
  const values = new Array(accessor.count);
  for (let index = 0; index < accessor.count; index += 1) {
    const offset = start + index * stride;
    if (accessor.componentType === 5121) {
      values[index] = glb.binary.readUInt8(offset);
    } else if (accessor.componentType === 5123) {
      values[index] = glb.binary.readUInt16LE(offset);
    } else if (accessor.componentType === 5125) {
      values[index] = glb.binary.readUInt32LE(offset);
    } else {
      throw new Error(`Unsupported index type ${accessor.componentType}.`);
    }
  }
  return values;
}

function accessorPayloadHash(glb, accessorIndex) {
  const accessor = glb.document.accessors[accessorIndex];
  const view = glb.document.bufferViews[accessor.bufferView];
  return sha256(
    glb.binary.subarray(
      view.byteOffset ?? 0,
      (view.byteOffset ?? 0) + view.byteLength,
    ),
  );
}

function texturePayloadHash(glb, textureIndex) {
  const image = textureImage(glb, textureIndex);
  const view = glb.document.bufferViews[image.bufferView];
  return sha256(
    glb.binary.subarray(
      view.byteOffset ?? 0,
      (view.byteOffset ?? 0) + view.byteLength,
    ),
  );
}

function textureImage(glb, textureIndex) {
  const imageIndex = glb.document.textures[textureIndex].source;
  return glb.document.images[imageIndex];
}

function triangleSetHash(indices) {
  const triangles = [];
  for (let offset = 0; offset < indices.length; offset += 3) {
    triangles.push(
      `${indices[offset]},${indices[offset + 1]},${indices[offset + 2]}`,
    );
  }
  triangles.sort();
  return sha256(Buffer.from(triangles.join('\n')));
}

function componentSize(componentType) {
  if (componentType === 5121) return 1;
  if (componentType === 5123) return 2;
  if (componentType === 5125) return 4;
  throw new Error(`Unsupported component type ${componentType}.`);
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

function relative(path) {
  const prefix = `${repositoryRoot}/`;
  return path.startsWith(prefix) ? path.slice(prefix.length) : path;
}

function validateRecipe(input) {
  assert.equal(input?.version, 1, 'recipe version');
  assert.equal(
    input.method,
    'deterministic-offline-team-accent-face-partition',
    'recipe method',
  );
  assert.equal(typeof input.taskId, 'string', 'provider task ID');
  assert.equal(
    typeof input.imageEnhancement,
    'boolean',
    'image-enhancement setting',
  );
  for (const required of ['model', 'baseColor', 'emission']) {
    validateArtifactPin(`input ${required}`, input.inputs?.[required], true);
  }
  for (const required of [
    'model',
    'losslessMask',
    'neutralHullBase',
    'neutralHullEmission',
  ]) {
    validateArtifactPin(
      `output ${required}`,
      input.outputs?.[required],
      false,
    );
  }
  assert.equal(input.thresholds?.triangleSamples, 7);
  assert.equal(input.thresholds?.triangleVoteThreshold, 5);
  assert.equal(input.coordinateContract?.facing, '+X');
  assert.equal(input.coordinateContract?.up, '+Y');
  assert.equal(input.coordinateContract?.boundsMin?.length, 3);
  assert.equal(input.coordinateContract?.boundsMax?.length, 3);
  assert.ok(input.coordinateContract?.tolerance > 0);
  assert.ok(
    Number.isSafeInteger(input.validation?.runtimeBudgetBytes) &&
      input.validation.runtimeBudgetBytes > 0,
  );
  assert.ok(Array.isArray(input.validation?.generatedTangentSpaceWarnings));
  if (input.reportFilename !== undefined) {
    assert.equal(basename(input.reportFilename), input.reportFilename);
  }
}

function validateArtifactPin(label, pin, input) {
  assert.ok(pin && typeof pin === 'object', `${label} pin`);
  const pathKey = input ? 'file' : 'fileName';
  assert.equal(typeof pin[pathKey], 'string', `${label} ${pathKey}`);
  assert.ok(Number.isSafeInteger(pin.bytes) && pin.bytes > 0, `${label} bytes`);
  assert.match(pin.sha256, /^[0-9a-f]{64}$/u, `${label} SHA-256`);
}

function repositoryFile(file, label) {
  const path = resolve(repositoryRoot, file);
  if (!path.startsWith(`${repositoryRoot}${sep}`)) {
    throw new Error(`The ${label} must resolve inside the repository.`);
  }
  return path;
}

function outputFile(directory, pin) {
  if (
    typeof pin?.fileName !== 'string' ||
    basename(pin.fileName) !== pin.fileName
  ) {
    throw new Error('Generated artifacts must use plain filenames.');
  }
  return resolve(directory, pin.fileName);
}

function assertPinnedArtifact(label, bytes, pin) {
  assert.equal(bytes.length, pin.bytes, `${label} bytes`);
  assert.equal(sha256(bytes), pin.sha256, `${label} SHA-256`);
}

function pngDimensions(bytes) {
  assert.equal(bytes.toString('hex', 1, 4), '504e47', 'PNG signature');
  return [bytes.readUInt32BE(16), bytes.readUInt32BE(20)];
}

function generatedTangentSpaceWarnings(document) {
  const warnings = [];
  for (let meshIndex = 0; meshIndex < (document.meshes?.length ?? 0); meshIndex += 1) {
    const mesh = document.meshes[meshIndex];
    for (
      let primitiveIndex = 0;
      primitiveIndex < (mesh.primitives?.length ?? 0);
      primitiveIndex += 1
    ) {
      const primitive = mesh.primitives[primitiveIndex];
      const material = document.materials?.[primitive.material ?? 0];
      if (material?.normalTexture && !primitive.attributes?.TANGENT) {
        warnings.push({
          code: 'MESH_PRIMITIVE_GENERATED_TANGENT_SPACE',
          mesh: meshIndex,
          primitive: primitiveIndex,
        });
      }
    }
  }
  return warnings;
}

function meshWorldBounds(glb, meshIndex) {
  const document = glb.document;
  const scene = document.scenes?.[document.scene ?? 0];
  assert.ok(scene, 'active scene');
  const matrices = [];
  const identity = [
    1, 0, 0, 0,
    0, 1, 0, 0,
    0, 0, 1, 0,
    0, 0, 0, 1,
  ];

  function visit(nodeIndex, parentMatrix) {
    const node = document.nodes?.[nodeIndex];
    assert.ok(node, `scene node ${nodeIndex}`);
    const world = multiplyMatrices(parentMatrix, nodeMatrix(node));
    if (node.mesh === meshIndex) matrices.push(world);
    for (const child of node.children ?? []) visit(child, world);
  }

  for (const root of scene.nodes ?? []) visit(root, identity);
  assert.equal(matrices.length, 1, `one scene instance for mesh ${meshIndex}`);

  const primitive = document.meshes?.[meshIndex]?.primitives?.[0];
  assert.ok(primitive, `mesh ${meshIndex} primitive`);
  const positions = readVector3Accessor(glb, primitive.attributes?.POSITION);
  const min = [Number.POSITIVE_INFINITY, Number.POSITIVE_INFINITY, Number.POSITIVE_INFINITY];
  const max = [Number.NEGATIVE_INFINITY, Number.NEGATIVE_INFINITY, Number.NEGATIVE_INFINITY];
  for (let offset = 0; offset < positions.length; offset += 3) {
    const point = transformPoint(
      matrices[0],
      positions[offset],
      positions[offset + 1],
      positions[offset + 2],
    );
    for (let axis = 0; axis < 3; axis += 1) {
      min[axis] = Math.min(min[axis], point[axis]);
      max[axis] = Math.max(max[axis], point[axis]);
    }
  }
  return { min, max };
}

function readVector3Accessor(glb, accessorIndex) {
  const accessor = glb.document.accessors?.[accessorIndex];
  assert.ok(accessor, `position accessor ${accessorIndex}`);
  assert.equal(accessor.type, 'VEC3');
  assert.equal(accessor.componentType, 5126);
  assert.equal(accessor.sparse, undefined);
  const view = glb.document.bufferViews?.[accessor.bufferView];
  assert.ok(view, `position buffer view ${accessor.bufferView}`);
  const stride = view.byteStride ?? 12;
  const start = (view.byteOffset ?? 0) + (accessor.byteOffset ?? 0);
  const output = new Array(accessor.count * 3);
  for (let item = 0; item < accessor.count; item += 1) {
    for (let component = 0; component < 3; component += 1) {
      output[item * 3 + component] = glb.binary.readFloatLE(
        start + item * stride + component * 4,
      );
    }
  }
  return output;
}

function nodeMatrix(node) {
  if (node.matrix) return node.matrix;
  const [tx, ty, tz] = node.translation ?? [0, 0, 0];
  const [x, y, z, w] = node.rotation ?? [0, 0, 0, 1];
  const [sx, sy, sz] = node.scale ?? [1, 1, 1];
  const xx = x * x;
  const xy = x * y;
  const xz = x * z;
  const xw = x * w;
  const yy = y * y;
  const yz = y * z;
  const yw = y * w;
  const zz = z * z;
  const zw = z * w;
  return [
    (1 - 2 * (yy + zz)) * sx,
    2 * (xy + zw) * sx,
    2 * (xz - yw) * sx,
    0,
    2 * (xy - zw) * sy,
    (1 - 2 * (xx + zz)) * sy,
    2 * (yz + xw) * sy,
    0,
    2 * (xz + yw) * sz,
    2 * (yz - xw) * sz,
    (1 - 2 * (xx + yy)) * sz,
    0,
    tx,
    ty,
    tz,
    1,
  ];
}

function multiplyMatrices(left, right) {
  const output = new Array(16).fill(0);
  for (let column = 0; column < 4; column += 1) {
    for (let row = 0; row < 4; row += 1) {
      for (let inner = 0; inner < 4; inner += 1) {
        output[column * 4 + row] +=
          left[inner * 4 + row] * right[column * 4 + inner];
      }
    }
  }
  return output;
}

function transformPoint(matrix, x, y, z) {
  return [
    matrix[0] * x + matrix[4] * y + matrix[8] * z + matrix[12],
    matrix[1] * x + matrix[5] * y + matrix[9] * z + matrix[13],
    matrix[2] * x + matrix[6] * y + matrix[10] * z + matrix[14],
  ];
}

function assertNearArray(actual, expected, tolerance) {
  assert.equal(actual.length, expected.length);
  for (let index = 0; index < actual.length; index += 1) {
    assert.ok(
      Math.abs(actual[index] - expected[index]) <= tolerance,
      `axis ${index}: expected ${expected[index]}, got ${actual[index]}`,
    );
  }
}
