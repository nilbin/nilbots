#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  geometryFingerprint,
  readGlb,
  triangleCount,
} from './model-memory.mjs';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const directory = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet-retexture-v3-multiview',
);
const planPath = path.join(directory, 'plan.json');
const plan = JSON.parse(readFileSync(planPath, 'utf8'));
const sourceAuditPath = path.resolve(repository, plan.sourceAudit);
const sourceAudit = JSON.parse(readFileSync(sourceAuditPath, 'utf8'));
const palisadePlanPath = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet-retexture-v3-calibration/plan.json',
);
const palisadePlan = JSON.parse(readFileSync(palisadePlanPath, 'utf8'));
if (plan.status !== 'owner-review' || plan.totalConsumedCredits !== 140)
  throw new Error('The brighter multiview fleet batch is not complete.');
if (palisadePlan.status !== 'owner-review' || palisadePlan.consumedCredits !== 10)
  throw new Error('The approved Palisade multiview calibration is missing.');

const planByClass = new Map(plan.entries.map((entry) => [entry.classId, entry]));
const entries = sourceAudit.entries.map((source) => inspect(source));
if (
  entries.length !== 16 ||
  entries.filter((entry) => entry.retextured).length !== 15 ||
  entries.filter((entry) => entry.newInCurrentBatch).length !== 14
)
  throw new Error('V3 must retain Kestrel, reuse Palisade, and add fourteen retextures.');

const audit = {
  schemaVersion: 1,
  status: 'owner-review',
  generatedAt: [...plan.entries, palisadePlan]
    .map((entry) => entry.finishedAt)
    .filter(Boolean)
    .sort()
    .at(-1),
  purpose:
    'Brighter matte/satin Meshy 7 ordered-multiview fleet review; approved Kestrel and Palisade retained, no runtime assets promoted',
  sourceAudit: path.relative(repository, sourceAuditPath),
  sourcePlan: path.relative(repository, planPath),
  retainedPalisadePlan: path.relative(repository, palisadePlanPath),
  materialPolicy: plan.materialPolicy,
  fleet: {
    classes: entries.length,
    retainedReviewedClasses: ['kestrel', 'palisade'],
    newlyRetexturedClasses: entries.filter((entry) => entry.newInCurrentBatch).length,
    successfulCurrentTasks: plan.entries.filter((entry) => entry.status === 'SUCCEEDED').length,
    currentBatchConsumedCredits: plan.totalConsumedCredits,
    retainedPalisadeConsumedCredits: palisadePlan.consumedCredits,
    balanceBeforeCurrentBatch: plan.balanceBefore,
    balanceAfter: plan.balanceAfter,
    runtimeAssetsPromoted: false,
  },
  entries,
};
writeFileSync(path.join(directory, 'fleet-audit.json'), `${JSON.stringify(audit, null, 2)}\n`);
console.log(
  `Audited ${entries.length} V3 fleet models; ${plan.totalConsumedCredits} current-batch credits consumed.`,
);

function inspect(source) {
  if (source.classId === 'kestrel') {
    return {
      ...source,
      retainedFrom: path.relative(repository, sourceAuditPath),
      retextured: false,
      newInCurrentBatch: false,
      materialDirection: 'owner-approved Kestrel retained byte-for-byte',
    };
  }
  if (source.classId === 'palisade') {
    const sourceManifestDirectory = path.dirname(path.resolve(repository, palisadePlan.sourceManifest));
    return inspectRetexture(source, {
      taskId: palisadePlan.completedTaskId,
      status: 'SUCCEEDED',
      consumedCredits: palisadePlan.consumedCredits,
      artifacts: palisadePlan.artifacts,
      runDirectory: path.join(
        path.dirname(palisadePlanPath),
        'palisade',
        palisadePlan.completedTaskId,
      ),
      newInCurrentBatch: false,
      materialAuthority: {
        kind: 'retained-owner-approved-ordered-multiview',
        finish: 'approved matte/satin Palisade calibration',
        inputs: palisadePlan.styleAuthority.inputs.map((input) => ({
          ...input,
          file: path.relative(repository, path.join(sourceManifestDirectory, input.file)),
        })),
      },
    });
  }
  const run = planByClass.get(source.classId);
  if (!run || run.status !== 'SUCCEEDED' || run.consumedCredits !== 10)
    throw new Error(`${source.classId}: successful 10-credit retexture is missing.`);
  return inspectRetexture(source, {
    taskId: run.completedTaskId,
    status: run.status,
    consumedCredits: run.consumedCredits,
    artifacts: run.artifacts,
    runDirectory: path.join(directory, source.classId, run.completedTaskId),
    newInCurrentBatch: true,
    materialAuthority: {
      kind: run.styleAuthority.kind,
      finish: plan.materialPolicy.finish,
      valueAudit: run.styleAuthority.valueAudit,
      inputs: run.styleAuthority.inputs,
    },
  });
}

function inspectRetexture(source, run) {
  const modelPath = path.join(run.runDirectory, 'raw-model.glb');
  const bytes = readFileSync(modelPath);
  const recorded = run.artifacts?.find((artifact) => artifact.file === 'raw-model.glb');
  if (!recorded || recorded.bytes !== bytes.length || recorded.sha256 !== sha256(bytes))
    throw new Error(`${source.classId}: downloaded retexture GLB failed provenance validation.`);
  const { document } = readGlb(bytes, `${source.classId} V3 retexture`);
  const sourceBytes = readFileSync(path.join(repository, source.rawModel.file));
  const { document: sourceDocument } = readGlb(sourceBytes, `${source.classId} source`);
  const beforeGeometry = geometryFingerprint(sourceBytes, `${source.classId} source`);
  const afterGeometry = geometryFingerprint(bytes, `${source.classId} retexture`);
  const rewrite = providerGeometryRewrite(sourceDocument, document);
  if (rewrite.dimensionScaleSpread > 0.0001 || Math.abs(rewrite.triangleDelta) > 500)
    throw new Error(`${source.classId}: retexture exceeded the bounded provider rewrite.`);
  return {
    classId: source.classId,
    taskId: run.taskId,
    endpoint: 'retexture',
    status: run.status,
    consumedCredits: run.consumedCredits,
    runDirectory: path.relative(repository, run.runDirectory),
    preview: path.relative(repository, path.join(run.runDirectory, 'provider-preview.png')),
    retextured: true,
    newInCurrentBatch: run.newInCurrentBatch,
    source: {
      taskId: source.taskId,
      rawModel: source.rawModel,
      geometrySha256: beforeGeometry,
    },
    providerGeometryRewrite: rewrite,
    materialDirection: run.materialAuthority,
    rawModel: {
      file: path.relative(repository, modelPath),
      bytes: bytes.length,
      sha256: sha256(bytes),
      geometrySha256: afterGeometry,
      triangles: triangleCount(document),
      meshes: document.meshes?.length ?? 0,
      materials: document.materials?.length ?? 0,
      textures: document.textures?.length ?? 0,
      imageMimeTypes: [
        ...new Set((document.images ?? []).map((image) => image.mimeType ?? 'unknown')),
      ],
    },
  };
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}

function providerGeometryRewrite(before, after) {
  const beforePrimitive = before.meshes?.[0]?.primitives?.[0];
  const afterPrimitive = after.meshes?.[0]?.primitives?.[0];
  const beforePosition = before.accessors?.[beforePrimitive?.attributes?.POSITION];
  const afterPosition = after.accessors?.[afterPrimitive?.attributes?.POSITION];
  if (!beforePosition?.min || !beforePosition?.max || !afterPosition?.min || !afterPosition?.max)
    throw new Error('Provider rewrite audit requires position accessor bounds.');
  const beforeDimensions = beforePosition.max.map((value, axis) => value - beforePosition.min[axis]);
  const afterDimensions = afterPosition.max.map((value, axis) => value - afterPosition.min[axis]);
  const dimensionScales = afterDimensions.map((value, axis) => value / beforeDimensions[axis]);
  return {
    kind: 'uniform provider rescale plus seam/tangent vertex rewrite',
    sourceTriangles: triangleCount(before),
    outputTriangles: triangleCount(after),
    triangleDelta: triangleCount(after) - triangleCount(before),
    sourcePositionVertices: beforePosition.count,
    outputPositionVertices: afterPosition.count,
    dimensionScales,
    dimensionScaleSpread: Math.max(...dimensionScales) - Math.min(...dimensionScales),
    normalizedReviewEnvelope: 0.99,
    exactGeometryFingerprintPreserved: false,
  };
}
