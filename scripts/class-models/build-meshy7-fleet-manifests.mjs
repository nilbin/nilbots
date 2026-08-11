#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const root = path.join(
  repository,
  'art/class-look-concepts/arc-relay/meshy7-hover-roster-v1/meshy7-input-candidates',
);
const identitySha256 = '3d60322ef767283da55dc3710af9ef097ed989bdd40cc597883925989af486d5';
const classes = {
  palisade: 'Workwear-cobalt satin armor, antique-gold projector ribs and bounded trim, dark-steel structure, restrained edge wear and recess grime.',
  towline: 'Mustard-ochre satin armor, gunmetal inner frame, worn unpainted-steel captive winch and edge hardware, restrained edge wear and recess grime.',
  patchbay: 'Muted-sage satin armor, dusty-coral repair panels, warm-bone repair arm and trim, charcoal joints, restrained edge wear and recess grime.',
  lantern: 'Eggplant-purple satin armor, aged-ivory scanner casing, tarnished-brass rings and survey hardware, charcoal flare-cell face, restrained edge wear and recess grime.',
  mortar: 'Field-olive faceted satin armor, aged-brass sealed iris and polygonal collar accents, charcoal underside and stabilizers, restrained edge wear and recess grime.',
  minesmith: 'Oxide-terracotta satin armor, muted-mustard rim and forge accents, gunmetal underside and forge rollers, restrained edge wear and recess grime.',
  hush: 'Medium smoke-plum satin lobes, lighter lavender-gray armor bands, satin-silver structural channels, medium-charcoal sealed Null core, restrained edge wear and recess grime.',
  relay: 'Oxide-brick satin outer arms, warm-cream catcher tips, tarnished-brass guide-rail hardware, charcoal empty cradle and underside, restrained edge wear and recess grime.',
  switchback: 'One aged-ivory satin lobe and one charcoal satin lobe, tarnished-brass protected link coils, dark-steel underside, restrained edge wear and recess grime.',
  longshot: 'Medium steel-blue satin side armor, desaturated-navy lower hull, brushed-silver embedded dorsal spine and staged cells, charcoal underside, restrained edge wear and recess grime.',
  mason: 'Muted-ochre satin side armor and guide forks, blue-gray forge casing, charcoal empty bay and underside, brushed-steel wear surfaces, restrained edge wear and recess grime.',
  sunder: 'Medium mulberry satin faceted armor, aged-ivory designator housing and three brackets, graphite inset faces and underside, dull-brass hardware, restrained edge wear and recess grime.',
  repulsor: 'Medium burgundy-brick satin field-coil armor, oxidized rose-gold hub rings and coil trim, basalt-gray core and underside, restrained edge wear and recess grime.',
  veil: 'Medium jade-sage satin crescent armor, warm-sand sealed canisters and lower trim, charcoal nose, sockets, and underside, dull-brass hardware, restrained edge wear and recess grime.',
  nest: 'Medium pine-sage satin rack armor, warm-umber rails and front corners, aubergine sealed cell lids, charcoal service lane and underside, dull-brass hardware, restrained edge wear and recess grime.',
};
const views = [
  ['strict-front', '01-front.png', true],
  ['strict-right', '02-right.png', false],
  ['strict-back', '03-back.png', false],
  ['front-right-three-quarter', '04-front-right-three-quarter.png', false],
];

for (const [classId, materials] of Object.entries(classes)) {
  const directory = path.join(root, `${classId}-v1`);
  const meshyInputs = [];
  for (const [index, [view, file, primary]] of views.entries()) {
    const bytes = await readFile(path.join(directory, file));
    const dimensions = pngDimensions(bytes);
    if (dimensions.width !== 1536 || dimensions.height !== 1024)
      throw new Error(`${classId}/${file} is ${dimensions.width}x${dimensions.height}.`);
    meshyInputs.push({
      order: index + 1,
      view,
      file,
      sha256: sha256(bytes),
      width: dimensions.width,
      height: dimensions.height,
      primary,
    });
  }
  const sheetBytes = await readFile(path.join(directory, 'turnaround-sheet.png'));
  const promptBytes = await readFile(path.join(directory, 'PROMPTS.md'));
  const manifest = {
    schemaVersion: 1,
    status: 'paid-generation-authorized',
    classId,
    identitySource: {
      file: '../../concept-sheet-v7.png',
      sha256: identitySha256,
    },
    turnaroundSource: {
      file: 'turnaround-sheet.png',
      sha256: sha256(sheetBytes),
      promptLog: 'PROMPTS.md',
      promptLogSha256: sha256(promptBytes),
    },
    meshyInputs,
    generationPromptLog: 'PROMPTS.md',
    providerRunCollection: 'arc-relay-meshy7-fleet',
    meshyRequestPlan: {
      endpoint: '/openapi/v1/multi-image-to-3d',
      ai_model: 'meshy-7',
      should_texture: true,
      enable_pbr: true,
      texture_resolution: '2k',
      should_remesh: false,
      image_enhancement: false,
      remove_lighting: true,
      pose_mode: '',
      texture_prompt:
        `Mature industrial ${classId} levitating Arc Relay bot. ${materials} ` +
        'Preserve crisp bounded material regions, broad silhouette, signature-skill hardware, and the flush front beam-emitter seam. No text, logos, decals, eyes, face, guns, barrels, white nose, legs, teal/orange team color, glow, bloom, exhaust, or baked lighting.',
      target_formats: ['glb'],
      pilotCreditCap: 30,
      authorized: true,
      authorization:
        'Owner approved the V7 identities, directed the remaining fleet to proceed after the Kestrel pilot, confirmed the available credits, and excluded auto-rigging.',
      authorizationConsumed: false,
      autoRigging: false,
    },
    creditCheck: {
      checkedAt: '2026-08-11',
      balance: 2180,
      texturedMeshy7MultiImagePerTask: 30,
      pendingFleetTaskCount: 15,
      authorizedFleetCredits: 450,
      expectedBalanceAfterFleet: 1730,
      autoRigging: false,
      pricingSource: 'https://docs.meshy.ai/en/api/pricing',
    },
    nextGate: 'raw Meshy 7 first-pass download and owner review; no runtime promotion',
  };
  await writeFile(path.join(directory, 'manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`);
  console.log(`Wrote ${classId}-v1/manifest.json`);
}

function pngDimensions(bytes) {
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  if (bytes.length < 24 || !bytes.subarray(0, 8).equals(signature))
    throw new Error('Input is not a valid PNG.');
  return { width: bytes.readUInt32BE(16), height: bytes.readUInt32BE(20) };
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}
