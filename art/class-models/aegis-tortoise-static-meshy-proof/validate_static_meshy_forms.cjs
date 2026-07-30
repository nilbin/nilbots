#!/usr/bin/env node
'use strict';

const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs/promises');
const path = require('node:path');

const [validatorPackage, proofRootArgument] = process.argv.slice(2);
if (!validatorPackage || !path.isAbsolute(validatorPackage))
  throw new Error(
    'usage: node validate_static_meshy_forms.cjs <absolute gltf-validator package> [proof-root]',
  );
const validator = require(validatorPackage);
const proofRoot = proofRootArgument
  ? path.resolve(proofRootArgument)
  : __dirname;
const repositoryRoot = path.resolve(proofRoot, '../../..');

const assets = [
  ['aegis-tortoise-shell', 'rich'],
  ['aegis-tortoise-shell', 'lean'],
  ['aegis-tortoise-turret', 'rich'],
  ['aegis-tortoise-turret', 'lean'],
];
const SHIPPING_LOOK_SCALE = {
  'aegis-tortoise-shell': 1.2,
  'aegis-tortoise-turret': 1.08,
};
const EMBER_V4_OPEN_EDGE_INSET = 0.14462;
const EMBER_V4_CENTRELINE_CLEARANCE =
  0.5 + EMBER_V4_OPEN_EDGE_INSET;
const REVIEW_SAFETY_MARGIN = 0.02;

function sha256(bytes) {
  return crypto.createHash('sha256').update(bytes).digest('hex');
}

function parseGlb(bytes) {
  assert.equal(bytes.subarray(0, 4).toString('ascii'), 'glTF');
  assert.equal(bytes.readUInt32LE(4), 2);
  assert.equal(bytes.readUInt32LE(8), bytes.length);
  let document;
  let binary;
  let offset = 12;
  while (offset < bytes.length) {
    const chunkLength = bytes.readUInt32LE(offset);
    const chunkType = bytes.readUInt32LE(offset + 4);
    const chunk = bytes.subarray(offset + 8, offset + 8 + chunkLength);
    if (chunkType === 0x4e4f534a)
      document = JSON.parse(
        chunk
          .toString('utf8')
          .replace(/[\u0000-\u0020]+$/u, ''),
      );
    else if (chunkType === 0x004e4942) binary = chunk;
    offset += 8 + chunkLength;
  }
  assert.ok(document);
  assert.ok(binary);
  return { document, binary };
}

function maximumHorizontalVertexRadius(document, binary) {
  const positionAccessors = document.meshes.flatMap((mesh) =>
    mesh.primitives.map((primitive) => primitive.attributes.POSITION),
  );
  let maximum = 0;
  for (const accessorIndex of positionAccessors) {
    const accessor = document.accessors[accessorIndex];
    const bufferView = document.bufferViews[accessor.bufferView];
    assert.equal(accessor.componentType, 5126);
    assert.equal(accessor.type, 'VEC3');
    assert.equal(accessor.sparse, undefined);
    const stride = bufferView.byteStride ?? 12;
    const start =
      (bufferView.byteOffset ?? 0) + (accessor.byteOffset ?? 0);
    for (let index = 0; index < accessor.count; index += 1) {
      const position = start + index * stride;
      const x = binary.readFloatLE(position);
      const z = binary.readFloatLE(position + 8);
      maximum = Math.max(maximum, Math.hypot(x, z));
    }
  }
  return maximum;
}

function geometryMeasurements(document, binary, assetId) {
  const sourceRadius = maximumHorizontalVertexRadius(document, binary);
  const shippingLookScale = SHIPPING_LOOK_SCALE[assetId];
  const shippingRadius = sourceRadius * shippingLookScale;
  const projectedPrimaryWebGlScale = Math.max(
    0.82,
    shippingLookScale * 0.9,
  );
  const projectedPrimaryWebGlRadius =
    sourceRadius * projectedPrimaryWebGlScale;
  const visualMargin =
    EMBER_V4_CENTRELINE_CLEARANCE - shippingRadius;
  const requiredInsetForReviewSafety = Math.max(
    0,
    shippingRadius + REVIEW_SAFETY_MARGIN - 0.5,
  );
  return {
    coordinateSystem: '+Y up; XZ horizontal',
    maximumHorizontalVertexRadius: sourceRadius,
    shippingLookScale,
    maximumHorizontalVertexRadiusAfterShippingLookScale:
      shippingRadius,
    primaryWebGlScaleProjection: {
      convention:
        'max(0.82, form look.scale * 0.9); projection for future whole-form wiring, not the current form-switch implementation',
      effectiveScale: projectedPrimaryWebGlScale,
      maximumHorizontalVertexRadius:
        projectedPrimaryWebGlRadius,
      staticVisualMargin:
        EMBER_V4_CENTRELINE_CLEARANCE -
        projectedPrimaryWebGlRadius,
      arbitraryYawClear:
        projectedPrimaryWebGlRadius <=
        EMBER_V4_CENTRELINE_CLEARANCE,
      meetsReviewSafetyMargin:
        projectedPrimaryWebGlRadius + REVIEW_SAFETY_MARGIN <=
        EMBER_V4_CENTRELINE_CLEARANCE,
    },
    emberV4: {
      finalOpenEdgeInset: EMBER_V4_OPEN_EDGE_INSET,
      openFloorCentrelineClearance: EMBER_V4_CENTRELINE_CLEARANCE,
      staticVisualMargin: visualMargin,
      reviewSafetyMargin: REVIEW_SAFETY_MARGIN,
      requiredOpenEdgeInsetForReviewSafety:
        requiredInsetForReviewSafety,
      maximumLookScaleForArbitraryYawClear:
        EMBER_V4_CENTRELINE_CLEARANCE / sourceRadius,
      maximumLookScaleForReviewSafety:
        (EMBER_V4_CENTRELINE_CLEARANCE -
          REVIEW_SAFETY_MARGIN) /
        sourceRadius,
      arbitraryYawClear: visualMargin >= 0,
      meetsReviewSafetyMargin:
        visualMargin >= REVIEW_SAFETY_MARGIN,
      insetAdjustmentRequiredForReviewSafety: Math.max(
        0,
        requiredInsetForReviewSafety - EMBER_V4_OPEN_EDGE_INSET,
      ),
    },
  };
}

function structuralChecks(document, assetId, tier) {
  const checks = {};
  const check = (name, action) => {
    action();
    checks[name] = true;
  };
  const primitives = document.meshes.flatMap((mesh) => mesh.primitives);
  const materials = document.materials;
  const byRole = new Map(
    materials.map((material, index) => [
      material.extras?.nilbotsRole,
      { index, material },
    ]),
  );

  check('one static mesh with hull + team primitives', () => {
    assert.equal(document.meshes.length, 1);
    assert.equal(primitives.length, 2);
    assert.deepEqual(
      primitives.map((primitive) => primitive.extras?.nilbotsRole),
      ['hull', 'team-accent'],
    );
  });
  check('material and primitive roles agree', () => {
    for (const primitive of primitives)
      assert.equal(
        materials[primitive.material].extras?.nilbotsRole,
        primitive.extras?.nilbotsRole,
      );
    assert.ok(byRole.has('hull'));
    assert.ok(byRole.has('team-accent'));
  });
  check('team material is neutral and texture-free in color/emission', () => {
    const team = byRole.get('team-accent').material;
    assert.deepEqual(
      team.pbrMetallicRoughness?.baseColorFactor ?? [1, 1, 1, 1],
      [1, 1, 1, 1],
    );
    assert.deepEqual(team.emissiveFactor ?? [0, 0, 0], [1, 1, 1]);
    assert.equal(team.pbrMetallicRoughness?.baseColorTexture, undefined);
    assert.equal(team.emissiveTexture, undefined);
    assert.ok(team.normalTexture);
    assert.ok(team.pbrMetallicRoughness?.metallicRoughnessTexture);
  });
  check('hull retains base, normal, metallic/roughness, and emission', () => {
    const hull = byRole.get('hull').material;
    assert.ok(hull.pbrMetallicRoughness?.baseColorTexture);
    assert.ok(hull.pbrMetallicRoughness?.metallicRoughnessTexture);
    assert.ok(hull.normalTexture);
    assert.ok(hull.emissiveTexture);
  });
  check('every primitive carries portable tangents and UVs', () => {
    for (const primitive of primitives) {
      assert.ok(Number.isInteger(primitive.attributes.TANGENT));
      assert.ok(Number.isInteger(primitive.attributes.TEXCOORD_0));
      assert.ok(Number.isInteger(primitive.attributes.NORMAL));
    }
  });
  check('static form contains no rig, clip, camera, or light', () => {
    assert.equal(document.animations?.length ?? 0, 0);
    assert.equal(document.skins?.length ?? 0, 0);
    assert.equal(document.cameras?.length ?? 0, 0);
    assert.equal(
      document.extensions?.KHR_lights_punctual?.lights?.length ?? 0,
      0,
    );
  });
  check('mesh node is an identity transform', () => {
    const meshNodes = document.nodes.filter((node) =>
      Number.isInteger(node.mesh),
    );
    assert.equal(meshNodes.length, 1);
    const node = meshNodes[0];
    assert.equal(node.matrix, undefined);
    assert.deepEqual(node.translation ?? [0, 0, 0], [0, 0, 0]);
    assert.deepEqual(node.rotation ?? [0, 0, 0, 1], [0, 0, 0, 1]);
    assert.deepEqual(node.scale ?? [1, 1, 1], [1, 1, 1]);
  });
  check('coordinate bounds are +Y-up, floored, and one-tile planform', () => {
    const positionAccessors = primitives.map(
      (primitive) => document.accessors[primitive.attributes.POSITION],
    );
    const minimum = [0, 1, 2].map((axis) =>
      Math.min(...positionAccessors.map((accessor) => accessor.min[axis])),
    );
    const maximum = [0, 1, 2].map((axis) =>
      Math.max(...positionAccessors.map((accessor) => accessor.max[axis])),
    );
    const span = maximum.map((value, axis) => value - minimum[axis]);
    assert.ok(Math.abs(minimum[1]) <= 0.001, `${assetId}/${tier} floor`);
    assert.ok(
      Math.abs(Math.max(span[0], span[2]) - 1) <= 0.001,
      `${assetId}/${tier} planform`,
    );
    assert.ok(maximum[1] > 0.24 && maximum[1] < 0.32);
  });
  check('textures are embedded without runtime-external dependencies', () => {
    assert.equal(document.images.length, 4);
    for (const image of document.images) {
      assert.ok(Number.isInteger(image.bufferView));
      assert.ok(['image/jpeg', 'image/png'].includes(image.mimeType));
      assert.equal(image.uri, undefined);
    }
  });

  return checks;
}

async function main() {
  const buildReport = JSON.parse(
    await fs.readFile(
      path.join(proofRoot, 'reports', 'build-report.json'),
      'utf8',
    ),
  );
  const reports = [];
  const reconciliations = [];
  for (const [assetId, tier] of assets) {
    const file = path.join(
      proofRoot,
      'candidate',
      assetId,
      tier,
      `${assetId}-${tier}.glb`,
    );
    const bytes = await fs.readFile(file);
    const { document, binary } = parseGlb(bytes);
    const validation = await validator.validateBytes(new Uint8Array(bytes), {
      uri: path.basename(file),
      format: 'glb',
      maxIssues: 0,
      writeTimestamp: false,
    });
    const checks = structuralChecks(document, assetId, tier);
    const pass =
      validation.issues.numErrors === 0 &&
      validation.issues.numWarnings === 0 &&
      Object.values(checks).every(Boolean);
    const buildCandidate = buildReport.candidates.find(
      (candidate) =>
        candidate.assetId === assetId && candidate.tier === tier,
    );
    assert.ok(buildCandidate);
    const reconciliation = {
      assetId,
      tier,
      bytesMatch: buildCandidate.model.bytes === bytes.length,
      sha256Match:
        buildCandidate.model.sha256 === sha256(bytes),
      trianglesMatch:
        buildCandidate.model.triangles ===
        validation.info.totalTriangleCount,
      verticesMatch:
        buildCandidate.model.vertices ===
        validation.info.totalVertexCount,
    };
    reconciliation.pass = Object.entries(reconciliation)
      .filter(([key]) => key.endsWith('Match'))
      .every(([, value]) => value);
    reconciliations.push(reconciliation);
    reports.push({
      assetId,
      tier,
      file: {
        path: path.relative(repositoryRoot, file),
        bytes: bytes.length,
        sha256: sha256(bytes),
      },
      geometry: geometryMeasurements(
        document,
        binary,
        assetId,
      ),
      verdict: pass ? 'pass' : 'fail',
      structuralChecks: checks,
      validation,
    });
  }
  const report = {
    schemaVersion: 1,
    validatorVersion: validator.version(),
    wallClearanceAuthority: {
      environment: 'Frontline Ember Forge V4',
      renderer:
        'web/src/render3d/arenaScene.ts',
      audit:
        'art/frontline-map-models/CLEARANCE-AUDIT.md',
      finalOpenEdgeInset: EMBER_V4_OPEN_EDGE_INSET,
      openFloorCentrelineClearance:
        EMBER_V4_CENTRELINE_CLEARANCE,
      reviewSafetyMargin: REVIEW_SAFETY_MARGIN,
    },
    buildReportReconciliation: reconciliations,
    verdict:
      reports.every(({ verdict }) => verdict === 'pass') &&
      reconciliations.every(({ pass }) => pass)
        ? 'pass'
        : 'fail',
    assets: reports,
  };
  const output = path.join(proofRoot, 'reports', 'khronos-validation.json');
  await fs.mkdir(path.dirname(output), { recursive: true });
  await fs.writeFile(output, `${JSON.stringify(report, null, 2)}\n`);
  process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
  if (report.verdict !== 'pass') process.exitCode = 1;
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
