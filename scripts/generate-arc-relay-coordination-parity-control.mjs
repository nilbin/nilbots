#!/usr/bin/env node

import { createHash } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const sourcePath = path.join(
  root,
  "arena-bots/arc-relay/forward-combat-operation-proof-v1-2026-08-03/sheets/baseline.json",
);
const outputPath = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/controls/coordination-parity-baseline.json",
);
const source = await readFile(sourcePath);
const document = JSON.parse(source);

document.sheetId = "baseline-coordination-parity-v1";
document.auditDimensions = {
  ...document.auditDimensions,
  attackCoordination: "shared damage budget and deterministic overkill cap",
  controlPurpose:
    "same stock doctrine with attack coordination parity; not a Home Siege counter",
};
document.dynamicStrategyAudit = {
  ...document.dynamicStrategyAudit,
  derivedFromSha256: createHash("sha256").update(source).digest("hex"),
  coordinationParityControl: true,
};
document.attackCoordination = {
  mode: "shared-damage-budget",
  targetPriorities: ["enemy-carrier", "lowest-health", "nearest"],
  tieBreakers: ["health", "distance", "actor-id"],
  maximumAttackersPerTarget: 5,
  overkillDamage: 0,
  lockTicks: 3,
};

await mkdir(path.dirname(outputPath), { recursive: true });
await writeFile(outputPath, `${JSON.stringify(document, null, 2)}\n`);
console.log(path.relative(root, outputPath));
