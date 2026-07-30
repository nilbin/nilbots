import { readFile, writeFile } from "node:fs/promises";
import { createRequire } from "node:module";
import { join, resolve } from "node:path";

const repoRoot = resolve(import.meta.dirname, "../../..");
const requireFromWeb = createRequire(join(repoRoot, "web/package.json"));
const sharp = requireFromWeb("sharp");

const reportPath = join(
  repoRoot,
  "art/class-models/provider-inputs/projectile-clean-input-report.json",
);
const report = JSON.parse(await readFile(reportPath, "utf8"));
const flat = { r: 184, g: 184, b: 184 };
const minimumTopIou = 0.99;
const validation = {
  schemaVersion: 1,
  sourceReport:
    "art/class-models/provider-inputs/projectile-clean-input-report.json",
  minimumTopIou,
  packages: [],
  passed: true,
};

async function authorityAlpha(authorityPath, view) {
  const rendered = await sharp(authorityPath, { density: 600 })
    .resize(1536, 1536, { fit: "fill" })
    .ensureAlpha()
    .png()
    .toBuffer();
  const { data, info } = await sharp(rendered)
    .raw()
    .toBuffer({ resolveWithObject: true });
  let minX = info.width;
  let minY = info.height;
  let maxX = -1;
  let maxY = -1;
  for (let y = 0; y < info.height; y += 1) {
    for (let x = 0; x < info.width; x += 1) {
      const alpha = data[(y * info.width + x) * info.channels + 3];
      if (alpha <= 8) continue;
      minX = Math.min(minX, x);
      minY = Math.min(minY, y);
      maxX = Math.max(maxX, x);
      maxY = Math.max(maxY, y);
    }
  }
  const cropped = await sharp(rendered)
    .extract({
      left: minX,
      top: minY,
      width: maxX - minX + 1,
      height: maxY - minY + 1,
    })
    .resize(view.objectWidth, view.objectHeight, {
      fit: "fill",
      kernel: sharp.kernel.lanczos3,
    })
    .extractChannel("alpha")
    .raw()
    .toBuffer();
  const canvas = Buffer.alloc(report.canvasSize * report.canvasSize);
  for (let y = 0; y < view.objectHeight; y += 1) {
    for (let x = 0; x < view.objectWidth; x += 1) {
      canvas[
        (view.objectTop + y) * report.canvasSize + view.objectLeft + x
      ] = cropped[y * view.objectWidth + x];
    }
  }
  return canvas;
}

for (const packageReport of report.packages) {
  const packageValidation = {
    id: packageReport.id,
    views: [],
    passed: true,
  };
  for (const view of packageReport.views) {
    const path = join(
      repoRoot,
      "art/class-models/provider-inputs/meshy",
      packageReport.id,
      "clean-v1",
      view.file,
    );
    const { data, info } = await sharp(path)
      .removeAlpha()
      .raw()
      .toBuffer({ resolveWithObject: true });
    const metadata = await sharp(path).metadata();
    let greenSpillPixels = 0;
    let backgroundViolations = 0;
    let exactBackgroundPixels = 0;
    let checkedBackgroundPixels = 0;
    let authority = null;
    if (view.role === "top") {
      authority = await authorityAlpha(
        join(repoRoot, packageReport.topAuthority),
        view,
      );
    }

    for (let y = 0; y < info.height; y += 1) {
      for (let x = 0; x < info.width; x += 1) {
        const index = y * info.width + x;
        const offset = index * info.channels;
        const r = data[offset];
        const g = data[offset + 1];
        const b = data[offset + 2];
        if (g - r > 15 && g - b > 15) greenSpillPixels += 1;
        const exactBackground = r === flat.r && g === flat.g && b === flat.b;
        const outsideObjectRectangle =
          x < view.objectLeft ||
          x >= view.objectLeft + view.objectWidth ||
          y < view.objectTop ||
          y >= view.objectTop + view.objectHeight;
        let outsideTopAuthority = authority !== null && authority[index] === 0;
        // Do not call the two-pixel antialias transition "background." Every
        // pixel beyond that transition, including aperture interiors, must be
        // exact #b8b8b8.
        if (outsideTopAuthority) {
          for (let dy = -2; dy <= 2 && outsideTopAuthority; dy += 1) {
            for (let dx = -2; dx <= 2; dx += 1) {
              const nx = x + dx;
              const ny = y + dy;
              if (
                nx >= 0 &&
                nx < info.width &&
                ny >= 0 &&
                ny < info.height &&
                authority[ny * info.width + nx] > 0
              ) {
                outsideTopAuthority = false;
                break;
              }
            }
          }
        }
        if (outsideObjectRectangle || outsideTopAuthority) {
          checkedBackgroundPixels += 1;
          if (exactBackground) exactBackgroundPixels += 1;
          else backgroundViolations += 1;
        }
      }
    }

    const topIou = view.alignment?.silhouetteIou ?? null;
    const viewPassed =
      metadata.width === report.canvasSize &&
      metadata.height === report.canvasSize &&
      metadata.hasAlpha !== true &&
      greenSpillPixels === 0 &&
      backgroundViolations === 0 &&
      view.objectWidth === report.objectExtent &&
      view.objectLeft >= 100 &&
      view.objectTop >= 100 &&
      (topIou === null || topIou >= minimumTopIou);
    packageValidation.views.push({
      role: view.role,
      path: `art/class-models/provider-inputs/meshy/${packageReport.id}/clean-v1/${view.file}`,
      width: metadata.width,
      height: metadata.height,
      hasAlpha: metadata.hasAlpha === true,
      objectWidth: view.objectWidth,
      objectHeight: view.objectHeight,
      objectLeft: view.objectLeft,
      objectTop: view.objectTop,
      greenSpillPixels,
      checkedBackgroundPixels,
      exactBackgroundPixels,
      backgroundViolations,
      topSilhouetteIou: topIou,
      passed: viewPassed,
    });
    if (!viewPassed) packageValidation.passed = false;
  }
  validation.packages.push(packageValidation);
  if (!packageValidation.passed) validation.passed = false;
}

await writeFile(
  join(
    repoRoot,
    "art/class-models/provider-inputs/projectile-input-validation.json",
  ),
  `${JSON.stringify(validation, null, 2)}\n`,
);

for (const packageValidation of validation.packages) {
  console.log(
    `${packageValidation.passed ? "PASS" : "FAIL"} ${packageValidation.id}`,
  );
  for (const view of packageValidation.views) {
    const top =
      view.topSilhouetteIou === null
        ? ""
        : ` topIoU=${(view.topSilhouetteIou * 100).toFixed(2)}%`;
    console.log(
      `  ${view.passed ? "PASS" : "FAIL"} ${view.role}: ` +
        `${view.width}x${view.height}, object=${view.objectWidth}x${view.objectHeight}, ` +
        `backgroundViolations=${view.backgroundViolations}, ` +
        `greenSpillPixels=${view.greenSpillPixels}${top}`,
    );
  }
}

if (!validation.passed) process.exitCode = 1;
