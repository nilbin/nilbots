#!/usr/bin/env node

import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";

const [baseArgument, outputArgument] = process.argv.slice(2);
if (!baseArgument || !outputArgument) {
  throw new Error("usage: generate-home-siege-v3-focus-matrix.mjs <base.json> <output-dir>");
}

const root = process.cwd();
const basePath = path.resolve(baseArgument);
const output = path.resolve(outputArgument);
const playbooks = path.join(output, "playbooks");
const layouts = path.join(output, "layouts");
fs.mkdirSync(playbooks, { recursive: true });
fs.rmSync(layouts, { force: true, recursive: true });
fs.symlinkSync(
  path.join(root, "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/layouts"),
  layouts,
  "dir",
);

const base = JSON.parse(fs.readFileSync(basePath, "utf8"));
const baseline = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/controls/coordination-parity-baseline.json",
);
const tactical = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-mind-v1",
);
const stock = path.join(root, "arena-bots/arc-relay/stock-mind-v4");
const candidates = [];

for (const horizonTicks of [1, 2, 3, 4]) {
  for (const minimumDirectShots of [1, 2, 3]) {
    for (const minimumCoveredOptions of [3, 5, 7]) {
      const document = structuredClone(base);
      const policy = document.engagements.find(
        (engagement) => engagement.engagementId === "siege-focus",
      );
      policy.dodgeCoverage.horizonTicks = horizonTicks;
      policy.dodgeCoverage.minimumDirectShots = minimumDirectShots;
      policy.dodgeCoverage.minimumCoveredOptions = minimumCoveredOptions;
      const id = `h${horizonTicks}-d${minimumDirectShots}-c${minimumCoveredOptions}`;
      const bytes = `${JSON.stringify(document, null, 2)}\n`;
      const file = path.join(playbooks, `${id}.json`);
      fs.writeFileSync(file, bytes);
      candidates.push({
        id,
        file,
        sha256: crypto.createHash("sha256").update(bytes).digest("hex"),
        horizonTicks,
        minimumDirectShots,
        minimumCoveredOptions,
      });
    }
  }
}

const makePlan = (assignment) => ({
  schema: "arc-relay-screen-batch-v1",
  bot: assignment === 0 ? tactical : stock,
  opponent: assignment === 0 ? stock : tactical,
  loopProfile: "forward-combat",
  cells: candidates.map((candidate) => ({
    cellId: candidate.id,
    sheet0: assignment === 0 ? candidate.file : baseline,
    sheet1: assignment === 0 ? baseline : candidate.file,
    seed: "202608062",
  })),
});

fs.writeFileSync(
  path.join(output, "candidates.json"),
  `${JSON.stringify({ schema: "home-siege-v3-focus-matrix-v1", candidates }, null, 2)}\n`,
);
for (const assignment of [0, 1]) {
  fs.writeFileSync(
    path.join(output, `plan-a${assignment}.json`),
    `${JSON.stringify(makePlan(assignment), null, 2)}\n`,
  );
}
console.log(`Generated ${candidates.length} focus candidates in ${output}`);
