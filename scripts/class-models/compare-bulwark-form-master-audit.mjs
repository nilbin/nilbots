#!/usr/bin/env node

/**
 * Deterministic raster and provenance measurements for the Bulwark form audit.
 *
 * Blender owns source-axis normalization, topology inspection, and fixed-view
 * rendering. This script compares those renders with the exact SVG and the
 * approved corrected concept-derived inputs without rotation, reflection, or
 * non-uniform warping, then writes overlays and contact sheets.
 */

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import process from 'node:process';

const SIZE = 1024;
const TARGET_SPAN = 820;
const MASK_THRESHOLD = 16;

const MASTERS = [
  {
    assetId: 'aegis-tortoise-shell',
    taskId: '019fb22d-c313-76f8-8eca-91ccbb46afd0',
    label: 'Aegis Tortoise Shell',
    sourceUp: '+y',
    sourceFacing: '-x',
    runtimeScale: 1.2,
    directional: true,
    concept:
      'art/class-models/concept-targets/aegis-tortoise-shell-3d-concept-v1/approval-sheet.png',
    cleanRoot:
      'art/class-models/provider-inputs/meshy/aegis-tortoise-shell/clean-v1',
    svg:
      'web/src/assets/class-looks/aegis-tortoise-shell/sprite.svg',
    semanticExpectation:
      'three team inlays: two shield-edge bars and one west-centre panel; central core is fixed class energy',
    verdict:
      'accepted-high-detail-shell-form-reference-requires-authored-retopo-and-rig',
    segmentationViability:
      'not-directly-segmentable-single-watertight-manifold',
    retopoRiggingViability:
      'viable-visual-and-material-reference-for-authored-rebuild',
    corrections: [
      'return the final blockout to the exact East-facing SVG outer planform: restore the north/south rear lobes, trim provider-only west pod and shield-shoulder protrusions, and preserve the hard +/-45-degree guard edges, open other three quadrants, and no muzzle',
      'retain the approved low planted side class; do not globally scale or flatten the raw shell to repair local contour drift',
      'author a new named root, directional guard hierarchy, and planted support hierarchy because exact welding proves the raw mesh has no separable rigid parts',
      'retopologize and rebake the approved top armor and side mechanics with tangents instead of rigging the 788k-triangle provider manifold',
      'isolate only the two shield bars and west-centre panel as neutral team-accent; preserve the central reactor as class energy',
    ],
  },
  {
    assetId: 'aegis-tortoise-turret',
    taskId: '019fb232-6e09-787f-85cc-dbaea29fb9b6',
    label: 'Aegis Tortoise whole Turret',
    sourceUp: '+y',
    sourceFacing: '+x',
    runtimeScale: 1.08,
    directional: false,
    concept:
      'art/class-models/concept-targets/aegis-tortoise-turret-3d-concept-v1/approval-sheet.png',
    cleanRoot:
      'art/class-models/provider-inputs/meshy/aegis-tortoise-turret/clean-v1',
    svg:
      'web/src/assets/class-looks/aegis-tortoise-turret/sprite.svg',
    semanticExpectation:
      'four equal top cardinal team inlays; central core is fixed class energy; no cyan under-arm panels',
    verdict:
      'accepted-high-detail-radial-top-reference-requires-authored-symmetric-rebuild',
    segmentationViability:
      'not-directly-segmentable-single-watertight-manifold',
    retopoRiggingViability:
      'viable-visual-and-material-reference-for-fourfold-authored-rebuild',
    corrections: [
      'retain one singular hub, four equal cardinal arms, four diagonal shoulder webs, and no privileged front; expand all four diagonal shoulder-web footprints and trim all four terminal pod tips equally to the exact SVG contour under strict fourfold symmetry',
      'rebuild one canonical arm/support hierarchy and rotate authored copies by exact 90-degree increments around one hub; mount the resulting whole turret once at runtime',
      'reconstruct the approved compact planted locks and underbody instead of rigging the fused provider lower structure',
      'remove the four unintended cyan under-arm faces visible in side/oblique review; isolate only the four top bars as neutral team-accent and keep the reactor as class energy',
      'retopologize and rebake with tangents; author anchor/deploy and fire response only on named rebuilt parts',
    ],
  },
];

function parseArgs(argv) {
  const result = {
    repoRoot: null,
    artifactsRoot: null,
    phase: null,
  };
  for (let index = 0; index < argv.length; index += 1) {
    const value = argv[index];
    if (value === '--repo-root') result.repoRoot = argv[++index];
    else if (value === '--artifacts-root')
      result.artifactsRoot = argv[++index];
    else if (value === '--phase') result.phase = argv[++index];
    else throw new Error(`Unknown argument: ${value}`);
  }
  if (!result.repoRoot || !result.artifactsRoot)
    throw new Error('--repo-root and --artifacts-root are required.');
  if (!['probe', 'final'].includes(result.phase))
    throw new Error('--phase must be probe or final.');
  result.repoRoot = path.resolve(result.repoRoot);
  result.artifactsRoot = path.resolve(result.artifactsRoot);
  return result;
}

function digest(input) {
  return createHash('sha256').update(input).digest('hex');
}

function boundingBox(mask, width = SIZE, height = SIZE) {
  let left = width;
  let right = -1;
  let top = height;
  let bottom = -1;
  for (let y = 0; y < height; y += 1) {
    const offset = y * width;
    for (let x = 0; x < width; x += 1) {
      if (!mask[offset + x]) continue;
      if (x < left) left = x;
      if (x > right) right = x;
      if (y < top) top = y;
      if (y > bottom) bottom = y;
    }
  }
  if (right < left || bottom < top) throw new Error('Mask is empty.');
  return {
    left,
    top,
    right,
    bottom,
    width: right - left + 1,
    height: bottom - top + 1,
  };
}

function normalizeMask(mask, mode) {
  const bounds = boundingBox(mask);
  const sourceSpan =
    mode === 'width'
      ? bounds.width
      : Math.max(bounds.width, bounds.height);
  const scale = TARGET_SPAN / sourceSpan;
  const targetWidth = Math.max(1, Math.round(bounds.width * scale));
  const targetHeight = Math.max(1, Math.round(bounds.height * scale));
  const left = Math.floor((SIZE - targetWidth) / 2);
  const top = Math.floor((SIZE - targetHeight) / 2);
  const result = new Uint8Array(SIZE * SIZE);
  for (let y = 0; y < targetHeight; y += 1) {
    const sourceY = Math.min(
      bounds.height - 1,
      Math.floor((y + 0.5) / scale),
    );
    const sourceOffset = (bounds.top + sourceY) * SIZE + bounds.left;
    const targetOffset = (top + y) * SIZE + left;
    for (let x = 0; x < targetWidth; x += 1) {
      const sourceX = Math.min(
        bounds.width - 1,
        Math.floor((x + 0.5) / scale),
      );
      result[targetOffset + x] = mask[sourceOffset + sourceX];
    }
  }
  return {
    mask: result,
    sourceBounds: bounds,
    normalizedBounds: boundingBox(result),
    scale,
  };
}

function overlap(reference, candidate, dx = 0, dy = 0) {
  let intersection = 0;
  let union = 0;
  let referenceCount = 0;
  let candidateCount = 0;
  for (let y = 0; y < SIZE; y += 1) {
    for (let x = 0; x < SIZE; x += 1) {
      const referenceValue = reference[y * SIZE + x] !== 0;
      const sourceX = x - dx;
      const sourceY = y - dy;
      const candidateValue =
        sourceX >= 0 &&
        sourceX < SIZE &&
        sourceY >= 0 &&
        sourceY < SIZE &&
        candidate[sourceY * SIZE + sourceX] !== 0;
      if (referenceValue) referenceCount += 1;
      if (candidateValue) candidateCount += 1;
      if (referenceValue && candidateValue) intersection += 1;
      if (referenceValue || candidateValue) union += 1;
    }
  }
  return {
    iou: union === 0 ? 0 : intersection / union,
    intersection,
    union,
    referenceCount,
    candidateCount,
    coverage:
      referenceCount === 0 ? 0 : intersection / referenceCount,
    precision:
      candidateCount === 0 ? 0 : intersection / candidateCount,
  };
}

function bestTranslation(reference, candidate, radius = 8) {
  let best = null;
  for (let dy = -radius; dy <= radius; dy += 1) {
    for (let dx = -radius; dx <= radius; dx += 1) {
      const metrics = overlap(reference, candidate, dx, dy);
      if (
        best === null ||
        metrics.iou > best.iou ||
        (metrics.iou === best.iou &&
          Math.abs(dx) + Math.abs(dy) <
            Math.abs(best.dx) + Math.abs(best.dy))
      ) {
        best = { ...metrics, dx, dy };
      }
    }
  }
  return best;
}

function shifted(mask, dx, dy) {
  const result = new Uint8Array(mask.length);
  for (let y = 0; y < SIZE; y += 1) {
    const sourceY = y - dy;
    if (sourceY < 0 || sourceY >= SIZE) continue;
    for (let x = 0; x < SIZE; x += 1) {
      const sourceX = x - dx;
      if (sourceX < 0 || sourceX >= SIZE) continue;
      result[y * SIZE + x] = mask[sourceY * SIZE + sourceX];
    }
  }
  return result;
}

function rotate90(mask) {
  const result = new Uint8Array(mask.length);
  for (let y = 0; y < SIZE; y += 1) {
    for (let x = 0; x < SIZE; x += 1) {
      result[x * SIZE + (SIZE - 1 - y)] = mask[y * SIZE + x];
    }
  }
  return result;
}

async function alphaMask(sharp, input) {
  const { data, info } = await sharp(input)
    .resize(SIZE, SIZE, { fit: 'fill' })
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  if (info.width !== SIZE || info.height !== SIZE || info.channels !== 4)
    throw new Error(`Unexpected raster shape for ${input}`);
  const result = new Uint8Array(SIZE * SIZE);
  for (let index = 0; index < result.length; index += 1)
    result[index] =
      data[index * 4 + 3] >= MASK_THRESHOLD ? 1 : 0;
  return result;
}

async function flatBackgroundMask(sharp, input) {
  const { data, info } = await sharp(input)
    .resize(SIZE, SIZE, { fit: 'fill' })
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const result = new Uint8Array(SIZE * SIZE);
  for (let index = 0; index < result.length; index += 1) {
    const offset = index * info.channels;
    const delta = Math.max(
      Math.abs(data[offset] - 184),
      Math.abs(data[offset + 1] - 184),
      Math.abs(data[offset + 2] - 184),
    );
    result[index] =
      data[offset + 3] >= MASK_THRESHOLD && delta >= 7 ? 1 : 0;
  }
  return result;
}

async function writeOverlay(
  sharp,
  output,
  reference,
  candidate,
) {
  const pixels = Buffer.alloc(SIZE * SIZE * 4);
  for (let index = 0; index < reference.length; index += 1) {
    const ref = Boolean(reference[index]);
    const model = Boolean(candidate[index]);
    const offset = index * 4;
    if (ref && model) {
      pixels[offset] = 245;
      pixels[offset + 1] = 245;
      pixels[offset + 2] = 245;
    } else if (model) {
      pixels[offset] = 239;
      pixels[offset + 1] = 68;
      pixels[offset + 2] = 68;
    } else if (ref) {
      pixels[offset] = 34;
      pixels[offset + 1] = 211;
      pixels[offset + 2] = 238;
    } else {
      pixels[offset] = 13;
      pixels[offset + 1] = 18;
      pixels[offset + 2] = 24;
    }
    pixels[offset + 3] = 255;
  }
  await sharp(pixels, {
    raw: { width: SIZE, height: SIZE, channels: 4 },
  })
    .png()
    .toFile(output);
}

function hueSaturationValue(red, green, blue) {
  const r = red / 255;
  const g = green / 255;
  const b = blue / 255;
  const maximum = Math.max(r, g, b);
  const minimum = Math.min(r, g, b);
  const delta = maximum - minimum;
  let hue = 0;
  if (delta !== 0) {
    if (maximum === r) hue = 60 * (((g - b) / delta) % 6);
    else if (maximum === g) hue = 60 * ((b - r) / delta + 2);
    else hue = 60 * ((r - g) / delta + 4);
  }
  if (hue < 0) hue += 360;
  return {
    hue,
    saturation: maximum === 0 ? 0 : delta / maximum,
    value: maximum,
  };
}

async function textureColorStats(sharp, input) {
  const { data, info } = await sharp(input)
    .resize(512, 512, { fit: 'fill' })
    .removeAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  let saturation = 0;
  let chromatic = 0;
  let cyan = 0;
  const pixels = info.width * info.height;
  for (let index = 0; index < pixels; index += 1) {
    const offset = index * info.channels;
    const hsv = hueSaturationValue(
      data[offset],
      data[offset + 1],
      data[offset + 2],
    );
    saturation += hsv.saturation;
    const strong = hsv.saturation >= 0.30 && hsv.value >= 0.20;
    if (!strong) continue;
    chromatic += 1;
    if (hsv.hue >= 165 && hsv.hue <= 215) cyan += 1;
  }
  return {
    sampledPixels: pixels,
    meanSaturation: saturation / pixels,
    stronglyChromaticFraction: chromatic / pixels,
    strongCyanFraction: cyan / pixels,
  };
}

function escapeXml(value) {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;');
}

function labelSvg(width, height, label, detail = '') {
  return Buffer.from(
    `<svg width="${width}" height="${height}" xmlns="http://www.w3.org/2000/svg">` +
      `<rect width="100%" height="100%" fill="#0d1218"/>` +
      `<text x="20" y="31" fill="#f5f5f5" font-size="22" font-family="Arial, sans-serif" font-weight="700">${escapeXml(label)}</text>` +
      (detail
        ? `<text x="20" y="56" fill="#9fb0c2" font-size="15" font-family="Arial, sans-serif">${escapeXml(detail)}</text>`
        : '') +
      `</svg>`,
  );
}

async function reviewPanel(
  sharp,
  input,
  label,
  detail = '',
  {
    width = 512,
    height = 512,
    imageHeight = 440,
    background = '#5b5b5b',
  } = {},
) {
  const source = await sharp(input)
    .resize(width, imageHeight, {
      fit: 'contain',
      background,
    })
    .png()
    .toBuffer();
  return sharp({
    create: {
      width,
      height,
      channels: 4,
      background: '#0d1218',
    },
  })
    .composite([
      { input: source, left: 0, top: 0 },
      {
        input: labelSvg(width, height - imageHeight, label, detail),
        left: 0,
        top: imageHeight,
      },
    ])
    .png()
    .toBuffer();
}

async function writeContactSheet(sharp, output, panels, columns = 4) {
  const width = 512 * columns;
  const rows = Math.ceil(panels.length / columns);
  const composites = panels.map((input, index) => ({
    input,
    left: (index % columns) * 512,
    top: Math.floor(index / columns) * 512,
  }));
  await sharp({
    create: {
      width,
      height: rows * 512,
      channels: 4,
      background: '#0d1218',
    },
  })
    .composite(composites)
    .png()
    .toFile(output);
}

function auditDirectory(args, master) {
  return path.join(
    args.repoRoot,
    'art/class-models/provider-runs/meshy',
    master.assetId,
    master.taskId,
    'audit',
  );
}

async function probe(args, sharp) {
  for (const master of MASTERS) {
    const directory = auditDirectory(args, master);
    const authorityPath = path.join(args.repoRoot, master.svg);
    const reference = normalizeMask(
      await alphaMask(sharp, await readFile(authorityPath)),
      'longest',
    );
    const candidates = {};
    for (const up of ['+z', '+y']) {
      for (const facing of ['+x', '-x']) {
        const suffix =
          `up-${up[1]}-facing-` +
          `${facing === '+x' ? 'plus' : 'minus'}-x-mask.png`;
        const candidate = normalizeMask(
          await alphaMask(
            sharp,
            path.join(directory, `probe-${suffix}`),
          ),
          'longest',
        );
        const metrics = bestTranslation(
          reference.mask,
          candidate.mask,
        );
        candidates[`up-${up}-facing-${facing}`] = {
          iou: metrics.iou,
          coverage: metrics.coverage,
          precision: metrics.precision,
          translationPixels: [metrics.dx, metrics.dy],
          sourceBounds: candidate.sourceBounds,
        };
      }
    }
    const ranked = Object.entries(candidates).sort(
      (left, right) => right[1].iou - left[1].iou,
    );
    const expectedKey =
      `up-${master.sourceUp}-facing-${master.sourceFacing}`;
    const report = {
      schemaVersion: 1,
      assetId: master.assetId,
      method:
        'two observed Meshy source-up conventions, both longitudinal facings, longest-span normalization, uniform scale and translation only',
      authority: {
        path: master.svg,
        sha256: digest(await readFile(authorityPath)),
        bounds: reference.sourceBounds,
      },
      candidates,
      bestCandidate: ranked[0][0],
      inspectedCandidate: expectedKey,
      inspectedCandidateIsBest:
        ranked[0][0] === expectedKey ||
        (!master.directional &&
          ranked[0][0].startsWith(`up-${master.sourceUp}`)),
      note: master.directional
        ? 'Shell facing is identity-bearing: the physical guard must end on target +X.'
        : 'Whole Turret is fourfold-radial and has no privileged facing; +X is a canonical storage choice.',
    };
    await writeFile(
      path.join(directory, 'orientation-audit.json'),
      `${JSON.stringify(report, null, 2)}\n`,
    );
    console.log(
      `${master.assetId}: best ${ranked[0][0]} ` +
        `${(ranked[0][1].iou * 100).toFixed(2)}%, ` +
        `inspected ${expectedKey} ` +
        `${(candidates[expectedKey].iou * 100).toFixed(2)}%`,
    );
  }
}

async function verifyDurableArtifacts(args, master, blenderReport) {
  const recordPath = path.join(
    args.repoRoot,
    'art/class-models/provider-runs/meshy',
    master.assetId,
    master.taskId,
    'request-record.json',
  );
  const record = JSON.parse(await readFile(recordPath, 'utf8'));
  const sourceRoot = path.join(
    args.artifactsRoot,
    master.assetId,
    master.taskId,
  );
  const observed = [];
  for (const artifact of record.artifacts) {
    if (
      artifact.file !== 'raw-model.glb' &&
      !artifact.file.startsWith('texture-')
    )
      continue;
    const input = await readFile(path.join(sourceRoot, artifact.file));
    const entry = {
      file: artifact.file,
      bytes: input.length,
      sha256: digest(input),
      recordBytes: artifact.bytes,
      recordSha256: artifact.sha256,
      matches:
        input.length === artifact.bytes &&
        digest(input) === artifact.sha256,
    };
    if (!entry.matches)
      throw new Error(
        `Durable artifact does not match request record: ${master.assetId}/${artifact.file}`,
      );
    observed.push(entry);
  }
  if (
    blenderReport.source.sha256 !==
    observed.find((item) => item.file === 'raw-model.glb').sha256
  )
    throw new Error(`Blender source hash drift: ${master.assetId}`);
  return {
    requestRecord: path.relative(args.repoRoot, recordPath),
    durableArtifactSetMatchesRecord: true,
    checkedArtifacts: observed,
    providerPreviewUsedForVerdict: false,
    paidCallsMade: 0,
  };
}

async function finalAudit(args, sharp) {
  const centralDirectory = path.join(
    args.repoRoot,
    'art/class-models/provider-runs/meshy/bulwark-form-master-audit',
  );
  await mkdir(centralDirectory, { recursive: true });
  const summaries = [];
  const globalPanels = [];

  for (const master of MASTERS) {
    const directory = auditDirectory(args, master);
    const blenderReport = JSON.parse(
      await readFile(
        path.join(directory, 'render-blender-report.json'),
        'utf8',
      ),
    );
    const authorityPath = path.join(args.repoRoot, master.svg);
    const cleanTopPath = path.join(
      args.repoRoot,
      master.cleanRoot,
      '01-top.png',
    );
    const cleanSidePath = path.join(
      args.repoRoot,
      master.cleanRoot,
      '02-side.png',
    );
    const conceptPath = path.join(args.repoRoot, master.concept);
    const modelTopPath = path.join(directory, 'top-mask.png');
    const modelSidePath = path.join(directory, 'side-mask.png');

    const authorityTop = normalizeMask(
      await alphaMask(sharp, await readFile(authorityPath)),
      'longest',
    );
    const cleanTop = normalizeMask(
      await flatBackgroundMask(sharp, cleanTopPath),
      'longest',
    );
    const modelTop = normalizeMask(
      await alphaMask(sharp, modelTopPath),
      'longest',
    );
    const cleanSide = normalizeMask(
      await flatBackgroundMask(sharp, cleanSidePath),
      'width',
    );
    const modelSide = normalizeMask(
      await alphaMask(sharp, modelSidePath),
      'width',
    );

    const svgMetrics = bestTranslation(
      authorityTop.mask,
      modelTop.mask,
    );
    const cleanTopMetrics = bestTranslation(
      cleanTop.mask,
      modelTop.mask,
    );
    const sideMetrics = bestTranslation(
      cleanSide.mask,
      modelSide.mask,
    );
    const alignedSvgTop = shifted(
      modelTop.mask,
      svgMetrics.dx,
      svgMetrics.dy,
    );
    const alignedCleanTop = shifted(
      modelTop.mask,
      cleanTopMetrics.dx,
      cleanTopMetrics.dy,
    );
    const alignedSide = shifted(
      modelSide.mask,
      sideMetrics.dx,
      sideMetrics.dy,
    );
    await writeOverlay(
      sharp,
      path.join(directory, 'top-overlay-svg.png'),
      authorityTop.mask,
      alignedSvgTop,
    );
    await writeOverlay(
      sharp,
      path.join(directory, 'top-overlay-clean.png'),
      cleanTop.mask,
      alignedCleanTop,
    );
    await writeOverlay(
      sharp,
      path.join(directory, 'side-overlay-clean.png'),
      cleanSide.mask,
      alignedSide,
    );

    const cleanSideRatio =
      cleanSide.sourceBounds.height / cleanSide.sourceBounds.width;
    const modelSideRatio =
      modelSide.sourceBounds.height / modelSide.sourceBounds.width;
    const sideHeightDrift =
      modelSideRatio / cleanSideRatio - 1;
    const symmetry90 = master.directional
      ? null
      : overlap(modelTop.mask, rotate90(modelTop.mask)).iou;
    const sourceRoot = path.join(
      args.artifactsRoot,
      master.assetId,
      master.taskId,
    );
    const baseColorStats = await textureColorStats(
      sharp,
      path.join(sourceRoot, 'texture-0-base-color.png'),
    );
    const emissionStats = await textureColorStats(
      sharp,
      path.join(sourceRoot, 'texture-0-emission.png'),
    );
    const provenance = await verifyDurableArtifacts(
      args,
      master,
      blenderReport,
    );
    const report = {
      schemaVersion: 1,
      assetId: master.assetId,
      label: master.label,
      taskId: master.taskId,
      source: blenderReport.source,
      provenance,
      normalization: blenderReport.normalization,
      geometry: blenderReport.geometry,
      topology: blenderReport.topology,
      silhouette: {
        method:
          'centered bounds, uniform scale and translation only; no rotation, reflection, deformation, or non-uniform warp',
        exactSvgTop: {
          authority: master.svg,
          authoritySha256: digest(await readFile(authorityPath)),
          iou: svgMetrics.iou,
          coverage: svgMetrics.coverage,
          precision: svgMetrics.precision,
          translationPixels: [svgMetrics.dx, svgMetrics.dy],
        },
        correctedConceptTop: {
          authority: path.relative(args.repoRoot, cleanTopPath),
          authoritySha256: digest(await readFile(cleanTopPath)),
          iou: cleanTopMetrics.iou,
          coverage: cleanTopMetrics.coverage,
          precision: cleanTopMetrics.precision,
          translationPixels: [
            cleanTopMetrics.dx,
            cleanTopMetrics.dy,
          ],
        },
        approvedSide: {
          authority: path.relative(args.repoRoot, cleanSidePath),
          authoritySha256: digest(await readFile(cleanSidePath)),
          iou: sideMetrics.iou,
          cleanHeightToLength: cleanSideRatio,
          modelHeightToLength: modelSideRatio,
          heightDrift: sideHeightDrift,
          translationPixels: [sideMetrics.dx, sideMetrics.dy],
        },
        fourfoldRotationalIou: symmetry90,
      },
      materials: {
        ...blenderReport.materials,
        sourceTextureColorStats: {
          baseColor: baseColorStats,
          emission: emissionStats,
        },
        teamNeutral: false,
        directlyTintable: false,
        semanticExpectation: master.semanticExpectation,
        verdict:
          'one fused material with baked cyan and no team-accent semantic role; offline semantic material authoring is mandatory',
      },
      rigging: {
        segmentationViability: master.segmentationViability,
        retopoRiggingViability: master.retopoRiggingViability,
        rawSkins: blenderReport.geometry.skins,
        rawAnimations: blenderReport.geometry.animations,
        rawMorphTargets: blenderReport.geometry.morphTargets,
      },
      runtime: {
        installed: false,
        currentSpriteScale: master.runtimeScale,
        authoredCandidateProduced: false,
      },
      correctionPrescription: master.corrections,
      verdict: master.verdict,
    };
    await writeFile(
      path.join(directory, 'audit-report.json'),
      `${JSON.stringify(report, null, 2)}\n`,
    );

    const svgRaster = await sharp(await readFile(authorityPath))
      .resize(1024, 1024, { fit: 'fill' })
      .png()
      .toBuffer();
    const panels = await Promise.all([
      reviewPanel(
        sharp,
        conceptPath,
        'Owner-approved 3D concept',
        'material, depth, and support authority',
      ),
      reviewPanel(
        sharp,
        svgRaster,
        'Exact runtime SVG top',
        'outer planform and form semantics',
      ),
      reviewPanel(
        sharp,
        cleanTopPath,
        'Corrected concept-derived top',
        'provider input; not flat vector replacement',
      ),
      reviewPanel(
        sharp,
        path.join(directory, 'normalized-top.png'),
        'Normalized raw GLB top',
        `${(cleanTopMetrics.iou * 100).toFixed(2)}% input · ${(svgMetrics.iou * 100).toFixed(2)}% SVG`,
      ),
      reviewPanel(
        sharp,
        path.join(directory, 'top-overlay-svg.png'),
        'Exact SVG overlay',
        'white overlap · red model · cyan SVG',
      ),
      reviewPanel(
        sharp,
        path.join(directory, 'top-overlay-clean.png'),
        'Corrected-input overlay',
        'uniform scale and translation only',
      ),
      reviewPanel(
        sharp,
        cleanSidePath,
        'Approved clean side input',
        'side/profile authority',
      ),
      reviewPanel(
        sharp,
        path.join(directory, 'normalized-side.png'),
        'Normalized raw GLB side',
        `${(sideHeightDrift * 100).toFixed(1)}% height drift`,
      ),
      reviewPanel(
        sharp,
        path.join(directory, 'side-overlay-clean.png'),
        'Approved-side overlay',
        `${(sideMetrics.iou * 100).toFixed(2)}% approximate IoU`,
      ),
      reviewPanel(
        sharp,
        path.join(directory, 'normalized-iso.png'),
        'Normalized raw GLB oblique',
        'strict Blender evidence',
      ),
      reviewPanel(
        sharp,
        path.join(directory, 'gameplay-camera.png'),
        '58° gameplay camera',
        `normalized master · sprite metadata scale ${master.runtimeScale.toFixed(2)}`,
        { background: '#0d1218' },
      ),
      reviewPanel(
        sharp,
        path.join(directory, 'components-iso.png'),
        'Exact-weld connectivity',
        `${blenderReport.topology.weldedComponents} watertight component`,
      ),
    ]);
    await writeContactSheet(
      sharp,
      path.join(directory, 'contact-sheet.png'),
      panels,
    );

    summaries.push({
      assetId: master.assetId,
      taskId: master.taskId,
      sourceSha256: blenderReport.source.sha256,
      exactSvgTopIou: svgMetrics.iou,
      correctedConceptTopIou: cleanTopMetrics.iou,
      approvedSideIouApprox: sideMetrics.iou,
      sideHeightDrift,
      fourfoldRotationalIou: symmetry90,
      weldedComponents: blenderReport.topology.weldedComponents,
      triangles: blenderReport.geometry.triangles,
      materials: blenderReport.materials.count,
      hasTeamAccentMaterial:
        blenderReport.materials.hasTeamAccentMaterial,
      verdict: master.verdict,
      segmentationViability: master.segmentationViability,
      retopoRiggingViability: master.retopoRiggingViability,
    });
    globalPanels.push(
      await reviewPanel(
        sharp,
        path.join(directory, 'gameplay-camera.png'),
        master.label,
        `${(svgMetrics.iou * 100).toFixed(1)}% SVG top · ${(sideHeightDrift * 100).toFixed(1)}% side drift`,
        { background: '#0d1218' },
      ),
      await reviewPanel(
        sharp,
        path.join(directory, 'normalized-iso.png'),
        `${master.label} oblique`,
        master.retopoRiggingViability.replaceAll('-', ' '),
      ),
    );
  }

  await writeContactSheet(
    sharp,
    path.join(centralDirectory, 'bulwark-form-comparison.png'),
    globalPanels,
    2,
  );
  const comparison = {
    schemaVersion: 1,
    blender: {
      version: '5.2.0',
      binary:
        'var/toolchains/Blender-5.2.0.app/Contents/MacOS/Blender',
    },
    sourcePolicy:
      'durable raw provider masters preserved; provider previews are provenance only and never verdict evidence',
    normalization:
      'per-master inspected source up/facing; +X target facing, +Y runtime up, one-tile authored planform; uniform scale and translation only',
    masters: summaries,
    authoredCandidatesProduced: 0,
    runtimeOrFrontendEdits: 0,
    paidCallsMade: 0,
  };
  await writeFile(
    path.join(centralDirectory, 'comparison-report.json'),
    `${JSON.stringify(comparison, null, 2)}\n`,
  );
  for (const summary of summaries) {
    console.log(
      `${summary.assetId}: SVG ${(summary.exactSvgTopIou * 100).toFixed(2)}%, ` +
        `input ${(summary.correctedConceptTopIou * 100).toFixed(2)}%, ` +
        `side drift ${(summary.sideHeightDrift * 100).toFixed(1)}%, ` +
        `${summary.weldedComponents} welded component(s)`,
    );
  }
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const requireFromWeb = createRequire(
    path.join(args.repoRoot, 'web/package.json'),
  );
  const sharp = requireFromWeb('sharp');
  if (args.phase === 'probe') await probe(args, sharp);
  else await finalAudit(args, sharp);
}

await main();
