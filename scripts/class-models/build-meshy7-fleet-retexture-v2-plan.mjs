#!/usr/bin/env node

import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const sourceAuditPath = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet/fleet-audit.json',
);
const outputPath = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet-retexture-v2/plan.json',
);
const sourceAudit = JSON.parse(readFileSync(sourceAuditPath, 'utf8'));

const palettes = new Map([
  ['palisade', 'dusted denim-blue armor, putty-gray shield planes, aged-nickel projector ribs'],
  ['towline', 'muted flax armor, mushroom-taupe side panels, worn pewter winch and edge hardware'],
  ['patchbay', 'eucalyptus-sage armor, dusty-mauve repair panels, warm-stone repair trim'],
  ['lantern', 'heather-lilac armor, parchment scanner casing, aged-pewter survey rings'],
  ['mortar', 'lichen-khaki armor, pale-limestone launch deck, muted-brass sealed iris'],
  ['minesmith', 'faded brick-red armor, biscuit-beige forge panels, worn-pewter rollers'],
  ['hush', 'lavender-gray lobes, mushroom-beige armor bands, matte pewter structural channels'],
  ['relay', 'desaturated periwinkle-gray arms, muted-flax catcher tips, aged-nickel guide rails'],
  ['switchback', 'one pale-greige lobe and one soft moss-gray lobe, warm-nickel protected coils'],
  ['longshot', 'cool cement-gray armor, muted-burgundy staged cells, brushed-nickel dorsal spine'],
  ['mason', 'muted clay-beige side armor, dusty slate-blue forge casing, worn-steel guide forks'],
  ['sunder', 'dusty-rose faceted armor, oatmeal designator brackets, medium-gray inset faces'],
  ['repulsor', 'muted mulberry coil armor, pale-bronze hub rings, medium basalt-gray core'],
  ['veil', 'pale yellow-sage crescent armor, warm-sand canisters, aged-pewter hardware'],
  ['nest', 'desaturated olive rack armor, faded-plum sealed lids, warm-gray rails and corners'],
]);

const entries = sourceAudit.entries
  .filter((entry) => entry.classId !== 'kestrel')
  .map((entry) => {
    const palette = palettes.get(entry.classId);
    if (!palette) throw new Error(`${entry.classId}: missing restrained retexture palette.`);
    const prompt =
      `Mature industrial ${entry.classId} levitating Arc Relay bot. High-roughness matte ` +
      `powder-coated ceramic and painted metal: ${palette}. Soft diffuse finish, fine dry ` +
      `surface grain, restrained wear, metal only on mechanisms. Medium-light values readable ` +
      `in a dark arena; charcoal only in narrow recesses. Preserve every material boundary. ` +
      `No glossy plastic, clearcoat, lacquer, wet shine, broad highlights, saturated or neon ` +
      `color, black body, pure white, teal, cyan, orange, glow, text, decals, eyes, guns, or legs.`;
    if (prompt.length > 600)
      throw new Error(`${entry.classId}: prompt is ${prompt.length} characters, over Meshy's cap.`);
    return {
      classId: entry.classId,
      source: {
        taskId: entry.taskId,
        endpoint: 'multi-image-to-3d',
        rawModel: entry.rawModel,
      },
      request: {
        ai_model: 'meshy-7',
        text_style_prompt: prompt,
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

if (entries.length !== 15 || palettes.size !== 15)
  throw new Error('The retexture pass must contain every non-Kestrel class exactly once.');

const plan = {
  schemaVersion: 1,
  status: 'paid-retexture-authorized',
  purpose:
    'Owner-directed matte, medium-value, restrained-color second texture pass; Kestrel retained unchanged',
  sourceAudit: path.relative(repository, sourceAuditPath),
  provider: 'Meshy',
  endpoint: '/openapi/v1/retexture',
  createdAt: '2026-08-11',
  authorization:
    'Owner identified shiny/plastic finishes, dark bodies, overly strong colors, and repeated palettes; directed a restrained retexture pass.',
  balanceCheck: {
    checkedAt: '2026-08-11',
    balance: 1730,
    creditsPerTask: 10,
    taskCount: entries.length,
    totalCreditCap: entries.reduce((sum, entry) => sum + entry.creditCap, 0),
    expectedBalanceAfter: 1580,
    pricingSource: 'https://docs.meshy.ai/en/api/pricing',
  },
  settingsSource: 'https://docs.meshy.ai/en/api/retexture',
  runtimeAssetsPromoted: false,
  entries,
};

mkdirSync(path.dirname(outputPath), { recursive: true });
writeFileSync(outputPath, `${JSON.stringify(plan, null, 2)}\n`);
console.log(`Wrote ${path.relative(repository, outputPath)} with ${entries.length} retextures.`);
