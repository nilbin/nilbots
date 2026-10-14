#!/usr/bin/env node
'use strict';

const crypto = require('node:crypto');
const fs = require('node:fs/promises');
const path = require('node:path');

const packagePath = process.argv[2];
const root = process.argv[3]
  ? path.resolve(process.argv[3])
  : __dirname;
if (!packagePath || !path.isAbsolute(packagePath))
  throw new Error(
    'usage: node khronos_validate_aegis_forms.cjs <absolute gltf-validator package> [forms root]',
  );
const validator = require(packagePath);

const assets = [
  'aegis-tortoise-shell',
  'aegis-tortoise-turret',
];

async function main() {
  const reports = [];
  for (const assetId of assets) {
    const file = path.join(root, 'candidate', `${assetId}.glb`);
    const bytes = await fs.readFile(file);
    const validation = await validator.validateBytes(new Uint8Array(bytes), {
      uri: path.basename(file),
      format: 'glb',
      maxIssues: 0,
      writeTimestamp: false,
    });
    reports.push({
      assetId,
      file: {
        name: path.basename(file),
        bytes: bytes.length,
        sha256: crypto.createHash('sha256').update(bytes).digest('hex'),
      },
      verdict:
        validation.issues.numErrors === 0 &&
        validation.issues.numWarnings === 0
          ? 'pass'
          : 'fail',
      validation,
    });
  }
  const report = {
    schemaVersion: 1,
    validatorVersion: validator.version(),
    verdict: reports.every(({ verdict }) => verdict === 'pass')
      ? 'pass'
      : 'fail',
    assets: reports,
  };
  const output = path.join(root, 'reports', 'aegis-forms-khronos-validation.json');
  await fs.mkdir(path.dirname(output), { recursive: true });
  await fs.writeFile(output, `${JSON.stringify(report, null, 2)}\n`);
  process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
  if (report.verdict !== 'pass') process.exitCode = 1;
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
