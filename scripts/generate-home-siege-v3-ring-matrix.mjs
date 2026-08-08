#!/usr/bin/env node

import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";

const [baseArgument, outputArgument] = process.argv.slice(2);
if (!baseArgument || !outputArgument) {
  throw new Error(
    "usage: generate-home-siege-v3-ring-matrix.mjs <base.json> <output-dir>",
  );
}

const root = process.cwd();
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
const base = JSON.parse(fs.readFileSync(path.resolve(baseArgument), "utf8"));
const layoutPath = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/layouts/counterflow-home-siege-v3.json",
);
const layoutSha256 = crypto.createHash("sha256")
  .update(fs.readFileSync(layoutPath))
  .digest("hex");
const baseline = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/controls/coordination-parity-baseline.json",
);
const tactical = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-mind-v1",
);
const stock = path.join(root, "arena-bots/arc-relay/stock-mind-v4");
const variants = [
  ["current", [[0, -2], [0, -1], [0, 1], [0, 2], [0, 0]], -1],
  ["nose", [[0, -2], [0, -1], [0, 1], [0, 2], [1, 0]], -1],
  ["inner-three", [[0, -2], [1, -1], [1, 1], [0, 2], [1, 0]], -1],
  ["line-forward", [[1, -2], [1, -1], [1, 1], [1, 2], [1, 0]], -1],
  ["inner-three-medics", [[0, -2], [1, -1], [1, 1], [0, 2], [1, 0]], 0],
  ["all-forward", [[1, -2], [1, -1], [1, 1], [1, 2], [1, 0]], 0],
];
const candidates = variants.map(([id, lineOffsets, medicX]) => {
  const document = structuredClone(base);
  document.layout.sha256 = layoutSha256;
  const ring = document.formations.find(
    (formation) => formation.formationId === "living-ring",
  );
  const lineBands = ring.placementBands.filter(
    (band) => band.roleId === "line",
  );
  lineBands[0].offsets = lineOffsets.slice(0, 2);
  lineBands[1].offsets = lineOffsets.slice(2, 4);
  lineBands[2].offsets = lineOffsets.slice(4);
  for (const band of ring.placementBands.filter(
    (candidate) => candidate.roleId === "medic",
  )) {
    band.offsets = band.offsets.map(([, y]) => [medicX, y]);
  }
  const bytes = `${JSON.stringify(document, null, 2)}\n`;
  const file = path.join(playbooks, `${id}.json`);
  fs.writeFileSync(file, bytes);
  return {
    id,
    file,
    lineOffsets,
    medicX,
    sha256: crypto.createHash("sha256").update(bytes).digest("hex"),
  };
});
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
  `${JSON.stringify({ schema: "home-siege-v3-ring-matrix-v1", candidates }, null, 2)}\n`,
);
for (const assignment of [0, 1]) {
  fs.writeFileSync(
    path.join(output, `plan-a${assignment}.json`),
    `${JSON.stringify(makePlan(assignment), null, 2)}\n`,
  );
}
console.log(`Generated ${candidates.length} living-ring candidates.`);
