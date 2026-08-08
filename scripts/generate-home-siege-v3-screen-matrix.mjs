#!/usr/bin/env node

import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";

const [baseArgument, outputArgument] = process.argv.slice(2);
if (!baseArgument || !outputArgument) {
  throw new Error("usage: generate-home-siege-v3-screen-matrix.mjs <base.json> <output-dir>");
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
const denyTask = (document) => document.coordination.tasks.find(
  (task) => task.taskId === "deny-visible-carrier",
);
const candidates = [];

for (const movement of [
  { kind: "enemy-carrier", lead: 0 },
  { kind: "enemy-carrier-cutoff", lead: 1 },
  { kind: "enemy-carrier-cutoff", lead: 2 },
  { kind: "enemy-carrier-cutoff", lead: 3 },
]) {
  for (const leash of [4, 5, 6, 7]) {
    for (const timeout of [12, 18, 24, 30]) {
      for (const classes of [
        ["repulsor", "sunder"],
        ["sunder", "repulsor"],
      ]) {
        const document = structuredClone(base);
        const authored = document.authoring.maneuvers["task-deny"]
          .tracks.interceptor;
        authored.movement.kind = movement.kind;
        if (movement.lead > 0) {
          authored.movement.leadTiles = movement.lead;
        } else {
          delete authored.movement.leadTiles;
        }
        authored.assignments["line-task-deny"].chaseLeash = leash;
        const task = denyTask(document);
        task.timeoutTicks = timeout;
        task.assignments[0].classes = classes;
        const id = [
          movement.lead > 0 ? `cut${movement.lead}` : "direct",
          `l${leash}`,
          `t${timeout}`,
          classes[0],
        ].join("-");
        const bytes = `${JSON.stringify(document, null, 2)}\n`;
        const file = path.join(playbooks, `${id}.json`);
        fs.writeFileSync(file, bytes);
        candidates.push({
          id,
          file,
          sha256: crypto.createHash("sha256").update(bytes).digest("hex"),
          movement: movement.kind,
          leadTiles: movement.lead,
          chaseLeash: leash,
          timeoutTicks: timeout,
          preferredClass: classes[0],
        });
      }
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
    seed: "202608060",
  })),
});

fs.writeFileSync(
  path.join(output, "candidates.json"),
  `${JSON.stringify({ schema: "home-siege-v3-semantic-matrix-v1", candidates }, null, 2)}\n`,
);
for (const assignment of [0, 1]) {
  fs.writeFileSync(
    path.join(output, `plan-a${assignment}.json`),
    `${JSON.stringify(makePlan(assignment), null, 2)}\n`,
  );
}
console.log(`Generated ${candidates.length} semantic candidates in ${output}`);
