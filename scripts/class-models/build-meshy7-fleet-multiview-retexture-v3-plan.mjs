#!/usr/bin/env node

import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const outputDirectory = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet-retexture-v3-multiview',
);
const outputPath = path.join(outputDirectory, 'plan.json');
const sourceAuditPath = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet/fleet-audit.json',
);
const styleManifestPath = path.join(
  repository,
  'art/class-look-concepts/arc-relay/meshy7-hover-roster-v1/meshy7-brighter-texture-inputs-v1/manifest.json',
);
const palisadePlanPath = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet-retexture-v3-calibration/plan.json',
);
if (existsSync(outputPath)) {
  const existing = JSON.parse(readFileSync(outputPath, 'utf8'));
  if (existing.entries?.some((entry) => entry.completedTaskId))
    throw new Error('Refusing to replace a V3 multiview plan after provider submission.');
}
const sourceAudit = JSON.parse(readFileSync(sourceAuditPath, 'utf8'));
const styleManifest = JSON.parse(readFileSync(styleManifestPath, 'utf8'));
const palisadePlan = JSON.parse(readFileSync(palisadePlanPath, 'utf8'));
const classOrder = [
  'towline',
  'patchbay',
  'lantern',
  'mortar',
  'minesmith',
  'hush',
  'relay',
  'switchback',
  'longshot',
  'mason',
  'sunder',
  'repulsor',
  'veil',
  'nest',
];
if (
  styleManifest.status !== 'paid-retexture-inputs-authorized' ||
  styleManifest.materialPolicy?.finish !== 'matte/satin high-roughness industrial surfaces' ||
  !styleManifest.materialPolicy?.sourceDirection?.includes('satin roughness') ||
  !styleManifest.materialPolicy?.sourceDirection?.includes('no toy gloss') ||
  palisadePlan.status !== 'owner-review' ||
  palisadePlan.consumedCredits !== 10
)
  throw new Error('The approved Palisade calibration or brighter style authority is missing.');
const sourceByClass = new Map(sourceAudit.entries.map((entry) => [entry.classId, entry]));
const styleByClass = new Map(styleManifest.entries.map((entry) => [entry.classId, entry]));

const entries = classOrder.map((classId) => {
  const source = sourceByClass.get(classId);
  const style = styleByClass.get(classId);
  if (
    !source ||
    !style ||
    style.retainedUnchanged ||
    style.inputs?.length !== 4 ||
    style.inputs[0]?.view !== 'strict-front' ||
    style.inputs[0]?.primary !== true ||
    style.inputs.some((input) => !input.transformApplied)
  )
    throw new Error(`${classId}: incomplete brighter multiview authority.`);
  if (
    !Number.isFinite(style.valueAudit?.medianLuminance) ||
    style.valueAudit.medianLuminance <= styleManifest.brightnessReference.medianLuminance
  )
    throw new Error(`${classId}: value audit is not above approved Palisade.`);
  return {
    classId,
    source: {
      taskId: source.taskId,
      endpoint: 'multi-image-to-3d',
      rawModel: source.rawModel,
    },
    styleAuthority: {
      kind: 'ordered-multiview-brighter-v1',
      valueAudit: style.valueAudit,
      inputs: style.inputs.map((input) => ({
        order: input.order,
        view: input.view,
        primary: input.primary,
        file: input.output,
        sha256: input.outputSha256,
        width: input.width,
        height: input.height,
      })),
    },
    request: {
      ai_model: 'meshy-7',
      enable_original_uv: true,
      enable_pbr: true,
      texture_resolution: '2k',
      alpha_thumbnail: true,
      target_formats: ['glb'],
    },
    creditCap: 10,
    authorized: true,
  };
});
if (entries.length !== 14 || new Set(entries.map((entry) => entry.classId)).size !== 14)
  throw new Error('V3 multiview plan must contain fourteen unique remaining classes.');

const plan = {
  schemaVersion: 1,
  status: 'paid-retexture-authorized',
  purpose:
    'Owner-approved Meshy 7 ordered-multiview fleet retexture using the successful Kestrel/Palisade route and brighter-than-Palisade tonal authority for the remaining fourteen classes',
  sourceAudit: path.relative(repository, sourceAuditPath),
  styleManifest: path.relative(repository, styleManifestPath),
  retained: {
    kestrel: {
      taskId: sourceByClass.get('kestrel').taskId,
      reviewArtifact:
        'art/class-models/runtime-tiers/arc-relay/meshy7-kestrel-review-v1/arc-kestrel-lean.glb',
    },
    palisade: {
      taskId: palisadePlan.completedTaskId,
      reviewArtifact:
        'art/class-models/runtime-tiers/arc-relay/meshy7-palisade-multiview-retexture-calibration-v1/arc-palisade-lean.glb',
    },
  },
  provider: 'Meshy',
  endpoint: '/openapi/v1/retexture',
  createdAt: '2026-08-11',
  authorization:
    'Owner approved the Palisade multiview calibration and directed the remaining fleet to use brighter dominant values than Palisade blue.',
  materialPolicy: {
    ...styleManifest.materialPolicy,
    enforcement:
      'Only the pinned four-view matte/satin V7 derivatives are submitted; the retexture request contains no competing text-style input.',
  },
  brightnessPolicy: {
    referenceClass: 'palisade',
    referenceMedianLuminance: styleManifest.brightnessReference.medianLuminance,
    transform: styleManifest.transform,
    enforcement:
      'Every remaining front-right-oblique style derivative must have a measured object median above Palisade before submission.',
  },
  balanceCheck: {
    checkedAt: '2026-08-11',
    balance: 1570,
    creditsPerTask: 10,
    taskCount: entries.length,
    totalCreditCap: entries.reduce((sum, entry) => sum + entry.creditCap, 0),
    expectedBalanceAfter: 1430,
    pricingSource: 'https://docs.meshy.ai/en/api/pricing',
  },
  settingsSource: 'https://docs.meshy.ai/en/api/retexture',
  runtimeAssetsPromoted: false,
  entries,
};
mkdirSync(outputDirectory, { recursive: true });
writeFileSync(outputPath, `${JSON.stringify(plan, null, 2)}\n`);
console.log(`Wrote ${path.relative(repository, outputPath)} with ${entries.length} tasks.`);
