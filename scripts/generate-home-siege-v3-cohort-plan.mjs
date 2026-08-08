#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";

const [sheetArgument, outputArgument, ...seedArguments] = process.argv.slice(2);
if (!sheetArgument || !outputArgument || seedArguments.length === 0) {
  throw new Error(
    "usage: generate-home-siege-v3-cohort-plan.mjs "
      + "<sheet.json> <output-dir> <seed> [seed ...]",
  );
}

const root = process.cwd();
const sheet = path.resolve(sheetArgument);
const baseline = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/controls/coordination-parity-baseline.json",
);
const tactical = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-mind-v1",
);
const stock = path.join(root, "arena-bots/arc-relay/stock-mind-v4");
const output = path.resolve(outputArgument);
fs.mkdirSync(output, { recursive: true });
for (const assignment of [0, 1]) {
  const cells = seedArguments.map((seed, index) => ({
    cellId: `seed-${String(index + 1).padStart(2, "0")}-a${assignment}`,
    sheet0: assignment === 0 ? sheet : baseline,
    sheet1: assignment === 0 ? baseline : sheet,
    seed,
  }));
  const plan = {
    schema: "arc-relay-screen-batch-v1",
    bot: assignment === 0 ? tactical : stock,
    opponent: assignment === 0 ? stock : tactical,
    loopProfile: "forward-combat",
    cells,
  };
  fs.writeFileSync(
    path.join(output, `plan-a${assignment}.json`),
    `${JSON.stringify(plan, null, 2)}\n`,
  );
}
console.log(`Generated ${seedArguments.length * 2} Home Siege cohort cells.`);
