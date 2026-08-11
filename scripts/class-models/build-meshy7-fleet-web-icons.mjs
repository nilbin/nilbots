#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web/package.json'));
const { chromium } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');
const check = process.argv.includes('--check');
const auditPath = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/meshy7-fleet-review-v3/audit.json',
);
const audit = JSON.parse(readFileSync(auditPath, 'utf8'));
const reviewDirectory = path.join(
  repository,
  'art/reviews/arc-relay-meshy7-fleet-retexture-v3',
);
const captureDirectory = path.join(reviewDirectory, 'top-down-east-icon-captures');
const evidencePath = path.join(reviewDirectory, 'top-down-east-icons.json');
const sheetPath = path.join(reviewDirectory, 'top-down-east-icons-sheet.png');
const webLooksDirectory = path.join(repository, 'web/src/assets/class-looks');
const baseUrl = option('--url', 'http://127.0.0.1:9074');
const viewerUrl = new URL('/scripts/class-models/model-turntable-review.html', baseUrl);
const captureSize = 512;
const runtimeSize = 192;
const frontBandPixels = 6;

if (
  audit.status !== 'owner-review' ||
  audit.classes?.length !== 16 ||
  new Set(audit.classes.map((entry) => entry.classId)).size !== 16
)
  throw new Error('The approved Meshy V3 icon source audit must contain sixteen classes.');
mkdirSync(captureDirectory, { recursive: true });

const records = [];
if (!check) {
  const browser = await chromium.launch({ headless: true });
  try {
    for (const entry of audit.classes) records.push(await renderIcon(browser, entry));
  } finally {
    await browser.close();
  }
  await buildSheet(records);
  writeFileSync(
    evidencePath,
    `${JSON.stringify({
      schemaVersion: 1,
      status: 'owner-approved-runtime-icon-tier',
      generator: 'scripts/class-models/build-meshy7-fleet-web-icons.mjs',
      sourceAudit: path.relative(repository, auditPath),
      contract: {
        projection: 'strict orthographic top-down',
        facing: '+X maps to screen-right/east',
        up: '+Y',
        transparentBackground: true,
        runtimePixels: { width: runtimeSize, height: runtimeSize },
        teamAccent:
          'renderer-owned narrow leading-edge mask derived from each east-facing silhouette',
        frontBandPixels,
      },
      sheet: {
        file: path.relative(repository, sheetPath),
        sha256: sha256(readFileSync(sheetPath)),
      },
      icons: records,
    }, null, 2)}\n`,
  );
} else {
  const recorded = JSON.parse(readFileSync(evidencePath, 'utf8'));
  if (recorded.icons?.length !== 16) throw new Error('Icon evidence must contain sixteen classes.');
  for (const record of recorded.icons) validateRecordedIcon(record);
  if (sha256(readFileSync(sheetPath)) !== recorded.sheet.sha256)
    throw new Error('Top-down icon sheet drifted.');
}

console.log(`${check ? 'Verified' : 'Built'} 16 top-down east-facing Arc Relay web icons.`);

async function renderIcon(browser, entry) {
  const sourcePath = path.join(repository, entry.artifact.file);
  const sourceBytes = readFileSync(sourcePath);
  if (sourceBytes.length !== entry.artifact.bytes || sha256(sourceBytes) !== entry.artifact.sha256)
    throw new Error(`${entry.classId}: reviewed icon source drifted.`);
  const context = await browser.newContext({
    viewport: { width: captureSize, height: captureSize },
    deviceScaleFactor: 1,
  });
  const page = await context.newPage();
  const pageErrors = [];
  const failedRequests = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('requestfailed', (request) =>
    failedRequests.push(`${request.method()} ${request.url()} ${request.failure()?.errorText ?? ''}`),
  );
  const url = new URL(viewerUrl);
  url.searchParams.set('model', `/${entry.artifact.file}`);
  url.searchParams.set('view', 'top-down');
  url.searchParams.set('transparent', '1');
  await page.goto(url.toString(), { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => window.__modelTurntableReady === true, null, {
    timeout: 60_000,
  });
  if (pageErrors.length || failedRequests.length)
    throw new Error(`${entry.classId}: icon render errors ${[...pageErrors, ...failedRequests].join('; ')}`);
  const capturePath = path.join(captureDirectory, `${entry.classId}-512.png`);
  const dataUrl = await page.locator('canvas').evaluate((canvas) => canvas.toDataURL('image/png'));
  const match = dataUrl.match(/^data:image\/png;base64,(.+)$/);
  if (!match) throw new Error(`${entry.classId}: canvas did not return a PNG data URL.`);
  writeFileSync(capturePath, Buffer.from(match[1], 'base64'));
  await context.close();

  const runtimePath = path.join(webLooksDirectory, `arc-${entry.classId}`, 'sprite.png');
  const runtimeBytes = await sharp(capturePath)
    .resize(runtimeSize, runtimeSize, { fit: 'fill' })
    .png({ compressionLevel: 9, adaptiveFiltering: true })
    .toBuffer();
  const { data, info } = await sharp(runtimeBytes)
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const bounds = alphaBounds(data, info);
  if (
    !bounds ||
    bounds.width < 48 ||
    bounds.height < 32 ||
    bounds.width > 180 ||
    bounds.height > 180
  )
    throw new Error(`${entry.classId}: invalid runtime icon alpha bounds.`);
  const { bytes: maskBytes, accentedPixels } = await leadingEdgeMask(data, info);
  const opaquePixels = alphaPixelCount(data, info);
  if (accentedPixels < opaquePixels * 0.006 || accentedPixels > opaquePixels * 0.14)
    throw new Error(`${entry.classId}: leading-edge team mask is not restrained.`);
  const maskPath = path.join(webLooksDirectory, `arc-${entry.classId}`, 'team-mask.png');
  writeFileSync(runtimePath, runtimeBytes);
  writeFileSync(maskPath, maskBytes);
  const teamTemplatePath = path.join(webLooksDirectory, `arc-${entry.classId}`, 'team.svg');
  const teamTemplate = readFileSync(teamTemplatePath, 'utf8').replace(
    /aria-label="[^"]*"/,
    `aria-label="${title(entry.classId)} facing East in a strict top-down projection"`,
  );
  writeFileSync(teamTemplatePath, teamTemplate);
  return {
    classId: entry.classId,
    source: {
      file: entry.artifact.file,
      bytes: sourceBytes.length,
      sha256: sha256(sourceBytes),
      geometrySha256: entry.geometrySha256,
    },
    camera: 'strict top-down; camera up -Z; +X maps screen-right/east',
    capture: {
      file: path.relative(repository, capturePath),
      sha256: sha256(readFileSync(capturePath)),
    },
    sprite: {
      file: path.relative(repository, runtimePath),
      bytes: runtimeBytes.length,
      sha256: sha256(runtimeBytes),
      width: runtimeSize,
      height: runtimeSize,
      alphaBounds: bounds,
      opaquePixels,
    },
    teamMask: {
      file: path.relative(repository, maskPath),
      bytes: maskBytes.length,
      sha256: sha256(maskBytes),
      frontBandPixels,
      accentedPixels,
    },
  };
}

function validateRecordedIcon(record) {
  for (const artifact of [record.source, record.capture, record.sprite, record.teamMask]) {
    const bytes = readFileSync(path.join(repository, artifact.file));
    if (artifact.bytes !== undefined && bytes.length !== artifact.bytes)
      throw new Error(`${record.classId}: ${artifact.file} byte count drifted.`);
    if (sha256(bytes) !== artifact.sha256)
      throw new Error(`${record.classId}: ${artifact.file} hash drifted.`);
  }
  if (
    record.sprite.width !== runtimeSize ||
    record.sprite.height !== runtimeSize ||
    record.teamMask.frontBandPixels !== frontBandPixels
  )
    throw new Error(`${record.classId}: runtime icon contract drifted.`);
}

function alphaBounds(data, info) {
  let minimumX = info.width;
  let minimumY = info.height;
  let maximumX = -1;
  let maximumY = -1;
  for (let y = 0; y < info.height; y += 1)
    for (let x = 0; x < info.width; x += 1) {
      const alpha = data[(y * info.width + x) * info.channels + 3];
      if (alpha < 16) continue;
      minimumX = Math.min(minimumX, x);
      minimumY = Math.min(minimumY, y);
      maximumX = Math.max(maximumX, x);
      maximumY = Math.max(maximumY, y);
    }
  if (maximumX < minimumX || maximumY < minimumY) return null;
  return {
    left: minimumX,
    top: minimumY,
    width: maximumX - minimumX + 1,
    height: maximumY - minimumY + 1,
  };
}

function alphaPixelCount(data, info) {
  let count = 0;
  for (let offset = 3; offset < data.length; offset += info.channels)
    if (data[offset] >= 16) count += 1;
  return count;
}

async function leadingEdgeMask(source, info) {
  const mask = Buffer.alloc(info.width * info.height * 4);
  let accentedPixels = 0;
  for (let y = 0; y < info.height; y += 1) {
    let leadingX = -1;
    for (let x = info.width - 1; x >= 0; x -= 1) {
      const alpha = source[(y * info.width + x) * info.channels + 3];
      if (alpha >= 32) {
        leadingX = x;
        break;
      }
    }
    if (leadingX < 0) continue;
    for (let x = Math.max(0, leadingX - frontBandPixels + 1); x <= leadingX; x += 1) {
      const sourceAlpha = source[(y * info.width + x) * info.channels + 3];
      if (sourceAlpha < 16) continue;
      const offset = (y * info.width + x) * 4;
      mask[offset] = 255;
      mask[offset + 1] = 255;
      mask[offset + 2] = 255;
      mask[offset + 3] = Math.min(255, Math.round(sourceAlpha * 0.9));
      accentedPixels += 1;
    }
  }
  const bytes = await sharp(mask, {
    raw: { width: info.width, height: info.height, channels: 4 },
  })
    .png({ compressionLevel: 9, adaptiveFiltering: true, palette: true })
    .toBuffer();
  return { bytes, accentedPixels };
}

async function buildSheet(iconRecords) {
  const tileSize = 240;
  const labelHeight = 32;
  const titleHeight = 68;
  const gap = 4;
  const width = tileSize * 4 + gap * 3;
  const height = titleHeight + (tileSize + labelHeight + gap) * 4 - gap;
  const composites = [
    {
      input: Buffer.from(
        `<svg width="${width}" height="${titleHeight}" xmlns="http://www.w3.org/2000/svg">` +
          '<rect width="100%" height="100%" fill="#080d12"/>' +
          '<text x="14" y="28" fill="#e8eef2" font-size="19" font-family="monospace" font-weight="700">' +
          'ARC RELAY · RUNTIME ICONS · TOP-DOWN EAST</text>' +
          '<text x="14" y="51" fill="#9fb0bb" font-size="13" font-family="monospace">' +
          'strict orthographic camera · transparent 192px assets · +X points right</text></svg>',
      ),
      left: 0,
      top: 0,
    },
  ];
  for (const [index, record] of iconRecords.entries()) {
    const column = index % 4;
    const row = Math.floor(index / 4);
    const left = column * (tileSize + gap);
    const top = titleHeight + row * (tileSize + labelHeight + gap);
    composites.push({
      input: Buffer.from(
        `<svg width="${tileSize}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg">` +
          '<rect width="100%" height="100%" fill="#10151a"/>' +
          `<text x="9" y="22" fill="#dbe7ef" font-size="15" font-family="monospace" font-weight="700">${record.classId.toUpperCase()}</text>` +
          '<text x="230" y="22" text-anchor="end" fill="#81939f" font-size="12" font-family="monospace">EAST →</text></svg>',
      ),
      left,
      top,
    });
    composites.push({
      input: await sharp(path.join(repository, record.sprite.file))
        .resize(216, 216)
        .png()
        .toBuffer(),
      left: left + 12,
      top: top + labelHeight + 12,
    });
  }
  await sharp({ create: { width, height, channels: 4, background: '#17191c' } })
    .composite(composites)
    .png()
    .toFile(sheetPath);
}

function title(value) {
  return value
    .split('-')
    .map((word) => word[0].toUpperCase() + word.slice(1))
    .join(' ');
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}

function option(name, fallback) {
  const index = process.argv.indexOf(name);
  if (index === -1) return fallback;
  const value = process.argv[index + 1];
  if (!value || value.startsWith('--')) throw new Error(`${name} requires a value.`);
  return value;
}
