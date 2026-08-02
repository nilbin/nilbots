#!/usr/bin/env node

/**
 * Deterministically build the sixteen Arc Relay GLB companions.
 *
 * No DCC and no provider participates in this route. Orthographic named-group
 * vector sources provide the actual planform and height layers; the premium
 * baked-oblique raster masters remain archived taste references rather than
 * being projected onto a second camera plane. Runtime files are generated.
 */

import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = join(dirname(fileURLToPath(import.meta.url)), '..');
const requireFromWeb = createRequire(join(repository, 'web', 'package.json'));
const sharp = requireFromWeb('sharp');
const THREE = requireFromWeb('three');
const recipePath = join(
  repository,
  'art',
  'class-models',
  'arc-relay',
  'fleet.json',
);
const recipeRoot = dirname(recipePath);
const checkOnly = process.argv.includes('--check');
const unknown = process.argv.slice(2).filter((value) => value !== '--check');
if (unknown.length > 0)
  throw new Error('Unknown arguments: ' + unknown.join(', '));

let recipe;
let requiredGroups;

async function main() {
recipe = JSON.parse(await readFile(recipePath, 'utf8'));
validateFleet(recipe);
const generatorBytes = await readFile(join(repository, recipe.generator));

requiredGroups = recipe.requiredGroups;
const sourceRoot = join(repository, recipe.sourceRoot);
const archivedMasterRoot = join(repository, recipe.archivedMasterRoot);
const runtimeRoot = join(repository, recipe.runtimeRoot);
const sourceOutputRoot = join(recipeRoot, 'sources');
const pending = [];
const ledgerEntries = [];

for (const entry of recipe.classes) {
  const runtimeId = 'arc-' + entry.id;
  const sourceDirectory = join(sourceOutputRoot, runtimeId);
  const runtimeDirectory = join(runtimeRoot, runtimeId);
  const vectorPath = join(sourceRoot, 'arc-' + entry.id + '.svg');
  const archivedAlbedoPath = join(archivedMasterRoot, entry.id + '-base.png');
  const archivedAccentPath = join(archivedMasterRoot, entry.id + '-team-mask.png');
  const vector = await readFile(vectorPath);
  const archivedAlbedo = await readFile(archivedAlbedoPath);
  const archivedAccent = await readFile(archivedAccentPath);
  const raster = await layeredRasterSource(vector, recipe.textureSize);
  const normal = await normalMap(raster, recipe.heightfieldCells);
  const built = buildModel(
    entry,
    recipe,
    raster,
    raster.albedoPng,
    normal,
    raster.emissivePng,
  );
  const modelHash = sha256(built.glb);
  const sourceHash = sha256(
    Buffer.from(
      stableJson({
        generator: recipe.generator,
        generatorVersion: recipe.generatorVersion,
        generatorSha256: sha256(generatorBytes),
        recipe: entry,
        vectorSha256: sha256(vector),
        archivedAlbedoSha256: sha256(archivedAlbedo),
        archivedTeamMaskSha256: sha256(archivedAccent),
        albedoSha256: sha256(raster.albedoPng),
        normalSha256: sha256(normal),
        emissiveSha256: sha256(raster.emissivePng),
      }),
    ),
  );
  const ledger = {
    id: runtimeId,
    bytes: built.glb.length,
    sha256: modelHash,
    triangles: built.triangles,
    vertices: built.vertices,
    materials: built.materials,
    textures: [
      { role: 'albedo', width: raster.width, height: raster.height, bytes: raster.albedoPng.length },
      { role: 'normal', width: raster.width, height: raster.height, bytes: normal.length },
      { role: 'emissive', width: raster.width, height: raster.height, bytes: raster.emissivePng.length },
    ],
    bounds: built.bounds,
  };
  if (ledger.bytes > recipe.assetBudgetBytes)
    throw new Error(
      runtimeId + ' exceeds ' + recipe.assetBudgetBytes + ' B budget: ' + ledger.bytes,
    );
  const hardwareLagTicks =
    entry.handling === 'swift' ? 0.18 : entry.handling === 'deliberate' ? 0.48 : 0.31;
  const manifest = {
    version: 1,
    id: runtimeId,
    file: 'model.glb',
    kind: 'bot',
    part: 'whole',
    facing: '+x',
    up: '+y',
    source: {
      generator: recipe.generator,
      recipe: relative(repository, recipePath),
      layeredSource: relative(repository, join(sourceDirectory, 'layers.json')),
      sourceSha256: sourceHash,
    },
    nodes: {
      locomotion: 'underbody-locomotion',
      chassis: 'chassis',
      hardware: 'weapon-hardware',
      teamAccents: 'team-accents',
      emissives: 'emissives',
      idle: entry.idleNodes ?? [],
    },
    motion: {
      locomotion: entry.locomotion,
      handling: entry.handling,
      hardwareLagTicks,
      hardwareOvershoot: entry.handling === 'swift' ? 0.12 : 0,
    },
    signature: entry.signature,
    ledger: {
      bytes: ledger.bytes,
      sha256: ledger.sha256,
      triangles: ledger.triangles,
      materials: ledger.materials,
      textureCount: ledger.textures.length,
    },
  };
  const layers = {
    version: 1,
    id: runtimeId,
    canonicalLayeredVector: relative(repository, vectorPath),
    generatedAlbedo: relative(repository, join(sourceDirectory, 'albedo.png')),
    archivedAlbedo: relative(repository, archivedAlbedoPath),
    archivedTeamMask: relative(repository, archivedAccentPath),
    archivedMasterRoot: relative(repository, archivedMasterRoot),
    archivedMasterProjectionDegrees: recipe.archivedMasterProjectionDegrees,
    generatedNormal: relative(repository, join(sourceDirectory, 'normal.png')),
    generatedEmissive: relative(repository, join(sourceDirectory, 'emissive.png')),
    sourceSha256: sourceHash,
    groups: {
      'underbody-locomotion': {
        construction: entry.locomotion,
        floorY: 0,
        extrusionHeight: locomotionHeight(entry.locomotion),
        bevel: 0.018,
      },
      chassis: {
        construction: 'orthographic-vector-distance-relief',
        sourceProjectionDegrees: recipe.sourceProjectionDegrees,
        extrusionHeight: 0.14,
        domeHeight: 0.1,
        bevel: 0.014,
        albedo: relative(repository, join(sourceDirectory, 'albedo.png')),
        normal: relative(repository, join(sourceDirectory, 'normal.png')),
      },
      'weapon-hardware': {
        construction: 'named-vector-group-distance-relief',
        sourceGroup: 'weapon-hardware',
        hardwareIdentity: entry.hardware,
        extrusionHeight: 0.225,
        domeHeight: 0.065,
        pivot: [0, 0, 0],
      },
      'team-accents': {
        construction: 'named-vector-semantic-surface',
        sourceGroup: 'team-accents',
        materialRole: 'team-accent',
        bakedTeamColor: false,
      },
      emissives: {
        construction: 'derived-authored-light-surface',
        map: relative(repository, join(sourceDirectory, 'emissive.png')),
      },
    },
  };

  pending.push(
    [join(runtimeDirectory, 'model.glb'), built.glb],
    [join(runtimeDirectory, 'model3d.json'), Buffer.from(stableJson(manifest))],
    [join(sourceDirectory, 'layers.json'), Buffer.from(stableJson(layers))],
    [join(sourceDirectory, 'albedo.png'), raster.albedoPng],
    [join(sourceDirectory, 'normal.png'), normal],
    [join(sourceDirectory, 'emissive.png'), raster.emissivePng],
  );
  ledgerEntries.push(ledger);
}

const fleetLedger = {
  version: 1,
  generator: recipe.generator,
  budgetBytesPerLook: recipe.assetBudgetBytes,
  totals: {
    bytes: ledgerEntries.reduce((sum, entry) => sum + entry.bytes, 0),
    triangles: ledgerEntries.reduce((sum, entry) => sum + entry.triangles, 0),
    materials: ledgerEntries.reduce((sum, entry) => sum + entry.materials, 0),
    textures: ledgerEntries.reduce((sum, entry) => sum + entry.textures.length, 0),
  },
  looks: ledgerEntries,
};
pending.push([join(recipeRoot, 'ledger.json'), Buffer.from(stableJson(fleetLedger))]);

for (const [path, bytes] of pending) {
  if (checkOnly) {
    let current;
    try {
      current = await readFile(path);
    } catch {
      throw new Error(relative(repository, path) + ' is missing; run the builder');
    }
    if (!current.equals(bytes))
      throw new Error(relative(repository, path) + ' is stale; run the builder');
  } else {
    await mkdir(dirname(path), { recursive: true });
    await writeFile(path, bytes);
  }
}

console.log(
  (checkOnly ? 'PASS' : 'Built') +
    ' ' +
    recipe.classes.length +
    ' Arc Relay GLBs · ' +
    fleetLedger.totals.bytes +
    ' B · ' +
    fleetLedger.totals.triangles +
    ' tris',
);
}

function validateFleet(input) {
  if (input?.version !== 1 || !Array.isArray(input.classes))
    throw new Error('fleet.json must be version 1 with classes');
  if (!Array.isArray(input.requiredGroups) || input.requiredGroups.length !== 5)
    throw new Error('fleet.json must name five required groups');
  if (
    input.generatorVersion !== 3 ||
    input.sourceProjectionDegrees !== 0 ||
    input.archivedMasterProjectionDegrees !== 20
  )
    throw new Error('fleet.json must name orthographic relief pipeline version 3');
  if (
    typeof input.sourceRoot !== 'string' ||
    typeof input.archivedMasterRoot !== 'string' ||
    input.textureSize !== 256 ||
    input.heightfieldCells !== 48
  )
    throw new Error('fleet.json must preserve the reviewed v3 source sampling contract');
  const expected = [
    'underbody-locomotion',
    'chassis',
    'weapon-hardware',
    'team-accents',
    'emissives',
  ];
  if (input.requiredGroups.join('|') !== expected.join('|'))
    throw new Error('fleet.json group order is part of deterministic output');
  if (input.classes.length !== 16)
    throw new Error('Arc Relay launch fleet must contain sixteen classes');
  const seen = new Set();
  for (const entry of input.classes) {
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(entry.id) || seen.has(entry.id))
      throw new Error('Invalid or duplicate class id ' + entry.id);
    seen.add(entry.id);
    if (!['low-hover', 'treads', 'wheels', 'skids'].includes(entry.locomotion))
      throw new Error(entry.id + ' has invalid locomotion');
    if (!['swift', 'standard', 'deliberate'].includes(entry.handling))
      throw new Error(entry.id + ' has invalid handling');
    if (typeof entry.hardware !== 'string' || entry.hardware.length === 0)
      throw new Error(entry.id + ' needs a named hardware identity');
  }
}

const topLevelLayerIds = [
  'underbody-locomotion',
  'chassis-depth',
  'chassis',
  'hardware-depth',
  'weapon-hardware',
  'team-accents',
  'emissives',
];

/** Rasterize a named-group orthographic vector without surrendering its layer semantics. */
async function layeredRasterSource(vector, size) {
  const markup = vector.toString('utf8');
  for (const id of topLevelLayerIds)
    if (!markup.includes(`id="${id}"`))
      throw new Error(`Layered vector is missing #${id}`);
  const modelMarkup = thinStrokeWidths(markup);

  // Team light is deliberately absent from the identity texture. The semantic surface
  // above it is the only owner of team colour, so a cyan source pixel can never leak
  // through when the renderer assigns amber. The two baked depth groups are also absent:
  // actual sidewalls and shadows now provide that information, and drawing both produced
  // the heavy black triple-contour that failed the first v3 taste pass.
  const albedoMarkup = injectLayerStyle(
    modelMarkup,
    ['chassis-depth', 'hardware-depth', 'team-accents'],
    false,
  );
  const albedoPng = await sharp(Buffer.from(albedoMarkup), { density: 192 })
    .resize(size, size, { fit: 'fill' })
    .png({ compressionLevel: 9, adaptiveFiltering: false })
    .toBuffer();
  const albedo = await sharp(albedoPng)
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });

  const masks = {};
  for (const [name, visible] of Object.entries({
    underbody: ['underbody-locomotion'],
    chassis: ['chassis'],
    hardware: ['weapon-hardware'],
    accent: ['team-accents'],
    emissive: ['emissives'],
  })) {
    const hidden = topLevelLayerIds.filter((id) => !visible.includes(id));
    const isolated = injectLayerStyle(modelMarkup, hidden, true);
    masks[name] = (
      await sharp(Buffer.from(isolated), { density: 192 })
        .resize(size, size, { fit: 'fill' })
        .ensureAlpha()
        .raw()
        .toBuffer({ resolveWithObject: true })
    ).data;
  }

  const hiddenForEmissive = topLevelLayerIds.filter((id) => id !== 'emissives');
  const emissiveMarkup = injectLayerStyle(modelMarkup, hiddenForEmissive, false);
  const emissivePng = await sharp(Buffer.from(emissiveMarkup), { density: 192 })
    .resize(size, size, { fit: 'fill' })
    .flatten({ background: { r: 0, g: 0, b: 0 } })
    .png({ compressionLevel: 9, adaptiveFiltering: false })
    .toBuffer();

  return {
    width: size,
    height: size,
    albedo: albedo.data,
    masks,
    albedoPng,
    emissivePng,
  };
}

/** Reduce sprite-era ink without changing the archived vector source itself. */
function thinStrokeWidths(markup) {
  return markup.replace(/stroke-width="([0-9]+(?:\.[0-9]+)?)"/g, (_, width) => {
    const authored = Number(width);
    // Outer ink is redundant with the real silhouette and bevel. Interior seams still
    // need to survive the gameplay camera, so they retain proportionally more weight.
    const factor = authored >= 12 ? 0.28 : authored >= 6 ? 0.48 : 0.75;
    const thinned = Math.max(0.35, authored * factor);
    return `stroke-width="${round6(thinned)}"`;
  });
}

/** Insert deterministic CSS immediately inside the root SVG. */
function injectLayerStyle(markup, hidden, mask) {
  const hiddenCss = hidden
    .map((id) => `#${id}`)
    .join(',');
  const maskCss = mask
    ? topLevelLayerIds
        .filter((id) => !hidden.includes(id))
        .map(
          (id) =>
            `#${id},#${id} *{opacity:1!important;filter:none!important}`,
        )
        .join('')
    : '';
  const css = `<style>${hiddenCss}{display:none!important}${maskCss}</style>`;
  return markup.replace(/<svg([^>]*)>/, `<svg$1>${css}`);
}

/** Normal texture follows the same distance relief as the actual vertices. */
async function normalMap(raster, cells) {
  const sampled = sampleMasks(raster, cells);
  const relief = reliefMaps(sampled, cells);
  const width = raster.width;
  const height = raster.height;
  const pixels = Buffer.alloc(width * height * 4);
  const sample = (x, y) =>
    bilinearGrid(
      relief.combined,
      cells,
      Math.max(0, Math.min(1, x / Math.max(1, width - 1))),
      Math.max(0, Math.min(1, y / Math.max(1, height - 1))),
    );
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      const dx = (sample(x + 1, y) - sample(x - 1, y)) * width * 0.48;
      const dz = (sample(x, y + 1) - sample(x, y - 1)) * height * 0.48;
      const normal = new THREE.Vector3(-dx, 1, -dz).normalize();
      const offset = (y * width + x) * 4;
      pixels[offset] = Math.round((normal.x * 0.5 + 0.5) * 255);
      pixels[offset + 1] = Math.round((normal.y * 0.5 + 0.5) * 255);
      pixels[offset + 2] = Math.round((normal.z * 0.5 + 0.5) * 255);
      pixels[offset + 3] = 255;
    }
  }
  return sharp(pixels, { raw: { width, height, channels: 4 } })
    .png({ compressionLevel: 9, adaptiveFiltering: false })
    .toBuffer();
}

function bilinearGrid(values, size, u, v) {
  const x = u * (size - 1);
  const y = v * (size - 1);
  const x0 = Math.floor(x);
  const y0 = Math.floor(y);
  const x1 = Math.min(size - 1, x0 + 1);
  const y1 = Math.min(size - 1, y0 + 1);
  const tx = x - x0;
  const ty = y - y0;
  const a = values[y0 * size + x0] * (1 - tx) + values[y0 * size + x1] * tx;
  const b = values[y1 * size + x0] * (1 - tx) + values[y1 * size + x1] * tx;
  return a * (1 - ty) + b * ty;
}

function buildModel(entry, fleet, raster, albedo, normal, emissive) {
  const cells = fleet.heightfieldCells;
  const sampled = sampleMasks(raster, cells);
  const relief = reliefMaps(sampled, cells);
  const planformWidth = normalizedPlanformWidth(sampled, cells);
  const underbody = heightfieldGeometry(sampled.underbody, relief.underbody, cells, {
    bottom: 0.018,
    base: 0.07,
    dome: 0.025,
    width: planformWidth,
    bevel: 0.01,
  });
  const chassis = heightfieldGeometry(sampled.chassis, relief.chassis, cells, {
    bottom: 0.045,
    base: 0.14,
    dome: 0.1,
    width: planformWidth,
    bevel: 0.014,
  });
  const hardwareMasks =
    entry.id === 'nest'
      ? splitMaskAcrossZ(sampled.hardware, cells)
      : [sampled.hardware];
  const hardware = hardwareMasks.map((mask) =>
    heightfieldGeometry(mask, relief.hardware, cells, {
      bottom: 0.115,
      base: 0.225,
      dome: 0.065,
      width: planformWidth,
      bevel: 0.012,
    }),
  );
  const accent = surfaceGeometry(sampled.accent, relief.combined, cells, {
    base: 0.004,
    dome: 1,
    width: planformWidth,
  });
  const light = surfaceGeometry(sampled.emissive, relief.combined, cells, {
    base: 0.007,
    dome: 1,
    width: planformWidth,
  });

  const locomotion = locomotionNodes(entry);
  const glb = new GlbBuilder(entry, requiredGroups, [albedo, normal, emissive]);
  const root = glb.node('arc-' + entry.id);
  const locomotionRoot = glb.node('underbody-locomotion');
  const chassisRoot = glb.node('chassis');
  const hardwareRoot = glb.node('weapon-hardware');
  const accentRoot = glb.node('team-accents', { nilbotsRole: 'team-accent' });
  const emissiveRoot = glb.node('emissives');
  glb.child(root, locomotionRoot);
  glb.child(root, chassisRoot);
  glb.child(root, hardwareRoot);
  glb.child(root, accentRoot);
  glb.child(root, emissiveRoot);

  if (underbody.getAttribute('position').count > 0)
    glb.child(
      locomotionRoot,
      glb.meshNode('underbody-layered-relief', underbody, 0),
    );
  for (const part of locomotion)
    glb.child(
      locomotionRoot,
      glb.meshNode(part.name, part.geometry, 0),
    );
  glb.child(chassisRoot, glb.meshNode('chassis-heightfield', chassis, 1));
  for (const [index, geometry] of hardware.entries())
    glb.child(
      hardwareRoot,
      glb.meshNode(hardwareNodeName(entry, index), geometry, 2),
    );
  if (accent.getAttribute('position').count > 0)
    glb.child(
      accentRoot,
      glb.meshNode('semantic-team-surfaces', accent, 3, {
        nilbotsRole: 'team-accent',
      }),
    );
  if (light.getAttribute('position').count > 0)
    glb.child(
      emissiveRoot,
      glb.meshNode('authored-emissive-surfaces', light, 4),
    );

  const output = glb.finish(root);
  return {
    glb: output.bytes,
    triangles: output.triangles,
    vertices: output.vertices,
    materials: 5,
    bounds: output.bounds,
  };
}

/** Fill the useful part of one tile while retaining a hard occupancy-safe gutter. */
function normalizedPlanformWidth(sampled, cells) {
  let minimumX = cells;
  let minimumY = cells;
  let maximumX = -1;
  let maximumY = -1;
  for (let y = 0; y < cells; y += 1) {
    for (let x = 0; x < cells; x += 1) {
      const index = y * cells + x;
      if (
        !sampled.underbody[index] &&
        !sampled.chassis[index] &&
        !sampled.hardware[index]
      )
        continue;
      minimumX = Math.min(minimumX, x);
      minimumY = Math.min(minimumY, y);
      maximumX = Math.max(maximumX, x);
      maximumY = Math.max(maximumY, y);
    }
  }
  const occupiedCells = Math.max(
    maximumX - minimumX + 1,
    maximumY - minimumY + 1,
  );
  if (occupiedCells <= 0) return 0.94;
  return Math.min(1.2, 0.9 / (occupiedCells / cells));
}

function hardwareNodeName(entry, index) {
  if (entry.id === 'lantern') return 'idle-lantern-dish';
  if (entry.id === 'nest')
    return index === 0 ? 'idle-nest-pod-left' : 'idle-nest-pod-right';
  return entry.hardware + '-relief';
}

function splitMaskAcrossZ(mask, size) {
  const near = new Uint8Array(mask.length);
  const far = new Uint8Array(mask.length);
  for (let y = 0; y < size; y += 1)
    for (let x = 0; x < size; x += 1)
      (y < size / 2 ? near : far)[y * size + x] = mask[y * size + x];
  return [near, far];
}

function reliefMaps(sampled, cells) {
  const underbody = distanceHeight(sampled.underbody, cells);
  const chassis = distanceHeight(sampled.chassis, cells);
  const hardware = distanceHeight(sampled.hardware, cells);
  const combined = new Float32Array(cells * cells);
  for (let index = 0; index < combined.length; index += 1) {
    const underbodyTop = sampled.underbody[index]
      ? 0.07 + underbody[index] * 0.025
      : 0;
    const chassisTop = sampled.chassis[index]
      ? 0.14 + chassis[index] * 0.1
      : 0;
    const hardwareTop = sampled.hardware[index]
      ? 0.225 + hardware[index] * 0.065
      : 0;
    combined[index] = Math.max(underbodyTop, chassisTop, hardwareTop);
  }
  return { underbody, chassis, hardware, combined };
}

function sampleMasks(raster, cells) {
  const underbody = new Uint8Array(cells * cells);
  const chassis = new Uint8Array(cells * cells);
  const hardware = new Uint8Array(cells * cells);
  const accent = new Uint8Array(cells * cells);
  const emissive = new Uint8Array(cells * cells);
  for (let y = 0; y < cells; y += 1) {
    for (let x = 0; x < cells; x += 1) {
      const px = Math.min(
        raster.width - 1,
        Math.floor(((x + 0.5) / cells) * raster.width),
      );
      const py = Math.min(
        raster.height - 1,
        Math.floor(((y + 0.5) / cells) * raster.height),
      );
      const offset = (py * raster.width + px) * 4;
      const index = y * cells + x;
      underbody[index] = raster.masks.underbody[offset + 3] > 24 ? 1 : 0;
      chassis[index] = raster.masks.chassis[offset + 3] > 24 ? 1 : 0;
      hardware[index] = raster.masks.hardware[offset + 3] > 24 ? 1 : 0;
      accent[index] = raster.masks.accent[offset + 3] > 24 ? 1 : 0;
      emissive[index] = raster.masks.emissive[offset + 3] > 24 ? 1 : 0;
    }
  }
  removeIslands(underbody, cells, 1);
  removeIslands(chassis, cells, 2);
  removeIslands(hardware, cells, 1);
  removeIslands(accent, cells, 1);
  removeIslands(emissive, cells, 1);
  return { underbody, chassis, hardware, accent, emissive };
}

function removeIslands(mask, size, minimum) {
  const seen = new Uint8Array(mask.length);
  for (let start = 0; start < mask.length; start += 1) {
    if (!mask[start] || seen[start]) continue;
    const component = [];
    const queue = [start];
    seen[start] = 1;
    while (queue.length > 0) {
      const index = queue.pop();
      component.push(index);
      const x = index % size;
      const y = Math.floor(index / size);
      for (const [dx, dy] of [
        [-1, 0],
        [1, 0],
        [0, -1],
        [0, 1],
      ]) {
        const nx = x + dx;
        const ny = y + dy;
        const next = ny * size + nx;
        if (
          nx >= 0 &&
          nx < size &&
          ny >= 0 &&
          ny < size &&
          mask[next] &&
          !seen[next]
        ) {
          seen[next] = 1;
          queue.push(next);
        }
      }
    }
    if (component.length < minimum)
      for (const index of component) mask[index] = 0;
  }
}

function distanceHeight(mask, size) {
  const distance = new Float32Array(mask.length);
  const boundary = [];
  for (let y = 0; y < size; y += 1) {
    for (let x = 0; x < size; x += 1) {
      const index = y * size + x;
      if (!mask[index]) continue;
      if (
        x === 0 ||
        x === size - 1 ||
        y === 0 ||
        y === size - 1 ||
        !mask[index - 1] ||
        !mask[index + 1] ||
        !mask[index - size] ||
        !mask[index + size]
      )
        boundary.push([x, y]);
    }
  }
  let maximum = 1;
  for (let y = 0; y < size; y += 1) {
    for (let x = 0; x < size; x += 1) {
      const index = y * size + x;
      if (!mask[index]) continue;
      let best = Number.POSITIVE_INFINITY;
      for (const [bx, by] of boundary) {
        const value = Math.hypot(x - bx, y - by);
        if (value < best) best = value;
      }
      distance[index] = best;
      maximum = Math.max(maximum, best);
    }
  }
  for (let index = 0; index < distance.length; index += 1)
    distance[index] = Math.min(1, distance[index] / Math.min(maximum, 6));
  return distance;
}

function heightfieldGeometry(mask, height, cells, style) {
  const positions = [];
  const normals = [];
  const uvs = [];
  const tangents = [];
  const indices = [];
  const top = new Map();
  const bottom = new Map();
  const cellHeight = (x, y) => {
    const cx = Math.max(0, Math.min(cells - 1, x));
    const cy = Math.max(0, Math.min(cells - 1, y));
    return style.base + height[cy * cells + cx] * style.dome;
  };
  const addVertex = (store, x, y, upper) => {
    const key = x + ',' + y;
    if (store.has(key)) return store.get(key);
    const scale = upper ? 1 - style.bevel : 1;
    const px = ((x / cells) - 0.5) * style.width * scale;
    const pz = ((y / cells) - 0.5) * style.width * scale;
    const py = upper
      ? (cellHeight(x - 1, y - 1) +
          cellHeight(x, y - 1) +
          cellHeight(x - 1, y) +
          cellHeight(x, y)) /
        4
      : style.bottom;
    const dhdx = upper ? cellHeight(x, y) - cellHeight(x - 1, y) : 0;
    const dhdz = upper ? cellHeight(x, y) - cellHeight(x, y - 1) : 0;
    const normal = upper
      ? new THREE.Vector3(-dhdx * cells, 1, -dhdz * cells).normalize()
      : new THREE.Vector3(0, -1, 0);
    const index = positions.length / 3;
    positions.push(px, py, pz);
    normals.push(normal.x, normal.y, normal.z);
    uvs.push(x / cells, 1 - y / cells);
    tangents.push(1, 0, 0, 1);
    store.set(key, index);
    return index;
  };
  for (let y = 0; y < cells; y += 1) {
    for (let x = 0; x < cells; x += 1) {
      if (!mask[y * cells + x]) continue;
      const a = addVertex(top, x, y, true);
      const b = addVertex(top, x + 1, y, true);
      const c = addVertex(top, x + 1, y + 1, true);
      const d = addVertex(top, x, y + 1, true);
      indices.push(a, d, b, b, d, c);
      const ba = addVertex(bottom, x, y, false);
      const bb = addVertex(bottom, x + 1, y, false);
      const bc = addVertex(bottom, x + 1, y + 1, false);
      const bd = addVertex(bottom, x, y + 1, false);
      indices.push(ba, bb, bd, bb, bc, bd);
    }
  }
  const edge = (x1, y1, x2, y2) => {
    const points = [];
    for (const [x, y, upper] of [
      [x1, y1, false],
      [x2, y2, false],
      [x2, y2, true],
      [x1, y1, true],
    ]) {
      const scale = upper ? 1 - style.bevel : 1;
      points.push(
        ((x / cells) - 0.5) * style.width * scale,
        upper
          ? (cellHeight(x - 1, y - 1) +
              cellHeight(x, y - 1) +
              cellHeight(x - 1, y) +
              cellHeight(x, y)) /
            4
          : style.bottom,
        ((y / cells) - 0.5) * style.width * scale,
      );
    }
    const va = new THREE.Vector3(points[3] - points[0], points[4] - points[1], points[5] - points[2]);
    const vb = new THREE.Vector3(points[6] - points[0], points[7] - points[1], points[8] - points[2]);
    const normal = va.cross(vb).normalize();
    const base = positions.length / 3;
    positions.push(...points);
    for (let index = 0; index < 4; index += 1)
      normals.push(normal.x, normal.y, normal.z);
    // Sample the albedo at this exact planform boundary. Mapping every side quad from
    // 0..1 stretched the entire drawing around each tiny wall and read as a black outline.
    const u1 = x1 / cells;
    const v1 = 1 - y1 / cells;
    const u2 = x2 / cells;
    const v2 = 1 - y2 / cells;
    uvs.push(u1, v1, u2, v2, u2, v2, u1, v1);
    tangents.push(1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1);
    indices.push(base, base + 1, base + 3, base + 1, base + 2, base + 3);
  };
  for (let y = 0; y < cells; y += 1) {
    for (let x = 0; x < cells; x += 1) {
      if (!mask[y * cells + x]) continue;
      if (y === 0 || !mask[(y - 1) * cells + x]) edge(x + 1, y, x, y);
      if (x === cells - 1 || !mask[y * cells + x + 1]) edge(x + 1, y + 1, x + 1, y);
      if (y === cells - 1 || !mask[(y + 1) * cells + x]) edge(x, y + 1, x + 1, y + 1);
      if (x === 0 || !mask[y * cells + x - 1]) edge(x, y, x, y + 1);
    }
  }
  return bufferGeometry(positions, normals, uvs, indices, tangents);
}

function surfaceGeometry(mask, height, cells, style) {
  const positions = [];
  const normals = [];
  const uvs = [];
  const indices = [];
  for (let y = 0; y < cells; y += 1) {
    for (let x = 0; x < cells; x += 1) {
      if (!mask[y * cells + x]) continue;
      const base = positions.length / 3;
      const value = style.base + height[y * cells + x] * style.dome;
      const x0 = ((x / cells) - 0.5) * style.width * 0.974;
      const x1 = (((x + 1) / cells) - 0.5) * style.width * 0.974;
      const z0 = ((y / cells) - 0.5) * style.width * 0.974;
      const z1 = (((y + 1) / cells) - 0.5) * style.width * 0.974;
      positions.push(x0, value, z0, x1, value, z0, x1, value, z1, x0, value, z1);
      normals.push(0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0);
      uvs.push(
        x / cells,
        1 - y / cells,
        (x + 1) / cells,
        1 - y / cells,
        (x + 1) / cells,
        1 - (y + 1) / cells,
        x / cells,
        1 - (y + 1) / cells,
      );
      indices.push(base, base + 3, base + 1, base + 1, base + 3, base + 2);
    }
  }
  return bufferGeometry(positions, normals, uvs, indices);
}

function bufferGeometry(positions, normals, uvs, indices, tangents) {
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute(
    'position',
    new THREE.Float32BufferAttribute(positions, 3),
  );
  geometry.setAttribute('normal', new THREE.Float32BufferAttribute(normals, 3));
  geometry.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
  if (tangents)
    geometry.setAttribute(
      'tangent',
      new THREE.Float32BufferAttribute(tangents, 4),
    );
  geometry.setIndex(indices);
  return geometry;
}

function locomotionHeight(kind) {
  return kind === 'low-hover' ? 0.055 : kind === 'skids' ? 0.07 : 0.16;
}

function locomotionNodes(entry) {
  const nodes = [];
  if (entry.locomotion === 'low-hover') {
    nodes.push({
      name: 'hover-plenum',
      geometry: roundedPlate(0.54, 0.04, 0.42, 0.035, 0.008),
    });
    for (const [name, x, z] of [
      ['hover-jet-front', 0.22, 0],
      ['hover-jet-left', -0.12, -0.18],
      ['hover-jet-right', -0.12, 0.18],
    ])
      nodes.push({
        name,
        geometry: cylinder(0.045, 0.055, x, 0.0275, z, 12),
      });
  } else if (entry.locomotion === 'treads') {
    for (const side of [-1, 1]) {
      nodes.push({
        name: side < 0 ? 'locomotion-left' : 'locomotion-right',
        geometry: roundedPlate(0.7, 0.14, 0.14, 0.025, 0.012, -0.03, side * 0.31),
      });
      for (const [index, x] of [-0.23, 0, 0.23].entries())
        nodes.push({
          name:
            (side < 0 ? 'wheel-left-' : 'wheel-right-') + index,
          geometry: wheel(0.07, 0.07, x, 0.07, side * 0.31),
        });
    }
  } else if (entry.locomotion === 'wheels') {
    for (const [sideName, z] of [
      ['left', -0.3],
      ['right', 0.3],
    ])
      for (const [index, x] of [-0.23, 0.23].entries())
        nodes.push({
          name: 'wheel-' + sideName + '-' + index,
          geometry: wheel(0.095, 0.075, x, 0.095, z),
        });
  } else {
    for (const [name, z] of [
      ['locomotion-left', -0.27],
      ['locomotion-right', 0.27],
    ])
      nodes.push({
        name,
        geometry: roundedPlate(0.62, 0.07, 0.075, 0.024, 0.01, -0.04, z),
      });
  }
  return nodes;
}

function roundedPlate(
  width,
  height,
  depth,
  radius,
  bevel,
  x = 0,
  z = 0,
  y = height / 2,
) {
  const shape = new THREE.Shape();
  const halfX = width / 2;
  const halfZ = depth / 2;
  shape.moveTo(-halfX + radius, -halfZ);
  shape.lineTo(halfX - radius, -halfZ);
  shape.quadraticCurveTo(halfX, -halfZ, halfX, -halfZ + radius);
  shape.lineTo(halfX, halfZ - radius);
  shape.quadraticCurveTo(halfX, halfZ, halfX - radius, halfZ);
  shape.lineTo(-halfX + radius, halfZ);
  shape.quadraticCurveTo(-halfX, halfZ, -halfX, halfZ - radius);
  shape.lineTo(-halfX, -halfZ + radius);
  shape.quadraticCurveTo(-halfX, -halfZ, -halfX + radius, -halfZ);
  const geometry = new THREE.ExtrudeGeometry(shape, {
    depth: Math.max(0.001, height - bevel * 2),
    bevelEnabled: true,
    bevelSegments: 1,
    bevelSize: bevel,
    bevelThickness: bevel,
    curveSegments: 2,
    steps: 1,
  });
  geometry.rotateX(-Math.PI / 2);
  geometry.translate(x, y - height / 2 + bevel, z);
  return geometry;
}

function cylinder(radius, height, x, y, z, segments = 14) {
  const geometry = new THREE.CylinderGeometry(radius, radius * 0.9, height, segments, 1);
  geometry.translate(x, y, z);
  return geometry;
}

function wheel(radius, width, x, y, z) {
  const geometry = new THREE.CylinderGeometry(radius, radius, width, 14, 1);
  geometry.rotateX(Math.PI / 2);
  geometry.translate(x, y, z);
  return geometry;
}

class GlbBuilder {
  constructor(entry, groups, images) {
    this.entry = entry;
    this.groups = groups;
    this.imageBytes = images;
    this.binary = [];
    this.byteLength = 0;
    this.accessors = [];
    this.bufferViews = [];
    this.meshes = [];
    this.nodes = [];
    this.triangles = 0;
    this.vertices = 0;
    this.minimum = [Number.POSITIVE_INFINITY, Number.POSITIVE_INFINITY, Number.POSITIVE_INFINITY];
    this.maximum = [Number.NEGATIVE_INFINITY, Number.NEGATIVE_INFINITY, Number.NEGATIVE_INFINITY];
  }

  node(name, extras) {
    const index = this.nodes.length;
    this.nodes.push({
      name,
      ...(extras ? { extras } : {}),
      children: [],
    });
    return index;
  }

  child(parent, child) {
    this.nodes[parent].children.push(child);
  }

  meshNode(name, geometry, material, extras) {
    const mesh = this.addGeometry(name, geometry, material, extras);
    const node = this.node(name, extras);
    this.nodes[node].mesh = mesh;
    return node;
  }

  addGeometry(name, geometry, material, extras) {
    const source = geometry;
    const position = source.getAttribute('position');
    const normal = source.getAttribute('normal');
    const uv = source.getAttribute('uv');
    const tangent = source.getAttribute('tangent');
    const index = source.index;
    if (!position || !normal || !uv)
      throw new Error(name + ' is missing position, normal or uv');
    const attributes = {
      POSITION: this.floatAccessor(position.array, 3, true),
      NORMAL: this.floatAccessor(normal.array, 3, false),
      TEXCOORD_0: this.floatAccessor(uv.array, 2, false),
    };
    if (tangent)
      attributes.TANGENT = this.floatAccessor(tangent.array, 4, false);
    const indexArray = index
      ? new Uint16Array(index.array)
      : Uint16Array.from({ length: position.count }, (_, value) => value);
    if (position.count > 65535)
      throw new Error(name + ' exceeds Uint16 vertex budget');
    const indexAccessor = this.indexAccessor(indexArray);
    this.vertices += position.count;
    this.triangles += indexArray.length / 3;
    for (let value = 0; value < position.count; value += 1) {
      for (let axis = 0; axis < 3; axis += 1) {
        const component = position.getComponent(value, axis);
        this.minimum[axis] = Math.min(this.minimum[axis], component);
        this.maximum[axis] = Math.max(this.maximum[axis], component);
      }
    }
    const primitive = {
      attributes,
      indices: indexAccessor,
      material,
      mode: 4,
      ...(extras ? { extras } : {}),
    };
    const mesh = this.meshes.length;
    this.meshes.push({ name, primitives: [primitive] });
    return mesh;
  }

  floatAccessor(array, itemSize, bounds) {
    const values = new Float32Array(array);
    const view = this.append(
      Buffer.from(values.buffer, values.byteOffset, values.byteLength),
      34962,
    );
    const accessor = {
      bufferView: view,
      componentType: 5126,
      count: values.length / itemSize,
      type: itemSize === 2 ? 'VEC2' : itemSize === 3 ? 'VEC3' : 'VEC4',
    };
    if (bounds) {
      const minimum = Array(itemSize).fill(Number.POSITIVE_INFINITY);
      const maximum = Array(itemSize).fill(Number.NEGATIVE_INFINITY);
      for (let offset = 0; offset < values.length; offset += itemSize)
        for (let axis = 0; axis < itemSize; axis += 1) {
          minimum[axis] = Math.min(minimum[axis], values[offset + axis]);
          maximum[axis] = Math.max(maximum[axis], values[offset + axis]);
        }
      accessor.min = minimum;
      accessor.max = maximum;
    }
    const index = this.accessors.length;
    this.accessors.push(accessor);
    return index;
  }

  indexAccessor(values) {
    const view = this.append(
      Buffer.from(values.buffer, values.byteOffset, values.byteLength),
      34963,
    );
    const index = this.accessors.length;
    this.accessors.push({
      bufferView: view,
      componentType: 5123,
      count: values.length,
      type: 'SCALAR',
      min: [values.length === 0 ? 0 : Math.min(...values)],
      max: [values.length === 0 ? 0 : Math.max(...values)],
    });
    return index;
  }

  append(bytes, target) {
    const padding = (4 - (this.byteLength % 4)) % 4;
    if (padding > 0) {
      this.binary.push(Buffer.alloc(padding));
      this.byteLength += padding;
    }
    const offset = this.byteLength;
    const buffer = Buffer.from(bytes);
    this.binary.push(buffer);
    this.byteLength += buffer.length;
    const view = this.bufferViews.length;
    this.bufferViews.push({
      buffer: 0,
      byteOffset: offset,
      byteLength: buffer.length,
      ...(target ? { target } : {}),
    });
    return view;
  }

  finish(root) {
    const imageViews = this.imageBytes.map((bytes) => this.append(bytes));
    const document = {
      asset: {
        version: '2.0',
        generator: 'Nilbots Arc Relay orthographic layered relief 3',
        extras: {
          provider: 'none',
          source: 'named-group-orthographic-vector',
          sourceProjectionDegrees: 0,
          archivedMasterProjectionDegrees: 20,
          requiredGroups: this.groups,
        },
      },
      scene: 0,
      scenes: [{ name: 'Arc Relay ' + this.entry.label, nodes: [root] }],
      nodes: this.nodes,
      meshes: this.meshes,
      materials: materials(),
      samplers: [
        {
          magFilter: 9729,
          minFilter: 9987,
          wrapS: 33071,
          wrapT: 33071,
        },
      ],
      images: imageViews.map((bufferView, index) => ({
        name: ['Arc Relay albedo', 'Arc Relay normal', 'Arc Relay emissive'][index],
        mimeType: 'image/png',
        bufferView,
      })),
      textures: imageViews.map((_, index) => ({
        sampler: 0,
        source: index,
      })),
      accessors: this.accessors,
      bufferViews: this.bufferViews,
      buffers: [{ byteLength: this.byteLength }],
    };
    const json = Buffer.from(JSON.stringify(document));
    const jsonPadding = (4 - (json.length % 4)) % 4;
    const jsonChunk = Buffer.concat([json, Buffer.alloc(jsonPadding, 0x20)]);
    const bin = Buffer.concat(this.binary);
    const binPadding = (4 - (bin.length % 4)) % 4;
    const binChunk = Buffer.concat([bin, Buffer.alloc(binPadding)]);
    const output = Buffer.alloc(12 + 8 + jsonChunk.length + 8 + binChunk.length);
    output.write('glTF', 0, 'ascii');
    output.writeUInt32LE(2, 4);
    output.writeUInt32LE(output.length, 8);
    output.writeUInt32LE(jsonChunk.length, 12);
    output.writeUInt32LE(0x4e4f534a, 16);
    jsonChunk.copy(output, 20);
    const binHeader = 20 + jsonChunk.length;
    output.writeUInt32LE(binChunk.length, binHeader);
    output.writeUInt32LE(0x004e4942, binHeader + 4);
    binChunk.copy(output, binHeader + 8);
    return {
      bytes: output,
      triangles: this.triangles,
      vertices: this.vertices,
      bounds: {
        min: this.minimum.map(round6),
        max: this.maximum.map(round6),
        planformSpan: round6(
          Math.max(
            this.maximum[0] - this.minimum[0],
            this.maximum[2] - this.minimum[2],
          ),
        ),
        floorY: round6(this.minimum[1]),
      },
    };
  }
}

function materials() {
  return [
    {
      name: 'Arc Underbody',
      pbrMetallicRoughness: {
        baseColorTexture: { index: 0 },
        baseColorFactor: [0.52, 0.56, 0.6, 1],
        metallicFactor: 0.58,
        roughnessFactor: 0.58,
      },
      emissiveTexture: { index: 0 },
      emissiveFactor: [0.2, 0.2, 0.2],
    },
    {
      name: 'Arc Layered Chassis',
      pbrMetallicRoughness: {
        baseColorTexture: { index: 0 },
        metallicFactor: 0.34,
        roughnessFactor: 0.5,
      },
      normalTexture: { index: 1, scale: 0.38 },
      // Preserve the source drawing's value floor in the intentionally dark arena. The
      // separate emissive mesh still owns actual lamps; this low albedo copy only keeps
      // dark armor from collapsing into an unreadable black silhouette.
      emissiveTexture: { index: 0 },
      emissiveFactor: [0.28, 0.28, 0.28],
    },
    {
      name: 'Arc Layered Hardware',
      pbrMetallicRoughness: {
        baseColorTexture: { index: 0 },
        metallicFactor: 0.44,
        roughnessFactor: 0.44,
      },
      normalTexture: { index: 1, scale: 0.42 },
      emissiveTexture: { index: 0 },
      emissiveFactor: [0.32, 0.32, 0.32],
    },
    {
      name: 'Arc Team Accent',
      extras: { nilbotsRole: 'team-accent' },
      pbrMetallicRoughness: {
        baseColorFactor: [1, 1, 1, 1],
        metallicFactor: 0.16,
        roughnessFactor: 0.3,
      },
      emissiveFactor: [1, 1, 1],
    },
    {
      name: 'Arc Emissive',
      pbrMetallicRoughness: {
        baseColorTexture: { index: 2 },
        metallicFactor: 0.08,
        roughnessFactor: 0.24,
      },
      emissiveTexture: { index: 2 },
      emissiveFactor: [1, 1, 1],
    },
  ];
}

function stableJson(value) {
  return JSON.stringify(value, null, 2) + '\n';
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

function round6(value) {
  return Math.round(value * 1_000_000) / 1_000_000;
}

await main();
