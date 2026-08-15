#!/usr/bin/env node

/** Build four coherent Meshy inputs and a separately authorized manifest per full rank. */

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const sharp = requireFromWeb('sharp');
const root = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-veterancy-modules-v1/inputs/veterancy-split-diadem-whole-v1',
);
const concept =
  '../../../../../../reviews/arc-relay-veterancy-modules-meshy7-v1/correction-v5/motion-safe-split-diadem-rank-1-5-clean.png';
const clearance =
  '../../../../../../reviews/arc-relay-veterancy-modules-meshy7-v1/correction-v5/motion-safe-split-diadem-rank-1-5-clearance.png';
const viewSpecs = [
  { file: '01-front-three-quarter.png', view: 'front-three-quarter', primary: true, column: 0, row: 0 },
  { file: '02-right-side.png', view: 'right-side', primary: false, column: 1, row: 0 },
  { file: '03-rear-three-quarter.png', view: 'rear-three-quarter', primary: false, column: 0, row: 1 },
  { file: '04-strict-top.png', view: 'strict-top', primary: false, column: 1, row: 1 },
];

const records = [];
for (let rank = 1; rank <= 5; rank += 1) records.push(await buildRank(rank));
await writeFile(
  path.join(root, 'collection.json'),
  `${JSON.stringify({
    schemaVersion: 1,
    id: 'veterancy-split-diadem-whole-v1',
    status: 'owner-approved-provider-inputs',
    ownerApproval:
      "Owner approved five complete rank-specific Meshy ornaments on 2026-08-15, conditional on fidelity to the approved conceptual art, with a 150-credit collection cap.",
    totalCreditCap: 150,
    perTaskCreditCap: 30,
    ranks: records,
  }, null, 2)}\n`,
);
await buildReviewBoard(records);
console.log(`Built five Meshy manifests under ${path.relative(repository, root)}.`);

async function buildRank(rank) {
  const sourceFile = path.join(root, 'source-turnarounds', `rank-${rank}.png`);
  const sourceBytes = await readFile(sourceFile);
  const sourceMetadata = await sharp(sourceBytes).metadata();
  const width = sourceMetadata.width ?? 0;
  const height = sourceMetadata.height ?? 0;
  if (width < 1024 || height < 1024)
    throw new Error(`Rank ${rank} source sheet is smaller than 1024px.`);
  const halfWidth = Math.floor(width / 2);
  const halfHeight = Math.floor(height / 2);
  const directory = path.join(root, `rank-${rank}`);
  await mkdir(directory, { recursive: true });

  const inputs = [];
  for (const [index, view] of viewSpecs.entries()) {
    const left = view.column === 0 ? 4 : halfWidth + 4;
    const top = view.row === 0 ? 4 : halfHeight + 4;
    const cropWidth = (view.column === 0 ? halfWidth : width - halfWidth) - 8;
    const cropHeight = (view.row === 0 ? halfHeight : height - halfHeight) - 8;
    const output = path.join(directory, view.file);
    await sharp(sourceBytes)
      .extract({ left, top, width: cropWidth, height: cropHeight })
      .resize(1536, 1024, {
        fit: 'contain',
        kernel: sharp.kernel.lanczos3,
        background: '#eeeeee',
      })
      .flatten({ background: '#eeeeee' })
      .png({ compressionLevel: 9 })
      .toFile(output);
    const bytes = await readFile(output);
    inputs.push({
      order: index + 1,
      view: view.view,
      primary: view.primary,
      file: view.file,
      sha256: sha256(bytes),
      width: 1536,
      height: 1024,
    });
  }

  const classId = `veterancy-split-diadem-rank-${rank}-v1`;
  const bladeCount = (rank - 1) * 2;
  const manifest = {
    schemaVersion: 1,
    id: `${classId}-meshy7`,
    classId,
    label: `Veterancy Split Diadem Rank ${rank}`,
    status: 'owner-approved-provider-input',
    providerRunCollection: 'arc-veterancy-modules-v1',
    ownerApproval:
      "Owner approved five complete rank-specific Meshy ornaments on 2026-08-15, conditional on fidelity to the approved conceptual art. This manifest consumes at most 30 of the authorized 150 credits.",
    canonicalIdentity: {
      sourceSheet: `../source-turnarounds/rank-${rank}.png`,
      approvedRankConcept: concept,
      motionClearanceConcept: clearance,
      rank,
      bladeCount,
      runtimeIntent:
        'One complete bilateral ornament replaces the corresponding renderer-built rank. It follows chassis yaw, stays world-up, and never alters Core animation.',
    },
    meshyInputs: inputs,
    inputGate: {
      status: 'passed-owner-concept-fidelity-review',
      oneObjectPerTask: true,
      disconnectedHalvesRequired: true,
      separateImages: true,
      plainBackground: true,
      diffuseLighting: true,
      castShadow: false,
      minimumShortestSide: 1024,
      exactBladeCount: bladeCount,
      family:
        'complete open bilateral mechanical moose-antler diadem in matte desaturated aged brass',
      angleNote:
        'Four coherent views of the unchanged full assembly: front three-quarter primary, strict right side, rear three-quarter, and strict top.',
    },
    meshyRequestPlan: {
      authorized: true,
      authorizationConsumed: false,
      authorization:
        `One Meshy 7 multiview generation for rank ${rank}, hard 30-credit cap within the owner-approved 150-credit collection; no remesh, retexture, auto-rig, or second task.`,
      pilotCreditCap: 30,
      primaryView: 'front-three-quarter',
      ai_model: 'meshy-7',
      should_texture: true,
      enable_pbr: true,
      texture_resolution: '2k',
      should_remesh: false,
      image_enhancement: false,
      remove_lighting: true,
      pose_mode: '',
      texture_prompt: texturePrompt(rank, bladeCount),
      target_formats: ['glb'],
      autoRigging: false,
    },
    creditCheck: {
      pricingSource: 'https://docs.meshy.ai/en/api/pricing',
      parameterSource: 'https://docs.meshy.ai/en/api/multi-image-to-3d',
      pricingVerifiedAt: '2026-08-15',
      expectedCredits: 30,
      hardCreditCap: 30,
      collectionHardCreditCap: 150,
      balanceBeforePreparation: 800,
    },
    nextGate: 'validate all five manifests, then submit exactly once per rank',
  };
  const manifestPath = path.join(directory, 'manifest.json');
  await writeFile(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
  return {
    rank,
    classId,
    bladeCount,
    source: path.relative(root, sourceFile),
    sourceSha256: sha256(sourceBytes),
    manifest: path.relative(root, manifestPath),
    inputs,
  };
}

function texturePrompt(rank, bladeCount) {
  return (
    `Complete Nilbots veterancy rank ${rank} split diadem: exactly two disconnected ` +
    `bilateral rail halves and exactly ${bladeCount} total clipped-tip mechanical blades. ` +
    'Preserve the wide full-height empty center corridor and open moose-antler silhouette. ' +
    'Rough matte desaturated aged warm brass/bronze plates; graphite only in recessed seams; ' +
    'dim warm-gold edge channels; sparse wear. Broad readable facets, shallow thickness. ' +
    'No bot, Core, bridge, connector, center spike, ring, roof, pedestal, organic antler, wing, ' +
    'weapon, text, team teal/cyan, saturated orange, blue, red, white plastic, chrome, lacquer, ' +
    'baked lighting, shadow, or extra blades.'
  );
}

async function buildReviewBoard(records) {
  const cardWidth = 560;
  const cardHeight = 420;
  const headerHeight = 100;
  const width = cardWidth * records.length;
  const composites = [{ input: headerSvg(width, headerHeight), left: 0, top: 0 }];
  for (const [index, record] of records.entries()) {
    const hero = path.join(root, `rank-${record.rank}`, '01-front-three-quarter.png');
    composites.push(
      { input: rankLabelSvg(cardWidth, 54, record), left: index * cardWidth, top: headerHeight },
      {
        input: await sharp(hero)
          .resize(cardWidth, cardHeight - 54, { fit: 'contain', background: '#eeeeee' })
          .png()
          .toBuffer(),
        left: index * cardWidth,
        top: headerHeight + 54,
      },
    );
  }
  await sharp({
    create: { width, height: headerHeight + cardHeight, channels: 4, background: '#090e13' },
  })
    .composite(composites)
    .png()
    .toFile(path.join(root, 'input-review.png'));
}

function headerSvg(width, height) {
  return Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">
    <rect width="100%" height="100%" fill="#090e13"/>
    <text x="24" y="40" fill="#edf3f6" font-family="Menlo,monospace" font-size="27" font-weight="700">WHOLE SPLIT DIADEM · RANKS 1–5 · MESHY 7 INPUT GATE</text>
    <text x="24" y="72" fill="#b9c5cc" font-family="Menlo,monospace" font-size="16">five complete ornaments · open Core corridor · matte neutral brass · 150-credit hard cap · no rigging</text>
  </svg>`);
}

function rankLabelSvg(width, height, record) {
  return Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">
    <rect width="100%" height="100%" fill="#121a22"/>
    <text x="18" y="34" fill="#f0d99a" font-family="Menlo,monospace" font-size="19" font-weight="700">RANK ${record.rank} · ${record.bladeCount} BLADES</text>
  </svg>`);
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
